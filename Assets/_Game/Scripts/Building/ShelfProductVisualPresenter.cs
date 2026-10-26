using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;
using UnityEngine;

namespace MaterialCityTycoon.Building
{
    /// <summary>Shows the mortar units exposed by one constructed commercial shelf.</summary>
    public sealed class ShelfProductVisualPresenter : MonoBehaviour
    {
        public static bool VerboseDiagnostics { get; set; }
        private const float GeometryTolerance = 0.0001f;
        private const float AnchorTolerance = 0.002f;
        private const int StackCount = 3;
        private const int StackLayers = 4;
        private readonly List<SlotVisuals> slots = new();
        private readonly List<MeshRenderer> shelfRenderers = new();
        private readonly List<Bounds> shelfBounds = new();
        private FurniturePlacementController placementController;
        private string furnitureId;

        public void Initialize(
            FurniturePlacementController controller,
            string placedFurnitureId,
            FurnitureDefinition definition)
        {
            placementController = controller;
            furnitureId = placedFurnitureId;
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                shelfRenderers.Add(renderer);
                shelfBounds.Add(GetBoundsInRootSpace(transform, renderer));
            }

            foreach (DisplaySlotDefinition slot in definition.DisplaySlots)
            {
                string trayName = slot.SlotId switch
                {
                    "shelf_bottom" => "Shelf_01",
                    "shelf_middle" => "Shelf_02",
                    "shelf_top" => "Shelf_03",
                    _ => null
                };
                Transform tray = trayName == null ? null : transform.Find(trayName);
                MeshRenderer trayRenderer = tray == null ? null : tray.GetComponent<MeshRenderer>();
                if (trayRenderer == null)
                {
                    Debug.LogWarning($"Cannot show products for shelf slot '{slot.SlotId}': tray not found.", this);
                    continue;
                }

                slots.Add(new SlotVisuals(slot, trayRenderer, GetBoundsInRootSpace(transform, trayRenderer)));
            }
        }

        private void Update()
        {
            // A copied placement preview has no initialized controller and must not create products.
            if (placementController == null || !placementController.ContainsFurniture(furnitureId))
            {
                return;
            }

            foreach (SlotVisuals slot in slots)
            {
                UpdateSlot(slot);
            }
        }

        private void UpdateSlot(SlotVisuals slot)
        {
            bool hasMortar = placementController.TryGetAssignedProductForDisplaySlot(
                    furnitureId, slot.definition.SlotId, out ProductDefinition product)
                && product.ProductId == "argamassa";
            GameObject prefab = hasMortar ? product.DisplayPrefab : null;
            int quantity = hasMortar
                ? placementController.GetDisplayedQuantity(furnitureId, slot.definition.SlotId)
                : 0;

            if (slot.prefab != prefab)
            {
                ClearInstances(slot);
                slot.prefab = prefab;
                slot.positions.Clear();
                slot.excessWarningShown = false;
                if (prefab != null)
                {
                    BuildPositions(slot, prefab);
                }
            }

            if (quantity > 0 && prefab == null && !slot.missingPrefabWarningShown)
            {
                Debug.LogWarning("Argamassa has no display prefab. Run Tools > Material City Tycoon > Configure Mortar Display Visual.", this);
                slot.missingPrefabWarningShown = true;
            }
            else if (quantity == 0 || prefab != null)
            {
                slot.missingPrefabWarningShown = false;
            }

            int targetCount = Mathf.Min(quantity, slot.positions.Count);
            while (slot.instances.Count > targetCount)
            {
                int last = slot.instances.Count - 1;
                Destroy(slot.instances[last]);
                slot.instances.RemoveAt(last);
            }

            while (slot.instances.Count < targetCount)
            {
                if (slot.container == null)
                {
                    slot.container = new GameObject($"Display_{slot.definition.SlotId}").transform;
                    slot.container.SetParent(transform, false);
                }

                GameObject instance = Instantiate(prefab, slot.container, false);
                instance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * prefab.transform.localRotation;
                instance.transform.localPosition = slot.positions[slot.instances.Count];
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }

                foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.isKinematic = true;
                }

