using UnityEngine;

namespace WarehouseDuel.Audio
{
    /// <summary>
    /// Shared non-weapon sounds (weapon sounds live on each WeaponDefinition).
    /// Stored at Assets/Resources/AudioLibrary.asset so the AudioManager can load it from any scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Warehouse Duel/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        public const string ResourcePath = "AudioLibrary";

        [Header("Music")]
        public AudioClip menuMusic;
        public AudioClip warehouseAmbience;

        [Header("UI")]
        public AudioClip buttonClick;
        public AudioClip countdownTick;
        public AudioClip countdownGo;
        public AudioClip victory;
        public AudioClip defeat;
        public AudioClip draw;

        [Header("Player")]
        public AudioClip[] footsteps;
        public AudioClip jump;
        public AudioClip land;
        public AudioClip hurt;
        public AudioClip death;
        public AudioClip spawn;
        public AudioClip weaponSwitch;

        [Header("Combat feedback")]
        public AudioClip hitMarker;
        public AudioClip headshotMarker;
        public AudioClip elimination;
        public AudioClip[] impacts;
    }
}
