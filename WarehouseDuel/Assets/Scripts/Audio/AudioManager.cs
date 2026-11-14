using UnityEngine;
using WarehouseWar.Core;

namespace WarehouseWar.Audio
{
    /// <summary>
    /// Process-wide audio service: one music source, one ambience source and a fixed pool of
    /// SFX sources (no AudioSource allocation during play). Volumes follow <see cref="GameSettings"/>.
    /// Created on first use and kept across scene loads.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int PoolSize = 20;

        private static AudioManager instance;

        private AudioSource music;
        private AudioSource ambience;
        private AudioSource ui;
        private AudioSource[] pool;
        private int nextPoolIndex;
        private float ambienceBaseVolume = 0.35f;
        private float musicBaseVolume = 1f;

        public static AudioLibrary Library { get; private set; }

        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Audio Manager");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<AudioManager>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            instance = this;
            Library = Resources.Load<AudioLibrary>(AudioLibrary.ResourcePath);
            if (Library == null) Debug.LogWarning("[Audio] AudioLibrary not found in Resources. Run Tools > Warehouse War > Generate Audio.");

            music = CreateSource("Music", loop: true);
            ambience = CreateSource("Ambience", loop: true);
            ui = CreateSource("UI", loop: false);

            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource s = CreateSource($"Sfx {i}", loop: false);
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2f;
                s.maxDistance = 45f;
                s.dopplerLevel = 0f;
                pool[i] = s;
            }

            GameSettings.Changed += ApplyVolumes;
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolumes;
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------ music

        public void PlayMusic(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (music.clip == clip && music.isPlaying) return;
            music.clip = clip;
            musicBaseVolume = volume;
            music.volume = volume * GameSettings.MusicVolume;
            music.Play();
        }

        public void StopMusic() => music.Stop();

        public void PlayAmbience(AudioClip clip, float volume = 0.35f)
        {
            if (clip == null) return;
            ambienceBaseVolume = volume;
            ambience.clip = clip;
            ambience.volume = volume * GameSettings.SfxVolume;
            if (!ambience.isPlaying) ambience.Play();
        }

        public void StopAmbience() => ambience.Stop();

        // ------------------------------------------------------------------ effects

        /// <summary>Non-positional sound (UI, local player's own weapon, announcer).</summary>
        public void Play2D(AudioClip clip, float volume = 1f, float pitchVariance = 0f)
        {
            if (clip == null) return;
            AudioSource source = NextSource();
            source.spatialBlend = 0f;
            source.transform.position = Vector3.zero;
            Play(source, clip, volume, pitchVariance);
        }

        /// <summary>3D sound at a world position (opponent's gunfire, impacts, footsteps).</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariance = 0.05f)
        {
            if (clip == null) return;
            AudioSource source = NextSource();
            source.spatialBlend = 1f;
            source.transform.position = position;
            Play(source, clip, volume, pitchVariance);
        }

        public void PlayUI(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            ui.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        public static AudioClip Pick(AudioClip[] clips) =>
            clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];

        private void Play(AudioSource source, AudioClip clip, float volume, float pitchVariance)
        {
            source.Stop();
            source.clip = clip;
            source.volume = volume * GameSettings.SfxVolume;
            source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            source.Play();
        }

        private AudioSource NextSource()
        {
            // Prefer an idle source; otherwise steal the oldest (round-robin).
            for (int i = 0; i < pool.Length; i++)
            {
                int index = (nextPoolIndex + i) % pool.Length;
                if (!pool[index].isPlaying)
                {
                    nextPoolIndex = (index + 1) % pool.Length;
                    return pool[index];
                }
            }
            AudioSource stolen = pool[nextPoolIndex];
            nextPoolIndex = (nextPoolIndex + 1) % pool.Length;
            return stolen;
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private void ApplyVolumes()
        {
            if (music != null) music.volume = musicBaseVolume * GameSettings.MusicVolume;
            if (ambience != null) ambience.volume = ambienceBaseVolume * GameSettings.SfxVolume;
        }
    }
}
