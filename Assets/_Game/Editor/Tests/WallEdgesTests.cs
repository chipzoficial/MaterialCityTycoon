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

            Assert.That(wall.transform.localScale.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(wall.transform.localScale.y, Is.EqualTo(3f));
            Assert.That(wall.transform.localScale.z, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(wall.transform.position.y, Is.EqualTo(1.5f));
            Assert.That(wall.GetComponent<Collider>().enabled, Is.True);
            Physics.SyncTransforms();
            Assert.That(wall.GetComponent<Collider>().bounds.size.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(preview.transform.localScale.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(preview.transform.localScale.y, Is.EqualTo(3f));
            Assert.That(preview.transform.localScale.z, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(Vector3.Dot(preview.transform.right, Vector3.forward), Is.EqualTo(1f).Within(0.0001f));
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

        [TestCase(WallEdgeOrientation.AlongX, 1, 0)]
        [TestCase(WallEdgeOrientation.AlongX, -1, 0)]
        [TestCase(WallEdgeOrientation.AlongZ, 0, 1)]
        [TestCase(WallEdgeOrientation.AlongZ, 0, -1)]
        [TestCase(WallEdgeOrientation.DiagonalAscending, 1, 1)]
        [TestCase(WallEdgeOrientation.DiagonalAscending, -1, -1)]
        [TestCase(WallEdgeOrientation.DiagonalDescending, 1, -1)]
        [TestCase(WallEdgeOrientation.DiagonalDescending, -1, 1)]
        public void DragPreviewAndBuiltSegmentsMatchTheirGridEndpoints(
            WallEdgeOrientation orientation, int directionX, int directionZ)
        {
            owner.transform.position = new Vector3(4f, 2f, 7f);
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallVisual", privateInstance);
            const float baseHeight = 0.35f;
            Vector2Int startVertex = new(8, 10);
            Vector3 startPoint = GetExpectedGridVertex(startVertex, 0f);
            Vector3 pointerPoint = startPoint + new Vector3(directionX * 5f * grid.CellSize, 0f,
                directionZ * 5f * grid.CellSize);
            List<WallEdge> line = new();
            walls.GetLineFromDirection(startVertex, pointerPoint, orientation, line);

            Assert.That(line, Has.Count.EqualTo(5));
            Assert.That(walls.TryAddRange(line), Is.True);
            for (int index = 0; index < line.Count; index++)
            {
                int firstX = startVertex.x + index * directionX;
                int firstZ = startVertex.y + index * directionZ;
                WallEdge expectedEdge = orientation == WallEdgeOrientation.AlongX
                    ? new WallEdge(directionX > 0 ? firstX : firstX - 1, firstZ, true)
                    : orientation == WallEdgeOrientation.AlongZ
                        ? new WallEdge(firstX, directionZ > 0 ? firstZ : firstZ - 1, false)
                        : new WallEdge(Mathf.Min(firstX, firstX + directionX),
                            Mathf.Min(firstZ, firstZ + directionZ), orientation);
                Assert.That(line[index], Is.EqualTo(expectedEdge));

                ExpectedEndpoints(expectedEdge, baseHeight + 1.5f, out Vector3 expectedStart, out Vector3 expectedEnd);
                GameObject wall = (GameObject)create.Invoke(controller,
                    new object[] { expectedEdge, baseHeight, null, null, false });
                GameObject preview = (GameObject)create.Invoke(controller,
                    new object[] { expectedEdge, baseHeight, null, null, true });
                Vector3 actualStart = wall.transform.TransformPoint(Vector3.left * 0.5f);
                Vector3 actualEnd = wall.transform.TransformPoint(Vector3.right * 0.5f);

                Assert.That(Vector3.Distance(actualStart, expectedStart), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(actualEnd, expectedEnd), Is.LessThan(0.0001f));
                Assert.That(wall.transform.position.y, Is.EqualTo(baseHeight + 1.5f).Within(0.0001f));
                Assert.That(wall.transform.localScale.x,
                    Is.EqualTo(Vector3.Distance(expectedStart, expectedEnd)).Within(0.0001f));
                Assert.That(wall.transform.localScale.y, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(wall.transform.localScale.z, Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(Vector3.Dot(wall.transform.right,
                    (expectedEnd - expectedStart).normalized), Is.EqualTo(1f).Within(0.0001f));

                BoxCollider collider = wall.GetComponent<BoxCollider>();
                Assert.That(collider.enabled, Is.True);
                Assert.That(collider.size, Is.EqualTo(Vector3.one));
                Assert.That(collider.center, Is.EqualTo(Vector3.zero));
                Physics.SyncTransforms();
                Assert.That(collider.bounds.min.y, Is.EqualTo(baseHeight).Within(0.0001f));
                Assert.That(collider.bounds.max.y, Is.EqualTo(baseHeight + 3f).Within(0.0001f));

                Assert.That(preview.GetComponent<Collider>().enabled, Is.False);
                Assert.That(Vector3.Distance(
                    preview.transform.TransformPoint(Vector3.left * 0.5f), expectedStart), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(
                    preview.transform.TransformPoint(Vector3.right * 0.5f), expectedEnd), Is.LessThan(0.0001f));
                Assert.That(preview.transform.position, Is.EqualTo(wall.transform.position));
                Assert.That(preview.transform.rotation, Is.EqualTo(wall.transform.rotation));
                Assert.That(preview.transform.localScale, Is.EqualTo(wall.transform.localScale));

                if (index > 0)
                {
                    ExpectedEndpoints(line[index - 1], baseHeight + 1.5f,
                        out Vector3 previousStart, out Vector3 previousEnd);
                    float nearestSharedEndpoint = Mathf.Min(
                        Mathf.Min(Vector3.Distance(previousStart, expectedStart), Vector3.Distance(previousStart, expectedEnd)),
                        Mathf.Min(Vector3.Distance(previousEnd, expectedStart), Vector3.Distance(previousEnd, expectedEnd)));
                    Assert.That(nearestSharedEndpoint, Is.LessThan(0.0001f));
                }
            }
        }

        [Test]
        public void ChangingDragDirectionReplacesThePreviousPreviewSequence()
        {
            Vector2Int startVertex = new(8, 10);
            Vector3 startPoint = GetExpectedGridVertex(startVertex, 0f);
            List<WallEdge> line = new();

            walls.GetLineFromDirection(startVertex, startPoint + Vector3.right * 3f,
                WallEdgeOrientation.AlongX, line);
            Assert.That(line, Has.Count.EqualTo(3));

            walls.GetLineFromDirection(startVertex, startPoint + Vector3.back * 5f,
                WallEdgeOrientation.AlongZ, line);
            CollectionAssert.AreEqual(new[]
            {
                new WallEdge(8, 9, false),
                new WallEdge(8, 8, false),
                new WallEdge(8, 7, false),
                new WallEdge(8, 6, false),
                new WallEdge(8, 5, false)
            }, line);
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

            Mesh jointMesh = joint.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(jointMesh.bounds.size.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(jointMesh.bounds.size.x, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(jointMesh.bounds.size.z, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(joint.transform.position.y, Is.EqualTo(1.5f));
            Assert.That(joint.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(jointMesh));
            Assert.That(preview.GetComponent<Collider>(), Is.Null);
            Assert.That(preview.GetComponent<MeshFilter>().sharedMesh.bounds, Is.EqualTo(jointMesh.bounds));
        }


        [Test]
        public void FinalJunctionConnectorIsCreatedForBranchesAndRemovedWhenOnlyTwoWallsRemain()
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
            WallEdge diagonal = new(5, 5, WallEdgeOrientation.DiagonalAscending);
            Vector2Int vertex = new(5, 5);

            Assert.That(walls.TryAddRange(new[] { west, east, diagonal }), Is.True);
            visualMap.Add(west, (GameObject)create.Invoke(controller, new object[] { west, 0f, null, null, false }));
            visualMap.Add(east, (GameObject)create.Invoke(controller, new object[] { east, 0f, null, null, false }));
            visualMap.Add(diagonal, (GameObject)create.Invoke(controller, new object[] { diagonal, 0f, null, null, false }));
            refresh.Invoke(controller, new object[] { new[] { west, east, diagonal }, 0f });
            Assert.That(junctionMap.ContainsKey(vertex), Is.True);
            Assert.That(junctionMap[vertex].GetComponent<MeshCollider>(), Is.Not.Null);

            Assert.That(walls.Remove(diagonal), Is.True);
            refresh.Invoke(controller, new object[] { new[] { diagonal }, 0f });
            Assert.That(junctionMap.ContainsKey(vertex), Is.False);
            Assert.That(walls.Contains(west) && walls.Contains(east), Is.True);

            Assert.That(walls.TryAdd(diagonal), Is.True);
            refresh.Invoke(controller, new object[] { new[] { diagonal }, 0f });
            Assert.That(junctionMap.ContainsKey(vertex), Is.True);
            Assert.That(walls.Contains(west) && walls.Contains(east) && walls.Contains(diagonal), Is.True);
        }

        [TestCase(WallEdgeOrientation.AlongX, 4, 5, WallEdgeOrientation.DiagonalAscending, 5, 5)]
        [TestCase(WallEdgeOrientation.AlongX, 4, 5, WallEdgeOrientation.DiagonalDescending, 4, 5)]
        [TestCase(WallEdgeOrientation.DiagonalAscending, 5, 5, WallEdgeOrientation.DiagonalDescending, 5, 4)]
        public void OrthogonalAndDiagonalEdgesShareTheirExactGridVertex(
            WallEdgeOrientation firstOrientation, int firstX, int firstZ,
            WallEdgeOrientation secondOrientation, int secondX, int secondZ)
        {
            WallEdge first = new(firstX, firstZ, firstOrientation);
            WallEdge second = new(secondX, secondZ, secondOrientation);
            Assert.That(walls.TryAddRange(new[] { first, second }), Is.True);

            WallEdges.GetGridEndpoints(first, out Vector2Int firstStart, out Vector2Int firstEnd);
            WallEdges.GetGridEndpoints(second, out Vector2Int secondStart, out Vector2Int secondEnd);
            Assert.That(firstStart == secondStart || firstStart == secondEnd
                || firstEnd == secondStart || firstEnd == secondEnd, Is.True);

            Vector2Int shared = firstStart == secondStart || firstStart == secondEnd ? firstStart : firstEnd;
            Assert.That(walls.GetConnectionCountAtVertex(shared), Is.EqualTo(2));
            Vector2Int firstOpen = firstStart == shared ? firstEnd : firstStart;
            Vector2Int secondOpen = secondStart == shared ? secondEnd : secondStart;
            Assert.That(walls.GetConnectionCountAtVertex(firstOpen), Is.EqualTo(1));
            Assert.That(walls.GetConnectionCountAtVertex(secondOpen), Is.EqualTo(1));
        }

        [TestCase(WallEdgeOrientation.AlongX, 4, 5, WallEdgeOrientation.DiagonalAscending, 5, 5)]
        [TestCase(WallEdgeOrientation.AlongX, 4, 5, WallEdgeOrientation.DiagonalDescending, 4, 5)]
        [TestCase(WallEdgeOrientation.DiagonalAscending, 5, 5, WallEdgeOrientation.DiagonalDescending, 5, 4)]
        public void AngularWallCollidersMeetOnlyWithinTheirSharedThickness(
            WallEdgeOrientation firstOrientation, int firstX, int firstZ,
            WallEdgeOrientation secondOrientation, int secondX, int secondZ)
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallVisual", privateInstance);
            GameObject first = (GameObject)create.Invoke(controller, new object[]
            {
                new WallEdge(firstX, firstZ, firstOrientation), 0f, null, null, false
            });
            GameObject second = (GameObject)create.Invoke(controller, new object[]
            {
                new WallEdge(secondX, secondZ, secondOrientation), 0f, null, null, false
            });
            Physics.SyncTransforms();

            Assert.That(Physics.ComputePenetration(
                first.GetComponent<Collider>(), first.transform.position, first.transform.rotation,
                second.GetComponent<Collider>(), second.transform.position, second.transform.rotation,
                out _, out float penetration), Is.True);
            Assert.That(penetration, Is.LessThanOrEqualTo(0.15f + 0.0001f));
        }

        [Test]
        public void CollinearWallCollidersMeetWithoutOverlapping()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallVisual", privateInstance);
            GameObject first = (GameObject)create.Invoke(controller,
                new object[] { new WallEdge(4, 5, true), 0f, null, null, false });
            GameObject second = (GameObject)create.Invoke(controller,
                new object[] { new WallEdge(5, 5, true), 0f, null, null, false });
            Physics.SyncTransforms();

            Assert.That(Physics.ComputePenetration(
                first.GetComponent<Collider>(), first.transform.position, first.transform.rotation,
                second.GetComponent<Collider>(), second.transform.position, second.transform.rotation,
                out _, out float penetration), Is.True);
            Assert.That(penetration, Is.LessThanOrEqualTo(0.0001f));
        }

        [Test]
        public void OrthogonalAndDiagonalLinesExposeOnlyTheirSharedEndpoints()
        {
            WallEdge[] line =
            {
                new(4, 5, true), new(5, 5, true),
                new(6, 5, WallEdgeOrientation.DiagonalAscending),
                new(7, 6, WallEdgeOrientation.DiagonalAscending)
            };
            Assert.That(walls.TryAddRange(line), Is.True);

            Assert.That(walls.GetConnectionCountAtVertex(new Vector2Int(5, 5)), Is.EqualTo(2));
            Assert.That(walls.GetConnectionCountAtVertex(new Vector2Int(7, 6)), Is.EqualTo(2));

            List<Vector2Int> junctions = new();
            walls.GetAffectedJunctionVertices(line, junctions);
            CollectionAssert.Contains(junctions, new Vector2Int(5, 5));
            CollectionAssert.Contains(junctions, new Vector2Int(6, 5));
            CollectionAssert.Contains(junctions, new Vector2Int(7, 6));
            Assert.That(junctions, Has.Count.EqualTo(3));
        }

        [Test]
        public void DiagonalBranchesCreateBoundedJunctionMeshMatchingPreviewAndFinal()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo previewMethod = typeof(WallConstructionController)
                .GetMethod("ShowPreviewJunctionVisuals", privateInstance);
            MethodInfo refresh = typeof(WallConstructionController)
                .GetMethod("RefreshFinalJunctionVisuals", privateInstance);
            List<GameObject> previews = (List<GameObject>)typeof(WallConstructionController)
                .GetField("previewJointVisuals", privateInstance).GetValue(controller);
            var finals = (Dictionary<Vector2Int, GameObject>)typeof(WallConstructionController)
                .GetField("jointVisuals", privateInstance).GetValue(controller);
            WallEdge[] existing =
            {
                new(4, 5, true), new(5, 5, true)
            };
            WallEdge proposed = new(5, 5, WallEdgeOrientation.DiagonalAscending);
            Vector2Int vertex = new(5, 5);

            Assert.That(walls.TryAddRange(existing), Is.True);
            previewMethod.Invoke(controller, new object[] { new[] { proposed }, 0f, null });
            Assert.That(previews, Has.Count.EqualTo(1));
            Mesh previewMesh = previews[0].GetComponent<MeshFilter>().sharedMesh;
            Assert.That(previews[0].activeSelf, Is.True);
            Assert.That(previews[0].GetComponent<Collider>(), Is.Null);
            Assert.That(previewMesh.bounds.size.x, Is.LessThanOrEqualTo(0.15f + 0.0001f));
            Assert.That(previewMesh.bounds.size.z, Is.LessThanOrEqualTo(0.15f + 0.0001f));
            Assert.That(previewMesh.bounds.min.y, Is.EqualTo(-1.5f).Within(0.0001f));
            Assert.That(previewMesh.bounds.max.y, Is.EqualTo(1.5f).Within(0.0001f));
            MethodInfo hide = typeof(WallConstructionController).GetMethod("HidePreviewVisuals", privateInstance);
            hide.Invoke(controller, null);
            Assert.That(previews[0].activeSelf, Is.False);
            Assert.That(walls.Contains(proposed), Is.False);

            Assert.That(walls.TryAdd(proposed), Is.True);
            refresh.Invoke(controller, new object[] { new[] { proposed }, 0f });
            Mesh finalMesh = finals[vertex].GetComponent<MeshFilter>().sharedMesh;
            CollectionAssert.AreEqual(previewMesh.vertices, finalMesh.vertices);
            CollectionAssert.AreEqual(previewMesh.triangles, finalMesh.triangles);
            Assert.That(finals[vertex].GetComponent<MeshCollider>().sharedMesh, Is.SameAs(finalMesh));
            Assert.That(walls.Contains(proposed), Is.True);
        }

        [Test]
        public void FourWayJunctionIncludesDiagonalBranchesWithoutExceedingWallThickness()
        {
            WallEdge[] branches =
            {
                new(4, 5, true), new(5, 5, true),
                new(5, 4, false), new(5, 5, false),
                new(5, 5, WallEdgeOrientation.DiagonalAscending),
                new(5, 4, WallEdgeOrientation.DiagonalDescending)
            };
            Vector2Int vertex = new(5, 5);
            Assert.That(walls.TryAddRange(branches), Is.True);

            List<WallEdge> incident = new();
            walls.GetIncidentEdgesAtVertex(vertex, null, incident);
            Assert.That(incident, Has.Count.EqualTo(6));
            Assert.That(walls.GetConnectionCountAtVertex(vertex), Is.EqualTo(6));

            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallJointVisual", privateInstance);
            GameObject connector = (GameObject)create.Invoke(controller,
                new object[] { vertex, 0f, null, "junction", false });
            Mesh mesh = connector.GetComponent<MeshFilter>().sharedMesh;

            Assert.That(mesh.bounds.size.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(mesh.bounds.size.x, Is.LessThanOrEqualTo(0.15f + 0.0001f));
            Assert.That(mesh.bounds.size.z, Is.LessThanOrEqualTo(0.15f + 0.0001f));
            Assert.That(connector.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(mesh));
            float signedVolume = 0f;
            for (int index = 0; index < mesh.triangles.Length; index += 3)
            {
                Vector3 first = mesh.vertices[mesh.triangles[index]];
                Vector3 second = mesh.vertices[mesh.triangles[index + 1]];
                Vector3 third = mesh.vertices[mesh.triangles[index + 2]];
                signedVolume += Vector3.Dot(first, Vector3.Cross(second, third)) / 6f;
            }
            Assert.That(signedVolume, Is.GreaterThan(0f));
        }

        [Test]
        public void PreviewTemporarilyReplacesAnExistingConnectorAndRestoresItWhenCancelled()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo refresh = typeof(WallConstructionController).GetMethod("RefreshFinalJunctionVisuals", privateInstance);
            MethodInfo show = typeof(WallConstructionController).GetMethod("ShowPreviewJunctionVisuals", privateInstance);
            MethodInfo hide = typeof(WallConstructionController).GetMethod("HidePreviewVisuals", privateInstance);
            var finals = (Dictionary<Vector2Int, GameObject>)typeof(WallConstructionController)
                .GetField("jointVisuals", privateInstance).GetValue(controller);
            List<GameObject> previews = (List<GameObject>)typeof(WallConstructionController)
                .GetField("previewJointVisuals", privateInstance).GetValue(controller);
            WallEdge[] existing =
            {
                new(4, 5, true),
                new(5, 5, false),
                new(5, 5, WallEdgeOrientation.DiagonalAscending)
            };
            WallEdge proposed = new(5, 5, true);
            Vector2Int vertex = new(5, 5);
            Assert.That(walls.TryAddRange(existing), Is.True);
            refresh.Invoke(controller, new object[] { existing, 0f });
            GameObject final = finals[vertex];

            show.Invoke(controller, new object[] { new[] { proposed }, 0f, null });
            Assert.That(final.activeSelf, Is.False);
            Assert.That(previews, Has.Count.EqualTo(1));
            Assert.That(previews[0].activeSelf, Is.True);
            Assert.That(walls.Contains(proposed), Is.False);

            hide.Invoke(controller, null);
            Assert.That(final.activeSelf, Is.True);
            Assert.That(previews[0].activeSelf, Is.False);
            Assert.That(walls.Contains(proposed), Is.False);
        }

        [Test]
        public void DoorInstallationSuppressesJunctionConnectorAndRestorationRebuildsIt()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallVisual", privateInstance);
            MethodInfo refresh = typeof(WallConstructionController).GetMethod("RefreshFinalJunctionVisuals", privateInstance);
            MethodInfo restore = typeof(WallConstructionController).GetMethod("RestoreWallFromDoor", privateInstance);
            var visuals = (Dictionary<WallEdge, GameObject>)typeof(WallConstructionController)
                .GetField("visuals", privateInstance).GetValue(controller);
            var joints = (Dictionary<Vector2Int, GameObject>)typeof(WallConstructionController)
                .GetField("jointVisuals", privateInstance).GetValue(controller);
            WallEdge west = new(4, 5, true);
            WallEdge[] branches =
            {
                west,
                new(5, 5, true),
                new(5, 5, false)
            };
            Vector2Int vertex = new(5, 5);
            Assert.That(walls.TryAddRange(branches), Is.True);
            foreach (WallEdge edge in branches)
            {
                visuals.Add(edge, (GameObject)create.Invoke(controller, new object[] { edge, 0f, null, null, false }));
            }
            refresh.Invoke(controller, new object[] { branches, 0f });
            Assert.That(joints.ContainsKey(vertex), Is.True);

            Assert.That(walls.TryInstallDoor(west), Is.True);
            Object.DestroyImmediate(visuals[west]);
            visuals.Remove(west);
            refresh.Invoke(controller, new object[] { new[] { west }, 0f });
            Assert.That(joints.ContainsKey(vertex), Is.False);

            restore.Invoke(controller, new object[] { west, 0f });
            Assert.That(walls.IsDoor(west), Is.False);
            Assert.That(joints.ContainsKey(vertex), Is.True);
            Assert.That(walls.Contains(branches[1]) && walls.Contains(branches[2]), Is.True);
        }

        [Test]
        public void ExternalGridVertexSupportsThreeWayDiagonalAndOrthogonalJunction()
        {
            WallEdge[] branches =
            {
                new(0, 0, true),
                new(0, 0, false),
                new(0, 0, WallEdgeOrientation.DiagonalAscending)
            };
            Assert.That(walls.TryAddRange(branches), Is.True);
            Assert.That(walls.GetConnectionCountAtVertex(Vector2Int.zero), Is.EqualTo(3));

            List<Vector2Int> junctions = new();
            walls.GetAffectedJunctionVertices(new[] { branches[2] }, junctions);
            CollectionAssert.Contains(junctions, Vector2Int.zero);
        }

        [Test]
        public void DiagonalRemovalRebuildsConnectorGeometryForTheRemainingThreeBranches()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallVisual", privateInstance);
            MethodInfo refresh = typeof(WallConstructionController).GetMethod("RefreshFinalJunctionVisuals", privateInstance);
            var visuals = (Dictionary<WallEdge, GameObject>)typeof(WallConstructionController)
                .GetField("visuals", privateInstance).GetValue(controller);
            var joints = (Dictionary<Vector2Int, GameObject>)typeof(WallConstructionController)
                .GetField("jointVisuals", privateInstance).GetValue(controller);
            WallEdge[] branches =
            {
                new(4, 5, true),
                new(5, 5, false),
                new(5, 5, WallEdgeOrientation.DiagonalAscending),
                new(5, 4, WallEdgeOrientation.DiagonalDescending)
            };
            Vector2Int vertex = new(5, 5);

            Assert.That(walls.TryAddRange(branches), Is.True);
            foreach (WallEdge edge in branches)
            {
                visuals.Add(edge, (GameObject)create.Invoke(controller, new object[] { edge, 0f, null, null, false }));
            }
            refresh.Invoke(controller, new object[] { branches, 0f });
            Mesh previousMesh = joints[vertex].GetComponent<MeshFilter>().sharedMesh;

            Assert.That(walls.Remove(branches[3]), Is.True);
            refresh.Invoke(controller, new object[] { new[] { branches[3] }, 0f });

            Assert.That(joints.ContainsKey(vertex), Is.True);
            Mesh updatedMesh = joints[vertex].GetComponent<MeshFilter>().sharedMesh;
            Assert.That(updatedMesh, Is.Not.SameAs(previousMesh));
            Assert.That(joints[vertex].GetComponent<MeshCollider>().sharedMesh, Is.SameAs(updatedMesh));
            Assert.That(walls.GetConnectionCountAtVertex(vertex), Is.EqualTo(3));
        }

        [Test]
        public void DiagonalRemovalRebuildsTheAffectedConnectorAndPreservesNeighborWalls()
        {
            WallConstructionController controller = owner.AddComponent<WallConstructionController>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WallConstructionController).GetField("grid", privateInstance).SetValue(controller, grid);
            typeof(WallConstructionController).GetField("edges", privateInstance).SetValue(controller, walls);
            MethodInfo create = typeof(WallConstructionController).GetMethod("CreateWallVisual", privateInstance);
            MethodInfo refresh = typeof(WallConstructionController).GetMethod("RefreshFinalJunctionVisuals", privateInstance);
            var visuals = (Dictionary<WallEdge, GameObject>)typeof(WallConstructionController)
                .GetField("visuals", privateInstance).GetValue(controller);
            var joints = (Dictionary<Vector2Int, GameObject>)typeof(WallConstructionController)
                .GetField("jointVisuals", privateInstance).GetValue(controller);
            WallEdge west = new(4, 5, true);
            WallEdge north = new(5, 5, false);
            WallEdge diagonal = new(5, 5, WallEdgeOrientation.DiagonalAscending);
            Vector2Int vertex = new(5, 5);
            WallEdge[] connected = { west, north, diagonal };

            Assert.That(walls.TryAddRange(connected), Is.True);
            foreach (WallEdge edge in connected)
            {
                visuals.Add(edge, (GameObject)create.Invoke(controller, new object[] { edge, 0f, null, null, false }));
            }
            refresh.Invoke(controller, new object[] { connected, 0f });
            GameObject originalConnector = joints[vertex];

            Assert.That(walls.Remove(diagonal), Is.True);
            refresh.Invoke(controller, new object[] { new[] { diagonal }, 0f });

            Assert.That(joints.ContainsKey(vertex), Is.False);
            Assert.That(walls.Contains(west) && walls.Contains(north), Is.True);
            Assert.That(visuals.ContainsKey(west) && visuals.ContainsKey(north), Is.True);
            Assert.That(originalConnector == null, Is.True);
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

        private Vector3 GetExpectedGridVertex(Vector2Int vertex, float height)
        {
            Vector3 gridMinimum = grid.transform.position
                - new Vector3(grid.Width * grid.CellSize * 0.5f, 0f, grid.Height * grid.CellSize * 0.5f);
            return new Vector3(
                gridMinimum.x + vertex.x * grid.CellSize,
                height,
                gridMinimum.z + vertex.y * grid.CellSize);
        }

        private void ExpectedEndpoints(WallEdge edge, float height, out Vector3 start, out Vector3 end)
        {
            if (edge.Orientation == WallEdgeOrientation.DiagonalAscending)
            {
                start = GetExpectedGridVertex(new Vector2Int(edge.X, edge.Z), height);
                end = GetExpectedGridVertex(new Vector2Int(edge.X + 1, edge.Z + 1), height);
            }
            else if (edge.Orientation == WallEdgeOrientation.DiagonalDescending)
            {
                start = GetExpectedGridVertex(new Vector2Int(edge.X, edge.Z + 1), height);
                end = GetExpectedGridVertex(new Vector2Int(edge.X + 1, edge.Z), height);
            }
            else if (edge.AlongX)
            {
                start = GetExpectedGridVertex(new Vector2Int(edge.X, edge.Z), height);
                end = GetExpectedGridVertex(new Vector2Int(edge.X + 1, edge.Z), height);
            }
            else
            {
                start = GetExpectedGridVertex(new Vector2Int(edge.X, edge.Z), height);
                end = GetExpectedGridVertex(new Vector2Int(edge.X, edge.Z + 1), height);
            }
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
