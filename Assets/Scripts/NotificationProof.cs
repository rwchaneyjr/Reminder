using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif

namespace RememberThis
{
    // A deliberately small, single-scene device test before building reminder storage.
    public sealed class NotificationProof : MonoBehaviour
    {
        private const string Channel = "remember_this_test";
        private ReminderStore store;
        private string storePath;
        private Transform reminderList;
        private ScrollRect pageScroll;
        private int editingId;
        private bool customSnooze;
        private int deleteConfirmId;
        private string listSignature;
        private Button cancelEdit;
        private Text status;
        private Button schedule;
        private bool busy;
        private bool voicePending;
        private Text voiceStatus;

        private OfflineVoice offlineVoice;
        private Button voiceButton;
        private Button cancelVoice;
        private DateTime spokenAt;
        private bool voiceForTime;
        private Button timeVoiceButton;
        private InputField whenInput;
        private GameObject voiceDialog;
        private Text voicePrompt;
        private Text voiceHeard;
        private Button voiceNext;
        private Button dialogRecord;
        private bool dialogTime;
        private Button returnToVoice;
        private GameObject reminderMenu;
        private Text menuPreview;
        private bool lastSaveSucceeded;
        private GameObject homeSection, editSection, calendarSection, formActions, savedSection;
        private Button backHome;

        private void ShowPage(string page)
        {
            if (busy || homeSection == null) return;
            homeSection.SetActive(page == "home");
            editSection.SetActive(page == "edit");
            calendarSection.SetActive(page == "calendar" || page == "edit");
            formActions.SetActive(page == "calendar" || page == "edit");
            savedSection.SetActive(page == "saved");
            backHome.gameObject.SetActive(page != "home");
            Canvas.ForceUpdateCanvases();
            pageScroll.verticalNormalizedPosition = 1;
        }

        private static GameObject MakeSection(Transform parent, string name)
        {
            var section = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            section.transform.SetParent(parent, false);
            var layout = section.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return section;
        }
        private GameObject dueDialog;
        private Text dueMessage;
        private SavedReminder visibleDueReminder;
        private AudioSource reminderAudio;
        private AudioClip reminderChime;
        private int soundedReminderId;
        private long soundedReminderTicks;

        private void PlayReminderChime(SavedReminder reminder)
        {
            if (soundedReminderId == reminder.id && soundedReminderTicks == reminder.utcTicks) return;
            soundedReminderId = reminder.id;
            soundedReminderTicks = reminder.utcTicks;
            if (reminderAudio == null)
            {
                reminderAudio = gameObject.AddComponent<AudioSource>();
                reminderAudio.playOnAwake = false;
                reminderAudio.spatialBlend = 0;
                reminderAudio.volume = 0.65f;
                if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
                const int rate = 22050;
                var samples = new float[rate * 2];
                var notes = new[] { 523.25f, 659.25f, 783.99f };
                for (int note = 0; note < notes.Length; note++)
                {
                    int offset = (int)(note * 0.45f * rate);
                    for (int i = 0; i < (int)(0.65f * rate); i++)
                    {
                        float t = i / (float)rate;
                        float envelope = Mathf.Min(t / 0.015f, 1f) * Mathf.Exp(-6f * t)
                            * Mathf.Clamp01((0.65f - t) / 0.05f);
                        samples[offset + i] += 0.35f * envelope * Mathf.Sin(2f * Mathf.PI * notes[note] * t);
                    }
                }
                reminderChime = AudioClip.Create("Reminder chime", samples.Length, 1, rate, false);
                reminderChime.SetData(samples, 0);
                reminderAudio.clip = reminderChime;
            }
            reminderAudio.Play();
        }

        private void OnDestroy()
        {
            if (reminderAudio != null) reminderAudio.Stop();
            if (reminderChime != null) Destroy(reminderChime);
        }

        private void ShowDueReminder()
        {
            if (store == null || dueDialog == null || busy || customSnooze) return;
            var due = store.reminders.Where(r => !r.deleted && !r.completed && r.utcTicks <= DateTime.UtcNow.Ticks)
                .OrderBy(r => r.utcTicks).ThenBy(r => r.id).FirstOrDefault();
            visibleDueReminder = due;
            dueDialog.SetActive(due != null);
            if (due == null) return;
            dueMessage.text = due.text + "\n\n" + due.LocalTime.ToString("MMM d, yyyy h:mm:ss tt");
            dueDialog.transform.SetAsLastSibling();
            PlayReminderChime(due);
        }

        private void RespondToDueReminder(int snoozeMinutes)
        {
            if (busy || visibleDueReminder == null) return;
            if (reminderAudio != null) reminderAudio.Stop();
            var reminder = visibleDueReminder;
            if (snoozeMinutes > 0) Snooze(reminder, DateTime.Now.AddMinutes(snoozeMinutes));
            else ChangeReminder(reminder, () => reminder.completed = true, "Alert canceled. Reminder completed.");
            ShowDueReminder();
        }

        private void OpenReminderMenu()
        {
            if (busy) return;
            voiceDialog.SetActive(false);
            menuPreview.text = "What: " + reminderInput.text + "\nWhen: "
                + (validTime ? selectedTime.ToString("MMM d, yyyy h:mm:ss tt") : "Choose a time")
                + "\n\nCheck the message and time. Confirm to save, cancel to discard this draft, or choose a correction below.";
            reminderMenu.SetActive(true);
        }

        private IEnumerator ConfirmFromMenu()
        {
            if (busy) yield break;
            yield return Schedule();
            menuPreview.text = status.text;
            if (lastSaveSucceeded) reminderMenu.SetActive(false);
        }

