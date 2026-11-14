using UnityEngine;

namespace WarehouseWar.UI
{
    /// <summary>
    /// Animates the 3D warehouse diorama behind the menus: a slowly drifting camera and a rotating
    /// weapon on a pedestal. Purely cosmetic.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [SerializeField] private Transform showcase;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float spinDegreesPerSecond = 25f;
        [SerializeField] private float driftAmplitude = 0.35f;
        [SerializeField] private float driftSpeed = 0.15f;

        private Vector3 cameraStart;

        private void Start()
        {
            if (cameraTransform != null) cameraStart = cameraTransform.position;
        }

        private void Update()
        {
            if (showcase != null) showcase.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            if (cameraTransform != null)
            {
                float t = Time.time * driftSpeed;
                cameraTransform.position = cameraStart + new Vector3(Mathf.Sin(t) * driftAmplitude, Mathf.Sin(t * 0.7f) * driftAmplitude * 0.3f, 0f);
            }
        }
    }
}
