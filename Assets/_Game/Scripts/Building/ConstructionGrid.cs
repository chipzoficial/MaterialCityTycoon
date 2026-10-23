using UnityEngine;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Defines a centered, axis-aligned grid on the XZ plane and converts between
    /// world positions and grid cells.
    /// </summary>
    public sealed class ConstructionGrid : MonoBehaviour
    {
        [SerializeField, Min(1)] private int width = 16;
        [SerializeField, Min(1)] private int height = 20;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;

        public bool TryGetCell(Vector3 worldPosition, out Vector2Int cell)
        {
            Vector3 minimum = GetMinimumWorldPosition();
            int x = Mathf.FloorToInt((worldPosition.x - minimum.x) / cellSize);
            int z = Mathf.FloorToInt((worldPosition.z - minimum.z) / cellSize);
            cell = new Vector2Int(x, z);

            return IsInside(cell);
        }

        public Vector3 GetCellCenter(Vector2Int cell, float worldHeight)
        {
            Vector3 minimum = GetMinimumWorldPosition();
            return new Vector3(
                minimum.x + (cell.x + 0.5f) * cellSize,
                worldHeight,
                minimum.z + (cell.y + 0.5f) * cellSize);
        }

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
        }

        private Vector3 GetMinimumWorldPosition()
        {
            return transform.position - new Vector3(width * cellSize, 0f, height * cellSize) * 0.5f;
        }

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellSize = Mathf.Max(0.01f, cellSize);
        }
    }
}
