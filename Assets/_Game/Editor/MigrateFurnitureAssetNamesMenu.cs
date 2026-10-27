using System;
using System.Collections.Generic;
using System.IO;
using MaterialCityTycoon.Building;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    /// <summary>One-off, idempotent AssetDatabase migration for M0.5b.2.</summary>
    public static class MigrateFurnitureAssetNamesMenu
    {
        private static readonly AssetMove[] Moves =
        {
            new("Assets/_Game/Prefabs/Building/PB_PrototypeShelf.prefab", "Assets/_Game/Prefabs/Building/PF_CommercialShelf.prefab", typeof(GameObject), false),
            new("Assets/_Game/Prefabs/Building/FB_PrototypeToolDisplay.prefab", "Assets/_Game/Prefabs/Building/PF_ToolDisplay.prefab", typeof(GameObject), false),
            new("Assets/_Game/Prefabs/Building/DEF_PrototypePartsBinRack.prefab", "Assets/_Game/Prefabs/Building/PF_PartsBinRack.prefab", typeof(GameObject), false),
            new("Assets/_Game/Art/Models/Storage/PF_EstanteDeposito.prefab", "Assets/_Game/Prefabs/Storage/PF_IndustrialStorageShelf.prefab", typeof(GameObject), false),
            new("Assets/_Game/ScriptableObjects/Furniture/DEF_PrototypeShelf.asset", "Assets/_Game/ScriptableObjects/Furniture/DEF_CommercialShelf.asset", typeof(FurnitureDefinition), true),
            new("Assets/_Game/ScriptableObjects/Furniture/DEF_PrototypeToolDisplay.asset", "Assets/_Game/ScriptableObjects/Furniture/DEF_ToolDisplay.asset", typeof(FurnitureDefinition), true),
            new("Assets/_Game/ScriptableObjects/Furniture/DEF_PrototypePartsBinRack.asset", "Assets/_Game/ScriptableObjects/Furniture/DEF_PartsBinRack.asset", typeof(FurnitureDefinition), true),
            new("Assets/_Game/ScriptableObjects/Furniture/DEF_EstanteDeposito.asset", "Assets/_Game/ScriptableObjects/Furniture/DEF_IndustrialStorageShelf.asset", typeof(FurnitureDefinition), true),
            new("Assets/_Game/ScriptableObjects/Furniture/DEF_EstanteDeposito_StorageCapacity.asset", "Assets/_Game/ScriptableObjects/Furniture/DEF_IndustrialStorageShelfCapacity.asset", typeof(StorageCapacityDefinition), true)
        };

        [MenuItem("Tools/Material City Tycoon/Migrate Furniture Asset Names (M0.5b.2)")]
        public static void Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before migrating furniture asset names.");
                return;
            }

            if (!TryValidateMigrationState(out bool isAlreadyMigrated))
            {
                return;
            }

            if (isAlreadyMigrated)
            {
                NormalizeDefinitionNames();
                AssetDatabase.SaveAssets();
                Debug.Log("Furniture asset-name migration is already complete.");
                return;
            }

            if (!EnsureDestinationFolders())
            {
                return;
            }

            List<AssetMove> moved = new();
            foreach (AssetMove move in Moves)
            {
                string error = AssetDatabase.MoveAsset(move.sourcePath, move.destinationPath);
                if (string.IsNullOrEmpty(error))
                {
                    moved.Add(move);
                    continue;
                }

                Debug.LogError($"Could not move '{move.sourcePath}' to '{move.destinationPath}': {error}. Attempting rollback.");
                Rollback(moved);
                return;
            }

            NormalizeDefinitionNames();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Furniture asset-name migration completed: {Moves.Length} assets moved with their existing GUIDs.");
        }

        private static bool TryValidateMigrationState(out bool isAlreadyMigrated)
        {
            int completedMoves = 0;
            foreach (AssetMove move in Moves)
            {
                UnityEngine.Object source = AssetDatabase.LoadMainAssetAtPath(move.sourcePath);
                UnityEngine.Object destination = AssetDatabase.LoadMainAssetAtPath(move.destinationPath);
                bool sourceExists = source != null;
                bool destinationExists = destination != null;

                if (sourceExists && destinationExists)
                {
                    Debug.LogError($"Both migration paths exist for '{move.destinationPath}'. Resolve the duplicate before migrating.");
                    isAlreadyMigrated = false;
                    return false;
                }

                if (!sourceExists && !destinationExists)
                {
                    Debug.LogError($"Neither migration path exists for '{move.sourcePath}'. No assets were moved.");
                    isAlreadyMigrated = false;
                    return false;
                }

                UnityEngine.Object asset = destinationExists ? destination : source;
                if (!move.expectedType.IsInstanceOfType(asset))
                {
                    Debug.LogError($"Asset '{AssetDatabase.GetAssetPath(asset)}' is '{asset.GetType().Name}', expected '{move.expectedType.Name}'. No assets were moved.", asset);
                    isAlreadyMigrated = false;
                    return false;
                }

                if (destinationExists)
                {
                    completedMoves++;
                }
            }

            if (completedMoves != 0 && completedMoves != Moves.Length)
            {
                Debug.LogError("Furniture asset-name migration is partially applied. Resolve the partial state before running this command again.");
                isAlreadyMigrated = false;
                return false;
            }

            isAlreadyMigrated = completedMoves == Moves.Length;
            return true;
        }

        private static bool EnsureDestinationFolders()
        {
            const string prefabsFolder = "Assets/_Game/Prefabs";
            const string storageFolder = "Assets/_Game/Prefabs/Storage";
            if (!AssetDatabase.IsValidFolder(prefabsFolder))
            {
                Debug.LogError($"Expected folder '{prefabsFolder}' was not found.");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(storageFolder)
                && string.IsNullOrEmpty(AssetDatabase.CreateFolder(prefabsFolder, "Storage")) )
            {
                Debug.LogError($"Could not create destination folder '{storageFolder}'.");
                return false;
            }

            return true;
        }

        private static void NormalizeDefinitionNames()
        {
            foreach (AssetMove move in Moves)
            {
                if (!move.renameAssetObject)
                {
                    continue;
                }

                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(move.destinationPath);
                string expectedName = Path.GetFileNameWithoutExtension(move.destinationPath);
                if (asset == null || asset.name == expectedName)
                {
                    continue;
                }

                Undo.RecordObject(asset, "Normalize Furniture Definition Name");
                asset.name = expectedName;
                EditorUtility.SetDirty(asset);
            }
        }

        private static void Rollback(List<AssetMove> moved)
        {
            for (int index = moved.Count - 1; index >= 0; index--)
            {
                AssetMove move = moved[index];
                string rollbackError = AssetDatabase.MoveAsset(move.destinationPath, move.sourcePath);
                if (!string.IsNullOrEmpty(rollbackError))
                {
                    Debug.LogError($"Could not roll back '{move.destinationPath}': {rollbackError}.");
                }
            }

            AssetDatabase.Refresh();
        }

        private readonly struct AssetMove
        {
            public readonly string sourcePath;
            public readonly string destinationPath;
            public readonly Type expectedType;
            public readonly bool renameAssetObject;

            public AssetMove(string sourcePath, string destinationPath, Type expectedType, bool renameAssetObject)
            {
                this.sourcePath = sourcePath;
                this.destinationPath = destinationPath;
                this.expectedType = expectedType;
                this.renameAssetObject = renameAssetObject;
            }
        }
    }
}
