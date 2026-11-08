using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using Serilog;

namespace NotchWin.Services
{
    public class SoundNotificationService : ISoundNotificationService, IDisposable
    {
        private const int SampleRate = 44100;

        private SoundPlayer? _timerPlayer;
        private SoundPlayer? _waterPlayer;
        private SoundPlayer? _prayerPlayer;
        private SoundPlayer? _startupPlayer;
        private int _prayerCallDurationMs = 21722;

        public int PrayerCallDurationMs => _prayerCallDurationMs;

        public SoundNotificationService()
        {
            try
            {
                var timerWav = GenerateTimerAlarmWav();
                _timerPlayer = new SoundPlayer(new MemoryStream(timerWav));
                _timerPlayer.LoadAsync();

                var waterWav = GenerateWaterReminderWav();
                _waterPlayer = new SoundPlayer(new MemoryStream(waterWav));
                _waterPlayer.LoadAsync();

                var startupWav = GenerateStartupChimeWav();
                _startupPlayer = new SoundPlayer(new MemoryStream(startupWav));
                _startupPlayer.LoadAsync();

                InitPrayerPlayer();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize SoundNotificationService audio streams");
            }
        }

        private void InitPrayerPlayer()
        {
            try
            {
                string? adhanPath = GetAdhanSoundPath();
                if (!string.IsNullOrEmpty(adhanPath) && File.Exists(adhanPath))
                {
                    _prayerPlayer?.Dispose();
                    _prayerPlayer = new SoundPlayer(adhanPath);
                    _prayerPlayer.LoadAsync();
                    return;
                }

                var prayerWav = GeneratePrayerCallWav();
                _prayerPlayer?.Dispose();
                _prayerPlayer = new SoundPlayer(new MemoryStream(prayerWav));
                _prayerPlayer.LoadAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize prayer sound player");
            }
        }

        private static string? GetAdhanSoundPath()
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds", "adhan_takbir.wav"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "adhan_takbir.wav"),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Sounds", "adhan_takbir.wav"),
                Path.Combine(Directory.GetCurrentDirectory(), "NotchWin", "Assets", "Sounds", "adhan_takbir.wav"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "adhan_takbir.wav")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public void PlayTimerAlarm()
        {
            try
            {
                _timerPlayer?.Play();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to play timer alarm sound");
                SystemSounds.Exclamation.Play();
            }
        }

        public void PlayWaterReminder()
        {
            try
            {
                _waterPlayer?.Play();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to play water reminder sound");
                SystemSounds.Asterisk.Play();
            }
        }

        public void PlayPrayerCall()
        {
            try
            {
                if (_prayerPlayer == null)
                {
                    InitPrayerPlayer();
                }
                _prayerPlayer?.Play();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to play prayer call sound");
                SystemSounds.Asterisk.Play();
            }
        }

        public void StopPrayerCall()
        {
            try
            {
                _prayerPlayer?.Stop();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to stop prayer call sound");
            }
        }

        public void PlayStartupSound()
        {
            try
            {
                _startupPlayer?.Play();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to play startup welcome sound");
                SystemSounds.Asterisk.Play();
            }
        }

        #region Sound Generators (16-bit PCM Mono 44.1kHz)

        /// <summary>
        /// Water Droplet & Bubble Hydration Sound (~420ms)
        /// Realistic pitch-swept liquid chirp with exponential damping.
        /// </summary>
        private static byte[] GenerateWaterReminderWav()
        {
            var samples = new List<short>();

            // Drop 1: 650 Hz -> 1350 Hz over 130ms
            int drop1Len = (int)(SampleRate * 0.13);
            for (int i = 0; i < drop1Len; i++)
            {
                double t = (double)i / SampleRate;
                double frac = t / 0.13;
                double freq = 650.0 + 700.0 * (frac * frac); // rising sweep
                double env = Math.Exp(-24.0 * t);
                double val = Math.Sin(2.0 * Math.PI * freq * t) * env;
                samples.Add((short)(val * 18000));
            }

            // Short silence between drops: 60ms
            int silence1 = (int)(SampleRate * 0.06);
            for (int i = 0; i < silence1; i++) samples.Add(0);

            // Drop 2: 900 Hz -> 1750 Hz over 140ms
            int drop2Len = (int)(SampleRate * 0.14);
            for (int i = 0; i < drop2Len; i++)
            {
                double t = (double)i / SampleRate;
                double frac = t / 0.14;
                double freq = 900.0 + 850.0 * (frac * frac);
                double env = Math.Exp(-22.0 * t);
                double val = Math.Sin(2.0 * Math.PI * freq * t) * env;
                samples.Add((short)(val * 16000));
            }

            // Trailing silence: 50ms
            int silence2 = (int)(SampleRate * 0.05);
            for (int i = 0; i < silence2; i++) samples.Add(0);

            return BuildWavFile(samples, SampleRate);
        }

