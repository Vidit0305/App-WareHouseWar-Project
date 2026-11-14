using System;
using System.IO;

namespace WarehouseWar.EditorTools.Audio
{
    /// <summary>
    /// Small offline synthesiser / DSP toolkit used to generate every sound in the game from code
    /// (no samples, no third-party audio). Everything renders into float buffers at <see cref="SampleRate"/>
    /// through a <see cref="Mix"/> (dry bus + reverb send bus, mono or stereo, optionally loop-wrapped).
    ///
    /// Toolkit: band-limited oscillators, resonant state-variable filter, ADSR, detuned multi-voice notes with
    /// filter envelope / vibrato / formants (strings, brass, choir), pitch-envelope drums, modal "metal" synthesis,
    /// friction / cloth / debris / creak generators, white/pink/brown noise, saturation, Freeverb-style stereo reverb
    /// with early reflections, compressor, normaliser and a 16-bit PCM WAV writer (mono or interleaved stereo).
    /// </summary>
    internal sealed class Synth
    {
        public const int SfxRate = 44100;
        public const int MusicRate = 32000;

        /// <summary>Current render rate; recipes switch it with <see cref="SetRate"/> before building a clip.</summary>
        public static int SampleRate { get; private set; } = SfxRate;

        public static void SetRate(int rate) => SampleRate = rate;

        private const double TwoPi = Math.PI * 2.0;

        // ------------------------------------------------------------------ randomness / noise

        private readonly Random random;
        private uint state;
        private float pink0, pink1, pink2, pink3, pink4, pink5, pink6, brown;

        public Synth(int seed)
        {
            random = new Random(seed);
            unchecked
            {
                state = (uint)seed * 2654435761u + 0x9E3779B9u;
            }
            if (state == 0) state = 0x6C078965u;
        }

