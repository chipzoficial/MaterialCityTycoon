using System;
using System.Collections.Generic;
using MaterialCityTycoon.Building;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    /// <summary>Applies the approved visual dimensions without changing construction footprints.</summary>
    public static class ConfigureFurnitureVisualDimensionsMenu
    {
        private const string CommercialShelfPrefabPath = "Assets/_Game/Prefabs/Building/PF_CommercialShelf.prefab";
        private const string ToolDisplayPrefabPath = "Assets/_Game/Prefabs/Building/PF_ToolDisplay.prefab";
        private const string PartsBinRackPrefabPath = "Assets/_Game/Prefabs/Building/PF_PartsBinRack.prefab";
        private const string CommercialShelfDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_CommercialShelf.asset";
        private const string ToolDisplayDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_ToolDisplay.asset";

        internal const float CommercialShelfHeight = 1.8f;
        internal const float ToolDisplayHeight = 1.8f;
        internal const float PartsBinRackHeight = 1.5f;
        internal const float IndustrialStorageShelfHeight = 2.4f;

        private const float Tolerance = 0.0001f;

        [MenuItem("Tools/Material City Tycoon/Configure Furniture Visual Dimensions")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before configuring furniture visual dimensions.");
                return;
            }

            if (!TryGetValidDefinition(CommercialShelfDefinitionPath, FurnitureTypeIds.CommercialShelf, out FurnitureDefinition shelfDefinition)
                || !TryGetValidDefinition(ToolDisplayDefinitionPath, FurnitureTypeIds.ToolDisplay, out FurnitureDefinition toolDefinition)
                || !ValidatePrefab(CommercialShelfPrefabPath, "Post_01", "Post_02", "Post_03", "Post_04", "Shelf_01", "Shelf_02", "Shelf_03")
                || !ValidatePrefab(ToolDisplayPrefabPath, "Panel_01", "Base_01")
                || !ValidatePrefab(PartsBinRackPrefabPath, "MCT_OrganizadorMiudezas"))
            {
                return;
            }

            if (!TryConfigurePrefab(CommercialShelfPrefabPath, ConfigureCommercialShelf)
                || !TryConfigurePrefab(ToolDisplayPrefabPath, ConfigureToolDisplay)
                || !TryConfigurePrefab(PartsBinRackPrefabPath, ConfigurePartsBinRack)
                || !ConfigureDisplaySlotPositions(shelfDefinition, new Dictionary<string, Vector3>
                {
                    ["shelf_bottom"] = new Vector3(0f, 0.23f, 0f),
                    ["shelf_middle"] = new Vector3(0f, 0.93f, 0f),
                    ["shelf_top"] = new Vector3(0f, 1.63f, 0f)
                })
                || !ConfigureDisplaySlotPositions(toolDefinition, new Dictionary<string, Vector3>
                {
                    ["tool_panel_main"] = new Vector3(0f, 0.975f, 0.03f)
                }))
            {
                return;
            }

            // The organizer's slot positions derive from its physical drawer centers. This
            // existing idempotent configurator preserves every existing slot id and data.
            ConfigurePartsBinRackSlotsMenu.ConfigurePartsBinRackSlots();
            Debug.Log(
                "Furniture visual dimensions configured: commercial shelf 1.80 m, tool display 1.80 m and parts bin rack 1.50 m. The industrial storage shelf remains a 2.00 m FBX and requires a Blender update to reach 2.40 m.");
        }

        internal static bool ConfigureCommercialShelf(GameObject root)
        {
            if (!TryGetRenderers(root, out Dictionary<string, MeshRenderer> renderers,
                    "Post_01", "Post_02", "Post_03", "Post_04", "Shelf_01", "Shelf_02", "Shelf_03"))
            {
                return false;
            }

            foreach (string postName in new[] { "Post_01", "Post_02", "Post_03", "Post_04" })
            {
                Transform post = renderers[postName].transform;
                SetLocalY(post, CommercialShelfHeight * 0.5f);
                SetLocalScaleY(post, CommercialShelfHeight / renderers[postName].localBounds.size.y);
            }

            SetLocalY(renderers["Shelf_01"].transform, 0.2f);
            SetLocalY(renderers["Shelf_02"].transform, 0.9f);
            SetLocalY(renderers["Shelf_03"].transform, 1.6f);
            return HasExpectedBounds(root, CommercialShelfHeight);
        }

        internal static bool ConfigureToolDisplay(GameObject root)
        {
            if (!TryGetRenderers(root, out Dictionary<string, MeshRenderer> renderers, "Panel_01", "Base_01"))
            {
                return false;
            }

            MeshRenderer panel = renderers["Panel_01"];
            SetLocalScaleY(panel.transform, (ToolDisplayHeight - 0.15f) / panel.localBounds.size.y);
            SetLocalY(panel.transform, 0.15f + (ToolDisplayHeight - 0.15f) * 0.5f);
            return HasExpectedBounds(root, ToolDisplayHeight);
        }

        internal static bool ConfigurePartsBinRack(GameObject root)
        {
            Transform organizer = FindDirectChild(root.transform, "MCT_OrganizadorMiudezas");
            if (organizer == null)
            {
                Debug.LogError("Parts bin rack is missing its imported organizer root.", root);
                return false;
            }

            float currentHeight = GetCombinedMeshBounds(root).size.y;
            if (currentHeight <= Tolerance)
            {
                Debug.LogError("Parts bin rack has no measurable MeshRenderer bounds.", root);
                return false;
            }

            Vector3 scale = organizer.localScale;
            scale.y *= PartsBinRackHeight / currentHeight;
            organizer.localScale = scale;
            return HasExpectedBounds(root, PartsBinRackHeight);
        }

        private static bool TryConfigurePrefab(string prefabPath, Func<GameObject, bool> configure)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Undo.RegisterFullObjectHierarchyUndo(contents, "Configure Furniture Visual Dimensions");
                if (!configure(contents))
                {
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        internal static bool ConfigureDisplaySlotPositions(FurnitureDefinition definition, IReadOnlyDictionary<string, Vector3> positions)
        {
            SerializedObject serialized = new(definition);
            SerializedProperty slots = serialized.FindProperty("displaySlots");
            if (slots == null || !slots.isArray)
            {
                Debug.LogError($"Furniture definition '{definition.name}' does not expose display slots.", definition);
                return false;
            }

            HashSet<string> found = new(StringComparer.Ordinal);
            bool changed = false;
            for (int index = 0; index < slots.arraySize; index++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(index);
                string slotId = slot.FindPropertyRelative("slotId").stringValue;
                if (!positions.TryGetValue(slotId, out Vector3 position))
                {
                    continue;
                }

                found.Add(slotId);
                SerializedProperty localPosition = slot.FindPropertyRelative("localDisplayPosition");
                if (localPosition.vector3Value != position)
                {
                    localPosition.vector3Value = position;
                    changed = true;
                }
            }

            if (found.Count != positions.Count)
            {
                Debug.LogError($"Furniture definition '{definition.name}' is missing a required display slot.", definition);
                return false;
            }

            if (changed)
            {
                Undo.RecordObject(definition, "Configure Furniture Visual Dimensions");
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }

            return true;
        }

        private static bool TryGetValidDefinition(string path, string furnitureTypeId, out FurnitureDefinition definition)
        {
            definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path);
            if (definition == null || definition.FurnitureTypeId != furnitureTypeId || !definition.IsValid(out _))
            {
                Debug.LogError($"Furniture definition at '{path}' is missing or invalid.", definition);
                return false;
            }

            return true;
        }

        private static bool ValidatePrefab(string prefabPath, params string[] requiredChildren)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (contents.transform.localScale != Vector3.one && prefabPath != PartsBinRackPrefabPath)
                {
                    Debug.LogError($"Prefab '{prefabPath}' requires a unit-scale root.", contents);
                    return false;
                }

                foreach (string childName in requiredChildren)
                {
                    if (FindDirectChild(contents.transform, childName) == null)
                    {
                        Debug.LogError($"Prefab '{prefabPath}' is missing '{childName}'.", contents);
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static bool TryGetRenderers(GameObject root, out Dictionary<string, MeshRenderer> renderers, params string[] names)
        {
            renderers = new Dictionary<string, MeshRenderer>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                Transform child = FindDirectChild(root.transform, name);
                MeshRenderer renderer = child == null ? null : child.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    Debug.LogError($"Prefab '{root.name}' is missing a MeshRenderer on '{name}'.", root);
                    return false;
                }

                renderers.Add(name, renderer);
            }

            return true;
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static void SetLocalY(Transform transform, float y)
        {
            Vector3 position = transform.localPosition;
            position.y = y;
            transform.localPosition = position;
        }

        private static void SetLocalScaleY(Transform transform, float y)
        {
            Vector3 scale = transform.localScale;
            scale.y = y;
            transform.localScale = scale;
        }

        private static bool HasExpectedBounds(GameObject root, float expectedHeight)
        {
            Bounds bounds = GetCombinedMeshBounds(root);
            if (Mathf.Abs(bounds.min.y) <= Tolerance && Mathf.Abs(bounds.size.y - expectedHeight) <= Tolerance)
            {
                return true;
            }

            Debug.LogError($"Prefab '{root.name}' did not produce the expected floor-aligned {expectedHeight:F2} m height. Bounds: {bounds}.", root);
            return false;
        }

        internal static Bounds GetCombinedMeshBounds(GameObject root)
        {
            bool hasBounds = false;
            Bounds result = default;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Bounds rendererBounds = TransformBounds(
                    renderer.localBounds,
                    root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix);
                if (hasBounds)
                {
                    result.Encapsulate(rendererBounds);
                }
                else
                {
                    result = rendererBounds;
                    hasBounds = true;
                }
            }

            return result;
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Vector3 transformedExtents = new(
                Mathf.Abs(matrix.m00) * extents.x + Mathf.Abs(matrix.m01) * extents.y + Mathf.Abs(matrix.m02) * extents.z,
                Mathf.Abs(matrix.m10) * extents.x + Mathf.Abs(matrix.m11) * extents.y + Mathf.Abs(matrix.m12) * extents.z,
                Mathf.Abs(matrix.m20) * extents.x + Mathf.Abs(matrix.m21) * extents.y + Mathf.Abs(matrix.m22) * extents.z);
            return new Bounds(center, transformedExtents * 2f);
        }
    }
}
