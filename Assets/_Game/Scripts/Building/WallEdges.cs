using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialCityTycoon.Building
{
    // X edges span (x,z) to (x+1,z); Z edges span (x,z) to (x,z+1).
    public readonly struct WallEdge : IEquatable<WallEdge>
    {
        public int X { get; }
        public int Z { get; }
        public bool AlongX { get; }
        public WallEdge(int x, int z, bool alongX) { X = x; Z = z; AlongX = alongX; }
        public bool Equals(WallEdge other) => X == other.X && Z == other.Z && AlongX == other.AlongX;
        public override bool Equals(object obj) => obj is WallEdge other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z, AlongX);
    }

    /// <summary>Wall occupancy independent of furniture; coordinates are grid vertex indices.</summary>
    public sealed class WallEdges
    {
        private readonly ConstructionGrid grid;
        private readonly HashSet<WallEdge> edges = new();
        public WallEdges(ConstructionGrid grid) => this.grid = grid != null ? grid : throw new ArgumentNullException(nameof(grid));
        public bool IsInside(WallEdge edge) => edge.X >= 0 && edge.Z >= 0 &&
            (edge.AlongX ? edge.X < grid.Width && edge.Z <= grid.Height : edge.X <= grid.Width && edge.Z < grid.Height);
        public bool Contains(WallEdge edge) => edges.Contains(edge);
        public bool TryAdd(WallEdge edge) => IsInside(edge) && edges.Add(edge);
        public bool Remove(WallEdge edge) => edges.Remove(edge);

        public bool TryFindNearest(Vector3 point, out WallEdge edge)
        {
            Vector3 min = grid.GetCellCenter(Vector2Int.zero, 0f) - new Vector3(grid.CellSize * 0.5f, 0f, grid.CellSize * 0.5f);
            float x = (point.x - min.x) / grid.CellSize, z = (point.z - min.z) / grid.CellSize;
            edge = default;
            if (float.IsNaN(x) || float.IsNaN(z) || x < 0 || z < 0 || x > grid.Width || z > grid.Height) return false;
            int cellX = Mathf.Min(Mathf.FloorToInt(x), grid.Width - 1);
            int cellZ = Mathf.Min(Mathf.FloorToInt(z), grid.Height - 1);
            float left = x - cellX, right = cellX + 1 - x, bottom = z - cellZ, top = cellZ + 1 - z;
            float nearestX = Mathf.Min(left, right), nearestZ = Mathf.Min(bottom, top);
            edge = nearestX < nearestZ
                ? new WallEdge(left <= right ? cellX : cellX + 1, cellZ, false)
                : new WallEdge(cellX, bottom <= top ? cellZ : cellZ + 1, true);
            return true;
        }

        public Vector3 Center(WallEdge edge, float baseHeight, float wallHeight)
        {
            Vector3 min = grid.GetCellCenter(Vector2Int.zero, 0f) - new Vector3(grid.CellSize * 0.5f, 0f, grid.CellSize * 0.5f);
            return new Vector3(min.x + (edge.X + (edge.AlongX ? 0.5f : 0f)) * grid.CellSize,
                baseHeight + wallHeight * 0.5f, min.z + (edge.Z + (edge.AlongX ? 0f : 0.5f)) * grid.CellSize);
        }
    }
}
