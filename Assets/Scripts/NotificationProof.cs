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
        private InputField timeInput;
        private InputField reminderInput;
        private Text chosenTime;
        private DateTime selectedTime;
        private bool validTime;
        private bool isPm;
        private Text periodLabel;
        private Text currentClock;
        private long displayedSecond = -1;

        private void Update()
        {
            if (currentClock == null) return;
            var now = DateTime.Now;
            var second = now.Ticks / TimeSpan.TicksPerSecond;
            if (second == displayedSecond) return;
            displayedSecond = second;
            currentClock.text = now.ToString("dddd, MMMM d, yyyy\nh:mm:ss tt",
                System.Globalization.CultureInfo.InvariantCulture);
            RefreshUpcoming();
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
            customSnooze = false;
            reminderInput.interactable = true;
            cancelEdit.GetComponentInChildren<Text>().text = "Cancel editing";
            editingId = reminder.id;
            reminderInput.text = reminder.text;
            isPm = reminder.LocalTime.Hour >= 12;
            periodLabel.text = isPm ? "PM" : "AM";
            timeInput.SetTextWithoutNotify(reminder.LocalTime.ToString("h:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
            selectedTime = reminder.LocalTime;
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
            var now = DateTime.Now;
            validTime = DateTime.TryParseExact(value.Trim() + (isPm ? " PM" : " AM"), new[] { "h:mm tt", "hh:mm tt", "h:mm:ss tt", "hh:mm:ss tt" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed);
            if (!validTime)
            {
                chosenTime.text = "Enter a time from 1 to 12, like 7:30 or 7:30:15, and choose AM or PM.";
                return;
            }
            selectedTime = now.Date.Add(parsed.TimeOfDay);
            if (selectedTime <= now) selectedTime = selectedTime.AddDays(1);
            chosenTime.text = "Remind me: " + selectedTime.ToString("ddd, MMM d 'at' h:mm:ss tt")
                + (selectedTime.Date > now.Date ? " (tomorrow)" : " (today)");
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
                    status.text = "Reminder set for " + due.ToString("MMM d, h:mm:ss tt") + ".\nLeave the app and watch for the notification.\n"
                        + (AndroidNotificationCenter.UsingExactScheduling
                            ? "Exact scheduling is available. Delivery still needs a phone test."
                            : "Android is using approximate timing; delivery may be delayed. Enable exact timing below, then schedule again.");
                }
                catch (Exception e) { ShowError(e); }
#else
                try { SaveReminder(reminderText, due); }
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
            scaler.matchWidthOrHeight = 0.5f;
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
            layout.padding = new RectOffset(40, 40, 50, 40);
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            Label(panel.transform, "REMEMBER THIS", 40, 55);
            currentClock = Label(panel.transform, "", 26, 75);
            Update();
            Label(panel.transform, "What do you want to remember?", 28, 45);
            reminderInput = MakeTimeInput(panel.transform);
            reminderInput.gameObject.name = "Reminder text";
            reminderInput.characterLimit = 120;
            reminderInput.textComponent.fontSize = 27;
            var placeholder = Label(reminderInput.transform, "Example: Call John", 27, 85);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.75f, 0.8f, 0.85f);
            placeholder.raycastTarget = false;
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(20, 0);
            placeholder.rectTransform.offsetMax = new Vector2(-20, 0);
            reminderInput.placeholder = placeholder;
            Label(panel.transform, "Choose a time from 1 to 12 and AM or PM. A time earlier than now means tomorrow.", 25, 90);
            var timeRow = new GameObject("Time and AM PM", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            timeRow.transform.SetParent(panel.transform, false);
            timeRow.GetComponent<LayoutElement>().preferredHeight = 85;
            var rowLayout = timeRow.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 16;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            timeInput = MakeTimeInput(timeRow.transform);
            timeInput.GetComponent<LayoutElement>().flexibleWidth = 3;
            var initialTime = DateTime.Now.AddMinutes(1);
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
            chosenTime = Label(panel.transform, "", 25, 85);
            timeInput.onValueChanged.AddListener(UpdateChosenTime);
            timeInput.text = initialTime.ToString("h:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            schedule = MakeButton(panel.transform, "Set reminder", () => StartCoroutine(Schedule()));
            cancelEdit = MakeButton(panel.transform, "Cancel editing", () => { if (!busy) { EndEdit(); status.text = "Editing canceled. Saved reminder unchanged."; } });
            cancelEdit.gameObject.SetActive(false);
            MakeButton(panel.transform, "Enable exact timing", RequestExactTiming);
            MakeButton(panel.transform, "Cancel most recent reminder", Cancel);
            status = Label(panel.transform, "Starting...", 25, 190);
            Label(panel.transform, "Upcoming reminders — scroll to see all", 28, 75);
            var list = new GameObject("Reminder cards", typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(panel.transform, false);
            var listLayout = list.GetComponent<VerticalLayoutGroup>();
            listLayout.spacing = 14;
            listLayout.childControlWidth = listLayout.childControlHeight = true;
            listLayout.childForceExpandHeight = false;
            reminderList = list.transform;
            Label(panel.transform, "Snooze starts from now. Tomorrow means this time tomorrow. Past due does not confirm notification delivery.", 22, 95);
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
            obj.GetComponent<Image>().color = new Color(0.12f, 0.35f, 0.40f);
            obj.GetComponent<LayoutElement>().preferredHeight = 94;
            var button = obj.GetComponent<Button>();
            button.onClick.AddListener(action);
            var text = Label(obj.transform, label, 27, 94);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12, 0); rect.offsetMax = new Vector2(-12, 0);
            return button;
        }
    }
}
