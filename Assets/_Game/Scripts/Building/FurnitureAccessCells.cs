using System.Collections.Generic;
using UnityEngine;

namespace MaterialCityTycoon.Building
{
    /// <summary>Rotates access offsets around the physical footprint, anchored at its minimum corner.</summary>
    public static class FurnitureAccessCells
    {
        public static IEnumerable<Vector2Int> GetCells(Vector2Int anchor, int width, int depth,
            int quarterTurns, IReadOnlyList<Vector2Int> offsets)
        {
            int rotation = ((quarterTurns % 4) + 4) % 4;
            foreach (Vector2Int offset in offsets)
            {
                Vector2Int rotated = rotation switch
                {
                    1 => new Vector2Int(offset.y, width - 1 - offset.x),
                    2 => new Vector2Int(width - 1 - offset.x, depth - 1 - offset.y),
                    3 => new Vector2Int(depth - 1 - offset.y, offset.x),
                    _ => offset
                };
                yield return anchor + rotated;
            }
        }
    }
}
