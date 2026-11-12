using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WarehouseDuel.Audio;
using Wave = WarehouseDuel.EditorTools.Audio.Synth.Wave;

namespace WarehouseDuel.EditorTools.Audio
{
    /// <summary>
    /// Generates all of the game's audio as original WAV files (no third-party assets, no licensing questions):
    /// weapon shots/reloads, player and UI sounds, a looping menu track and a warehouse ambience loop.
    /// Also creates Assets/Resources/AudioLibrary.asset referencing them.
    /// </summary>
    internal static class AudioGenerator
    {
        public const string SfxFolder = "Assets/Audio/SFX";
        public const string MusicFolder = "Assets/Audio/Music";
        public const string LibraryPath = "Assets/Resources/AudioLibrary.asset";

        /// <summary>Weapon clip names, read by the weapon builder: Fire_{key}, Reload_{key}.</summary>
        public static readonly string[] WeaponKeys = { "Rifle", "SMG", "Shotgun", "Pistol" };

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

            var written = new List<string>();
            void Sfx(string name, float[] data) { Synth.Normalize(data); written.Add(Save($"{SfxFolder}/{name}.wav", data)); }

            // Weapons --------------------------------------------------------------
            Sfx("Fire_Rifle", Gunshot(1, body: 105f, crack: 7000f, decay: 0.07f, tail: 0.28f, length: 0.55f));
            Sfx("Fire_SMG", Gunshot(2, body: 140f, crack: 8000f, decay: 0.045f, tail: 0.18f, length: 0.4f));
            Sfx("Fire_Shotgun", Gunshot(3, body: 70f, crack: 4500f, decay: 0.13f, tail: 0.45f, length: 0.85f, weight: 1.6f));
            Sfx("Fire_Pistol", Gunshot(4, body: 160f, crack: 6500f, decay: 0.055f, tail: 0.22f, length: 0.45f));
            Sfx("Reload_Rifle", Reload(5, new[] { 0.05f, 0.55f, 1.05f, 1.35f }, 1.6f));
            Sfx("Reload_SMG", Reload(6, new[] { 0.05f, 0.45f, 0.95f, 1.15f }, 1.4f));
            Sfx("Reload_Pistol", Reload(7, new[] { 0.05f, 0.5f, 0.9f }, 1.15f));
            Sfx("Reload_Shotgun", ShotgunReload(8));
            Sfx("DryFire", Click(9, 2600f, 0.012f, 0.12f));

            // Player -----------------------------------------------------------------
            for (int i = 0; i < 4; i++) Sfx($"Footstep_{i + 1}", Footstep(20 + i));
            Sfx("Jump", Jump(30));
            Sfx("Land", Land(31));
            Sfx("Hurt", Hurt(32));
            Sfx("Death", Death(33));
            Sfx("Spawn", Spawn());
            Sfx("WeaponSwitch", WeaponSwitch(34));

            // Feedback / UI ------------------------------------------------------------
            Sfx("HitMarker", Ping(1900f, 0.05f, 0.09f));
            Sfx("HeadshotMarker", HeadshotPing());
            Sfx("Elimination", Chime(new[] { 784f, 1175f, 1568f }, 0.07f, 0.6f));
            Sfx("ButtonClick", Click(40, 1400f, 0.02f, 0.06f));
            Sfx("CountdownTick", Ping(660f, 0.12f, 0.25f));
            Sfx("CountdownGo", Ping(1320f, 0.25f, 0.55f, Wave.Square));
            Sfx("Victory", Jingle(new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.13f, 1.6f, major: true));
            Sfx("Defeat", Jingle(new[] { 440f, 392f, 349f, 262f }, 0.2f, 1.6f, major: false));
            Sfx("Draw", Jingle(new[] { 523f, 659f, 523f }, 0.2f, 1.2f, major: true));
            for (int i = 0; i < 3; i++) Sfx($"Impact_{i + 1}", Impact(50 + i));

            // Music & ambience (normalised quieter so they sit under the effects).
            float[] menu = MenuMusic();
            Synth.Normalize(menu, 0.8f);
            written.Add(Save($"{MusicFolder}/Menu_Loop.wav", menu));
            float[] ambience = Ambience(60);
            Synth.Normalize(ambience, 0.7f);
            written.Add(Save($"{MusicFolder}/Warehouse_Ambience.wav", ambience));

