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
        public const string RequiredApplicationId = "com.robertchaney.rememberthis";
        public static void VerifyApplicationId()
        {
            if (PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android) != RequiredApplicationId)
                throw new BuildFailedException("Android application ID must be " + RequiredApplicationId + ". Set it in Project Settings > Player; build scripts must not overwrite it.");
        }
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!File.Exists("Assets/StreamingAssets/Whisper/ggml-tiny.bin"))
                throw new BuildFailedException("Run Tools/Restore-Whisper.ps1 to restore the offline speech model.");
            PlayerSettings.iOS.microphoneUsageDescription = "Record a short reminder and transcribe it offline on this device.";
            if (report.summary.platform == BuildTarget.Android) Configure();
        }

        [MenuItem("Remember This/Configure Android notification test")]
        public static void Configure()
        {
            VerifyApplicationId();
            NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart = true;
            NotificationSettings.AndroidSettings.ExactSchedulingOption =
                AndroidExactSchedulingOption.ExactWhenAvailable |
                AndroidExactSchedulingOption.AddScheduleExactPermission;
            PlayerSettings.productName = "Remember This";
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Remember This/Build signed release AAB")]
        public static void BuildReleaseBundle()
        {
            Configure();
            if (!File.Exists("user.keystore") || PlayerSettings.Android.keyaliasName != "rememberthis")
                throw new BuildFailedException("Expected existing user.keystore and release alias rememberthis.");
            var password = System.Environment.GetEnvironmentVariable("REMEMBERTHIS_SIGNING_PASSWORD");
            var keystorePassword = string.IsNullOrEmpty(password) ? PlayerSettings.Android.keystorePass : password;
            var aliasPassword = string.IsNullOrEmpty(password) ? PlayerSettings.Android.keyaliasPass : password;
            if (string.IsNullOrEmpty(keystorePassword) || string.IsNullOrEmpty(aliasPassword))
                throw new BuildFailedException("Enter the keystore and alias passwords in Player Settings > Publishing Settings, or provide REMEMBERTHIS_SIGNING_PASSWORD for this build process.");
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath("user.keystore");
            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasPass = aliasPassword;
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            Directory.CreateDirectory("Builds/Android");
            try
            {
                VerifyApplicationId();
                Debug.Log("Release AAB verified application ID: " + RequiredApplicationId + "; signing alias: " + PlayerSettings.Android.keyaliasName);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                    locationPathName = "Builds/Android/RememberThis-release.aab",
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Release AAB build failed: " + report.summary.result);
            }
            finally
            {
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
            }
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
