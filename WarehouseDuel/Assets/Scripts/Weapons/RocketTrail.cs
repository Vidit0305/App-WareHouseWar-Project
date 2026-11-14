using UnityEngine;

namespace WarehouseWar.Weapons
{
    /// <summary>
    /// Pooled smoke trail for the (instant) rocket: emits smoke puffs and a short hot streak along the
    /// muzzle → impact line. Lives on the FX_RocketTrail prefab next to its PooledEffect. No allocations.
    /// </summary>
    public sealed class RocketTrail : MonoBehaviour
    {
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private ParticleSystem streak;
        [Tooltip("Metres between smoke puffs.")]
        [SerializeField, Min(0.1f)] private float spacing = 0.55f;
        [SerializeField, Min(1)] private int maxPuffs = 90;

        public void Configure(ParticleSystem smokeSystem, ParticleSystem streakSystem)
        {
            smoke = smokeSystem;
            streak = streakSystem;
        }

        /// <summary>Draws the trail from <paramref name="from"/> to <paramref name="to"/> (world space).</summary>
        public void Show(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.1f) return;

            int count = Mathf.Clamp(Mathf.CeilToInt(length / spacing), 1, maxPuffs);
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float u = (i + Random.value) / count;
                if (smoke != null)
                {
                    emit.position = from + delta * u + Random.insideUnitSphere * 0.05f;
                    emit.velocity = Random.insideUnitSphere * 0.3f + Vector3.up * 0.12f;
                    emit.startLifetime = Random.Range(0.9f, 1.7f);
                    emit.startSize = Mathf.Lerp(0.18f, 0.32f, u) * Random.Range(0.8f, 1.2f);
                    smoke.Emit(emit, 1);
                }
                if (streak != null && (i & 1) == 0)
                {
                    emit.position = from + delta * u;
                    emit.velocity = Vector3.zero;
                    emit.startLifetime = Random.Range(0.08f, 0.18f);
                    emit.startSize = Random.Range(0.12f, 0.2f);
                    streak.Emit(emit, 1);
                }
            }
        }
    }
}
