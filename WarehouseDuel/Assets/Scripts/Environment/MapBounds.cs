using UnityEngine;

namespace WarehouseDuel.Environment
{
    /// <summary>
    /// Describes the playable volume of the map. The authoritative player code uses it as a safety net:
    /// anyone found outside it (e.g. after a physics glitch) is respawned instead of falling forever.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapBounds : MonoBehaviour
    {
        [SerializeField] private Bounds playableArea = new Bounds(Vector3.up * 4f, new Vector3(36f, 10f, 56f));
        [Tooltip("Anything below this world height is considered out of the map.")]
        [SerializeField] private float killHeight = -5f;
        [Tooltip("Outdoor map without a roof: the boundary walls/cliffs alone keep players in (used by the map validator).")]
        [SerializeField] private bool openSky;

        public static MapBounds Current { get; private set; }

        public Bounds PlayableArea => playableArea;
        public float KillHeight => killHeight;
        public bool OpenSky => openSky;

        public void Configure(Bounds area, float killY, bool isOpenSky = false)
        {
            playableArea = area;
            killHeight = killY;
            openSky = isOpenSky;
        }

        /// <summary>True when <paramref name="position"/> is inside the map with some tolerance.</summary>
        public bool IsInside(Vector3 position, float margin = 1f)
        {
            if (position.y < killHeight) return false;
            Bounds expanded = playableArea;
            expanded.Expand(margin * 2f);
            return expanded.Contains(position);
        }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(playableArea.center, playableArea.size);
        }
#endif
    }
}