        /// <summary>White noise in [-1, 1) (xorshift32 - fast enough to call per sample).</summary>
        public float Noise()
        {
            unchecked
            {
                uint x = state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                state = x;
                return (int)x * 4.656613e-10f;
            }
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Uniform() => Noise() * 0.5f + 0.5f;

        public float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        public bool Chance(float probability) => random.NextDouble() < probability;

        /// <summary>Pink (-3 dB/oct) noise, Paul Kellet's filter.</summary>
        public float Pink()
        {
            float w = Noise();
            pink0 = 0.99886f * pink0 + w * 0.0555179f;
            pink1 = 0.99332f * pink1 + w * 0.0750759f;
            pink2 = 0.96900f * pink2 + w * 0.1538520f;
            pink3 = 0.86650f * pink3 + w * 0.3104856f;
            pink4 = 0.55000f * pink4 + w * 0.5329522f;
            pink5 = -0.7616f * pink5 - w * 0.0168980f;
            float p = pink0 + pink1 + pink2 + pink3 + pink4 + pink5 + pink6 + w * 0.5362f;
            pink6 = w * 0.115926f;
            return p * 0.2f;
        }

        /// <summary>Brown (-6 dB/oct) noise: leaky integrated white noise, roughly unit RMS.</summary>
        public float Brown()
        {
            brown = (brown + 0.02f * Noise()) / 1.02f;
            return brown * 10f;
        }

        public enum NoiseColor { White, Pink, Brown }

        public float NextNoise(NoiseColor color) =>
            color == NoiseColor.Pink ? Pink() : color == NoiseColor.Brown ? Brown() : Noise();

        // ------------------------------------------------------------------ small maths helpers

        /// <summary>Cheap tanh-like soft clipper (exact 1 at |x| = 3).</summary>
        public static float Tanh(float x)
        {
            if (x > 3f) return 1f;
            if (x < -3f) return -1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        public static float Midi(float note) => 440f * (float)Math.Pow(2.0, (note - 69f) / 12.0);

        public static float Db(float db) => (float)Math.Pow(10.0, db / 20.0);

        /// <summary>Constant-power pan law, pan in [-1, 1].</summary>
        public static void Pan(float pan, out float left, out float right)
        {
            if (pan < -1f) pan = -1f;
            if (pan > 1f) pan = 1f;
            double angle = (pan + 1.0) * Math.PI * 0.25;
            left = (float)Math.Cos(angle);
            right = (float)Math.Sin(angle);
        }

        /// <summary>Per-sample multiplier that decays by 1/e over <paramref name="seconds"/>.</summary>
        public static float DecayCoef(float seconds) => (float)Math.Exp(-1.0 / (Math.Max(1e-5, seconds) * SampleRate));

        // ------------------------------------------------------------------ envelopes

        /// <summary>Linear attack then exponential decay (time constant <paramref name="decay"/>).</summary>
        public static float Ar(float t, float attack, float decay)
        {
            if (t < 0f) return 0f;
            if (t < attack) return t / attack;
            return (float)Math.Exp(-(t - attack) / Math.Max(1e-5f, decay));
        }

        /// <summary>
        /// ADSR: smooth attack, exponential decay towards sustain (time constant <paramref name="d"/>),
        /// gate held for <paramref name="gate"/> seconds, then a smooth release of length <paramref name="r"/>.
        /// </summary>
        public static float Adsr(float t, float gate, float a, float d, float s, float r)
        {
            if (t < 0f) return 0f;
            float held = Held(Math.Min(t, gate), a, d, s);
            if (t <= gate) return held;
            float x = 1f - (t - gate) / Math.Max(1e-4f, r);
            return x <= 0f ? 0f : held * x * x;
        }

        private static float Held(float t, float a, float d, float s)
        {
            if (t < a)
            {
                float u = t / a;
                return u * (2f - u); // ease-out attack (smooth "bow"/"breath" onset)
            }
            return s + (1f - s) * (float)Math.Exp(-(t - a) / Math.Max(1e-4f, d));
        }

        // ------------------------------------------------------------------ oscillators

        public enum Wave { Sine, Square, Saw, Triangle }

        private static float Blep(double t, double dt)
        {
            if (t < dt)
            {
                t /= dt;
                return (float)(t + t - t * t - 1.0);
            }
            if (t > 1.0 - dt)
            {
                t = (t - 1.0) / dt;
                return (float)(t * t + t + t + 1.0);
            }
            return 0f;
        }

        /// <summary>Band-limited (polyBLEP) oscillator. <paramref name="phase"/> in [0,1), <paramref name="dt"/> = f / SampleRate.</summary>
        public static float Osc(Wave wave, double phase, double dt)
        {
            switch (wave)
            {
                case Wave.Saw:
                    return (float)(2.0 * phase - 1.0) - Blep(phase, dt);
                case Wave.Square:
                {
                    float v = phase < 0.5 ? 1f : -1f;
                    v += Blep(phase, dt);
                    double p2 = phase + 0.5;
                    if (p2 >= 1.0) p2 -= 1.0;
                    v -= Blep(p2, dt);
                    return v * 0.8f;
                }
                case Wave.Triangle:
                    return (float)(1.0 - 4.0 * Math.Abs(phase - 0.5));
                default:
                    return (float)Math.Sin(phase * TwoPi);
            }
        }

        // ------------------------------------------------------------------ filters

        public enum FilterType { Low, Band, High }

        /// <summary>
        /// Topology-preserving (Cytomic/Simper) state-variable filter: stable under fast modulation, gives LP/BP/HP at once.
        /// Band output is normalised to unity gain at the centre frequency.
        /// </summary>
        public sealed class Svf
        {
            private float ic1, ic2, a1, a2, a3, k;
            public float Low, Band, High;
            /// <summary>tan(pi fc / fs) of the current setting - handy to normalise impulse excitation.</summary>
            public float G { get; private set; }

            public Svf(float cutoff, float q = 0.7071f) => Set(cutoff, q);

            public void Set(float cutoff, float q = 0.7071f)
            {
                double fc = Math.Min(Math.Max(cutoff, 5.0), SampleRate * 0.45);
                double g = Math.Tan(Math.PI * fc / SampleRate);
                double kk = 1.0 / Math.Max(0.05, q);
                k = (float)kk;
                a1 = (float)(1.0 / (1.0 + g * (g + kk)));
                a2 = (float)(g * a1);
                a3 = (float)(g * a2);
                G = (float)g;
            }

            public float Process(float v0)
            {
                float v3 = v0 - ic2;
                float v1 = a1 * ic1 + a2 * v3;
                float v2 = ic2 + a2 * ic1 + a3 * v3;
                ic1 = 2f * v1 - ic1;
                ic2 = 2f * v2 - ic2;
                Low = v2;
                Band = v1 * k;
                High = v0 - k * v1 - v2;
                return v2;
            }

            public float Process(float v0, FilterType type)
            {
                Process(v0);
                return type == FilterType.Low ? Low : type == FilterType.Band ? Band : High;
            }
        }

        /// <summary>Filters a whole buffer in place (12 dB/oct).</summary>
        public static void Filter(float[] buffer, FilterType type, float cutoff, float q = 0.7071f)
        {
            if (buffer == null) return;
            var f = new Svf(cutoff, q);
            for (int i = 0; i < buffer.Length; i++) buffer[i] = f.Process(buffer[i], type);
        }

        // ------------------------------------------------------------------ mix bus

        /// <summary>
        /// Render target: dry bus plus a reverb-send bus, mono or stereo. With <see cref="Loop"/> writes past the end wrap
        /// around to the start (ringing notes continue seamlessly) and <see cref="Finish(Space, Action{float[], float[]}, out float[], out float[])"/>
        /// runs effects over two concatenated copies and keeps the second, so reverb/compressor state is continuous across the loop point.
        /// </summary>
        public sealed class Mix
        {
            public readonly float[] L, R, SendL, SendR;
            public readonly int Length;
            public readonly bool Loop;
            public bool Stereo => R != null;

            public Mix(float seconds, bool stereo = false, bool loop = false)
            {
                Length = Math.Max(1, (int)Math.Ceiling(seconds * SampleRate));
                Loop = loop;
                L = new float[Length];
                SendL = new float[Length];
                if (stereo)
                {
                    R = new float[Length];
                    SendR = new float[Length];
                }
            }

            /// <summary>True when a one-shot write would fall past the end (renderers stop there).</summary>
            public bool Outside(int i) => !Loop && i >= Length;

            public void Add(int i, float l, float r, float send)
            {
                if (i >= Length)
                {
                    if (!Loop) return;
                    i %= Length;
                }
                else if (i < 0)
                {
                    if (!Loop) return;
                    i = ((i % Length) + Length) % Length;
                }

                if (R == null)
                {
                    float v = (l + r) * 0.70710678f;
                    L[i] += v;
                    SendL[i] += v * send;
                }
                else
                {
                    L[i] += l;
                    R[i] += r;
                    SendL[i] += l * send;
                    SendR[i] += r * send;
                }
            }

            /// <summary>Mono render with an optional space (reverb/early reflections).</summary>
            public float[] Finish(Space space)
            {
                Finish(space, null, out float[] left, out _);
                return left;
            }

            public void Finish(Space space, Action<float[], float[]> master, out float[] left, out float[] right)
            {
                int passes = Loop ? 2 : 1;
                float[] outL = Tile(L, passes);
                float[] outR = R == null ? null : Tile(R, passes);
                if (space != null)
                {
                    float[] sendL = Tile(SendL, passes);
                    float[] sendR = SendR == null ? null : Tile(SendR, passes);
                    space.Apply(sendL, sendR, outL, outR);
                }
                master?.Invoke(outL, outR);
                left = Loop ? Slice(outL, Length) : outL;
                right = outR == null ? null : Loop ? Slice(outR, Length) : outR;
            }

            private static float[] Tile(float[] source, int passes)
            {
                var result = new float[source.Length * passes];
                for (int p = 0; p < passes; p++) Array.Copy(source, 0, result, p * source.Length, source.Length);
                return result;
            }

            private static float[] Slice(float[] source, int start)
            {
                var result = new float[source.Length - start];
                Array.Copy(source, start, result, 0, result.Length);
                return result;
            }
        }

        // ------------------------------------------------------------------ space: early reflections + reverb

        /// <summary>Acoustic space applied to the send bus: conditioning filters, early reflections, Freeverb tail.</summary>
        public sealed class Space
        {
            public float Room = 0.8f;       // 0..1 tail length
            public float Damp = 0.5f;       // 0..1 high-frequency damping
            public float Size = 1f;         // delay-line scale (bigger = sparser, larger room)
            public float Wet = 0.3f;
            public float Width = 1f;
            public float PreDelay = 0.01f;
            public float LowCut = 120f;
            public float HighCut = 8000f;
            public float[] Taps;            // early reflection delays (s)
            public float[] TapGains;
            public float TapCut = 4000f;

            public Space Clone() => (Space)MemberwiseClone();

            public void Apply(float[] sendL, float[] sendR, float[] outL, float[] outR)
            {
                Filter(sendL, FilterType.High, LowCut);
                Filter(sendL, FilterType.Low, HighCut);
                Filter(sendR, FilterType.High, LowCut);
                Filter(sendR, FilterType.Low, HighCut);
                if (Taps != null && TapGains != null)
                {
                    EarlyReflections(sendL, outL, Taps, TapGains, TapCut, 1f);
                    if (sendR != null && outR != null) EarlyReflections(sendR, outR, Taps, TapGains, TapCut, 1.09f);
                }
                if (Wet > 0f) new Reverb(Room, Damp, Size).Process(sendL, sendR, outL, outR, Wet, Width, PreDelay);
            }
        }

        /// <summary>Adds a low-passed multi-tap echo of <paramref name="input"/> to <paramref name="output"/> (wall slap-back).</summary>
        public static void EarlyReflections(float[] input, float[] output, float[] taps, float[] gains, float cutoff, float timeScale)
        {
            int count = Math.Min(taps.Length, gains.Length);
            var delays = new int[count];
            for (int k = 0; k < count; k++) delays[k] = Math.Max(1, (int)(taps[k] * timeScale * SampleRate));
            float a = 1f - (float)Math.Exp(-TwoPi * cutoff / SampleRate);
            float lp = 0f;
            int n = Math.Min(input.Length, output.Length);
            for (int i = 0; i < n; i++)
            {
                float acc = 0f;
                for (int k = 0; k < count; k++)
                {
                    int j = i - delays[k];
                    if (j >= 0) acc += input[j] * gains[k];
                }
                lp += (acc - lp) * a;
                output[i] += lp;
            }
        }

        /// <summary>Freeverb (Schroeder/Moorer): 8 damped feedback combs + 4 series allpasses per channel, stereo-spread.</summary>
        public sealed class Reverb
        {
            private static readonly int[] CombTuning = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
            private static readonly int[] AllpassTuning = { 556, 441, 341, 225 };
            private const int StereoSpread = 23;
            private const float FixedGain = 0.015f;

            private readonly float[][] combL = new float[8][], combR = new float[8][];
            private readonly float[][] apL = new float[4][], apR = new float[4][];
            private readonly int[] combIdxL = new int[8], combIdxR = new int[8], apIdxL = new int[4], apIdxR = new int[4];
            private readonly float[] storeL = new float[8], storeR = new float[8];
            private readonly float feedback, damp1, damp2;

            public Reverb(float room, float damping, float size = 1f)
            {
                double scale = SampleRate / 44100.0 * Math.Max(0.2, size);
                for (int c = 0; c < 8; c++)
                {
                    combL[c] = new float[Math.Max(1, (int)(CombTuning[c] * scale))];
                    combR[c] = new float[Math.Max(1, (int)((CombTuning[c] + StereoSpread) * scale))];
                }
                for (int a = 0; a < 4; a++)
                {
                    apL[a] = new float[Math.Max(1, (int)(AllpassTuning[a] * scale))];
                    apR[a] = new float[Math.Max(1, (int)((AllpassTuning[a] + StereoSpread) * scale))];
                }
                feedback = Math.Min(0.985f, room * 0.28f + 0.7f);
                damp1 = damping * 0.4f;
                damp2 = 1f - damp1;
            }

            /// <summary>Adds the wet signal of (inL[, inR]) to (outL[, outR]). Pass nulls for the right channel to run mono.</summary>
            public void Process(float[] inL, float[] inR, float[] outL, float[] outR, float wet, float width, float preDelay)
            {
                bool stereo = inR != null && outR != null;
                int pre = Math.Max(0, (int)(preDelay * SampleRate));
                float wet1 = wet * 3f * (width * 0.5f + 0.5f);
                float wet2 = wet * 3f * ((1f - width) * 0.5f);
                int n = outL.Length;
                for (int i = 0; i < n; i++)
                {
                    int j = i - pre;
                    float input = 0f;
                    if (j >= 0) input = stereo ? (inL[j] + inR[j]) * FixedGain : inL[j] * (2f * FixedGain);

                    float l = 0f;
                    for (int c = 0; c < 8; c++) l += Comb(combL[c], ref combIdxL[c], ref storeL[c], input);
                    for (int a = 0; a < 4; a++) l = Allpass(apL[a], ref apIdxL[a], l);

                    if (!stereo)
                    {
                        outL[i] += l * wet * 3f;
                        continue;
                    }

                    float r = 0f;
                    for (int c = 0; c < 8; c++) r += Comb(combR[c], ref combIdxR[c], ref storeR[c], input);
                    for (int a = 0; a < 4; a++) r = Allpass(apR[a], ref apIdxR[a], r);

                    outL[i] += l * wet1 + r * wet2;
                    outR[i] += r * wet1 + l * wet2;
                }
            }

            private float Comb(float[] buffer, ref int index, ref float store, float input)
            {
                float output = buffer[index];
                store = output * damp2 + store * damp1 + 1e-25f;
                buffer[index] = input + store * feedback;
                if (++index >= buffer.Length) index = 0;
                return output;
            }

            private static float Allpass(float[] buffer, ref int index, float input)
            {
                float buffered = buffer[index];
                buffer[index] = input + buffered * 0.5f;
                if (++index >= buffer.Length) index = 0;
                return buffered - input;
            }
        }

        // ------------------------------------------------------------------ dynamics / mastering

        /// <summary>Stereo-linked feed-forward peak compressor with soft knee (pass null for <paramref name="right"/> on mono).</summary>
        public static void Compress(float[] left, float[] right, float thresholdDb, float ratio, float attack, float release, float makeupDb = 0f, float kneeDb = 6f)
        {
            float ac = DecayCoef(attack), rc = DecayCoef(release);
            float slope = 1f - 1f / Math.Max(1f, ratio);
            float reduction = 0f;
            for (int i = 0; i < left.Length; i++)
            {
                float peak = Math.Abs(left[i]);
                if (right != null) peak = Math.Max(peak, Math.Abs(right[i]));
                float levelDb = 20f * (float)Math.Log10(peak + 1e-9f);
                float over = levelDb - thresholdDb;
                float target;
                if (over <= -kneeDb * 0.5f) target = 0f;
                else if (over >= kneeDb * 0.5f) target = over * slope;
                else
                {
                    float x = over + kneeDb * 0.5f;
                    target = slope * x * x / (2f * kneeDb);
                }
                reduction = target > reduction ? ac * reduction + (1f - ac) * target : rc * reduction + (1f - rc) * target;
                float gain = (float)Math.Pow(10.0, (makeupDb - reduction) / 20f);
                left[i] *= gain;
                if (right != null) right[i] *= gain;
            }
        }

        /// <summary>Soft saturation: y = tanh(x * (1 + drive)) / tanh(1 + drive).</summary>
        public static void Saturate(float[] buffer, float drive)
        {
            if (buffer == null || drive <= 0f) return;
            float k = 1f + drive, norm = 1f / Tanh(k);
            for (int i = 0; i < buffer.Length; i++) buffer[i] = Tanh(buffer[i] * k) * norm;
        }

        /// <summary>Scales to unit peak, optionally soft-clips (adds density/loudness), then scales to <paramref name="peak"/>.</summary>
        public static void Normalize(float[] buffer, float peak = 0.9f, float drive = 0f) => Normalize(buffer, null, peak, drive);

        public static void Normalize(float[] left, float[] right, float peak, float drive)
        {
            float max = MaxAbs(left, right);
            if (max < 1e-9f) return;
            if (drive <= 0f)
            {
                Scale(left, right, peak / max);
                return;
            }
            Scale(left, right, 1f / max);
            Saturate(left, drive);
            Saturate(right, drive);
            max = MaxAbs(left, right);
            if (max < 1e-9f) return;
            Scale(left, right, peak / max);
        }

        public static float MaxAbs(float[] left, float[] right = null)
        {
            float max = 0f;
            for (int i = 0; i < left.Length; i++) max = Math.Max(max, Math.Abs(left[i]));
            if (right != null) for (int i = 0; i < right.Length; i++) max = Math.Max(max, Math.Abs(right[i]));
            return max;
        }

        private static void Scale(float[] left, float[] right, float gain)
        {
            for (int i = 0; i < left.Length; i++) left[i] *= gain;
            if (right != null) for (int i = 0; i < right.Length; i++) right[i] *= gain;
        }

        public static void FadeOut(float[] buffer, float seconds)
        {
            int length = Math.Min(buffer.Length, (int)(seconds * SampleRate));
            for (int i = 0; i < length; i++)
            {
                float x = i / (float)length;
                buffer[buffer.Length - 1 - i] *= x * x;
            }
        }

        public static void FadeIn(float[] buffer, float seconds)
        {
            int length = Math.Min(buffer.Length, (int)(seconds * SampleRate));
            for (int i = 0; i < length; i++) buffer[i] *= i / (float)length;
        }

        // ------------------------------------------------------------------ tonal building blocks

        /// <summary>
        /// Sine/wave burst whose pitch glides exponentially from <paramref name="f0"/> to <paramref name="f1"/>
        /// (time constant <paramref name="glide"/>) - the core of kicks, war drums, body thumps and booms.
        /// </summary>
        public static void Tone(Mix m, float start, float duration, float f0, float f1, float glide, float gain,
            float attack, float decay, Wave wave = Wave.Sine, float drive = 0f, float pan = 0f, float send = 0f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            int n = (int)(duration * sr);
            int att = Math.Max(1, (int)(attack * sr));
            int tail = Math.Max(1, Math.Min(n / 4, (int)(0.004f * sr)));
            Pan(pan, out float gl, out float gr);
            float glideK = DecayCoef(glide), decK = DecayCoef(decay);
            float pitchEnv = 1f, env = 1f, df = f0 - f1;
            float k = 1f + drive, norm = drive > 0f ? 1f / Tanh(k) : 1f;
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = begin + i;
                if (m.Outside(idx)) break;
                float a;
                if (i < att) a = i / (float)att;
                else
                {
                    env *= decK;
                    a = env;
                    if (a < 2e-4f) break;
                }
                double f = f1 + df * pitchEnv;
                pitchEnv *= glideK;
                double dt = f / sr;
                phase += dt;
                if (phase >= 1.0) phase -= 1.0;
                float s = Osc(wave, phase, dt);
                if (drive > 0f) s = Tanh(s * k) * norm;
                float fade = n - i < tail ? (n - i) / (float)tail : 1f;
                float v = s * a * gain * fade;
                m.Add(idx, v * gl, v * gr, send);
            }
        }

        /// <summary>
        /// Enveloped noise through a resonant filter whose cutoff glides from <paramref name="cutStart"/> to <paramref name="cutEnd"/>.
        /// White noise is level-compensated for the filter bandwidth so gains stay comparable across cutoffs.
        /// </summary>
        public void NoiseHit(Mix m, float start, float duration, float gain, float attack, float decay,
            FilterType type, float cutStart, float cutEnd, float cutTime, float q = 0.7071f,
            NoiseColor color = NoiseColor.White, float drive = 0f, float pan = 0f, float send = 0f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            int n = (int)(duration * sr);
            int att = Math.Max(1, (int)(attack * sr));
            int tail = Math.Max(1, Math.Min(n / 4, (int)(0.003f * sr)));
            Pan(pan, out float gl, out float gr);
            var filter = new Svf(cutStart, q);
            float cutK = DecayCoef(cutTime), decK = DecayCoef(decay);
            float cutEnv = 1f, env = 1f, delta = cutStart - cutEnd;
            float k = 1f + drive;

            float comp = 1f;
            if (color == NoiseColor.White)
            {
                float nyquist = sr * 0.5f;
                if (type == FilterType.Low) comp = (float)Math.Sqrt(nyquist / Math.Max(50f, cutStart));
                else if (type == FilterType.Band) comp = (float)Math.Sqrt(nyquist / Math.Max(50f, cutStart / Math.Max(0.3f, q)));
                comp = Math.Min(comp, 8f);
            }

            for (int i = 0; i < n; i++)
            {
                int idx = begin + i;
                if (m.Outside(idx)) break;
                float a;
                if (i < att) a = i / (float)att;
                else
                {
                    env *= decK;
                    a = env;
                    if (a < 2e-4f) break;
                }
                if ((i & 7) == 0) filter.Set(cutEnd + delta * cutEnv, q);
                cutEnv *= cutK;
                float y = filter.Process(NextNoise(color), type) * comp;
                if (drive > 0f) y = Tanh(y * k);
                float fade = n - i < tail ? (n - i) / (float)tail : 1f;
                float v = y * a * gain * fade;
                m.Add(idx, v * gl, v * gr, send);
            }
        }

        /// <summary>
        /// Modal synthesis: a sum of exponentially decaying sines at (inharmonic) <paramref name="ratios"/> of <paramref name="frequency"/>,
        /// optionally excited by a short high-passed noise strike. Metal parts, helmets, plates, timpani, bells.
        /// </summary>
        public void Modal(Mix m, float start, float frequency, float[] ratios, float[] amps, float[] decays, float gain,
            float pan = 0f, float send = 0f, float jitter = 0f, float strike = 0f, float maxDuration = 4f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            Pan(pan, out float gl, out float gr);
            int count = Math.Min(ratios.Length, Math.Min(amps.Length, decays.Length));
            for (int p = 0; p < count; p++)
            {
                double f = frequency * ratios[p] * (1.0 + jitter * Noise());
                if (f <= 0.0 || f >= sr * 0.45) continue;
                double w = TwoPi * f / sr;
                double c = 2.0 * Math.Cos(w);
                double y1 = -Math.Sin(w), y2 = -Math.Sin(2.0 * w);
                float dk = DecayCoef(decays[p]);
                float env = amps[p] * gain;
                int n = (int)(Math.Min(maxDuration, decays[p] * 7f) * sr);
                for (int i = 0; i < n; i++)
                {
                    int idx = begin + i;
                    if (m.Outside(idx)) break;
                    double y = c * y1 - y2;
                    y2 = y1;
                    y1 = y;
                    float v = (float)y * env;
                    env *= dk;
                    m.Add(idx, v * gl, v * gr, send);
                }
            }
            if (strike > 0f)
                NoiseHit(m, start, 0.012f, gain * strike, 0.0001f, 0.0015f, FilterType.High, frequency * 0.8f, frequency * 0.8f, 1f, 0.7071f, NoiseColor.White, 0f, pan, send);
        }

        /// <summary>Short metallic click (free-bar modes) with an optional low knock - triggers, latches, bolts, switches.</summary>
        public void MetalClick(Mix m, float start, float frequency, float gain, float decay = 0.025f, float body = 0.3f, float pan = 0f, float send = 0.2f)
        {
            float f = frequency * Range(0.97f, 1.03f);
            Modal(m, start, f, BarRatios, BarAmps, new[] { decay, decay * 0.7f, decay * 0.5f, decay * 0.35f, decay * 0.25f }, gain, pan, send, 0.01f, 0.45f, 1f);
            if (body > 0f) Tone(m, start, 0.08f, f * 0.11f, f * 0.075f, 0.01f, gain * body, 0.0004f, Math.Max(0.006f, decay * 0.45f), Wave.Sine, 0.5f, pan, send);
        }

        private static readonly float[] BarRatios = { 1f, 2.756f, 5.404f, 8.933f, 13.34f };
        private static readonly float[] BarAmps = { 1f, 0.7f, 0.5f, 0.32f, 0.18f };

        /// <summary>Heavy mechanical "seat": low body thud + muffled mid noise (magazine seating, pump closing, body hitting gear).</summary>
        public void Thunk(Mix m, float start, float gain, float pitch = 1f, float pan = 0f, float send = 0.2f)
        {
            Tone(m, start, 0.15f, 170f * pitch, 95f * pitch, 0.012f, gain, 0.0006f, 0.03f, Wave.Sine, 0.8f, pan, send);
            NoiseHit(m, start, 0.08f, gain * 0.45f, 0.0004f, 0.014f, FilterType.Low, 1800f * pitch, 500f, 0.02f, 0.9f, NoiseColor.White, 0.5f, pan, send);
        }

        /// <summary>Grainy random impulses into a re-tuned resonant band-pass: debris, grit, crunch.</summary>
        public void Crackle(Mix m, float start, float duration, float density, float gain, float decay, float lowFreq, float highFreq, float pan = 0f, float send = 0f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            int n = (int)(duration * sr);
            Pan(pan, out float gl, out float gr);
            var filter = new Svf(lowFreq, 2f);
            float norm = 1f / Math.Max(0.01f, filter.G);
            float decK = DecayCoef(decay), env = 1f, perSample = density / sr;
            int tail = Math.Max(1, Math.Min(n / 4, (int)(0.005f * sr)));
            for (int i = 0; i < n; i++)
            {
                int idx = begin + i;
                if (m.Outside(idx)) break;
                float x = 0f;
                if (Uniform() < perSample * env)
                {
                    filter.Set(Range(lowFreq, highFreq), Range(1.2f, 4f));
                    norm = 1f / Math.Max(0.01f, filter.G);
                    x = (Noise() >= 0f ? 1f : -1f) * Range(0.25f, 1f) * env;
                }
                filter.Process(x);
                float fade = n - i < tail ? (n - i) / (float)tail : 1f;
                float v = filter.Band * norm * 0.25f * gain * fade;
                env *= decK;
                m.Add(idx, v * gl, v * gr, send);
            }
        }

        /// <summary>Friction slide (magazine in/out, bolt travel): rough-modulated band-passed noise with stick-slip ticks.</summary>
        public void Scrape(Mix m, float start, float duration, float gain, float freqStart, float freqEnd, float q = 1.5f,
            float roughness = 0.5f, float pan = 0f, float send = 0.15f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            int n = (int)(duration * sr);
            Pan(pan, out float gl, out float gr);
            var bp = new Svf(freqStart, q);
            int edge = Math.Max(1, Math.Min(n / 3, (int)(0.008f * sr)));
            float rough = 0.5f, roughA = 1f - (float)Math.Exp(-TwoPi * 70.0 / sr);
            float tickChance = 300f * roughness / sr;
            double ratio = freqEnd / Math.Max(1f, freqStart);
            for (int i = 0; i < n; i++)
            {
                int idx = begin + i;
                if (m.Outside(idx)) break;
                float u = i / (float)n;
                if ((i & 15) == 0) bp.Set(freqStart * (float)Math.Pow(ratio, u), q);
                rough += (Math.Abs(Noise()) - rough) * roughA;
                float amp = (1f - roughness) + roughness * rough * 2.2f;
                float x = Noise() * amp;
                if (Uniform() < tickChance) x += (Noise() >= 0f ? 3f : -3f);
                float y = bp.Process(x, FilterType.Band);
                float w = Math.Min(1f, Math.Min(i / (float)edge, (n - i) / (float)edge));
                float v = y * w * gain;
                m.Add(idx, v * gl, v * gr, send);
            }
        }

        /// <summary>Cloth / gear rustle: crinkly, burst-modulated band-passed noise with a bell envelope.</summary>
        public void Rustle(Mix m, float start, float duration, float gain, float freqStart = 900f, float freqEnd = 2200f, float pan = 0f, float send = 0.1f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            int n = (int)(duration * sr);
            Pan(pan, out float gl, out float gr);
            var bp = new Svf(freqStart, 0.8f);
            var hp = new Svf(400f, 0.7f);
            float crinkle = 0f, crA = 1f - (float)Math.Exp(-TwoPi * 25.0 / sr);
            double ratio = freqEnd / Math.Max(1f, freqStart);
            for (int i = 0; i < n; i++)
            {
                int idx = begin + i;
                if (m.Outside(idx)) break;
                float u = i / (float)n;
                if ((i & 15) == 0) bp.Set(freqStart * (float)Math.Pow(ratio, u), 0.8f);
                crinkle += (Math.Abs(Noise()) - crinkle) * crA;
                float c = crinkle * 2f;
                float bell = (float)Math.Sin(Math.PI * u);
                float y = hp.Process(bp.Process(Noise(), FilterType.Band), FilterType.High);
                float v = y * bell * (0.35f + c * c * 1.3f) * gain;
                m.Add(idx, v * gl, v * gr, send);
            }
        }

        /// <summary>Stick-slip metal creak: an irregular pulse train exciting a few sharp resonances.</summary>
        public void Creak(Mix m, float start, float duration, float gain, float rateStart, float rateEnd, float[] resonances,
            float q = 14f, float pan = 0f, float send = 0.3f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            int n = (int)(duration * sr);
            Pan(pan, out float gl, out float gr);
            var bank = new Svf[resonances.Length];
            for (int r = 0; r < bank.Length; r++) bank[r] = new Svf(resonances[r], q);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = begin + i;
                if (m.Outside(idx)) break;
                float u = i / (float)n;
                float rate = (rateStart + (rateEnd - rateStart) * u) * (1f + 0.35f * Noise());
                phase += rate / sr;
                float x = 0f;
                if (phase >= 1.0)
                {
                    phase -= 1.0;
                    x = Range(0.4f, 1f);
                }
                float y = 0f;
                for (int r = 0; r < bank.Length; r++)
                {
                    bank[r].Process(x);
                    y += bank[r].Band / (r + 1f);
                }
                float bell = (float)Math.Sin(Math.PI * u);
                float v = y * bell * gain;
                m.Add(idx, v * gl, v * gr, send);
            }
        }

