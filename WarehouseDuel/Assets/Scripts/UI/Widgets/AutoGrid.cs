using UnityEngine;
using UnityEngine.UI;

namespace WarehouseWar.UI.Widgets
{
    /// <summary>
    /// Fits a <see cref="GridLayoutGroup"/> to whatever width the phone gives it (16:9 up to 20:9 and wider): as many
    /// columns as fit, with the spare width shared out (wider cells or wider gaps) so every row spans edge to edge
    /// instead of leaving an empty strip on the right.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class AutoGrid : MonoBehaviour
    {
        public Vector2 baseCell = new Vector2(160f, 160f);
        public float minSpacing = 24f;
        [Tooltip("Grow the cells to use the spare width (otherwise the gaps grow).")]
        public bool stretchCells;
        [Tooltip("When stretching, grow the cell height with the width.")]
        public bool keepAspect;

        private GridLayoutGroup grid;
        private float lastWidth = -1f;

        private void OnEnable()
        {
            lastWidth = -1f;
            Fit();
        }

        private void OnRectTransformDimensionsChange() => Fit();

        private void Update() => Fit(); // returns at once unless the width changed

        private void Fit()
        {
            if (grid == null) grid = GetComponent<GridLayoutGroup>();
            float width = ((RectTransform)transform).rect.width - grid.padding.horizontal;
            if (width <= 1f || Mathf.Abs(width - lastWidth) < 0.5f) return;
            lastWidth = width;

            int columns = Mathf.Max(1, Mathf.FloorToInt((width + minSpacing) / (baseCell.x + minSpacing)));
            float spare = Mathf.Max(0f, width - columns * baseCell.x - (columns - 1) * minSpacing);
            Vector2 cell = baseCell;
            float spacing = minSpacing;
            if (stretchCells || columns == 1)
            {
                cell.x += spare / columns;
                if (keepAspect) cell.y = baseCell.y * cell.x / baseCell.x;
            }
            else spacing += spare / (columns - 1);

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = cell;
            grid.spacing = new Vector2(spacing, grid.spacing.y);
        }
    }
}
