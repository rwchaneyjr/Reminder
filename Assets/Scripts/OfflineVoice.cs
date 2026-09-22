using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Whisper;

namespace RememberThis
{
    public sealed class OfflineVoice : MonoBehaviour
    {
        private WhisperWrapper model;
        private AudioClip recording;
        private bool stop;
        private bool canceled;
        private bool processing;
        private float processingStarted;
        private int lastDisplayedSecond = -1;
        private int inferencePercent;
        private bool loading;
        public bool IsRecording { get; private set; }
        public bool IsProcessing => processing;
        public Action<string> Progress;
        public Action<string, string> Finished;

        public void Begin() { stop = canceled = false; StartCoroutine(Capture()); }
        public void Stop() { stop = true; }
        public void Cancel() { canceled = true; stop = true; }

        private void Update()
        {
            if (!processing) return;
            int elapsed = (int)(Time.realtimeSinceStartup - processingStarted);
            if (elapsed == lastDisplayedSecond) return;
            lastDisplayedSecond = elapsed;
            var percent = System.Threading.Volatile.Read(ref inferencePercent);
            Progress?.Invoke((canceled ? "Canceling…" : loading ? "Getting voice ready" : "Preparing your reminder: " + percent + "%")
                + " — " + elapsed + " seconds."
                + (elapsed >= 30 ? "\nThis is taking longer than usual. You can cancel and type instead." : ""));
        }

        private IEnumerator Capture()
        {
            Progress?.Invoke("Allow microphone access when prompted.");
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                bool pending = true;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ => pending = false;
                callbacks.PermissionDenied += _ => pending = false;
                callbacks.PermissionDeniedAndDontAskAgain += _ => pending = false;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
                while (pending && !canceled) yield return null;
            }
            bool allowed = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            bool allowed = Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
            if (canceled) { Finish(null, "Recording canceled. Your text is unchanged."); yield break; }
            if (!allowed) { Finish(null, "Microphone permission is off. Enable it in device settings or type your reminder."); yield break; }
            if (Microphone.devices.Length == 0) { Finish(null, "No microphone detected. Check your device or BlueStacks microphone settings."); yield break; }
            try { recording = Microphone.Start(null, false, 20, 16000); }
            catch (Exception e) { Debug.LogWarning("Could not start microphone: " + e.Message); }
            if (recording == null) { Finish(null, "Microphone could not start."); yield break; }
            IsRecording = true;
            Progress?.Invoke("Listening… Say your reminder, then pause. Or tap Done speaking.");
            var started = Time.realtimeSinceStartup;
            var pause = new SpeechPauseDetector();
            int analyzed = 0;
            int blockFrames = recording.frequency / 10;
            var block = new float[blockFrames * recording.channels];
            while (!stop && Time.realtimeSinceStartup - started < 19.5f)
            {
                int available = Microphone.GetPosition(null);
                while (!stop && analyzed + blockFrames <= available)
                {
                    if (!recording.GetData(block, analyzed)) break;
                    analyzed += blockFrames;
                    stop = pause.Add(block, recording.frequency, recording.channels);
                }
                yield return null;
            }
            int count = Microphone.GetPosition(null);
            Microphone.End(null);
            IsRecording = false;
            if (canceled) { Release(); Finish(null, "Recording canceled. Your text is unchanged."); yield break; }
            if (count < 1600) { Release(); Finish(null, "No usable audio captured. Check your microphone and try again."); yield break; }
            var all = new float[recording.samples * recording.channels];
            int frequency = recording.frequency;
            int channels = recording.channels;
            bool read = recording.GetData(all, 0);
            Release();
            var samples = new float[count * channels];
            Array.Copy(all, samples, samples.Length);
            float peak = 0;
            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            if (!read || peak < 0.005f) { Finish(null, "The recording was silent. Check microphone input and try again."); yield break; }
            Transcribe(samples, frequency, channels);
        }

        private async void Transcribe(float[] samples, int frequency, int channels)
        {
            processing = true;
            processingStarted = Time.realtimeSinceStartup;
            lastDisplayedSecond = -1;
            inferencePercent = 0;
            Debug.Log("Whisper transcription starting: " + (samples.Length / (float)(frequency * channels)).ToString("F1") + "s audio, " + frequency + "Hz, " + SystemInfo.processorType);
            try
            {
                if (model == null)
                {
                    loading = true;
                    Progress?.Invoke("Getting voice ready for the first time…");
                    var context = WhisperContextParams.GetDefaultParams();
                    context.UseGpu = false;
                    model = await WhisperWrapper.InitFromFileAsync(Path.Combine(Application.streamingAssetsPath, "Whisper/ggml-tiny.bin"), context);
                    if (model == null) throw new InvalidOperationException("Speech model failed to load. Restore Whisper dependencies and rebuild.");
                    model.OnProgress += value => System.Threading.Volatile.Write(ref inferencePercent, value);
                }
                loading = false;
                if (!this || canceled) { Finish(null, "Voice entry canceled. Your text is unchanged."); return; }
                Progress?.Invoke("Preparing your reminder…");
                var settings = WhisperParams.GetDefaultParams();
                settings.Language = "en";
                settings.Translate = false;
                settings.NoContext = true;
                settings.ThreadsCount = Mathf.Clamp(SystemInfo.processorCount, 1, 4);
                var result = await model.GetTextAsync(samples, frequency, channels, settings);
                if (!this) return;
                if (canceled) Finish(null, "Voice entry canceled. Your text is unchanged.");
                else if (string.IsNullOrWhiteSpace(result?.Result)) Finish(null, "No words recognized. Try again or type the reminder.");
                else Finish(result.Result.Trim(), null);
            }
            catch (Exception e) { if (this) Finish(null, "Offline voice failed: " + e.Message); }
            finally { processing = false; loading = false; }
        }

        private void Finish(string text, string error)
        {
            processing = false;
            Debug.Log(error == null ? "Whisper transcription completed." : "Whisper: " + error);
            if (this) Finished?.Invoke(text, error);
        }
        private void Release() { if (recording != null) Destroy(recording); recording = null; }
        private void OnApplicationPause(bool paused) { if (paused && IsRecording) Cancel(); }
        private void OnDestroy() { canceled = true; if (IsRecording) Microphone.End(null); Release(); }
    }
}
