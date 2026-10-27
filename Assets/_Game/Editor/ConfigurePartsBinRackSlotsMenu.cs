using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public static class ConfigurePartsBinRackSlotsMenu
    {
        private const string PartsBinRackDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_PartsBinRack.asset";
        private const string PartsBinRackPrefabPath = "Assets/_Game/Prefabs/Building/PF_PartsBinRack.prefab";
        private const string PartsBinRackFurnitureTypeId = "prototype_parts_bin_rack";
        private const string PartsBinRackDefinitionName = "DEF_PartsBinRack";
        private const string BinBottomTransformName = "Bin_bottom";
        private const float RowGroupingTolerance = 0.01f;
        private static readonly Regex NewBinMeshNamePattern = new(@"^Caixa_Organizadora_(\d{2})$", RegexOptions.CultureInvariant);

        private static readonly string[] RequiredBinChildNames =
        {
            "Bin_bottom",
            "Bin_left",
            "Bin_right",
            "Bin_Rear",
            "Bin_front"
        };

        private static readonly Regex ManagedSlotIdPattern = new(@"^bin_r\d+_c\d+$", RegexOptions.CultureInvariant);

        [MenuItem("Tools/Material City Tycoon/Configure Parts Bin Rack Slots")]
        public static void ConfigurePartsBinRackSlots()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before configuring parts bin rack slots.");
                return;
            }

            if (!TryGetPartsBinRackDefinition(out FurnitureDefinition partsBinRackDefinition)
                || !TryGetPartsBinRackPrefab(partsBinRackDefinition, out GameObject partsBinRackPrefab)
                || !TryGetBinSlotConfigurations(partsBinRackPrefab, out List<BinSlotConfiguration> binSlotConfigurations, out int rowCount, out int columnCount)
                || !TryGetDisplaySlotsProperty(partsBinRackDefinition, out SerializedObject serializedDefinition, out SerializedProperty displaySlotsProperty)
                || !ValidateExistingSlotIds(displaySlotsProperty)
                || !ValidateExpectedFiveByFiveLayout(binSlotConfigurations, rowCount, columnCount)
                || !ValidateExistingManagedSlotIds(displaySlotsProperty, binSlotConfigurations))
            {
                return;
            }

            Undo.RecordObject(partsBinRackDefinition, "Configure Parts Bin Rack Slots");
            foreach (BinSlotConfiguration configuration in binSlotConfigurations)
            {
                int slotIndex = FindSlotIndexById(displaySlotsProperty, configuration.slotId);
                bool isNewSlot = slotIndex == -1;
                if (slotIndex == -1)
                {
                    slotIndex = displaySlotsProperty.arraySize;
                    displaySlotsProperty.arraySize++;
                }

                SerializedProperty slotProperty = displaySlotsProperty.GetArrayElementAtIndex(slotIndex);
                if (!TryGetSlotProperties(slotProperty, out SlotProperties slotProperties))
                {
                    return;
                }

                if (isNewSlot)
                {
                    slotProperties.slotId.stringValue = configuration.slotId;
                    slotProperties.displayName.stringValue = configuration.displayName;
                    slotProperties.allowedDisplayModes.arraySize = 1;
                    slotProperties.allowedDisplayModes.GetArrayElementAtIndex(0).enumValueIndex = (int)ProductDisplayMode.Bin;
                    slotProperties.capacityUnits.intValue = 0;
                }
                slotProperties.localDisplayPosition.vector3Value = configuration.localDisplayPosition;
            }

            partsBinRackDefinition.name = PartsBinRackDefinitionName;
            serializedDefinition.ApplyModifiedProperties();
            EditorUtility.SetDirty(partsBinRackDefinition);
            AssetDatabase.SaveAssetIfDirty(partsBinRackDefinition);
            Debug.Log(
                $"Parts bin rack: detected {binSlotConfigurations.Count} complete bins in {rowCount} rows x {columnCount} columns; configured {binSlotConfigurations.Count} Bin slots.",
                partsBinRackDefinition);
        }

        private static bool TryGetPartsBinRackDefinition(out FurnitureDefinition partsBinRackDefinition)
        {
            partsBinRackDefinition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(PartsBinRackDefinitionPath);
            if (partsBinRackDefinition == null)
            {
                Debug.LogError($"Parts bin rack definition was not found at '{PartsBinRackDefinitionPath}'.");
                return false;
            }

            if (partsBinRackDefinition.FurnitureTypeId != PartsBinRackFurnitureTypeId)
            {
                Debug.LogError(
                    $"Furniture definition at '{PartsBinRackDefinitionPath}' must use Furniture Type Id '{PartsBinRackFurnitureTypeId}'.",
                    partsBinRackDefinition);
                return false;
            }

            return true;
        }

        private static bool TryGetPartsBinRackPrefab(FurnitureDefinition partsBinRackDefinition, out GameObject partsBinRackPrefab)
        {
            partsBinRackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartsBinRackPrefabPath);
            if (partsBinRackPrefab == null)
            {
                Debug.LogError($"Parts bin rack prefab was not found at '{PartsBinRackPrefabPath}'.", partsBinRackDefinition);
                return false;
            }

            if (partsBinRackDefinition.Prefab != partsBinRackPrefab)
            {
                Debug.LogError(
                    "Parts bin rack definition references a different prefab. Configure the furniture definition before configuring its slots.",
                    partsBinRackDefinition);
                return false;
            }

            return true;
        }

        internal static bool TryGetBinSlotConfigurations(
            GameObject partsBinRackPrefab,
            out List<BinSlotConfiguration> configurations,
            out int rowCount,
            out int columnCount)
        {
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(partsBinRackPrefab));
            try
            {
                List<BinCandidate> newOrganizerBins = new();
                List<BinCandidate> legacyBins = new();
                foreach (Transform candidate in prefabContents.GetComponentsInChildren<Transform>(true))
                {
                    if (TryGetNewBinMeshCenter(prefabContents.transform, candidate, out Vector3 meshCenter))
                    {
                        newOrganizerBins.Add(new BinCandidate(meshCenter));
                        continue;
                    }

                    if (!TryGetCompleteBinBottom(candidate, out Transform binBottom))
                    {
                        continue;
                    }

                    if (!TryGetBottomSurfaceCenterInRootSpace(prefabContents.transform, binBottom, out Vector3 localDisplayPosition))
                    {
                        configurations = null;
                        rowCount = 0;
                        columnCount = 0;
                        return false;
                    }

                    legacyBins.Add(new BinCandidate(localDisplayPosition));
                }

                // The v2 prefab nests the imported FBX while retaining the legacy ProBuilder
                // hierarchy. The named FBX drawers are the authoritative representation; mixing
                // both sets makes an invalid composite layout and prevents the update.
                List<BinCandidate> bins = newOrganizerBins.Count > 0 ? newOrganizerBins : legacyBins;
                return TryCreateSpatialConfigurations(bins, out configurations, out rowCount, out columnCount);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static bool TryGetCompleteBinBottom(Transform candidate, out Transform binBottom)
        {
            binBottom = null;
            if (candidate.childCount != RequiredBinChildNames.Length)
            {
                return false;
            }

            foreach (string childName in RequiredBinChildNames)
            {
                if (!TryGetDirectChild(candidate, childName, out Transform child)
                    || child.GetComponent<MeshRenderer>() == null)
                {
                    return false;
                }

                if (childName == BinBottomTransformName)
                {
                    binBottom = child;
                }
            }

            return binBottom != null;
        }

        private static bool TryGetNewBinMeshCenter(Transform root, Transform candidate, out Vector3 localDisplayPosition)
        {
            localDisplayPosition = default;
            if (!NewBinMeshNamePattern.IsMatch(candidate.name) || candidate.GetComponent<MeshRenderer>() is not MeshRenderer renderer)
                return false;
            Matrix4x4 toRoot = root.worldToLocalMatrix * candidate.localToWorldMatrix;
            localDisplayPosition = toRoot.MultiplyPoint3x4(renderer.localBounds.center);
            return true;
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
            return false;
        }

        private static bool TryGetBottomSurfaceCenterInRootSpace(Transform root, Transform binBottom, out Vector3 localDisplayPosition)
        {
            MeshRenderer bottomRenderer = binBottom.GetComponent<MeshRenderer>();
            if (bottomRenderer == null)
            {
                localDisplayPosition = default;
                Debug.LogError($"Bin bottom '{binBottom.name}' requires a MeshRenderer.", binBottom);
                return false;
            }

            Bounds bottomBounds = bottomRenderer.localBounds;
            Vector3 bottomTopCenter = new(bottomBounds.center.x, bottomBounds.max.y, bottomBounds.center.z);
            Matrix4x4 bottomToRoot = root.worldToLocalMatrix * binBottom.localToWorldMatrix;
            localDisplayPosition = bottomToRoot.MultiplyPoint3x4(bottomTopCenter);
            return true;
        }

        private static bool TryCreateSpatialConfigurations(
            List<BinCandidate> bins,
            out List<BinSlotConfiguration> configurations,
            out int rowCount,
            out int columnCount)
        {
            configurations = new List<BinSlotConfiguration>();
            rowCount = 0;
            columnCount = 0;

            if (bins.Count == 0)
            {
                Debug.LogError("No complete parts bins were detected. Expected direct children Bin_bottom, Bin_left, Bin_right, Bin_Rear and Bin_front.");
                return false;
            }

            bins.Sort(BinCandidate.CompareByHeightThenX);
            List<List<BinCandidate>> rows = new();

            foreach (BinCandidate bin in bins)
            {
                if (rows.Count == 0
                    || Mathf.Abs(bin.localDisplayPosition.y - rows[^1][0].localDisplayPosition.y) > RowGroupingTolerance)
                {
                    rows.Add(new List<BinCandidate>());
                }

                rows[^1].Add(bin);
            }

            columnCount = rows[0].Count;
            foreach (List<BinCandidate> row in rows)
            {
                row.Sort(BinCandidate.CompareByX);
                if (row.Count != columnCount)
                {
                    Debug.LogError("Parts bins do not form a rectangular row-and-column layout. No slots were configured.");
                    return false;
                }
            }

            rowCount = rows.Count;
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
                {
                    BinCandidate bin = rows[rowIndex][columnIndex];
                    configurations.Add(new BinSlotConfiguration(
                        PartsBinRackSlotLayout.GetSlotId(rowIndex, columnIndex),
                        PartsBinRackSlotLayout.GetDisplayName(rowIndex, columnIndex),
                        bin.localDisplayPosition));
                }
            }

            return true;
        }

        private static bool ValidateExpectedFiveByFiveLayout(List<BinSlotConfiguration> configurations, int rowCount, int columnCount)
        {
            if (rowCount == PartsBinRackSlotLayout.Rows && columnCount == PartsBinRackSlotLayout.Columns
                && configurations.Count == PartsBinRackSlotLayout.Rows * PartsBinRackSlotLayout.Columns)
                return true;
            Debug.LogError($"The updated organizer must contain exactly {PartsBinRackSlotLayout.Rows} rows x {PartsBinRackSlotLayout.Columns} drawers. Detected {rowCount} x {columnCount}; no slots were changed.");
            return false;
        }

        private static bool ValidateExistingManagedSlotIds(SerializedProperty slots, List<BinSlotConfiguration> configurations)
        {
            HashSet<string> expected = new(configurations.ConvertAll(configuration => configuration.slotId), StringComparer.Ordinal);
            for (int index = 0; index < slots.arraySize; index++)
            {
                string id = slots.GetArrayElementAtIndex(index).FindPropertyRelative("slotId").stringValue;
                if (IsManagedBinSlotId(id) && !expected.Contains(id))
                {
                    Debug.LogError($"Existing managed slot '{id}' is outside the 5 x 5 organizer layout. Resolve it before updating.");
                    return false;
                }
            }
            return true;
        }

        private static bool TryGetDisplaySlotsProperty(
            FurnitureDefinition partsBinRackDefinition,
            out SerializedObject serializedDefinition,
            out SerializedProperty displaySlotsProperty)
        {
            serializedDefinition = new SerializedObject(partsBinRackDefinition);
            displaySlotsProperty = serializedDefinition.FindProperty("displaySlots");
            if (displaySlotsProperty != null && displaySlotsProperty.isArray)
            {
                return true;
            }

            Debug.LogError("FurnitureDefinition does not expose the expected Display Slots field.", partsBinRackDefinition);
            return false;
        }

        private static bool ValidateExistingSlotIds(SerializedProperty displaySlotsProperty)
        {
            HashSet<string> slotIds = new(StringComparer.Ordinal);
            for (int index = 0; index < displaySlotsProperty.arraySize; index++)
            {
                SerializedProperty slotIdProperty = displaySlotsProperty
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("slotId");
                string slotId = slotIdProperty?.stringValue;

                if (string.IsNullOrWhiteSpace(slotId))
                {
                    Debug.LogError($"Display slot at index {index} has an empty Slot Id. Resolve it before configuring the parts bin rack.");
                    return false;
                }

                if (!slotIds.Add(slotId))
                {
                    Debug.LogError($"Display slot id '{slotId}' is duplicated. Resolve it before configuring the parts bin rack.");
                    return false;
                }
            }

            return true;
        }

        private static int FindSlotIndexById(SerializedProperty displaySlotsProperty, string slotId)
        {
            for (int index = 0; index < displaySlotsProperty.arraySize; index++)
            {
                if (displaySlotsProperty.GetArrayElementAtIndex(index).FindPropertyRelative("slotId").stringValue == slotId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool IsManagedBinSlotId(string slotId)
        {
            return ManagedSlotIdPattern.IsMatch(slotId);
        }

        private static bool TryGetSlotProperties(SerializedProperty slotProperty, out SlotProperties slotProperties)
        {
            slotProperties = new SlotProperties(
                slotProperty.FindPropertyRelative("slotId"),
                slotProperty.FindPropertyRelative("displayName"),
                slotProperty.FindPropertyRelative("allowedDisplayModes"),
                slotProperty.FindPropertyRelative("localDisplayPosition"),
                slotProperty.FindPropertyRelative("capacityUnits"));

            if (slotProperties.IsComplete)
            {
                return true;
            }

            Debug.LogError("DisplaySlotDefinition fields do not match the parts bin rack configuration tool.");
            return false;
        }

        private readonly struct BinCandidate
        {
            public readonly Vector3 localDisplayPosition;

            public BinCandidate(Vector3 localDisplayPosition)
            {
                this.localDisplayPosition = localDisplayPosition;
            }

            public static int CompareByHeightThenX(BinCandidate first, BinCandidate second)
            {
                int heightComparison = first.localDisplayPosition.y.CompareTo(second.localDisplayPosition.y);
                return heightComparison != 0
                    ? heightComparison
                    : first.localDisplayPosition.x.CompareTo(second.localDisplayPosition.x);
            }

            public static int CompareByX(BinCandidate first, BinCandidate second)
            {
                return first.localDisplayPosition.x.CompareTo(second.localDisplayPosition.x);
            }
        }

        internal readonly struct BinSlotConfiguration
        {
            public readonly string slotId;
            public readonly string displayName;
            public readonly Vector3 localDisplayPosition;

            public BinSlotConfiguration(string slotId, string displayName, Vector3 localDisplayPosition)
            {
                this.slotId = slotId;
                this.displayName = displayName;
                this.localDisplayPosition = localDisplayPosition;
            }
        }

        private readonly struct SlotProperties
        {
            public readonly SerializedProperty slotId;
            public readonly SerializedProperty displayName;
            public readonly SerializedProperty allowedDisplayModes;
            public readonly SerializedProperty localDisplayPosition;
            public readonly SerializedProperty capacityUnits;

            public bool IsComplete => slotId != null
                && displayName != null
                && allowedDisplayModes != null
                && localDisplayPosition != null
                && capacityUnits != null;

            public SlotProperties(
                SerializedProperty slotId,
                SerializedProperty displayName,
                SerializedProperty allowedDisplayModes,
                SerializedProperty localDisplayPosition,
                SerializedProperty capacityUnits)
            {
                this.slotId = slotId;
                this.displayName = displayName;
                this.allowedDisplayModes = allowedDisplayModes;
                this.localDisplayPosition = localDisplayPosition;
                this.capacityUnits = capacityUnits;
            }
        }
    }
}
