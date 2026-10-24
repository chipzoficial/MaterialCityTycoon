using System;
using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public static class ConfigurePrototypeShelfDisplaySlotsMenu
    {
        private const string PrototypeShelfDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_PrototypeShelf.asset";
        private const string PrototypeShelfPrefabPath = "Assets/_Game/Prefabs/Building/PB_PrototypeShelf.prefab";
        private const string PrototypeShelfFurnitureTypeId = "prototype_shelf";

        private static readonly SlotConfiguration[] ShelfSlots =
        {
            new("shelf_bottom", "Nível inferior", "Shelf_01"),
            new("shelf_middle", "Nível intermediário", "Shelf_02"),
            new("shelf_top", "Nível superior", "Shelf_03")
        };

        [MenuItem("Tools/Material City Tycoon/Configure Prototype Shelf Display Slots")]
        public static void ConfigurePrototypeShelfDisplaySlots()
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

            Undo.RecordObject(shelfDefinition, "Configure Prototype Shelf Display Slots");
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
                slotProperties.capacityUnits.intValue = 0;
            }

            serializedDefinition.ApplyModifiedProperties();
            EditorUtility.SetDirty(shelfDefinition);
            AssetDatabase.SaveAssetIfDirty(shelfDefinition);
            Debug.Log("Prototype shelf display slots configured: shelf_bottom, shelf_middle and shelf_top.", shelfDefinition);
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
                    Debug.LogError($"Prototype shelf slot id '{configuration.slotId}' is duplicated.");
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetShelfDefinition(out FurnitureDefinition shelfDefinition)
        {
            shelfDefinition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(PrototypeShelfDefinitionPath);
            if (shelfDefinition == null)
            {
                Debug.LogError($"Prototype shelf definition was not found at '{PrototypeShelfDefinitionPath}'.");
                return false;
            }

            if (shelfDefinition.FurnitureTypeId != PrototypeShelfFurnitureTypeId)
            {
                Debug.LogError(
                    $"Furniture definition at '{PrototypeShelfDefinitionPath}' must use Furniture Type Id '{PrototypeShelfFurnitureTypeId}'.",
                    shelfDefinition);
                return false;
            }

            return true;
        }

        private static bool TryGetShelfPrefab(FurnitureDefinition shelfDefinition, out GameObject shelfPrefab)
        {
            shelfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeShelfPrefabPath);
            if (shelfPrefab == null)
            {
                Debug.LogError($"Prototype shelf prefab was not found at '{PrototypeShelfPrefabPath}'.", shelfDefinition);
                return false;
            }

            if (shelfDefinition.Prefab != shelfPrefab)
            {
                Debug.LogError(
                    "Prototype shelf definition references a different prefab. Configure the furniture definition before configuring its display slots.",
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
            Debug.LogError($"Prototype shelf prefab is missing the expected tray '{childName}'.", parent);
            return false;
        }

        private static bool TryGetTopCenterInRootSpace(Transform root, Transform shelfTransform, out Vector3 topCenter)
        {
            MeshRenderer renderer = shelfTransform.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                topCenter = default;
                Debug.LogError($"Prototype shelf tray '{shelfTransform.name}' requires a MeshRenderer.", shelfTransform);
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
