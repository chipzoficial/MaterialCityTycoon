using System.Collections.Generic;
using MaterialCityTycoon.Building;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public static class ConfigureFurnitureAccessMenu
    {
        private const string Folder = "Assets/_Game/ScriptableObjects/Furniture/";

        [MenuItem("Tools/Material City Tycoon/Configure Furniture Access")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before configuring furniture access.");
                return;
            }

            FurnitureDefinition shelf = Load("DEF_CommercialShelf.asset", FurnitureTypeIds.CommercialShelf, 1, 2);
            FurnitureDefinition tool = Load("DEF_ToolDisplay.asset", FurnitureTypeIds.ToolDisplay, 1, 1);
            FurnitureDefinition rack = Load("DEF_PartsBinRack.asset", FurnitureTypeIds.PartsBinRack, 2, 1);
            FurnitureDefinition storage = Load("DEF_IndustrialStorageShelf.asset", FurnitureTypeIds.IndustrialStorageShelf, 2, 1);
            if (shelf == null || tool == null || rack == null || storage == null)
            {
                return;
            }

            SetAccessGroups(shelf,
                new AccessGroupConfiguration("left", new Vector2Int(-1, 0), new Vector2Int(-1, 1)),
                new AccessGroupConfiguration("right", new Vector2Int(1, 0), new Vector2Int(1, 1)));
            SetAccessGroups(tool,
                new AccessGroupConfiguration("front", new Vector2Int(0, -1)),
                new AccessGroupConfiguration("back", new Vector2Int(0, 1)));
            SetAccessGroups(rack,
                new AccessGroupConfiguration("front", new Vector2Int(0, 1), new Vector2Int(1, 1)));
            SetAccessGroups(storage,
                new AccessGroupConfiguration("front", new Vector2Int(0, 1), new Vector2Int(1, 1)),
                new AccessGroupConfiguration("back", new Vector2Int(0, -1), new Vector2Int(1, -1)));
            Debug.Log("Furniture access configured: commercial shelf, tool display and industrial shelf use alternative service faces; parts bin rack keeps its required +Z front.");
        }

        private static FurnitureDefinition Load(string filename, string id, int width, int depth)
        {
            FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(Folder + filename);
            if (definition == null || definition.FurnitureTypeId != id || definition.Width != width
                || definition.Depth != depth || definition.Prefab == null || !definition.IsValid(out _))
            {
                Debug.LogError($"Furniture access not configured: '{Folder + filename}' is missing or has an unexpected ID, footprint, prefab or invalid data.");
                return null;
            }

            return definition;
        }

        private static void SetAccessGroups(FurnitureDefinition definition, params AccessGroupConfiguration[] groups)
        {
            SerializedObject serialized = new(definition);
            SerializedProperty offsetsProperty = serialized.FindProperty("accessCellOffsets");
            SerializedProperty groupsProperty = serialized.FindProperty("accessGroups");
            List<Vector2Int> flattenedOffsets = new();
            foreach (AccessGroupConfiguration group in groups)
            {
                flattenedOffsets.AddRange(group.cellOffsets);
            }

            bool same = offsetsProperty.arraySize == flattenedOffsets.Count
                && groupsProperty.arraySize == groups.Length;
            for (int index = 0; same && index < flattenedOffsets.Count; index++)
            {
                same = offsetsProperty.GetArrayElementAtIndex(index).vector2IntValue == flattenedOffsets[index];
            }

            for (int groupIndex = 0; same && groupIndex < groups.Length; groupIndex++)
            {
                SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(groupIndex);
                AccessGroupConfiguration expected = groups[groupIndex];
                same = groupProperty.FindPropertyRelative("groupId").stringValue == expected.groupId;
                SerializedProperty cellsProperty = groupProperty.FindPropertyRelative("cellOffsets");
                same &= cellsProperty.arraySize == expected.cellOffsets.Length;
                for (int cellIndex = 0; same && cellIndex < expected.cellOffsets.Length; cellIndex++)
                {
                    same = cellsProperty.GetArrayElementAtIndex(cellIndex).vector2IntValue == expected.cellOffsets[cellIndex];
                }
            }

            if (same)
            {
                return;
            }

            Undo.RecordObject(definition, "Configure Furniture Access");
            offsetsProperty.arraySize = flattenedOffsets.Count;
            for (int index = 0; index < flattenedOffsets.Count; index++)
            {
                offsetsProperty.GetArrayElementAtIndex(index).vector2IntValue = flattenedOffsets[index];
            }

            groupsProperty.arraySize = groups.Length;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(groupIndex);
                AccessGroupConfiguration configuration = groups[groupIndex];
                groupProperty.FindPropertyRelative("groupId").stringValue = configuration.groupId;
                SerializedProperty cellsProperty = groupProperty.FindPropertyRelative("cellOffsets");
                cellsProperty.arraySize = configuration.cellOffsets.Length;
                for (int cellIndex = 0; cellIndex < configuration.cellOffsets.Length; cellIndex++)
                {
                    cellsProperty.GetArrayElementAtIndex(cellIndex).vector2IntValue = configuration.cellOffsets[cellIndex];
                }
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        private readonly struct AccessGroupConfiguration
        {
            public readonly string groupId;
            public readonly Vector2Int[] cellOffsets;

            public AccessGroupConfiguration(string groupId, params Vector2Int[] cellOffsets)
            {
                this.groupId = groupId;
                this.cellOffsets = cellOffsets;
            }
        }
    }
}
