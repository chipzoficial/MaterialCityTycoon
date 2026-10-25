using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;
using UnityEngine;

namespace MaterialCityTycoon.Building
{
    [Serializable]
    public sealed class StorageLevelDefinition
    {
        [SerializeField] private string levelId;
        [SerializeField] private string displayName;
        [SerializeField] private Vector3 localSupportCenter;
        [SerializeField] private Vector2 usableArea;
        [SerializeField] private float usableHeight;

        public string LevelId => levelId;
        public string DisplayName => displayName;
        public Vector3 LocalSupportCenter => localSupportCenter;
        public Vector2 UsableArea => usableArea;
        public float UsableHeight => usableHeight;

        public StorageLevelDefinition(string id, string name, Vector3 support, Vector2 area, float height)
        {
            levelId = id; displayName = name; localSupportCenter = support; usableArea = area; usableHeight = height;
        }

        public bool IsValid() => !string.IsNullOrWhiteSpace(levelId) && !string.IsNullOrWhiteSpace(displayName)
            && StoragePacking.IsFinite(localSupportCenter.x) && StoragePacking.IsFinite(localSupportCenter.y)
            && StoragePacking.IsFinite(localSupportCenter.z) && StoragePacking.IsPositive(usableArea.x)
            && StoragePacking.IsPositive(usableArea.y) && StoragePacking.IsPositive(usableHeight);
    }

    /// <summary>Physical configuration shared by furniture of one type; never stores quantities.</summary>
    public sealed class StorageCapacityDefinition : ScriptableObject
    {
        [SerializeField] private ProductDefinition product;
        [SerializeField] private Vector3 orientedProductSize;
        [SerializeField] private float productYawDegrees;
        [SerializeField] private float spacing;
        [SerializeField] private StorageLevelDefinition[] levels = Array.Empty<StorageLevelDefinition>();

        public ProductDefinition Product => product;
        public Vector3 OrientedProductSize => orientedProductSize;
        public float ProductYawDegrees => productYawDegrees;
        public float Spacing => spacing;
        public IReadOnlyList<StorageLevelDefinition> Levels => levels ?? Array.Empty<StorageLevelDefinition>();

        public bool IsValid()
        {
            if (product == null || !product.IsValid(out _) || levels == null || levels.Length == 0
                || !StoragePacking.IsPositive(orientedProductSize.x) || !StoragePacking.IsPositive(orientedProductSize.y)
                || !StoragePacking.IsPositive(orientedProductSize.z) || !StoragePacking.IsFinite(productYawDegrees)
                || !StoragePacking.IsFinite(spacing) || spacing < 0f)
                return false;
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (StorageLevelDefinition level in levels)
                if (level == null || !level.IsValid() || !ids.Add(level.LevelId)
                    || !StoragePacking.TryGetCapacity(level, orientedProductSize, spacing, out _)) return false;
            return true;
        }

        public bool TryGetCapacity(string levelId, ProductDefinition requestedProduct, out int capacity)
        {
            capacity = 0;
            if (!IsValid() || requestedProduct == null || requestedProduct != product || string.IsNullOrWhiteSpace(levelId)) return false;
            foreach (StorageLevelDefinition level in levels)
                if (level.LevelId == levelId) return StoragePacking.TryGetCapacity(level, orientedProductSize, spacing, out capacity);
            return false;
        }

        public bool TryGetTotalCapacity(ProductDefinition requestedProduct, out int capacity)
        {
            capacity = 0;
            if (!IsValid() || requestedProduct == null || requestedProduct != product) return false;
            foreach (StorageLevelDefinition level in levels)
            {
                if (!StoragePacking.TryGetCapacity(level, orientedProductSize, spacing, out int units)
                    || capacity > int.MaxValue - units) { capacity = 0; return false; }
                capacity += units;
            }
            return true;
        }
    }

    /// <summary>Counts a regular, non-overlapping arrangement inside an already inset area.</summary>
    public static class StoragePacking
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool IsPositive(float value) => IsFinite(value) && value > 0f;

        public static bool TryGetCapacity(StorageLevelDefinition level, Vector3 size, float spacing, out int capacity)
        {
            capacity = 0;
            if (level == null || !level.IsValid() || !IsPositive(size.x) || !IsPositive(size.y)
                || !IsPositive(size.z) || !IsFinite(spacing) || spacing < 0f) return false;
            double x = Math.Floor(((double)level.UsableArea.x + spacing) / ((double)size.x + spacing));
            double z = Math.Floor(((double)level.UsableArea.y + spacing) / ((double)size.z + spacing));
            double y = Math.Floor(((double)level.UsableHeight + spacing) / ((double)size.y + spacing));
            double total = x * y * z;
            if (double.IsNaN(total) || double.IsInfinity(total) || total > int.MaxValue) return false;
            capacity = (int)total;
            return true;
        }
    }
}
