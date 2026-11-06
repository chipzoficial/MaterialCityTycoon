using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialCityTycoon.Building
{
    /// <summary>Logical floor coverings keyed by their construction-grid cell.</summary>
    public sealed class FloorCells
    {
        public const string ProvisionalFloorTypeId = "provisional_floor";

        private readonly Dictionary<Vector2Int, string> floorTypeByCell = new();

        public int Count => floorTypeByCell.Count;
        public bool Contains(Vector2Int cell) => floorTypeByCell.ContainsKey(cell);

        public bool TryGetFloorType(Vector2Int cell, out string floorTypeId)
        {
            return floorTypeByCell.TryGetValue(cell, out floorTypeId);
        }

        public bool CanAddRange(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) return false;
            HashSet<Vector2Int> unique = new();
            foreach (Vector2Int cell in cells)
            {
                if (!unique.Add(cell) || floorTypeByCell.ContainsKey(cell)) return false;
            }

            return true;
        }

        public bool TryAddRange(IReadOnlyList<Vector2Int> cells, string floorTypeId = ProvisionalFloorTypeId)
        {
            if (string.IsNullOrWhiteSpace(floorTypeId) || !CanAddRange(cells)) return false;
            foreach (Vector2Int cell in cells) floorTypeByCell.Add(cell, floorTypeId);
            return true;
        }

        public bool CanRemoveRange(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) return false;
            HashSet<Vector2Int> unique = new();
            foreach (Vector2Int cell in cells)
            {
                if (!unique.Add(cell) || !floorTypeByCell.ContainsKey(cell)) return false;
            }

            return true;
        }

        public bool TryRemoveRange(IReadOnlyList<Vector2Int> cells)
        {
            if (!CanRemoveRange(cells)) return false;
            foreach (Vector2Int cell in cells) floorTypeByCell.Remove(cell);
            return true;
        }

        public static void GetRectangle(Vector2Int first, Vector2Int second, List<Vector2Int> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            int minimumX = Mathf.Min(first.x, second.x);
            int maximumX = Mathf.Max(first.x, second.x);
            int minimumZ = Mathf.Min(first.y, second.y);
            int maximumZ = Mathf.Max(first.y, second.y);
            for (int z = minimumZ; z <= maximumZ; z++)
            {
                for (int x = minimumX; x <= maximumX; x++)
                {
                    result.Add(new Vector2Int(x, z));
                }
            }
        }
    }
}
