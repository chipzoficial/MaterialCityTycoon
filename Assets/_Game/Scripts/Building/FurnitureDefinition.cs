using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;
using UnityEngine;

namespace MaterialCityTycoon.Building
{
    [CreateAssetMenu(
        fileName = "FurnitureDefinition",
        menuName = "Material City Tycoon/Furniture Definition")]
    public sealed class FurnitureDefinition : ScriptableObject
    {
        [SerializeField] private string furnitureTypeId;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(1)] private int width = 1;
        [SerializeField, Min(1)] private int depth = 1;
        [SerializeField] private DisplaySlotDefinition[] displaySlots = Array.Empty<DisplaySlotDefinition>();
        [SerializeField] private Vector2Int[] accessCellOffsets = Array.Empty<Vector2Int>();
        [SerializeField] private FurnitureAccessGroupDefinition[] accessGroups = Array.Empty<FurnitureAccessGroupDefinition>();
        [SerializeField] private StorageCapacityDefinition storageCapacity;

        public string FurnitureTypeId => furnitureTypeId;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public int Width => width;
        public int Depth => depth;
        public IReadOnlyList<DisplaySlotDefinition> DisplaySlots => displaySlots;
        public IReadOnlyList<Vector2Int> AccessCellOffsets => accessCellOffsets ?? Array.Empty<Vector2Int>();
        public IReadOnlyList<FurnitureAccessGroupDefinition> AccessGroups => accessGroups ?? Array.Empty<FurnitureAccessGroupDefinition>();
        public StorageCapacityDefinition StorageCapacity => storageCapacity;

        /// <summary>
        /// Returns each alternative service face. Definitions that have not yet been migrated
        /// retain their former behavior: every legacy offset belongs to one required group.
        /// </summary>
        public IEnumerable<IReadOnlyList<Vector2Int>> GetAccessCellGroups()
        {
            if (AccessGroups.Count == 0)
            {
                if (AccessCellOffsets.Count > 0)
                {
                    yield return AccessCellOffsets;
                }

                yield break;
            }

            foreach (FurnitureAccessGroupDefinition group in AccessGroups)
            {
                if (group != null)
                {
                    yield return group.CellOffsets;
                }
            }
        }

        public bool IsValid(out string validationMessage)
        {
            if (string.IsNullOrWhiteSpace(furnitureTypeId))
            {
                validationMessage = "Furniture Type Id cannot be empty.";
                return false;
            }

            if (prefab == null)
            {
                validationMessage = "Prefab is not assigned.";
                return false;
            }

            if (width < 1 || depth < 1)
            {
                validationMessage = "Width and Depth must be at least 1.";
                return false;
            }

            if (displaySlots == null)
            {
                validationMessage = "Display Slots cannot be null.";
                return false;
            }

            if (!ValidateAccessGroups(out validationMessage))
            {
                return false;
            }

            HashSet<string> slotIds = new(StringComparer.Ordinal);
            for (int index = 0; index < displaySlots.Length; index++)
            {
                DisplaySlotDefinition slot = displaySlots[index];
                if (slot == null)
                {
                    validationMessage = $"Display slot at index {index} is not assigned.";
                    return false;
                }

                if (!slot.IsValid(out string slotValidationMessage))
                {
                    validationMessage = $"Display slot at index {index} is invalid: {slotValidationMessage}";
                    return false;
                }

                if (!slotIds.Add(slot.SlotId))
                {
                    validationMessage = $"Display slot id '{slot.SlotId}' is duplicated.";
                    return false;
                }
            }

            validationMessage = null;
            return true;
        }

        private bool ValidateAccessGroups(out string validationMessage)
        {
            HashSet<Vector2Int> accessCells = new();
            HashSet<string> groupIds = new(StringComparer.Ordinal);
            bool hasExplicitGroups = AccessGroups.Count > 0;

            foreach (IReadOnlyList<Vector2Int> groupCells in GetAccessCellGroups())
            {
                if (groupCells == null || groupCells.Count == 0)
                {
                    validationMessage = "Each access group must contain at least one cell.";
                    return false;
                }

                foreach (Vector2Int offset in groupCells)
                {
                    if ((offset.x >= 0 && offset.x < width && offset.y >= 0 && offset.y < depth)
                        || !accessCells.Add(offset))
                    {
                        validationMessage = "Access offsets must be unique and outside the physical footprint.";
                        return false;
                    }
                }
            }

            if (hasExplicitGroups)
            {
                foreach (FurnitureAccessGroupDefinition group in AccessGroups)
                {
                    if (group == null || string.IsNullOrWhiteSpace(group.GroupId) || !groupIds.Add(group.GroupId))
                    {
                        validationMessage = "Access group ids must be assigned and unique.";
                        return false;
                    }
                }
            }

            validationMessage = null;
            return true;
        }

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            depth = Mathf.Max(1, depth);
            displaySlots ??= Array.Empty<DisplaySlotDefinition>();
            accessCellOffsets ??= Array.Empty<Vector2Int>();
            accessGroups ??= Array.Empty<FurnitureAccessGroupDefinition>();

            foreach (DisplaySlotDefinition slot in displaySlots)
            {
                slot?.Sanitize();
            }
        }
    }

    [Serializable]
    public sealed class FurnitureAccessGroupDefinition
    {
        [SerializeField] private string groupId;
        [SerializeField] private Vector2Int[] cellOffsets = Array.Empty<Vector2Int>();

        public string GroupId => groupId;
        public IReadOnlyList<Vector2Int> CellOffsets => cellOffsets ?? Array.Empty<Vector2Int>();
    }

    [Serializable]
    public sealed class DisplaySlotDefinition
    {
        [SerializeField] private string slotId;
        [SerializeField] private string displayName;
        [SerializeField] private ProductDisplayMode[] allowedDisplayModes = Array.Empty<ProductDisplayMode>();
        [SerializeField] private Vector3 localDisplayPosition;
        [SerializeField, Min(0)] private int capacityUnits;

        public string SlotId => slotId;
        public string DisplayName => displayName;
        public IReadOnlyList<ProductDisplayMode> AllowedDisplayModes => allowedDisplayModes;
        public Vector3 LocalDisplayPosition => localDisplayPosition;
        public int CapacityUnits => capacityUnits;
        public bool HasConfiguredCapacity => capacityUnits > 0;

        public bool IsValid(out string validationMessage)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                validationMessage = "Slot Id cannot be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                validationMessage = "Display Name cannot be empty.";
                return false;
            }

            if (allowedDisplayModes == null || allowedDisplayModes.Length == 0)
            {
                validationMessage = "At least one allowed display mode is required.";
                return false;
            }

            if (capacityUnits < 0)
            {
                validationMessage = "Capacity Units cannot be negative.";
                return false;
            }

            for (int index = 0; index < allowedDisplayModes.Length; index++)
            {
                for (int otherIndex = index + 1; otherIndex < allowedDisplayModes.Length; otherIndex++)
                {
                    if (allowedDisplayModes[index] != allowedDisplayModes[otherIndex])
                    {
                        continue;
                    }

                    validationMessage = "Allowed display modes cannot contain duplicates.";
                    return false;
                }
            }

            validationMessage = null;
            return true;
        }

        internal void Sanitize()
        {
            allowedDisplayModes ??= Array.Empty<ProductDisplayMode>();
            capacityUnits = Mathf.Max(0, capacityUnits);
        }
    }
}
