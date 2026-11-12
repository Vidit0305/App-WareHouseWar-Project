using Fusion;
using UnityEngine;

namespace WarehouseDuel.Multiplayer
{
    public enum InputButton
    {
        Fire = 0,
        Aim = 1,
        Reload = 2,
        Jump = 3,
        Sprint = 4,
        NextWeapon = 5,
    }

    /// <summary>
    /// Per-tick input sent from each player to the host. Look angles are absolute (not deltas) so a lost
    /// or repeated input never makes the view drift, and resimulation stays deterministic.
    /// </summary>
    public struct NetworkInputData : INetworkInput
    {
        public Vector2 Move;
        public float Yaw;
        public float Pitch;
        public NetworkButtons Buttons;
        /// <summary>Requested weapon slot + 1 (0 = no request).</summary>
        public byte WeaponRequest;
    }
}