        /// <summary>
        /// Modern Electronic Timer Alert (~950ms)
        /// Energetic ascending alert sequence: A5 -> B5 -> D6 -> E6 (repeated).
        /// </summary>
        private static byte[] GenerateTimerAlarmWav()
        {
            var samples = new List<short>();

            void AppendBeep(double freq, double durationSec, double volume = 20000)
            {
                int len = (int)(SampleRate * durationSec);
                int attack = (int)(SampleRate * 0.005);
                int release = (int)(SampleRate * 0.015);

                for (int i = 0; i < len; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = 1.0;
                    if (i < attack) env = (double)i / attack;
                    else if (i > len - release) env = (double)(len - i) / release;

                    // Add subtle 2nd harmonic for richer chime body
                    double val = (Math.Sin(2.0 * Math.PI * freq * t) * 0.85 +
                                  Math.Sin(4.0 * Math.PI * freq * t) * 0.15) * env;

                    samples.Add((short)(val * volume));
                }
            }

            void AppendSilence(double durationSec)
            {
                int len = (int)(SampleRate * durationSec);
                for (int i = 0; i < len; i++) samples.Add(0);
            }

            // Round 1
            AppendBeep(880.0, 0.075);   // A5
            AppendSilence(0.035);
            AppendBeep(987.77, 0.075);  // B5
            AppendSilence(0.035);
            AppendBeep(1174.66, 0.090); // D6
            AppendSilence(0.035);
            AppendBeep(1318.51, 0.180); // E6

            // Break
            AppendSilence(0.120);

            // Round 2
            AppendBeep(1174.66, 0.080); // D6
            AppendSilence(0.035);
            AppendBeep(1318.51, 0.220); // E6

            AppendSilence(0.050);

            return BuildWavFile(samples, SampleRate);
        }

        /// <summary>
        /// Serene Chime for Prayer Arrival (~2.5 seconds)
        /// Meditative harmonic bell sequence: F4 -> Bb4 -> C5 -> F5 with long reverberant decay.
        /// </summary>
        private static byte[] GeneratePrayerCallWav()
        {
            var samples = new List<short>();

            void AppendHarmonicChime(double freq, double durationSec, double decayRate = 4.0, double volume = 17000)
            {
                int len = (int)(SampleRate * durationSec);
                int attack = (int)(SampleRate * 0.015);

                for (int i = 0; i < len; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = Math.Exp(-decayRate * t);
                    if (i < attack) env *= (double)i / attack;

                    // Rich acoustic bell timbre: fundamental + 2nd + 3rd + 4.2x bell shimmer
                    double val = (Math.Sin(2.0 * Math.PI * freq * t) * 0.60 +
                                  Math.Sin(2.0 * Math.PI * (freq * 2.0) * t) * 0.25 +
                                  Math.Sin(2.0 * Math.PI * (freq * 3.0) * t) * 0.10 +
                                  Math.Sin(2.0 * Math.PI * (freq * 4.2) * t) * 0.05) * env;

                    samples.Add((short)(val * volume));
                }
            }

            // Note 1: F4 (349.23 Hz)
            AppendHarmonicChime(349.23, 0.38, 5.0);

            // Note 2: Bb4 (466.16 Hz)
            AppendHarmonicChime(466.16, 0.38, 5.0);

            // Note 3: C5 (523.25 Hz)
            AppendHarmonicChime(523.25, 0.42, 4.5);

            // Note 4: F5 (698.46 Hz) with long resonant ringing tail
            AppendHarmonicChime(698.46, 1.25, 2.5);

            return BuildWavFile(samples, SampleRate);
        }

        /// <summary>
        /// Warm Welcome Chime (~1.9 seconds)
        /// Soft ascending major arpeggio (C5 -> E5 -> G5 -> C6) with overlapping bell decay.
        /// </summary>
        private static byte[] GenerateStartupChimeWav()
        {
            const double totalSeconds = 1.9;
            int totalSamples = (int)(SampleRate * totalSeconds);
            var mix = new double[totalSamples];

            void AddTone(double freq, double startSec, double durationSec, double gain)
            {
                int start = (int)(SampleRate * startSec);
                int len = (int)(SampleRate * durationSec);
                int attack = (int)(SampleRate * 0.012);

                for (int i = 0; i < len && start + i < totalSamples; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = Math.Exp(-3.8 * t);
                    if (i < attack) env *= (double)i / attack;

                    double val = (Math.Sin(2.0 * Math.PI * freq * t) * 0.72 +
                                  Math.Sin(2.0 * Math.PI * freq * 2.0 * t) * 0.20 +
                                  Math.Sin(2.0 * Math.PI * freq * 3.0 * t) * 0.08) * env;

                    mix[start + i] += val * gain;
                }
            }

            AddTone(523.25, 0.00, 1.10, 0.40);  // C5
            AddTone(659.25, 0.14, 1.05, 0.42);  // E5
            AddTone(783.99, 0.28, 1.00, 0.44);  // G5
            AddTone(1046.50, 0.42, 1.35, 0.50); // C6

            var samples = new List<short>(totalSamples);
            for (int i = 0; i < totalSamples; i++)
            {
                double v = Math.Clamp(mix[i], -1.0, 1.0);
                samples.Add((short)(v * 24000));
            }

            return BuildWavFile(samples, SampleRate);
        }

        private static byte[] BuildWavFile(IReadOnlyList<short> samples, int sampleRate)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            int subChunk2Size = samples.Count * sizeof(short);
            int chunkSize = 36 + subChunk2Size;

            // RIFF chunk descriptor
            bw.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            bw.Write(chunkSize);
            bw.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });

            // fmt sub-chunk
            bw.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            bw.Write(16); // SubChunk1Size (16 for PCM)
            bw.Write((short)1); // AudioFormat (1 = PCM)
            bw.Write((short)1); // NumChannels (1 = Mono)
            bw.Write(sampleRate); // SampleRate
            bw.Write(sampleRate * 2); // ByteRate (SampleRate * 1 * 16 / 8)
            bw.Write((short)2); // BlockAlign (NumChannels * BitsPerSample / 8)
            bw.Write((short)16); // BitsPerSample

            // data sub-chunk
            bw.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            bw.Write(subChunk2Size);

            for (int i = 0; i < samples.Count; i++)
            {
                bw.Write(samples[i]);
            }

            return ms.ToArray();
        }

        #endregion

        public void Dispose()
        {
            _timerPlayer?.Dispose();
            _waterPlayer?.Dispose();
            _prayerPlayer?.Dispose();
            _startupPlayer?.Dispose();
        }
    }
}
