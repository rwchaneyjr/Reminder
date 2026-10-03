using System;
using UnityEngine;

namespace RememberThis
{
    // Android uses the same preference in Unity and in the background notification receiver.
    public static class ReminderLanguagePreference
    {
        private const string Key = "remember_this_language";

        public static string Load()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var actions = new AndroidJavaClass("com.unity.androidnotifications.ReminderNotificationActions"))
                return ReminderLocalization.NormalizeChoice(actions.CallStatic<string>("getLanguage", activity));
#else
            return ReminderLocalization.NormalizeChoice(PlayerPrefs.GetString(Key, "auto"));
#endif
        }

        public static void Save(string choice)
        {
            choice = ReminderLocalization.NormalizeChoice(choice);
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var actions = new AndroidJavaClass("com.unity.androidnotifications.ReminderNotificationActions"))
                if (!actions.CallStatic<bool>("setLanguage", activity, choice))
                    throw new InvalidOperationException("Could not persist language preference.");
#else
            PlayerPrefs.SetString(Key, choice);
            PlayerPrefs.Save();
#endif
        }
    }
}
