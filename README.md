# Remember This: notification proof

First milestone only: a local notification at a chosen time on Android. No voice, reminder database, or companion yet.

## Run

1. Open this project in Unity **2022.3.62f2** and let Package Manager install Mobile Notifications **2.4.3**.
2. Open `Assets/Scenes/SampleScene.unity` and press Play to preview the interface. The UI is created at runtime; no inspector wiring is required. Editor preview does not schedule notifications.
3. Select **Remember This > Build Android test APK**. The build configures portrait orientation, a development application ID, exact-alarm permission, and notification rescheduling after restart. Output: `Builds/Android/RememberThis.apk`.
4. Install the APK on your Android phone. Tap **Enable exact timing** if required, allow alarms/reminders, and return to the app.
5. Enter a clock time from **1 to 12**, such as **2:30** or **2:30:15**, and tap the **AM/PM** button to choose the period. The displayed date shows whether it means today or tomorrow. Tap **Set reminder**, grant notification permission, and check the scheduled time. Leave the app and check the notification drawer.

For a quick test, choose a time 20–30 seconds ahead. Invalid times are rejected; a selected time that passes before saving must be changed. If you enter a time earlier than now, the preview explicitly shows tomorrow.

Each new test replaces the previous test notification. Cancel removes both pending and displayed test notifications. The default app icon is used for this prototype.

## Device acceptance checks

Record phone model, Android version, target time, actual delivery time, and result for each case. Scheduling is not proof of delivery.

| Case | Expected observation |
| --- | --- |
| App open | Notification appears in the notification drawer |
| Home pressed | Notification appears while app is in background |
| Dismissed from recent apps | Notification appears without reopening app; record manufacturer restrictions |
| Reboot before due time | Non-expired notification is restored; unlock phone and inspect drawer |
| Permission denied | App explains how to enable notifications; no success claim |
| Exact timing denied | App reports approximate timing; record any delay |
| Cancel before due time | No notification at the canceled time |
| Schedule twice | Only the latest test fires |

Test Android Settings **Force stop** separately from dismissing recent apps. It is not the normal closed-app acceptance case. Also check channel settings, Do Not Disturb, and battery restrictions when diagnosing missing alerts. This prototype is not a reliability guarantee.

## Implementation

- `Assets/Scripts/NotificationProof.cs`: runtime UI, permission flow, scheduling and cancellation.
- `Assets/Editor/NotificationProofBuild.cs`: repeatable Android build configuration and APK menu.
- `Packages/manifest.json`: pinned Mobile Notifications dependency.

Uses the official [Unity Android notification APIs](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/Android.html).
