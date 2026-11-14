using UnityEngine;
using UnityEngine.UI;

namespace WarehouseWar.UI.Widgets
{
    /// <summary>Pill-shaped on/off switch: slides the knob and tints the track to follow its Toggle.</summary>
    [RequireComponent(typeof(Toggle))]
    public sealed class ToggleSwitch : MonoBehaviour
    {
        [SerializeField] private Image track;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Color onColor = new Color(1f, 0.69f, 0.13f, 1f);
        [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.18f);

        private Toggle toggle;
        private float position;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            position = toggle.isOn ? 1f : 0f;
            Apply();
        }

        private void OnEnable()
        {
            if (toggle != null) position = toggle.isOn ? 1f : 0f;
            Apply();
        }

        private void Update()
        {
            float target = toggle.isOn ? 1f : 0f;
            if (Mathf.Approximately(position, target)) return;
            position = Mathf.MoveTowards(position, target, Time.unscaledDeltaTime * 8f);
            Apply();
        }

        private void Apply()
        {
            if (knob != null)
            {
                float x = Mathf.SmoothStep(0f, 1f, position);
                knob.anchorMin = knob.anchorMax = new Vector2(Mathf.Lerp(0.27f, 0.73f, x), 0.5f);
                knob.anchoredPosition = Vector2.zero;
            }
            if (track != null) track.color = Color.Lerp(offColor, onColor, position);
        }
    }
}
