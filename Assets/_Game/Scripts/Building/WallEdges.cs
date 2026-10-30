using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialCityTycoon.Building
{
    public enum WallEdgeOrientation
    {
        AlongX,
        AlongZ,
        DiagonalAscending,
        DiagonalDescending
    }

    // X edges span (x,z) to (x+1,z); Z edges span (x,z) to (x,z+1).
    public readonly struct WallEdge : IEquatable<WallEdge>
    {
        public int X { get; }
        public int Z { get; }
        public WallEdgeOrientation Orientation { get; }
        public bool AlongX => Orientation == WallEdgeOrientation.AlongX;
        public bool AlongZ => Orientation == WallEdgeOrientation.AlongZ;
        public bool IsDiagonal => Orientation == WallEdgeOrientation.DiagonalAscending
            || Orientation == WallEdgeOrientation.DiagonalDescending;
        public bool IsAscendingDiagonal => Orientation == WallEdgeOrientation.DiagonalAscending;
        public WallEdge(int x, int z, bool alongX)
            : this(x, z, alongX ? WallEdgeOrientation.AlongX : WallEdgeOrientation.AlongZ) { }
        public WallEdge(int x, int z, WallEdgeOrientation orientation)
        {
            X = x;
            Z = z;
            Orientation = orientation;
        }
        public bool Equals(WallEdge other) => X == other.X && Z == other.Z && Orientation == other.Orientation;
        public override bool Equals(object obj) => obj is WallEdge other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z, Orientation);
    }

    /// <summary>Wall occupancy independent of furniture; coordinates are grid vertex indices.</summary>
    public sealed class WallEdges
    {
        private readonly ConstructionGrid grid;
        private readonly HashSet<WallEdge> edges = new();
        private readonly HashSet<WallEdge> doors = new();
        public WallEdges(ConstructionGrid grid) => this.grid = grid != null ? grid : throw new ArgumentNullException(nameof(grid));
        public bool IsInside(WallEdge edge)
        {
            if (edge.X < 0 || edge.Z < 0) return false;
            if (edge.IsDiagonal) return edge.X < grid.Width && edge.Z < grid.Height;
            return edge.AlongX
                ? edge.X < grid.Width && edge.Z <= grid.Height
                : edge.X <= grid.Width && edge.Z < grid.Height;
        }
        public bool Contains(WallEdge edge) => edges.Contains(edge);
        public bool ContainsDiagonalAtCell(Vector2Int cell) =>
            edges.Contains(new WallEdge(cell.x, cell.y, WallEdgeOrientation.DiagonalAscending))
            || edges.Contains(new WallEdge(cell.x, cell.y, WallEdgeOrientation.DiagonalDescending));
        public bool TryAdd(WallEdge edge) => CanAddRange(new[] { edge }) && edges.Add(edge);
        public bool Remove(WallEdge edge)
        {
            doors.Remove(edge);
            return edges.Remove(edge);
        }
        public bool IsDoor(WallEdge edge) => doors.Contains(edge);
        public bool IsExternalEdge(WallEdge edge) => IsInside(edge) && TryGetExternalDoorInteriorCell(edge, out _);
        public bool IsExternalDoor(WallEdge edge) => IsDoor(edge) && IsExternalEdge(edge);
        public bool CanInstallDoor(WallEdge edge) => !edge.IsDiagonal && Contains(edge) && !IsDoor(edge)
            && (TryGetAdjacentCells(edge, out _, out _) || TryGetExternalDoorInteriorCell(edge, out _));
        public bool TryInstallDoor(WallEdge edge) => CanInstallDoor(edge) && doors.Add(edge);
        public bool RemoveDoor(WallEdge edge) => doors.Remove(edge);

        /// <summary>Returns the two walkable cells separated by an interior edge.</summary>
        public bool TryGetAdjacentCells(WallEdge edge, out Vector2Int firstCell, out Vector2Int secondCell)
        {
            firstCell = default;
            secondCell = default;
            if (!IsInside(edge) || edge.IsDiagonal) return false;

            firstCell = edge.AlongX
                ? new Vector2Int(edge.X, edge.Z - 1)
                : new Vector2Int(edge.X - 1, edge.Z);
            secondCell = edge.AlongX
                ? new Vector2Int(edge.X, edge.Z)
                : new Vector2Int(edge.X, edge.Z);
            return IsInsideCell(firstCell) && IsInsideCell(secondCell);
        }

        /// <summary>
        /// Returns the only grid cell adjacent to an external perimeter edge. The other side
        /// deliberately remains outside the construction grid and is not represented as a cell.
        /// </summary>
        public bool TryGetExternalDoorInteriorCell(WallEdge edge, out Vector2Int interiorCell)
        {
            interiorCell = default;
            if (!IsInside(edge) || edge.IsDiagonal) return false;

            Vector2Int firstCandidate = edge.AlongX
                ? new Vector2Int(edge.X, edge.Z - 1)
                : new Vector2Int(edge.X - 1, edge.Z);
            Vector2Int secondCandidate = edge.AlongX
                ? new Vector2Int(edge.X, edge.Z)
                : new Vector2Int(edge.X, edge.Z);
            bool firstIsInside = IsInsideCell(firstCandidate);
            bool secondIsInside = IsInsideCell(secondCandidate);
            if (firstIsInside == secondIsInside) return false;

            interiorCell = firstIsInside ? firstCandidate : secondCandidate;
            return true;
        }

        /// <summary>Reports whether a cell is one of the two passage cells for an installed door.</summary>
        public bool IsDoorwayCell(Vector2Int cell)
        {
            return IsDoor(new WallEdge(cell.x, cell.y, true))
                || IsDoor(new WallEdge(cell.x, cell.y + 1, true))
                || IsDoor(new WallEdge(cell.x, cell.y, false))
                || IsDoor(new WallEdge(cell.x + 1, cell.y, false));
        }

        private bool IsInsideCell(Vector2Int cell) => cell.x >= 0 && cell.x < grid.Width && cell.y >= 0 && cell.y < grid.Height;

        /// <summary>
        /// Counts occupied segments that meet at a grid vertex. Optional candidates are
        /// treated as proposed segments so preview and final geometry share one topology.
        /// </summary>
        public int GetConnectionCountAtVertex(Vector2Int vertex, IReadOnlyList<WallEdge> candidates = null)
        {
            int count = 0;
            CountIfPresent(new WallEdge(vertex.x - 1, vertex.y, true));
            CountIfPresent(new WallEdge(vertex.x, vertex.y, true));
            CountIfPresent(new WallEdge(vertex.x, vertex.y - 1, false));
            CountIfPresent(new WallEdge(vertex.x, vertex.y, false));
            return count;

            void CountIfPresent(WallEdge edge)
            {
                if (IsInside(edge) && (Contains(edge) || IncludesCandidate(candidates, edge)))
                {
                    count++;
                }
            }
        }

        private static bool IncludesCandidate(IReadOnlyList<WallEdge> candidates, WallEdge edge)
        {
            if (candidates == null) return false;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].Equals(edge)) return true;
            }

            return false;
        }

        /// <summary>Gets the candidate endpoints where at least two wall segments meet.</summary>
        public void GetAffectedJunctionVertices(IReadOnlyList<WallEdge> candidates, List<Vector2Int> result)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (result == null) throw new ArgumentNullException(nameof(result));

            result.Clear();
            HashSet<Vector2Int> endpoints = new();
            foreach (WallEdge edge in candidates)
            {
                if (edge.IsDiagonal) continue;
                endpoints.Add(new Vector2Int(edge.X, edge.Z));
                endpoints.Add(edge.AlongX ? new Vector2Int(edge.X + 1, edge.Z) : new Vector2Int(edge.X, edge.Z + 1));
            }

            foreach (Vector2Int endpoint in endpoints)
            {
                if (GetConnectionCountAtVertex(endpoint, candidates) >= 2)
                {
                    result.Add(endpoint);
                }
            }
        }

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
            return TryGetSeparatingEdge(firstCell, secondCell, out WallEdge edge) && Contains(edge) && !IsDoor(edge);
        }

        /// <summary>Reports whether an external perimeter wall currently provides an open entrance.</summary>
        public bool IsExternalPassageOpen(WallEdge edge) => IsExternalDoor(edge);

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

        /// <summary>Returns the nearest allowed wall orientation for a world-space drag vector.</summary>
        public static WallEdgeOrientation GetSnappedOrientation(Vector2 direction)
        {
            float x = Mathf.Abs(direction.x);
            float z = Mathf.Abs(direction.y);
            if (x <= Mathf.Epsilon && z <= Mathf.Epsilon) return WallEdgeOrientation.AlongX;

            // The boundary halfway between a cardinal axis and a 45 degree diagonal is 22.5 degrees.
            const float diagonalThreshold = 0.41421356f; // tan(22.5 degrees)
            if (Mathf.Min(x, z) / Mathf.Max(x, z) >= diagonalThreshold)
            {
                return direction.x * direction.y >= 0f
                    ? WallEdgeOrientation.DiagonalAscending
                    : WallEdgeOrientation.DiagonalDescending;
            }

            return x >= z ? WallEdgeOrientation.AlongX : WallEdgeOrientation.AlongZ;
        }

        /// <summary>Writes a cardinal line constrained to the selected axis.</summary>
        public void GetOrthogonalLine(Vector2Int startVertex, Vector2Int endVertex, bool alongX, List<WallEdge> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            int delta = alongX ? endVertex.x - startVertex.x : endVertex.y - startVertex.y;
            if (delta == 0) return;

            int step = delta > 0 ? 1 : -1;
            for (int index = 0; index < Mathf.Abs(delta); index++)
            {
                int coordinate = step > 0 ? (alongX ? startVertex.x : startVertex.y) + index
                    : (alongX ? startVertex.x : startVertex.y) - index - 1;
                result.Add(alongX
                    ? new WallEdge(coordinate, startVertex.y, true)
                    : new WallEdge(startVertex.x, coordinate, false));
            }
        }

        /// <summary>Writes a contiguous 45 degree line of same-oriented diagonal segments.</summary>
        public bool GetDiagonalLine(WallEdge startEdge, Vector2Int endCell, List<WallEdge> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            if (!startEdge.IsDiagonal) return false;

            int deltaX = endCell.x - startEdge.X;
            int deltaZ = endCell.y - startEdge.Z;
            bool isAligned = startEdge.IsAscendingDiagonal ? deltaX == deltaZ : deltaX == -deltaZ;
            if (!isAligned) return false;

            int count = Mathf.Abs(deltaX);
            int stepX = deltaX >= 0 ? 1 : -1;
            int stepZ = deltaZ >= 0 ? 1 : -1;
            for (int index = 0; index <= count; index++)
            {
                result.Add(new WallEdge(startEdge.X + index * stepX, startEdge.Z + index * stepZ,
                    startEdge.Orientation));
            }

            return true;
        }

        /// <summary>
        /// Projects an arbitrary drag end cell onto the diagonal selected by <paramref name="startEdge" />.
        /// This makes angular snapping stable even when the cursor is not exactly on a 45 degree cell line.
        /// </summary>
        public bool GetDiagonalLineFromDirection(WallEdge startEdge, Vector2Int endCell, List<WallEdge> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            if (!startEdge.IsDiagonal) return false;

            int deltaX = endCell.x - startEdge.X;
            int deltaZ = endCell.y - startEdge.Z;
            int count = Mathf.Max(Mathf.Abs(deltaX), Mathf.Abs(deltaZ));
            int stepX;
            if (deltaX != 0) stepX = deltaX > 0 ? 1 : -1;
            else if (deltaZ != 0) stepX = startEdge.IsAscendingDiagonal
                ? (deltaZ > 0 ? 1 : -1)
                : (deltaZ > 0 ? -1 : 1);
            else stepX = 1;
            int stepZ = startEdge.IsAscendingDiagonal ? stepX : -stepX;

            for (int index = 0; index <= count; index++)
            {
                result.Add(new WallEdge(startEdge.X + index * stepX, startEdge.Z + index * stepZ,
                    startEdge.Orientation));
            }

            return true;
        }

        /// <summary>Checks a complete line before it is committed, preserving atomic construction.</summary>
        public bool CanAddRange(IReadOnlyList<WallEdge> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return false;
            }

            HashSet<WallEdge> unique = new();
            HashSet<Vector2Int> diagonalCells = new();
            for (int index = 0; index < candidates.Count; index++)
            {
                WallEdge edge = candidates[index];
                if (!IsInside(edge) || Contains(edge) || !unique.Add(edge))
                {
                    return false;
                }

                if (edge.IsDiagonal)
                {
                    Vector2Int cell = new(edge.X, edge.Z);
                    if (ContainsDiagonalAtCell(cell) || !diagonalCells.Add(cell)) return false;
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

        /// <summary>Snaps a point inside a grid cell to the selected diagonal orientation.</summary>
        public bool TryFindNearestDiagonal(Vector3 point, bool ascending, out WallEdge edge)
        {
            edge = default;
            if (!TryGetCell(point, out Vector2Int cell)) return false;
            edge = new WallEdge(cell.x, cell.y, ascending
                ? WallEdgeOrientation.DiagonalAscending
                : WallEdgeOrientation.DiagonalDescending);
            return true;
        }

        /// <summary>Finds an existing segment by its world-space distance, including diagonals.</summary>
        public bool TryFindNearestExisting(Vector3 point, out WallEdge edge)
        {
            edge = default;
            float nearestDistance = float.PositiveInfinity;
            foreach (WallEdge candidate in edges)
            {
                GetEndpoints(candidate, out Vector3 start, out Vector3 end);
                float distance = DistanceToSegmentSquared(new Vector2(point.x, point.z),
                    new Vector2(start.x, start.z), new Vector2(end.x, end.z));
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    edge = candidate;
                }
            }

            return !float.IsPositiveInfinity(nearestDistance);
        }

        public Vector3 Center(WallEdge edge, float baseHeight, float wallHeight)
        {
            if (edge.IsDiagonal)
            {
                Vector3 cellCenter = grid.GetCellCenter(new Vector2Int(edge.X, edge.Z), baseHeight);
                return cellCenter + Vector3.up * (wallHeight * 0.5f);
            }

            Vector3 vertex = GetVertexPosition(new Vector2Int(edge.X, edge.Z), baseHeight);
            return new Vector3(vertex.x + (edge.AlongX ? 0.5f : 0f) * grid.CellSize,
                baseHeight + wallHeight * 0.5f, vertex.z + (edge.AlongX ? 0f : 0.5f) * grid.CellSize);
        }

        public Vector3 GetVertexPosition(Vector2Int vertex, float baseHeight)
        {
            Vector3 min = grid.GetCellCenter(Vector2Int.zero, 0f) - new Vector3(grid.CellSize * 0.5f, 0f, grid.CellSize * 0.5f);
            return new Vector3(min.x + vertex.x * grid.CellSize, baseHeight, min.z + vertex.y * grid.CellSize);
        }

        private bool TryGetCell(Vector3 point, out Vector2Int cell)
        {
            Vector3 min = grid.GetCellCenter(Vector2Int.zero, 0f)
                - new Vector3(grid.CellSize * 0.5f, 0f, grid.CellSize * 0.5f);
            float x = (point.x - min.x) / grid.CellSize;
            float z = (point.z - min.z) / grid.CellSize;
            cell = new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(z));
            return !float.IsNaN(x) && !float.IsNaN(z) && IsInsideCell(cell);
        }

        private void GetEndpoints(WallEdge edge, out Vector3 start, out Vector3 end)
        {
            start = GetVertexPosition(new Vector2Int(edge.X, edge.Z), 0f);
            if (edge.IsAscendingDiagonal) end = GetVertexPosition(new Vector2Int(edge.X + 1, edge.Z + 1), 0f);
            else if (edge.IsDiagonal) { start = GetVertexPosition(new Vector2Int(edge.X, edge.Z + 1), 0f); end = GetVertexPosition(new Vector2Int(edge.X + 1, edge.Z), 0f); }
            else end = GetVertexPosition(edge.AlongX ? new Vector2Int(edge.X + 1, edge.Z) : new Vector2Int(edge.X, edge.Z + 1), 0f);
        }

        private static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon) return (point - start).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return (point - (start + segment * t)).sqrMagnitude;
        }
    }
}
