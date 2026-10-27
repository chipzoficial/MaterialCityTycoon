using System;
using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public static class ConfigureIndustrialStorageCapacityMenu
    {
        private const string FurniturePath = "Assets/_Game/ScriptableObjects/Furniture/DEF_IndustrialStorageShelf.asset";
        private const string CapacityPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_IndustrialStorageShelfCapacity.asset";
        private const string ProductPath = "Assets/_Game/ScriptableObjects/Products/DEF_Argamassa.asset";
        private const float Margin = 0.01f;
        private const float Tolerance = 0.0001f;
        private static readonly string[] PlatformNames = { "Plataforma_Inferior", "Plataforma_Intermediaria", "Plataforma_Superior" };
        private static readonly string[] LevelIds = { "storage_bottom", "storage_middle", "storage_top" };
        private static readonly string[] LevelNames = { "Nível inferior", "Nível intermediário", "Nível superior" };

        [MenuItem("Tools/Material City Tycoon/Configure Industrial Shelf Capacity")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play Mode before configuring storage capacity."); return; }
            FurnitureDefinition furniture = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(FurniturePath);
            ProductDefinition product = AssetDatabase.LoadAssetAtPath<ProductDefinition>(ProductPath);
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(CapacityPath);
            if (furniture == null || furniture.FurnitureTypeId != "industrial_storage_shelf" || !furniture.IsValid(out _)
                || furniture.Prefab == null || product == null || product.ProductId != "argamassa" || !product.IsValid(out _)
                || product.DisplayPrefab == null || (existing != null && existing is not StorageCapacityDefinition)
                || (existing is StorageCapacityDefinition configured && configured.Product != null && configured.Product != product)
                || (furniture.StorageCapacity != null && furniture.StorageCapacity != existing))
            {
                Debug.LogError("A valid industrial shelf and Argamassa visual are required; existing capacity references were preserved.");
                return;
            }

            if (!TryMeasure(furniture.Prefab, product.DisplayPrefab, out StorageLevelDefinition[] levels, out Vector3 size)) return;
            int total = 0;
            foreach (StorageLevelDefinition level in levels)
            {
                if (!StoragePacking.TryGetCapacity(level, size, Margin, out int count) || count == 0 || total > int.MaxValue - count)
                { Debug.LogError($"No valid product arrangement was found for '{level.LevelId}'."); return; }
                total += count;
            }

            StorageCapacityDefinition capacity = existing as StorageCapacityDefinition;
            if (capacity == null)
            {
                capacity = ScriptableObject.CreateInstance<StorageCapacityDefinition>();
                capacity.name = "DEF_IndustrialStorageShelfCapacity";
                AssetDatabase.CreateAsset(capacity, CapacityPath);
                Undo.RegisterCreatedObjectUndo(capacity, "Create Industrial Shelf Capacity");
            }
            else Undo.RecordObject(capacity, "Configure Industrial Shelf Capacity");

            SerializedObject data = new(capacity);
            data.FindProperty("product").objectReferenceValue = product;
            data.FindProperty("orientedProductSize").vector3Value = size;
            data.FindProperty("productYawDegrees").floatValue = 90f;
            data.FindProperty("spacing").floatValue = Margin;
            SerializedProperty serializedLevels = data.FindProperty("levels");
            serializedLevels.arraySize = levels.Length;
            for (int i = 0; i < levels.Length; i++)
            {
                SerializedProperty item = serializedLevels.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("levelId").stringValue = levels[i].LevelId;
                item.FindPropertyRelative("displayName").stringValue = levels[i].DisplayName;
                item.FindPropertyRelative("localSupportCenter").vector3Value = levels[i].LocalSupportCenter;
                item.FindPropertyRelative("usableArea").vector2Value = levels[i].UsableArea;
                item.FindPropertyRelative("usableHeight").floatValue = levels[i].UsableHeight;
            }
            data.ApplyModifiedProperties();
            EditorUtility.SetDirty(capacity);
            SerializedObject furnitureData = new(furniture);
            if (furniture.StorageCapacity != capacity)
            {
                Undo.RecordObject(furniture, "Assign Industrial Shelf Capacity");
                furnitureData.FindProperty("storageCapacity").objectReferenceValue = capacity;
                furnitureData.ApplyModifiedProperties();
                EditorUtility.SetDirty(furniture);
                AssetDatabase.SaveAssetIfDirty(furniture);
            }
            AssetDatabase.SaveAssetIfDirty(capacity);
            Debug.Log($"Industrial shelf configured: {total} mortar bags total, 1 cm spacing and edge/ceiling clearance.", capacity);
        }

        private static bool TryMeasure(GameObject shelf, GameObject bag, out StorageLevelDefinition[] levels, out Vector3 orientedSize)
        {
            levels = null; orientedSize = default;
            if (shelf.transform.localScale != Vector3.one || bag.transform.localScale != Vector3.one
                || Quaternion.Angle(shelf.transform.localRotation, Quaternion.identity) > 0.01f
                || Quaternion.Angle(bag.transform.localRotation, Quaternion.identity) > 0.01f)
            { Debug.LogError("Shelf and bag roots must have unit scale and identity rotation for measurement."); return false; }
            MeshRenderer[] renderers = shelf.GetComponentsInChildren<MeshRenderer>(true);
            MeshRenderer[] products = bag.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0 || products.Length == 0) { Debug.LogError("Shelf and bag need MeshRenderers."); return false; }
            Bounds[] bounds = new Bounds[renderers.Length];
            Bounds all = BoundsInRoot(shelf.transform, renderers[0]);
            for (int i = 0; i < renderers.Length; i++) { bounds[i] = BoundsInRoot(shelf.transform, renderers[i]); all.Encapsulate(bounds[i]); }
            Bounds bagBounds = BoundsInRoot(bag.transform, products[0]);
            foreach (MeshRenderer renderer in products) bagBounds.Encapsulate(BoundsInRoot(bag.transform, renderer));
            orientedSize = new Vector3(bagBounds.size.z, bagBounds.size.y, bagBounds.size.x);
            float left = float.NegativeInfinity, right = float.PositiveInfinity, back = float.NegativeInfinity, front = float.PositiveInfinity;
            int postCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].name.StartsWith("Montante_", StringComparison.Ordinal)) continue;
                Bounds post = bounds[i]; postCount++;
                if (post.center.x < 0f) left = Mathf.Max(left, post.max.x); else right = Mathf.Min(right, post.min.x);
                if (post.center.z < 0f) back = Mathf.Max(back, post.max.z); else front = Mathf.Min(front, post.min.z);
            }
            if (postCount != 4 || !StoragePacking.IsPositive(right - left) || !StoragePacking.IsPositive(front - back))
            { Debug.LogError("The four shelf posts could not be measured safely."); return false; }

            levels = new StorageLevelDefinition[3];
            for (int levelIndex = 0; levelIndex < levels.Length; levelIndex++)
            {
                int trayIndex = Array.FindIndex(renderers, r => r.name == PlatformNames[levelIndex]);
                if (trayIndex < 0 || Array.FindLastIndex(renderers, r => r.name == PlatformNames[levelIndex]) != trayIndex)
                { Debug.LogError($"Expected exactly one '{PlatformNames[levelIndex]}' renderer."); return false; }
                Bounds tray = bounds[trayIndex];
                float minX = Mathf.Max(tray.min.x, left) + Margin, maxX = Mathf.Min(tray.max.x, right) - Margin;
                float minZ = Mathf.Max(tray.min.z, back) + Margin, maxZ = Mathf.Min(tray.max.z, front) - Margin;
                float ceiling = all.max.y;
                for (int i = 0; i < bounds.Length; i++)
                {
                    if (i == trayIndex) continue;
                    Bounds obstacle = bounds[i];
                    if (obstacle.max.x <= minX + Tolerance || obstacle.min.x >= maxX - Tolerance
                        || obstacle.max.z <= minZ + Tolerance || obstacle.min.z >= maxZ - Tolerance
                        || obstacle.max.y <= tray.max.y + Tolerance) continue;
                    if (obstacle.min.y <= tray.max.y + Tolerance)
                    { Debug.LogError($"An obstacle intersects the usable area of '{PlatformNames[levelIndex]}'."); return false; }
                    ceiling = Mathf.Min(ceiling, obstacle.min.y);
                }
                Vector3 support = new((minX + maxX) * 0.5f, tray.max.y, (minZ + maxZ) * 0.5f);
                levels[levelIndex] = new StorageLevelDefinition(LevelIds[levelIndex], LevelNames[levelIndex], support,
                    new Vector2(maxX - minX, maxZ - minZ), ceiling - tray.max.y - Margin);
                if (!levels[levelIndex].IsValid()) { Debug.LogError($"Invalid usable dimensions for '{PlatformNames[levelIndex]}'."); return false; }
                StoragePacking.TryGetCapacity(levels[levelIndex], orientedSize, Margin, out int count);
                Debug.Log($"{LevelIds[levelIndex]}: support {support.ToString("F4")}, usable area {levels[levelIndex].UsableArea.ToString("F4")}, height {levels[levelIndex].UsableHeight:F4}, capacity {count}.");
            }
            Debug.Log($"Mortar bag rotated 90 degrees: bounds {orientedSize.ToString("F4")}; post opening {right-left:F4} x {front-back:F4} m.");
            return true;
        }

        private static Bounds BoundsInRoot(Transform root, MeshRenderer renderer)
        {
            Bounds source = renderer.localBounds;
            Matrix4x4 matrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            Bounds result = new(matrix.MultiplyPoint3x4(source.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                result.Encapsulate(matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents, new Vector3(x, y, z))));
            return result;
        }
    }
}