        private void CheckNotificationStatus()
        {
            if (busy) return;
            OpenReminderMenu();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var reminder = store?.reminders.Where(r => !r.deleted && !r.completed)
                    .OrderByDescending(r => r.id).FirstOrDefault();
                if (reminder == null)
                {
                    menuPreview.text = "No active saved reminder. Transcribing alone does not schedule one.";
                    return;
                }
                var notificationStatus = AndroidNotificationCenter.CheckScheduledNotificationStatus(reminder.id);
                menuPreview.text = reminder.text + "\nDue: " + reminder.LocalTime.ToString("MMM d, h:mm:ss tt")
                    + "\nAndroid: " + notificationStatus
                    + " | Exact timing: " + (AndroidNotificationCenter.UsingExactScheduling ? "on" : "off")
                    + "\n" + (notificationStatus == NotificationStatus.Delivered
                        ? "Android reports it in the notification drawer. Swipe down from the top."
                        : notificationStatus == NotificationStatus.Scheduled
                            ? "Still pending. Check the due time; approximate alarms may be delayed."
                            : "Not confirmed as pending or visible. Check app notification permissions; a dismissed alert can also appear missing.");
            }
            catch (Exception e) { menuPreview.text = "Could not check notifications: " + e.Message; }
#else
            menuPreview.text = "Notification status requires the Android APK. Unity Play mode only saves a preview.";
#endif
        }

        private void ViewCalendar()
        {
            if (busy) return;
            reminderMenu.SetActive(false);
            voiceDialog.SetActive(false);
            ShowPage("calendar");
            returnToVoice.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            pageScroll.verticalNormalizedPosition = 1;
        }

        private void OpenVoiceDialog(bool timeStep)
        {
            if (busy || customSnooze) return;
            reminderMenu.SetActive(false);
            dialogTime = timeStep;
            voiceDialog.SetActive(true);
            if (returnToVoice != null) returnToVoice.gameObject.SetActive(false);
            voicePrompt.text = timeStep ? "When should I remind you?" : "Speak your reminder";
            voiceHeard.text = timeStep ? "For example: in an hour, or tomorrow at 3 PM." : "For example: Call John in one hour. Then pause when you are finished.";
            voiceNext.GetComponentInChildren<Text>().text = "Review and confirm";
            voiceNext.interactable = false;
            voiceNext.GetComponent<Image>().color = Color.white;
            dialogRecord.GetComponentInChildren<Text>().text = "Speak reminder";
            RecordDialogAnswer();
        }

        private void RecordDialogAnswer()
        {
            if (!voicePending) voiceForTime = dialogTime;
            voiceNext.interactable = false;
            voiceNext.GetComponent<Image>().color = Color.white;
            StartVoice();
            dialogRecord.GetComponentInChildren<Text>().text = "Done speaking?";
        }

        private void StartTimeVoice()
        {
            if (voicePending) { StartVoice(); return; }
            if (busy || customSnooze) return;
            voiceForTime = true;
            StartVoice();
        }

        private void StartVoice()
        {
            if (voicePending)
            {
                offlineVoice.Stop();
                voiceStatus.text = "Preparing your reminder…";
                return;
            }
            if (busy || customSnooze)
            {
                voiceStatus.text = "Finish the current action before recording.";
                return;
            }
            if (offlineVoice == null)
            {
                offlineVoice = gameObject.AddComponent<OfflineVoice>();
                offlineVoice.Progress = message =>
                {
                    voiceStatus.text = message;
                    if (voiceHeard != null) voiceHeard.text = message;
                    bool canStop = !offlineVoice.IsProcessing;
                    voiceButton.interactable = timeVoiceButton.interactable = dialogRecord.interactable = canStop;
                    var label = canStop ? "Done speaking?" : "Preparing reminder…";
                    voiceButton.GetComponentInChildren<Text>().text = label;
                    timeVoiceButton.GetComponentInChildren<Text>().text = label;
                    dialogRecord.GetComponentInChildren<Text>().text = label;
                };
                offlineVoice.Finished = FinishVoice;
            }
            busy = voicePending = true;
            schedule.interactable = false;
            reminderInput.interactable = false;
            voiceButton.GetComponentInChildren<Text>().text = "Done speaking?";
            timeVoiceButton.GetComponentInChildren<Text>().text = "Done speaking?";
            cancelVoice.gameObject.SetActive(true);
            spokenAt = DateTime.Now;
            offlineVoice.Begin();
        }

        private void FinishVoice(string text, string error)
        {
            busy = voicePending = false;
            voiceButton.interactable = timeVoiceButton.interactable = dialogRecord.interactable = true;
            schedule.interactable = store != null;
            reminderInput.interactable = !customSnooze;
            voiceButton.GetComponentInChildren<Text>().text = "Speak reminder";
            timeVoiceButton.GetComponentInChildren<Text>().text = "Speak when";
            cancelVoice.gameObject.SetActive(false);
            if (text != null)
            {
                if (!voiceForTime)
                {
                    reminderInput.text = text.Length > 120 ? text.Substring(0, 120) : text;
                    if (ReminderCommandParser.TryParse(text, spokenAt, out var task, out var due))
                    {
                        reminderInput.text = task.Length > 120 ? task.Substring(0, 120) : task;
                        SetVoiceDue(due);
                    }
                    else
                    {
                        timeInput.text = "";
                        validTime = false;
                    }
                    voiceStatus.text = "What: " + reminderInput.text + "\nWhen: "
                        + (validTime ? selectedTime.ToString("MMM d, h:mm:ss tt") : "Choose or speak a time before confirming.");
                }
                else
                {
                    whenInput.SetTextWithoutNotify(text.Length > 120 ? text.Substring(0, 120) : text);
                    ApplyWhen(text, spokenAt);
                }
            }
            else voiceStatus.text = error;
            if (voiceDialog != null && voiceDialog.activeSelf)
            {
                voiceHeard.text = text == null ? error : "Heard: " + text + "\n" + voiceStatus.text;
                voiceNext.interactable = text != null;
                voiceNext.GetComponent<Image>().color = text != null ? new Color(0.78f, 0.95f, 0.81f) : Color.white;
                voiceNext.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
                dialogRecord.GetComponentInChildren<Text>().text = "Speak reminder";
            }
            voiceForTime = false;

        }

