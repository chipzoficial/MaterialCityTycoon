using System;
using System.Collections.Generic;
using System.Linq;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaterialCityTycoon.EditorTools
{
    /// <summary>Creates the two provisional display furniture definitions without replacing existing assets.</summary>
    public static class ConfigureDisplayFurnitureMenu
    {
        private const string StoreScenePath = "Assets/_Game/Scenes/Store.unity";
        private const string DefinitionFolder = "Assets/_Game/ScriptableObjects/Furniture";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Building";
        private const string MaterialFolder = "Assets/_Game/Art/Materials/Furniture/Provisional";

        private static readonly FurnitureConfiguration Pallet = new(
            "DEF_PalletBagDisplay", "pallet_bag_display", "Expositor de Sacarias", 1, 1,
            "PF_PalletBagDisplay", new Color(0.40f, 0.24f, 0.10f, 1f),
            new[]
            {
                new SlotConfiguration(
                    "pallet_bags", "Sacarias", Vector3.zero, 40,
                    new PalletBagLayoutConfiguration(new Vector2(1.20f, 1.00f), 0.15f, 0f))
            },
            new[]
            {
                new AccessGroupConfiguration("front", new Vector2Int(0, -1)),
                new AccessGroupConfiguration("back", new Vector2Int(0, 1))
            });

        private static readonly FurnitureConfiguration Gondola = new(
            "DEF_DoubleSidedGondola", "double_sided_gondola", "Gôndola Central", 2, 1,
            "PF_DoubleSidedGondola", new Color(0.30f, 0.34f, 0.38f, 1f),
            new[]
            {
                new SlotConfiguration("gondola_front_lower", "Frente — nível inferior", new Vector3(0f, 0.48f, -0.31f), 4),
                new SlotConfiguration("gondola_front_upper", "Frente — nível superior", new Vector3(0f, 1.10f, -0.31f), 4),
                new SlotConfiguration("gondola_back_lower", "Verso — nível inferior", new Vector3(0f, 0.48f, 0.31f), 4),
                new SlotConfiguration("gondola_back_upper", "Verso — nível superior", new Vector3(0f, 1.10f, 0.31f), 4)
            },
            new[]
            {
                new AccessGroupConfiguration("front", new Vector2Int(0, -1), new Vector2Int(1, -1)),
                new AccessGroupConfiguration("back", new Vector2Int(0, 1), new Vector2Int(1, 1))
            });

        [MenuItem("Tools/Material City Tycoon/Configure Display Furniture")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before configuring display furniture.");
                return;
            }

            Scene storeScene = SceneManager.GetSceneByPath(StoreScenePath);
            if (!storeScene.IsValid() || !storeScene.isLoaded)
            {
                Debug.LogError("Open Store before configuring display furniture.");
                return;
            }

            if (!TryGetPlacementController(storeScene, out FurniturePlacementController placementController)
                || !ValidateProductCatalog()
                || !ValidateTargetAssets(Pallet)
                || !ValidateTargetAssets(Gondola))
            {
                return;
            }

            EnsureFolder(DefinitionFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            if (!TryConfigure(Pallet, out FurnitureDefinition pallet)
                || !TryConfigure(Gondola, out FurnitureDefinition gondola)
                || !TryAppendDefinitions(placementController, storeScene, pallet, gondola))
            {
                return;
            }

            AssetDatabase.SaveAssets();
            ConfigureFurnitureProductCatalogMenu.ConfigureFurnitureProductCatalog();
            ConfigureBuildHudMenu.Configure();
            Debug.Log("Display furniture configured: Expositor de Sacarias and Gôndola Central. Save Store manually.");
        }

        [MenuItem("Tools/Material City Tycoon/Configure Pallet Bag Display")]
        public static void ConfigurePalletBagDisplay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before configuring the pallet bag display.");
                return;
            }

            if (!ValidateProductCatalog() || !ValidateTargetAssets(Pallet))
            {
                return;
            }

            EnsureFolder(DefinitionFolder);
            if (TryConfigure(Pallet, out _))
            {
                AssetDatabase.SaveAssets();
                Debug.Log("Pallet bag display definition configured without modifying its prefab.");
            }
        }

        private static bool ValidateProductCatalog()
        {
            string[] productPaths =
            {
                "Assets/_Game/ScriptableObjects/Products/DEF_Argamassa.asset",
                "Assets/_Game/ScriptableObjects/Products/DEF_Cimento50kg.asset",
                "Assets/_Game/ScriptableObjects/Products/DEF_TintaAcrilica18L.asset"
            };
            foreach (string path in productPaths)
            {
                ProductDefinition product = AssetDatabase.LoadAssetAtPath<ProductDefinition>(path);
                if (product == null || !product.IsValid(out _))
                {
                    Debug.LogError($"Display furniture requires the valid product definition '{path}'.");
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateTargetAssets(FurnitureConfiguration configuration)
        {
            UnityEngine.Object definition = AssetDatabase.LoadMainAssetAtPath(GetDefinitionPath(configuration));
            UnityEngine.Object prefab = AssetDatabase.LoadMainAssetAtPath(GetPrefabPath(configuration));
            UnityEngine.Object material = AssetDatabase.LoadMainAssetAtPath(GetMaterialPath(configuration));
            if ((definition != null && definition is not FurnitureDefinition)
                || (prefab != null && prefab is not GameObject)
                || (material != null && material is not Material))
            {
                Debug.LogError($"One or more target assets for '{configuration.DisplayName}' have an unexpected type.");
                return false;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:FurnitureDefinition"))
            {
                FurnitureDefinition existing = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (existing != null && existing.FurnitureTypeId == configuration.FurnitureTypeId
                    && AssetDatabase.GetAssetPath(existing) != GetDefinitionPath(configuration))
                {
                    Debug.LogError($"Furniture Type Id '{configuration.FurnitureTypeId}' is already used by '{AssetDatabase.GetAssetPath(existing)}'.", existing);
                    return false;
                }
            }

            return true;
        }

        private static bool TryConfigure(FurnitureConfiguration configuration, out FurnitureDefinition definition)
        {
            definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(GetDefinitionPath(configuration));
            if (!TryGetOrCreatePrefab(configuration, out GameObject prefab))
            {
                return false;
            }

            bool created = definition == null;
            if (created)
            {
                definition = ScriptableObject.CreateInstance<FurnitureDefinition>();
                definition.name = configuration.AssetName;
                AssetDatabase.CreateAsset(definition, GetDefinitionPath(configuration));
                Undo.RegisterCreatedObjectUndo(definition, $"Create {configuration.DisplayName} Definition");
            }
            else
            {
                Undo.RecordObject(definition, $"Configure {configuration.DisplayName} Definition");
            }

            SerializedObject serialized = new(definition);
            SerializedProperty typeId = serialized.FindProperty("furnitureTypeId");
            SerializedProperty displayName = serialized.FindProperty("displayName");
            SerializedProperty prefabProperty = serialized.FindProperty("prefab");
            SerializedProperty width = serialized.FindProperty("width");
            SerializedProperty depth = serialized.FindProperty("depth");
            SerializedProperty slots = serialized.FindProperty("displaySlots");
            SerializedProperty offsets = serialized.FindProperty("accessCellOffsets");
            SerializedProperty groups = serialized.FindProperty("accessGroups");
            if (typeId == null || displayName == null || prefabProperty == null || width == null || depth == null
                || slots == null || offsets == null || groups == null)
            {
                Debug.LogError("FurnitureDefinition fields do not match the display furniture configuration tool.", definition);
                return false;
            }

            definition.name = configuration.AssetName;
            typeId.stringValue = configuration.FurnitureTypeId;
            displayName.stringValue = configuration.DisplayName;
            prefabProperty.objectReferenceValue = prefab;
            width.intValue = configuration.Width;
            depth.intValue = configuration.Depth;
            ConfigureSlots(slots, configuration.Slots);
            ConfigureAccess(offsets, groups, configuration.AccessGroups);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition.IsValid(out _);
        }

        private static void ConfigureSlots(SerializedProperty slots, IReadOnlyList<SlotConfiguration> configurations)
        {
            slots.arraySize = configurations.Count;
            for (int index = 0; index < configurations.Count; index++)
            {
                SlotConfiguration configuration = configurations[index];
                SerializedProperty slot = slots.GetArrayElementAtIndex(index);
                slot.FindPropertyRelative("slotId").stringValue = configuration.Id;
                slot.FindPropertyRelative("displayName").stringValue = configuration.DisplayName;
                SerializedProperty modes = slot.FindPropertyRelative("allowedDisplayModes");
                modes.arraySize = 1;
                modes.GetArrayElementAtIndex(0).enumValueIndex = (int)ProductDisplayMode.Stack;
                slot.FindPropertyRelative("localDisplayPosition").vector3Value = configuration.Position;
                slot.FindPropertyRelative("capacityUnits").intValue = configuration.Capacity;
                SerializedProperty palletLayout = slot.FindPropertyRelative("palletBagDisplayLayout");
                if (palletLayout != null)
                {
                    Vector2 surfaceSize = configuration.PalletBagLayout?.SurfaceSize ?? Vector2.zero;
                    float surfaceHeight = configuration.PalletBagLayout?.SurfaceHeight ?? 0f;
                    float spacing = configuration.PalletBagLayout?.Spacing ?? 0f;
                    palletLayout.FindPropertyRelative("surfaceSize").vector2Value = surfaceSize;
                    palletLayout.FindPropertyRelative("surfaceHeight").floatValue = surfaceHeight;
                    palletLayout.FindPropertyRelative("spacing").floatValue = spacing;
                }
            }
        }

        private static void ConfigureAccess(SerializedProperty offsets, SerializedProperty groups,
            IReadOnlyList<AccessGroupConfiguration> configurations)
        {
            List<Vector2Int> flattened = new();
            foreach (AccessGroupConfiguration group in configurations) flattened.AddRange(group.Cells);
            offsets.arraySize = flattened.Count;
            for (int index = 0; index < flattened.Count; index++) offsets.GetArrayElementAtIndex(index).vector2IntValue = flattened[index];
            groups.arraySize = configurations.Count;
            for (int index = 0; index < configurations.Count; index++)
            {
                SerializedProperty group = groups.GetArrayElementAtIndex(index);
                group.FindPropertyRelative("groupId").stringValue = configurations[index].Id;
                SerializedProperty cells = group.FindPropertyRelative("cellOffsets");
                cells.arraySize = configurations[index].Cells.Length;
                for (int cell = 0; cell < cells.arraySize; cell++) cells.GetArrayElementAtIndex(cell).vector2IntValue = configurations[index].Cells[cell];
            }
        }

        private static bool TryAppendDefinitions(FurniturePlacementController controller, Scene scene,
            params FurnitureDefinition[] additions)
        {
            SerializedObject serialized = new(controller);
            SerializedProperty definitions = serialized.FindProperty("furnitureDefinitions");
            if (definitions == null) return false;
            List<FurnitureDefinition> ordered = new();
            HashSet<string> ids = new(StringComparer.Ordinal);
            for (int index = 0; index < definitions.arraySize; index++)
            {
                FurnitureDefinition definition = definitions.GetArrayElementAtIndex(index).objectReferenceValue as FurnitureDefinition;
                if (definition != null && ids.Add(definition.FurnitureTypeId)) ordered.Add(definition);
            }

            foreach (FurnitureDefinition addition in additions)
            {
                if (addition == null || !ids.Add(addition.FurnitureTypeId)) continue;
                ordered.Add(addition);
            }

            bool changed = definitions.arraySize != ordered.Count;
            for (int index = 0; !changed && index < ordered.Count; index++)
                changed = definitions.GetArrayElementAtIndex(index).objectReferenceValue != ordered[index];
            if (!changed) return true;
            Undo.RecordObject(controller, "Add Display Furniture Definitions");
            definitions.arraySize = ordered.Count;
            for (int index = 0; index < ordered.Count; index++) definitions.GetArrayElementAtIndex(index).objectReferenceValue = ordered[index];
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static bool TryGetOrCreatePrefab(FurnitureConfiguration configuration, out GameObject prefab)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetPrefabPath(configuration));
            if (prefab != null)
            {
                return prefab.GetComponentsInChildren<MeshRenderer>(true).Length > 0 && prefab.GetComponentsInChildren<Collider>(true).Length == 0;
            }

            if (configuration.FurnitureTypeId == FurnitureTypeIds.PalletBagDisplay)
            {
                Debug.LogError("PF_PalletBagDisplay must be supplied by the imported Blender model; the configuration tool will not create a replacement.");
                return false;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(GetMaterialPath(configuration));
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) return false;
                material = new Material(shader) { name = $"MAT_{configuration.PrefabName}" };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", configuration.Color);
                else if (material.HasProperty("_Color")) material.SetColor("_Color", configuration.Color);
                AssetDatabase.CreateAsset(material, GetMaterialPath(configuration));
            }

            GameObject root = new(configuration.PrefabName);
            BuildGondola(root.transform, material);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, GetPrefabPath(configuration));
            UnityEngine.Object.DestroyImmediate(root);
            return prefab != null;
        }

        private static void BuildGondola(Transform root, Material material)
        {
            AddCube(root, "Base", new Vector3(0f, 0.06f, 0f), new Vector3(1.9f, 0.12f, 0.90f), material);
            AddCube(root, "Spine", new Vector3(0f, 0.82f, 0f), new Vector3(1.84f, 1.52f, 0.08f), material);
            foreach (float y in new[] { 0.36f, 0.96f })
            {
                AddCube(root, "FrontShelf", new Vector3(0f, y, -0.28f), new Vector3(1.82f, 0.06f, 0.30f), material);
                AddCube(root, "BackShelf", new Vector3(0f, y, 0.28f), new Vector3(1.82f, 0.06f, 0.30f), material);
            }
        }

        private static void AddCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        }

        private static bool TryGetPlacementController(Scene scene, out FurniturePlacementController controller)
        {
            FurniturePlacementController[] controllers = Resources.FindObjectsOfTypeAll<FurniturePlacementController>()
                .Where(candidate => candidate.gameObject.scene == scene).ToArray();
            controller = controllers.Length == 1 ? controllers[0] : null;
            if (controller == null) Debug.LogError("Display furniture configuration requires exactly one FurniturePlacementController in Store.");
            return controller != null;
        }

        private static string GetDefinitionPath(FurnitureConfiguration configuration) => $"{DefinitionFolder}/{configuration.AssetName}.asset";
        private static string GetPrefabPath(FurnitureConfiguration configuration) => $"{PrefabFolder}/{configuration.PrefabName}.prefab";
        private static string GetMaterialPath(FurnitureConfiguration configuration) => $"{MaterialFolder}/MAT_{configuration.PrefabName}.mat";

        private static void EnsureFolder(string folderPath)
        {
            string current = "Assets";
            foreach (string part in folderPath.Split('/').Skip(1))
            {
                string next = $"{current}/{part}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }

        private readonly struct FurnitureConfiguration
        {
            public FurnitureConfiguration(string assetName, string furnitureTypeId, string displayName, int width, int depth,
                string prefabName, Color color, SlotConfiguration[] slots, AccessGroupConfiguration[] accessGroups)
            {
                AssetName = assetName; FurnitureTypeId = furnitureTypeId; DisplayName = displayName; Width = width; Depth = depth;
                PrefabName = prefabName; Color = color; Slots = slots; AccessGroups = accessGroups;
            }
            public string AssetName { get; }
            public string FurnitureTypeId { get; }
            public string DisplayName { get; }
            public int Width { get; }
            public int Depth { get; }
            public string PrefabName { get; }
            public Color Color { get; }
            public SlotConfiguration[] Slots { get; }
            public AccessGroupConfiguration[] AccessGroups { get; }
        }

        private readonly struct SlotConfiguration
        {
            public SlotConfiguration(string id, string displayName, Vector3 position, int capacity,
                PalletBagLayoutConfiguration palletBagLayout = null)
            {
                Id = id;
                DisplayName = displayName;
                Position = position;
                Capacity = capacity;
                PalletBagLayout = palletBagLayout;
            }
            public string Id { get; }
            public string DisplayName { get; }
            public Vector3 Position { get; }
            public int Capacity { get; }
            public PalletBagLayoutConfiguration PalletBagLayout { get; }
        }

        private sealed class PalletBagLayoutConfiguration
        {
            public PalletBagLayoutConfiguration(Vector2 surfaceSize, float surfaceHeight, float spacing)
            {
                SurfaceSize = surfaceSize;
                SurfaceHeight = surfaceHeight;
                Spacing = spacing;
            }

            public Vector2 SurfaceSize { get; }
            public float SurfaceHeight { get; }
            public float Spacing { get; }
        }

        private readonly struct AccessGroupConfiguration
        {
            public AccessGroupConfiguration(string id, params Vector2Int[] cells) { Id = id; Cells = cells; }
            public string Id { get; }
            public Vector2Int[] Cells { get; }
        }
    }
}
