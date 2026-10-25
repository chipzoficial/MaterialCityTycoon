using System.Collections.Generic;
using MaterialCityTycoon.Building;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaterialCityTycoon.EditorTools
{
    public static class ConfigurePrototypeShelfMenu
    {
        private const string StorePrototypeScenePath = "Assets/_Game/Scenes/StorePrototype.unity";
        private const string DefinitionFolderPath = "Assets/_Game/ScriptableObjects/Furniture";
        private const string PrototypeShelfDefinitionAssetPath = DefinitionFolderPath + "/DEF_PrototypeShelf.asset";
        private const string PrototypeShelfFurnitureTypeId = "prototype_shelf";
        private const string PrototypeToolDisplayDefinitionAssetPath = DefinitionFolderPath + "/DEF_PrototypeToolDisplay.asset";
        private const string PrototypeToolDisplayFurnitureTypeId = "prototype_tool_display";
        private const string ToolDisplayPanelName = "Panel_01";
        private const string ToolDisplayBaseName = "Base_01";
        private const float BoundsTolerance = 0.0001f;
        private const float FootprintTolerance = 0.001f;

        private static readonly FurnitureConfiguration PrototypeShelf = new(
            "Assets/_Game/Prefabs/Building/PB_PrototypeShelf.prefab",
            PrototypeShelfDefinitionAssetPath,
            "DEF_PrototypeShelf",
            PrototypeShelfFurnitureTypeId,
            "Prateleira",
            1,
            2);

        private static readonly FurnitureConfiguration PrototypeToolDisplay = new(
            "Assets/_Game/Prefabs/Building/FB_PrototypeToolDisplay.prefab",
            PrototypeToolDisplayDefinitionAssetPath,
            "DEF_PrototypeToolDisplay",
            PrototypeToolDisplayFurnitureTypeId,
            "Expositor de ferramentas",
            1,
            1);

        private static readonly FurnitureConfiguration PrototypePartsBinRack = new(
            "Assets/_Game/Prefabs/Building/DEF_PrototypePartsBinRack.prefab",
            DefinitionFolderPath + "/DEF_PrototypePartsBinRack.asset",
            "DEF_PrototypePartsBinRack",
            "prototype_parts_bin_rack",
            "Organizador de ferragens",
            2,
            1);

        private static readonly FurnitureConfiguration IndustrialStorageShelf = new(
            "Assets/_Game/Art/Models/Storage/PF_EstanteDeposito.prefab",
            DefinitionFolderPath + "/DEF_EstanteDeposito.asset",
            "DEF_EstanteDeposito",
            "industrial_storage_shelf",
            "Estante de Depósito",
            2,
            1,
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1),
                new Vector2Int(0, -1), new Vector2Int(1, -1) });

        [MenuItem("Tools/Material City Tycoon/Configure Prototype Shelf")]
        private static void ConfigurePrototypeShelf()
        {
            ConfigureFurnitureDefinition(PrototypeShelf);
        }

        [MenuItem("Tools/Material City Tycoon/Configure Tool Display")]
        private static void ConfigureToolDisplay()
        {
            if (!CanConfigureInEditor()
                || !TryGetConfiguredDefinition(
                    PrototypeShelfDefinitionAssetPath,
                    PrototypeShelfFurnitureTypeId,
                    out FurnitureDefinition shelfDefinition))
            {
                return;
            }

            ConfigureFurnitureDefinition(PrototypeToolDisplay, shelfDefinition);
        }

        [MenuItem("Tools/Material City Tycoon/Configure Parts Bin Rack")]
        private static void ConfigurePartsBinRack()
        {
            if (!CanConfigureInEditor()
                || !TryGetConfiguredDefinition(
                    PrototypeShelfDefinitionAssetPath,
                    PrototypeShelfFurnitureTypeId,
                    out FurnitureDefinition shelfDefinition)
                || !TryGetConfiguredDefinition(
                    PrototypeToolDisplayDefinitionAssetPath,
                    PrototypeToolDisplayFurnitureTypeId,
                    out FurnitureDefinition toolDisplayDefinition))
            {
                return;
            }

            ConfigureFurnitureDefinition(
                PrototypePartsBinRack,
                shelfDefinition,
                toolDisplayDefinition);
        }

        [MenuItem("Tools/Material City Tycoon/Configure Industrial Storage Shelf")]
        private static void ConfigureIndustrialStorageShelf()
        {
            if (!CanConfigureInEditor()
                || !TryGetConfiguredDefinition(IndustrialStorageShelf.definitionAssetPath, IndustrialStorageShelf.furnitureTypeId, out FurnitureDefinition definition)
                || !TryGetFurniturePrefab(IndustrialStorageShelf, out GameObject prefab))
            {
                return;
            }

            if (definition.Prefab != prefab || definition.Width != 2 || definition.Depth != 1)
            {
                Debug.LogError("Industrial shelf definition must reference the existing prefab and use a 2x1 footprint.", definition);
                return;
            }

            if (prefab.transform.localScale != Vector3.one || !TryGetCombinedMeshRendererBounds(prefab.transform, out Bounds bounds))
            {
                Debug.LogError("Industrial shelf requires a unit-scale root and measurable MeshRenderers.", prefab);
                return;
            }

            Debug.Log($"Industrial shelf local bounds: center {bounds.center.ToString("F3")}, min {bounds.min.ToString("F3")}, size {bounds.size.ToString("F3")}; operational front is +Z.", prefab);
            if (Mathf.Abs(bounds.center.x) > FootprintTolerance || Mathf.Abs(bounds.center.z) > FootprintTolerance
                || Mathf.Abs(bounds.min.y) > FootprintTolerance || bounds.size.x > 2f + FootprintTolerance || bounds.size.z > 1f + FootprintTolerance)
            {
                Debug.LogWarning("Industrial shelf visual bounds are offset from its pivot or exceed the 2x1 footprint; the prefab was not modified.", prefab);
            }

            SerializedObject serialized = new(definition);
            SerializedProperty offsets = serialized.FindProperty("accessCellOffsets");
            bool changed = offsets.arraySize != IndustrialStorageShelf.accessOffsets.Length;
            for (int i = 0; !changed && i < offsets.arraySize; i++)
            {
                changed = offsets.GetArrayElementAtIndex(i).vector2IntValue != IndustrialStorageShelf.accessOffsets[i];
            }

            if (changed)
            {
                Undo.RecordObject(definition, "Configure Industrial Shelf Access");
                offsets.arraySize = IndustrialStorageShelf.accessOffsets.Length;
                for (int i = 0; i < offsets.arraySize; i++)
                {
                    offsets.GetArrayElementAtIndex(i).vector2IntValue = IndustrialStorageShelf.accessOffsets[i];
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }

            Debug.Log("Industrial shelf access configured on both long faces; the scene was not modified.", definition);
        }

        [MenuItem("Tools/Material City Tycoon/Center Parts Bin Rack on Grid")]
        private static void CenterPartsBinRackOnGrid()
        {
            if (!CanConfigureInEditor()
                || !TryGetFurniturePrefab(PrototypePartsBinRack, out GameObject prefab))
            {
                return;
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform root = prefabContents.transform;
                if (root.GetComponent<MeshRenderer>() != null)
                {
                    Debug.LogError(
                        "Parts bin rack prefab root must not contain a MeshRenderer because only child transforms are centered.",
                        prefab);
                    return;
                }

                if (!TryGetCombinedMeshRendererBounds(root, out Bounds boundsBefore))
                {
                    Debug.LogError("Parts bin rack prefab does not contain MeshRenderers to center.", prefab);
                    return;
                }

                Vector3 offset = new(-boundsBefore.center.x, -boundsBefore.min.y, -boundsBefore.center.z);
                if (offset.sqrMagnitude > BoundsTolerance * BoundsTolerance)
                {
                    Transform[] directChildren = GetDirectChildren(root);
                    Undo.RecordObjects(directChildren, "Center Parts Bin Rack on Grid");
                    foreach (Transform child in directChildren)
                    {
                        child.localPosition += offset;
                    }

                    PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                }

                if (!TryGetCombinedMeshRendererBounds(root, out Bounds boundsAfter))
                {
                    Debug.LogError("Unable to calculate parts bin rack bounds after centering.", prefab);
                    return;
                }

                LogPartsBinRackBounds(prefab, root, boundsBefore, boundsAfter);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        [MenuItem("Tools/Material City Tycoon/Center Tool Display on Grid")]
        private static void CenterToolDisplayOnGrid()
        {
            if (!CanConfigureInEditor()
                || !TryGetFurniturePrefab(PrototypeToolDisplay, out GameObject prefab))
            {
                return;
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform root = prefabContents.transform;
                if (!TryGetDirectChild(root, ToolDisplayPanelName, out Transform panel)
                    || !TryGetDirectChild(root, ToolDisplayBaseName, out Transform baseTransform))
                {
                    return;
                }

                bool changed = CenterLocalPosition(root);
                changed |= CenterLocalPosition(panel);
                changed |= CenterLocalPosition(baseTransform);
                if (!changed)
                {
                    Debug.Log("Tool display prefab is already centered on its grid pivot.", prefab);
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                Debug.Log("Tool display prefab centered on its grid pivot.", prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static void ConfigureFurnitureDefinition(
            FurnitureConfiguration configuration,
            params FurnitureDefinition[] requiredFirstDefinitions)
        {
            if (!CanConfigureInEditor())
            {
                return;
            }

            if (!TryGetStorePrototypeScene(out Scene storePrototypeScene)
                || !TryGetFurniturePrefab(configuration, out GameObject prefab)
                || !TryGetPlacementController(storePrototypeScene, out FurniturePlacementController controller)
                || !TryGetControllerDefinitions(controller, out SerializedObject controllerSerialized, out SerializedProperty definitionsProperty))
            {
                return;
            }

            UnityEngine.Object existingAsset = AssetDatabase.LoadMainAssetAtPath(configuration.definitionAssetPath);
            if (existingAsset != null && existingAsset is not FurnitureDefinition)
            {
                Debug.LogError(
                    $"Cannot configure '{configuration.definitionAssetName}' because '{configuration.definitionAssetPath}' is occupied by '{existingAsset.GetType().Name}'.",
                    existingAsset);
                return;
            }

            FurnitureDefinition definition = existingAsset as FurnitureDefinition;
            if (configuration.accessOffsets != null && definition != null
                && !string.IsNullOrWhiteSpace(definition.FurnitureTypeId)
                && definition.FurnitureTypeId != configuration.furnitureTypeId)
            {
                Debug.LogError($"Cannot overwrite a different furniture definition at '{configuration.definitionAssetPath}'.", definition);
                return;
            }
            bool createDefinition = definition == null;
            if (createDefinition)
            {
                definition = ScriptableObject.CreateInstance<FurnitureDefinition>();
            }

            if (!TryGetDefinitionProperties(definition, out SerializedObject definitionSerialized, out DefinitionProperties definitionProperties))
            {
                if (createDefinition)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }

                return;
            }

            if (!CanUseFurnitureTypeId(
                    definitionsProperty,
                    definition,
                    configuration.furnitureTypeId,
                    out string validationError))
            {
                Debug.LogError(validationError, controller);

                if (createDefinition)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }

                return;
            }

            if (createDefinition)
            {
                EnsureAssetFolder(DefinitionFolderPath);
                definition.name = configuration.definitionAssetName;
                AssetDatabase.CreateAsset(definition, configuration.definitionAssetPath);
                Undo.RegisterCreatedObjectUndo(definition, $"Create {configuration.displayName} Definition");
            }
            else
            {
                Undo.RecordObject(definition, $"Configure {configuration.displayName} Definition");
            }

            definition.name = configuration.definitionAssetName;
            definitionSerialized.Update();
            definitionProperties.furnitureTypeId.stringValue = configuration.furnitureTypeId;
            definitionProperties.displayName.stringValue = configuration.displayName;
            definitionProperties.prefab.objectReferenceValue = prefab;
            definitionProperties.width.intValue = configuration.width;
            definitionProperties.depth.intValue = configuration.depth;
            if (configuration.accessOffsets != null)
            {
                SerializedProperty offsets = definitionSerialized.FindProperty("accessCellOffsets");
                offsets.arraySize = configuration.accessOffsets.Length;
                for (int i = 0; i < configuration.accessOffsets.Length; i++)
                {
                    offsets.GetArrayElementAtIndex(i).vector2IntValue = configuration.accessOffsets[i];
                }
            }
            definitionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);

            Undo.RecordObject(controller, $"Assign {configuration.displayName} Definition");
            List<FurnitureDefinition> definitionsInRequiredOrder = new(requiredFirstDefinitions)
            {
                definition
            };
            bool sceneChanged = EnsureDefinitionOrder(
                definitionsProperty,
                definitionsInRequiredOrder.ToArray());
            if (sceneChanged)
            {
                controllerSerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(storePrototypeScene);
            }

            AssetDatabase.SaveAssetIfDirty(definition);
            Debug.Log(
                $"{configuration.displayName} definition configured. StorePrototype was marked dirty when its Furniture Definitions list changed; save the scene manually when ready.",
                definition);
        }

        private static bool CanConfigureInEditor()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return true;
            }

            Debug.LogError("Exit Play Mode before configuring furniture definitions.");
            return false;
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
            Debug.LogError($"Prefab '{parent.name}' does not contain the expected child '{childName}'.", parent);
            return false;
        }

        private static bool CenterLocalPosition(Transform transform)
        {
            Vector3 localPosition = transform.localPosition;
            Vector3 centeredPosition = new(0f, localPosition.y, 0f);
            if (localPosition == centeredPosition)
            {
                return false;
            }

            Undo.RecordObject(transform, "Center Tool Display on Grid");
            transform.localPosition = centeredPosition;
            return true;
        }

        private static Transform[] GetDirectChildren(Transform parent)
        {
            Transform[] children = new Transform[parent.childCount];
            for (int index = 0; index < parent.childCount; index++)
            {
                children[index] = parent.GetChild(index);
            }

            return children;
        }

        private static bool TryGetCombinedMeshRendererBounds(Transform root, out Bounds combinedBounds)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                combinedBounds = default;
                return false;
            }

            combinedBounds = TransformBoundsToRootSpace(root, renderers[0]);
            for (int index = 1; index < renderers.Length; index++)
            {
                combinedBounds.Encapsulate(TransformBoundsToRootSpace(root, renderers[index]));
            }

            return true;
        }

        private static Bounds TransformBoundsToRootSpace(Transform root, Renderer renderer)
        {
            Bounds rendererBounds = renderer.localBounds;
            Matrix4x4 rendererToRoot = root.worldToLocalMatrix * renderer.localToWorldMatrix;
            Vector3 center = rendererBounds.center;
            Vector3 extents = rendererBounds.extents;
            Bounds transformedBounds = new(
                rendererToRoot.MultiplyPoint3x4(center),
                Vector3.zero);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(
                            extents,
                            new Vector3(x, y, z));
                        transformedBounds.Encapsulate(rendererToRoot.MultiplyPoint3x4(corner));
                    }
                }
            }

            return transformedBounds;
        }

        private static void LogPartsBinRackBounds(
            GameObject prefab,
            Transform root,
            Bounds boundsBefore,
            Bounds boundsAfter)
        {
            Vector3 effectiveSize = Vector3.Scale(
                boundsAfter.size,
                new Vector3(
                    Mathf.Abs(root.lossyScale.x),
                    Mathf.Abs(root.lossyScale.y),
                    Mathf.Abs(root.lossyScale.z)));
            bool isCompatible = effectiveSize.x <= PrototypePartsBinRack.width + FootprintTolerance
                && effectiveSize.z <= PrototypePartsBinRack.depth + FootprintTolerance;
            string compatibility = isCompatible ? "compatible" : "not compatible";

            Debug.Log(
                $"Parts bin rack bounds before: center {boundsBefore.center}, min {boundsBefore.min}, size {boundsBefore.size}. "
                + $"Bounds after: center {boundsAfter.center}, min {boundsAfter.min}, size {boundsAfter.size}. "
                + $"Effective width X: {effectiveSize.x:F3} m; effective depth Z: {effectiveSize.z:F3} m; "
                + $"footprint {PrototypePartsBinRack.width}x{PrototypePartsBinRack.depth}: {compatibility}.",
                prefab);

            if (!isCompatible)
            {
                Debug.LogWarning(
                    "Parts bin rack visual bounds exceed its logical 2x1 footprint. The prefab was centered but not resized.",
                    prefab);
            }
        }

        private static bool TryGetStorePrototypeScene(out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(StorePrototypeScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                return true;
            }

            Debug.LogError($"Open '{StorePrototypeScenePath}' before running this command.");
            return false;
        }

        private static bool TryGetFurniturePrefab(
            FurnitureConfiguration configuration,
            out GameObject prefab)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(configuration.prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Required prefab was not found at '{configuration.prefabPath}'.");
                return false;
            }

            if (prefab.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
            {
                Debug.LogError($"Prefab '{configuration.prefabPath}' does not contain a MeshRenderer.", prefab);
                return false;
            }

            return true;
        }

        private static bool TryGetPlacementController(Scene scene, out FurniturePlacementController controller)
        {
            controller = null;
            foreach (FurniturePlacementController candidate in Resources.FindObjectsOfTypeAll<FurniturePlacementController>())
            {
                if (candidate.gameObject.scene != scene)
                {
                    continue;
                }

                if (controller != null)
                {
                    Debug.LogError(
                        $"More than one FurniturePlacementController was found in '{scene.path}'. Configure the intended controller manually.",
                        controller);
                    return false;
                }

                controller = candidate;
            }

            if (controller != null)
            {
                return true;
            }

            Debug.LogError($"No FurniturePlacementController was found in '{scene.path}'.");
            return false;
        }

        private static bool TryGetControllerDefinitions(
            FurniturePlacementController controller,
            out SerializedObject serializedController,
            out SerializedProperty definitionsProperty)
        {
            serializedController = new SerializedObject(controller);
            definitionsProperty = serializedController.FindProperty("furnitureDefinitions");
            if (definitionsProperty != null && definitionsProperty.isArray)
            {
                return true;
            }

            Debug.LogError(
                "FurniturePlacementController does not expose the expected serialized Furniture Definitions list.",
                controller);
            return false;
        }

        private static bool TryGetDefinitionProperties(
            FurnitureDefinition definition,
            out SerializedObject serializedDefinition,
            out DefinitionProperties properties)
        {
            serializedDefinition = new SerializedObject(definition);
            properties = new DefinitionProperties(
                serializedDefinition.FindProperty("furnitureTypeId"),
                serializedDefinition.FindProperty("displayName"),
                serializedDefinition.FindProperty("prefab"),
                serializedDefinition.FindProperty("width"),
                serializedDefinition.FindProperty("depth"));

            if (properties.IsComplete)
            {
                return true;
            }

            Debug.LogError("FurnitureDefinition does not expose the expected serialized fields.", definition);
            return false;
        }

        private static bool TryGetConfiguredDefinition(
            string definitionAssetPath,
            string expectedFurnitureTypeId,
            out FurnitureDefinition definition)
        {
            definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(definitionAssetPath);
            if (definition == null)
            {
                Debug.LogError($"Required furniture definition was not found at '{definitionAssetPath}'.");
                return false;
            }

            if (definition.FurnitureTypeId != expectedFurnitureTypeId)
            {
                Debug.LogError(
                    $"Furniture definition '{definitionAssetPath}' must use Furniture Type Id '{expectedFurnitureTypeId}'.",
                    definition);
                return false;
            }

            if (!definition.IsValid(out string validationMessage))
            {
                Debug.LogError(
                    $"Furniture definition '{definitionAssetPath}' is invalid: {validationMessage}",
                    definition);
                return false;
            }

            return true;
        }

        private static bool CanUseFurnitureTypeId(
            SerializedProperty definitionsProperty,
            FurnitureDefinition definition,
            string furnitureTypeId,
            out string validationError)
        {
            for (int index = 0; index < definitionsProperty.arraySize; index++)
            {
                FurnitureDefinition existingDefinition = definitionsProperty
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as FurnitureDefinition;

                if (existingDefinition != null
                    && existingDefinition != definition
                    && existingDefinition.FurnitureTypeId == furnitureTypeId)
                {
                    validationError =
                        $"Furniture Definitions already contains '{existingDefinition.name}' with Furniture Type Id '{furnitureTypeId}'. Resolve that duplicate before configuring this asset.";
                    return false;
                }
            }

            foreach (string assetGuid in AssetDatabase.FindAssets("t:FurnitureDefinition"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                FurnitureDefinition existingDefinition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(assetPath);
                if (existingDefinition != null
                    && existingDefinition != definition
                    && existingDefinition.FurnitureTypeId == furnitureTypeId)
                {
                    validationError =
                        $"Furniture definition '{existingDefinition.name}' at '{assetPath}' already uses Furniture Type Id '{furnitureTypeId}'. Resolve that duplicate before configuring this asset.";
                    return false;
                }
            }

            validationError = null;
            return true;
        }

        private static bool EnsureDefinitionOrder(
            SerializedProperty definitionsProperty,
            params FurnitureDefinition[] requiredFirstDefinitions)
        {
            List<FurnitureDefinition> orderedDefinitions = new();
            HashSet<FurnitureDefinition> addedDefinitions = new();

            foreach (FurnitureDefinition definition in requiredFirstDefinitions)
            {
                if (definition != null && addedDefinitions.Add(definition))
                {
                    orderedDefinitions.Add(definition);
                }
            }

            for (int index = 0; index < definitionsProperty.arraySize; index++)
            {
                FurnitureDefinition definition = definitionsProperty
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as FurnitureDefinition;
                if (definition != null && addedDefinitions.Add(definition))
                {
                    orderedDefinitions.Add(definition);
                }
            }

            bool changed = definitionsProperty.arraySize != orderedDefinitions.Count;
            if (!changed)
            {
                for (int index = 0; index < orderedDefinitions.Count; index++)
                {
                    if (definitionsProperty.GetArrayElementAtIndex(index).objectReferenceValue != orderedDefinitions[index])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
            {
                return false;
            }

            definitionsProperty.arraySize = orderedDefinitions.Count;
            for (int index = 0; index < orderedDefinitions.Count; index++)
            {
                definitionsProperty.GetArrayElementAtIndex(index).objectReferenceValue = orderedDefinitions[index];
            }

            return true;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string[] folders = folderPath.Split('/');
            string currentPath = folders[0];

            for (int index = 1; index < folders.Length; index++)
            {
                string nextPath = $"{currentPath}/{folders[index]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[index]);
                }

                currentPath = nextPath;
            }
        }

        private readonly struct DefinitionProperties
        {
            public readonly SerializedProperty furnitureTypeId;
            public readonly SerializedProperty displayName;
            public readonly SerializedProperty prefab;
            public readonly SerializedProperty width;
            public readonly SerializedProperty depth;

            public bool IsComplete => furnitureTypeId != null
                && displayName != null
                && prefab != null
                && width != null
                && depth != null;

            public DefinitionProperties(
                SerializedProperty furnitureTypeId,
                SerializedProperty displayName,
                SerializedProperty prefab,
                SerializedProperty width,
                SerializedProperty depth)
            {
                this.furnitureTypeId = furnitureTypeId;
                this.displayName = displayName;
                this.prefab = prefab;
                this.width = width;
                this.depth = depth;
            }
        }

        private readonly struct FurnitureConfiguration
        {
            public readonly string prefabPath;
            public readonly string definitionAssetPath;
            public readonly string definitionAssetName;
            public readonly string furnitureTypeId;
            public readonly string displayName;
            public readonly int width;
            public readonly int depth;
            public readonly Vector2Int[] accessOffsets;

            public FurnitureConfiguration(
                string prefabPath,
                string definitionAssetPath,
                string definitionAssetName,
                string furnitureTypeId,
                string displayName,
                int width,
                int depth,
                Vector2Int[] accessOffsets = null)
            {
                this.prefabPath = prefabPath;
                this.definitionAssetPath = definitionAssetPath;
                this.definitionAssetName = definitionAssetName;
                this.furnitureTypeId = furnitureTypeId;
                this.displayName = displayName;
                this.width = width;
                this.depth = depth;
                this.accessOffsets = accessOffsets;
            }
        }
    }
}
