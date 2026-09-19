using System.IO;
using Unity.Notifications;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RememberThis.Editor
{
    public sealed class NotificationProofBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) Configure();
        }

        [MenuItem("Remember This/Configure Android notification test")]
        public static void Configure()
        {
            NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart = true;
            NotificationSettings.AndroidSettings.ExactSchedulingOption =
                AndroidExactSchedulingOption.ExactWhenAvailable |
                AndroidExactSchedulingOption.AddScheduleExactPermission;
            PlayerSettings.productName = "Remember This";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.rememberthis.notificationproof");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Remember This/Build Android test APK")]
        public static void BuildAndroid()
        {
            Configure();
            Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = "Builds/Android/RememberThis.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Android notification test build failed: " + report.summary.result);
        }
    }
}
