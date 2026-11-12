using UnityEngine;

namespace WarehouseDuel.Match
{
    /// <summary>
    /// Match tuning shared by host and client. Lives in Assets/ScriptableObjects/GameSettings/MatchRules.asset.
    /// Only the host's values matter for outcomes; clients use them for display.
    /// </summary>
    [CreateAssetMenu(menuName = "Warehouse Duel/Match Rules", fileName = "MatchRules")]
    public sealed class MatchRules : ScriptableObject
    {
        [Header("Timing")]
        [Min(10f)] public float matchDurationSeconds = 180f;
        [Min(0f)] public float countdownSeconds = 3f;

        [Header("Players")]
        [Min(1f)] public float maxHealth = 100f;
        [Min(0f)] public float respawnDelaySeconds = 3f;
        [Min(0f)] public float spawnProtectionSeconds = 2f;

        [Header("Movement")]
        [Min(0.5f)] public float walkSpeed = 4.6f;
        [Min(0.5f)] public float sprintSpeed = 7f;
        [Min(0.1f)] public float aimMoveMultiplier = 0.6f;
        [Min(1f)] public float jumpHeight = 1.1f;
        [Min(1f)] public float gravity = 22f;
    }
}