        private void ApplyWhen(string text, DateTime reference)
        {
            if (ReminderCommandParser.TryParseWhen(text, reference, selectedDate, out var due))
            {
                SetVoiceDue(due);
                voiceStatus.text = "When: " + due.ToString("MMM d, h:mm:ss tt") + "\nReview before saving.";
            }
            else
            {
                timeInput.text = "";
                validTime = false;
                voiceStatus.text = "Time unclear. Try 'in an hour' or 'tomorrow at 3 PM', or use the calendar and clock.";
            }
        }
        private void SetVoiceDue(DateTime due)
        {
            selectedDate = due.Date;
            calendarMonth = new DateTime(due.Year, due.Month, 1);
            isPm = due.Hour >= 12;
            periodLabel.text = isPm ? "PM" : "AM";
            timeInput.SetTextWithoutNotify(due.ToString("h:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
            UpdateChosenTime(timeInput.text);
            ShowCalendar();
        }
        private InputField timeInput;
        private InputField reminderInput;
        private Text chosenTime;
        private DateTime selectedTime;
        private DateTime selectedDate = DateTime.Today;
        private DateTime calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        private Transform calendar;

        private void ShowCalendar()
        {
            if (calendar == null) return;
            foreach (Transform child in calendar) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var navigation = ActionRow(calendar);
            MakeButton(navigation, "<", () => MoveMonth(-1));
            var month = Label(navigation, calendarMonth.ToString("MMMM yyyy"), 25, 80);
            month.alignment = TextAnchor.MiddleCenter;
            month.GetComponent<LayoutElement>().preferredWidth = 300;
            MakeButton(navigation, ">", () => MoveMonth(1));
            var weekdays = ActionRow(calendar);
            weekdays.GetComponent<LayoutElement>().preferredHeight = 40;
            foreach (var day in new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" })
            {
                var label = Label(weekdays, day, 22, 40);
                label.alignment = TextAnchor.MiddleCenter;
                label.GetComponent<LayoutElement>().preferredWidth = 0;
                label.GetComponent<LayoutElement>().flexibleWidth = 1;
            }
            int offset = (int)calendarMonth.DayOfWeek;
            int days = DateTime.DaysInMonth(calendarMonth.Year, calendarMonth.Month);
            for (int week = 0; week < (offset + days + 6) / 7; week++)
            {
                var row = ActionRow(calendar);
                row.GetComponent<LayoutElement>().preferredHeight = 65;
                for (int column = 0; column < 7; column++)
                {
                    int day = week * 7 + column - offset + 1;
                    if (day < 1 || day > days)
                    {
                        var blank = Label(row, "", 22, 65).GetComponent<LayoutElement>();
                        blank.preferredWidth = 0; blank.flexibleWidth = 1;
                        continue;
                    }
                    var date = calendarMonth.AddDays(day - 1);
                    var button = MakeButton(row, day.ToString(), () => SelectDate(date));
                    var element = button.GetComponent<LayoutElement>();
                    element.preferredWidth = 0; element.flexibleWidth = 1;
                    button.interactable = date >= DateTime.Today;
                    if (date == selectedDate) button.GetComponent<Image>().color = new Color(0.78f, 0.95f, 0.81f);
                }
            }
            MakeButton(calendar, "Today", () => SelectDate(DateTime.Today));
        }

        private void MoveMonth(int delta)
        {
            if (busy || (calendarMonth.Year == 1 && calendarMonth.Month == 1 && delta < 0) ||
                (calendarMonth.Year == 9999 && calendarMonth.Month == 12 && delta > 0)) return;
            calendarMonth = calendarMonth.AddMonths(delta);
            ShowCalendar();
        }

        private void SelectDate(DateTime date)
        {
            if (busy) return;
            selectedDate = date.Date;
            calendarMonth = new DateTime(date.Year, date.Month, 1);
            ShowCalendar();
            UpdateChosenTime(timeInput.text);
        }
        private bool validTime;
        private bool isPm;
        private Text periodLabel;
        private Text currentClock;
        private long displayedSecond = -1;

        private void Update()
        {
            if (dialogRecord != null)
            {
                var normal = Color.white;
                bool listening = voicePending && offlineVoice != null && offlineVoice.IsRecording;
                float pulse = listening ? (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.8f) + 1f) * 0.5f : 0f;
                dialogRecord.GetComponent<Image>().color = Color.Lerp(normal, new Color(1f, 0.88f, 0.48f), pulse);
            }
            if (currentClock == null) return;
            var now = DateTime.Now;
            var second = now.Ticks / TimeSpan.TicksPerSecond;
            if (second == displayedSecond) return;
            displayedSecond = second;
            currentClock.text = now.ToString("dddd, MMMM d, yyyy\nh:mm:ss tt",
                System.Globalization.CultureInfo.InvariantCulture);
            RefreshUpcoming();
            ShowDueReminder();
            HandleNotificationSnooze();
        }

        private void HandleNotificationSnooze()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (store == null || busy) return;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var actions = new AndroidJavaClass("com.unity.androidnotifications.ReminderNotificationActions"))
            {
                var token = actions.CallStatic<string>("peek", activity);
                if (string.IsNullOrEmpty(token)) return;
                try
                {
                    var parts = token.Split(':');
                    if (parts.Length == 4 && parts[0] == "remember-snooze" &&
                        int.TryParse(parts[1], out var id) && long.TryParse(parts[2], out var originalTicks))
                    {
                        var reminder = store.reminders.FirstOrDefault(r => r.id == id && !r.completed && !r.deleted && r.utcTicks == originalTicks);
                        if (reminder != null)
                        {
                            if (parts[3] == "snooze") Snooze(reminder, DateTime.Now.AddMinutes(10));
                            else if (parts[3] == "cancel") ChangeReminder(reminder, () => reminder.completed = true, "Alert canceled. Reminder completed.");
                        }
                    }
                }
                finally { actions.CallStatic("acknowledge", activity, token); }
            }
#endif
        }

