using System.Collections.Generic;
using UnityEngine;

namespace WarehouseWar.Environment
{
    /// <summary>
    /// A player spawn location. Every map has <see cref="MaxSlots"/> of them, one per room slot: slot 0 belongs to
    /// the first player in the room (the host), slot 1 to the second, and so on. Each player spawns and respawns at
    /// their own slot's point. The forward (blue) axis is the direction the player faces after spawning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpawnPoint : MonoBehaviour
    {
        /// <summary>Spawn points per map (= maximum players in a room).</summary>
        public const int MaxSlots = 8;

        [SerializeField, Range(0, MaxSlots - 1)] private int slot;

        private static readonly List<SpawnPoint> Active = new List<SpawnPoint>(MaxSlots);

        public int Slot => slot;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        public static IReadOnlyList<SpawnPoint> All => Active;

        /// <summary>
        /// Returns the spawn for <paramref name="slotIndex"/>. If the scene has no spawn for that slot, falls back to
        /// another spawn (slot modulo count); returns null only if the scene has no spawns at all.
        /// </summary>
        public static SpawnPoint ForSlot(int slotIndex)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                if (Active[i].slot == slotIndex) return Active[i];
            }
            if (Active.Count == 0) return null;
            int count = Active.Count;
            return Active[((slotIndex % count) + count) % count];
        }

        public void Configure(int slotIndex) => slot = slotIndex;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

#if UNITY_EDITOR
        private static readonly Color[] SlotColours =
        {
            new Color(0.2f, 0.6f, 1f), new Color(1f, 0.35f, 0.25f), new Color(0.3f, 0.9f, 0.4f), new Color(1f, 0.85f, 0.2f),
            new Color(0.75f, 0.4f, 1f), new Color(0.2f, 0.9f, 0.9f), new Color(1f, 0.55f, 0.85f), new Color(0.95f, 0.95f, 0.95f),
        };

        private void OnDrawGizmos()
        {
            Gizmos.color = SlotColours[Mathf.Clamp(slot, 0, SlotColours.Length - 1)];
            Vector3 p = transform.position;
            Gizmos.DrawWireCube(p + Vector3.up * 0.9f, new Vector3(0.8f, 1.8f, 0.8f));
            Gizmos.DrawLine(p + Vector3.up * 1.6f, p + Vector3.up * 1.6f + transform.forward * 1.5f);
        }
#endif
    }
}
