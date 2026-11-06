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
    public static class ConfigureProductCatalogMenu
    {
        private const string ProductDefinitionsFolder = "Assets/_Game/ScriptableObjects/Products";
        private const string ProvisionalPrefabsFolder = "Assets/_Game/Prefabs/Products/Provisional";
        private const string ProvisionalMaterialsFolder = "Assets/_Game/Art/Materials/Products/Provisional";

        private static readonly ProductConfiguration[] ProductDefinitions =
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
                ProductDisplayMode.Bin),
            new(
                "DEF_Cimento50kg",
                "cimento_50kg",
                "Cimento 50 kg",
                "construction_materials",
                ProductSellingUnit.Bag,
                3500,
                new ProductVisualConfiguration(
                    "PF_Cimento50kg_Provisional",
                    PrimitiveType.Cube,
                    new Vector3(0.42f, 0.12f, 0.60f),
                    new Color(0.52f, 0.52f, 0.48f, 1f)),
                ProductDisplayMode.Stack),
            new(
                "DEF_TintaAcrilica18L",
                "tinta_acrilica_18l",
                "Tinta acrílica 18 L",
                "paints",
                ProductSellingUnit.Package,
                15990,
                new ProductVisualConfiguration(
                    "PF_TintaAcrilica18L_Provisional",
                    PrimitiveType.Cylinder,
                    new Vector3(0.19f, 0.17f, 0.19f),
                    new Color(0.18f, 0.42f, 0.78f, 1f)),
                ProductDisplayMode.Stack),
            new(
                "DEF_Trena5m",
                "trena_5m",
                "Trena 5 m",
                "hand_tools",
                ProductSellingUnit.Unit,
                2990,
                new ProductVisualConfiguration(
                    "PF_Trena5m_Provisional",
                    PrimitiveType.Cube,
                    new Vector3(0.18f, 0.06f, 0.13f),
                    new Color(0.98f, 0.72f, 0.12f, 1f)),
                ProductDisplayMode.Hang),
            new(
                "DEF_BuchasPlasticasN8",
                "buchas_plasticas_n8",
                "Buchas plásticas nº 8",
                "fasteners",
                ProductSellingUnit.Package,
                890,
                new ProductVisualConfiguration(
                    "PF_BuchasPlasticasN8_Provisional",
                    PrimitiveType.Cube,
                    new Vector3(0.14f, 0.07f, 0.09f),
                    new Color(0.92f, 0.84f, 0.18f, 1f)),
                ProductDisplayMode.Bin)
        };

        [MenuItem("Tools/Material City Tycoon/Configure Product Definitions")]
        public static void ConfigureProductDefinitions()
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
            EnsureAssetFolder(ProvisionalPrefabsFolder);
            EnsureAssetFolder(ProvisionalMaterialsFolder);

            foreach (ProductConfiguration configuration in ProductDefinitions)
            {
                ConfigureProductDefinition(configuration);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Product definitions configured: {ProductDefinitions.Length} catalog entries.");
        }

        private static bool ValidateConfigurations()
        {
            HashSet<string> productIds = new(StringComparer.Ordinal);

            foreach (ProductConfiguration configuration in ProductDefinitions)
            {
                if (string.IsNullOrWhiteSpace(configuration.ProductId)
                    || string.IsNullOrWhiteSpace(configuration.AssetName)
                    || configuration.AllowedDisplayModes.Length == 0
                    || configuration.InitialSalePriceCents is < 0)
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
            foreach (ProductConfiguration configuration in ProductDefinitions)
            {
                string targetPath = GetAssetPath(configuration);
                UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(targetPath);

                if (mainAsset != null && mainAsset is not ProductDefinition)
                {
                    Debug.LogError($"'{targetPath}' already exists and is not a ProductDefinition.");
                    return false;
                }

                if (configuration.Visual == null)
                {
                    continue;
                }

                UnityEngine.Object prefabAsset = AssetDatabase.LoadMainAssetAtPath(GetPrefabPath(configuration.Visual));
                if (prefabAsset != null && prefabAsset is not GameObject)
                {
                    Debug.LogError($"'{GetPrefabPath(configuration.Visual)}' already exists and is not a prefab.");
                    return false;
                }

                UnityEngine.Object materialAsset = AssetDatabase.LoadMainAssetAtPath(GetMaterialPath(configuration.Visual));
                if (materialAsset != null && materialAsset is not Material)
                {
                    Debug.LogError($"'{GetMaterialPath(configuration.Visual)}' already exists and is not a material.");
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

                foreach (ProductConfiguration configuration in ProductDefinitions)
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

            if (configuration.InitialSalePriceCents.HasValue)
            {
                SerializedProperty salePriceCents = serializedDefinition.FindProperty("salePriceCents");
                if (salePriceCents == null)
                {
                    Debug.LogError("ProductDefinition sale price field does not match the editor configuration tool.", definition);
                    return;
                }

                salePriceCents.longValue = configuration.InitialSalePriceCents.Value;
            }

            if (configuration.Visual != null)
            {
                if (!TryGetOrCreateProvisionalVisual(configuration.Visual, out GameObject displayPrefab))
                {
                    return;
                }

                SerializedProperty displayPrefabProperty = serializedDefinition.FindProperty("displayPrefab");
                if (displayPrefabProperty == null)
                {
                    Debug.LogError("ProductDefinition display prefab field does not match the editor configuration tool.", definition);
                    return;
                }

                if (displayPrefabProperty.objectReferenceValue != null
                    && displayPrefabProperty.objectReferenceValue != displayPrefab)
                {
                    Debug.LogError(
                        $"{configuration.DisplayName} already references a different display prefab; its existing reference was preserved.",
                        definition);
                    return;
                }

                displayPrefabProperty.objectReferenceValue = displayPrefab;
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

        private static string GetPrefabPath(ProductVisualConfiguration visual)
        {
            return $"{ProvisionalPrefabsFolder}/{visual.PrefabName}.prefab";
        }

        private static string GetMaterialPath(ProductVisualConfiguration visual)
        {
            return $"{ProvisionalMaterialsFolder}/MAT_{visual.PrefabName}.mat";
        }

        private static bool TryGetOrCreateProvisionalVisual(ProductVisualConfiguration visual, out GameObject prefab)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetPrefabPath(visual));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GetMaterialPath(visual));
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    Debug.LogError("No supported shader was found for the provisional product material.");
                    return false;
                }

                material = new Material(shader) { name = $"MAT_{visual.PrefabName}" };
                AssetDatabase.CreateAsset(material, GetMaterialPath(visual));
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", visual.Color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", visual.Color);
            EditorUtility.SetDirty(material);

            if (prefab != null)
            {
                return prefab.GetComponentsInChildren<MeshRenderer>(true).Length > 0;
            }

            GameObject source = GameObject.CreatePrimitive(visual.Primitive);
            source.name = visual.PrefabName;
            source.transform.localScale = visual.LocalScale;
            foreach (Collider collider in source.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = source.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            prefab = PrefabUtility.SaveAsPrefabAsset(source, GetPrefabPath(visual));
            UnityEngine.Object.DestroyImmediate(source);
            if (prefab == null)
            {
                Debug.LogError($"Could not create provisional product prefab '{GetPrefabPath(visual)}'.");
                return false;
            }

            return true;
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
                InitialSalePriceCents = null;
                Visual = null;
            }

            public ProductConfiguration(
                string assetName,
                string productId,
                string displayName,
                string categoryId,
                ProductSellingUnit sellingUnit,
                long initialSalePriceCents,
                ProductVisualConfiguration visual,
                params ProductDisplayMode[] allowedDisplayModes)
                : this(assetName, productId, displayName, categoryId, sellingUnit, allowedDisplayModes)
            {
                InitialSalePriceCents = initialSalePriceCents;
                Visual = visual;
            }

            public string AssetName { get; }
            public string ProductId { get; }
            public string DisplayName { get; }
            public string CategoryId { get; }
            public ProductSellingUnit SellingUnit { get; }
            public ProductDisplayMode[] AllowedDisplayModes { get; }
            public long? InitialSalePriceCents { get; }
            public ProductVisualConfiguration Visual { get; }
        }

        private sealed class ProductVisualConfiguration
        {
            public ProductVisualConfiguration(string prefabName, PrimitiveType primitive, Vector3 localScale, Color color)
            {
                PrefabName = prefabName;
                Primitive = primitive;
                LocalScale = localScale;
                Color = color;
            }

            public string PrefabName { get; }
            public PrimitiveType Primitive { get; }
            public Vector3 LocalScale { get; }
            public Color Color { get; }
        }
    }

    public static class ConfigureFurnitureProductCatalogMenu
    {
        private const string StoreScenePath = "Assets/_Game/Scenes/Store.unity";
        private const string ProductDefinitionsFolder = "Assets/_Game/ScriptableObjects/Products";

        [MenuItem("Tools/Material City Tycoon/Configure Furniture Product Catalog")]
        public static void ConfigureFurnitureProductCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before configuring the furniture product catalog.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(StoreScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"Open '{StoreScenePath}' before running this command.");
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
                $"Furniture product catalog configured with {catalog.Count} definitions. Store was marked dirty; save the scene manually when ready.",
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
