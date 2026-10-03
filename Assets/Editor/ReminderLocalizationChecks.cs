using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RememberThis.Editor
{
    public static class ReminderLocalizationChecks
    {
        [MenuItem("Remember This/Check reminder localization")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            var previousLanguage = ReminderLocalization.Language;
            var previousChoice = ReminderLanguagePreference.Load();
            var existingEventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
            try
            {
                foreach (var language in new[] { "en", "es" })
                {
                    ReminderLocalization.Configure(language);
                    var root = new GameObject("Localization checks");
                    try
                    {
                        var app = root.AddComponent<NotificationProof>();
                        Invoke(app, "BuildUI");
                        var texts = root.GetComponentsInChildren<Text>(true);
                        Require(texts.Any(t => t.text == ReminderLocalization.T("Speak reminder")), "Localized home button missing.");
                        Require(texts.Any(t => t.text == ReminderLocalization.T("IT'S TIME")), "Localized due popup missing.");
                        Require(texts.Any(t => t.text == ReminderLocalization.T("Confirm reminder — save it")), "Localized confirm button missing.");
                        Require(texts.Any(t => t.text == ReminderLocalization.Culture.DateTimeFormat.AbbreviatedDayNames[0]), "Calendar weekday missing.");
                        var input = root.GetComponentsInChildren<InputField>(true).Single(f => f.name == "Reminder text");
                        input.text = "Call María — tomar agua";
                        Invoke(app, "OpenReminderMenu");
                        Require(texts.Any(t => t.text.Contains("Call María — tomar agua") && t.text.StartsWith(ReminderLocalization.T("What: "))),
                            "Review must localize labels and preserve reminder text.");
                        Require(root.GetComponentsInChildren<Button>(true).All(b => b.GetComponentInChildren<Text>(true) != null), "Button label missing.");
                        CheckSelector(app, root);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                Debug.Log("PASS: English/Spanish UI, language selector, persistence, Automatic, and preservation of draft/edit/snooze state.");
            }
            finally
            {
                ReminderLocalization.Configure(previousLanguage);
                ReminderLanguagePreference.Save(previousChoice);
                if (existingEventSystem == null)
                {
                    var created = UnityEngine.Object.FindObjectOfType<EventSystem>();
                    if (created != null) UnityEngine.Object.DestroyImmediate(created.gameObject);
                }
            }
        }

        private static void CheckSelector(NotificationProof app, GameObject root)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Action<string, object> set = (name, value) => typeof(NotificationProof).GetField(name, flags).SetValue(app, value);
            Func<string, object> get = name => typeof(NotificationProof).GetField(name, flags).GetValue(app);
            var store = new ReminderStore();
            var saved = new SavedReminder { id = 22000, text = "Saved English / español", utcTicks = DateTime.UtcNow.AddDays(3).Ticks };
            store.reminders.Add(saved);
            set("store", store);
            set("editingId", saved.id);
            set("customSnooze", true);
            var selectedDay = DateTime.Today.AddDays(2);
            set("selectedDate", selectedDay);
            set("calendarMonth", new DateTime(selectedDay.Year, selectedDay.Month, 1));
            set("isPm", true);
            ((InputField)get("timeInput")).SetTextWithoutNotify("7:45:12");
            ((InputField)get("whenInput")).SetTextWithoutNotify("my original time phrase");
            Invoke(app, "ShowPage", "home");
            foreach (var choice in new[] { "es", "en", "auto" })
            {
                var button = root.GetComponentsInChildren<Button>(true).Single(b => b.name == "Language " + choice);
                button.onClick.Invoke();
                Require(ReminderLanguagePreference.Load() == choice, "Language choice was not saved.");
                var expected = choice == "auto" ? (Application.systemLanguage == SystemLanguage.Spanish ? "es" : "en") : choice;
                Require(ReminderLocalization.Language == expected, "Language choice was not applied.");
                var texts = root.GetComponentsInChildren<Text>(true);
                Require(texts.Any(t => t.text == ReminderLocalization.T("Speak reminder")), "Screen did not switch language.");
                Require(((InputField)get("reminderInput")).text == "Call María — tomar agua", "Draft text changed.");
                Require(((InputField)get("whenInput")).text == "my original time phrase", "When phrase changed.");
                Require((DateTime)get("selectedTime") == selectedDay.AddHours(19).AddMinutes(45).AddSeconds(12), "Selected time changed.");
                Require((int)get("editingId") == saved.id && (bool)get("customSnooze"), "Edit/snooze state changed.");
                Require(!((InputField)get("reminderInput")).interactable, "Snooze text should remain locked.");
                Require(saved.text == "Saved English / español" && store.reminders.Count == 1, "Saved reminders changed.");
                Require(!root.GetComponentsInChildren<Button>(true).Single(b => b.name == "Language " + choice).interactable,
                    "Selected language is not indicated.");
            }
        }

        private static void Invoke(NotificationProof app, string method, params object[] arguments) =>
            typeof(NotificationProof).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, arguments);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
