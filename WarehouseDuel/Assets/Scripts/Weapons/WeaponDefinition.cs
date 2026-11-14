using UnityEngine;

namespace WarehouseWar.Weapons
{
    public enum FireMode : byte
    {
        Automatic = 0,
        SemiAutomatic = 1,
    }

    /// <summary>How a weapon resolves a shot.</summary>
    public enum WeaponKind : byte
    {
        /// <summary>Instant lag-compensated rays (pellets) with damage falloff: rifle, SMG, shotgun, pistol, sniper, LMG.</summary>
        Hitscan = 0,
        /// <summary>Short lag-compensated ray fan in front of the player; no ammo: knife.</summary>
        Melee = 1,
        /// <summary>Instant ray to the impact point, then splash damage around it: RPG.</summary>
        Explosive = 2,
    }

    /// <summary>
    /// All tuning and presentation data for one weapon. Gameplay code reads only from here,
    /// so balancing never requires code changes. Assets live in Assets/ScriptableObjects/Weapons.
    /// </summary>
    [CreateAssetMenu(menuName = "Warehouse War/Weapon Definition", fileName = "Weapon_")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Weapon";
        [TextArea] public string description;
        public Sprite icon;

        [Header("Damage")]
        [Min(1f)] public float damage = 20f;
        [Min(1f)] public float headshotMultiplier = 1.5f;
        [Tooltip("Rays per shot. 1 for bullets, 6-10 for a shotgun.")]
        [Range(1, 16)] public int pellets = 1;
        [Min(1f)] public float range = 80f;
        [Tooltip("Full damage up to this distance (m).")]
        [Min(0f)] public float falloffStart = 25f;
        [Tooltip("Minimum damage reached at this distance (m).")]
        [Min(0f)] public float falloffEnd = 60f;
        [Range(0.05f, 1f)] public float minDamageMultiplier = 0.6f;

        [Header("Firing")]
        public FireMode fireMode = FireMode.Automatic;
        [Min(30f)] public float roundsPerMinute = 600f;
        [Tooltip("Cone half-angle in degrees when firing from the hip.")]
        [Range(0f, 15f)] public float hipSpread = 2f;
        [Tooltip("Cone half-angle in degrees while aiming.")]
        [Range(0f, 15f)] public float aimSpread = 0.5f;

        [Header("Ammunition")]
        [Min(1)] public int magazineSize = 30;
        [Tooltip("Spare rounds carried in addition to the loaded magazine (refilled on respawn).")]
        [Min(0)] public int reserveAmmo = 120;
        [Min(0.1f)] public float reloadSeconds = 2f;

        [Header("Handling")]
        [Tooltip("Camera kick per shot, degrees upward.")]
        [Range(0f, 10f)] public float recoilPitch = 1f;
        [Tooltip("Random sideways camera kick per shot, degrees (±).")]
        [Range(0f, 5f)] public float recoilYaw = 0.3f;
        [Range(0.5f, 1.2f)] public float moveSpeedMultiplier = 1f;
        [Tooltip("Camera field of view while fully aimed (a scoped sniper uses ~18).")]
        [Range(10f, 80f)] public float aimFieldOfView = 55f;
        [Min(0f)] public float equipSeconds = 0.35f;

        [Header("Presentation")]
        public GameObject viewModelPrefab;
        [Tooltip("Extra first-person hip offset for this weapon (m, camera space). Fades out while aiming.")]
        public Vector3 viewModelOffset;
        public GameObject worldModelPrefab;
        public AudioClip fireSound;
        public AudioClip reloadSound;
        public AudioClip dryFireSound;
        public Color muzzleFlashColor = new Color(1f, 0.8f, 0.4f);
        [Min(0.1f)] public float muzzleFlashScale = 1f;
        [Tooltip("View-model kick back per shot (m).")]
        [Range(0f, 0.2f)] public float viewKick = 0.04f;

        // ------------------------------------------------------------------ appended for the 8-weapon arsenal

        [Header("Weapon type")]
        public WeaponKind kind = WeaponKind.Hitscan;
        [Tooltip("False for melee weapons: no magazine, never reloads (the HUD shows ∞).")]
        public bool usesAmmo = true;
        [Tooltip("Aiming looks through a scope: the HUD draws the scope overlay and the first-person model hides when fully aimed.")]
        public bool hasScope;
        [Tooltip("Extra first-person rotation of the view model (degrees), e.g. to angle the knife blade.")]
        public Vector3 viewModelEuler;

        [Header("Melee")]
        [Tooltip("Damage multiplier when the victim is hit from behind (knife).")]
        [Min(1f)] public float backstabMultiplier = 2f;

        [Header("Explosive")]
        [Tooltip("Splash radius around the impact point (m).")]
        [Min(0f)] public float explosionRadius = 4.5f;
        [Tooltip("Splash damage at the centre of the explosion.")]
        [Min(0f)] public float explosionDamage = 125f;
        [Tooltip("Full splash damage within this distance (m); falls off linearly to the edge.")]
        [Min(0f)] public float explosionFullDamageRadius = 1f;
        [Tooltip("Fraction of the centre damage dealt at the very edge of the radius.")]
        [Range(0f, 1f)] public float explosionEdgeMultiplier = 0.15f;
        [Tooltip("Fraction of the splash the shooter takes from their own explosion. Self-damage can never kill (leaves at least 1 HP).")]
        [Range(0f, 1f)] public float selfDamageMultiplier = 0.3f;

        [Header("Impact audio (melee / explosive)")]
        [Tooltip("Knife hitting a surface, or the explosion of an explosive weapon.")]
        public AudioClip impactSound;
        [Tooltip("Knife hitting a player (falls back to impactSound).")]
        public AudioClip impactPlayerSound;

        /// <summary>Seconds between shots implied by <see cref="roundsPerMinute"/>.</summary>
        public float SecondsBetweenShots => 60f / roundsPerMinute;

        /// <summary>
        /// Scale for guns held by the chunky voxel soldiers (menu/lobby showcase and other players in a match).
        /// Real-size models look like toys against the blocky arms, so held weapons are drawn larger.
        /// </summary>
        public const float HeldScale = 1.55f;

        /// <summary>Damage shown in menus: splash for explosives, all pellets for shotguns.</summary>
        public float DisplayDamage => IsExplosive ? explosionDamage : damage * pellets;

        /// <summary>Range shown in menus: full range when damage never falls off, else the falloff end.</summary>
        public float DisplayRange => IsMelee || minDamageMultiplier >= 0.99f ? range : Mathf.Min(range, falloffEnd);

        public bool IsMelee => kind == WeaponKind.Melee;
        public bool IsExplosive => kind == WeaponKind.Explosive;

        /// <summary>Damage multiplier for a hit at <paramref name="distance"/> metres.</summary>
        public float FalloffMultiplier(float distance)
        {
            if (distance <= falloffStart) return 1f;
            if (distance >= falloffEnd) return minDamageMultiplier;
            return Mathf.Lerp(1f, minDamageMultiplier, Mathf.InverseLerp(falloffStart, falloffEnd, distance));
        }

        /// <summary>Splash damage for a target <paramref name="distance"/> metres from the explosion (0 outside the radius).</summary>
        public float SplashDamage(float distance)
        {
            if (distance >= explosionRadius) return 0f;
            if (distance <= explosionFullDamageRadius) return explosionDamage;
            float t = Mathf.InverseLerp(explosionFullDamageRadius, explosionRadius, distance);
            return explosionDamage * Mathf.Lerp(1f, explosionEdgeMultiplier, t);
        }

        private void OnValidate()
        {
            if (falloffEnd < falloffStart) falloffEnd = falloffStart;
            if (range < falloffEnd) range = falloffEnd;
            if (explosionFullDamageRadius > explosionRadius) explosionFullDamageRadius = explosionRadius;
        }
    }
}
