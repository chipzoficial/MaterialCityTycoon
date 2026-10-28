using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MaterialCityTycoon.Building;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    /// <summary>One-off, conservative cleanup of the empty ProBuilder-era parts-bin hierarchy.</summary>
    public static class CleanLegacyPartsBinRackGeometryMenu
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Building/PF_PartsBinRack.prefab";
        private const string DefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_PartsBinRack.asset";
        private const string ModelPath = "Assets/_Game/Art/Models/Storage/MCT_OrganizadorMiudezas.fbx";
        private const int ExpectedLegacyGroupCount = 4;
        private const int ExpectedLegacyBinsPerGroup = 5;
        private const int ExpectedDrawerCount = 25;
        private static readonly Regex LegacyGroupNamePattern = new(@"^Bins(?: \(\d+\))?$", RegexOptions.CultureInvariant);
        private static readonly Regex LegacyBinNamePattern = new(@"^PB_DrawerCabinet_Bin(?: \(\d+\))?$", RegexOptions.CultureInvariant);
        private static readonly Regex NewDrawerNamePattern = new(@"^Caixa_Organizadora_(\d{2})$", RegexOptions.CultureInvariant);

        [MenuItem("Tools/Material City Tycoon/Clean Legacy Parts Bin Rack Geometry (M0.5c.1)")]
        public static void Clean()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before cleaning legacy parts-bin geometry.");
                return;
            }

            if (!TryValidateDefinition(out GameObject prefab))
            {
                return;
            }

            string prefabGuidBefore = AssetDatabase.AssetPathToGUID(PrefabPath);
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (!TryValidatePrefabContents(prefabContents.transform, out Transform newModelRoot, out List<Transform> legacyGroups))
                {
                    return;
                }

                TransformSnapshot newModelTransform = new(newModelRoot);
                if (legacyGroups.Count == 0)
                {
                    Debug.Log("Parts-bin rack legacy geometry is already clean; no objects were removed.", prefab);
                    return;
                }

                int removedObjectCount = legacyGroups.Sum(group => 1 + group.childCount);
                Undo.RegisterFullObjectHierarchyUndo(prefabContents, "Clean Legacy Parts Bin Rack Geometry");
                foreach (Transform legacyGroup in legacyGroups)
                {
                    UnityEngine.Object.DestroyImmediate(legacyGroup.gameObject);
                }

                if (!newModelTransform.Matches(newModelRoot)
                    || !TryValidatePrefabContents(prefabContents.transform, out _, out List<Transform> remainingLegacyGroups)
                    || remainingLegacyGroups.Count != 0)
                {
                    throw new InvalidOperationException("Cleanup validation failed before saving the prefab.");
                }

                PrefabUtility.SaveAsPrefabAsset(prefabContents, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (AssetDatabase.AssetPathToGUID(PrefabPath) != prefabGuidBefore)
                {
                    throw new InvalidOperationException("The parts-bin rack prefab GUID changed unexpectedly.");
                }

                Debug.Log(
                    $"Removed {removedObjectCount} legacy empty transforms: {ExpectedLegacyGroupCount} Bins groups and "
                    + $"{ExpectedLegacyGroupCount * ExpectedLegacyBinsPerGroup} PB_DrawerCabinet_Bin children. "
                    + $"The {ExpectedDrawerCount}-drawer FBX transform was preserved.",
                    prefab);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Parts-bin rack cleanup was not saved: {exception.Message}", prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static bool TryValidateDefinition(out GameObject prefab)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(DefinitionPath);
            if (prefab == null || definition == null
                || definition.Prefab != prefab
                || definition.FurnitureTypeId != FurnitureTypeIds.PartsBinRack
                || definition.Width != 2
                || definition.Depth != 1
                || definition.DisplaySlots.Count != ExpectedDrawerCount)
            {
                Debug.LogError("Parts-bin rack prefab or definition is not in the expected 2x1, 25-slot configuration. No objects were removed.", definition);
                return false;
            }

            HashSet<string> slotIds = new(definition.DisplaySlots.Select(slot => slot?.SlotId));
            for (int row = 1; row <= 5; row++)
            for (int column = 1; column <= 5; column++)
            {
                if (!slotIds.Contains($"bin_r{row:00}_c{column:00}"))
                {
                    Debug.LogError("Parts-bin rack Definition does not expose the expected 5x5 slot IDs. No objects were removed.", definition);
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidatePrefabContents(
            Transform root,
            out Transform newModelRoot,
            out List<Transform> legacyGroups)
        {
            newModelRoot = FindNewModelRoot(root);
            legacyGroups = root.Cast<Transform>().Where(child => LegacyGroupNamePattern.IsMatch(child.name)).ToList();
            if (newModelRoot == null || !HasExpectedNewDrawers(root) || legacyGroups.Count > ExpectedLegacyGroupCount)
            {
                Debug.LogError("Parts-bin rack hierarchy is unexpected. No objects were removed.", root);
                return false;
            }

            if (legacyGroups.Count == 0)
            {
                return true;
            }

            if (legacyGroups.Count != ExpectedLegacyGroupCount
                || legacyGroups.Any(group => !IsExpectedEmptyLegacyGroup(group)))
            {
                Debug.LogError("Legacy parts-bin hierarchy is incomplete or contains functional components. No objects were removed.", root);
                return false;
            }

            return true;
        }

        private static Transform FindNewModelRoot(Transform root)
        {
            foreach (Transform child in root)
            {
                UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (source != null && AssetDatabase.GetAssetPath(source) == ModelPath)
                {
                    return child;
                }
            }

            return null;
        }

        private static bool HasExpectedNewDrawers(Transform root)
        {
            MeshRenderer[] drawers = root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => NewDrawerNamePattern.IsMatch(renderer.name))
                .ToArray();
            if (drawers.Length != ExpectedDrawerCount || drawers.Select(drawer => drawer.name).Distinct(StringComparer.Ordinal).Count() != ExpectedDrawerCount)
            {
                return false;
            }

            HashSet<int> drawerNumbers = new();
            foreach (MeshRenderer drawer in drawers)
            {
                Match match = NewDrawerNamePattern.Match(drawer.name);
                if (!int.TryParse(match.Groups[1].Value, out int number) || number < 1 || number > ExpectedDrawerCount || !drawerNumbers.Add(number))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsExpectedEmptyLegacyGroup(Transform group)
        {
            if (group.parent == null
                || group.childCount != ExpectedLegacyBinsPerGroup
                || group.GetComponents<Component>().Length != 1
                || group.GetComponentsInChildren<Collider>(true).Length != 0
                || group.GetComponentsInChildren<MeshFilter>(true).Length != 0
                || group.GetComponentsInChildren<MeshRenderer>(true).Length != 0)
            {
                return false;
            }

            foreach (Transform child in group)
            {
                if (!LegacyBinNamePattern.IsMatch(child.name)
                    || child.childCount != 0
                    || child.GetComponents<Component>().Length != 1)
                {
                    return false;
                }
            }

            return true;
        }

        private readonly struct TransformSnapshot
        {
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly Vector3 localScale;

            public TransformSnapshot(Transform transform)
            {
                localPosition = transform.localPosition;
                localRotation = transform.localRotation;
                localScale = transform.localScale;
            }

            public bool Matches(Transform transform)
            {
                return transform.localPosition == localPosition
                    && transform.localRotation == localRotation
                    && transform.localScale == localScale;
            }
        }
    }
}
