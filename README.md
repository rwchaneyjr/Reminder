# Remember This: notification proof

Local reminders with custom text, chosen times, and a saved Upcoming list. No voice or companion yet.

## Run

1. Open this project in Unity **2022.3.62f2** and let Package Manager install Mobile Notifications **2.4.3**.
2. Open `Assets/Scenes/SampleScene.unity` and press Play to preview the interface. The UI is created at runtime; no inspector wiring is required. Editor preview does not schedule notifications.
3. Select **Remember This > Build Android test APK**. The build configures portrait orientation, a development application ID, exact-alarm permission, and notification rescheduling after restart. Output: `Builds/Android/RememberThis.apk`.
4. Install the APK on your Android phone. Tap **Enable exact timing** if required, allow alarms/reminders, and return to the app.
5. Enter a clock time from **1 to 12**, such as **2:30** or **2:30:15**, and tap the **AM/PM** button to choose the period. The displayed date shows whether it means today or tomorrow. Tap **Set reminder**, grant notification permission, and check the scheduled time. Leave the app and check the notification drawer.

For a quick test, choose a time 20–30 seconds ahead. Invalid times are rejected; a selected time that passes before saving must be changed. If you enter a time earlier than now, the preview explicitly shows tomorrow.

Each reminder gets a separate notification. Scroll down for Upcoming, sorted by time. Reminders are stored in `reminders.json` under Unity's persistent data directory, with a backup on replacement saves. Reopening restores the list and retries missing future Android notifications. Passed times leave Upcoming but remain stored; this does not prove delivery. Times are stored as UTC instants and displayed in local time.

Each reminder has **Edit**, **Complete**, **Delete**, and snooze controls. Edit fills the form; **Save changes** updates the same notification ID. **Cancel editing** leaves the saved reminder unchanged. Past reminders require a future time before an edit can be saved. **Delete** requires confirmation; **Keep** cancels deletion. **Complete** cancels the notification and moves the reminder to Completed. Completed reminders can be deleted.

Snooze uses **10 minutes**, **1 hour**, or **this time tomorrow**, measured from now. Past due reminders stay accessible for these actions. Completed and deleted flags persist; deleted records stay hidden so the app can retry canceling their notifications on reopening. **Cancel most recent reminder** opens a deletion confirmation for the latest active record. Editor Play mode saves preview reminders locally but sends no Android alerts. Reinstall the APK without uninstalling the app to preserve app data.

Run **Remember This > Check reminder storage** for reload, unique IDs, ordering, backup, and corruption checks. Rebuild the APK for device testing.

## Notification actions

New notifications have **Snooze 10 min** and **Cancel** actions. Expand the Android notification if the buttons are hidden. Tapping either opens/resumes the app and applies the action automatically: Snooze schedules the same reminder ten minutes from now; Cancel marks it complete and dismisses its notification. This is a normal notification, not a continuously ringing full-screen alarm. An obsolete notification cannot change an edited, deleted, or completed reminder.

The Android build hook adds the actions when Mobile Notifications 2.4.3 delivers the notification, so action PendingIntents are not serialized into its stored notifications. It changes generated Gradle sources only and fails the build if the expected integration point changes. Test actions with the app open, backgrounded, and dismissed. Rebuild and reinstall before testing; existing displayed notifications do not gain buttons retroactively.

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
| Schedule twice | Both reminders remain listed and fire independently |
| Same time | Two reminders have distinct notifications |
| Close and reopen | Upcoming text and times survive, without duplicate notifications |
| Cancel most recent | Older reminders remain scheduled |
| Edit one of two reminders | Only that reminder changes; its old alert is canceled and the new text/time fires |
| Cancel editing | Original text/time stay saved |
| Complete and reopen | Record stays Completed and no longer fires |
| Delete then Keep | Reminder stays unchanged |
| Confirm Delete and reopen | Record stays hidden and no longer fires |
| Snooze a past-due reminder | Record returns to Upcoming at the displayed new time with one notification |

Test Android Settings **Force stop** separately from dismissing recent apps. It is not the normal closed-app acceptance case. Also check channel settings, Do Not Disturb, and battery restrictions when diagnosing missing alerts. This prototype is not a reliability guarantee.

## Implementation

- `Assets/Scripts/NotificationProof.cs`: runtime UI, permission flow, scheduling and cancellation.
- `Assets/Editor/NotificationProofBuild.cs`: repeatable Android build configuration and APK menu.
- `Packages/manifest.json`: pinned Mobile Notifications dependency.

Uses the official [Unity Android notification APIs](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/Android.html).