                slot.instances.Add(instance);
            }

            if (prefab != null && quantity > slot.positions.Count && !slot.excessWarningShown)
            {
                Debug.LogWarning(
                    $"Shelf '{furnitureId}', slot '{slot.definition.SlotId}': {quantity} units are exposed but only {slot.positions.Count} fit in the visual stacks. The logical quantity is unchanged.", this);
                slot.excessWarningShown = true;
            }
            else if (quantity <= slot.positions.Count)
            {
                slot.excessWarningShown = false;
            }
        }

        private void BuildPositions(SlotVisuals slot, GameObject prefab)
        {
            MeshRenderer[] productRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (productRenderers.Length == 0 || prefab.transform.localScale != Vector3.one)
            {
                Debug.LogWarning("The display prefab needs a MeshRenderer and a unit-scale root.", prefab);
                return;
            }

            Bounds productBounds = GetBoundsInRootSpace(prefab.transform, productRenderers[0]);
            for (int index = 1; index < productRenderers.Length; index++)
            {
                productBounds.Encapsulate(GetBoundsInRootSpace(prefab.transform, productRenderers[index]));
            }

            Quaternion displayRotation = Quaternion.Euler(0f, 90f, 0f) * prefab.transform.localRotation;
            Bounds rotatedBounds = new(displayRotation * productBounds.center, Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        rotatedBounds.Encapsulate(displayRotation *
                            (productBounds.center + Vector3.Scale(productBounds.extents, new Vector3(x, y, z))));
                    }
                }
            }

            productBounds = rotatedBounds;

            Vector3 anchor = slot.definition.LocalDisplayPosition;
            Bounds tray = slot.trayBounds;
            if (productBounds.size.x <= GeometryTolerance || productBounds.size.y <= GeometryTolerance
                || productBounds.size.z <= GeometryTolerance
                || Mathf.Abs(anchor.y - tray.max.y) > AnchorTolerance
                || anchor.x - productBounds.extents.x < tray.min.x - GeometryTolerance
                || anchor.x + productBounds.extents.x > tray.max.x + GeometryTolerance)
            {
                Debug.LogWarning($"Product bounds do not fit the measured tray or anchor for '{slot.definition.SlotId}'.", this);
                return;
            }

            float depth = 2f * Mathf.Min(anchor.z - tray.min.z, tray.max.z - anchor.z);
            int count = Mathf.Clamp(Mathf.FloorToInt((depth + GeometryTolerance) / productBounds.size.z), 0, StackCount);
            List<float> centers = new(count);
            for (int index = 0; index < count; index++)
            {
                centers.Add(anchor.z + (index - (count - 1) * 0.5f) * productBounds.size.z);
            }

            centers.Sort((first, second) => Mathf.Abs(first - anchor.z).CompareTo(Mathf.Abs(second - anchor.z)));
            bool[] blockedStacks = new bool[centers.Count];
            for (int layer = 0; layer < StackLayers; layer++)
            {
                for (int index = 0; index < centers.Count; index++)
                {
                    if (blockedStacks[index])
                    {
                        continue;
                    }

                    Vector3 offset = new(
                        anchor.x - productBounds.center.x,
                        anchor.y - productBounds.min.y + layer * productBounds.size.y,
                        centers[index] - productBounds.center.z);
                    Bounds candidate = new(productBounds.center + offset, productBounds.size);
                    if (OverlapsShelf(candidate, slot.trayRenderer))
                    {
                        blockedStacks[index] = true;
                        continue;
                    }

                    slot.positions.Add(offset);
                }
            }

            if (VerboseDiagnostics)
                Debug.Log($"Shelf '{furnitureId}', slot '{slot.definition.SlotId}': visual stacks fit {slot.positions.Count} mortar bag(s).", this);
            if (slot.positions.Count < StackCount * StackLayers)
            {
                Debug.LogWarning($"Shelf slot '{slot.definition.SlotId}' cannot fit three stacks of four bags without overlapping the shelf. The logical capacity is unchanged.", this);
            }
        }

        private bool OverlapsShelf(Bounds candidate, MeshRenderer trayRenderer)
        {
            for (int index = 0; index < shelfRenderers.Count; index++)
            {
                Bounds obstacle = shelfBounds[index];
                if (shelfRenderers[index] == trayRenderer)
                {
                    continue;
                }

                if (candidate.min.x < obstacle.max.x - GeometryTolerance
                    && candidate.max.x > obstacle.min.x + GeometryTolerance
                    && candidate.min.y < obstacle.max.y - GeometryTolerance
                    && candidate.max.y > obstacle.min.y + GeometryTolerance
                    && candidate.min.z < obstacle.max.z - GeometryTolerance
                    && candidate.max.z > obstacle.min.z + GeometryTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static Bounds GetBoundsInRootSpace(Transform root, MeshRenderer renderer)
        {
            Bounds local = renderer.localBounds;
            Matrix4x4 toRoot = root.worldToLocalMatrix * renderer.localToWorldMatrix;
            Bounds result = new(toRoot.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        result.Encapsulate(toRoot.MultiplyPoint3x4(
                            local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
                    }
                }
            }

            return result;
        }

        private static void ClearInstances(SlotVisuals slot)
        {
            foreach (GameObject instance in slot.instances)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            slot.instances.Clear();
        }

        private sealed class SlotVisuals
        {
            public readonly DisplaySlotDefinition definition;
            public readonly MeshRenderer trayRenderer;
            public readonly Bounds trayBounds;
            public readonly List<Vector3> positions = new();
            public readonly List<GameObject> instances = new();
            public GameObject prefab;
            public Transform container;
            public bool missingPrefabWarningShown;
            public bool excessWarningShown;

            public SlotVisuals(DisplaySlotDefinition definition, MeshRenderer trayRenderer, Bounds trayBounds)
            {
                this.definition = definition;
                this.trayRenderer = trayRenderer;
                this.trayBounds = trayBounds;
            }
        }
    }
}
