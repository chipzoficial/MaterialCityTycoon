using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;
using UnityEngine;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Displays lightweight product units at occupied non-shelf display slots.
    /// Quantities stay in the inventory; this component never changes them.
    /// </summary>
    public sealed class DisplaySlotProductVisualPresenter : MonoBehaviour
    {
        private readonly Dictionary<string, SlotVisual> visualsBySlotId = new(StringComparer.Ordinal);
        private FurniturePlacementController placementController;
        private string furnitureId;

        public void Initialize(
            FurniturePlacementController controller,
            string placedFurnitureId,
            FurnitureDefinition definition)
        {
            if (placementController != null || controller == null || string.IsNullOrWhiteSpace(placedFurnitureId)
                || definition == null)
            {
                return;
            }

            placementController = controller;
            furnitureId = placedFurnitureId;
            foreach (DisplaySlotDefinition slot in definition.DisplaySlots)
            {
                if (slot != null && !string.IsNullOrWhiteSpace(slot.SlotId))
                {
                    visualsBySlotId[slot.SlotId] = new SlotVisual(definition.FurnitureTypeId, slot);
                }
            }
        }

        private void Update()
        {
            if (placementController == null || !placementController.ContainsFurniture(furnitureId))
            {
                return;
            }

            foreach (KeyValuePair<string, SlotVisual> pair in visualsBySlotId)
            {
                Sync(pair.Key, pair.Value);
            }
        }

        private void Sync(string slotId, SlotVisual visual)
        {
            bool hasProduct = placementController.TryGetAssignedProductForDisplaySlot(
                    furnitureId, slotId, out ProductDefinition product)
                && placementController.GetDisplayedQuantity(furnitureId, slotId) > 0;
            GameObject desiredPrefab = hasProduct ? product.DisplayPrefab : null;
            int quantity = hasProduct ? placementController.GetDisplayedQuantity(furnitureId, slotId) : 0;

            if (visual.Prefab != desiredPrefab)
            {
                ClearInstances(visual);
                visual.Prefab = desiredPrefab;
                visual.Positions.Clear();
                visual.LayoutWarningLogged = false;
                if (desiredPrefab != null)
                {
                    if (!BuildPositions(visual, desiredPrefab, out string failureReason) && !visual.LayoutWarningLogged)
                    {
                        Debug.LogWarning($"Cannot display '{desiredPrefab.name}' in slot '{slotId}': {failureReason}", this);
                        visual.LayoutWarningLogged = true;
                    }
                }
            }

            if (desiredPrefab == null)
            {
                return;
            }

            int targetCount = Mathf.Min(quantity, visual.Positions.Count);
            while (visual.Instances.Count > targetCount)
            {
                int last = visual.Instances.Count - 1;
                Destroy(visual.Instances[last]);
                visual.Instances.RemoveAt(last);
            }

            while (visual.Instances.Count < targetCount)
            {
                GameObject instance = Instantiate(desiredPrefab, transform, false);
                instance.name = $"Display_{slotId}";
                ProductVisualPlacement placement = visual.Positions[visual.Instances.Count];
                instance.transform.localPosition = placement.LocalPosition;
                instance.transform.localRotation = Quaternion.Euler(0f, placement.QuarterTurns * 90f, 0f)
                    * desiredPrefab.transform.localRotation;
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
                visual.Instances.Add(instance);
            }
        }

        private static bool BuildPositions(SlotVisual visual, GameObject prefab, out string failureReason)
        {
            failureReason = null;
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                failureReason = "The product prefab has no MeshRenderer.";
                return false;
            }

            Bounds bounds = GetBoundsInRootSpace(prefab.transform, renderers[0]);
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(GetBoundsInRootSpace(prefab.transform, renderers[index]));
            if (bounds.size.x <= 0f || bounds.size.y <= 0f || bounds.size.z <= 0f)
            {
                failureReason = "The product prefab has empty bounds.";
                return false;
            }

            if (visual.IsPalletBagSlot)
            {
                if (!PalletBagDisplayLayout.TryCreatePlacements(bounds, visual.PalletLayout, out List<PalletBagPlacement> palletPlacements, out failureReason))
                {
                    return false;
                }

                foreach (PalletBagPlacement placement in palletPlacements)
                {
                    visual.Positions.Add(new ProductVisualPlacement(
                        visual.LocalPosition + placement.LocalPosition,
                        placement.QuarterTurns));
                }

                return true;
            }

            int maximum = visual.Capacity > 0 ? visual.Capacity : 1;
            int columns = maximum >= 12 ? 4 : Mathf.Min(maximum, 4);
            int rows = maximum >= 12 ? 3 : 1;
            int perLayer = columns * rows;
            const float spacing = 0.015f;
            for (int index = 0; index < maximum; index++)
            {
                int layer = index / perLayer;
                int inLayer = index % perLayer;
                int column = inLayer % columns;
                int row = inLayer / columns;
                Vector3 position = visual.LocalPosition + new Vector3(
                    (column - (columns - 1) * 0.5f) * (bounds.size.x + spacing) - bounds.center.x,
                    layer * (bounds.size.y + spacing) - bounds.min.y,
                    (row - (rows - 1) * 0.5f) * (bounds.size.z + spacing) - bounds.center.z);
                visual.Positions.Add(new ProductVisualPlacement(position, 0));
            }

            return true;
        }

        private static Bounds GetBoundsInRootSpace(Transform root, MeshRenderer renderer)
        {
            Bounds source = renderer.localBounds;
            Matrix4x4 toRoot = root.worldToLocalMatrix * renderer.localToWorldMatrix;
            Bounds result = new(toRoot.MultiplyPoint3x4(source.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        result.Encapsulate(toRoot.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents, new Vector3(x, y, z))));
            return result;
        }

        private static void ClearInstances(SlotVisual visual)
        {
            foreach (GameObject instance in visual.Instances)
                if (instance != null) Destroy(instance);
            visual.Instances.Clear();
        }

        private sealed class SlotVisual
        {
            public SlotVisual(string furnitureTypeId, DisplaySlotDefinition slot)
            {
                FurnitureTypeId = furnitureTypeId;
                SlotId = slot.SlotId;
                LocalPosition = slot.LocalDisplayPosition;
                Capacity = slot.CapacityUnits;
                PalletLayout = slot.PalletBagDisplayLayout;
            }

            public readonly string FurnitureTypeId;
            public readonly string SlotId;
            public readonly Vector3 LocalPosition;
            public readonly int Capacity;
            public readonly PalletBagDisplayLayoutDefinition PalletLayout;
            public bool IsPalletBagSlot => FurnitureTypeId == FurnitureTypeIds.PalletBagDisplay && SlotId == "pallet_bags";
            public GameObject Prefab;
            public bool LayoutWarningLogged;
            public readonly List<ProductVisualPlacement> Positions = new();
            public readonly List<GameObject> Instances = new();
        }

        private readonly struct ProductVisualPlacement
        {
            public ProductVisualPlacement(Vector3 localPosition, int quarterTurns)
            {
                LocalPosition = localPosition;
                QuarterTurns = quarterTurns;
            }

            public Vector3 LocalPosition { get; }
            public int QuarterTurns { get; }
        }
    }
}
