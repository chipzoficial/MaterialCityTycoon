using System;
using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public static class ConfigureCommercialShelfDisplaySlotsMenu
    {
        private const string CommercialShelfDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_CommercialShelf.asset";
        private const string CommercialShelfPrefabPath = "Assets/_Game/Prefabs/Building/PF_CommercialShelf.prefab";
        private const string CommercialShelfFurnitureTypeId = FurnitureTypeIds.CommercialShelf;

        private static readonly SlotConfiguration[] ShelfSlots =
        {
            new("shelf_bottom", "Nível inferior", "Shelf_01"),
            new("shelf_middle", "Nível intermediário", "Shelf_02"),
            new("shelf_top", "Nível superior", "Shelf_03")
        };

        [MenuItem("Tools/Material City Tycoon/Configure Mortar Display Visual")]
        public static void ConfigureMortarDisplayVisual()
        {
            const string productPath = "Assets/_Game/ScriptableObjects/Products/DEF_Argamassa.asset";
            const string prefabPath = "Assets/_Game/Prefabs/Products/PF_SacoArgamassa20kg.prefab";
            ProductDefinition product = AssetDatabase.LoadAssetAtPath<ProductDefinition>(productPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || product == null || !product.IsValid(out _) || product.ProductId != "argamassa"
                || prefab == null || prefab.GetComponentsInChildren<MeshRenderer>(true).Length == 0
                || !TryGetShelfDefinition(out FurnitureDefinition shelfDefinition)
                || !TryGetShelfPrefab(shelfDefinition, out _))
            {
                Debug.LogError("A valid Argamassa definition, product prefab, and configured shelf are required outside Play Mode.");
                return;
            }

            if (product.DisplayPrefab != null && product.DisplayPrefab != prefab)
            {
                Debug.LogError("Argamassa already references a different display prefab; its existing reference was preserved.", product);
                return;
            }

            if (product.DisplayPrefab == prefab)
            {
                Debug.Log("Argamassa already references the mortar display prefab.", product);
                return;
            }

            SerializedObject serializedProduct = new(product);
            SerializedProperty displayPrefab = serializedProduct.FindProperty("displayPrefab");
            if (displayPrefab == null)
            {
                Debug.LogError("The product display prefab field is unavailable.", product);
                return;
            }

            Undo.RecordObject(product, "Configure Mortar Display Visual");
            displayPrefab.objectReferenceValue = prefab;
            serializedProduct.ApplyModifiedProperties();
            EditorUtility.SetDirty(product);
            AssetDatabase.SaveAssetIfDirty(product);
            Debug.Log("Argamassa display prefab configured.", product);
        }

        [MenuItem("Tools/Material City Tycoon/Inspect Mortar Shelf Geometry")]
        public static void InspectMortarShelfGeometry()
        {
            const string productPath = "Assets/_Game/Prefabs/Products/PF_SacoArgamassa20kg.prefab";
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || AssetDatabase.LoadAssetAtPath<GameObject>(productPath) == null
                || !TryGetShelfDefinition(out FurnitureDefinition shelfDefinition)
                || !TryGetShelfPrefab(shelfDefinition, out _))
            {
                Debug.LogError("The mortar prefab and configured shelf must be available outside Play Mode.");
                return;
            }

            GameObject product = PrefabUtility.LoadPrefabContents(productPath);
            GameObject shelf = PrefabUtility.LoadPrefabContents(CommercialShelfPrefabPath);
            try
            {
                foreach (MeshRenderer renderer in product.GetComponentsInChildren<MeshRenderer>(true))
                {
                    LogRendererBounds("Product", product.transform, renderer);
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Debug.Log($"Product material: {AssetDatabase.GetAssetPath(material)}; shader: {(material != null && material.shader != null ? material.shader.name : "missing")}");
                    }
                }

                foreach (MeshRenderer renderer in shelf.GetComponentsInChildren<MeshRenderer>(true))
                {
                    LogRendererBounds("Shelf", shelf.transform, renderer);
                }

                foreach (DisplaySlotDefinition slot in shelfDefinition.DisplaySlots)
                {
                    Debug.Log($"Slot {slot.SlotId}: anchor {slot.LocalDisplayPosition.ToString("F5")}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(product);
                PrefabUtility.UnloadPrefabContents(shelf);
            }
        }

        private static void LogRendererBounds(string label, Transform root, MeshRenderer renderer)
        {
            Bounds source = renderer.localBounds;
            Matrix4x4 toRoot = root.worldToLocalMatrix * renderer.localToWorldMatrix;
            Bounds result = new(toRoot.MultiplyPoint3x4(source.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        result.Encapsulate(toRoot.MultiplyPoint3x4(
                            source.center + Vector3.Scale(source.extents, new Vector3(x, y, z))));
                    }
                }
            }

            Debug.Log($"{label} {renderer.name}: min {result.min.ToString("F5")}, max {result.max.ToString("F5")}, size {result.size.ToString("F5")}");
        }

        [MenuItem("Tools/Material City Tycoon/Configure Commercial Shelf Display Slots")]
        public static void ConfigureCommercialShelfDisplaySlots()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before configuring prototype shelf display slots.");
                return;
            }

            if (!ValidateSlotConfigurations()
                || !TryGetShelfDefinition(out FurnitureDefinition shelfDefinition)
                || !TryGetShelfPrefab(shelfDefinition, out GameObject shelfPrefab)
                || !TryGetSlotAnchorPositions(shelfPrefab, out Vector3[] slotAnchorPositions)
                || !TryGetDisplaySlotsProperty(shelfDefinition, out SerializedObject serializedDefinition, out SerializedProperty displaySlotsProperty))
            {
                return;
            }

            Undo.RecordObject(shelfDefinition, "Configure Commercial Shelf Display Slots");
            displaySlotsProperty.arraySize = ShelfSlots.Length;

            for (int index = 0; index < ShelfSlots.Length; index++)
            {
                SerializedProperty slotProperty = displaySlotsProperty.GetArrayElementAtIndex(index);
                if (!TryGetSlotProperties(slotProperty, out SlotProperties slotProperties))
                {
                    return;
                }

                SlotConfiguration configuration = ShelfSlots[index];
                slotProperties.slotId.stringValue = configuration.slotId;
                slotProperties.displayName.stringValue = configuration.displayName;
                slotProperties.allowedDisplayModes.arraySize = 1;
                slotProperties.allowedDisplayModes.GetArrayElementAtIndex(0).enumValueIndex = (int)ProductDisplayMode.Stack;
                slotProperties.localDisplayPosition.vector3Value = slotAnchorPositions[index];
                slotProperties.capacityUnits.intValue = 12;
            }

            serializedDefinition.ApplyModifiedProperties();
            EditorUtility.SetDirty(shelfDefinition);
            AssetDatabase.SaveAssetIfDirty(shelfDefinition);
            Debug.Log("Commercial shelf display slots configured: shelf_bottom, shelf_middle and shelf_top.", shelfDefinition);
        }

        private static bool ValidateSlotConfigurations()
        {
            HashSet<string> slotIds = new(StringComparer.Ordinal);
            foreach (SlotConfiguration configuration in ShelfSlots)
            {
                if (string.IsNullOrWhiteSpace(configuration.slotId)
                    || string.IsNullOrWhiteSpace(configuration.displayName)
                    || string.IsNullOrWhiteSpace(configuration.shelfTransformName))
                {
                    Debug.LogError("A prototype shelf slot configuration is incomplete.");
                    return false;
                }

                if (!slotIds.Add(configuration.slotId))
                {
                    Debug.LogError($"Commercial shelf slot id '{configuration.slotId}' is duplicated.");
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetShelfDefinition(out FurnitureDefinition shelfDefinition)
        {
            shelfDefinition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(CommercialShelfDefinitionPath);
            if (shelfDefinition == null)
            {
                Debug.LogError($"Commercial shelf definition was not found at '{CommercialShelfDefinitionPath}'.");
                return false;
            }

            if (shelfDefinition.FurnitureTypeId != CommercialShelfFurnitureTypeId)
            {
                Debug.LogError(
                    $"Furniture definition at '{CommercialShelfDefinitionPath}' must use Furniture Type Id '{CommercialShelfFurnitureTypeId}'.",
                    shelfDefinition);
                return false;
            }

            return true;
        }

        private static bool TryGetShelfPrefab(FurnitureDefinition shelfDefinition, out GameObject shelfPrefab)
        {
            shelfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CommercialShelfPrefabPath);
            if (shelfPrefab == null)
            {
                Debug.LogError($"Commercial shelf prefab was not found at '{CommercialShelfPrefabPath}'.", shelfDefinition);
                return false;
            }

            if (shelfDefinition.Prefab != shelfPrefab)
            {
                Debug.LogError(
                    "Commercial shelf definition references a different prefab. Configure the furniture definition before configuring its display slots.",
                    shelfDefinition);
                return false;
            }

            return true;
        }

        private static bool TryGetSlotAnchorPositions(GameObject shelfPrefab, out Vector3[] slotAnchorPositions)
        {
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(shelfPrefab));
            try
            {
                slotAnchorPositions = new Vector3[ShelfSlots.Length];
                for (int index = 0; index < ShelfSlots.Length; index++)
                {
                    if (!TryGetDirectChild(prefabContents.transform, ShelfSlots[index].shelfTransformName, out Transform shelfTransform)
                        || !TryGetTopCenterInRootSpace(prefabContents.transform, shelfTransform, out Vector3 anchorPosition))
                    {
                        slotAnchorPositions = null;
                        return false;
                    }

                    slotAnchorPositions[index] = anchorPosition;
                }

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static bool TryGetDirectChild(Transform parent, string childName, out Transform child)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                child = parent.GetChild(index);
                if (child.name == childName)
                {
                    return true;
                }
            }

            child = null;
            Debug.LogError($"Commercial shelf prefab is missing the expected tray '{childName}'.", parent);
            return false;
        }

        private static bool TryGetTopCenterInRootSpace(Transform root, Transform shelfTransform, out Vector3 topCenter)
        {
            MeshRenderer renderer = shelfTransform.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                topCenter = default;
                Debug.LogError($"Commercial shelf tray '{shelfTransform.name}' requires a MeshRenderer.", shelfTransform);
                return false;
            }

            Bounds localBounds = renderer.localBounds;
            Matrix4x4 rendererToRoot = root.worldToLocalMatrix * renderer.localToWorldMatrix;
            Bounds rootSpaceBounds = new(rendererToRoot.MultiplyPoint3x4(localBounds.center), Vector3.zero);
            Vector3 extents = localBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = localBounds.center + Vector3.Scale(
                            extents,
                            new Vector3(x, y, z));
                        rootSpaceBounds.Encapsulate(rendererToRoot.MultiplyPoint3x4(corner));
                    }
                }
            }

            topCenter = new Vector3(rootSpaceBounds.center.x, rootSpaceBounds.max.y, rootSpaceBounds.center.z);
            return true;
        }

        private static bool TryGetDisplaySlotsProperty(
            FurnitureDefinition shelfDefinition,
            out SerializedObject serializedDefinition,
            out SerializedProperty displaySlotsProperty)
        {
            serializedDefinition = new SerializedObject(shelfDefinition);
            displaySlotsProperty = serializedDefinition.FindProperty("displaySlots");
            if (displaySlotsProperty != null && displaySlotsProperty.isArray)
            {
                return true;
            }

            Debug.LogError("FurnitureDefinition does not expose the expected Display Slots field.", shelfDefinition);
            return false;
        }

        private static bool TryGetSlotProperties(SerializedProperty slotProperty, out SlotProperties slotProperties)
        {
            slotProperties = new SlotProperties(
                slotProperty.FindPropertyRelative("slotId"),
                slotProperty.FindPropertyRelative("displayName"),
                slotProperty.FindPropertyRelative("allowedDisplayModes"),
                slotProperty.FindPropertyRelative("localDisplayPosition"),
                slotProperty.FindPropertyRelative("capacityUnits"));

            if (slotProperties.IsComplete)
            {
                return true;
            }

            Debug.LogError("DisplaySlotDefinition fields do not match the shelf configuration tool.");
            return false;
        }

        private readonly struct SlotProperties
        {
            public readonly SerializedProperty slotId;
            public readonly SerializedProperty displayName;
            public readonly SerializedProperty allowedDisplayModes;
            public readonly SerializedProperty localDisplayPosition;
            public readonly SerializedProperty capacityUnits;

            public bool IsComplete => slotId != null
                && displayName != null
                && allowedDisplayModes != null
                && localDisplayPosition != null
                && capacityUnits != null;

            public SlotProperties(
                SerializedProperty slotId,
                SerializedProperty displayName,
                SerializedProperty allowedDisplayModes,
                SerializedProperty localDisplayPosition,
                SerializedProperty capacityUnits)
            {
                this.slotId = slotId;
                this.displayName = displayName;
                this.allowedDisplayModes = allowedDisplayModes;
                this.localDisplayPosition = localDisplayPosition;
                this.capacityUnits = capacityUnits;
            }
        }

        private readonly struct SlotConfiguration
        {
            public readonly string slotId;
            public readonly string displayName;
            public readonly string shelfTransformName;

            public SlotConfiguration(string slotId, string displayName, string shelfTransformName)
            {
                this.slotId = slotId;
                this.displayName = displayName;
                this.shelfTransformName = shelfTransformName;
            }
        }
    }
}
