using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    /// <summary>Repairs only known user-facing strings written by earlier mojibake generators.</summary>
    public static class RepairPortugueseTextEncodingMenu
    {
        private const string PaintPath = "Assets/_Game/ScriptableObjects/Products/DEF_TintaAcrilica18L.asset";
        private const string WallPlugsPath = "Assets/_Game/ScriptableObjects/Products/DEF_BuchasPlasticasN8.asset";
        private const string GondolaPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_DoubleSidedGondola.asset";

        [MenuItem("Tools/Material City Tycoon/Repair Portuguese Text Encoding")]
        public static void Repair()
        {
            ProductDefinition paint = AssetDatabase.LoadAssetAtPath<ProductDefinition>(PaintPath);
            ProductDefinition wallPlugs = AssetDatabase.LoadAssetAtPath<ProductDefinition>(WallPlugsPath);
            FurnitureDefinition gondola = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(GondolaPath);
            if (paint == null || wallPlugs == null || gondola == null)
            {
                Debug.LogError("Portuguese text repair requires the Paint, Wall Plugs, and Gondola definition assets.");
                return;
            }

            bool changed = SetDisplayName(paint, "Tinta acrílica 18 L")
                | SetDisplayName(wallPlugs, "Buchas plásticas nº 8")
                | SetDisplayName(gondola, "Gôndola Central")
                | SetSlotNames(gondola, new Dictionary<string, string>
                {
                    ["gondola_front_lower"] = "Frente — nível inferior",
                    ["gondola_front_upper"] = "Frente — nível superior",
                    ["gondola_back_lower"] = "Verso — nível inferior",
                    ["gondola_back_upper"] = "Verso — nível superior"
                });

            if (!changed)
            {
                Debug.Log("Portuguese text encoding is already correct.");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Repaired Portuguese text encoding in the affected product and furniture definitions.");
        }

        private static bool SetDisplayName(Object asset, string expectedName)
        {
            SerializedObject serialized = new(asset);
            SerializedProperty displayName = serialized.FindProperty("displayName");
            if (displayName == null || displayName.stringValue == expectedName)
            {
                return false;
            }

            Undo.RecordObject(asset, "Repair Portuguese Text Encoding");
            displayName.stringValue = expectedName;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            return true;
        }

        private static bool SetSlotNames(FurnitureDefinition definition, IReadOnlyDictionary<string, string> namesBySlotId)
        {
            SerializedObject serialized = new(definition);
            SerializedProperty slots = serialized.FindProperty("displaySlots");
            if (slots == null)
            {
                return false;
            }

            bool changed = false;
            for (int index = 0; index < slots.arraySize; index++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(index);
                string slotId = slot.FindPropertyRelative("slotId").stringValue;
                if (!namesBySlotId.TryGetValue(slotId, out string expectedName))
                {
                    continue;
                }

                SerializedProperty displayName = slot.FindPropertyRelative("displayName");
                if (displayName.stringValue == expectedName)
                {
                    continue;
                }

                if (!changed)
                {
                    Undo.RecordObject(definition, "Repair Portuguese Text Encoding");
                    changed = true;
                }

                displayName.stringValue = expectedName;
            }

            if (!changed)
            {
                return false;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            return true;
        }
    }
}
