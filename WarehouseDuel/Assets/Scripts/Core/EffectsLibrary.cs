using UnityEngine;

namespace WarehouseDuel.Core
{
    /// <summary>Pooled effect prefabs, stored at Assets/Resources/EffectsLibrary.asset.</summary>
    [CreateAssetMenu(menuName = "Warehouse Duel/Effects Library", fileName = "EffectsLibrary")]
    public sealed class EffectsLibrary : ScriptableObject
    {
        public const string ResourcePath = "EffectsLibrary";

        public GameObject muzzleFlash;
        public GameObject impactEnvironment;
        public GameObject impactPlayer;
        public GameObject bulletHole;
        public GameObject elimination;
        public GameObject spawn;

        private static EffectsLibrary cached;

        public static EffectsLibrary Instance
        {
            get
            {
                if (cached == null) cached = Resources.Load<EffectsLibrary>(ResourcePath);
                return cached;
            }
        }

        /// <summary>Pre-creates pooled instances so the first fight doesn't hitch.</summary>
        public void WarmPools()
        {
            EffectPool.Warm(muzzleFlash, 4);
            EffectPool.Warm(impactEnvironment, 10);
            EffectPool.Warm(impactPlayer, 6);
            EffectPool.Warm(bulletHole, 24);
            EffectPool.Warm(elimination, 2);
            EffectPool.Warm(spawn, 2);
        }
    }
}
