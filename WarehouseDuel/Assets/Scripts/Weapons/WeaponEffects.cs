using UnityEngine;
using WarehouseWar.Core;

namespace WarehouseWar.Weapons
{
    /// <summary>
    /// Pooled visual effects for the melee and explosive weapons (bullets keep using PlayerVisuals.OnImpact).
    /// Called by <see cref="WeaponController"/> on every peer that shows the event. Presentation only.
    /// </summary>
    public static class WeaponEffects
    {
        /// <summary>Knife connecting: blood puff on a player, sparks + dust on a surface (no bullet hole).</summary>
        public static void MeleeImpact(Vector3 point, Vector3 normal, bool hitPlayer)
        {
            EffectsLibrary fx = EffectsLibrary.Instance;
            if (fx == null) return;
            Quaternion facing = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up);
            EffectPool.Spawn(hitPlayer ? fx.impactPlayer : fx.impactEnvironment, point, facing);
        }

        /// <summary>
        /// Rocket: smoke trail from <paramref name="origin"/> to <paramref name="point"/>, fireball, and a scorch
        /// mark when it hit level geometry (<paramref name="normal"/> is zero for a mid-air detonation).
        /// </summary>
        public static void Explosion(Vector3 origin, Vector3 point, Vector3 normal, bool hitPlayer)
        {
            EffectsLibrary fx = EffectsLibrary.Instance;
            if (fx == null) return;
            bool onSurface = normal.sqrMagnitude > 0.001f;
            Quaternion facing = Quaternion.LookRotation(onSurface ? normal : Vector3.up);

            if (fx.rocketTrail != null && (origin - point).sqrMagnitude > 0.25f)
            {
                PooledEffect trail = EffectPool.Spawn(fx.rocketTrail, origin, Quaternion.identity);
                if (trail != null && trail.TryGetComponent(out RocketTrail rocketTrail)) rocketTrail.Show(origin, point);
            }

            // Fall back to the elimination burst until the effects have been regenerated.
            EffectPool.Spawn(fx.explosion != null ? fx.explosion : fx.elimination, point + (onSurface ? normal * 0.1f : Vector3.zero), facing);
            if (onSurface && !hitPlayer) EffectPool.Spawn(fx.scorchMark, point, facing);
        }
    }
}