        /// <summary>Pea whistle (trench whistle): fast irregular frequency warble plus breath noise.</summary>
        public void Whistle(Mix m, float start, float duration, float frequency, float gain, float trillRate = 32f, float trillDepth = 0.035f,
            float pan = 0f, float send = 0.3f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            int n = (int)((duration + 0.08f) * sr);
            Pan(pan, out float gl, out float gr);
            var breath = new Svf(frequency, 5f);
            double phase = 0, trill = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = begin + i;
                if (m.Outside(idx)) break;
                float t = i / (float)sr;
                float env = Adsr(t, duration, 0.025f, 0.2f, 0.8f, 0.08f);
                trill += rateJitter(trillRate) / sr;
                float tr = (float)Math.Sin(trill * TwoPi);
                tr = Tanh(tr * 2.5f); // pea rattle: squarish
                float onset = 0.93f + 0.07f * (1f - (float)Math.Exp(-t / 0.04f));
                double f = frequency * onset * (1.0 + trillDepth * tr);
                phase += f / sr;
                if (phase >= 1.0) phase -= 1.0;
                float tone = (float)Math.Sin(phase * TwoPi) + 0.18f * (float)Math.Sin(phase * 2.0 * TwoPi);
                float air = breath.Process(Noise(), FilterType.Band) * 0.35f;
                float v = (tone + air) * env * (0.85f + 0.15f * tr) * gain;
                m.Add(idx, v * gl, v * gr, send);
            }

