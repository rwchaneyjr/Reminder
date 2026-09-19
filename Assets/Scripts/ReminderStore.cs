using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RememberThis
{
    [Serializable]
    public sealed class SavedReminder
    {
        public int id;
        public string text;
        public long utcTicks;
        public bool completed;
        public bool deleted;
        public DateTime LocalTime => new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
    }

    [Serializable]
    public sealed class ReminderStore
    {
        public int nextId = 22000;
        public List<SavedReminder> reminders = new List<SavedReminder>();

        public static ReminderStore Load(string path)
        {
            if (!File.Exists(path)) return new ReminderStore();
            var data = JsonUtility.FromJson<ReminderStore>(File.ReadAllText(path));
            if (data == null || data.reminders == null || data.nextId < 22000 ||
                data.reminders.Any(r => r == null || r.id < 22000 || r.id >= data.nextId ||
                    string.IsNullOrWhiteSpace(r.text) || r.utcTicks <= 0 || r.utcTicks > DateTime.MaxValue.Ticks) ||
                data.reminders.Select(r => r.id).Distinct().Count() != data.reminders.Count)
                throw new InvalidDataException("Saved reminders could not be read. The original file has been preserved.");
            return data;
        }

        public void Save(string path)
        {
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(this, true));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }

        public IEnumerable<SavedReminder> Upcoming(DateTime utcNow) =>
            reminders.Where(r => !r.completed && !r.deleted && r.utcTicks > utcNow.Ticks).OrderBy(r => r.utcTicks).ThenBy(r => r.id);
    }
}
