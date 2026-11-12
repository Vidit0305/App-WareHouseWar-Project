using UnityEngine;

namespace WarehouseDuel.Core
{
    /// <summary>Effect instance managed by <see cref="EffectPool"/>; plays all child particle systems.</summary>
    public sealed class PooledEffect : MonoBehaviour
    {
        [Min(0.05f)] public float lifetime = 1f;

        private EffectPool owner;
        private GameObject sourcePrefab;
        private ParticleSystem[] systems;
        private float returnTime;

        internal void Bind(EffectPool pool, GameObject prefab)
        {
            owner = pool;
            sourcePrefab = prefab;
            systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        internal void Play()
        {
            gameObject.SetActive(true);
            returnTime = Time.time + lifetime;
            foreach (ParticleSystem system in systems)
            {
                system.Clear(true);
                system.Play(true);
            }
        }

        internal void ReturnNow()
        {
            if (owner != null) owner.Return(this, sourcePrefab);
        }

        private void Update()
        {
            if (Time.time >= returnTime) ReturnNow();
        }
    }
}
