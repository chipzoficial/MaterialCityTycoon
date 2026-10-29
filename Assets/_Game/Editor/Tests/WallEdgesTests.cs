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
            Assert.That(wall.transform.position.y, Is.EqualTo(1.5f));
            Assert.That(wall.GetComponent<Collider>().enabled, Is.True);
            Physics.SyncTransforms();
            Assert.That(wall.GetComponent<Collider>().bounds.size.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(preview.transform.localScale.y, Is.EqualTo(3f));
            Assert.That(preview.transform.position.y, Is.EqualTo(1.5f));
            Assert.That(preview.GetComponent<Collider>().enabled, Is.False);
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
    }
}
