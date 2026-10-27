using System;
using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public static class ConfigurePrototypeToolDisplaySlotsMenu
    {
        private const string ToolDisplayDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_ToolDisplay.asset";
        private const string ToolDisplayPrefabPath = "Assets/_Game/Prefabs/Building/PF_ToolDisplay.prefab";
        private const string ToolDisplayFurnitureTypeId = "prototype_tool_display";
        private const string PanelTransformName = "Panel_01";
        private const string SlotId = "tool_panel_main";
        private const string SlotDisplayName = "Painel principal";

        [MenuItem("Tools/Material City Tycoon/Configure Tool Display Slots")]
        public static void ConfigureToolDisplaySlots()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before configuring tool display slots.");
                return;
            }

            if (!TryGetToolDisplayDefinition(out FurnitureDefinition toolDisplayDefinition)
                || !TryGetToolDisplayPrefab(toolDisplayDefinition, out GameObject toolDisplayPrefab)
                || !TryGetPanelFrontPosition(toolDisplayPrefab, out Vector3 panelFrontPosition)
                || !TryGetDisplaySlotsProperty(toolDisplayDefinition, out SerializedObject serializedDefinition, out SerializedProperty displaySlotsProperty)
                || !TryFindTargetSlotIndex(displaySlotsProperty, out int targetSlotIndex))
            {
                return;
            }

            Undo.RecordObject(toolDisplayDefinition, "Configure Tool Display Slots");
            if (targetSlotIndex == displaySlotsProperty.arraySize)
            {
                displaySlotsProperty.arraySize++;
            }

            SerializedProperty slotProperty = displaySlotsProperty.GetArrayElementAtIndex(targetSlotIndex);
            if (!TryGetSlotProperties(slotProperty, out SlotProperties slotProperties))
            {
                return;
            }

            slotProperties.slotId.stringValue = SlotId;
            slotProperties.displayName.stringValue = SlotDisplayName;
            slotProperties.allowedDisplayModes.arraySize = 1;
            slotProperties.allowedDisplayModes.GetArrayElementAtIndex(0).enumValueIndex = (int)ProductDisplayMode.Hang;
            slotProperties.localDisplayPosition.vector3Value = panelFrontPosition;
            slotProperties.capacityUnits.intValue = 0;

            serializedDefinition.ApplyModifiedProperties();
            EditorUtility.SetDirty(toolDisplayDefinition);
            AssetDatabase.SaveAssetIfDirty(toolDisplayDefinition);
            Debug.Log("Tool display slot configured: tool_panel_main.", toolDisplayDefinition);
        }

        private static bool TryGetToolDisplayDefinition(out FurnitureDefinition toolDisplayDefinition)
        {
            toolDisplayDefinition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(ToolDisplayDefinitionPath);
            if (toolDisplayDefinition == null)
            {
                Debug.LogError($"Tool display definition was not found at '{ToolDisplayDefinitionPath}'.");
                return false;
            }

            if (toolDisplayDefinition.FurnitureTypeId != ToolDisplayFurnitureTypeId)
            {
                Debug.LogError(
                    $"Furniture definition at '{ToolDisplayDefinitionPath}' must use Furniture Type Id '{ToolDisplayFurnitureTypeId}'.",
                    toolDisplayDefinition);
                return false;
            }

            return true;
        }

        private static bool TryGetToolDisplayPrefab(FurnitureDefinition toolDisplayDefinition, out GameObject toolDisplayPrefab)
        {
            toolDisplayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToolDisplayPrefabPath);
            if (toolDisplayPrefab == null)
            {
                Debug.LogError($"Tool display prefab was not found at '{ToolDisplayPrefabPath}'.", toolDisplayDefinition);
                return false;
            }

            if (toolDisplayDefinition.Prefab != toolDisplayPrefab)
            {
                Debug.LogError(
                    "Tool display definition references a different prefab. Configure the furniture definition before configuring its display slot.",
                    toolDisplayDefinition);
                return false;
            }

            return true;
        }

        private static bool TryGetPanelFrontPosition(GameObject toolDisplayPrefab, out Vector3 panelFrontPosition)
        {
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(toolDisplayPrefab));
            try
            {
                if (!TryGetDirectChild(prefabContents.transform, PanelTransformName, out Transform panelTransform))
                {
                    panelFrontPosition = default;
                    return false;
                }

                MeshRenderer panelRenderer = panelTransform.GetComponent<MeshRenderer>();
                if (panelRenderer == null)
                {
                    panelFrontPosition = default;
                    Debug.LogError($"Tool display panel '{PanelTransformName}' requires a MeshRenderer.", panelTransform);
                    return false;
                }

                Bounds panelBounds = panelRenderer.localBounds;
                Vector3 panelLocalFrontCenter = new(
                    panelBounds.center.x,
                    panelBounds.center.y,
                    panelBounds.max.z);
                Matrix4x4 panelToRoot = prefabContents.transform.worldToLocalMatrix * panelTransform.localToWorldMatrix;
                panelFrontPosition = panelToRoot.MultiplyPoint3x4(panelLocalFrontCenter);
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
            Debug.LogError($"Tool display prefab is missing the expected panel '{childName}'.", parent);
            return false;
        }

        private static bool TryGetDisplaySlotsProperty(
            FurnitureDefinition toolDisplayDefinition,
            out SerializedObject serializedDefinition,
            out SerializedProperty displaySlotsProperty)
        {
            serializedDefinition = new SerializedObject(toolDisplayDefinition);
            displaySlotsProperty = serializedDefinition.FindProperty("displaySlots");
            if (displaySlotsProperty != null && displaySlotsProperty.isArray)
            {
                return true;
            }

            Debug.LogError("FurnitureDefinition does not expose the expected Display Slots field.", toolDisplayDefinition);
            return false;
        }

        private static bool TryFindTargetSlotIndex(SerializedProperty displaySlotsProperty, out int targetSlotIndex)
        {
            HashSet<string> slotIds = new(StringComparer.Ordinal);
            targetSlotIndex = displaySlotsProperty.arraySize;

            for (int index = 0; index < displaySlotsProperty.arraySize; index++)
            {
                SerializedProperty slotIdProperty = displaySlotsProperty
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("slotId");
                string existingSlotId = slotIdProperty?.stringValue;

                if (string.IsNullOrWhiteSpace(existingSlotId))
                {
                    Debug.LogError($"Display slot at index {index} has an empty Slot Id. Resolve it before configuring the tool display.");
                    return false;
                }

                if (!slotIds.Add(existingSlotId))
                {
                    Debug.LogError($"Display slot id '{existingSlotId}' is duplicated. Resolve it before configuring the tool display.");
                    return false;
                }

                if (existingSlotId == SlotId)
                {
                    targetSlotIndex = index;
                }
            }

            return true;
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

            Debug.LogError("DisplaySlotDefinition fields do not match the tool display configuration tool.");
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
    }
}
