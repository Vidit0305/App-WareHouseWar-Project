using UnityEngine;
using UnityEngine.EventSystems;
using WarehouseDuel.Audio;

namespace WarehouseDuel.UI.Widgets
{
    /// <summary>Plays the shared click sound when a UI element is pressed.</summary>
    public sealed class UIButtonSound : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            var selectable = GetComponent<UnityEngine.UI.Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;
            AudioManager manager = AudioManager.Instance; // created on first use; loads the library
            manager.PlayUI(AudioManager.Library != null ? AudioManager.Library.buttonClick : null, 0.7f);
        }
    }
}
