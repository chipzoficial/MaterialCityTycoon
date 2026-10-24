using System;
using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaterialCityTycoon.EditorTools
{
    public static class ConfigureInitialProductDefinitionsMenu
    {
        private const string ProductDefinitionsFolder = "Assets/_Game/ScriptableObjects/Products";

        private static readonly ProductConfiguration[] InitialProducts =
        {
            new(
                "DEF_Argamassa",
                "argamassa",
                "Argamassa",
                "construction_materials",
                ProductSellingUnit.Bag,
                ProductDisplayMode.Stack),
            new(
                "DEF_Martelo",
                "martelo",
                "Martelo",
                "hand_tools",
                ProductSellingUnit.Unit,
                ProductDisplayMode.Hang),
            new(
                "DEF_Parafusos",
                "parafusos",
                "Parafusos",
                "fasteners",
                ProductSellingUnit.Unit,
                ProductDisplayMode.Bin)
        };

        [MenuItem("Tools/Material City Tycoon/Configure Initial Product Definitions")]
        public static void ConfigureInitialProductDefinitions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Product definitions cannot be configured while Play Mode is active.");
                return;
            }

            if (!ValidateConfigurations() || !ValidateExistingAssets())
            {
                return;
            }

            EnsureAssetFolder(ProductDefinitionsFolder);

            foreach (ProductConfiguration configuration in InitialProducts)
            {
                ConfigureProductDefinition(configuration);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Initial product definitions configured: Argamassa, Martelo and Parafusos.");
        }

        private static bool ValidateConfigurations()
        {
            HashSet<string> productIds = new(StringComparer.Ordinal);

            foreach (ProductConfiguration configuration in InitialProducts)
            {
                if (string.IsNullOrWhiteSpace(configuration.ProductId)
                    || string.IsNullOrWhiteSpace(configuration.AssetName)
                    || configuration.AllowedDisplayModes.Length == 0)
                {
                    Debug.LogError("An initial product definition has an invalid configuration.");
                    return false;
                }

                if (!productIds.Add(configuration.ProductId))
                {
                    Debug.LogError($"Duplicate initial product id '{configuration.ProductId}'.");
                    return false;
                }
            }

            ProductDefinition probe = ScriptableObject.CreateInstance<ProductDefinition>();
            bool hasExpectedProperties = TryFindDefinitionProperties(
                new SerializedObject(probe),
                out _,
                out _,
                out _,
                out _,
                out _);
            UnityEngine.Object.DestroyImmediate(probe);
            return hasExpectedProperties;
        }

        private static bool ValidateExistingAssets()
        {
            foreach (ProductConfiguration configuration in InitialProducts)
            {
                string targetPath = GetAssetPath(configuration);
                UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(targetPath);

                if (mainAsset != null && mainAsset is not ProductDefinition)
                {
                    Debug.LogError($"'{targetPath}' already exists and is not a ProductDefinition.");
                    return false;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ProductDefinition"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ProductDefinition definition = AssetDatabase.LoadAssetAtPath<ProductDefinition>(assetPath);

                if (definition == null)
                {
                    continue;
                }

                foreach (ProductConfiguration configuration in InitialProducts)
                {
                    if (definition.ProductId != configuration.ProductId || assetPath == GetAssetPath(configuration))
                    {
                        continue;
                    }

                    Debug.LogError(
                        $"Product Type Id conflict: '{configuration.ProductId}' is already used by '{assetPath}'. "
                        + "No product definitions were changed.");
                    return false;
                }
            }

            return true;
        }

        private static void ConfigureProductDefinition(ProductConfiguration configuration)
        {
            string assetPath = GetAssetPath(configuration);
            ProductDefinition definition = AssetDatabase.LoadAssetAtPath<ProductDefinition>(assetPath);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ProductDefinition>();
                definition.name = configuration.AssetName;
                AssetDatabase.CreateAsset(definition, assetPath);
                Undo.RegisterCreatedObjectUndo(definition, $"Create {configuration.AssetName}");
            }
            else
            {
                Undo.RecordObject(definition, $"Configure {configuration.AssetName}");
            }

            SerializedObject serializedDefinition = new(definition);
            if (!TryFindDefinitionProperties(
                    serializedDefinition,
                    out SerializedProperty productId,
                    out SerializedProperty displayName,
                    out SerializedProperty categoryId,
                    out SerializedProperty sellingUnit,
                    out SerializedProperty allowedDisplayModes))
            {
                return;
            }

            definition.name = configuration.AssetName;
            productId.stringValue = configuration.ProductId;
            displayName.stringValue = configuration.DisplayName;
            categoryId.stringValue = configuration.CategoryId;
            sellingUnit.enumValueIndex = (int)configuration.SellingUnit;
            allowedDisplayModes.arraySize = configuration.AllowedDisplayModes.Length;

            for (int index = 0; index < configuration.AllowedDisplayModes.Length; index++)
            {
                allowedDisplayModes.GetArrayElementAtIndex(index).enumValueIndex = (int)configuration.AllowedDisplayModes[index];
            }

            serializedDefinition.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
        }

        private static bool TryFindDefinitionProperties(
            SerializedObject serializedDefinition,
            out SerializedProperty productId,
            out SerializedProperty displayName,
            out SerializedProperty categoryId,
            out SerializedProperty sellingUnit,
            out SerializedProperty allowedDisplayModes)
        {
            productId = serializedDefinition.FindProperty("productId");
            displayName = serializedDefinition.FindProperty("displayName");
            categoryId = serializedDefinition.FindProperty("categoryId");
            sellingUnit = serializedDefinition.FindProperty("sellingUnit");
            allowedDisplayModes = serializedDefinition.FindProperty("allowedDisplayModes");

            if (productId != null
                && displayName != null
                && categoryId != null
                && sellingUnit != null
                && allowedDisplayModes != null)
            {
                return true;
            }

            Debug.LogError("ProductDefinition fields do not match the editor configuration tool.");
            return false;
        }

        private static string GetAssetPath(ProductConfiguration configuration)
        {
            return $"{ProductDefinitionsFolder}/{configuration.AssetName}.asset";
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string parentFolder = System.IO.Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(assetFolder);

            if (string.IsNullOrEmpty(parentFolder) || string.IsNullOrEmpty(folderName))
            {
                throw new InvalidOperationException($"Cannot create asset folder '{assetFolder}'.");
            }

            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                EnsureAssetFolder(parentFolder);
            }

            AssetDatabase.CreateFolder(parentFolder, folderName);
        }

        private readonly struct ProductConfiguration
        {
            public ProductConfiguration(
                string assetName,
                string productId,
                string displayName,
                string categoryId,
                ProductSellingUnit sellingUnit,
                params ProductDisplayMode[] allowedDisplayModes)
            {
                AssetName = assetName;
                ProductId = productId;
                DisplayName = displayName;
                CategoryId = categoryId;
                SellingUnit = sellingUnit;
                AllowedDisplayModes = allowedDisplayModes;
            }

            public string AssetName { get; }
            public string ProductId { get; }
            public string DisplayName { get; }
            public string CategoryId { get; }
            public ProductSellingUnit SellingUnit { get; }
            public ProductDisplayMode[] AllowedDisplayModes { get; }
        }
    }

    public static class ConfigureFurnitureProductCatalogMenu
    {
        private const string StorePrototypeScenePath = "Assets/_Game/Scenes/StorePrototype.unity";
        private const string ProductDefinitionsFolder = "Assets/_Game/ScriptableObjects/Products";

        [MenuItem("Tools/Material City Tycoon/Configure Furniture Product Catalog")]
        public static void ConfigureFurnitureProductCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before configuring the furniture product catalog.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(StorePrototypeScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"Open '{StorePrototypeScenePath}' before running this command.");
                return;
            }

            if (!TryGetSinglePlacementController(scene, out FurniturePlacementController placementController)
                || !TryBuildCatalog(placementController, out List<ProductDefinition> catalog))
            {
                return;
            }

            Undo.RecordObject(placementController, "Configure Furniture Product Catalog");
            SerializedObject serializedController = new(placementController);
            SerializedProperty productDefinitions = serializedController.FindProperty("productDefinitions");
            productDefinitions.arraySize = catalog.Count;
            for (int index = 0; index < catalog.Count; index++)
            {
                productDefinitions.GetArrayElementAtIndex(index).objectReferenceValue = catalog[index];
            }

            serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(placementController);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                $"Furniture product catalog configured with {catalog.Count} definitions. StorePrototype was marked dirty; save the scene manually when ready.",
                placementController);
        }

        private static bool TryGetSinglePlacementController(Scene scene, out FurniturePlacementController placementController)
        {
            placementController = null;
            foreach (FurniturePlacementController candidate in Resources.FindObjectsOfTypeAll<FurniturePlacementController>())
            {
                if (candidate.gameObject.scene != scene)
                {
                    continue;
                }

                if (placementController != null)
                {
                    Debug.LogError(
                        $"More than one FurniturePlacementController was found in '{scene.path}'.",
                        placementController);
                    return false;
                }

                placementController = candidate;
            }

            if (placementController != null)
            {
                return true;
            }

            Debug.LogError($"No FurniturePlacementController was found in '{scene.path}'.");
            return false;
        }

        private static bool TryBuildCatalog(
            FurniturePlacementController placementController,
            out List<ProductDefinition> catalog)
        {
            catalog = new List<ProductDefinition>();
            Dictionary<string, ProductDefinition> definitionsById = new(StringComparer.Ordinal);

            SerializedProperty configuredProducts = new SerializedObject(placementController)
                .FindProperty("productDefinitions");
            for (int index = 0; index < configuredProducts.arraySize; index++)
            {
                ProductDefinition definition = configuredProducts.GetArrayElementAtIndex(index)
                    .objectReferenceValue as ProductDefinition;
                if (definition != null && !TryAddDefinition(definition, definitionsById, catalog))
                {
                    return false;
                }
            }

            List<string> assetPaths = new();
            foreach (string guid in AssetDatabase.FindAssets("t:ProductDefinition", new[] { ProductDefinitionsFolder }))
            {
                assetPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            assetPaths.Sort(StringComparer.Ordinal);
            foreach (string assetPath in assetPaths)
            {
                ProductDefinition definition = AssetDatabase.LoadAssetAtPath<ProductDefinition>(assetPath);
                if (definition != null && !TryAddDefinition(definition, definitionsById, catalog))
                {
                    return false;
                }
            }

            if (catalog.Count > 0)
            {
                return true;
            }

            Debug.LogError($"No ProductDefinition assets were found in '{ProductDefinitionsFolder}'.");
            return false;
        }

        private static bool TryAddDefinition(
            ProductDefinition definition,
            Dictionary<string, ProductDefinition> definitionsById,
            List<ProductDefinition> catalog)
        {
            if (!definition.IsValid(out string validationMessage))
            {
                Debug.LogError($"Product definition '{definition.name}' is invalid: {validationMessage}", definition);
                return false;
            }

            if (definitionsById.TryGetValue(definition.ProductId, out ProductDefinition existingDefinition))
            {
                if (existingDefinition == definition)
                {
                    return true;
                }

                Debug.LogError(
                    $"Product Id conflict: '{definition.ProductId}' is used by both '{existingDefinition.name}' and '{definition.name}'.");
                return false;
            }

            definitionsById.Add(definition.ProductId, definition);
            catalog.Add(definition);
            return true;
        }
    }
}
