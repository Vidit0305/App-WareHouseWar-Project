using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WarehouseDuel.Audio;
using FilterType = WarehouseDuel.EditorTools.Audio.Synth.FilterType;
using NoiseColor = WarehouseDuel.EditorTools.Audio.Synth.NoiseColor;
using Wave = WarehouseDuel.EditorTools.Audio.Synth.Wave;

namespace WarehouseDuel.EditorTools.Audio
{
    /// <summary>
    /// Generates all of the game's audio as original WAV files from code (no third-party assets, no licensing questions):
    /// layered battlefield weapon shots and reloads, foley, combat/UI feedback, a cinematic war-theme menu loop
    /// and a battlefield ambience loop. Also creates Assets/Resources/AudioLibrary.asset referencing them.
    /// SFX render mono at 44.1 kHz; music renders at 32 kHz (menu theme stereo, ambience mono).
    /// </summary>
    internal static class AudioGenerator
    {
        public const string SfxFolder = "Assets/Audio/SFX";
        public const string MusicFolder = "Assets/Audio/Music";
        public const string LibraryPath = "Assets/Resources/AudioLibrary.asset";

        /// <summary>Weapon clip names, read by the weapon builder: Fire_{key}, Reload_{key}.</summary>
        public static readonly string[] WeaponKeys = { "Rifle", "SMG", "Shotgun", "Pistol" };

        /// <summary>One rendered clip (mono when <see cref="Right"/> is null).</summary>
        internal sealed class Clip
        {
            public readonly string Folder, Name;
            public readonly float[] Left, Right;
            public readonly int SampleRate;

            public Clip(string folder, string name, float[] left, float[] right, int sampleRate)
            {
                Folder = folder;
                Name = name;
                Left = left;
                Right = right;
                SampleRate = sampleRate;
            }

            public float Seconds => Left.Length / (float)SampleRate;
        }

        [MenuItem("Tools/Warehouse Duel/Generate Audio", priority = 40)]
        public static void GenerateFromMenu()
        {
            var report = new SetupReport();
            Generate(report);
            report.PrintToConsole("Audio generation");
        }

        public static void Generate(SetupReport report)
        {
            FolderStructure.EnsureFolder(SfxFolder);
            FolderStructure.EnsureFolder(MusicFolder);
            FolderStructure.EnsureFolder("Assets/Resources");

            var timer = System.Diagnostics.Stopwatch.StartNew();
            var written = new List<string>();
            var stereo = new HashSet<string>();
            float totalSeconds = 0f;
            RenderAll(clip =>
            {
                string path = $"{clip.Folder}/{clip.Name}.wav";
                Synth.WriteWav(path, clip.Left, clip.Right, clip.SampleRate);
                written.Add(path);
                if (clip.Right != null) stereo.Add(path);
                totalSeconds += clip.Seconds;
            });

            AssetDatabase.Refresh();
            foreach (string path in written) ConfigureImporter(path, stereo.Contains(path));

            BuildLibrary();
            report.Pass($"Generated {written.Count} original audio clips ({totalSeconds:0} s of audio in {timer.Elapsed.TotalSeconds:0.0} s) and {LibraryPath}.");
        }

