using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace RememberThis.Editor
{
    // Pinned Mobile Notifications 2.4.3 integration. Never edit PackageCache.
    public sealed class NotificationActionBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            NotificationProofBuild.VerifyApplicationId();
            var root = Directory.GetParent(path).FullName;
            var files = Directory.GetFiles(root, "UnityNotificationManager.java", SearchOption.AllDirectories);
            if (files.Length != 1) throw new BuildFailedException("Expected one Mobile Notifications Java source for snooze integration.");
            var file = files.Single();
            const string anchor = "finalizeNotificationForDisplay(builder);";
            const string call = "ReminderNotificationActions.attach(mContext, openActivity, builder, id);";
            var source = File.ReadAllText(file);
            if (!source.Contains(call))
            {
                if (source.Split(new[] { anchor }, StringSplitOptions.None).Length != 2)
                    throw new BuildFailedException("Mobile Notifications changed: review the snooze delivery hook before building.");
                File.WriteAllText(file, source.Replace(anchor, call + "\n        " + anchor));
            }
            File.Copy(Path.Combine(Application.dataPath, "AndroidSource/ReminderNotificationActions.java.txt"),
                Path.Combine(Path.GetDirectoryName(file), "ReminderNotificationActions.java"), true);
            var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            var manifest = XDocument.Load(manifestPath);
            XNamespace android = "http://schemas.android.com/apk/res/android";
            var application = manifest.Root.Element("application");
            const string activityName = "com.unity.androidnotifications.ReminderVoiceActivity";
            application.Elements("activity").Where(a => (string)a.Attribute(android + "name") == activityName).Remove();
            if (!manifest.Root.Elements("uses-permission").Any(p => (string)p.Attribute(android + "name") == "android.permission.RECORD_AUDIO"))
                manifest.Root.Add(new XElement("uses-permission", new XAttribute(android + "name", "android.permission.RECORD_AUDIO")));
            manifest.Save(manifestPath);
        }
    }
}