            float rateJitter(float r) => r * (1f + 0.25f * Noise());
        }

        /// <summary>Ricochet whine: a tumbling-bullet tone sweeping down in pitch (Doppler) with rough amplitude wobble.</summary>
        public void Ricochet(Mix m, float start, float duration, float f0, float f1, float gain, float pan = 0f, float send = 0.3f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            int n = (int)(duration * sr);
            var bp = new Svf(f0, 6f);
            double phase = 0, wob = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = begin + i;
                if (m.Outside(idx)) break;
                float u = i / (float)n;
                float t = i / (float)sr;
                float f = f1 + (f0 - f1) * (float)Math.Pow(1f - u, 1.6f);
                wob += (60f + 25f * Noise()) / sr;
                float vib = 1f + 0.02f * (float)Math.Sin(wob * TwoPi);
                phase += f * vib / sr;
                if (phase >= 1.0) phase -= 1.0;
                if ((i & 15) == 0) bp.Set(f, 6f);
                float tone = (float)Math.Sin(phase * TwoPi) + 0.25f * (float)Math.Sin(phase * 2.0 * TwoPi);
                float hiss = bp.Process(Noise(), FilterType.Band) * 0.4f;
                float env = Math.Min(1f, t / 0.008f) * (float)Math.Pow(1f - u, 1.3f);
                float am = 0.75f + 0.25f * (float)Math.Sin(wob * TwoPi * 0.5);
                // Doppler pan sweep: passes from one side to the other.
                Pan(pan + (u - 0.5f) * 0.8f, out float gl, out float gr);
                float v = (tone + hiss) * env * am * gain;
                m.Add(idx, v * gl, v * gr, send);
            }
        }

        // ------------------------------------------------------------------ percussion instruments

        /// <summary>Taiko / war drum: deep pitch-dropping membrane, second mode, stick slap and low-frequency air.</summary>
        public void Taiko(Mix m, float start, float gain, float pitch = 1f, float pan = 0f, float send = 0.3f)
        {
            Tone(m, start, 1.4f, 155f * pitch, 52f * pitch, 0.035f, gain, 0.0015f, 0.34f, Wave.Sine, 0.9f, pan, send);
            Tone(m, start, 0.4f, 265f * pitch, 108f * pitch, 0.025f, gain * 0.3f, 0.001f, 0.08f, Wave.Sine, 0f, pan, send);
            NoiseHit(m, start, 0.15f, gain * 0.3f, 0.0008f, 0.022f, FilterType.Low, 2600f * pitch, 300f, 0.02f, 0.7f, NoiseColor.White, 0f, pan, send);
            NoiseHit(m, start, 0.8f, gain * 0.3f, 0.004f, 0.2f, FilterType.Low, 240f * pitch, 90f, 0.15f, 0.8f, NoiseColor.Brown, 0f, pan, send);
        }

        /// <summary>Cinematic sub boom / impact.</summary>
        public void Boom(Mix m, float start, float gain, float pan = 0f, float send = 0.4f)
        {
            Tone(m, start, 3f, 72f, 27f, 0.13f, gain, 0.003f, 0.85f, Wave.Sine, 0.7f, pan, send);
            NoiseHit(m, start, 2.5f, gain * 0.45f, 0.006f, 0.55f, FilterType.Low, 320f, 60f, 0.4f, 0.7f, NoiseColor.Brown, 0.4f, pan, send);
            NoiseHit(m, start, 0.25f, gain * 0.3f, 0.001f, 0.04f, FilterType.Low, 1600f, 200f, 0.05f, 0.7f, NoiseColor.White, 0f, pan, send);
        }

        /// <summary>Tight military field snare: shell tone, bright wires and stick snap.</summary>
        public void Snare(Mix m, float start, float gain, float pan = 0f, float send = 0.25f, float length = 1f)
        {
            Tone(m, start, 0.2f, 245f, 188f, 0.01f, gain * 0.45f, 0.0008f, 0.045f * length, Wave.Triangle, 0f, pan, send);
            Tone(m, start, 0.12f, 345f, 330f, 0.01f, gain * 0.18f, 0.0008f, 0.025f * length, Wave.Sine, 0f, pan, send);
            NoiseHit(m, start, 0.4f, gain * 0.5f, 0.0006f, 0.1f * length, FilterType.High, 2200f, 1600f, 0.08f, 0.7f, NoiseColor.White, 0.6f, pan, send);
            NoiseHit(m, start, 0.08f, gain * 0.25f, 0.0004f, 0.016f, FilterType.Band, 5200f, 3800f, 0.02f, 0.9f, NoiseColor.White, 0f, pan, send);
        }

        private static readonly float[] TimpaniRatios = { 1f, 1.504f, 1.742f, 2.0f, 2.245f, 2.494f };
        private static readonly float[] TimpaniAmps = { 1f, 0.5f, 0.25f, 0.3f, 0.15f, 0.1f };
        private static readonly float[] TimpaniDecays = { 1.3f, 0.8f, 0.5f, 0.55f, 0.35f, 0.3f };

        public void Timpani(Mix m, float start, float frequency, float gain, float decayScale = 1f, float pan = -0.25f, float send = 0.35f)
        {
            var decays = new float[TimpaniDecays.Length];
            for (int i = 0; i < decays.Length; i++) decays[i] = TimpaniDecays[i] * decayScale;
            Modal(m, start, frequency, TimpaniRatios, TimpaniAmps, decays, gain * 0.7f, pan, send, 0.002f);
            Tone(m, start, 0.5f, frequency * 1.12f, frequency, 0.03f, gain * 0.45f, 0.002f, 0.18f * decayScale, Wave.Sine, 0.3f, pan, send);
            NoiseHit(m, start, 0.08f, gain * 0.25f, 0.0006f, 0.015f, FilterType.Low, 1200f, 300f, 0.02f, 0.7f, NoiseColor.White, 0f, pan, send);
        }

        /// <summary>Crash cymbal: bright decaying noise plus a cloud of inharmonic partials.</summary>
        public void Cymbal(Mix m, float start, float gain, float decay = 1.4f, float pan = 0f, float send = 0.35f)
        {
            NoiseHit(m, start, decay * 4f, gain * 0.5f, 0.0008f, decay, FilterType.High, 7000f, 4000f, decay * 0.4f, 0.6f, NoiseColor.White, 0f, pan, send);
            NoiseHit(m, start, decay * 2f, gain * 0.22f, 0.001f, decay * 0.45f, FilterType.Band, 3400f, 2600f, 0.4f, 1.4f, NoiseColor.White, 0f, pan, send);
            const int partials = 14;
            var ratios = new float[partials];
            var amps = new float[partials];
            var decays = new float[partials];
            for (int i = 0; i < partials; i++)
            {
                ratios[i] = Range(1f, 18f);
                amps[i] = Range(0.05f, 0.14f);
                decays[i] = decay * Range(0.25f, 0.9f);
            }
            Modal(m, start, 420f, ratios, amps, decays, gain, pan, send);
        }

        /// <summary>"Reverse cymbal" swell that ends abruptly at <paramref name="end"/> - the classic pre-impact suck-in.</summary>
        public void ReverseSwell(Mix m, float end, float duration, float gain, float pan = 0f, float send = 0.4f)
        {
            int sr = SampleRate;
            int n = (int)(duration * sr);
            int begin = (int)(end * sr) - n;
            Pan(pan, out float gl, out float gr);
            var hp = new Svf(1500f, 0.7f);
            float tau = duration * 0.28f;
            int tail = Math.Max(1, (int)(0.006f * sr));
            for (int i = 0; i < n; i++)
            {
                float t = (i - n) / (float)sr; // negative, reaches 0 at the end
                if ((i & 15) == 0) hp.Set(1500f + 5000f * (i / (float)n), 0.7f);
                float env = (float)Math.Exp(t / tau);
                float fade = n - i < tail ? (n - i) / (float)tail : 1f;
                float v = hp.Process(Noise(), FilterType.High) * env * fade * gain;
                m.Add(begin + i, v * gl, v * gr, send);
            }
        }

        /// <summary>Noise riser: band-pass sweeping up with a growing envelope.</summary>
        public void Riser(Mix m, float start, float duration, float gain, float pan = 0f, float send = 0.5f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            int n = (int)(duration * sr);
            Pan(pan, out float gl, out float gr);
            var bp = new Svf(250f, 2.5f);
            int tail = Math.Max(1, (int)(0.01f * sr));
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                if ((i & 15) == 0) bp.Set(250f * (float)Math.Pow(24.0, u), 2.5f);
                float fade = n - i < tail ? (n - i) / (float)tail : 1f;
                float v = bp.Process(Noise(), FilterType.Band) * u * u * fade * gain * 2.5f;
                m.Add(begin + i, v * gl, v * gr, send);
            }
        }

        // ------------------------------------------------------------------ pitched instruments

        /// <summary>Timbre description for <see cref="Note"/> (strings, brass, choir, bass...).</summary>
        public sealed class Patch
        {
            public Wave Wave = Wave.Saw;
            public int Voices = 1;            // unison voices
            public float Detune;              // total detune spread across voices, cents
            public float Spread;              // stereo spread of the voices, 0..1
            public float Attack = 0.01f, Decay = 0.3f, Sustain = 0.8f, Release = 0.2f;
            public float Cutoff;              // low-pass cutoff (0 = off)
            public float Resonance = 0.7071f;
            public float FilterEnv;           // cutoff * (1 + FilterEnv * amplitude envelope)
            public float KeyTrack;            // cutoff *= (f / 220)^KeyTrack
            public float HighPass;            // 0 = off
            public float Drive;               // pre-filter saturation
            public float Sub;                 // sine one octave down
            public float Breath;              // noise mixed into the source
            public float VibratoRate = 5f, VibratoDepth, VibratoDelay = 0.3f; // depth in cents
            public float[] Formants, FormantGains; // parallel band-passes (choir vowels)
            public float FormantQ = 7f;

            public Patch Clone() => (Patch)MemberwiseClone();
        }

        /// <summary>
        /// Plays one note: detuned unison oscillators (supersaw/ensemble) with stereo spread, optional sub/breath,
        /// saturation, formant bank or resonant low-pass with envelope, ADSR and delayed vibrato.
        /// </summary>
        public void Note(Mix m, float start, float length, float frequency, float gain, Patch p, float pan = 0f, float send = 0.25f)
        {
            int sr = SampleRate;
            int begin = (int)(start * sr);
            int total = (int)((length + p.Release) * sr) + 1;
            int voices = Math.Max(1, p.Voices);
            bool stereo = m.Stereo;

            var phase = new double[voices];
            var inc = new double[voices];
            var vl = new float[voices];
            var vr = new float[voices];
            for (int v = 0; v < voices; v++)
            {
                float x = voices == 1 ? 0f : v / (float)(voices - 1) * 2f - 1f;
                double cents = x * p.Detune * 0.5;
                inc[v] = frequency * Math.Pow(2.0, cents / 1200.0) / sr;
                phase[v] = random.NextDouble();
                Pan(pan + x * p.Spread, out vl[v], out vr[v]);
                if (!stereo)
                {
                    vl[v] = (vl[v] + vr[v]) * 0.70710678f;
                    vr[v] = 0f;
                }
            }
            float norm = 1f / (float)Math.Sqrt(voices);
            Pan(pan, out float cl, out float cr);
            if (!stereo)
            {
                cl = (cl + cr) * 0.70710678f;
                cr = 0f;
            }

            float baseCut = p.Cutoff > 0f ? p.Cutoff * (p.KeyTrack > 0f ? (float)Math.Pow(frequency / 220f, p.KeyTrack) : 1f) : 0f;
            Svf lpL = baseCut > 0f ? new Svf(baseCut, p.Resonance) : null;
            Svf lpR = baseCut > 0f && stereo ? new Svf(baseCut, p.Resonance) : null;
            Svf hpL = p.HighPass > 0f ? new Svf(p.HighPass) : null;
            Svf hpR = p.HighPass > 0f && stereo ? new Svf(p.HighPass) : null;

            int formants = p.Formants != null ? p.Formants.Length : 0;
            Svf[] fL = null, fR = null;
            if (formants > 0)
            {
                fL = new Svf[formants];
                fR = stereo ? new Svf[formants] : null;
                for (int k = 0; k < formants; k++)
                {
                    fL[k] = new Svf(p.Formants[k], p.FormantQ);
                    if (stereo) fR[k] = new Svf(p.Formants[k], p.FormantQ);
                }
            }

            float driveK = 1f + p.Drive, driveNorm = p.Drive > 0f ? 1f / Tanh(driveK) : 1f;
            double vibW = TwoPi * p.VibratoRate / sr;
            double subPhase = 0, subInc = frequency * 0.5 / sr;

            for (int i = 0; i < total; i++)
            {
                int idx = begin + i;
                if (m.Outside(idx)) break;
                float t = i / (float)sr;
                float env = Adsr(t, length, p.Attack, p.Decay, p.Sustain, p.Release);
                if (env <= 0f && t > length) break;

                float vib = 1f;
                if (p.VibratoDepth > 0f && t > p.VibratoDelay)
                {
                    float fadeIn = Math.Min(1f, (t - p.VibratoDelay) / 0.4f);
                    vib = 1f + 0.000577623f * p.VibratoDepth * fadeIn * (float)Math.Sin(vibW * i);
                }

                float sL = 0f, sR = 0f;
                for (int v = 0; v < voices; v++)
                {
                    double ph = phase[v] + inc[v] * vib;
                    if (ph >= 1.0) ph -= 1.0;
                    phase[v] = ph;
                    float o = Osc(p.Wave, ph, inc[v]);
                    sL += o * vl[v];
                    sR += o * vr[v];
                }
                sL *= norm;
                sR *= norm;

                if (p.Sub > 0f)
                {
                    subPhase += subInc * vib;
                    if (subPhase >= 1.0) subPhase -= 1.0;
                    float sub = (float)Math.Sin(subPhase * TwoPi) * p.Sub;
                    sL += sub * cl;
                    sR += sub * cr;
                }
                if (p.Breath > 0f)
                {
                    float b = Noise() * p.Breath;
                    sL += b * cl;
                    sR += b * cr;
                }
                if (p.Drive > 0f)
                {
                    sL = Tanh(sL * driveK) * driveNorm;
                    if (stereo) sR = Tanh(sR * driveK) * driveNorm;
                }

                if (formants > 0)
                {
                    float oL = 0f, oR = 0f;
                    for (int k = 0; k < formants; k++)
                    {
                        float g = p.FormantGains != null && k < p.FormantGains.Length ? p.FormantGains[k] : 1f;
                        oL += fL[k].Process(sL, FilterType.Band) * g;
                        if (stereo) oR += fR[k].Process(sR, FilterType.Band) * g;
                    }
                    sL = oL * 2.5f;
                    sR = oR * 2.5f;
                }

                if (lpL != null)
                {
                    if ((i & 15) == 0)
                    {
                        float cut = baseCut * (1f + p.FilterEnv * env);
                        lpL.Set(cut, p.Resonance);
                        lpR?.Set(cut, p.Resonance);
                    }
                    sL = lpL.Process(sL);
                    if (lpR != null) sR = lpR.Process(sR);
                }
                if (hpL != null)
                {
                    sL = hpL.Process(sL, FilterType.High);
                    if (hpR != null) sR = hpR.Process(sR, FilterType.High);
                }

                float a = env * gain;
                if (stereo) m.Add(idx, sL * a, sR * a, send);
                else m.Add(idx, sL * a * 0.70710678f, sL * a * 0.70710678f, send);
            }
        }

        // ------------------------------------------------------------------ output

        /// <summary>Writes 16-bit PCM mono.</summary>
        public static void WriteWav(string path, float[] samples, int sampleRate) => WriteWav(path, samples, null, sampleRate);

        /// <summary>Writes 16-bit PCM; stereo (interleaved) when <paramref name="right"/> is not null.</summary>
        public static void WriteWav(string path, float[] left, float[] right, int sampleRate)
        {
            int channels = right != null ? 2 : 1;
            int frames = right != null ? Math.Min(left.Length, right.Length) : left.Length;
            int dataBytes = frames * channels * 2;
            var bytes = new byte[44 + dataBytes];
            WriteAscii(bytes, 0, "RIFF");
            WriteInt(bytes, 4, 36 + dataBytes);
            WriteAscii(bytes, 8, "WAVE");
            WriteAscii(bytes, 12, "fmt ");
            WriteInt(bytes, 16, 16);
            WriteShort(bytes, 20, 1); // PCM
            WriteShort(bytes, 22, (short)channels);
            WriteInt(bytes, 24, sampleRate);
            WriteInt(bytes, 28, sampleRate * channels * 2);
            WriteShort(bytes, 32, (short)(channels * 2));
            WriteShort(bytes, 34, 16);
            WriteAscii(bytes, 36, "data");
            WriteInt(bytes, 40, dataBytes);
            int o = 44;
            for (int i = 0; i < frames; i++)
            {
                WriteShort(bytes, o, ToPcm(left[i]));
                o += 2;
                if (right != null)
                {
                    WriteShort(bytes, o, ToPcm(right[i]));
                    o += 2;
                }
            }
            File.WriteAllBytes(path, bytes);
        }

        private static short ToPcm(float sample)
        {
            if (float.IsNaN(sample)) sample = 0f;
            if (sample > 1f) sample = 1f;
            if (sample < -1f) sample = -1f;
            return (short)Math.Round(sample * 32767f);
        }

        private static void WriteAscii(byte[] b, int o, string s)
        {
            for (int i = 0; i < s.Length; i++) b[o + i] = (byte)s[i];
        }

        private static void WriteInt(byte[] b, int o, int v)
        {
            b[o] = (byte)v;
            b[o + 1] = (byte)(v >> 8);
            b[o + 2] = (byte)(v >> 16);
            b[o + 3] = (byte)(v >> 24);
        }

        private static void WriteShort(byte[] b, int o, short v)
        {
            b[o] = (byte)v;
            b[o + 1] = (byte)(v >> 8);
        }
    }
}