        public static AudioClip Load(string folder, string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{name}.wav");

        /// <summary>Renders every clip and hands it to <paramref name="sink"/> (no Unity API used - pure DSP).</summary>
        internal static void RenderAll(Action<Clip> sink)
        {
            int previousRate = Synth.SampleRate;
            try
            {
                Synth.SetRate(Synth.SfxRate);
                void Sfx(string name, float[] data, float drive = 0.3f)
                {
                    Synth.Normalize(data, 0.9f, drive);
                    sink(new Clip(SfxFolder, name, data, null, Synth.SfxRate));
                }

                // Weapons --------------------------------------------------------------
                Sfx("Fire_Rifle", Gunshot(1, length: 1.0f, crack: 0.9f, blastCut: 7000f, blastDecay: 0.045f, blastGain: 0.85f,
                    midFreq: 1300f, midGain: 0.5f, body0: 175f, body1: 52f, bodyDecay: 0.075f, bodyGain: 0.95f,
                    rumbleDecay: 0.32f, rumbleGain: 0.55f, mechTime: 0.038f, mechFreq: 2500f, mechGain: 0.16f, wet: 0.3f), 1.2f);
                Sfx("Fire_SMG", Gunshot(2, length: 0.75f, crack: 0.75f, blastCut: 8500f, blastDecay: 0.028f, blastGain: 0.8f,
                    midFreq: 1700f, midGain: 0.35f, body0: 220f, body1: 72f, bodyDecay: 0.045f, bodyGain: 0.7f,
                    rumbleDecay: 0.2f, rumbleGain: 0.35f, mechTime: 0.022f, mechFreq: 3100f, mechGain: 0.2f, wet: 0.24f), 1.2f);
                Sfx("Fire_Shotgun", Gunshot(3, length: 1.6f, crack: 0.7f, blastCut: 4200f, blastDecay: 0.085f, blastGain: 1.0f,
                    midFreq: 750f, midGain: 0.6f, body0: 125f, body1: 36f, bodyDecay: 0.17f, bodyGain: 1.25f,
                    rumbleDecay: 0.6f, rumbleGain: 0.95f, mechTime: 0f, mechFreq: 0f, mechGain: 0f, wet: 0.36f, boom: 0.65f), 1.2f);
                Sfx("Fire_Pistol", Gunshot(4, length: 0.85f, crack: 1.0f, blastCut: 9000f, blastDecay: 0.032f, blastGain: 0.8f,
                    midFreq: 2500f, midGain: 0.45f, body0: 235f, body1: 80f, bodyDecay: 0.04f, bodyGain: 0.6f,
                    rumbleDecay: 0.24f, rumbleGain: 0.3f, mechTime: 0.028f, mechFreq: 2200f, mechGain: 0.24f, wet: 0.3f), 1.2f);

                // Reload clip lengths stay inside each weapon's reloadSeconds (rifle 2.0, SMG 1.7, pistol 1.4, shotgun 2.4).
                Sfx("Reload_Rifle", MagazineReload(5, length: 1.95f, pitch: 1f, release: 0.10f, magOut: 0.14f, outDuration: 0.17f, magIn: 0.92f, rack: 1.40f));
                Sfx("Reload_SMG", MagazineReload(6, length: 1.62f, pitch: 1.15f, release: 0.08f, magOut: 0.11f, outDuration: 0.13f, magIn: 0.74f, rack: 1.15f));
                Sfx("Reload_Pistol", PistolReload(7));
                Sfx("Reload_Shotgun", ShotgunReload(8));
                Sfx("DryFire", DryFire(9));

                // Player -----------------------------------------------------------------
                for (int i = 0; i < 4; i++) Sfx($"Footstep_{i + 1}", Footstep(20 + i));
                Sfx("Jump", Jump(30));
                Sfx("Land", Land(31));
                Sfx("Hurt", Hurt(32), 0.6f);
                Sfx("Death", Death(33));
                Sfx("Spawn", Spawn(35));
                Sfx("WeaponSwitch", WeaponSwitch(34));

                // Feedback / UI ------------------------------------------------------------
                Sfx("HitMarker", HitMarker(41));
                Sfx("HeadshotMarker", HeadshotMarker(42));
                Sfx("Elimination", Elimination(43), 0.5f);
                Sfx("ButtonClick", ButtonClick(40));
                Sfx("CountdownTick", CountdownTick(44), 0.4f);
                Sfx("CountdownGo", CountdownGo(45), 0.5f);
                Sfx("Victory", Victory(46));
                Sfx("Defeat", Defeat(47));
                Sfx("Draw", Draw(48));
                for (int i = 0; i < 3; i++) Sfx($"Impact_{i + 1}", Impact(50 + i, i), 0.6f);

                // Music & ambience (normalised quieter so they sit under the effects) ------------
                Synth.SetRate(Synth.MusicRate);
                MenuMusic(out float[] left, out float[] right);
                sink(new Clip(MusicFolder, "Menu_Loop", left, right, Synth.MusicRate));
                float[] ambience = Ambience(60);
                sink(new Clip(MusicFolder, "Warehouse_Ambience", ambience, null, Synth.MusicRate));
            }
            finally
            {
                Synth.SetRate(previousRate);
            }
        }

        // ------------------------------------------------------------------ spaces & patches

        /// <summary>Concrete warehouse: strong wall slap-back plus a medium, fairly bright tail.</summary>
        private static readonly Synth.Space Warehouse = new Synth.Space
        {
            Room = 0.8f, Damp = 0.35f, Size = 1f, Wet = 0.28f, PreDelay = 0.012f, LowCut = 140f, HighCut = 6500f,
            Taps = new[] { 0.017f, 0.029f, 0.043f, 0.061f, 0.083f, 0.112f },
            TapGains = new[] { 0.42f, 0.34f, 0.28f, 0.22f, 0.16f, 0.11f },
            TapCut = 3800f,
        };

        private static readonly Synth.Space SmallRoom = new Synth.Space
        {
            Room = 0.45f, Damp = 0.5f, Size = 0.45f, Wet = 0.12f, PreDelay = 0.003f, LowCut = 250f, HighCut = 9000f,
        };

        private static readonly Synth.Space Hall = new Synth.Space
        {
            Room = 0.82f, Damp = 0.45f, Size = 1.15f, Wet = 0.3f, PreDelay = 0.02f, LowCut = 150f, HighCut = 7000f,
        };

        private static readonly Synth.Space MusicHall = new Synth.Space
        {
            Room = 0.88f, Damp = 0.45f, Size = 1.3f, Wet = 0.32f, PreDelay = 0.03f, LowCut = 170f, HighCut = 6500f, Width = 1f,
        };

        private static readonly Synth.Space DistantField = new Synth.Space
        {
            Room = 0.9f, Damp = 0.7f, Size = 1.4f, Wet = 0.45f, PreDelay = 0.05f, LowCut = 40f, HighCut = 2500f,
        };

        private static Synth.Space With(Synth.Space space, float wet)
        {
            Synth.Space copy = space.Clone();
            copy.Wet = wet;
            return copy;
        }

        // Low strings (celli/basses) sustained.
        private static readonly Synth.Patch LowStrings = new Synth.Patch
        {
            Wave = Wave.Saw, Voices = 4, Detune = 16f, Spread = 0.5f, Attack = 0.6f, Decay = 1f, Sustain = 1f, Release = 1.2f,
            Cutoff = 520f, Resonance = 0.6f, FilterEnv = 0.4f, VibratoDepth = 6f, VibratoRate = 4.8f, VibratoDelay = 0.5f,
        };

        // Spiccato ostinato strings.
        private static readonly Synth.Patch Strings = new Synth.Patch
        {
            Wave = Wave.Saw, Voices = 3, Detune = 10f, Spread = 0.3f, Attack = 0.003f, Decay = 0.08f, Sustain = 0.15f, Release = 0.07f,
            Cutoff = 380f, Resonance = 0.9f, FilterEnv = 3.5f, KeyTrack = 0.5f, Drive = 0.4f,
        };

        private static readonly Synth.Patch StringsAccent = Variant(Strings, p => { p.Cutoff = 520f; p.FilterEnv = 5f; p.Decay = 0.12f; p.Drive = 0.8f; });
        private static readonly Synth.Patch StringsSoft = Variant(Strings, p => { p.Cutoff = 260f; p.FilterEnv = 2f; });

        private static readonly Synth.Patch Violins = new Synth.Patch
        {
            Wave = Wave.Saw, Voices = 3, Detune = 12f, Spread = 0.4f, Attack = 0.004f, Decay = 0.07f, Sustain = 0.2f, Release = 0.06f,
            Cutoff = 1400f, Resonance = 0.7f, FilterEnv = 2f, KeyTrack = 0.5f,
        };

        // Horns / trombones: the filter opens with the amplitude envelope for the brassy bloom.
        private static readonly Synth.Patch Brass = new Synth.Patch
        {
            Wave = Wave.Saw, Voices = 3, Detune = 8f, Spread = 0.35f, Attack = 0.25f, Decay = 0.8f, Sustain = 0.75f, Release = 0.45f,
            Cutoff = 260f, Resonance = 1.0f, FilterEnv = 6f, KeyTrack = 0.6f, Drive = 0.8f, Breath = 0.02f,
            VibratoDepth = 7f, VibratoDelay = 0.5f,
        };

        private static readonly Synth.Patch BrassSoft = Variant(Brass, p => { p.Attack = 1.5f; p.Cutoff = 200f; p.FilterEnv = 4f; p.Release = 1f; });
        private static readonly Synth.Patch BrassSwell = Variant(Brass, p => { p.Attack = 4.2f; p.Release = 0.25f; p.FilterEnv = 7f; });
        private static readonly Synth.Patch BrassStab = Variant(Brass, p => { p.Attack = 0.02f; p.Decay = 0.25f; p.Sustain = 0.3f; p.Release = 0.2f; p.FilterEnv = 7f; });
        private static readonly Synth.Patch BrassOutro = Variant(Brass, p => { p.Attack = 0.05f; p.Decay = 1.5f; p.Sustain = 0.45f; p.Release = 2.6f; });

        private static readonly Synth.Patch BrassLead = new Synth.Patch
        {
            Wave = Wave.Saw, Voices = 4, Detune = 9f, Spread = 0.25f, Attack = 0.12f, Decay = 0.6f, Sustain = 0.8f, Release = 0.4f,
            Cutoff = 380f, Resonance = 1.1f, FilterEnv = 6f, KeyTrack = 0.5f, Drive = 1f, Breath = 0.02f,
            VibratoDepth = 12f, VibratoRate = 5.2f, VibratoDelay = 0.35f,
        };

        private static readonly Synth.Patch BrassFanfare = Variant(BrassLead, p => { p.Attack = 0.025f; p.Decay = 0.4f; p.Release = 0.35f; p.Cutoff = 450f; p.VibratoDelay = 0.3f; });
        private static readonly Synth.Patch BrassDark = Variant(Brass, p => { p.Attack = 0.18f; p.Release = 0.8f; p.Cutoff = 220f; p.FilterEnv = 3.5f; p.Drive = 0.6f; p.VibratoDepth = 6f; });
        private static readonly Synth.Patch BrassHit = Variant(Brass, p => { p.Attack = 0.012f; p.Decay = 0.3f; p.Sustain = 0.35f; p.Release = 0.5f; p.FilterEnv = 8f; p.Drive = 1.5f; });

        // Trailer "braam": heavily driven, detuned low brass with a sub.
        private static readonly Synth.Patch BraamPatch = new Synth.Patch
        {
            Wave = Wave.Saw, Voices = 4, Detune = 22f, Spread = 0.6f, Attack = 0.02f, Decay = 0.9f, Sustain = 0.3f, Release = 1.6f,
            Cutoff = 140f, Resonance = 1.4f, FilterEnv = 12f, Drive = 3f, Sub = 0.6f,
        };

        private static readonly Synth.Patch ChoirAah = new Synth.Patch
        {
            Wave = Wave.Saw, Voices = 4, Detune = 14f, Spread = 0.8f, Attack = 0.9f, Decay = 1f, Sustain = 1f, Release = 1.4f,
            VibratoDepth = 14f, VibratoRate = 5.3f, VibratoDelay = 0.2f, Breath = 0.03f, HighPass = 120f,
            Formants = new[] { 730f, 1090f, 2440f }, FormantGains = new[] { 1f, 0.55f, 0.3f }, FormantQ = 8f,
        };

        private static readonly Synth.Patch ChoirOoh = Variant(ChoirAah, p =>
        {
            p.Formants = new[] { 320f, 800f, 2240f };
            p.FormantGains = new[] { 1f, 0.35f, 0.12f };
            p.Attack = 1.4f;
        });

        private static Synth.Patch Variant(Synth.Patch source, Action<Synth.Patch> change)
        {
            Synth.Patch p = source.Clone();
            change(p);
            return p;
        }

        // Modal tables.
        private static readonly float[] PlateRatios = { 1f, 1.59f, 2.14f, 2.30f, 2.65f, 2.92f, 3.16f, 3.5f, 4.1f };
        private static readonly float[] PlateAmps = { 1f, 0.8f, 0.6f, 0.55f, 0.45f, 0.4f, 0.3f, 0.25f, 0.2f };
        private static readonly float[] PlateDecays = { 0.35f, 0.28f, 0.24f, 0.2f, 0.17f, 0.14f, 0.12f, 0.1f, 0.08f };
        private static readonly float[] HelmetRatios = { 1f, 1.47f, 2.09f, 2.56f, 3.31f, 4.14f };
        private static readonly float[] HelmetAmps = { 1f, 0.6f, 0.5f, 0.35f, 0.25f, 0.15f };
        private static readonly float[] HelmetDecays = { 0.32f, 0.24f, 0.18f, 0.14f, 0.1f, 0.07f };
        private static readonly float[] GunBodyRatios = { 1f, 2.31f, 3.87f, 5.12f, 6.9f, 8.4f };
        private static readonly float[] GunBodyAmps = { 1f, 0.7f, 0.5f, 0.4f, 0.25f, 0.2f };
        private static readonly float[] GunBodyDecays = { 0.12f, 0.1f, 0.08f, 0.06f, 0.05f, 0.04f };

        // ------------------------------------------------------------------ weapons

        /// <summary>
        /// Layered gunshot: sub-millisecond crack, saturated muzzle blast (closing low-pass), mid bark,
        /// pitch-dropping punch, brown-noise rumble, bolt/slide action clicks and a concrete-warehouse slap + tail.
        /// </summary>
        private static float[] Gunshot(int seed, float length, float crack, float blastCut, float blastDecay, float blastGain,
            float midFreq, float midGain, float body0, float body1, float bodyDecay, float bodyGain,
            float rumbleDecay, float rumbleGain, float mechTime, float mechFreq, float mechGain, float wet, float boom = 0f)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(length);

            s.NoiseHit(m, 0f, 0.01f, crack, 0.0001f, 0.0009f, FilterType.High, 2500f, 2500f, 1f, 0.7071f, NoiseColor.White, 1.5f, 0f, 0.6f);
            s.NoiseHit(m, 0f, 0.5f, blastGain, 0.0003f, blastDecay, FilterType.Low, blastCut, 600f, blastDecay * 1.5f, 0.9f, NoiseColor.White, 2.5f, 0f, 1f);
            s.NoiseHit(m, 0.0005f, 0.3f, midGain, 0.0005f, blastDecay * 1.2f, FilterType.Band, midFreq, midFreq * 0.75f, 0.04f, 1.1f, NoiseColor.White, 1.2f, 0f, 1f);
            Synth.Tone(m, 0f, 0.6f, body0, body1, 0.022f, bodyGain, 0.0006f, bodyDecay, Wave.Sine, 1.8f, 0f, 0.7f);
            s.NoiseHit(m, 0.002f, length, rumbleGain, 0.008f, rumbleDecay, FilterType.Low, 450f, 110f, rumbleDecay * 0.8f, 0.7f, NoiseColor.Brown, 0.3f, 0f, 1f);
            if (boom > 0f) Synth.Tone(m, 0f, 1.2f, 75f, 30f, 0.06f, boom, 0.002f, 0.35f, Wave.Sine, 0.8f, 0f, 0.8f);

            if (mechGain > 0f)
            {
                s.MetalClick(m, mechTime, mechFreq, mechGain, 0.02f, 0.4f, 0f, 0.15f);
                s.MetalClick(m, mechTime + 0.018f, mechFreq * 1.3f, mechGain * 0.6f, 0.012f, 0.2f, 0f, 0.1f);
            }

            Synth.Space space = With(Warehouse, wet);
            if (boom > 0f)
            {
                space.Room = 0.86f;
                space.Size = 1.2f;
            }
            float[] b = m.Finish(space);
            Synth.FadeOut(b, 0.08f);
            return b;
        }

