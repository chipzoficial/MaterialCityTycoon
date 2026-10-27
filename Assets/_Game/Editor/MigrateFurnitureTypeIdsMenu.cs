using System;
using System.Collections.Generic;
using MaterialCityTycoon.Building;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    /// <summary>One-off, idempotent AssetDatabase migration for M0.5b.4.</summary>
    public static class MigrateFurnitureTypeIdsMenu
    {
        private const string DefinitionsFolderPath = "Assets/_Game/ScriptableObjects/Furniture";

        private static readonly TypeIdMigration[] Migrations =
        {
            new("DEF_CommercialShelf.asset", "prototype_shelf", FurnitureTypeIds.CommercialShelf),
            new("DEF_ToolDisplay.asset", "prototype_tool_display", FurnitureTypeIds.ToolDisplay),
            new("DEF_PartsBinRack.asset", "prototype_parts_bin_rack", FurnitureTypeIds.PartsBinRack)
        };

        [MenuItem("Tools/Material City Tycoon/Migrate Furniture Type IDs (M0.5b.4)")]
        public static void Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before migrating Furniture Type IDs.");
                return;
            }

            if (!TryValidateMigrationState(out List<FurnitureDefinition> definitionsToMigrate, out bool isAlreadyMigrated))
            {
                return;
            }

            if (isAlreadyMigrated)
            {
                Debug.Log("Furniture Type ID migration is already complete.");
                return;
            }

            Dictionary<FurnitureDefinition, string> originalIds = new();
            for (int index = 0; index < definitionsToMigrate.Count; index++)
            {
                originalIds.Add(definitionsToMigrate[index], definitionsToMigrate[index].FurnitureTypeId);
            }

            Undo.RecordObjects(definitionsToMigrate.ToArray(), "Migrate Furniture Type IDs");
            try
            {
                for (int index = 0; index < Migrations.Length; index++)
                {
                    SetFurnitureTypeId(definitionsToMigrate[index], Migrations[index].targetId);
                }

                AssetDatabase.SaveAssets();
                Debug.Log("Furniture Type ID migration completed: commercial_shelf, tool_display and parts_bin_rack.");
            }
            catch (Exception exception)
            {
                foreach (KeyValuePair<FurnitureDefinition, string> original in originalIds)
                {
                    SetFurnitureTypeId(original.Key, original.Value);
                }

                AssetDatabase.SaveAssets();
                Debug.LogError($"Furniture Type ID migration failed and was rolled back: {exception.Message}");
            }
        }

        private static bool TryValidateMigrationState(
            out List<FurnitureDefinition> definitionsToMigrate,
            out bool isAlreadyMigrated)
        {
            definitionsToMigrate = new List<FurnitureDefinition>(Migrations.Length);
            int migratedCount = 0;
            foreach (TypeIdMigration migration in Migrations)
            {
                string assetPath = DefinitionsFolderPath + "/" + migration.assetFileName;
                FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(assetPath);
                if (definition == null)
                {
                    Debug.LogError($"Furniture definition was not found at '{assetPath}'. No IDs were changed.");
                    isAlreadyMigrated = false;
                    return false;
                }

                if (definition.FurnitureTypeId == migration.targetId)
                {
                    migratedCount++;
                }
                else if (definition.FurnitureTypeId != migration.legacyId)
                {
                    Debug.LogError(
                        $"Furniture definition '{assetPath}' has unexpected Furniture Type ID '{definition.FurnitureTypeId}'. No IDs were changed.",
                        definition);
                    isAlreadyMigrated = false;
                    return false;
                }

                definitionsToMigrate.Add(definition);
            }

            if (migratedCount != 0 && migratedCount != Migrations.Length)
            {
                Debug.LogError("Furniture Type ID migration is partially applied. Resolve the partial state before running this command again.");
                isAlreadyMigrated = false;
                return false;
            }

            FurnitureDefinition industrialShelf = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(
                DefinitionsFolderPath + "/DEF_IndustrialStorageShelf.asset");
            if (industrialShelf == null || industrialShelf.FurnitureTypeId != FurnitureTypeIds.IndustrialStorageShelf)
            {
                Debug.LogError("Industrial storage shelf must retain Furniture Type ID 'industrial_storage_shelf'. No IDs were changed.", industrialShelf);
                isAlreadyMigrated = false;
                return false;
            }

            if (!HasNoConflictingFurnitureTypeIds(definitionsToMigrate))
            {
                isAlreadyMigrated = false;
                return false;
            }

            isAlreadyMigrated = migratedCount == Migrations.Length;
            return true;
        }

        private static bool HasNoConflictingFurnitureTypeIds(IReadOnlyList<FurnitureDefinition> definitionsToMigrate)
        {
            Dictionary<string, FurnitureDefinition> definitionsById = new(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:FurnitureDefinition", new[] { DefinitionsFolderPath }))
            {
                FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null)
                {
                    continue;
                }

                string typeId = definition.FurnitureTypeId;
                int migrationIndex = FindMigrationIndex(definitionsToMigrate, definition);
                if (migrationIndex >= 0)
                {
                    typeId = Migrations[migrationIndex].targetId;
                }
                else if (IsLegacyTypeId(typeId))
                {
                    Debug.LogError(
                        $"Furniture definition '{definition.name}' still uses legacy Furniture Type ID '{typeId}'. No IDs were changed.",
                        definition);
                    return false;
                }

                if (definitionsById.TryGetValue(typeId, out FurnitureDefinition existingDefinition))
                {
                    Debug.LogError(
                        $"Furniture Type ID '{typeId}' would be used by both '{existingDefinition.name}' and '{definition.name}'. No IDs were changed.",
                        definition);
                    return false;
                }

                definitionsById.Add(typeId, definition);
            }

            return true;
        }

        private static int FindMigrationIndex(IReadOnlyList<FurnitureDefinition> definitionsToMigrate, FurnitureDefinition definition)
        {
            for (int index = 0; index < definitionsToMigrate.Count; index++)
            {
                if (definitionsToMigrate[index] == definition)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool IsLegacyTypeId(string typeId)
        {
            foreach (TypeIdMigration migration in Migrations)
            {
                if (migration.legacyId == typeId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetFurnitureTypeId(FurnitureDefinition definition, string typeId)
        {
            SerializedObject serializedDefinition = new(definition);
            SerializedProperty furnitureTypeId = serializedDefinition.FindProperty("furnitureTypeId");
            if (furnitureTypeId == null)
            {
                throw new InvalidOperationException($"Furniture definition '{definition.name}' does not expose its Furniture Type ID field.");
            }

            furnitureTypeId.stringValue = typeId;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private readonly struct TypeIdMigration
        {
            public readonly string assetFileName;
            public readonly string legacyId;
            public readonly string targetId;

            public TypeIdMigration(string assetFileName, string legacyId, string targetId)
            {
                this.assetFileName = assetFileName;
                this.legacyId = legacyId;
                this.targetId = targetId;
            }
        }
    }
}
