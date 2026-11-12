using System.Collections.Generic;
using UnityEngine;

namespace WarehouseDuel.Environment
{
    /// <summary>
    /// A player spawn location. Slot 0 belongs to the first player in the room (the host),
    /// slot 1 to the second. The forward (blue) axis is the direction the player faces after spawning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private int slot;

        private static readonly List<SpawnPoint> Active = new List<SpawnPoint>(2);

        public int Slot => slot;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        public static IReadOnlyList<SpawnPoint> All => Active;

        /// <summary>Returns the spawn for <paramref name="slotIndex"/>, or null if the scene has none.</summary>
        public static SpawnPoint ForSlot(int slotIndex)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                if (Active[i].slot == slotIndex) return Active[i];
            }
            return Active.Count > 0 ? Active[slotIndex % Active.Count] : null;
        }

        public void Configure(int slotIndex) => slot = slotIndex;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = slot == 0 ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.35f, 0.25f);
            Vector3 p = transform.position;
            Gizmos.DrawWireCube(p + Vector3.up * 0.9f, new Vector3(0.8f, 1.8f, 0.8f));
            Gizmos.DrawLine(p + Vector3.up * 1.6f, p + Vector3.up * 1.6f + transform.forward * 1.5f);
        }
#endif
    }
}