        private void RefreshUpcoming()
        {
            if (reminderList == null || store == null) return;
            var text = new StringBuilder();
            foreach (var r in store.reminders.Where(r => !r.deleted))
                text.Append(r.id).Append(r.text).Append(r.utcTicks).Append(r.completed).Append(r.utcTicks <= DateTime.UtcNow.Ticks);
            text.Append(deleteConfirmId);
            if (listSignature == text.ToString()) return;
            listSignature = text.ToString();
            foreach (Transform child in reminderList) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (var group in new[] { "Upcoming", "Past due", "Completed" })
            {
                Label(reminderList, group, 30, 50);
                var items = store.reminders.Where(r => !r.deleted && (group == "Completed" ? r.completed :
                    !r.completed && (group == "Upcoming" ? r.utcTicks > DateTime.UtcNow.Ticks : r.utcTicks <= DateTime.UtcNow.Ticks)))
                    .OrderBy(r => r.utcTicks).ThenBy(r => r.id).ToArray();
                if (items.Length == 0) Label(reminderList, "None", 24, 40);
                foreach (var r in items)
                {
                    Label(reminderList, r.text + "\n" + r.LocalTime.ToString("MMM d, h:mm:ss tt"), 25, -1);
                    if (!r.completed)
                    {
                        var row = ActionRow(reminderList);
                        MakeButton(row, "Edit", () => BeginEdit(r));
                        MakeButton(row, "Complete", () => ChangeReminder(r, () => r.completed = true, "Reminder completed."));
                        Label(reminderList, "Snooze", 25, 40);
                        var snooze = ActionRow(reminderList);
                        MakeButton(snooze, "+10 min", () => Snooze(r, DateTime.Now.AddMinutes(10)));
                        MakeButton(snooze, "+1 hour", () => Snooze(r, DateTime.Now.AddHours(1)));
                        MakeButton(snooze, "Tomorrow", () => Snooze(r, DateTime.Now.AddDays(1)));
                        MakeButton(reminderList, "Custom...", () => BeginCustomSnooze(r));
                    }
                    if (deleteConfirmId == r.id)
                    {
                        Label(reminderList, "Delete this reminder?", 25, 45);
                        var row = ActionRow(reminderList);
                        MakeButton(row, "Delete", () => ChangeReminder(r, () => r.deleted = true, "Reminder deleted."));
                        MakeButton(row, "Keep", () => { deleteConfirmId = 0; RefreshUpcoming(); });
                    }
                    else MakeButton(reminderList, "Delete...", () => { if (busy) return; deleteConfirmId = r.id; RefreshUpcoming(); });
                }
            }
        }

        private static Transform ActionRow(Transform parent)
        {
            var row = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 80;
            row.GetComponent<HorizontalLayoutGroup>().spacing = 12;
            row.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
            row.GetComponent<HorizontalLayoutGroup>().childControlHeight = true;
            return row.transform;
        }