        private static void MagRelease(Synth s, Synth.Mix m, float t, float pitch) =>
            s.MetalClick(m, t, 3400f * pitch, 0.5f, 0.014f, 0.15f);

        private static void MagOut(Synth s, Synth.Mix m, float t, float duration, float pitch)
        {
            s.Scrape(m, t, duration, 0.35f, 2300f * pitch, 1500f * pitch, 1.4f, 0.55f);
            s.MetalClick(m, t + duration * 0.15f, 2000f * pitch, 0.2f, 0.02f, 0.1f);
        }

        private static void MagIn(Synth s, Synth.Mix m, float t, float pitch)
        {
            s.Scrape(m, t, 0.08f, 0.32f, 1500f * pitch, 2400f * pitch, 1.4f, 0.5f);
            s.Thunk(m, t + 0.085f, 0.8f, pitch);
            s.MetalClick(m, t + 0.09f, 2800f * pitch, 0.75f, 0.022f, 0.25f);
        }

        /// <summary>Charging handle: pull back (scrape + catch click), let go, bolt slams home.</summary>
        private static void Rack(Synth s, Synth.Mix m, float t, float pitch, float weight)
        {
            s.Scrape(m, t, 0.075f, 0.3f, 2600f * pitch, 1800f * pitch, 1.6f, 0.6f);
            s.MetalClick(m, t + 0.075f, 1900f * pitch, 0.65f * weight, 0.03f, 0.35f);
            s.Scrape(m, t + 0.17f, 0.03f, 0.25f, 1800f * pitch, 2800f * pitch, 1.6f, 0.4f);
            s.MetalClick(m, t + 0.2f, 1350f * pitch, weight, 0.055f, 0.6f);
            s.Thunk(m, t + 0.2f, 0.55f * weight, pitch * 1.1f);
            s.MetalClick(m, t + 0.205f, 3300f * pitch, 0.35f, 0.018f, 0f);
        }

        private static float[] MagazineReload(int seed, float length, float pitch, float release, float magOut, float outDuration, float magIn, float rack)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(length);
            s.Rustle(m, 0f, 0.18f, 0.18f);
            MagRelease(s, m, release, pitch);
            MagOut(s, m, magOut, outDuration, pitch);
            float handling = magOut + outDuration + 0.05f;
            s.Rustle(m, handling, Math.Max(0.1f, magIn - handling), 0.14f, 700f, 1600f);
            MagIn(s, m, magIn, pitch);
            Rack(s, m, rack, pitch, 1f);
            float[] b = m.Finish(SmallRoom);
            Synth.FadeOut(b, 0.05f);
            return b;
        }

        private static float[] PistolReload(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(1.3f);
            s.MetalClick(m, 0.07f, 3800f, 0.5f, 0.012f, 0.15f);              // mag release button
            s.Scrape(m, 0.1f, 0.1f, 0.3f, 2600f, 1900f, 1.5f, 0.5f);          // magazine drops out
            s.Rustle(m, 0.25f, 0.25f, 0.14f, 800f, 1700f);
            MagIn(s, m, 0.55f, 1.2f);
            s.MetalClick(m, 0.955f, 3000f, 0.3f, 0.012f, 0f);                 // slide stop lever
            s.Scrape(m, 0.962f, 0.022f, 0.25f, 2000f, 3000f, 1.6f, 0.4f);
            s.MetalClick(m, 0.985f, 1700f, 1f, 0.05f, 0.5f);                  // slide slams forward
            s.Thunk(m, 0.985f, 0.45f, 1.2f);
            s.MetalClick(m, 0.99f, 3600f, 0.35f, 0.015f, 0f);
            float[] b = m.Finish(SmallRoom);
            Synth.FadeOut(b, 0.05f);
            return b;
        }

        private static float[] ShotgunReload(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(2.3f);
            s.Rustle(m, 0f, 1.6f, 0.08f, 700f, 1300f);
            for (int i = 0; i < 4; i++)
            {
                float t = 0.12f + i * 0.36f + s.Range(-0.015f, 0.015f);
                s.Scrape(m, t, 0.05f, 0.2f, 1800f, 1200f, 1.3f, 0.5f);       // shell slides into the loading port
                s.MetalClick(m, t + 0.045f, 2300f, 0.4f, 0.02f, 0.2f);       // brass rim on the receiver
                s.Thunk(m, t + 0.05f, 0.35f, 0.8f);                            // thumb pushes it home
                s.MetalClick(m, t + 0.07f, 3800f, 0.22f, 0.01f, 0f);         // follower spring / shell latch
            }
            // Pump back...
            s.Scrape(m, 1.7f, 0.075f, 0.4f, 1300f, 900f, 1.2f, 0.7f);
            s.MetalClick(m, 1.775f, 1100f, 0.85f, 0.05f, 0.7f);
            s.Thunk(m, 1.775f, 0.6f, 0.85f);
            // ...and forward.
            s.Scrape(m, 1.92f, 0.055f, 0.35f, 900f, 1400f, 1.2f, 0.6f);
            s.MetalClick(m, 1.975f, 1250f, 1f, 0.05f, 0.7f);
            s.Thunk(m, 1.975f, 0.7f, 0.9f);
            s.MetalClick(m, 1.98f, 3000f, 0.3f, 0.015f, 0f);
            float[] b = m.Finish(SmallRoom);
            Synth.FadeOut(b, 0.05f);
            return b;
        }

        private static float[] DryFire(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(0.2f);
            s.MetalClick(m, 0f, 4200f, 0.2f, 0.006f, 0f);                    // trigger break
            s.MetalClick(m, 0.012f, 1500f, 0.8f, 0.018f, 0.8f);              // hammer falls on an empty chamber
            Synth.Tone(m, 0.012f, 0.05f, 320f, 210f, 0.01f, 0.35f, 0.0005f, 0.012f);
            float[] b = m.Finish(SmallRoom);
            Synth.FadeOut(b, 0.03f);
            return b;
        }

        // ------------------------------------------------------------------ player foley

