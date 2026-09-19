using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RememberThis.Editor
{
    public static class ReminderStoreChecks
    {
        [MenuItem("Remember This/Check reminder storage")]
        public static void Run()
        {
            var directory = Path.Combine(Application.temporaryCachePath, "ReminderChecks-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "reminders.json");
            try
            {
                var now = DateTime.UtcNow;
                var store = ReminderStore.Load(path);
                Require(store.reminders.Count == 0, "First launch should have no reminders.");
                store.reminders.Add(new SavedReminder { id = store.nextId++, text = "Later", utcTicks = now.AddHours(2).Ticks });
                store.reminders.Add(new SavedReminder { id = store.nextId++, text = "Call John", utcTicks = now.AddHours(1).Ticks });
                store.reminders.Add(new SavedReminder { id = store.nextId++, text = "Same time", utcTicks = now.AddHours(1).Ticks });
                store.Save(path);
                var loaded = ReminderStore.Load(path);
                Require(loaded.reminders.Count == 3 && loaded.nextId == 22003, "Reload must preserve records and ID allocation.");
                Require(loaded.Upcoming(now).Select(r => r.text).SequenceEqual(new[] { "Call John", "Same time", "Later" }), "Upcoming order must be chronological, including same-time reminders.");
                Require(!loaded.Upcoming(now.AddHours(3)).Any(), "Past reminders must leave Upcoming.");
                Require(loaded.reminders[0].LocalTime.ToUniversalTime().Ticks == store.reminders[0].utcTicks, "Time must survive UTC/local conversion.");
                loaded.reminders.RemoveAt(2);
                loaded.Save(path);
                Require(ReminderStore.Load(path).reminders.Count == 2 && File.Exists(path + ".bak"), "Replacement save must persist changes and keep a backup.");
                File.WriteAllText(path, "not valid json");
                bool rejected = false;
                try { ReminderStore.Load(path); } catch { rejected = true; }
                Require(rejected && File.ReadAllText(path) == "not valid json", "Corrupt storage must be rejected without overwriting it.");
                Debug.Log("Reminder storage checks passed.");
            }
            finally
            {
                // Only the uniquely named files created by this check are removed.
                foreach (var file in new[] { path, path + ".bak", path + ".tmp" })
                    if (File.Exists(file)) File.Delete(file);
                Directory.Delete(directory);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
