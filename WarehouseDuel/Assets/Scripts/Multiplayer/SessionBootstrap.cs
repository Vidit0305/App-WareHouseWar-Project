using UnityEngine;

namespace WarehouseDuel.Multiplayer
{
    /// <summary>
    /// Placed in every scene that needs networking. Makes sure exactly one SessionService exists,
    /// whichever scene the game (or the Editor's Play button) starts in.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SessionBootstrap : MonoBehaviour
    {
        [SerializeField] private SessionService sessionServicePrefab;

        private void Awake()
        {
            if (sessionServicePrefab != null) SessionService.EnsureInstance(sessionServicePrefab);
            else Debug.LogError("[SessionBootstrap] SessionService prefab is not assigned.");
        }
    }
}
