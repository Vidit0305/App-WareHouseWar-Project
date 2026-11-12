using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WarehouseDuel.EditorTools.Audio
{
    /// <summary>
    /// Tiny offline synthesiser used to generate the game's original sound effects and music.
    /// Everything works on float buffers at <see cref="SampleRate"/>; <see cref="WriteWav"/> saves 16-bit mono PCM.
    /// </summary>
    internal sealed class Synth
    {
        public const int SampleRate = 22050;

        private readonly System.Random random;

        public Synth(int seed) => random = new System.Random(seed);

        public static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

        public float Noise() => (float)(random.NextDouble() * 2.0 - 1.0);

        public float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        // ------------------------------------------------------------------ building blocks

        /// <summary>Adds white noise shaped by an exponential decay and a sweeping one-pole low-pass.</summary>
        public void AddNoiseBurst(float[] buffer, float start, float gain, float decay, float cutoffStart, float cutoffEnd, float attack = 0.002f)
        {
            int begin = (int)(start * SampleRate);
            float lp = 0f;
            for (int i = begin; i < buffer.Length; i++)
            {
                float t = (i - begin) / (float)SampleRate;
                float env = Envelope(t, attack, decay);
                if (env < 0.0005f && t > attack) break;
                float cutoff = Mathf.Lerp(cutoffEnd, cutoffStart, Mathf.Exp(-t / Mathf.Max(0.001f, decay)));
                lp = OnePole(lp, Noise(), cutoff);
                buffer[i] += lp * env * gain;
            }
        }

        /// <summary>Adds a tone whose frequency glides exponentially from <paramref name="f0"/> to <paramref name="f1"/>.</summary>
        public static void AddTone(float[] buffer, float start, float duration, float f0, float f1, float gain, float decay, Wave wave = Wave.Sine, float attack = 0.002f)
        {
            int begin = (int)(start * SampleRate);
            int end = Mathf.Min(buffer.Length, begin + (int)(duration * SampleRate));
            double phase = 0;
            for (int i = begin; i < end; i++)
            {
                float t = (i - begin) / (float)SampleRate;
                float frequency = f0 * Mathf.Pow(f1 / f0, Mathf.Clamp01(t / duration));
                phase += frequency / SampleRate;
                float tail = Mathf.Clamp01((end - i) / (0.004f * SampleRate)); // de-click the end
                buffer[i] += Oscillator(wave, phase) * Envelope(t, attack, decay) * gain * tail;
            }
        }

        /// <summary>A note with attack/sustain/release, for music.</summary>
        public static void AddNote(float[] buffer, float start, float duration, float frequency, float gain, Wave wave, float attack = 0.01f, float release = 0.08f, float cutoff = 0f)
        {
            int begin = (int)(start * SampleRate);
            int length = (int)((duration + release) * SampleRate);
            double phase = 0;
            float lp = 0f;
            for (int n = 0; n < length; n++)
            {
                int i = (begin + n) % buffer.Length; // wrap: notes ringing past the loop end continue at its start
                float t = n / (float)SampleRate;
                float env = t < attack ? t / attack : t < duration ? 1f : Mathf.Clamp01(1f - (t - duration) / release);
                phase += frequency / SampleRate;
                float sample = Oscillator(wave, phase);
                if (cutoff > 0f) sample = lp = OnePole(lp, sample, cutoff);
                buffer[i] += sample * env * gain;
            }
        }

        public enum Wave { Sine, Square, Saw, Triangle }

        public static float Oscillator(Wave wave, double phase)
        {
            float p = (float)(phase - Math.Floor(phase));
            switch (wave)
            {
                case Wave.Square: return p < 0.5f ? 0.6f : -0.6f;
                case Wave.Saw: return (p * 2f - 1f) * 0.6f;
                case Wave.Triangle: return 1f - 4f * Mathf.Abs(p - 0.5f);
                default: return Mathf.Sin(p * Mathf.PI * 2f);
            }
        }

        public static float Envelope(float t, float attack, float decay) =>
            t < attack ? t / attack : Mathf.Exp(-(t - attack) / Mathf.Max(0.0005f, decay));

        public static float OnePole(float previous, float input, float cutoffHz)
        {
            float x = Mathf.Exp(-2f * Mathf.PI * cutoffHz / SampleRate);
            return input * (1f - x) + previous * x;
        }

        /// <summary>Cheap feedback-delay "room" so shots don't sound bone dry inside the warehouse.</summary>
        public static void AddRoom(float[] buffer, float delaySeconds = 0.043f, float feedback = 0.32f, float mix = 0.35f)
        {
            int delay = (int)(delaySeconds * SampleRate);
            var wet = new float[buffer.Length];
            for (int i = delay; i < buffer.Length; i++) wet[i] = (buffer[i - delay] + wet[i - delay]) * feedback;
            float lp = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                lp = OnePole(lp, wet[i], 2500f);
                buffer[i] += lp * mix;
            }
        }

        /// <summary>Soft-clips and scales the buffer so its peak sits at <paramref name="peak"/>.</summary>
        public static void Normalize(float[] buffer, float peak = 0.89f)
        {
            float max = 0.0001f;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (float)Math.Tanh(buffer[i] * 1.2f);
                max = Mathf.Max(max, Mathf.Abs(buffer[i]));
            }
            float scale = peak / max;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= scale;
        }

        public static void FadeOut(float[] buffer, float seconds)
        {
            int length = Mathf.Min(buffer.Length, (int)(seconds * SampleRate));
            for (int i = 0; i < length; i++) buffer[buffer.Length - 1 - i] *= i / (float)length;
        }

        public static void WriteWav(string path, float[] samples)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
            foreach (float sample in samples) writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
        }
    }
}
