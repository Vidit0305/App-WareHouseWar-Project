using UnityEngine;

namespace WarehouseDuel.Weapons
{
    /// <summary>Marks a Fusion Hitbox as head/body/legs and gives its damage multiplier.</summary>
    public sealed class HitboxRegion : MonoBehaviour
    {
        public enum Region { Body, Head, Legs }

        [SerializeField] private Region region = Region.Body;
        [SerializeField, Min(0.1f)] private float damageMultiplier = 1f;

        public Region Kind => region;
        public float DamageMultiplier => damageMultiplier;
        public bool IsHead => region == Region.Head;

        public void Configure(Region kind, float multiplier)
        {
            region = kind;
            damageMultiplier = multiplier;
        }
    }
}
