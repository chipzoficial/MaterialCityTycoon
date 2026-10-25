using System.Collections.Generic;
using MaterialCityTycoon.Commerce;
using UnityEngine;

namespace MaterialCityTycoon.Building
{
    /// <summary>Shows the allocations of one industrial shelf without changing inventory.</summary>
    public sealed class IndustrialStorageVisualPresenter : MonoBehaviour
    {
        private const float Tolerance = 0.001f;
        private readonly Dictionary<string, LevelVisuals> levels = new();
        private StoreShelfInventory inventory;
        private string furnitureId;
        private ProductDefinition product;
        private GameObject prefab;
        private Quaternion bagRotation;
        private Bounds bagBounds;
        private StorageCapacityDefinition capacity;

        public void Initialize(StoreShelfInventory source, string id, StorageCapacityDefinition definition)
        {
            if (inventory != null) return;
            if (source == null || string.IsNullOrWhiteSpace(id) || definition == null || !definition.IsValid())
            {
                Debug.LogWarning("Cannot initialize industrial storage visuals: invalid inventory or capacity.", this);
                return;
            }

            product = definition.Product;
            prefab = product.DisplayPrefab;
            capacity = definition;
            if (prefab == null || prefab.transform.localScale != Vector3.one || !TryGetRotatedBounds(prefab, definition.ProductYawDegrees, out bagBounds))
            {
                Debug.LogWarning("Industrial storage visuals require the configured product prefab with MeshRenderers and unit root scale.", this);
                return;
            }
            if ((bagBounds.size - definition.OrientedProductSize).sqrMagnitude > Tolerance * Tolerance)
            {
                Debug.LogWarning("The product's visual dimensions no longer match the configured storage capacity.", this);
                return;
            }
            bagRotation = Quaternion.Euler(0f, definition.ProductYawDegrees, 0f) * prefab.transform.localRotation;
            furnitureId = id;
            inventory = source;
            foreach (StorageLevelDefinition level in definition.Levels)
                levels.Add(level.LevelId, new LevelVisuals(level));
            inventory.Changed += OnInventoryChanged;
            foreach (LevelVisuals level in levels.Values) SyncLevel(level);
        }

        private void OnInventoryChanged(string changedFurnitureId, string levelId)
        {
            if (changedFurnitureId == furnitureId && levels.TryGetValue(levelId, out LevelVisuals level))
                SyncLevel(level);
        }

        private void SyncLevel(LevelVisuals visuals)
        {
            StorageLevelDefinition level = visuals.Definition;
            int target = inventory.GetQuantity(furnitureId, level.LevelId, product);
            while (visuals.Instances.Count > target)
            {
                int last = visuals.Instances.Count - 1;
                GameObject instance = visuals.Instances[last];
                visuals.Instances.RemoveAt(last);
                if (instance != null)
                {
                    instance.SetActive(false);
                    if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
                }
            }
            if (target == 0) return;
            if (visuals.Container == null)
            {
                visuals.Container = new GameObject($"Storage_{level.LevelId}").transform;
                visuals.Container.SetParent(transform, false);
            }
            int columns = Mathf.FloorToInt((level.UsableArea.x + capacity.Spacing) / (capacity.OrientedProductSize.x + capacity.Spacing));
            int rows = Mathf.FloorToInt((level.UsableArea.y + capacity.Spacing) / (capacity.OrientedProductSize.z + capacity.Spacing));
            while (visuals.Instances.Count < target)
            {
                int index = visuals.Instances.Count;
                int layer = index / (columns * rows);
                int withinLayer = index % (columns * rows);
                int x = withinLayer % columns;
                int z = withinLayer / columns;
                Vector3 center = level.LocalSupportCenter + new Vector3(
                    (x - (columns - 1) * 0.5f) * (capacity.OrientedProductSize.x + capacity.Spacing),
                    capacity.OrientedProductSize.y * 0.5f + layer * (capacity.OrientedProductSize.y + capacity.Spacing),
                    (z - (rows - 1) * 0.5f) * (capacity.OrientedProductSize.z + capacity.Spacing));
                GameObject instance = Instantiate(prefab, visuals.Container, false);
                instance.transform.localRotation = bagRotation;
                instance.transform.localPosition = center - bagBounds.center;
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
                visuals.Instances.Add(instance);
            }
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
        }

        private static bool TryGetRotatedBounds(GameObject source, float yaw, out Bounds result)
        {
            result = default;
            MeshRenderer[] renderers = source.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) return false;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * source.transform.localRotation;
            bool first = true;
            foreach (MeshRenderer renderer in renderers)
            {
                Bounds bounds = renderer.localBounds;
                Matrix4x4 toRoot = source.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 point = rotation * toRoot.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                            if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                            else result.Encapsulate(point);
                        }
            }
            return true;
        }

        private sealed class LevelVisuals
        {
            public readonly StorageLevelDefinition Definition;
            public readonly List<GameObject> Instances = new();
            public Transform Container;
            public LevelVisuals(StorageLevelDefinition definition) => Definition = definition;
        }
    }
}
