using UnityEngine;

namespace WarehouseDuel.Weapons
{
    /// <summary>
    /// Recolours a pooled particle effect (the muzzle flash) per weapon without allocating:
    /// each child system's start colour is blended from its authored colour towards the tint.
    /// </summary>
    public sealed class EffectTint : MonoBehaviour
    {
        [Tooltip("0 = authored colours, 1 = fully the tint colour (alpha always stays authored).")]
        [SerializeField, Range(0f, 1f)] private float strength = 0.8f;

        private ParticleSystem[] systems;
        private Color[] authored;
        private bool tinted;

        private void Awake() => Cache();

        // Pooled instances are shared by everyone: each spawn starts from the authored colours,
        // so a caller that does not tint (e.g. the opponent's flash) never inherits the previous tint.
        private void OnEnable()
        {
            if (!tinted || systems == null) return;
            tinted = false;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.startColor = authored[i];
            }
        }

        private void Cache()
        {
            if (systems != null) return;
            systems = GetComponentsInChildren<ParticleSystem>(true);
            authored = new Color[systems.Length];
            for (int i = 0; i < systems.Length; i++) authored[i] = systems[i].main.startColor.color;
        }

        public void SetTint(Color tint)
        {
            Cache();
            tinted = true;
            for (int i = 0; i < systems.Length; i++)
            {
                Color baseColor = authored[i];
                Color c = Color.Lerp(baseColor, tint, strength);
                c.a = baseColor.a;
                ParticleSystem.MainModule main = systems[i].main;
                main.startColor = c;
            }
        }
    }
}
