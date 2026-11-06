using System.Linq;
using MaterialCityTycoon.Building;
using NUnit.Framework;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class PalletBagDisplayLayoutTests
    {
        private static readonly Bounds CementNominalBounds = new(Vector3.zero, new Vector3(0.60f, 0.18f, 0.40f));
        private static readonly Bounds CementVisualBounds = new(Vector3.zero, new Vector3(0.57f, 0.18f, 0.38f));
        private static readonly Bounds LongitudinalZBounds = new(Vector3.zero, new Vector3(0.40f, 0.12f, 0.60f));

        [Test]
        public void EightLayersUseFiveBagsAndAlternateTheTiedABPattern()
        {
            Assert.That(TryCreate(CementNominalBounds, out var placements, out string reason), Is.True, reason);
            Assert.That(placements, Has.Count.EqualTo(40));

            for (int layer = 0; layer < PalletBagDisplayLayout.LayerCount; layer++)
            {
                var bags = placements.Where(placement => placement.LayerIndex == layer).ToArray();
                Assert.That(bags, Has.Length.EqualTo(PalletBagDisplayLayout.BagsPerLayer));
                Assert.That(bags.Select(placement => placement.IndexInLayer), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));

                bool pairsFirst = layer % 2 == 1;
                Assert.That(bags.Take(pairsFirst ? 2 : 3).Select(placement => placement.QuarterTurns),
                    Is.All.EqualTo(pairsFirst ? 0 : 1));
                Assert.That(bags.Skip(pairsFirst ? 2 : 3).Select(placement => placement.QuarterTurns),
                    Is.All.EqualTo(pairsFirst ? 1 : 0));
            }
        }

        [Test]
        public void FirstTwoLayersUseTheConfiguredThreePlusTwoAndTwoPlusThreeRowPositions()
        {
            Assert.That(TryCreate(CementNominalBounds, out var placements, out string reason), Is.True, reason);

            AssertPlacement(placements[0], -0.40f, -0.20f, 1);
            AssertPlacement(placements[1], 0.00f, -0.20f, 1);
            AssertPlacement(placements[2], 0.40f, -0.20f, 1);
            AssertPlacement(placements[3], -0.30f, 0.30f, 0);
            AssertPlacement(placements[4], 0.30f, 0.30f, 0);

            AssertPlacement(placements[5], -0.30f, -0.30f, 0);
            AssertPlacement(placements[6], 0.30f, -0.30f, 0);
            AssertPlacement(placements[7], -0.40f, 0.20f, 1);
            AssertPlacement(placements[8], 0.00f, 0.20f, 1);
            AssertPlacement(placements[9], 0.40f, 0.20f, 1);
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(7)]
        [TestCase(20)]
        [TestCase(39)]
        [TestCase(40)]
        public void PartialQuantitiesUseTheFirstPlacementsAndRemovalUsesTheReverseOrder(int quantity)
        {
            Assert.That(TryCreate(CementNominalBounds, out var placements, out string reason), Is.True, reason);
            var visible = placements.Take(quantity).ToArray();

            Assert.That(visible, Has.Length.EqualTo(quantity));
            Assert.That(visible[^1], Is.EqualTo(placements[quantity - 1]));
            if (quantity < placements.Count)
            {
                Assert.That(visible.Contains(placements[quantity]), Is.False);
            }
        }

        [Test]
        public void BothTiedLayersFitTheConfiguredPalletSurface()
        {
            Assert.That(TryCreate(CementNominalBounds, out var placements, out string reason), Is.True, reason);
            Assert.That(placements, Has.Count.EqualTo(PalletBagDisplayLayout.Capacity));

            foreach (PalletBagPlacement placement in placements)
            {
                Assert.That(GetFootprint(CementNominalBounds, placement).min.x, Is.GreaterThanOrEqualTo(-0.6001f));
                Assert.That(GetFootprint(CementNominalBounds, placement).max.x, Is.LessThanOrEqualTo(0.6001f));
                Assert.That(GetFootprint(CementNominalBounds, placement).min.z, Is.GreaterThanOrEqualTo(-0.5001f));
                Assert.That(GetFootprint(CementNominalBounds, placement).max.z, Is.LessThanOrEqualTo(0.5001f));
            }
        }

        [Test]
        public void ApproximateCementVisualBoundsFitWithoutAFalsePhysicalIncompatibility()
        {
            Assert.That(TryCreate(CementVisualBounds, out var placements, out string reason), Is.True, reason);
            Assert.That(placements, Has.Count.EqualTo(PalletBagDisplayLayout.Capacity));
            Assert.That(placements.Take(3).Select(placement => placement.QuarterTurns), Is.All.EqualTo(1));
            Assert.That(placements.Skip(3).Take(2).Select(placement => placement.QuarterTurns), Is.All.EqualTo(0));
        }

        [Test]
        public void LongitudinalZInputRemainsSupported()
        {
            Assert.That(TryCreate(LongitudinalZBounds, out var placements, out string reason), Is.True, reason);
            Assert.That(placements.Take(3).Select(placement => placement.QuarterTurns), Is.All.EqualTo(0));
            Assert.That(placements.Skip(3).Take(2).Select(placement => placement.QuarterTurns), Is.All.EqualTo(1));
        }

        [Test]
        public void LayoutUsesTheConfiguredSurfaceHeightAndActualBagHeightWithABasePivot()
        {
            Bounds basePivotBounds = new(new Vector3(0f, 0.09f, 0f), CementNominalBounds.size);
            Assert.That(TryCreate(basePivotBounds, out var placements, out string reason), Is.True, reason);
            Assert.That(placements[0].LocalPosition.y, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(placements[5].LocalPosition.y - placements[0].LocalPosition.y,
                Is.EqualTo(CementNominalBounds.size.y).Within(0.0001f));
        }

        [Test]
        public void BagsInEachLayerDoNotOverlapOnThePalletPlane()
        {
            Assert.That(TryCreate(CementNominalBounds, out var placements, out string reason), Is.True, reason);

            foreach (var layer in placements.GroupBy(placement => placement.LayerIndex))
            {
                PalletBagPlacement[] bags = layer.ToArray();
                for (int first = 0; first < bags.Length; first++)
                {
                    for (int second = first + 1; second < bags.Length; second++)
                    {
                        Assert.That(OverlapsOnPlane(
                                GetFootprint(CementNominalBounds, bags[first]),
                                GetFootprint(CementNominalBounds, bags[second])),
                            Is.False,
                            $"Layer {layer.Key} has overlapping bags {first} and {second}.");
                    }
                }
            }
        }

        private static Bounds GetFootprint(Bounds bagBounds, PalletBagPlacement placement)
        {
            bool isQuarterTurn = placement.QuarterTurns % 2 != 0;
            Vector3 size = isQuarterTurn
                ? new Vector3(bagBounds.size.z, bagBounds.size.y, bagBounds.size.x)
                : bagBounds.size;
            Vector3 center = isQuarterTurn
                ? new Vector3(-bagBounds.center.z, bagBounds.center.y, bagBounds.center.x)
                : bagBounds.center;
            return new Bounds(center + placement.LocalPosition, size);
        }

        private static bool OverlapsOnPlane(Bounds first, Bounds second)
        {
            const float epsilon = 0.0001f;
            return first.min.x < second.max.x - epsilon
                && first.max.x > second.min.x + epsilon
                && first.min.z < second.max.z - epsilon
                && first.max.z > second.min.z + epsilon;
        }

        private static void AssertPlacement(PalletBagPlacement placement, float x, float z, int quarterTurns)
        {
            Assert.That(placement.LocalPosition.x, Is.EqualTo(x).Within(0.0001f));
            Assert.That(placement.LocalPosition.z, Is.EqualTo(z).Within(0.0001f));
            Assert.That(placement.QuarterTurns, Is.EqualTo(quarterTurns));
        }

        private static bool TryCreate(Bounds bagBounds, out System.Collections.Generic.List<PalletBagPlacement> placements, out string reason)
        {
            return PalletBagDisplayLayout.TryCreatePlacements(
                bagBounds,
                new PalletBagDisplayLayoutDefinition(new Vector2(1.20f, 1.00f), 0.15f, 0f),
                out placements,
                out reason);
        }
    }
}
