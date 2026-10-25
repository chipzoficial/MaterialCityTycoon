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

            FurnitureDefinition shelf = Load("DEF_PrototypeShelf.asset", "prototype_shelf", 1, 2);
            FurnitureDefinition tool = Load("DEF_PrototypeToolDisplay.asset", "prototype_tool_display", 1, 1);
            FurnitureDefinition rack = Load("DEF_PrototypePartsBinRack.asset", "prototype_parts_bin_rack", 2, 1);
            if (shelf == null || tool == null || rack == null)
            {
                return;
            }

            SetOffsets(shelf, new[] { new Vector2Int(-1, 0), new Vector2Int(-1, 1),
                new Vector2Int(1, 0), new Vector2Int(1, 1) });
            SetOffsets(tool, new[] { new Vector2Int(0, -1), new Vector2Int(0, 1) });
            SetOffsets(rack, new[] { new Vector2Int(0, 1), new Vector2Int(1, 1) });
            Debug.Log("Furniture access configured: shelf on both long sides, tool panel on both faces, parts rack on its +Z front.");
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

        private static void SetOffsets(FurnitureDefinition definition, Vector2Int[] offsets)
        {
            SerializedObject serialized = new(definition);
            SerializedProperty property = serialized.FindProperty("accessCellOffsets");
            bool same = property.arraySize == offsets.Length;
            for (int index = 0; same && index < offsets.Length; index++)
            {
                same = property.GetArrayElementAtIndex(index).vector2IntValue == offsets[index];
            }

            if (same)
            {
                return;
            }

            Undo.RecordObject(definition, "Configure Furniture Access");
            property.arraySize = offsets.Length;
            for (int index = 0; index < offsets.Length; index++)
            {
                property.GetArrayElementAtIndex(index).vector2IntValue = offsets[index];
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
        }
    }
}
