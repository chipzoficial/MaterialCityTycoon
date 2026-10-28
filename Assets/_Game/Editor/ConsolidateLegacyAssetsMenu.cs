using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    /// <summary>One-off, reversible AssetDatabase consolidation for M0.5c.2.</summary>
    public static class ConsolidateLegacyAssetsMenu
    {
        private const string LegacyShelfPath = "Assets/_Game/Prefabs/Building/PrototypeShelf.prefab";
        private const string LegacyShelfReviewPath = "Assets/_Game/Prefabs/Building/Review/M0.5c.2/PrototypeShelf.prefab";
        private const string ActiveMortarMaterialPath = "Assets/_Game/Art/Textures/Products/Materials/TEX_SacoArgamassa20kg.mat";
        private const string InactiveMortarMaterialPath = "Assets/_Game/Art/Materials/Products/MAT_SacoArgamassa20kg.mat";
        private const string InactiveMortarMaterialReviewPath = "Assets/_Game/Art/Materials/Products/Review/M0.5c.2/MAT_SacoArgamassa20kg.mat";
        private const string ConsolidatedMortarMaterialPath = "Assets/_Game/Art/Materials/Products/MAT_SacoArgamassa20kg.mat";
        private const string MortarProductPrefabPath = "Assets/_Game/Prefabs/Products/PF_SacoArgamassa20kg.prefab";

        [MenuItem("Tools/Material City Tycoon/Consolidate Legacy Assets (M0.5c.2)")]
        public static void Consolidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before consolidating legacy assets.");
                return;
            }

            if (!TryValidateInitialState(out ConsolidationState state, out bool isAlreadyConsolidated))
            {
                return;
            }

            if (isAlreadyConsolidated)
            {
                Debug.Log("Legacy asset consolidation is already complete.");
                return;
            }

            if (!EnsureReviewFolders())
            {
                return;
            }

            AssetMove[] moves =
            {
                new(InactiveMortarMaterialPath, InactiveMortarMaterialReviewPath),
                new(ActiveMortarMaterialPath, ConsolidatedMortarMaterialPath),
                new(LegacyShelfPath, LegacyShelfReviewPath)
            };
            List<AssetMove> completedMoves = new();
            foreach (AssetMove move in moves)
            {
                string error = AssetDatabase.MoveAsset(move.sourcePath, move.destinationPath);
                if (string.IsNullOrEmpty(error))
                {
                    completedMoves.Add(move);
                    continue;
                }

                Debug.LogError($"Could not move '{move.sourcePath}' to '{move.destinationPath}': {error}. Attempting rollback.");
                Rollback(completedMoves);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!TryValidateFinalState(state))
            {
                Debug.LogError("Legacy asset consolidation did not pass post-move validation. Attempting rollback.");
                Rollback(completedMoves);
                return;
            }

            Debug.Log(
                "Legacy asset consolidation completed.\n"
                + $"Isolated for review: {LegacyShelfPath} -> {LegacyShelfReviewPath}\n"
                + $"Isolated for review: {InactiveMortarMaterialPath} -> {InactiveMortarMaterialReviewPath}\n"
                + $"Standardized active material: {ActiveMortarMaterialPath} -> {ConsolidatedMortarMaterialPath} "
                + "(original GUID preserved).");
        }

        private static bool TryValidateInitialState(out ConsolidationState state, out bool isAlreadyConsolidated)
        {
            state = default;
            isAlreadyConsolidated = false;
            bool initialLayout = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyShelfPath) != null
                && AssetDatabase.LoadAssetAtPath<Material>(ActiveMortarMaterialPath) != null
                && AssetDatabase.LoadAssetAtPath<Material>(InactiveMortarMaterialPath) != null
                && AssetDatabase.LoadMainAssetAtPath(LegacyShelfReviewPath) == null
                && AssetDatabase.LoadMainAssetAtPath(InactiveMortarMaterialReviewPath) == null;
            bool finalLayout = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyShelfReviewPath) != null
                && AssetDatabase.LoadAssetAtPath<Material>(ConsolidatedMortarMaterialPath) != null
                && AssetDatabase.LoadAssetAtPath<Material>(InactiveMortarMaterialReviewPath) != null
                && AssetDatabase.LoadMainAssetAtPath(LegacyShelfPath) == null
                && AssetDatabase.LoadMainAssetAtPath(ActiveMortarMaterialPath) == null;

            if (finalLayout)
            {
                isAlreadyConsolidated = true;
                return true;
            }

            if (!initialLayout)
            {
                Debug.LogError("Legacy asset consolidation is in an unexpected or partially applied state. No assets were moved.");
                return false;
            }

            GameObject legacyShelf = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyShelfPath);
            Material activeMaterial = AssetDatabase.LoadAssetAtPath<Material>(ActiveMortarMaterialPath);
            Material inactiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(InactiveMortarMaterialPath);
            if (!IsUnused(legacyShelf, LegacyShelfPath)
                || !IsUnused(inactiveMaterial, InactiveMortarMaterialPath)
                || !TryValidateActiveMortarMaterial(activeMaterial))
            {
                return false;
            }

            state = new ConsolidationState(
                AssetDatabase.AssetPathToGUID(LegacyShelfPath),
                AssetDatabase.AssetPathToGUID(ActiveMortarMaterialPath),
                AssetDatabase.AssetPathToGUID(InactiveMortarMaterialPath),
                new MaterialSnapshot(activeMaterial));
            return true;
        }

        private static bool TryValidateFinalState(ConsolidationState state)
        {
            GameObject legacyShelf = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyShelfReviewPath);
            Material activeMaterial = AssetDatabase.LoadAssetAtPath<Material>(ConsolidatedMortarMaterialPath);
            Material inactiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(InactiveMortarMaterialReviewPath);
            if (legacyShelf == null || activeMaterial == null || inactiveMaterial == null
                || AssetDatabase.AssetPathToGUID(LegacyShelfReviewPath) != state.legacyShelfGuid
                || AssetDatabase.AssetPathToGUID(ConsolidatedMortarMaterialPath) != state.activeMaterialGuid
                || AssetDatabase.AssetPathToGUID(InactiveMortarMaterialReviewPath) != state.inactiveMaterialGuid
                || !state.activeMaterialSnapshot.Matches(activeMaterial)
                || !TryValidateActiveMortarMaterial(activeMaterial))
            {
                return false;
            }

            return IsUnused(legacyShelf, LegacyShelfReviewPath) && IsUnused(inactiveMaterial, InactiveMortarMaterialReviewPath);
        }

        private static bool TryValidateActiveMortarMaterial(Material activeMaterial)
        {
            if (activeMaterial == null || activeMaterial.shader == null || activeMaterial.GetTexture("_BaseMap") == null)
            {
                Debug.LogError("The active mortar material is incomplete. No assets were moved.", activeMaterial);
                return false;
            }

            GameObject mortarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MortarProductPrefabPath);
            bool isUsedByPrefab = mortarPrefab != null && mortarPrefab.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer.sharedMaterials.Any(material => material == activeMaterial));
            if (!isUsedByPrefab)
            {
                Debug.LogError("The mortar product prefab does not use the expected active mortar material. No assets were moved.", mortarPrefab);
                return false;
            }

            return true;
        }

        private static bool IsUnused(UnityEngine.Object asset, string assetPath)
        {
            if (asset == null)
            {
                Debug.LogError($"Expected asset '{assetPath}' was not found.");
                return false;
            }

            List<string> referencers = GetReferencingAssetPaths(assetPath);
            if (referencers.Count == 0)
            {
                return true;
            }

            Debug.LogError($"Asset '{assetPath}' is still referenced by: {string.Join(", ", referencers)}. No assets were moved.", asset);
            return false;
        }

        private static List<string> GetReferencingAssetPaths(string targetPath)
        {
            List<string> referencers = new();
            foreach (string assetPath in AssetDatabase.GetAllAssetPaths())
            {
                if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath == targetPath)
                {
                    continue;
                }

                if (AssetDatabase.GetDependencies(assetPath, false).Contains(targetPath))
                {
                    referencers.Add(assetPath);
                }
            }

            referencers.Sort(StringComparer.Ordinal);
            return referencers;
        }

        private static bool EnsureReviewFolders()
        {
            return EnsureFolder("Assets/_Game/Prefabs/Building", "Review")
                && EnsureFolder("Assets/_Game/Prefabs/Building/Review", "M0.5c.2")
                && EnsureFolder("Assets/_Game/Art/Materials/Products", "Review")
                && EnsureFolder("Assets/_Game/Art/Materials/Products/Review", "M0.5c.2");
        }

        private static bool EnsureFolder(string parentPath, string folderName)
        {
            string folderPath = parentPath + "/" + folderName;
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(AssetDatabase.CreateFolder(parentPath, folderName)))
            {
                return true;
            }

            Debug.LogError($"Could not create review folder '{folderPath}'.");
            return false;
        }

        private static void Rollback(List<AssetMove> completedMoves)
        {
            for (int index = completedMoves.Count - 1; index >= 0; index--)
            {
                AssetMove move = completedMoves[index];
                string error = AssetDatabase.MoveAsset(move.destinationPath, move.sourcePath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"Could not roll back '{move.destinationPath}': {error}.");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private readonly struct AssetMove
        {
            public readonly string sourcePath;
            public readonly string destinationPath;

            public AssetMove(string sourcePath, string destinationPath)
            {
                this.sourcePath = sourcePath;
                this.destinationPath = destinationPath;
            }
        }

        private readonly struct ConsolidationState
        {
            public readonly string legacyShelfGuid;
            public readonly string activeMaterialGuid;
            public readonly string inactiveMaterialGuid;
            public readonly MaterialSnapshot activeMaterialSnapshot;

            public ConsolidationState(
                string legacyShelfGuid,
                string activeMaterialGuid,
                string inactiveMaterialGuid,
                MaterialSnapshot activeMaterialSnapshot)
            {
                this.legacyShelfGuid = legacyShelfGuid;
                this.activeMaterialGuid = activeMaterialGuid;
                this.inactiveMaterialGuid = inactiveMaterialGuid;
                this.activeMaterialSnapshot = activeMaterialSnapshot;
            }
        }

        private readonly struct MaterialSnapshot
        {
            private readonly Shader shader;
            private readonly Texture baseMap;
            private readonly Color baseColor;
            private readonly float smoothness;
            private readonly float metallic;

            public MaterialSnapshot(Material material)
            {
                shader = material.shader;
                baseMap = material.GetTexture("_BaseMap");
                baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0f;
                metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            }

            public bool Matches(Material material)
            {
                return material != null
                    && material.shader == shader
                    && material.GetTexture("_BaseMap") == baseMap
                    && (!material.HasProperty("_BaseColor") || material.GetColor("_BaseColor") == baseColor)
                    && (!material.HasProperty("_Smoothness") || Mathf.Approximately(material.GetFloat("_Smoothness"), smoothness))
                    && (!material.HasProperty("_Metallic") || Mathf.Approximately(material.GetFloat("_Metallic"), metallic));
            }
        }
    }
}
