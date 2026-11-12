using UnityEngine;
using UnityEngine.UI;

namespace WarehouseDuel.UI.Widgets
{
    /// <summary>Keeps this Graphic's colour equal to another one (e.g. the two bars of the hit marker).</summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class ColorMirror : MonoBehaviour
    {
        public Graphic source;

        private Graphic target;

        private void Awake() => target = GetComponent<Graphic>();

        private void LateUpdate()
        {
            if (source != null && target.color != source.color) target.color = source.color;
        }
    }
}
