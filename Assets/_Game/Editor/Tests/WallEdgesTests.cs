using System.Collections.Generic;
using System.Reflection;
using MaterialCityTycoon.Building;
using NUnit.Framework;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class WallEdgesTests
    {
        private GameObject owner;
        private ConstructionGrid grid;
        private WallEdges walls;
        [SetUp] public void Setup() { owner = new GameObject("grid"); grid = owner.AddComponent<ConstructionGrid>(); walls = new WallEdges(grid); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(owner);

        [Test]
        public void NearestEdgesAndGeometryUseGridOrigin()
        {
            owner.transform.position = new Vector3(4, 2, 7);
            Vector3 center = grid.GetCellCenter(new Vector2Int(5, 6), 2);
            Assert.That(walls.TryFindNearest(center + new Vector3(0, 0, -0.45f), out WallEdge horizontal), Is.True);
            Assert.That(horizontal, Is.EqualTo(new WallEdge(5, 6, true)));
            Assert.That(walls.Center(horizontal, 2, 2.8f), Is.EqualTo(center + new Vector3(0, 1.4f, -0.5f)));
            Assert.That(walls.TryFindNearest(center + new Vector3(0.45f, 0, 0), out WallEdge vertical), Is.True);
            Assert.That(vertical, Is.EqualTo(new WallEdge(6, 6, false)));
            Assert.That(walls.Center(vertical, 2, 2.8f), Is.EqualTo(center + new Vector3(0.5f, 1.4f, 0)));
        }

        [Test]
        public void BoundariesDuplicatesAndRemovalAreValidated()
        {
            WallEdge[] valid = { new(0, 0, true), new(15, 20, true), new(0, 19, false), new(16, 0, false), new(5, 6, true) };
            foreach (WallEdge edge in valid) { Assert.That(walls.TryAdd(edge), Is.True); Assert.That(walls.TryAdd(edge), Is.False); }
            Assert.That(walls.TryAdd(new WallEdge(16, 20, true)), Is.False);
            Assert.That(walls.TryAdd(new WallEdge(16, 20, false)), Is.False);
            Assert.That(walls.TryAdd(new WallEdge(-1, 0, true)), Is.False);
            Assert.That(walls.Remove(valid[0]), Is.True);
            Assert.That(walls.TryAdd(valid[0]), Is.True);
            Vector3 outside = grid.GetCellCenter(Vector2Int.zero, 0) - new Vector3(0.51f, 0, 0);
            Assert.That(walls.TryFindNearest(outside, out _), Is.False);
        }

        [Test]
        public void SeparatingEdgeMapsBothDirectionsBetweenAdjacentCells()
        {
            Assert.That(WallEdges.TryGetSeparatingEdge(new Vector2Int(4, 6), new Vector2Int(5, 6), out WallEdge east), Is.True);
            Assert.That(east, Is.EqualTo(new WallEdge(5, 6, false)));
            Assert.That(WallEdges.TryGetSeparatingEdge(new Vector2Int(5, 6), new Vector2Int(4, 6), out WallEdge west), Is.True);
            Assert.That(west, Is.EqualTo(east));

            Assert.That(WallEdges.TryGetSeparatingEdge(new Vector2Int(4, 6), new Vector2Int(4, 7), out WallEdge north), Is.True);
            Assert.That(north, Is.EqualTo(new WallEdge(4, 7, true)));
            Assert.That(WallEdges.TryGetSeparatingEdge(new Vector2Int(4, 6), new Vector2Int(5, 7), out _), Is.False);
        }

        [Test]
        public void SingleSegmentRangeCreatesExactlyOneWall()
        {
            WallEdge edge = new(4, 5, true);
            Assert.That(walls.TryAddRange(new[] { edge }), Is.True);
            Assert.That(walls.Contains(edge), Is.True);
            Assert.That(walls.TryAddRange(new[] { edge }), Is.False);
        }

        [Test]
        public void WallVisualsUseThreeMetresAndKeepPreviewsColliderFree()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallVisual", privateInstance);

            GameObject wall = (GameObject)create.Invoke(controller, new object[] { new WallEdge(4, 5, true), 0f, null, null, false });
            GameObject preview = (GameObject)create.Invoke(controller, new object[] { new WallEdge(4, 5, false), 0f, null, null, true });

            Assert.That(wall.transform.localScale.y, Is.EqualTo(3f));
            Assert.That(wall.transform.localScale.z, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(wall.transform.position.y, Is.EqualTo(1.5f));
            Assert.That(wall.GetComponent<Collider>().enabled, Is.True);
            Physics.SyncTransforms();
            Assert.That(wall.GetComponent<Collider>().bounds.size.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(preview.transform.localScale.y, Is.EqualTo(3f));
            Assert.That(preview.transform.localScale.x, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(preview.transform.position.y, Is.EqualTo(1.5f));
            Assert.That(preview.GetComponent<Collider>().enabled, Is.False);
        }

        [Test]
        public void DiagonalsUseCellCoordinatesRejectCrossingsAndRespectGridBounds()
        {
            WallEdge ascending = new(4, 5, WallEdgeOrientation.DiagonalAscending);
            WallEdge descending = new(4, 5, WallEdgeOrientation.DiagonalDescending);
            Assert.That(walls.TryAdd(ascending), Is.True);
            Assert.That(walls.TryAdd(ascending), Is.False);
            Assert.That(walls.TryAdd(descending), Is.False);
            Assert.That(walls.ContainsDiagonalAtCell(new Vector2Int(4, 5)), Is.True);
            Assert.That(walls.TryAdd(new WallEdge(15, 19, WallEdgeOrientation.DiagonalDescending)), Is.True);
            Assert.That(walls.TryAdd(new WallEdge(16, 19, WallEdgeOrientation.DiagonalAscending)), Is.False);
            Assert.That(walls.TryAdd(new WallEdge(15, 20, WallEdgeOrientation.DiagonalAscending)), Is.False);
            Assert.That(walls.CanInstallDoor(ascending), Is.False);
        }

        [Test]
        public void DiagonalLinesAreAlignedAtomicAndIncludeTheirInitiallyHighlightedSegment()
        {
            List<WallEdge> line = new();
            WallEdge ascending = new(4, 5, WallEdgeOrientation.DiagonalAscending);
            Assert.That(walls.GetDiagonalLine(ascending, new Vector2Int(7, 8), line), Is.True);
            CollectionAssert.AreEqual(new[]
            {
                new WallEdge(4, 5, WallEdgeOrientation.DiagonalAscending),
                new WallEdge(5, 6, WallEdgeOrientation.DiagonalAscending),
                new WallEdge(6, 7, WallEdgeOrientation.DiagonalAscending),
                new WallEdge(7, 8, WallEdgeOrientation.DiagonalAscending)
            }, line);
            Assert.That(walls.GetDiagonalLine(ascending, new Vector2Int(7, 7), line), Is.False);
            Assert.That(line, Is.Empty);

            WallEdge descending = new(7, 8, WallEdgeOrientation.DiagonalDescending);
            Assert.That(walls.GetDiagonalLine(descending, new Vector2Int(4, 11), line), Is.True);
            CollectionAssert.AreEqual(new[]
            {
                new WallEdge(7, 8, WallEdgeOrientation.DiagonalDescending),
                new WallEdge(6, 9, WallEdgeOrientation.DiagonalDescending),
                new WallEdge(5, 10, WallEdgeOrientation.DiagonalDescending),
                new WallEdge(4, 11, WallEdgeOrientation.DiagonalDescending)
            }, line);
            Assert.That(walls.TryAdd(new WallEdge(5, 6, WallEdgeOrientation.DiagonalDescending)), Is.True);
            Assert.That(walls.TryAddRange(new[]
            {
                new WallEdge(4, 5, WallEdgeOrientation.DiagonalDescending),
                new WallEdge(5, 6, WallEdgeOrientation.DiagonalAscending)
            }), Is.False);
            Assert.That(walls.Contains(new WallEdge(4, 5, WallEdgeOrientation.DiagonalDescending)), Is.False);
        }

        [TestCase(4f, 0.2f, WallEdgeOrientation.AlongX)]
        [TestCase(-4f, 0.2f, WallEdgeOrientation.AlongX)]
        [TestCase(0.2f, 4f, WallEdgeOrientation.AlongZ)]
        [TestCase(0.2f, -4f, WallEdgeOrientation.AlongZ)]
        [TestCase(4f, 3f, WallEdgeOrientation.DiagonalAscending)]
        [TestCase(-4f, -3f, WallEdgeOrientation.DiagonalAscending)]
        [TestCase(4f, -3f, WallEdgeOrientation.DiagonalDescending)]
        [TestCase(-4f, 3f, WallEdgeOrientation.DiagonalDescending)]
        public void WorldSpaceDragSnapsToTheNearestSupportedOrientation(float x, float z, WallEdgeOrientation expected)
        {
            Assert.That(WallEdges.GetSnappedOrientation(new Vector2(x, z)), Is.EqualTo(expected));
        }

        [Test]
        public void AutoSnappedLinesKeepTheirStartAndUpdateWhenTheDragDirectionChanges()
        {
            List<WallEdge> line = new();
            Vector2Int startVertex = new(5, 5);
            walls.GetOrthogonalLine(startVertex, new Vector2Int(8, 6), true, line);
            CollectionAssert.AreEqual(new[]
            {
                new WallEdge(5, 5, true), new WallEdge(6, 5, true), new WallEdge(7, 5, true)
            }, line);

            WallEdge ascending = new(5, 5, WallEdgeOrientation.DiagonalAscending);
            Assert.That(walls.GetDiagonalLineFromDirection(ascending, new Vector2Int(8, 7), line), Is.True);
            CollectionAssert.AreEqual(new[]
            {
                new WallEdge(5, 5, WallEdgeOrientation.DiagonalAscending),
                new WallEdge(6, 6, WallEdgeOrientation.DiagonalAscending),
                new WallEdge(7, 7, WallEdgeOrientation.DiagonalAscending),
                new WallEdge(8, 8, WallEdgeOrientation.DiagonalAscending)
            }, line);

            WallEdge descending = new(5, 5, WallEdgeOrientation.DiagonalDescending);
            Assert.That(walls.GetDiagonalLineFromDirection(descending, new Vector2Int(8, 3), line), Is.True);
            CollectionAssert.AreEqual(new[]
            {
                new WallEdge(5, 5, WallEdgeOrientation.DiagonalDescending),
                new WallEdge(6, 4, WallEdgeOrientation.DiagonalDescending),
                new WallEdge(7, 3, WallEdgeOrientation.DiagonalDescending),
                new WallEdge(8, 2, WallEdgeOrientation.DiagonalDescending)
            }, line);
        }

        [TestCase(WallEdgeOrientation.DiagonalAscending, 45f)]
        [TestCase(WallEdgeOrientation.DiagonalDescending, -45f)]
        public void DiagonalVisualAndPreviewUseTheSamePhysicalGeometry(WallEdgeOrientation orientation, float expectedYaw)
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallVisual", privateInstance);
            WallEdge edge = new(4, 5, orientation);
            GameObject wall = (GameObject)create.Invoke(controller, new object[] { edge, 0f, null, null, false });
            GameObject preview = (GameObject)create.Invoke(controller, new object[] { edge, 0f, null, null, true });

            Vector3 expectedCenter = grid.GetCellCenter(new Vector2Int(4, 5), 1.5f);
            Assert.That(wall.transform.position, Is.EqualTo(expectedCenter));
            Assert.That(wall.transform.localScale.x, Is.EqualTo(Mathf.Sqrt(2f)).Within(0.0001f));
            Assert.That(wall.transform.localScale.y, Is.EqualTo(3f));
            Assert.That(wall.transform.localScale.z, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(Mathf.DeltaAngle(wall.transform.eulerAngles.y, expectedYaw), Is.Zero.Within(0.0001f));
            Assert.That(preview.transform.position, Is.EqualTo(wall.transform.position));
            Assert.That(preview.transform.rotation, Is.EqualTo(wall.transform.rotation));
            Assert.That(preview.transform.localScale, Is.EqualTo(wall.transform.localScale));
            Assert.That(wall.GetComponent<Collider>().enabled, Is.True);
            Assert.That(preview.GetComponent<Collider>().enabled, Is.False);
        }

        [Test]
        public void ExistingDiagonalSelectionDoesNotSelectAnAdjacentOrthogonalWall()
        {
            WallEdge diagonal = new(4, 5, WallEdgeOrientation.DiagonalAscending);
            WallEdge orthogonal = new(4, 5, true);
            Assert.That(walls.TryAdd(diagonal), Is.True);
            Assert.That(walls.TryAdd(orthogonal), Is.True);
            Vector3 center = walls.Center(diagonal, 0f, 0f);
            Assert.That(walls.TryFindNearestExisting(center, out WallEdge selected), Is.True);
            Assert.That(selected, Is.EqualTo(diagonal));
        }

        [Test]
        public void ProjectionSelectsOnlyTheFourDiscreteExternalBordersAtTheGroundSurface()
        {
            WallConstructionController controller = CreateProjectionController();
            Vector3 minimum = grid.GetCellCenter(Vector2Int.zero, 0f) - new Vector3(grid.CellSize * 0.5f, 0f, grid.CellSize * 0.5f);
            Vector3 maximum = minimum + new Vector3(grid.Width * grid.CellSize, 0f, grid.Height * grid.CellSize);
            float horizontalCenter = grid.GetCellCenter(new Vector2Int(8, 10), 0f).x;
            float verticalCenter = grid.GetCellCenter(new Vector2Int(8, 10), 0f).z;
            (Vector3 point, WallEdge edge)[] samples =
            {
                (new Vector3(minimum.x, 0f, verticalCenter), new WallEdge(0, 10, false)),
                (new Vector3(maximum.x, 0f, verticalCenter), new WallEdge(grid.Width, 10, false)),
                (new Vector3(horizontalCenter, 0f, minimum.z), new WallEdge(8, 0, true)),
                (new Vector3(horizontalCenter, 0f, maximum.z), new WallEdge(8, grid.Height, true))
            };

            foreach ((Vector3 point, WallEdge expected) in samples)
            {
                Ray ray = new(point + Vector3.up * 10f, Vector3.down);
                Assert.That(Project(controller, ray, out Vector3 projected), Is.True);
                Assert.That(projected.y, Is.Zero.Within(0.0001f));
                Assert.That(walls.TryFindNearest(projected, out WallEdge selected), Is.True);
                Assert.That(selected, Is.EqualTo(expected));
            }
        }

        [Test]
        public void SmallPointerMovementAndExistingWallCollidersCannotChangeExternalEdgeHeightOrAlignment()
        {
            WallConstructionController controller = CreateProjectionController();
            Vector3 minimum = grid.GetCellCenter(Vector2Int.zero, 0f) - new Vector3(grid.CellSize * 0.5f, 0f, grid.CellSize * 0.5f);
            float verticalCenter = grid.GetCellCenter(new Vector2Int(8, 10), 0f).z;
            GameObject existingWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            existingWall.transform.SetParent(owner.transform, false);
            existingWall.transform.position = new Vector3(minimum.x, 1.5f, verticalCenter);
            existingWall.transform.localScale = new Vector3(0.15f, 3f, 1f);
            Physics.SyncTransforms();

            Ray rayAtWall = new(new Vector3(minimum.x, 10f, verticalCenter), Vector3.down);
            Assert.That(existingWall.GetComponent<Collider>().Raycast(rayAtWall, out RaycastHit wallHit, 500f), Is.True);
            Assert.That(wallHit.point.y, Is.GreaterThan(0f));
            Assert.That(Project(controller, rayAtWall, out Vector3 projected), Is.True);
            Assert.That(projected.y, Is.Zero.Within(0.0001f));
            Assert.That(walls.TryFindNearest(projected, out WallEdge selected), Is.EqualTo(true));
            Assert.That(selected, Is.EqualTo(new WallEdge(0, 10, false)));

            Ray slightlyAbove = new(new Vector3(minimum.x, 10f, verticalCenter + 0.004f), Vector3.down);
            Ray slightlyBelow = new(new Vector3(minimum.x, 10f, verticalCenter - 0.004f), Vector3.down);
            Assert.That(Project(controller, slightlyAbove, out Vector3 abovePoint), Is.True);
            Assert.That(Project(controller, slightlyBelow, out Vector3 belowPoint), Is.True);
            Assert.That(walls.TryFindNearest(abovePoint, out WallEdge above), Is.True);
            Assert.That(walls.TryFindNearest(belowPoint, out WallEdge below), Is.True);
            Assert.That(above, Is.EqualTo(selected));
            Assert.That(below, Is.EqualTo(selected));
            Assert.That(abovePoint.y, Is.EqualTo(projected.y).Within(0.0001f));
            Assert.That(belowPoint.y, Is.EqualTo(projected.y).Within(0.0001f));
        }

        [Test]
        public void ProjectionRejectsPointsOutsideTheGridAndKeepsPreviewAndFinalWallAtTheSamePosition()
        {
            WallConstructionController controller = CreateProjectionController();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            Vector3 minimum = grid.GetCellCenter(Vector2Int.zero, 0f) - new Vector3(grid.CellSize * 0.5f, 0f, grid.CellSize * 0.5f);
            Ray outside = new(new Vector3(minimum.x - 0.001f, 10f, 0f), Vector3.down);
            Assert.That(Project(controller, outside, out _), Is.False);

            float verticalCenter = grid.GetCellCenter(new Vector2Int(8, 10), 0f).z;
            Vector3 borderPoint = new(minimum.x, 0f, verticalCenter);
            Ray rotatedCameraRay = new(new Vector3(-10f, 10f, -10f), (borderPoint - new Vector3(-10f, 10f, -10f)).normalized);
            Assert.That(Project(controller, rotatedCameraRay, out Vector3 projected), Is.True);
            Assert.That(walls.TryFindNearest(projected, out WallEdge edge), Is.True);
            Assert.That(edge, Is.EqualTo(new WallEdge(0, 10, false)));

            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallVisual", privateInstance);
            GameObject visual = (GameObject)create.Invoke(controller, new object[] { edge, projected.y, null, null, true });
            Vector3 expectedPosition = walls.Center(edge, 0f, 3f);
            Assert.That(visual.transform.position.x, Is.EqualTo(expectedPosition.x).Within(0.0001f));
            Assert.That(visual.transform.position.y, Is.EqualTo(expectedPosition.y).Within(0.0001f));
            Assert.That(visual.transform.position.z, Is.EqualTo(expectedPosition.z).Within(0.0001f));
            Assert.That(visual.transform.localScale.y, Is.EqualTo(3f));
        }

        [Test]
        public void SmallPointerMovementStaysClickUntilDragThreshold()
        {
            const BindingFlags privateStatic = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo exceeds = typeof(WallConstructionController).GetMethod("HasExceededDragThreshold", privateStatic);

            Assert.That((bool)exceeds.Invoke(null, new object[] { Vector2.zero, new Vector2(7.9f, 0f) }), Is.False);
            Assert.That((bool)exceeds.Invoke(null, new object[] { Vector2.zero, new Vector2(8f, 0f) }), Is.True);
        }

        [Test]
        public void StraightDragBuildsExpectedSegmentsInAllDirections()
        {
            List<WallEdge> result = new();

            walls.GetStraightLine(new Vector2Int(4, 5), new Vector2Int(7, 5), result);
            CollectionAssert.AreEqual(new[] { new WallEdge(4, 5, true), new WallEdge(5, 5, true), new WallEdge(6, 5, true) }, result);

            walls.GetStraightLine(new Vector2Int(4, 5), new Vector2Int(1, 5), result);
            CollectionAssert.AreEqual(new[] { new WallEdge(3, 5, true), new WallEdge(2, 5, true), new WallEdge(1, 5, true) }, result);

            walls.GetStraightLine(new Vector2Int(4, 5), new Vector2Int(4, 8), result);
            CollectionAssert.AreEqual(new[] { new WallEdge(4, 5, false), new WallEdge(4, 6, false), new WallEdge(4, 7, false) }, result);

            walls.GetStraightLine(new Vector2Int(4, 5), new Vector2Int(4, 2), result);
            CollectionAssert.AreEqual(new[] { new WallEdge(4, 4, false), new WallEdge(4, 3, false), new WallEdge(4, 2, false) }, result);
        }

        [Test]
        public void StraightDragUsesStableDominantAxisAndAllowsLengthToChange()
        {
            List<WallEdge> result = new();
            Vector2Int start = new(5, 5);

            walls.GetStraightLine(start, new Vector2Int(8, 7), result);
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result.TrueForAll(edge => edge.AlongX), Is.True);

            walls.GetStraightLine(start, new Vector2Int(7, 8), result);
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result.TrueForAll(edge => !edge.AlongX), Is.True);

            walls.GetStraightLine(start, start, result);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void StraightDragFromHoveredEdgeKeepsTheFirstHighlightedSegment()
        {
            List<WallEdge> result = new();

            walls.GetStraightLineFromEdge(new WallEdge(4, 5, true), new Vector2Int(5, 5), new Vector2Int(7, 5), result);
            CollectionAssert.AreEqual(new[] { new WallEdge(4, 5, true), new WallEdge(5, 5, true), new WallEdge(6, 5, true) }, result);

            walls.GetStraightLineFromEdge(new WallEdge(4, 5, true), new Vector2Int(5, 5), new Vector2Int(2, 5), result);
            CollectionAssert.AreEqual(new[] { new WallEdge(4, 5, true), new WallEdge(3, 5, true), new WallEdge(2, 5, true) }, result);

            walls.GetStraightLineFromEdge(new WallEdge(4, 5, false), new Vector2Int(4, 6), new Vector2Int(4, 8), result);
            CollectionAssert.AreEqual(new[] { new WallEdge(4, 5, false), new WallEdge(4, 6, false), new WallEdge(4, 7, false) }, result);

            walls.GetStraightLineFromEdge(new WallEdge(4, 5, false), new Vector2Int(4, 6), new Vector2Int(4, 3), result);
            CollectionAssert.AreEqual(new[] { new WallEdge(4, 5, false), new WallEdge(4, 4, false), new WallEdge(4, 3, false) }, result);
        }

        [Test]
        public void SixMetreDragFromHoveredEdgeCreatesSixContiguousSegments()
        {
            List<WallEdge> result = new();
            walls.GetStraightLineFromEdge(new WallEdge(4, 5, true), new Vector2Int(5, 5), new Vector2Int(10, 5), result);

            Assert.That(result, Has.Count.EqualTo(6));
            Assert.That(result[0], Is.EqualTo(new WallEdge(4, 5, true)));
            Assert.That(result[5], Is.EqualTo(new WallEdge(9, 5, true)));
        }

        [Test]
        public void DragRangeValidationIsAtomicForDuplicatesOccupiedAndOutsideEdges()
        {
            List<WallEdge> valid = new();
            walls.GetStraightLine(new Vector2Int(1, 1), new Vector2Int(4, 1), valid);
            Assert.That(walls.CanAddRange(valid), Is.True);
            Assert.That(walls.TryAddRange(valid), Is.True);
            Assert.That(walls.CanAddRange(valid), Is.False);
            Assert.That(walls.TryAddRange(valid), Is.False);

            List<WallEdge> partialConflict = new() { new(4, 1, true), new(3, 1, true) };
            Assert.That(walls.TryAddRange(partialConflict), Is.False);
            Assert.That(walls.Contains(new WallEdge(4, 1, true)), Is.False);

            List<WallEdge> invalid = new() { new(15, 20, true), new(16, 20, true) };
            Assert.That(walls.TryAddRange(invalid), Is.False);
            Assert.That(walls.Contains(new WallEdge(15, 20, true)), Is.False);

            List<WallEdge> duplicate = new() { new(8, 2, true), new(8, 2, true) };
            Assert.That(walls.TryAddRange(duplicate), Is.False);
        }

        [Test]
        public void JunctionTopologyRecognizesStraightCornerTAndCrossWithoutChangingOccupancy()
        {
            Vector2Int vertex = new(5, 5);
            WallEdge west = new(4, 5, true);
            WallEdge east = new(5, 5, true);
            WallEdge north = new(5, 5, false);
            WallEdge south = new(5, 4, false);

            Assert.That(walls.TryAddRange(new[] { west, east }), Is.True);
            Assert.That(walls.GetConnectionCountAtVertex(vertex), Is.EqualTo(2));

            Assert.That(walls.TryAdd(north), Is.True);
            Assert.That(walls.GetConnectionCountAtVertex(vertex), Is.EqualTo(3));

            Assert.That(walls.TryAdd(south), Is.True);
            Assert.That(walls.GetConnectionCountAtVertex(vertex), Is.EqualTo(4));
            Assert.That(walls.Contains(west) && walls.Contains(east) && walls.Contains(north) && walls.Contains(south), Is.True);
        }

        [Test]
        public void RightAngleCornerAndOpenEndAreDistinguished()
        {
            Vector2Int corner = new(5, 5);
            WallEdge horizontal = new(4, 5, true);
            WallEdge vertical = new(5, 5, false);
            Assert.That(walls.TryAddRange(new[] { horizontal, vertical }), Is.True);

            Assert.That(walls.GetConnectionCountAtVertex(corner), Is.EqualTo(2));
            Assert.That(walls.GetConnectionCountAtVertex(new Vector2Int(4, 5)), Is.EqualTo(1));
            Assert.That(walls.GetConnectionCountAtVertex(new Vector2Int(5, 6)), Is.EqualTo(1));
        }

        [Test]
        public void PreviewCandidatesUseTheSameJunctionTopologyAsFinalWalls()
        {
            WallEdge existing = new(4, 5, true);
            WallEdge proposed = new(5, 5, false);
            Assert.That(walls.TryAdd(existing), Is.True);

            List<Vector2Int> junctions = new();
            walls.GetAffectedJunctionVertices(new[] { proposed }, junctions);

            CollectionAssert.AreEquivalent(new[] { new Vector2Int(5, 5) }, junctions);
            Assert.That(walls.GetConnectionCountAtVertex(new Vector2Int(5, 5), new[] { proposed }), Is.EqualTo(2));
            Assert.That(walls.Contains(proposed), Is.False);
        }

        [Test]
        public void RemovingAndRebuildingAnEdgeUpdatesTheSameJunctionTopology()
        {
            WallEdge[] cross =
            {
                new(4, 5, true), new(5, 5, true), new(5, 5, false), new(5, 4, false)
            };
            Vector2Int vertex = new(5, 5);
            Assert.That(walls.TryAddRange(cross), Is.True);
            Assert.That(walls.GetConnectionCountAtVertex(vertex), Is.EqualTo(4));

            Assert.That(walls.Remove(cross[3]), Is.True);
            Assert.That(walls.GetConnectionCountAtVertex(vertex), Is.EqualTo(3));
            Assert.That(walls.Remove(cross[2]), Is.True);
            Assert.That(walls.GetConnectionCountAtVertex(vertex), Is.EqualTo(2));
            Assert.That(walls.TryAdd(cross[2]), Is.True);
            Assert.That(walls.GetConnectionCountAtVertex(vertex), Is.EqualTo(3));
            Assert.That(walls.Contains(cross[0]) && walls.Contains(cross[1]), Is.True);
        }

        [Test]
        public void WallJointVisualMatchesWallHeightAndPreviewHasNoCollider()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallJointVisual", privateInstance);

            GameObject joint = (GameObject)create.Invoke(controller, new object[] { new Vector2Int(4, 5), 0f, null, "joint", false });
            GameObject preview = (GameObject)create.Invoke(controller, new object[] { new Vector2Int(4, 5), 0f, null, "preview", true });

            Assert.That(joint.transform.localScale.y, Is.EqualTo(3f));
            Assert.That(joint.transform.localScale.x, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(joint.transform.localScale.z, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(joint.transform.position.y, Is.EqualTo(1.5f));
            Assert.That(joint.GetComponent<Collider>().enabled, Is.True);
            Assert.That(preview.GetComponent<Collider>().enabled, Is.False);
        }


        [Test]
        public void FinalJunctionVisualIsCreatedAndRemovedWithItsAffectedEdges()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallVisual", privateInstance);
            MethodInfo refresh = typeof(WallConstructionController).GetMethod("RefreshFinalJunctionVisuals", privateInstance);
            var visualMap = (Dictionary<WallEdge, GameObject>)typeof(WallConstructionController)
                .GetField("visuals", privateInstance).GetValue(controller);
            var junctionMap = (Dictionary<Vector2Int, GameObject>)typeof(WallConstructionController)
                .GetField("jointVisuals", privateInstance).GetValue(controller);
            WallEdge west = new(4, 5, true);
            WallEdge east = new(5, 5, true);

            Assert.That(walls.TryAddRange(new[] { west, east }), Is.True);
            visualMap.Add(west, (GameObject)create.Invoke(controller, new object[] { west, 0f, null, null, false }));
            visualMap.Add(east, (GameObject)create.Invoke(controller, new object[] { east, 0f, null, null, false }));
            refresh.Invoke(controller, new object[] { new[] { west, east }, 0f });
            Assert.That(junctionMap.ContainsKey(new Vector2Int(5, 5)), Is.True);

            Assert.That(walls.Remove(east), Is.True);
            refresh.Invoke(controller, new object[] { new[] { east }, 0f });
            Assert.That(junctionMap.ContainsKey(new Vector2Int(5, 5)), Is.False);
        }

        [Test]
        public void DoorsRequireAnExistingWallAndRejectDuplicates()
        {
            WallEdge empty = new(5, 5, true);
            Assert.That(walls.CanInstallDoor(empty), Is.False);
            Assert.That(walls.TryInstallDoor(empty), Is.False);
            Assert.That(walls.TryAdd(empty), Is.True);
            Assert.That(walls.TryInstallDoor(empty), Is.True);
            Assert.That(walls.TryInstallDoor(empty), Is.False);
        }

        [Test]
        public void ExternalDoorsAreRecognizedOnEveryGridPerimeterAndRestoreTheirClosedPassage()
        {
            WallEdge[] externalEdges =
            {
                new(5, 0, true),
                new(5, grid.Height, true),
                new(0, 6, false),
                new(grid.Width, 6, false)
            };
            Assert.That(walls.TryAddRange(externalEdges), Is.True);

            foreach (WallEdge edge in externalEdges)
            {
                Assert.That(walls.IsExternalEdge(edge), Is.True);
                Assert.That(walls.IsExternalPassageOpen(edge), Is.False);
                Assert.That(walls.TryGetExternalDoorInteriorCell(edge, out Vector2Int interior), Is.True);
                Assert.That(grid.IsInside(interior), Is.True);
                Assert.That(walls.CanInstallDoor(edge), Is.True);
                Assert.That(walls.TryInstallDoor(edge), Is.True);
                Assert.That(walls.IsExternalDoor(edge), Is.True);
                Assert.That(walls.IsExternalPassageOpen(edge), Is.True);
                Assert.That(walls.IsDoorwayCell(interior), Is.True);
                Assert.That(walls.RemoveDoor(edge), Is.True);
                Assert.That(walls.Contains(edge), Is.True);
                Assert.That(walls.IsExternalPassageOpen(edge), Is.False);
            }
        }

        [Test]
        public void InteriorDoorsAreNotClassifiedAsExternalEntrances()
        {
            WallEdge internalEdge = new(5, 5, true);
            Assert.That(walls.TryAdd(internalEdge), Is.True);
            Assert.That(walls.TryGetExternalDoorInteriorCell(internalEdge, out _), Is.False);
            Assert.That(walls.IsExternalEdge(internalEdge), Is.False);
            Assert.That(walls.TryInstallDoor(internalEdge), Is.True);
            Assert.That(walls.IsExternalDoor(internalEdge), Is.False);
            Assert.That(walls.IsExternalPassageOpen(internalEdge), Is.False);
        }

        [Test]
        public void ExternalDoorChangesTheControllerPassageStatusWithoutASecondGridCell()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            WallEdge exteriorEdge = new(0, 6, false);

            Assert.That(walls.TryAdd(exteriorEdge), Is.True);
            Assert.That(controller.IsPassageBlocked(exteriorEdge), Is.True);
            Assert.That(walls.TryInstallDoor(exteriorEdge), Is.True);
            Assert.That(controller.IsPassageBlocked(exteriorEdge), Is.False);
            Assert.That(walls.RemoveDoor(exteriorEdge), Is.True);
            Assert.That(controller.IsPassageBlocked(exteriorEdge), Is.True);
        }

        [Test]
        public void DoorsAllowPassageForBothWallOrientationsAndRestoreBlockingWhenRemoved()
        {
            WallEdge alongX = new(5, 5, true);
            WallEdge alongZ = new(8, 7, false);
            Assert.That(walls.TryAddRange(new[] { alongX, alongZ }), Is.True);
            Assert.That(walls.TryGetAdjacentCells(alongX, out Vector2Int south, out Vector2Int north), Is.True);
            Assert.That(walls.TryGetAdjacentCells(alongZ, out Vector2Int west, out Vector2Int east), Is.True);
            Assert.That(walls.IsPassageBlocked(south, north), Is.True);
            Assert.That(walls.IsPassageBlocked(west, east), Is.True);

            Assert.That(walls.TryInstallDoor(alongX), Is.True);
            Assert.That(walls.TryInstallDoor(alongZ), Is.True);
            Assert.That(walls.IsPassageBlocked(south, north), Is.False);
            Assert.That(walls.IsPassageBlocked(west, east), Is.False);
            Assert.That(walls.RemoveDoor(alongX), Is.True);
            Assert.That(walls.Contains(alongX), Is.True);
            Assert.That(walls.IsPassageBlocked(south, north), Is.True);
        }

        [Test]
        public void DoorsPreserveWallTopologyAndIdentifyOnlyTheirPassageCells()
        {
            WallEdge west = new(4, 5, true);
            WallEdge door = new(5, 5, true);
            Vector2Int sharedVertex = new(5, 5);
            Assert.That(walls.TryAddRange(new[] { west, door }), Is.True);
            Assert.That(walls.GetConnectionCountAtVertex(sharedVertex), Is.EqualTo(2));
            Assert.That(walls.TryInstallDoor(door), Is.True);
            Assert.That(walls.GetConnectionCountAtVertex(sharedVertex), Is.EqualTo(2));
            Assert.That(walls.IsDoorwayCell(new Vector2Int(5, 4)), Is.True);
            Assert.That(walls.IsDoorwayCell(new Vector2Int(5, 5)), Is.True);
            Assert.That(walls.IsDoorwayCell(new Vector2Int(4, 4)), Is.False);
        }

        [Test]
        public void DoorVisualLeavesTheOpeningWithoutACollider()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateDoorVisual", privateInstance);

            GameObject door = (GameObject)create.Invoke(controller, new object[] { new WallEdge(4, 5, true), 0f, null, "door", false });
            Collider[] colliders = door.GetComponentsInChildren<Collider>();
            Physics.SyncTransforms();
            Assert.That(door.GetComponent<Collider>(), Is.Null);
            Assert.That(colliders, Has.Length.EqualTo(1));
            Assert.That(colliders[0].bounds.min.y, Is.EqualTo(2.1f).Within(0.0001f));
            Assert.That(colliders[0].bounds.max.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(colliders[0].bounds.size.z, Is.EqualTo(0.15f).Within(0.0001f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DoorPreviewOutlinesTheFullOpeningWithoutColliders(bool alongX)
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateDoorVisual", privateInstance);
            WallEdge edge = new(4, 5, alongX);

            GameObject preview = (GameObject)create.Invoke(controller, new object[] { edge, 0f, null, "preview", true });
            Transform left = preview.transform.Find("Door Opening Left");
            Transform right = preview.transform.Find("Door Opening Right");
            Transform header = preview.transform.Find("Door Opening Header");
            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);
            Assert.That(header, Is.Not.Null);
            Assert.That(preview.transform.Find("Door Lintel"), Is.Null);
            Assert.That(left.localPosition.y - left.localScale.y * 0.5f, Is.Zero.Within(0.0001f));
            Assert.That(left.localPosition.y + left.localScale.y * 0.5f, Is.EqualTo(2.1f).Within(0.0001f));
            Assert.That(header.localPosition.y + header.localScale.y * 0.5f, Is.EqualTo(2.1f).Within(0.0001f));

            float openingWidth = alongX
                ? right.localPosition.x + right.localScale.x * 0.5f - (left.localPosition.x - left.localScale.x * 0.5f)
                : right.localPosition.z + right.localScale.z * 0.5f - (left.localPosition.z - left.localScale.z * 0.5f);
            Assert.That(openingWidth, Is.EqualTo(grid.CellSize).Within(0.0001f));
            foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False);
            }
        }

        [Test]
        public void DoorPreviewRepositionsBetweenAxesAndIsHiddenWhenCancelled()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateDoorVisual", privateInstance);
            MethodInfo position = typeof(WallConstructionController).GetMethod("PositionDoor", privateInstance);
            MethodInfo hide = typeof(WallConstructionController).GetMethod("HideDoorPreview", privateInstance);
            GameObject preview = (GameObject)create.Invoke(controller, new object[] { new WallEdge(4, 5, true), 0f, null, "preview", true });
            typeof(WallConstructionController).GetField("doorPreviewVisual", privateInstance).SetValue(controller, preview);

            position.Invoke(controller, new object[] { preview, new WallEdge(4, 5, false), 0f });
            Transform left = preview.transform.Find("Door Opening Left");
            Transform right = preview.transform.Find("Door Opening Right");
            Assert.That(left.localPosition.x, Is.Zero.Within(0.0001f));
            Assert.That(right.localPosition.x, Is.Zero.Within(0.0001f));
            Assert.That(Mathf.Abs(left.localPosition.z), Is.EqualTo(Mathf.Abs(right.localPosition.z)).Within(0.0001f));
            Assert.That(left.localScale.z, Is.EqualTo(0.035f).Within(0.0001f));

            hide.Invoke(controller, null);
            Assert.That(preview.activeSelf, Is.False);
        }

        private WallConstructionController CreateProjectionController()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            BoxCollider ground = owner.AddComponent<BoxCollider>();
            ground.center = Vector3.down * 0.05f;
            ground.size = new Vector3(grid.Width, 0.1f, grid.Height);
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("ground", privateInstance).SetValue(controller, ground);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            return controller;
        }

        private static bool Project(WallConstructionController controller, Ray ray, out Vector3 point)
        {
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            object[] arguments = { ray, null };
            bool result = (bool)typeof(WallConstructionController)
                .GetMethod("TryProjectRayToGridPlane", privateInstance)
                .Invoke(controller, arguments);
            point = (Vector3)arguments[1];
            return result;
        }

    }
}
