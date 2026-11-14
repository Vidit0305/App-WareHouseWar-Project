using UnityEngine;
using UnityEngine.EventSystems;

namespace WarehouseWar.UI.Widgets
{
    /// <summary>Drag horizontally on this (transparent) UI area to spin a 3D object, e.g. the lobby character.</summary>
    public sealed class DragRotate : MonoBehaviour, IDragHandler
    {
        [SerializeField] private Transform target;
        [SerializeField] private float degreesPerPixel = 0.35f;

        public void OnDrag(PointerEventData eventData)
        {
            if (target != null) target.Rotate(0f, -eventData.delta.x * degreesPerPixel, 0f, Space.World);
        }
    }
}