        private static void GearRattle(Synth s, Synth.Mix m, float start, float span, int count, float gain)
        {
            for (int i = 0; i < count; i++)
            {
                float t = start + s.Range(0f, span);
                s.MetalClick(m, t, s.Range(2400f, 5200f), gain * s.Range(0.4f, 1f), s.Range(0.008f, 0.02f), 0.1f, s.Range(-0.3f, 0.3f), 0.15f);
            }
            // a strap buckle / sling swivel: lower and duller
            s.MetalClick(m, start + s.Range(0f, span), s.Range(900f, 1400f), gain * 0.5f, 0.02f, 0.3f, 0f, 0.15f);
        }

        /// <summary>Boot on concrete: heel thud, toe tap and gritty scuff; each seed is a different step.</summary>
        private static float[] Footstep(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(0.32f);
            float heel = s.Range(0.9f, 1.1f);
            Synth.Tone(m, 0f, 0.15f, 125f * heel, 52f * heel, 0.014f, 0.85f, 0.0012f, 0.028f, Wave.Sine, 0.8f, 0f, 0.3f);
            s.NoiseHit(m, 0f, 0.12f, 0.55f, 0.001f, 0.022f, FilterType.Low, s.Range(550f, 850f), 160f, 0.02f, 0.8f, NoiseColor.White, 0.3f, 0f, 0.3f);
            float toe = s.Range(0.035f, 0.055f);
            Synth.Tone(m, toe, 0.08f, 150f * heel, 80f * heel, 0.01f, 0.3f, 0.001f, 0.015f, Wave.Sine, 0f, 0f, 0.3f);
            s.NoiseHit(m, toe, 0.1f, s.Range(0.18f, 0.26f), 0.002f, 0.025f, FilterType.Band, s.Range(2500f, 3800f), 1800f, 0.03f, 0.9f, NoiseColor.White, 0f, 0f, 0.2f);
            s.Crackle(m, 0.003f, s.Range(0.06f, 0.09f), 1400f, 0.3f, 0.025f, 2000f, 7000f, 0f, 0.2f);
            float[] b = m.Finish(With(SmallRoom, 0.1f));
            Synth.FadeOut(b, 0.03f);
            return b;
        }

        private static float[] Jump(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(0.45f);
            Synth.Tone(m, 0f, 0.12f, 110f, 60f, 0.015f, 0.45f, 0.001f, 0.03f, Wave.Sine, 0.5f, 0f, 0.2f);          // push off
            s.NoiseHit(m, 0f, 0.1f, 0.25f, 0.001f, 0.02f, FilterType.Low, 700f, 200f, 0.02f, 0.8f, NoiseColor.White, 0f, 0f, 0.2f);
            s.Rustle(m, 0.01f, 0.28f, 0.55f, 600f, 2200f, 0f, 0.15f);                                                 // cloth whoosh
            GearRattle(s, m, 0.02f, 0.14f, 5, 0.22f);
            float[] b = m.Finish(SmallRoom);
            Synth.FadeOut(b, 0.04f);
            return b;
        }

        private static float[] Land(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(0.5f);
            Synth.Tone(m, 0f, 0.3f, 105f, 40f, 0.02f, 1f, 0.001f, 0.065f, Wave.Sine, 1f, 0f, 0.3f);                  // heavy boots
            s.NoiseHit(m, 0f, 0.25f, 0.75f, 0.001f, 0.045f, FilterType.Low, 800f, 150f, 0.04f, 0.8f, NoiseColor.White, 0.6f, 0f, 0.3f);
            s.Crackle(m, 0.002f, 0.1f, 1800f, 0.35f, 0.03f, 1800f, 6500f);                                            // grit
            s.NoiseHit(m, 0.03f, 0.12f, 0.2f, 0.002f, 0.03f, FilterType.Band, 3000f, 1800f, 0.03f, 0.9f);            // scuff
            GearRattle(s, m, 0.008f, 0.12f, 7, 0.32f);
            s.Rustle(m, 0f, 0.2f, 0.3f, 800f, 1500f);
            float[] b = m.Finish(SmallRoom);
            Synth.FadeOut(b, 0.04f);
            return b;
        }

        /// <summary>Bullet hitting the body: dull thwack, punch, wet slap, crunch and a low whomp.</summary>
        private static float[] Hurt(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(0.4f);
            s.NoiseHit(m, 0f, 0.15f, 0.95f, 0.0004f, 0.018f, FilterType.Low, 1500f, 300f, 0.015f, 0.8f, NoiseColor.White, 1.5f, 0f, 0.2f);
            Synth.Tone(m, 0f, 0.25f, 150f, 55f, 0.012f, 0.95f, 0.0005f, 0.05f, Wave.Sine, 1.4f, 0f, 0.2f);
            s.NoiseHit(m, 0.001f, 0.05f, 0.45f, 0.0003f, 0.01f, FilterType.Band, 950f, 700f, 0.01f, 2f, NoiseColor.White, 0.8f);
            s.Crackle(m, 0.002f, 0.05f, 2600f, 0.45f, 0.014f, 1500f, 4500f);
            Synth.Tone(m, 0f, 0.35f, 75f, 38f, 0.05f, 0.5f, 0.003f, 0.11f, Wave.Sine, 0.4f, 0f, 0.3f);
            s.Rustle(m, 0f, 0.12f, 0.2f, 900f, 1800f);
            float[] b = m.Finish(SmallRoom);
            Synth.FadeOut(b, 0.04f);
            return b;
        }

        /// <summary>Low boom + tinnitus ring, knees hit, body and gear fall, rifle clatters on concrete.</summary>
        private static float[] Death(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(1.9f);
            Synth.Tone(m, 0f, 1.8f, 58f, 26f, 0.25f, 0.75f, 0.01f, 0.75f, Wave.Sine, 0.5f, 0f, 0.4f);
            Synth.Tone(m, 0.02f, 1.8f, 3720f, 3720f, 1f, 0.1f, 0.12f, 0.8f, Wave.Sine, 0f, 0f, 0.1f);
            Synth.Tone(m, 0.02f, 1.8f, 3733f, 3733f, 1f, 0.08f, 0.15f, 0.7f, Wave.Sine, 0f, 0f, 0.1f);

            Synth.Tone(m, 0.12f, 0.2f, 110f, 55f, 0.012f, 0.5f, 0.001f, 0.04f, Wave.Sine, 0.8f, 0f, 0.3f);           // knees
            s.NoiseHit(m, 0.12f, 0.1f, 0.35f, 0.001f, 0.025f, FilterType.Low, 700f, 150f, 0.02f, 0.8f, NoiseColor.White, 0.3f, 0f, 0.3f);
            GearRattle(s, m, 0.12f, 0.06f, 3, 0.18f);

            Synth.Tone(m, 0.42f, 0.35f, 95f, 36f, 0.025f, 1f, 0.001f, 0.09f, Wave.Sine, 1.2f, 0f, 0.35f);             // body
            s.NoiseHit(m, 0.42f, 0.3f, 0.85f, 0.001f, 0.06f, FilterType.Low, 900f, 140f, 0.05f, 0.8f, NoiseColor.White, 0.8f, 0f, 0.35f);
            s.Crackle(m, 0.42f, 0.12f, 1600f, 0.3f, 0.04f, 1500f, 6000f);
            GearRattle(s, m, 0.43f, 0.15f, 6, 0.3f);
            s.Rustle(m, 0.38f, 0.3f, 0.3f, 700f, 1400f);

            s.Modal(m, 0.56f, 480f, GunBodyRatios, GunBodyAmps, GunBodyDecays, 0.45f, 0.2f, 0.3f, 0.02f, 0.4f);     // weapon drops
            s.Thunk(m, 0.56f, 0.35f, 1.3f);
            s.Modal(m, 0.68f, 505f, GunBodyRatios, GunBodyAmps, GunBodyDecays, 0.22f, 0.25f, 0.3f, 0.02f, 0.3f);    // ...and bounces
            float[] b = m.Finish(With(Warehouse, 0.22f));
            Synth.FadeOut(b, 0.2f);
            return b;
        }

        /// <summary>Tactical deploy: low drop, gear rustle, charging handle racked.</summary>
        private static float[] Spawn(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(1f);
            Synth.Tone(m, 0f, 0.9f, 62f, 34f, 0.12f, 0.45f, 0.02f, 0.3f, Wave.Sine, 0.4f, 0f, 0.4f);
            s.Rustle(m, 0f, 0.3f, 0.45f, 700f, 2000f);
            Rack(s, m, 0.32f, 1f, 1f);
            float[] b = m.Finish(With(SmallRoom, 0.15f));
            Synth.FadeOut(b, 0.08f);
            return b;
        }

        private static float[] WeaponSwitch(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(0.45f);
            s.Rustle(m, 0f, 0.15f, 0.45f, 900f, 2000f);
            s.MetalClick(m, 0.11f, 1150f, 0.85f, 0.05f, 0.5f);
            s.MetalClick(m, 0.115f, 2900f, 0.4f, 0.02f, 0f);
            s.Thunk(m, 0.11f, 0.35f, 1.2f);
            s.MetalClick(m, 0.24f, 3700f, 0.35f, 0.012f, 0.1f);
            float[] b = m.Finish(SmallRoom);
            Synth.FadeOut(b, 0.04f);
            return b;
        }

