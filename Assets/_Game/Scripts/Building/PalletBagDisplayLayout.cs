using System.Collections.Generic;
using UnityEngine;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Calculates the tied A/B stack for five bags per layer without creating any Unity objects.
    /// </summary>
    public static class PalletBagDisplayLayout
    {
        public const int BagsPerLayer = 5;
        public const int LayerCount = 8;
        public const int Capacity = BagsPerLayer * LayerCount;

        public static bool TryCreatePlacements(
            Bounds productBounds,
            PalletBagDisplayLayoutDefinition settings,
            out List<PalletBagPlacement> placements,
            out string failureReason)
        {
            placements = new List<PalletBagPlacement>(Capacity);
            failureReason = null;
            if (settings == null || !settings.IsConfigured)
            {
                failureReason = "The pallet bag layout surface is not configured.";
                return false;
            }

            if (productBounds.size.x <= 0f || productBounds.size.y <= 0f || productBounds.size.z <= 0f)
            {
                failureReason = "The product visual has invalid bounds.";
                return false;
            }

            List<PalletBagPlacement> firstLayer = CreateLayer(productBounds, settings, pairsFirst: false);
            if (!FitsSurface(firstLayer, productBounds, settings.SurfaceSize))
            {
                failureReason = "Layer A does not fit on the configured pallet surface.";
                return false;
            }

            List<PalletBagPlacement> secondLayer = CreateLayer(productBounds, settings, pairsFirst: true);
            if (!FitsSurface(secondLayer, productBounds, settings.SurfaceSize))
            {
                failureReason = "Layer B does not fit on the configured pallet surface.";
                return false;
            }

            float layerStep = productBounds.size.y + settings.Spacing;
            for (int layer = 0; layer < LayerCount; layer++)
            {
                List<PalletBagPlacement> source = layer % 2 == 0 ? firstLayer : secondLayer;
                for (int index = 0; index < BagsPerLayer; index++)
                {
                    PalletBagPlacement placement = source[index];
                    placement.LayerIndex = layer;
                    placement.IndexInLayer = index;
                    placement.LocalPosition += Vector3.up * (settings.SurfaceHeight + layer * layerStep - productBounds.min.y);
                    placements.Add(placement);
                }
            }

            return true;
        }

        private static List<PalletBagPlacement> CreateLayer(
            Bounds productBounds,
            PalletBagDisplayLayoutDefinition settings,
            bool pairsFirst)
        {
            Bounds turnedBounds = RotateBounds(productBounds, 1);
            float gap = settings.Spacing;
            List<PalletBagPlacement> result = new(BagsPerLayer);

            SelectRowOrientations(
                productBounds,
                turnedBounds,
                settings.SurfaceSize,
                gap,
                out Bounds threeBagBounds,
                out int threeBagQuarterTurns,
                out Bounds twoBagBounds,
                out int twoBagQuarterTurns);

            float totalDepth = threeBagBounds.size.z + twoBagBounds.size.z + gap;
            Bounds firstRowBounds = pairsFirst ? twoBagBounds : threeBagBounds;
            int firstRowCount = pairsFirst ? 2 : 3;
            Bounds secondRowBounds = pairsFirst ? threeBagBounds : twoBagBounds;
            int secondRowCount = pairsFirst ? 3 : 2;
            float firstRowZ = -totalDepth * 0.5f + firstRowBounds.size.z * 0.5f;
            float secondRowZ = totalDepth * 0.5f - secondRowBounds.size.z * 0.5f;

            AddRow(
                result,
                firstRowBounds,
                firstRowCount,
                firstRowZ,
                pairsFirst ? twoBagQuarterTurns : threeBagQuarterTurns,
                gap);
            AddRow(
                result,
                secondRowBounds,
                secondRowCount,
                secondRowZ,
                pairsFirst ? threeBagQuarterTurns : twoBagQuarterTurns,
                gap);

            return result;
        }

        private static void SelectRowOrientations(
            Bounds originalBounds,
            Bounds turnedBounds,
            Vector2 surfaceSize,
            float gap,
            out Bounds threeBagBounds,
            out int threeBagQuarterTurns,
            out Bounds twoBagBounds,
            out int twoBagQuarterTurns)
        {
            if (FitsRowArrangement(originalBounds, 3, turnedBounds, 2, surfaceSize, gap))
            {
                threeBagBounds = originalBounds;
                threeBagQuarterTurns = 0;
                twoBagBounds = turnedBounds;
                twoBagQuarterTurns = 1;
                return;
            }

            threeBagBounds = turnedBounds;
            threeBagQuarterTurns = 1;
            twoBagBounds = originalBounds;
            twoBagQuarterTurns = 0;
        }

        private static bool FitsRowArrangement(
            Bounds firstRowBounds,
            int firstRowCount,
            Bounds secondRowBounds,
            int secondRowCount,
            Vector2 surfaceSize,
            float gap)
        {
            float firstRowWidth = firstRowCount * firstRowBounds.size.x + (firstRowCount - 1) * gap;
            float secondRowWidth = secondRowCount * secondRowBounds.size.x + (secondRowCount - 1) * gap;
            float totalDepth = firstRowBounds.size.z + secondRowBounds.size.z + gap;
            return firstRowWidth <= surfaceSize.x
                && secondRowWidth <= surfaceSize.x
                && totalDepth <= surfaceSize.y;
        }

        private static void AddRow(
            ICollection<PalletBagPlacement> placements,
            Bounds bounds,
            int count,
            float z,
            int quarterTurns,
            float gap)
        {
            for (int index = 0; index < count; index++)
            {
                float x = (index - (count - 1) * 0.5f) * (bounds.size.x + gap);
                placements.Add(new PalletBagPlacement(
                    new Vector3(x - bounds.center.x, 0f, z - bounds.center.z), quarterTurns));
            }
        }

        private static bool FitsSurface(IReadOnlyList<PalletBagPlacement> placements, Bounds productBounds, Vector2 surfaceSize)
        {
            float halfWidth = surfaceSize.x * 0.5f;
            float halfDepth = surfaceSize.y * 0.5f;
            foreach (PalletBagPlacement placement in placements)
            {
                Bounds bounds = RotateBounds(productBounds, placement.QuarterTurns);
                bounds.center += placement.LocalPosition;
                if (bounds.min.x < -halfWidth || bounds.max.x > halfWidth
                    || bounds.min.z < -halfDepth || bounds.max.z > halfDepth)
                {
                    return false;
                }
            }

            return true;
        }

        private static Bounds RotateBounds(Bounds source, int quarterTurns)
        {
            int normalizedTurns = ((quarterTurns % 4) + 4) % 4;
            if (normalizedTurns % 2 == 0)
            {
                return source;
            }

            return new Bounds(
                new Vector3(-source.center.z, source.center.y, source.center.x),
                new Vector3(source.size.z, source.size.y, source.size.x));
        }
    }

    public struct PalletBagPlacement
    {
        public PalletBagPlacement(Vector3 localPosition, int quarterTurns)
        {
            LocalPosition = localPosition;
            QuarterTurns = quarterTurns;
            LayerIndex = 0;
            IndexInLayer = 0;
        }

        public Vector3 LocalPosition;
        public int QuarterTurns;
        public int LayerIndex;
        public int IndexInLayer;
    }
}
