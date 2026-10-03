# Remember This: notification proof

Local reminders with typed or spoken text, chosen dates and times, and saved reminder lists. Spoken alerts and a companion are not implemented yet.

The home screen shows **Speak reminder**, **Calendar**, and **My reminders**. Calendar opens the date/time controls, with **Edit reminder text / time** for keyboard entry. My reminders contains the saved list and notification settings. Each section has **Back to home**; longer sections scroll. Voice entry and review open over the current section.

The in-app reminder popup plays a short three-note chime once per reminder time. Snooze and Cancel stop it; a snoozed reminder chimes again when due. It uses device media volume. Background Android notifications retain the system notification sound and respect notification/channel settings; the custom chime runs only while the app is active.

While the app is open, a due saved reminder opens a message window with its text and time, **Snooze 10 minutes**, and **Cancel reminder**. Snooze schedules the same reminder again; Cancel completes it. Overdue unfinished reminders also appear when returning to the app, oldest first. The popup waits for recording or saving to finish. Background notifications still depend on Android delivery; this popup does not prove that a background notification arrived.

## Languages

English and Spanish translations are bundled with the app; there is no translation service, runtime download, or per-user translation fee. The home screen has a **Language / Idioma** selector: **Automatic**, **English**, and **Español**. The selection is saved and switches the interface immediately without changing saved reminders or an unsaved draft. Automatic uses Spanish for Spanish-language devices and English otherwise; fully close and reopen the app after changing the phone language. Controls, guidance, calendar dates, and notification channel text are localized. Android notification action buttons use the saved language choice when the alert is delivered, even with the app closed; Automatic follows the device language. Already visible notifications keep their existing button labels. The app name **Remember This**, original license text, and technical exception details remain unchanged. User-written reminders are never translated or modified by language selection.

Offline speech recognition uses English or Spanish to match the interface. Spanish whole commands support relative times, such as **Llamar a Juan en una hora**, **Caminar en media hora**, and **Comprar leche dentro de dos días**. The separate When field also accepts **en 10 minutos** and **mañana a las 3 PM**. Numeric clock times require AM/PM; unsupported phrases need manual correction. Review and confirm before saving.

Run `pwsh -File Tools/Test-ReminderLocalization.ps1` and `pwsh -File Tools/Test-ReminderCommands.ps1`, plus **Remember This > Check reminder localization** in Unity. Before release, test English and Spanish on a physical Android phone: home/calendar/edit/review/list/popup layouts, microphone and notification permission messages, Spanish transcription, scheduled alert delivery and actions with the app backgrounded, and reopening with existing reminders. Also test an unsupported device language for English fallback. Rebuild and publish a new app version for Google Play users to receive these changes.

## Voice entry

**Menu: calendar / speak / review** opens a preview with **View calendar**, **Speak reminder**, **Edit text / date / time**, **Confirm reminder**, and **Cancel reminder — discard draft**. Speak a whole relative command such as **Call John in one hour**, pause, then review the extracted task and time. If the time is unclear, choose it manually or use **Speak when**. After editing or viewing the calendar, use **Return to menu / confirm**. Confirm uses the existing validation and notification permission flow; The review screen highlights Confirm in green and Cancel in red, with correction options below. Cancel discards the unsaved draft; existing saved reminders remain unchanged. Speech never saves automatically.

The separate When field accepts spoken or typed phrases such as **in an hour**, **in ten minutes**, and **tomorrow at 3 PM**. Clock times require AM/PM. Unsupported or unclear phrases require manual correction. Relative voice times are measured from the recording request, so long transcription delays can leave a short reminder time in the past; it must be corrected before saving. Run `pwsh -File Tools/Test-ReminderCommands.ps1` for parser checks.

Tap **Speak reminder** to start listening immediately, allow microphone access if asked, and speak. After speech followed by about 1.5 seconds of quiet, the app prepares the result and highlights the green **Review and confirm** button. **Done speaking** ends recording manually if background noise prevents pause detection. Recording also stops after about 20 seconds. Pause detection uses microphone volume, so test it with your microphone and normal background noise. Whisper transcribes offline on the device; no Google speech app, account, API key, or runtime download is needed. The first use loads the model and can take longer. Review and correct the result before saving. Whole commands support relative times; the separate When field also supports today/tomorrow with a numeric AM/PM clock time. Other dates can be selected on the calendar. Text is limited to 120 characters. Cancellation, silence, or failure preserves the original text, and speech never saves a reminder automatically.

The bundled tiny multilingual model uses English or Spanish transcription to match the app language. Test voice in Windows Unity Play mode, Android ARM64, and iOS. BlueStacks needs ARM64 app compatibility and a configured microphone; physical devices remain the release gate. iOS speech integration is included, but the reminder app's notification implementation is still Android-only. Spoken alerts are not implemented. Cancellation during transcription discards the result after processing finishes. Custom snooze keeps text fixed and does not allow voice entry.

Whisper 1.4.0 is pinned to revision `529a628a915a97799e89e061af9cb7c71407124d`. To restore ignored dependencies on a new checkout, run `pwsh -File Tools/Restore-Whisper.ps1` from the project directory. Downloads are checked against pinned Git blob hashes. The approximately 78 MB model and native package libraries stay out of Git; installed files remain on disk and are bundled in builds. Windows, macOS, Android ARM64, and iOS libraries are included; Linux is omitted. Licenses are bundled under Resources and accessible through **Open-source licenses**.

## Run

1. Open this project in Unity **2022.3.62f2** and let Package Manager install Mobile Notifications **2.4.3**.
2. Open `Assets/Scenes/SampleScene.unity` and press Play to preview the interface. The UI is created at runtime; no inspector wiring is required. Editor preview does not schedule notifications.
3. Select **Remember This > Build Android test APK**. The build configures **IL2CPP / ARM64**, portrait orientation, a development application ID, microphone permission, exact-alarm permission, and notification rescheduling after restart. Output: `Builds/Android/RememberThis.apk`. The first IL2CPP build can take longer.
4. Install the APK on your Android phone. Tap **Enable exact timing** if required, allow alarms/reminders, and return to the app.
5. Choose a day in the month-view calendar, using the arrows to change months or **Today** to return to today. The selected day is green; past days cannot be selected. Enter a clock time from **1 to 12**, such as **2:30** or **2:30:15**, and choose **AM/PM**. Review the full date and time, then tap **Set reminder** and grant notification permission. Leave the app and check the notification drawer.

For a quick test, select today and a time 20–30 seconds ahead. Invalid or past times are rejected, without silently moving the reminder to tomorrow. Editing and custom snooze use the calendar as well.

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
