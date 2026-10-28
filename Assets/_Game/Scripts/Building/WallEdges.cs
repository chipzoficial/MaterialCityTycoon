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

        /// <summary>Gets the grid edge separating two cardinally adjacent cells.</summary>
        public static bool TryGetSeparatingEdge(Vector2Int firstCell, Vector2Int secondCell, out WallEdge edge)
        {
            int deltaX = secondCell.x - firstCell.x;
            int deltaZ = secondCell.y - firstCell.y;
            if (Mathf.Abs(deltaX) + Mathf.Abs(deltaZ) != 1)
            {
                edge = default;
                return false;
            }

            edge = deltaX != 0
                ? new WallEdge(Mathf.Max(firstCell.x, secondCell.x), firstCell.y, false)
                : new WallEdge(firstCell.x, Mathf.Max(firstCell.y, secondCell.y), true);
            return true;
        }

        public bool IsPassageBlocked(Vector2Int firstCell, Vector2Int secondCell)
        {
            return TryGetSeparatingEdge(firstCell, secondCell, out WallEdge edge) && Contains(edge);
        }

        /// <summary>
        /// Converts a world point on the grid to its nearest grid vertex. Vertices, unlike
        /// cells, include the outer boundary of the grid.
        /// </summary>
        public bool TryFindNearestVertex(Vector3 point, out Vector2Int vertex)
        {
            Vector3 min = grid.GetCellCenter(Vector2Int.zero, 0f) - new Vector3(grid.CellSize * 0.5f, 0f, grid.CellSize * 0.5f);
            float x = (point.x - min.x) / grid.CellSize;
            float z = (point.z - min.z) / grid.CellSize;
            vertex = default;
            if (float.IsNaN(x) || float.IsNaN(z) || x < 0f || z < 0f || x > grid.Width || z > grid.Height)
            {
                return false;
            }

            vertex = new Vector2Int(Mathf.FloorToInt(x + 0.5f), Mathf.FloorToInt(z + 0.5f));
            return vertex.x >= 0 && vertex.x <= grid.Width && vertex.y >= 0 && vertex.y <= grid.Height;
        }

        /// <summary>
        /// Writes the straight one-metre segments from start to end. Ties deliberately favour
        /// X so a near-diagonal drag does not alternate axes from frame to frame.
        /// </summary>
        public void GetStraightLine(Vector2Int start, Vector2Int end, List<WallEdge> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();

            int deltaX = end.x - start.x;
            int deltaZ = end.y - start.y;
            if (deltaX == 0 && deltaZ == 0)
            {
                return;
            }

            if (Mathf.Abs(deltaX) >= Mathf.Abs(deltaZ))
            {
                int step = deltaX >= 0 ? 1 : -1;
                for (int index = 0; index < Mathf.Abs(deltaX); index++)
                {
                    int edgeX = step > 0 ? start.x + index : start.x - index - 1;
                    result.Add(new WallEdge(edgeX, start.y, true));
                }
            }
            else
            {
                int step = deltaZ >= 0 ? 1 : -1;
                for (int index = 0; index < Mathf.Abs(deltaZ); index++)
                {
                    int edgeZ = step > 0 ? start.y + index : start.y - index - 1;
                    result.Add(new WallEdge(start.x, edgeZ, false));
                }
            }
        }

        /// <summary>
        /// Builds a straight drag while preserving the edge selected at its start. If the
        /// player changes to the other axis, it falls back to the snapped start vertex so the
        /// drag remains a single, predictable straight line.
        /// </summary>
        public void GetStraightLineFromEdge(WallEdge startEdge, Vector2Int snappedStartVertex, Vector2Int end, List<WallEdge> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            int deltaX = end.x - snappedStartVertex.x;
            int deltaZ = end.y - snappedStartVertex.y;
            if (deltaX == 0 && deltaZ == 0)
            {
                result.Clear();
                return;
            }

            bool alongX = Mathf.Abs(deltaX) >= Mathf.Abs(deltaZ);
            if (startEdge.AlongX != alongX)
            {
                GetStraightLine(snappedStartVertex, end, result);
                return;
            }

            bool positive = alongX ? deltaX > 0 : deltaZ > 0;
            Vector2Int startVertex = alongX
                ? new Vector2Int(startEdge.X + (positive ? 0 : 1), startEdge.Z)
                : new Vector2Int(startEdge.X, startEdge.Z + (positive ? 0 : 1));
            GetStraightLine(startVertex, end, result);
        }

        /// <summary>Checks a complete line before it is committed, preserving atomic construction.</summary>
        public bool CanAddRange(IReadOnlyList<WallEdge> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return false;
            }

            HashSet<WallEdge> unique = new();
            for (int index = 0; index < candidates.Count; index++)
            {
                WallEdge edge = candidates[index];
                if (!IsInside(edge) || Contains(edge) || !unique.Add(edge))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Adds an entire validated line, or leaves occupancy unchanged.</summary>
        public bool TryAddRange(IReadOnlyList<WallEdge> candidates)
        {
            if (!CanAddRange(candidates))
            {
                return false;
            }

            for (int index = 0; index < candidates.Count; index++)
            {
                edges.Add(candidates[index]);
            }

            return true;
        }

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