        // ------------------------------------------------------------------ feedback / UI

        private static float[] HitMarker(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(0.09f);
            s.MetalClick(m, 0f, 3300f, 1f, 0.007f, 0.25f, 0f, 0f);
            s.NoiseHit(m, 0f, 0.004f, 0.35f, 0.0001f, 0.0007f, FilterType.High, 5000f, 5000f, 1f);
            float[] b = m.Finish(null);
            Synth.FadeOut(b, 0.01f);
            return b;
        }

        private static float[] HeadshotMarker(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(0.55f);
            s.MetalClick(m, 0f, 3300f, 0.6f, 0.007f, 0.2f, 0f, 0f);
            s.Modal(m, 0.002f, 2150f, HelmetRatios, HelmetAmps, HelmetDecays, 0.8f, 0f, 0.15f, 0.01f, 0.35f);       // helmet "tink"
            float[] b = m.Finish(SmallRoom);
            Synth.FadeOut(b, 0.05f);
            return b;
        }

        private static float[] Elimination(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(1.6f);
            Synth.Tone(m, 0f, 1f, 100f, 34f, 0.035f, 1f, 0.001f, 0.26f, Wave.Sine, 1.4f, 0f, 0.35f);
            s.NoiseHit(m, 0f, 0.4f, 0.55f, 0.0008f, 0.07f, FilterType.Low, 1500f, 120f, 0.06f, 0.8f, NoiseColor.White, 0.6f, 0f, 0.4f);
            int[] notes = { 38, 45, 50, 53 };
            float[] gains = { 0.28f, 0.22f, 0.2f, 0.16f };
            for (int i = 0; i < notes.Length; i++) s.Note(m, 0.005f, 0.32f, Synth.Midi(notes[i]), gains[i], BrassHit, 0f, 0.45f);
            s.Timpani(m, 0f, Synth.Midi(38), 0.4f, 1f, 0f);
            float[] b = m.Finish(Hall);
            Synth.FadeOut(b, 0.15f);
            return b;
        }

        /// <summary>Crisp two-stage metal click, like flicking a safety selector.</summary>
        private static float[] ButtonClick(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(0.06f);
            s.MetalClick(m, 0f, 2900f, 0.9f, 0.006f, 0.3f, 0f, 0f);
            s.MetalClick(m, 0.011f, 4300f, 0.45f, 0.004f, 0f, 0f, 0f);
            float[] b = m.Finish(null);
            Synth.FadeOut(b, 0.008f);
            return b;
        }

        private static float[] CountdownTick(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(1f);
            s.Taiko(m, 0f, 1f, 1f, 0f, 0.35f);
            float[] b = m.Finish(Hall);
            Synth.FadeOut(b, 0.1f);
            return b;
        }

        /// <summary>Trench whistle blast over a big war-drum boom.</summary>
        private static float[] CountdownGo(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(1.8f);
            s.Taiko(m, 0f, 1f, 0.85f, 0f, 0.35f);
            s.Boom(m, 0f, 0.8f, 0f, 0.35f);
            s.Cymbal(m, 0f, 0.22f, 0.9f, 0f, 0.3f);
            s.Whistle(m, 0f, 0.65f, 2750f, 0.42f, 32f, 0.035f, 0f, 0.3f);
            float[] b = m.Finish(Hall);
            Synth.FadeOut(b, 0.15f);
            return b;
        }

        private static void Chord(Synth s, Synth.Mix m, float start, float length, int[] notes, float gain, Synth.Patch patch, float send = 0.4f)
        {
            for (int i = 0; i < notes.Length; i++)
            {
                float pan = notes.Length == 1 ? 0f : (i / (float)(notes.Length - 1) - 0.5f) * 0.6f;
                s.Note(m, start, length, Synth.Midi(notes[i]), gain, patch, pan, send);
            }
        }

        /// <summary>Short military fanfare in D major: triplet pickup, fifth, held tonic chord, snare roll and final hit.</summary>
        private static float[] Victory(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(2.8f);
            float[] triplet = { 0f, 0.1f, 0.2f };
            foreach (float t in triplet)
            {
                s.Note(m, t, 0.075f, Synth.Midi(62), 0.3f, BrassFanfare, 0.05f, 0.35f);
                s.Note(m, t, 0.075f, Synth.Midi(57), 0.2f, BrassFanfare, -0.15f, 0.35f);
                s.Snare(m, t, 0.4f + t, 0.15f, 0.25f, 0.8f);
            }
            s.Note(m, 0.32f, 0.22f, Synth.Midi(69), 0.3f, BrassFanfare, 0.05f, 0.35f);
            s.Note(m, 0.32f, 0.22f, Synth.Midi(62), 0.2f, BrassFanfare, -0.15f, 0.35f);
            s.Snare(m, 0.32f, 0.6f, 0.15f);
            s.Timpani(m, 0.32f, Synth.Midi(45), 0.5f);

            s.Note(m, 0.6f, 1.45f, Synth.Midi(74), 0.3f, BrassFanfare, 0.05f, 0.4f);
            Chord(s, m, 0.6f, 1.45f, new[] { 38, 50, 62, 66, 69 }, 0.16f, Brass);
            s.Taiko(m, 0.6f, 1f, 1f, 0f, 0.3f);
            s.Timpani(m, 0.6f, Synth.Midi(38), 0.8f);
            s.Cymbal(m, 0.6f, 0.45f, 1.4f, 0.3f);
            Roll(s, m, 1.45f, 1.95f, 0.15f, 0.5f, 16f);
            s.Snare(m, 2.0f, 0.7f, 0.15f);
            s.Taiko(m, 2.0f, 0.9f, 0.95f, 0f, 0.35f);
            s.Timpani(m, 2.0f, Synth.Midi(38), 0.7f);
            float[] b = m.Finish(Hall);
            Synth.FadeOut(b, 0.3f);
            return b;
        }

        /// <summary>Low, somber brass cadence (Gm/Bb - A - Dm) with timpani and a distant boom.</summary>
        private static float[] Defeat(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(3f);
            Chord(s, m, 0f, 0.75f, new[] { 34, 43, 50, 58 }, 0.2f, BrassDark);
            s.Timpani(m, 0f, Synth.Midi(46), 0.6f);
            Chord(s, m, 0.8f, 0.75f, new[] { 33, 45, 52, 57, 61 }, 0.18f, BrassDark);
            s.Timpani(m, 0.8f, Synth.Midi(45), 0.6f);
            Chord(s, m, 1.6f, 1.0f, new[] { 38, 50, 53, 57, 62 }, 0.2f, BrassDark);
            s.Timpani(m, 1.6f, Synth.Midi(38), 0.9f, 1.4f);
            s.Boom(m, 1.6f, 0.5f);
            float[] b = m.Finish(Hall);
            Synth.FadeOut(b, 0.4f);
            return b;
        }