        private void BeginEdit(SavedReminder reminder)
        {
            if (busy) return;
            ShowPage("edit");
            customSnooze = false;
            reminderInput.interactable = true;
            cancelEdit.GetComponentInChildren<Text>().text = "Cancel editing";
            editingId = reminder.id;
            reminderInput.text = reminder.text;
            isPm = reminder.LocalTime.Hour >= 12;
            periodLabel.text = isPm ? "PM" : "AM";
            timeInput.SetTextWithoutNotify(reminder.LocalTime.ToString("h:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
            selectedTime = reminder.LocalTime;
            selectedDate = selectedTime.Date;
            calendarMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
            ShowCalendar();
            validTime = true;
            chosenTime.text = "Editing: " + selectedTime.ToString("MMM d, h:mm:ss tt");
            schedule.GetComponentInChildren<Text>().text = "Save changes";
            cancelEdit.gameObject.SetActive(true);
            status.text = "Edit the text or time, then save. Past reminders need a future time.";
            pageScroll.verticalNormalizedPosition = 1;
        }

        private void BeginCustomSnooze(SavedReminder reminder)
        {
            if (busy) return;
            BeginEdit(reminder);
            customSnooze = true;
            reminderInput.interactable = false;
            var initial = DateTime.Now.AddMinutes(10);
            selectedDate = initial.Date;
            calendarMonth = new DateTime(initial.Year, initial.Month, 1);
            ShowCalendar();
            isPm = initial.Hour >= 12;
            periodLabel.text = isPm ? "PM" : "AM";
            timeInput.SetTextWithoutNotify(initial.ToString("h:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
            UpdateChosenTime(timeInput.text);
            schedule.GetComponentInChildren<Text>().text = "Confirm snooze";
            cancelEdit.GetComponentInChildren<Text>().text = "Cancel snooze";
            status.text = "Choose a new time and AM or PM, then confirm snooze. The original reminder stays unchanged until you confirm.";
        }

        private void EndEdit()
        {
            editingId = 0;
            customSnooze = false;
            reminderInput.interactable = true;
            schedule.GetComponentInChildren<Text>().text = "Set reminder";
            cancelEdit.gameObject.SetActive(false);
        }

        private void Snooze(SavedReminder r, DateTime due) =>
            ChangeReminder(r, () => { r.utcTicks = due.ToUniversalTime().Ticks; r.completed = false; }, "Snoozed until " + due.ToString("MMM d, h:mm:ss tt"));

        private void ChangeReminder(SavedReminder r, Action change, string message)
        {
            if (busy) return;
            try
            {
                var before = JsonUtility.ToJson(r);
                change();
                try { store.Save(storePath); }
                catch { JsonUtility.FromJsonOverwrite(before, r); throw; }
                if (editingId == r.id) EndEdit();
                deleteConfirmId = 0;
                RefreshUpcoming();
                SyncReminder(r);
                status.text = message;
            }
            catch (Exception e) { ShowError(e); }
        }

        private static void SyncReminder(SavedReminder r)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidNotificationCenter.CancelNotification(r.id);
                if (!r.deleted && !r.completed && r.utcTicks > DateTime.UtcNow.Ticks) SendReminder(r);
            }
            catch (Exception e) { throw new InvalidOperationException("Change saved, but the phone notification could not be updated. Reopen the app to retry.", e); }
#endif
        }

        private void UpdateChosenTime(string value)
        {
            validTime = DateTime.TryParseExact(value.Trim() + (isPm ? " PM" : " AM"), new[] { "h:mm tt", "hh:mm tt", "h:mm:ss tt", "hh:mm:ss tt" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed);
            if (!validTime)
            {
                chosenTime.text = "Enter a time from 1 to 12, like 7:30 or 7:30:15, and choose AM or PM.";
                return;
            }
            selectedTime = selectedDate.Add(parsed.TimeOfDay);
            chosenTime.text = "Remind me: " + selectedTime.ToString("ddd, MMM d, yyyy 'at' h:mm:ss tt")
                + (selectedTime <= DateTime.Now ? "\nChoose a future date and time." : "");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<NotificationProof>() == null)
                new GameObject("Notification proof").AddComponent<NotificationProof>();
        }

        private void Start()
        {
            BuildUI();
            storePath = Path.Combine(Application.persistentDataPath, "reminders.json");
            try { store = ReminderStore.Load(storePath); RefreshUpcoming(); }
            catch (Exception e) { ShowError(e); schedule.interactable = false; return; }
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
                {
                    Id = Channel, Name = "Reminder tests", Importance = Importance.High,
                    Description = "Reminders at your chosen time"
                });
                // Stable IDs avoid duplicate alerts when reopening the app.
                foreach (var reminder in store.reminders)
                    if (reminder.deleted || reminder.completed || reminder.utcTicks > DateTime.UtcNow.Ticks)
                        SyncReminder(reminder);
                status.text = "Ready. Allow notifications when asked, then leave the app to test delivery.";
            }
            catch (Exception e) { ShowError(e); }
#else
            status.text = "Editor preview only. Build and run on an Android phone to test real notifications.";
#endif
        }

