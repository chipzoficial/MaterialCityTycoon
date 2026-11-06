using System.Collections.Generic;
using System.Reflection;
using MaterialCityTycoon.Building;
using NUnit.Framework;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class FloorConstructionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject owner;
        private ConstructionGrid grid;
        private FloorConstructionController controller;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("floor test grid");
            grid = owner.AddComponent<ConstructionGrid>();
            controller = owner.AddComponent<FloorConstructionController>();
            BoxCollider ground = owner.AddComponent<BoxCollider>();
            ground.center = Vector3.down * 0.05f;
            ground.size = new Vector3(16f, 0.1f, 20f);
            typeof(FloorConstructionController).GetField("grid", Private).SetValue(controller, grid);
            typeof(FloorConstructionController).GetField("ground", Private).SetValue(controller, ground);
            typeof(FloorConstructionController).GetField("inputCamera", Private).SetValue(controller, owner.AddComponent<UnityEngine.Camera>());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(owner);

        [Test]
        public void FloorCellsRegisterTypeRejectDuplicatesAndKeepOperationsAtomic()
        {
            FloorCells cells = new();
            Vector2Int first = new(2, 3);
            Vector2Int second = new(3, 3);
            Assert.That(cells.TryAddRange(new[] { first, second }), Is.True);
            Assert.That(cells.Count, Is.EqualTo(2));
            Assert.That(cells.TryGetFloorType(first, out string type), Is.True);
            Assert.That(type, Is.EqualTo(FloorCells.ProvisionalFloorTypeId));
            Assert.That(cells.TryAddRange(new[] { second, new Vector2Int(4, 3) }), Is.False);
            Assert.That(cells.Count, Is.EqualTo(2));
            Assert.That(cells.TryRemoveRange(new[] { first, new Vector2Int(8, 8) }), Is.False);
            Assert.That(cells.Contains(first), Is.True);
            Assert.That(cells.Contains(second), Is.True);
        }

        [TestCase(2, 3, 5, 5)]
        [TestCase(5, 5, 2, 3)]
        [TestCase(5, 3, 2, 5)]
        [TestCase(2, 5, 5, 3)]
        public void RectangleSelectionIncludesEveryCellInAllDragDirections(int firstX, int firstZ, int secondX, int secondZ)
        {
            List<Vector2Int> result = new();
            FloorCells.GetRectangle(new Vector2Int(firstX, firstZ), new Vector2Int(secondX, secondZ), result);
            Assert.That(result, Has.Count.EqualTo(12));
            CollectionAssert.Contains(result, new Vector2Int(2, 3));
            CollectionAssert.Contains(result, new Vector2Int(5, 5));
        }

        [Test]
        public void ControllerBuildsAndRemovesRectanglesWithoutCollidersOrGridOccupancySideEffects()
        {
            List<Vector2Int> rectangle = new();
            FloorCells.GetRectangle(new Vector2Int(3, 4), new Vector2Int(6, 6), rectangle);
            Assert.That(controller.TryBuildFloors(rectangle), Is.True);
            Assert.That(controller.Floors.Count, Is.EqualTo(12));
            Assert.That(grid.IsInside(new Vector2Int(3, 4)), Is.True);

            var visuals = (Dictionary<Vector2Int, GameObject>)typeof(FloorConstructionController)
                .GetField("visuals", Private).GetValue(controller);
            Assert.That(visuals, Has.Count.EqualTo(12));
            foreach (GameObject visual in visuals.Values)
            {
                Assert.That(visual.GetComponent<Collider>(), Is.Null);
                Assert.That(visual.transform.localScale.x, Is.EqualTo(1f));
                Assert.That(visual.transform.localScale.z, Is.EqualTo(1f));
            }

            Assert.That(controller.TryRemoveFloors(rectangle), Is.True);
            Assert.That(controller.Floors.Count, Is.Zero);
            Assert.That(controller.TryBuildFloors(rectangle), Is.True);
        }

        [Test]
        public void ControllerRejectsOutOfBoundsAndPartiallyOccupiedAreasWithoutChangingState()
        {
            Vector2Int occupied = new(1, 1);
            Assert.That(controller.TryBuildFloors(new[] { occupied }), Is.True);
            Assert.That(controller.TryBuildFloors(new[] { occupied, new Vector2Int(2, 1) }), Is.False);
            Assert.That(controller.Floors.Count, Is.EqualTo(1));
            Assert.That(controller.TryBuildFloors(new[] { new Vector2Int(-1, 1) }), Is.False);
            Assert.That(controller.Floors.Count, Is.EqualTo(1));
        }

        [Test]
        public void VisualCellsAreCenteredSlightlyAboveTheGroundSurface()
        {
            Vector2Int cell = new(7, 9);
            Assert.That(controller.TryBuildFloors(new[] { cell }), Is.True);
            var visuals = (Dictionary<Vector2Int, GameObject>)typeof(FloorConstructionController)
                .GetField("visuals", Private).GetValue(controller);
            GameObject visual = visuals[cell];
            Vector3 expectedCenter = grid.GetCellCenter(cell, visual.transform.position.y);
            Assert.That(visual.transform.position.x, Is.EqualTo(expectedCenter.x).Within(0.0001f));
            Assert.That(visual.transform.position.z, Is.EqualTo(expectedCenter.z).Within(0.0001f));
            Assert.That(visual.transform.position.y, Is.GreaterThan(0f));
        }
    }
}
