using System;

namespace RememberThis
{
    // Uses audio duration, not frame time, so a slow frame cannot end speech early.
    public sealed class SpeechPauseDetector
    {
        private double voicedSeconds;
        private double quietSeconds;

        public bool Add(float[] samples, int sampleRate, int channels)
        {
            double energy = 0;
            foreach (var sample in samples) energy += sample * sample;
            double duration = samples.Length / (double)(sampleRate * channels);
            if (Math.Sqrt(energy / samples.Length) >= 0.012)
            {
                voicedSeconds += duration;
                quietSeconds = 0;
            }
            else quietSeconds += duration;
            return voicedSeconds >= 0.25 && quietSeconds >= 1.5;
        }
    }
}