            AssetDatabase.Refresh();
            foreach (string path in written) ConfigureImporter(path);

            BuildLibrary();
            report.Pass($"Generated {written.Count} original audio clips and {LibraryPath}.");
        }

        public static AudioClip Load(string folder, string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{name}.wav");

        // ------------------------------------------------------------------ recipes

        private static float[] Gunshot(int seed, float body, float crack, float decay, float tail, float length, float weight = 1f)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(length);
            s.AddNoiseBurst(b, 0f, 1.0f, decay, crack, 900f, 0.0008f);            // muzzle crack
            Synth.AddTone(b, 0f, 0.25f, body, body * 0.45f, 0.9f * weight, decay * 1.4f); // body thump
            s.AddNoiseBurst(b, 0.004f, 0.35f * weight, tail, 1400f, 300f, 0.01f);  // low rumble tail
            Synth.AddRoom(b, 0.041f, 0.35f, 0.4f);
            Synth.FadeOut(b, 0.05f);
            return b;
        }

        private static float[] Reload(int seed, float[] clickTimes, float length)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(length);
            for (int i = 0; i < clickTimes.Length; i++)
            {
                float t = clickTimes[i];
                s.AddNoiseBurst(b, t, 0.7f, 0.012f, 5000f, 1800f, 0.0005f);
                Synth.AddTone(b, t, 0.08f, 1800f + i * 350f, 1500f + i * 300f, 0.35f, 0.02f, Wave.Triangle);
                s.AddNoiseBurst(b, t + 0.03f, 0.25f, 0.04f, 900f, 300f);
            }
            Synth.AddRoom(b, 0.03f, 0.2f, 0.2f);
            return b;
        }

        private static float[] ShotgunReload(int seed)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(2.4f);
            for (int i = 0; i < 4; i++)
            {
                float t = 0.15f + i * 0.42f;
                s.AddNoiseBurst(b, t, 0.6f, 0.02f, 3500f, 900f);
                Synth.AddTone(b, t, 0.1f, 900f, 700f, 0.3f, 0.03f, Wave.Triangle);
            }
            // Pump: back and forward.
            s.AddNoiseBurst(b, 1.95f, 0.9f, 0.05f, 2500f, 600f);
            Synth.AddTone(b, 1.95f, 0.12f, 300f, 180f, 0.5f, 0.05f);
            s.AddNoiseBurst(b, 2.12f, 1.0f, 0.04f, 3000f, 700f);
            Synth.AddTone(b, 2.12f, 0.12f, 260f, 200f, 0.5f, 0.04f);
            return b;
        }

        private static float[] Click(int seed, float pitch, float decay, float length)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(length);
            s.AddNoiseBurst(b, 0f, 0.5f, decay * 0.6f, 6000f, 2000f, 0.0003f);
            Synth.AddTone(b, 0f, length, pitch, pitch * 0.8f, 0.6f, decay, Wave.Triangle, 0.0005f);
            return b;
        }

        private static float[] Footstep(int seed)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(0.22f);
            s.AddNoiseBurst(b, 0f, 0.9f, 0.03f, s.Range(500f, 800f), 150f, 0.002f);
            Synth.AddTone(b, 0f, 0.12f, s.Range(90f, 120f), 60f, 0.6f, 0.03f);
            s.AddNoiseBurst(b, 0.01f, 0.15f, 0.015f, 4000f, 2000f, 0.001f); // grit
            return b;
        }

        private static float[] Jump(int seed)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(0.3f);
            s.AddNoiseBurst(b, 0f, 0.5f, 0.06f, 700f, 200f, 0.004f);
            s.AddNoiseBurst(b, 0.02f, 0.35f, 0.1f, 1200f, 3000f, 0.03f); // whoosh
            return b;
        }

        private static float[] Land(int seed)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(0.3f);
            s.AddNoiseBurst(b, 0f, 1f, 0.05f, 600f, 120f, 0.001f);
            Synth.AddTone(b, 0f, 0.2f, 85f, 45f, 0.9f, 0.06f);
            return b;
        }

        private static float[] Hurt(int seed)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(0.3f);
            Synth.AddTone(b, 0f, 0.25f, 210f, 120f, 0.8f, 0.07f, Wave.Square);
            s.AddNoiseBurst(b, 0f, 0.5f, 0.04f, 1500f, 400f);
            return b;
        }

        private static float[] Death(int seed)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(0.9f);
            Synth.AddTone(b, 0f, 0.8f, 420f, 90f, 0.7f, 0.3f, Wave.Saw, 0.005f);
            s.AddNoiseBurst(b, 0.05f, 0.4f, 0.2f, 900f, 150f, 0.01f);
            Synth.AddRoom(b);
            return b;
        }

        private static float[] Spawn()
        {
            float[] b = Synth.Buffer(0.7f);
            Synth.AddTone(b, 0f, 0.5f, 400f, 1600f, 0.4f, 0.25f, Wave.Triangle, 0.02f);
            Synth.AddTone(b, 0.05f, 0.5f, 600f, 2400f, 0.25f, 0.25f, Wave.Sine, 0.02f);
            Synth.AddRoom(b, 0.06f, 0.35f, 0.4f);
            return b;
        }

        private static float[] WeaponSwitch(int seed)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(0.3f);
            s.AddNoiseBurst(b, 0f, 0.6f, 0.015f, 4000f, 1500f, 0.0005f);
            Synth.AddTone(b, 0f, 0.06f, 1400f, 1200f, 0.3f, 0.02f, Wave.Triangle);
            s.AddNoiseBurst(b, 0.14f, 0.7f, 0.015f, 4500f, 1500f, 0.0005f);
            Synth.AddTone(b, 0.14f, 0.06f, 1700f, 1500f, 0.3f, 0.02f, Wave.Triangle);
            return b;
        }

        private static float[] Ping(float frequency, float decay, float length, Wave wave = Wave.Sine)
        {
            float[] b = Synth.Buffer(length);
            Synth.AddTone(b, 0f, length, frequency, frequency, 0.8f, decay, wave, 0.001f);
            Synth.AddTone(b, 0f, length, frequency * 2f, frequency * 2f, 0.2f, decay * 0.5f, Wave.Sine, 0.001f);
            return b;
        }

        private static float[] HeadshotPing()
        {
            float[] b = Synth.Buffer(0.18f);
            Synth.AddTone(b, 0f, 0.18f, 2400f, 2400f, 0.6f, 0.05f, Wave.Sine, 0.001f);
            Synth.AddTone(b, 0.045f, 0.13f, 3200f, 3200f, 0.5f, 0.05f, Wave.Sine, 0.001f);
            return b;
        }

        private static float[] Chime(float[] notes, float spacing, float length)
        {
            float[] b = Synth.Buffer(length);
            for (int i = 0; i < notes.Length; i++)
            {
                Synth.AddTone(b, i * spacing, length - i * spacing, notes[i], notes[i], 0.5f, 0.18f, Wave.Triangle, 0.003f);
                Synth.AddTone(b, i * spacing, length - i * spacing, notes[i] * 2f, notes[i] * 2f, 0.15f, 0.1f, Wave.Sine, 0.003f);
            }
            Synth.AddRoom(b, 0.07f, 0.3f, 0.3f);
            return b;
        }

        private static float[] Jingle(float[] notes, float spacing, float length, bool major)
        {
            float[] b = Synth.Buffer(length);
            for (int i = 0; i < notes.Length; i++)
            {
                bool last = i == notes.Length - 1;
                float duration = last ? length - i * spacing - 0.3f : spacing * 0.9f;
                Synth.AddNote(b, i * spacing, duration, notes[i], 0.35f, Wave.Square, 0.005f, 0.25f, 3500f);
                Synth.AddNote(b, i * spacing, duration, notes[i] / 2f, 0.3f, Wave.Triangle, 0.005f, 0.25f);
            }
            float root = notes[notes.Length - 1];
            float third = root * (major ? 1.26f : 1.19f);
            float start = (notes.Length - 1) * spacing;
            Synth.AddNote(b, start, length - start - 0.3f, third, 0.2f, Wave.Triangle, 0.01f, 0.25f);
            Synth.AddNote(b, start, length - start - 0.3f, root * 1.5f, 0.2f, Wave.Triangle, 0.01f, 0.25f);
            Synth.AddRoom(b, 0.08f, 0.3f, 0.3f);
            return b;
        }

        private static float[] Impact(int seed)
        {
            var s = new Synth(seed);
            float[] b = Synth.Buffer(0.18f);
            s.AddNoiseBurst(b, 0f, 1f, 0.02f, s.Range(3500f, 6000f), 900f, 0.0003f);
            Synth.AddTone(b, 0f, 0.08f, s.Range(500f, 900f), 300f, 0.3f, 0.02f, Wave.Triangle);
            return b;
        }

        /// <summary>8-bar loop, 104 BPM, Am–F–C–G: bass, filtered saw pad, arpeggio, drums. Loops seamlessly.</summary>
        private static float[] MenuMusic()
        {
            const float bpm = 104f;
            float beat = 60f / bpm;
            int bars = 8;
            float[] b = Synth.Buffer(bars * 4 * beat);
            var s = new Synth(99);

            float[][] chords =
            {
                new[] { 220.00f, 261.63f, 329.63f }, // Am
                new[] { 174.61f, 220.00f, 261.63f }, // F
                new[] { 261.63f, 329.63f, 392.00f }, // C
                new[] { 196.00f, 246.94f, 293.66f }, // G
            };

            for (int bar = 0; bar < bars; bar++)
            {
                float[] chord = chords[bar % 4];
                float barStart = bar * 4 * beat;

                foreach (float note in chord) Synth.AddNote(b, barStart, 4 * beat - 0.05f, note, 0.07f, Wave.Saw, 0.25f, 0.3f, 1200f);

                for (int e = 0; e < 8; e++)
                {
                    float t = barStart + e * beat / 2f;
                    float bass = chord[0] / 2f * (e % 4 == 3 ? 1.5f : 1f);
                    Synth.AddNote(b, t, beat / 2f - 0.04f, bass, 0.22f, Wave.Square, 0.005f, 0.04f, 700f);
                }

                if (bar >= 2)
                {
                    for (int sx = 0; sx < 16; sx++)
                    {
                        float note = chord[sx % 3] * (sx % 6 < 3 ? 2f : 4f);
                        Synth.AddNote(b, barStart + sx * beat / 4f, beat / 4f * 0.6f, note, 0.05f, Wave.Triangle, 0.003f, 0.05f);
                    }
                }

                for (int q = 0; q < 4; q++)
                {
                    float t = barStart + q * beat;
                    if (q % 2 == 0) Synth.AddTone(b, t, 0.25f, 120f, 45f, 0.75f, 0.07f, Wave.Sine, 0.001f);
                    else s.AddNoiseBurst(b, t, 0.35f, 0.07f, 5000f, 1500f, 0.001f);
                    s.AddNoiseBurst(b, t + beat / 2f, 0.08f, 0.02f, 9000f, 6000f, 0.0005f);
                }
            }
            return b;
        }

        /// <summary>Low industrial hum with a slow-moving fan noise; crossfaded so it loops seamlessly.</summary>
        private static float[] Ambience(int seed)
        {
            var s = new Synth(seed);
            const float seconds = 12f;
            float[] b = Synth.Buffer(seconds);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Synth.SampleRate;
                lp = Synth.OnePole(lp, s.Noise(), 260f);
                lp2 = Synth.OnePole(lp2, lp, 180f);
                float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / seconds * 2f);
                float hum = 0.25f * Mathf.Sin(2f * Mathf.PI * 60f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 120f * t);
                b[i] = lp2 * 3f * swell + hum * 0.4f;
            }
            // Crossfade the last second into the first so the loop point is inaudible.
            int fade = Synth.SampleRate;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                b[i] = b[i] * w + b[b.Length - fade + i] * (1f - w);
            }
            System.Array.Resize(ref b, b.Length - fade);
            return b;
        }

        // ------------------------------------------------------------------ assets

        private static string Save(string path, float[] data)
        {
            Synth.WriteWav(path, data);
            return path;
        }

        private static void ConfigureImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            bool isMusic = path.StartsWith(MusicFolder);
            importer.forceToMono = true;
            importer.loadInBackground = isMusic;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isMusic ? 0.6f : 0.75f;
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
