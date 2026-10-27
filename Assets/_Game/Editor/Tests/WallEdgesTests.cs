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
    }
}