        /// <summary>Neutral: Dsus4 resolving to an open fifth (no third - neither major nor minor).</summary>
        private static float[] Draw(int seed)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(2.4f);
            Chord(s, m, 0f, 0.7f, new[] { 38, 50, 55, 57, 62 }, 0.18f, Brass);
            s.Timpani(m, 0f, Synth.Midi(38), 0.6f);
            Chord(s, m, 0.75f, 1f, new[] { 38, 50, 57, 62 }, 0.2f, Brass);
            s.Timpani(m, 0.75f, Synth.Midi(38), 0.5f);
            s.Taiko(m, 0.75f, 0.5f, 0.95f, 0f, 0.35f);
            float[] b = m.Finish(Hall);
            Synth.FadeOut(b, 0.35f);
            return b;
        }

        /// <summary>Bullet impacts: 0 = concrete with debris, 1 = sheet metal ring, 2 = concrete + ricochet whine.</summary>
        private static float[] Impact(int seed, int kind)
        {
            var s = new Synth(seed);
            var m = new Synth.Mix(kind == 0 ? 0.55f : 0.75f);
            s.NoiseHit(m, 0f, 0.01f, 0.9f, 0.0001f, 0.0012f, FilterType.High, 3000f, 3000f, 1f, 0.7071f, NoiseColor.White, 1.2f);
            if (kind == 1)
            {
                s.Modal(m, 0.0005f, s.Range(900f, 1250f), PlateRatios, PlateAmps, PlateDecays, 0.55f, 0f, 0.3f, 0.015f, 0.6f);
                s.MetalClick(m, 0f, 3500f, 0.5f, 0.01f, 0.3f);
                s.Crackle(m, 0.003f, 0.12f, 900f, 0.2f, 0.04f, 2500f, 8000f);
            }
            else
            {
                float g = kind == 2 ? 0.8f : 1f;
                s.NoiseHit(m, 0f, 0.1f, 0.65f * g, 0.0003f, 0.014f, FilterType.Band, 1150f, 800f, 0.02f, 1.2f, NoiseColor.White, 1f, 0f, 0.3f);
                Synth.Tone(m, 0f, 0.1f, 280f, 120f, 0.01f, 0.45f * g, 0.0004f, 0.016f, Wave.Sine, 0.8f, 0f, 0.3f);
                s.Crackle(m, 0.004f, 0.4f, 1500f, 0.4f * g, 0.07f, 1500f, 8000f, 0f, 0.2f);                         // debris
                s.NoiseHit(m, 0.002f, 0.3f, 0.18f * g, 0.004f, 0.07f, FilterType.High, 3500f, 2500f, 0.1f, 0.7071f, NoiseColor.White, 0f, 0f, 0.3f); // dust
                if (kind == 2) s.Ricochet(m, 0.015f, 0.5f, s.Range(3600f, 4200f), s.Range(1300f, 1600f), 0.42f, -0.2f, 0.35f);
            }
            float[] b = m.Finish(With(Warehouse, 0.14f));
            Synth.FadeOut(b, 0.05f);
            return b;
        }

        private static void Roll(Synth s, Synth.Mix m, float start, float end, float gainStart, float gainEnd, float hitsPerSecond, float pan = 0.15f, float send = 0.3f)
        {
            int hits = (int)((end - start) * hitsPerSecond);
            for (int i = 0; i < hits; i++)
            {
                float u = hits <= 1 ? 1f : i / (float)(hits - 1);
                s.Snare(m, start + i / hitsPerSecond, gainStart + (gainEnd - gainStart) * u * u, pan, send, 0.6f);
            }
        }

        // ------------------------------------------------------------------ music

        /// <summary>
        /// Cinematic war theme, D minor, 96 BPM, 34 bars (~85 s), stereo, seamless loop:
        /// intro (drone, distant war drums, ooh choir) -> main (spiccato ostinato, taiko 3-3-2 groove, military snare, brass swells)
        /// -> break/build (aah choir, timpani + snare roll crescendo, riser, reverse swell) -> climax (braam, heroic brass melody,
        /// violin arpeggios, full percussion) -> outro hit whose tail flows back into the intro.
        /// </summary>
        private static void MenuMusic(out float[] left, out float[] right)
        {
            const float bpm = 96f;
            const int bars = 34;
            float beat = 60f / bpm;
            var m = new Synth.Mix(bars * 4 * beat, stereo: true, loop: true);
            var s = new Synth(99);
            float T(float bar, float b = 0f) => (bar * 4f + b) * beat;

            // [bass, three upper chord tones] with smooth voice leading around D3-Bb3.
            int[] dm = { 38, 50, 53, 57 }, bb = { 34, 50, 53, 58 }, gm = { 31, 50, 55, 58 }, a = { 33, 49, 52, 57 }, f = { 29, 48, 53, 57 }, c = { 36, 48, 52, 55 };
            int[][] chords =
            {
                dm, dm, dm, dm, bb, bb, a, a,      // 0-7   intro
                dm, bb, gm, a, dm, bb, gm, a,      // 8-15  main
                bb, bb, gm, gm, dm, dm, a, a,      // 16-23 break / build
                dm, bb, f, c, gm, bb, a, a,        // 24-31 climax
                dm, dm,                            // 32-33 outro
            };
            int Section(int bar) => bar < 8 ? 0 : bar < 16 ? 1 : bar < 24 ? 2 : bar < 32 ? 3 : 4;

            // ---- sustained low strings (merged over repeated chords within a section)
            for (int bar = 0; bar < bars;)
            {
                int run = 1;
                while (bar + run < bars && chords[bar + run] == chords[bar] && Section(bar + run) == Section(bar)) run++;
                int[] ch = chords[bar];
                int sec = Section(bar);
                float g = sec == 0 ? 0.13f : sec == 2 ? 0.14f : 0.16f;
                float len = T(run) - 0.05f;
                s.Note(m, T(bar), len, Synth.Midi(ch[0]), g, LowStrings, -0.15f, 0.3f);
                s.Note(m, T(bar), len, Synth.Midi(ch[0] + 12), g * 0.7f, LowStrings, 0.15f, 0.3f);
                bar += run;
            }
            s.Note(m, T(0), T(6) - 0.1f, Synth.Midi(26), 0.12f, LowStrings, 0f, 0.25f); // D pedal under the intro

            // ---- choir
            void ChoirChord(float start, float length, int[] ch, Synth.Patch patch, float gain, bool high, bool low)
            {
                for (int i = 1; i < ch.Length; i++)
                {
                    float pan = (i - 2) * 0.35f;
                    if (low) s.Note(m, start, length, Synth.Midi(ch[i]), gain, patch, pan, 0.5f);
                    if (high) s.Note(m, start, length, Synth.Midi(ch[i] + 12), gain * 0.85f, patch, -pan, 0.5f);
                }
            }
            ChoirChord(T(2), T(2), dm, ChoirOoh, 0.05f, false, true);
            ChoirChord(T(4), T(2), bb, ChoirOoh, 0.055f, false, true);
            ChoirChord(T(6), T(2), a, ChoirOoh, 0.06f, true, true);
            for (int bar = 16; bar < 24; bar += 2) ChoirChord(T(bar), T(2), chords[bar], ChoirAah, 0.055f, true, true);
            for (int bar = 24; bar < 32; bar++)
            {
                if (bar == 31) continue; // bar 30-31 are both A: one longer chord
                ChoirChord(T(bar), bar == 30 ? T(2) : T(1), chords[bar], ChoirAah, 0.05f, true, true);
            }
            ChoirChord(T(32), T(1.5f), dm, ChoirAah, 0.05f, true, true);

            // ---- spiccato ostinato (3-3-2 accents) and climax violin arpeggios
            for (int bar = 6; bar < 32; bar++)
            {
                int sec = Section(bar);
                int[] ch = chords[bar];
                int root = ch[0] + 12;
                bool thin = sec == 2 && bar < 20;
                bool soft = sec == 0 || thin;
                float g = sec == 0 ? (bar == 6 ? 0.07f : 0.1f) : sec == 1 ? 0.17f : sec == 2 ? (thin ? 0.1f : 0.14f) : 0.19f;
                for (int step = 0; step < 16; step++)
                {
                    bool accent = step == 0 || step == 3 || step == 6 || step == 8 || step == 11 || step == 14;
                    if (thin && !accent) continue;
                    if (bar == 23 && step >= 14) continue; // breath before the climax
                    float t = T(bar, step * 0.25f);
                    Synth.Patch patch = soft ? StringsSoft : accent ? StringsAccent : Strings;
                    s.Note(m, t, beat * 0.17f, Synth.Midi(root), accent ? g : g * 0.55f, patch, -0.25f, 0.22f);
                    if (accent && (sec == 1 || sec == 3)) s.Note(m, t, beat * 0.17f, Synth.Midi(root + 12), g * 0.5f, StringsAccent, 0.3f, 0.22f);
                }
                if (sec == 3)
                {
                    int[] arp = { ch[1] + 12, ch[2] + 12, ch[3] + 12, ch[2] + 12 };
                    for (int step = 0; step < 16; step++)
                        s.Note(m, T(bar, step * 0.25f), beat * 0.18f, Synth.Midi(arp[step % 4]), step % 4 == 0 ? 0.075f : 0.05f, Violins, 0.4f, 0.3f);
                }
            }

            // ---- brass
            void BrassChord(float start, float length, int[] ch, Synth.Patch patch, float gain, bool trombone)
            {
                for (int i = 1; i < ch.Length; i++) s.Note(m, start, length, Synth.Midi(ch[i]), gain, patch, (i - 2) * 0.25f, 0.4f);
                if (trombone) s.Note(m, start, length, Synth.Midi(ch[0] + 12), gain * 0.9f, patch, -0.1f, 0.4f);
            }
            BrassChord(T(4), T(2) - 0.1f, bb, BrassSoft, 0.06f, false);
            BrassChord(T(6), T(2) - 0.1f, a, BrassSoft, 0.07f, true);
            for (int bar = 8; bar < 16; bar++)
            {
                float g = bar < 12 ? 0.085f : 0.11f;
                BrassChord(T(bar), 4 * beat - 0.12f, chords[bar], Brass, g, true);
                if (bar >= 12)
                {
                    BrassChord(T(bar, 1.5f), beat * 0.3f, chords[bar], BrassStab, g * 0.8f, false);
                    BrassChord(T(bar, 3f), beat * 0.3f, chords[bar], BrassStab, g * 0.8f, false);
                }
            }
            BrassChord(T(22), T(2) - 0.5f * beat, a, BrassSwell, 0.11f, true);
            for (int bar = 24; bar < 32; bar++) BrassChord(T(bar), 4 * beat - 0.12f, chords[bar], Brass, 0.06f, true);
            BrassChord(T(32), 4 * beat, dm, BrassOutro, 0.11f, true);
            Braam(s, m, T(24), 1f);
            Braam(s, m, T(32), 0.9f);

            // Heroic melody over the climax: (bar, beat, length in beats, midi).
            float[,] melody =
            {
                { 0, 0, 2, 69 }, { 0, 2, 1, 67 }, { 0, 3, 1, 65 },
                { 1, 0, 2, 65 }, { 1, 2, 2, 62 },
                { 2, 0, 3, 72 }, { 2, 3, 1, 69 },
                { 3, 0, 4, 67 },
                { 4, 0, 2, 70 }, { 4, 2, 1, 69 }, { 4, 3, 1, 67 },
                { 5, 0, 2, 65 }, { 5, 2, 2, 74 },
                { 6, 0, 2, 76 }, { 6, 2, 2, 73 },
                { 7, 0, 4, 69 },
            };
            for (int i = 0; i < melody.GetLength(0); i++)
            {
                float start = T(24 + melody[i, 0], melody[i, 1]);
                float length = melody[i, 2] * beat - 0.04f;
                s.Note(m, start, length, Synth.Midi(melody[i, 3]), 0.2f, BrassLead, 0.05f, 0.4f);
                s.Note(m, start, length, Synth.Midi(melody[i, 3] - 12), 0.13f, Brass, -0.1f, 0.4f);
            }

            // ---- percussion
            float[] snareMain = { 0, 0, 0, 0, 0.85f, 0, 0.2f, 0, 0, 0, 0.25f, 0, 0.85f, 0, 0.25f, 0.35f };
            float[] snareClimax = { 0.3f, 0, 0.18f, 0.22f, 0.9f, 0, 0.22f, 0.18f, 0.3f, 0.18f, 0.28f, 0, 0.9f, 0.22f, 0.3f, 0.45f };
            float rollRate = 8f / beat; // 32nd notes

            void Groove(int bar, float gain, bool climax)
            {
                for (int e = 0; e < 8; e++)
                {
                    float t = T(bar, e * 0.5f);
                    if (e == 0 || e == 3 || e == 6) s.Taiko(m, t, gain * (e == 0 ? 0.95f : 0.72f), 1f, 0f, 0.3f);
                    if (e == 2 || e == 5 || e == 7) s.Taiko(m, t, gain * 0.32f, 1.55f, e == 5 ? -0.4f : 0.4f, 0.3f);
                }
                if (climax) s.Taiko(m, T(bar, 3.75f), gain * 0.45f, 1.3f, -0.3f, 0.3f);
            }

            void SnarePattern(int bar, float[] pattern, float fromBeat)
            {
                for (int step = 0; step < 16; step++)
                {
                    if (pattern[step] <= 0f || step * 0.25f >= fromBeat) continue;
                    s.Snare(m, T(bar, step * 0.25f), pattern[step] * 0.45f, 0.15f, 0.25f);
                }
            }

            void TomFill(int bar, float fromBeat)
            {
                for (float b = fromBeat; b < 4f; b += 0.25f)
                    s.Taiko(m, T(bar, b), 0.3f + 0.1f * (b - fromBeat), b % 0.5f == 0f ? 1.35f : 1.7f, b % 0.5f == 0f ? -0.35f : 0.35f, 0.3f);
            }

            for (int bar = 0; bar < bars; bar++)
            {
                int sec = Section(bar);
                int[] ch = chords[bar];
                float timp = Synth.Midi(ch[0] < 36 ? ch[0] + 12 : ch[0]);
                switch (sec)
                {
                    case 0:
                        s.Taiko(m, T(bar), bar == 0 ? 0.75f : 0.55f, 0.9f, 0f, 0.65f); // distant war drums
                        if (bar >= 2)
                        {
                            s.Taiko(m, T(bar, 2.5f), 0.25f, 1.05f, -0.35f, 0.7f);
                            s.Taiko(m, T(bar, 3f), 0.35f, 0.95f, 0.35f, 0.7f);
                        }
                        if (bar >= 4) s.Timpani(m, T(bar), timp, 0.3f);
                        if (bar == 7)
                        {
                            for (int e = 1; e < 8; e++) s.Taiko(m, T(7, e * 0.5f), 0.35f + 0.06f * e, e % 2 == 0 ? 0.95f : 1.3f, e % 2 == 0 ? 0f : 0.3f, 0.5f);
                            Roll(s, m, T(7, 1), T(8), 0.04f, 0.4f, rollRate);
                            s.ReverseSwell(m, T(8), 2f * beat, 0.25f);
                        }
                        break;

                    case 1:
                        Groove(bar, 1f, false);
                        SnarePattern(bar, snareMain, bar == 11 ? 3f : bar == 15 ? 2f : 4f);
                        s.Timpani(m, T(bar), timp, 0.45f);
                        if (bar == 8 || bar == 12)
                        {
                            s.Boom(m, T(bar), 0.8f);
                            s.Cymbal(m, T(bar), 0.5f, 1.6f, -0.4f);
                        }
                        if (bar == 11) Roll(s, m, T(11, 3), T(12), 0.15f, 0.5f, rollRate);
                        if (bar == 15)
                        {
                            Roll(s, m, T(15, 2), T(16), 0.15f, 0.65f, rollRate);
                            TomFill(15, 2f);
                        }
                        break;

                    case 2:
                        if (bar == 16)
                        {
                            s.Boom(m, T(16), 1f);
                            s.Taiko(m, T(16), 1f, 0.85f, 0f, 0.4f);
                            s.Cymbal(m, T(16), 0.45f, 2.2f, 0.3f);
                            s.Timpani(m, T(16), timp, 0.6f, 1.5f);
                        }
                        else if (bar < 20)
                        {
                            s.Taiko(m, T(bar), 0.4f, 0.88f, 0f, 0.75f);
                            if (bar == 19) s.Taiko(m, T(19, 3), 0.35f, 0.9f, 0.2f, 0.7f);
                        }
                        else if (bar < 22)
                        {
                            for (int q = 0; q < 4; q++)
                            {
                                float u = ((bar - 20) * 4 + q) / 8f;
                                s.Taiko(m, T(bar, q), 0.35f + 0.25f * u, q % 2 == 0 ? 0.9f : 1.2f, q % 2 == 0 ? 0f : 0.3f, 0.45f);
                                if (q % 2 == 0) s.Timpani(m, T(bar, q), timp, 0.2f + 0.2f * u, 0.7f);
                            }
                        }
                        else
                        {
                            for (int e = 0; e < 8; e++)
                            {
                                if (bar == 23 && e == 7) continue;
                                float u = ((bar - 22) * 8 + e) / 15f;
                                s.Taiko(m, T(bar, e * 0.5f), 0.5f + 0.5f * u, e % 2 == 0 ? 0.9f : 1.25f, e % 2 == 0 ? 0f : -0.3f, 0.35f);
                            }
                            if (bar == 23)
                                for (int k = 0; k < 14; k++) s.Taiko(m, T(23, k * 0.25f), 0.25f + k * 0.02f, 1.6f, k % 2 == 0 ? -0.3f : 0.3f, 0.35f);
                        }
                        if (bar == 21)
                        {
                            Roll(s, m, T(21), T(22), 0.04f, 0.15f, 4f / beat);
                            Roll(s, m, T(22), T(23, 3.5f), 0.15f, 0.6f, rollRate);
                            for (float b = 0f; b < 7.5f; b += 0.25f)
                                s.Timpani(m, T(22, b), Synth.Midi(45), 0.1f + 0.35f * (b / 7.5f), 0.5f);
                            s.Riser(m, T(22), T(24) - T(22), 0.22f);
                            s.ReverseSwell(m, T(24), 2.5f * beat, 0.35f);
                        }
                        break;

                    case 3:
                        Groove(bar, 1.1f, true);
                        SnarePattern(bar, snareClimax, bar == 27 ? 3f : bar == 31 ? 2f : 4f);
                        s.Timpani(m, T(bar), timp, 0.55f);
                        s.Timpani(m, T(bar, 2), timp, 0.35f);
                        if (bar == 24 || bar == 28)
                        {
                            s.Boom(m, T(bar), 1f);
                            s.Cymbal(m, T(bar), 0.55f, 1.8f, 0.35f);
                        }
                        if (bar == 27) Roll(s, m, T(27, 3), T(28), 0.2f, 0.55f, rollRate);
                        if (bar == 31)
                        {
                            Roll(s, m, T(31, 2), T(32), 0.2f, 0.7f, rollRate);
                            TomFill(31, 2f);
                        }
                        break;

                    default:
                        if (bar == 32)
                        {
                            s.Boom(m, T(32), 1f);
                            s.Taiko(m, T(32), 1f, 0.85f, 0f, 0.4f);
                            s.Timpani(m, T(32), Synth.Midi(38), 0.7f, 1.6f);
                            s.Cymbal(m, T(32), 0.5f, 2.4f, -0.3f);
                        }
                        else s.Taiko(m, T(33, 2), 0.3f, 0.85f, 0f, 0.8f); // distant echo before the intro returns
                        break;
                }
            }

            m.Finish(MusicHall, (l, r) =>
            {
                Synth.Filter(l, FilterType.High, 28f);
                Synth.Filter(r, FilterType.High, 28f);
                Synth.Normalize(l, r, 1f, 0f);
                Synth.Compress(l, r, -12f, 3f, 0.015f, 0.3f);
            }, out left, out right);
            Synth.Normalize(left, right, 0.8f, 0.25f);
        }

        /// <summary>Trailer "braam": driven low brass cluster on D with a sub boom.</summary>
        private static void Braam(Synth s, Synth.Mix m, float t, float gain)
        {
            int[] notes = { 26, 38, 45, 50 };
            float[] gains = { 0.5f, 0.4f, 0.3f, 0.25f };
            for (int i = 0; i < notes.Length; i++) s.Note(m, t, 1.2f, Synth.Midi(notes[i]), gains[i] * gain * 0.6f, BraamPatch, (i - 1.5f) * 0.2f, 0.45f);
            s.Boom(m, t, gain);
        }

        /// <summary>
        /// Battlefield bed (~40 s, mono, seamless): wind and room tone with periodic gusts, distant artillery with long dark tails,
        /// far-off small-arms bursts, metal creaks and a distant clank.
        /// </summary>
        private static float[] Ambience(int seed)
        {
            const float seconds = 40f;
            var s = new Synth(seed);
            var m = new Synth.Mix(seconds, stereo: false, loop: true);
            int n = m.Length, sr = Synth.SampleRate;

            // Bed: all modulation completes whole cycles over the loop; the extra tail is crossfaded into the start.
            int crossfade = sr * 2;
            var bed = new float[n + crossfade];
            var windBp = new Synth.Svf(300f, 1.2f);
            var whistleBp = new Synth.Svf(1100f, 9f);
            var roomLp = new Synth.Svf(140f);
            for (int i = 0; i < bed.Length; i++)
            {
                double ph = (double)i / n * Math.PI * 2.0;
                float gust = 0.55f + 0.25f * (float)Math.Sin(2.0 * ph) + 0.15f * (float)Math.Sin(5.0 * ph + 1.3) + 0.05f * (float)Math.Sin(11.0 * ph + 0.4);
                if ((i & 31) == 0)
                {
                    windBp.Set(260f + 260f * gust, 1.2f);
                    whistleBp.Set(900f + 500f * gust, 9f);
                }
                float room = roomLp.Process(s.Brown()) * 0.5f;
                float wind = windBp.Process(s.Pink(), FilterType.Band) * gust * 0.9f;
                float whistle = whistleBp.Process(s.Noise(), FilterType.Band) * Math.Max(0f, gust - 0.6f) * 0.5f;
                bed[i] = room + wind + whistle;
            }
            for (int i = 0; i < crossfade; i++)
            {
                float w = i / (float)crossfade;
                bed[i] = bed[i] * (float)Math.Sqrt(w) + bed[n + i] * (float)Math.Sqrt(1f - w);
            }
            for (int i = 0; i < n; i++) m.Add(i, bed[i] * 0.70710678f, bed[i] * 0.70710678f, 0.15f);

            // Distant artillery (some with a follow-up salvo).
            float[] booms = { 1.2f, 5.9f, 9.6f, 14.8f, 18.1f, 23.4f, 27.0f, 31.7f, 36.2f };
            foreach (float t in booms)
            {
                float g = s.Range(0.35f, 1f);
                DistantBoom(s, m, t, g);
                if (s.Chance(0.3f)) DistantBoom(s, m, t + s.Range(0.4f, 0.9f), g * 0.6f);
            }

            // Far-off small-arms fire.
            DistantBurst(s, m, 3.6f, 6, 0.095f, 0.5f);
            DistantBurst(s, m, 12.2f, 3, 0.11f, 0.35f);
            DistantBurst(s, m, 20.5f, 9, 0.085f, 0.45f);
            DistantBurst(s, m, 29.3f, 2, 0.4f, 0.4f);
            DistantBurst(s, m, 34.0f, 5, 0.1f, 0.3f);

            // Structure creaks and a distant clank.
            s.Creak(m, 10.5f, 1.3f, 0.05f, 14f, 30f, new[] { 340f, 790f, 1630f }, 16f, 0f, 0.4f);
            s.Creak(m, 25.4f, 0.9f, 0.04f, 22f, 12f, new[] { 420f, 960f, 2100f }, 16f, 0f, 0.4f);
            s.Creak(m, 37.0f, 0.7f, 0.035f, 18f, 26f, new[] { 380f, 870f, 1900f }, 16f, 0f, 0.4f);
            s.Modal(m, 16.6f, 310f, PlateRatios, PlateAmps, PlateDecays, 0.06f, 0f, 0.9f, 0.01f, 0.2f);

            m.Finish(DistantField, (l, r) =>
            {
                Synth.Filter(l, FilterType.High, 25f);
                Synth.Normalize(l, null, 1f, 0f);
                Synth.Compress(l, null, -18f, 3f, 0.01f, 0.4f);
            }, out float[] result, out _);
            Synth.Normalize(result, 0.65f);
            return result;
        }

        private static void DistantBoom(Synth s, Synth.Mix m, float t, float gain)
        {
            Synth.Tone(m, t, 3f, 62f, 30f, 0.2f, gain, 0.02f, 0.7f, Wave.Sine, 0.4f, 0f, 0.8f);
            s.NoiseHit(m, t, 3f, gain * 0.8f, 0.015f, 0.6f, FilterType.Low, 260f, 70f, 0.5f, 0.7f, NoiseColor.Brown, 0f, 0f, 0.8f);
            s.NoiseHit(m, t, 0.4f, gain * 0.25f, 0.004f, 0.08f, FilterType.Low, 700f, 200f, 0.1f, 0.7f, NoiseColor.White, 0f, 0f, 0.9f);
        }

        private static void DistantBurst(Synth s, Synth.Mix m, float start, int shots, float interval, float gain)
        {
            float t = start;
            for (int k = 0; k < shots; k++)
            {
                float g = gain * s.Range(0.7f, 1f);
                s.NoiseHit(m, t, 0.2f, g * 0.5f, 0.001f, 0.03f, FilterType.Low, 1100f, 350f, 0.03f, 0.8f, NoiseColor.White, 1f, 0f, 0.9f);
                Synth.Tone(m, t, 0.2f, 140f, 65f, 0.015f, g * 0.35f, 0.001f, 0.04f, Wave.Sine, 0.4f, 0f, 0.9f);
                t += interval * s.Range(0.9f, 1.1f);
            }
        }

        // ------------------------------------------------------------------ assets

        private static void ConfigureImporter(string path, bool stereo)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            bool isMusic = path.StartsWith(MusicFolder, StringComparison.Ordinal);
            importer.forceToMono = !stereo;
            importer.loadInBackground = isMusic;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isMusic ? 0.55f : 0.75f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static void BuildLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            AudioClip Sfx(string name) => Load(SfxFolder, name);

            library.menuMusic = Load(MusicFolder, "Menu_Loop");
            library.warehouseAmbience = Load(MusicFolder, "Warehouse_Ambience");
            library.buttonClick = Sfx("ButtonClick");
            library.countdownTick = Sfx("CountdownTick");
            library.countdownGo = Sfx("CountdownGo");
            library.victory = Sfx("Victory");
            library.defeat = Sfx("Defeat");
            library.draw = Sfx("Draw");
            library.footsteps = new[] { Sfx("Footstep_1"), Sfx("Footstep_2"), Sfx("Footstep_3"), Sfx("Footstep_4") };
            library.jump = Sfx("Jump");
            library.land = Sfx("Land");
            library.hurt = Sfx("Hurt");
            library.death = Sfx("Death");
            library.spawn = Sfx("Spawn");
            library.weaponSwitch = Sfx("WeaponSwitch");
            library.hitMarker = Sfx("HitMarker");
            library.headshotMarker = Sfx("HeadshotMarker");
            library.elimination = Sfx("Elimination");
            library.impacts = new[] { Sfx("Impact_1"), Sfx("Impact_2"), Sfx("Impact_3") };

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
    }
}