        private IEnumerator Schedule()
        {
            lastSaveSucceeded = false;
            if (busy || store == null) yield break;
            var reminderText = reminderInput.text.Trim();
            if (string.IsNullOrWhiteSpace(reminderText))
            {
                status.text = "Enter what you want to remember, such as Call John.";
                yield break;
            }
            if (!validTime || selectedTime <= DateTime.Now)
            {
                status.text = "Enter a valid future time before setting the reminder.";
                yield break;
            }
            var due = selectedTime;
            busy = true;
            timeInput.interactable = false;
            reminderInput.interactable = false;
            schedule.interactable = false;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                status.text = "Waiting for notification permission...";
                PermissionRequest request = null;
                try { request = new PermissionRequest(); }
                catch (Exception e) { ShowError(e); }
                if (request == null) yield break;
                while (request.Status == PermissionStatus.RequestPending) yield return null;
                if (request.Status != PermissionStatus.Allowed)
                {
                    status.text = "Notifications are disabled. Enable them in Android Settings > Apps > Remember This > Notifications, then try again.";
                    yield break;
                }
                try
                {
                    if (due <= DateTime.Now)
                    {
                        status.text = "That time passed while waiting for permission. Choose a new time.";
                        yield break;
                    }
                    SaveReminder(reminderText, due);
                    lastSaveSucceeded = true;
                    status.text = "Reminder set for " + due.ToString("MMM d, h:mm:ss tt") + ".\nLeave the app and watch for the notification.\n"
                        + (AndroidNotificationCenter.UsingExactScheduling
                            ? "Exact scheduling is available. Delivery still needs a phone test."
                            : "Android is using approximate timing; delivery may be delayed. Enable exact timing below, then schedule again.");
                }
                catch (Exception e) { ShowError(e); }
#else
                try { SaveReminder(reminderText, due); lastSaveSucceeded = true; }
                catch (Exception e) { ShowError(e); yield break; }
                status.text = "Preview: " + reminderText + "\nFor " + due.ToString("MMM d, h:mm:ss tt") + ". No notification was scheduled on this device.";
                yield return null;
#endif
            }
            finally { busy = false; schedule.interactable = true; timeInput.interactable = true; reminderInput.interactable = !customSnooze; }
        }

        private void SaveReminder(string text, DateTime due)
        {
            if (editingId != 0)
            {
                var existing = store.reminders.Single(r => r.id == editingId && !r.deleted && !r.completed);
                var oldText = existing.text;
                var oldTime = existing.utcTicks;
                existing.text = text; existing.utcTicks = due.ToUniversalTime().Ticks;
                try { store.Save(storePath); }
                catch { existing.text = oldText; existing.utcTicks = oldTime; throw; }
                EndEdit();
                RefreshUpcoming();
                SyncReminder(existing);
                return;
            }
            var reminder = new SavedReminder { id = store.nextId, text = text, utcTicks = due.ToUniversalTime().Ticks };
            store.nextId = checked(store.nextId + 1);
            store.reminders.Add(reminder);
            try { store.Save(storePath); }
            catch { store.reminders.Remove(reminder); store.nextId--; throw; }
#if UNITY_ANDROID && !UNITY_EDITOR
            // Persist first: if scheduling fails, reopening can retry this saved reminder.
            try { SendReminder(reminder); }
            catch (Exception e) { throw new InvalidOperationException("Reminder saved, but notification scheduling failed. Reopen the app to retry.", e); }
#endif
            RefreshUpcoming();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void SendReminder(SavedReminder reminder)
        {
            AndroidNotificationCenter.SendNotificationWithExplicitID(new AndroidNotification
            {
                Title = "Remember This", Text = reminder.text, FireTime = reminder.LocalTime,
                IntentData = "remember-snooze:" + reminder.id + ":" + reminder.utcTicks
            }, Channel, reminder.id);
        }
#endif

        private void Cancel()
        {
            if (busy || store == null) return;
            try
            {
                var reminder = store.reminders.Where(r => !r.deleted && !r.completed).OrderByDescending(r => r.id).FirstOrDefault();
                if (reminder == null) { status.text = "No saved reminder to cancel."; return; }
                deleteConfirmId = reminder.id;
                status.text = "Confirm deletion in the reminder list below.";
                RefreshUpcoming();
            }
            catch (Exception e) { ShowError(e); }
        }

        private void RequestExactTiming()
        {
            if (busy) return;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (AndroidNotificationCenter.UsingExactScheduling)
                    status.text = "Exact timing is already available. Tap Notify me to start a new test.";
                else
                {
                    AndroidNotificationCenter.RequestExactScheduling();
                    status.text = "Allow alarms and reminders in Android settings, return here, then schedule a new test.";
                }
#else
                status.text = "Exact timing permissions can only be tested on an Android phone.";
#endif
            }
            catch (Exception e) { ShowError(e); }
        }

        private void ShowError(Exception e)
        {
            Debug.LogException(e);
            status.text = "The notification operation failed. Please try again.\n" + e.Message;
        }

        private void BuildUI()
        {
            var canvasObject = new GameObject("Reminder UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(canvasObject.transform, false);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.055f, 0.09f, 0.14f);

            var viewport = new GameObject("Scroll viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(background.transform, false);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var safe = viewport.GetComponent<RectTransform>();
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            var panel = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(viewport.transform, false);
            var content = panel.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>();
            pageScroll = scroll;
            scroll.viewport = safe; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 36, 32);
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            Label(panel.transform, "REMEMBER THIS", 40, 55);
            currentClock = Label(panel.transform, "", 26, 75);
            Update();
            backHome = MakeButton(panel.transform, "Back to home", () => ShowPage("home"));
            homeSection = MakeSection(panel.transform, "Home");
            Label(homeSection.transform, "What would you like to do?", 30, 60);
            MakeButton(homeSection.transform, "Speak reminder", () => OpenVoiceDialog(false));
            MakeButton(homeSection.transform, "Calendar", ViewCalendar);
            MakeButton(homeSection.transform, "My reminders", () => ShowPage("saved"));
            Label(homeSection.transform, "Speak to create a reminder, choose a date, or manage saved reminders.", 26, 120);
            editSection = MakeSection(panel.transform, "Edit reminder");
            calendarSection = MakeSection(panel.transform, "Calendar page");
            formActions = MakeSection(panel.transform, "Save reminder");
            savedSection = MakeSection(panel.transform, "My reminders");
            Label(editSection.transform, "What do you want to remember?", 28, 45);
            reminderInput = MakeTimeInput(editSection.transform);
            reminderInput.gameObject.name = "Reminder text";
            reminderInput.characterLimit = 120;
            reminderInput.textComponent.fontSize = 30;
            var placeholder = Label(reminderInput.transform, "Example: Call John", 27, 85);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.75f, 0.8f, 0.85f);
            placeholder.raycastTarget = false;
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(20, 0);
            placeholder.rectTransform.offsetMax = new Vector2(-20, 0);
            reminderInput.placeholder = placeholder;
            MakeButton(editSection.transform, "Menu: calendar / speak / review", OpenReminderMenu);
            voiceButton = MakeButton(editSection.transform, "Speak reminder", () => OpenVoiceDialog(false));
            cancelVoice = MakeButton(editSection.transform, "Cancel voice entry", () => offlineVoice.Cancel());
            cancelVoice.gameObject.SetActive(false);
            voiceStatus = Label(editSection.transform, "Tap Speak reminder, say what and when, then pause. Review before saving.", 24, -1);
            Label(editSection.transform, "When? Say or type: in an hour, in 10 minutes, or tomorrow at 3 PM.", 22, 85);
            whenInput = MakeTimeInput(editSection.transform);
            whenInput.characterLimit = 120;
            whenInput.textComponent.fontSize = 30;
            whenInput.onEndEdit.AddListener(value => { if (!busy && !string.IsNullOrWhiteSpace(value)) ApplyWhen(value, DateTime.Now); });
            timeVoiceButton = MakeButton(editSection.transform, "Speak when", () => OpenVoiceDialog(true));
            Text licenses = null;
            MakeButton(editSection.transform, "Open-source licenses", () => licenses.gameObject.SetActive(!licenses.gameObject.activeSelf));
            licenses = Label(editSection.transform, string.Join("\n\n", new[] { "WhisperLicense", "WhisperCppLicense", "WhisperModelLicense" }
                .Select(name => Resources.Load<TextAsset>(name)?.text ?? name)), 18, -1);
            licenses.gameObject.SetActive(false);
            Label(calendarSection.transform, "Choose a day, then a time from 1 to 12 and AM or PM. Selected day is green.", 25, 90);
            returnToVoice = MakeButton(calendarSection.transform, "Return to menu / confirm", OpenReminderMenu);
            returnToVoice.gameObject.SetActive(false);
            var calendarObject = new GameObject("Calendar", typeof(RectTransform), typeof(VerticalLayoutGroup));
            calendarObject.transform.SetParent(calendarSection.transform, false);
            var calendarLayout = calendarObject.GetComponent<VerticalLayoutGroup>();
            calendarLayout.spacing = 8;
            calendarLayout.childControlWidth = calendarLayout.childControlHeight = true;
            calendarLayout.childForceExpandHeight = false;
            calendar = calendarObject.transform;
            var timeRow = new GameObject("Time and AM PM", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            timeRow.transform.SetParent(calendarSection.transform, false);
            timeRow.GetComponent<LayoutElement>().preferredHeight = 85;
            var rowLayout = timeRow.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 16;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            timeInput = MakeTimeInput(timeRow.transform);
            timeInput.GetComponent<LayoutElement>().flexibleWidth = 3;
            var initialTime = DateTime.Now.AddMinutes(1);
            selectedDate = initialTime.Date;
            calendarMonth = new DateTime(initialTime.Year, initialTime.Month, 1);
            ShowCalendar();
            isPm = initialTime.Hour >= 12;
            var periodButton = MakeButton(timeRow.transform, isPm ? "PM" : "AM", () =>
            {
                if (busy) return;
                isPm = !isPm;
                periodLabel.text = isPm ? "PM" : "AM";
                UpdateChosenTime(timeInput.text);
            });
            periodButton.GetComponent<LayoutElement>().flexibleWidth = 1;
            periodLabel = periodButton.GetComponentInChildren<Text>();
            chosenTime = Label(calendarSection.transform, "", 25, 85);
            timeInput.onValueChanged.AddListener(UpdateChosenTime);
            timeInput.text = initialTime.ToString("h:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            MakeButton(formActions.transform, "Edit reminder text / time", () => ShowPage("edit"));
            MakeButton(formActions.transform, "Review and confirm", OpenReminderMenu);
            schedule = MakeButton(formActions.transform, "Set reminder", () => StartCoroutine(Schedule()));
            cancelEdit = MakeButton(formActions.transform, "Cancel editing", () => { if (!busy) { EndEdit(); status.text = "Editing canceled. Saved reminder unchanged."; } });
            cancelEdit.gameObject.SetActive(false);
            MakeButton(savedSection.transform, "Enable exact timing", RequestExactTiming);
            MakeButton(savedSection.transform, "Cancel most recent reminder", Cancel);
            status = Label(panel.transform, "Starting...", 25, 190);
            Label(savedSection.transform, "Upcoming reminders — scroll to see all", 28, 75);
            var list = new GameObject("Reminder cards", typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(savedSection.transform, false);
            var listLayout = list.GetComponent<VerticalLayoutGroup>();
            listLayout.spacing = 14;
            listLayout.childControlWidth = listLayout.childControlHeight = true;
            listLayout.childForceExpandHeight = false;
            reminderList = list.transform;
            Label(savedSection.transform, "Snooze starts from now. Tomorrow means this time tomorrow. Past due does not confirm notification delivery.", 22, 95);
            BuildVoiceDialog(canvasObject.transform);
            ShowPage("home");
        }

        private void BuildVoiceDialog(Transform canvas)
        {
            voiceDialog = new GameObject("Voice questions", typeof(Image));
            voiceDialog.transform.SetParent(canvas, false);
            var overlay = voiceDialog.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            voiceDialog.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.07f, 0.98f);
            var box = new GameObject("Question", typeof(RectTransform), typeof(VerticalLayoutGroup));
            box.transform.SetParent(voiceDialog.transform, false);
            var rect = box.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.06f, 0.15f); rect.anchorMax = new Vector2(0.94f, 0.85f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var layout = box.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 24;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            voicePrompt = Label(box.transform, "", 34, 100);
            voiceHeard = Label(box.transform, "", 26, 230);
            MakeButton(box.transform, "View calendar", ViewCalendar);
            dialogRecord = MakeButton(box.transform, "Speak reminder", RecordDialogAnswer);
            voiceNext = MakeButton(box.transform, "Review and confirm", () =>
            {
                if (busy) return;
                OpenReminderMenu();
            });
            MakeButton(box.transform, "Cancel voice / close", () =>
            {
                if (voicePending) offlineVoice.Cancel();
                voiceDialog.SetActive(false);
            });
            voiceDialog.SetActive(false);
            reminderMenu = new GameObject("Reminder menu", typeof(Image));
            reminderMenu.transform.SetParent(canvas, false);
            var menuRect = reminderMenu.GetComponent<RectTransform>();
            menuRect.anchorMin = Vector2.zero; menuRect.anchorMax = Vector2.one;
            menuRect.offsetMin = menuRect.offsetMax = Vector2.zero;
            reminderMenu.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.07f, 1);
            var menuBox = new GameObject("Menu options", typeof(RectTransform), typeof(VerticalLayoutGroup));
            menuBox.transform.SetParent(reminderMenu.transform, false);
            var menuBoxRect = menuBox.GetComponent<RectTransform>();
            menuBoxRect.anchorMin = new Vector2(0.06f, 0.04f); menuBoxRect.anchorMax = new Vector2(0.94f, 0.96f);
            menuBoxRect.offsetMin = menuBoxRect.offsetMax = Vector2.zero;
            var menuLayout = menuBox.GetComponent<VerticalLayoutGroup>();
            menuLayout.spacing = 16;
            menuLayout.childControlWidth = menuLayout.childControlHeight = true;
            menuLayout.childForceExpandHeight = false;
            Label(menuBox.transform, "Review your reminder", 34, 65);
            menuPreview = Label(menuBox.transform, "", 23, 250);
            Label(menuBox.transform, "Ready? Choose one:", 26, 45);
            var confirm = MakeButton(menuBox.transform, "Confirm reminder — save it", () => StartCoroutine(ConfirmFromMenu()));
            confirm.GetComponent<Image>().color = new Color(0.78f, 0.95f, 0.81f);
            confirm.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            confirm.GetComponent<LayoutElement>().preferredHeight = 94;
            var cancel = MakeButton(menuBox.transform, "Cancel reminder — discard draft", () =>
            {
                if (busy) return;
                EndEdit();
                reminderInput.text = "";
                whenInput.SetTextWithoutNotify("");
                timeInput.text = "";
                voiceStatus.text = "Draft canceled. Speak or type a new reminder.";
                reminderMenu.SetActive(false);
                returnToVoice.gameObject.SetActive(false);
                status.text = "Draft canceled. Previously saved reminders are unchanged.";
            });
            cancel.GetComponent<Image>().color = new Color(1f, 0.82f, 0.80f);
            cancel.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            cancel.GetComponent<LayoutElement>().preferredHeight = 94;
            Label(menuBox.transform, "Need a change? Edit, check a date, or speak again:", 24, 60);
            MakeButton(menuBox.transform, "View calendar", ViewCalendar);
            MakeButton(menuBox.transform, "Speak reminder", () => OpenVoiceDialog(false));
            MakeButton(menuBox.transform, "Edit text / date / time", () =>
            {
                if (busy) return;
                reminderMenu.SetActive(false);
                returnToVoice.gameObject.SetActive(true);
                pageScroll.verticalNormalizedPosition = 1;
                ShowPage("edit");
                reminderInput.ActivateInputField();
            });
            MakeButton(menuBox.transform, "Check notification status", CheckNotificationStatus);
            reminderMenu.SetActive(false);
            BuildDueDialog(canvas);
        }

        private void BuildDueDialog(Transform canvas)
        {
            dueDialog = new GameObject("Due reminder", typeof(Image));
            dueDialog.transform.SetParent(canvas, false);
            var overlay = dueDialog.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            dueDialog.GetComponent<Image>().color = new Color(0.95f, 0.60f, 0.12f, 1);
            var box = new GameObject("Alert content", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            box.transform.SetParent(dueDialog.transform, false);
            box.GetComponent<Image>().color = new Color(1f, 0.97f, 0.88f, 1);
            var rect = box.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.06f, 0.12f); rect.anchorMax = new Vector2(0.94f, 0.88f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var layout = box.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 32, 32);
            layout.spacing = 24;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var heading = Label(box.transform, "IT'S TIME", 48, 90);
            heading.color = new Color(0.35f, 0.18f, 0.02f);
            heading.fontStyle = FontStyle.Bold;
            heading.alignment = TextAnchor.MiddleCenter;
            dueMessage = Label(box.transform, "", 36, 300);
            dueMessage.color = new Color(0.12f, 0.16f, 0.20f);
            dueMessage.alignment = TextAnchor.MiddleCenter;
            var shortSnooze = ActionRow(box.transform);
            MakeButton(shortSnooze, "Snooze 5 min", () => RespondToDueReminder(5));
            MakeButton(shortSnooze, "Snooze 10 min", () => RespondToDueReminder(10));
            var longSnooze = ActionRow(box.transform);
            MakeButton(longSnooze, "Snooze 1 hour", () => RespondToDueReminder(60));
            MakeButton(longSnooze, "Tomorrow", () => RespondToDueReminder(1440));
            MakeButton(box.transform, "Custom snooze…", () =>
            {
                if (busy || visibleDueReminder == null) return;
                if (reminderAudio != null) reminderAudio.Stop();
                BeginCustomSnooze(visibleDueReminder);
                dueDialog.SetActive(false);
            });
            var cancelButton = MakeButton(box.transform, "Cancel reminder", () => RespondToDueReminder(0));
            cancelButton.GetComponent<Image>().color = new Color(1f, 0.82f, 0.80f);
            cancelButton.GetComponent<LayoutElement>().preferredHeight = 80;
            dueDialog.SetActive(false);
        }

        private static InputField MakeTimeInput(Transform parent)
        {
            var obj = new GameObject("Reminder time", typeof(Image), typeof(InputField), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = new Color(0.16f, 0.22f, 0.29f);
            obj.GetComponent<LayoutElement>().preferredHeight = 85;
            var text = Label(obj.transform, "", 32, 85);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(20, 0);
            text.rectTransform.offsetMax = new Vector2(-20, 0);
            var field = obj.GetComponent<InputField>();
            field.textComponent = text;
            field.characterLimit = 8;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        private static Text Label(Transform parent, string value, int size, float height)
        {
            var obj = new GameObject("Text", typeof(Text), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = size; text.color = Color.white;
            text.supportRichText = false;
            obj.GetComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private static Button MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(label, typeof(Image), typeof(Button), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().color = Color.white;
            obj.GetComponent<LayoutElement>().preferredHeight = 80;
            var button = obj.GetComponent<Button>();
            button.onClick.AddListener(action);
            var text = Label(obj.transform, label, 30, 80);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 24;
            text.resizeTextMaxSize = 30;
            text.color = Color.black;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12, 0); rect.offsetMax = new Vector2(-12, 0);
            return button;
        }
    }
}
