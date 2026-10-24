using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Places prototype furniture footprints on the construction grid.
    /// </summary>
    public sealed class FurniturePlacementController : MonoBehaviour
    {
        private const int QuarterTurnCount = 4;

        [Header("References")]
        [SerializeField] private ConstructionGrid constructionGrid;
        [SerializeField] private GridCellPointer cellPointer;

        [Header("Furniture Definitions")]
        [SerializeField] private List<FurnitureDefinition> furnitureDefinitions = new();

        [Header("Product Definitions")]
        [SerializeField] private List<ProductDefinition> productDefinitions = new();

        [Header("Placement Preview")]
        [SerializeField] private Material occupiedCellMaterial;
        [SerializeField] private Material validPreviewMaterial;
        [SerializeField] private Material invalidPreviewMaterial;

        private readonly Dictionary<string, PlacedFurnitureState> furnitureById = new();
        private readonly Dictionary<Vector2Int, string> occupancyByCell = new();
        private readonly Dictionary<string, GameObject> visualsByFurnitureId = new();
        private readonly FurnitureDisplaySlotAssignments displaySlotAssignments = new();
        private Collider groundCollider;
        private bool placementModeActive;
        private GameObject previewVisual;
        private FurnitureDefinition previewFurnitureDefinition;
        private FurnitureDefinition selectedFurnitureDefinition;
        private bool? previewUsesValidMaterial;
        private PlacedFurnitureState movingFurniture;
        private int nextFurnitureRotationQuarterTurns;
        private int movingFurnitureRotationQuarterTurns;

        /// <summary>
        /// Gets whether construction input is currently active.
        /// </summary>
        public bool IsPlacementModeActive => placementModeActive;

        private bool IsMovingFurniture => movingFurniture != null;

        /// <summary>
        /// Resolves the placed furniture occupying a construction-grid cell.
        /// </summary>
        public bool TryGetFurnitureAtCell(
            Vector2Int cell,
            out string furnitureId,
            out FurnitureDefinition definition)
        {
            furnitureId = null;
            definition = null;

            if (!occupancyByCell.TryGetValue(cell, out string occupantId)
                || !furnitureById.TryGetValue(occupantId, out PlacedFurnitureState furniture))
            {
                return false;
            }

            if (!TryGetFurnitureDefinition(furniture, out definition))
            {
                return false;
            }

            furnitureId = furniture.id;
            return true;
        }

        /// <summary>
        /// Gets whether a placed furniture state still exists for an identifier.
        /// </summary>
        public bool ContainsFurniture(string furnitureId)
        {
            return !string.IsNullOrWhiteSpace(furnitureId)
                && furnitureById.ContainsKey(furnitureId);
        }

        /// <summary>
        /// Gets the compatible configured products for one slot of a placed furniture instance.
        /// </summary>
        public void GetCompatibleProductsForDisplaySlot(
            string furnitureId,
            string slotId,
            List<ProductDefinition> compatibleProducts)
        {
            if (compatibleProducts == null)
            {
                return;
            }

            compatibleProducts.Clear();
            if (!TryGetDisplaySlotDefinition(furnitureId, slotId, out DisplaySlotDefinition slotDefinition))
            {
                return;
            }

            for (int index = 0; index < productDefinitions.Count; index++)
            {
                ProductDefinition productDefinition = productDefinitions[index];
                if (IsConfiguredProductDefinition(productDefinition)
                    && DisplaySlotCompatibility.IsCompatible(productDefinition, slotDefinition))
                {
                    compatibleProducts.Add(productDefinition);
                }
            }
        }

        /// <summary>
        /// Gets the assigned product for a placed furniture slot, if one exists.
        /// </summary>
        public bool TryGetAssignedProductForDisplaySlot(
            string furnitureId,
            string slotId,
            out ProductDefinition productDefinition)
        {
            productDefinition = null;
            return TryGetDisplaySlotDefinition(furnitureId, slotId, out _)
                && displaySlotAssignments.TryGetProductId(furnitureId, slotId, out string productId)
                && TryGetConfiguredProductDefinition(productId, out productDefinition);
        }

        /// <summary>
        /// Assigns a compatible configured product, or removes the assignment when productDefinition is null.
        /// </summary>
        public bool TryAssignProductToDisplaySlot(
            string furnitureId,
            string slotId,
            ProductDefinition productDefinition)
        {
            if (!TryGetDisplaySlotDefinition(furnitureId, slotId, out DisplaySlotDefinition slotDefinition))
            {
                return false;
            }

            if (productDefinition == null)
            {
                displaySlotAssignments.RemoveProductId(furnitureId, slotId);
                return true;
            }

            if (!IsConfiguredProductDefinition(productDefinition)
                || !DisplaySlotCompatibility.IsCompatible(productDefinition, slotDefinition))
            {
                return false;
            }

            displaySlotAssignments.SetProductId(furnitureId, slotId, productDefinition.ProductId);
            return true;
        }

        private void Awake()
        {
            if (constructionGrid == null || cellPointer == null)
            {
                Debug.LogError("FurniturePlacementController requires a grid and cell pointer.", this);
                enabled = false;
                return;
            }

            groundCollider = constructionGrid.GetComponent<Collider>();
            if (groundCollider == null)
            {
                Debug.LogError("FurniturePlacementController requires a Collider on the grid ground.", this);
                enabled = false;
                return;
            }

            ValidateFurnitureDefinitions();
            ValidateProductDefinitions();
            SelectFirstValidFurnitureDefinition();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (IsMovingFurniture)
                    {
                        CancelFurnitureMove();
                        UpdatePlacementPreview();
                    }
                    else
                    {
                        ExitPlacementMode();
                    }

                    return;
                }

                if (keyboard.bKey.wasPressedThisFrame)
                {
                    if (placementModeActive)
                    {
                        ExitPlacementMode();
                    }
                    else
                    {
                        EnterPlacementMode();
                    }
                }
            }

            if (!placementModeActive)
            {
                return;
            }

            if (!IsMovingFurniture && keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                TryStartFurnitureMove();
            }

            if (!IsMovingFurniture && keyboard != null)
            {
                TrySelectFurnitureDefinitionFromKeyboard(keyboard);
            }

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                RotatePreview();
            }

            if (!IsMovingFurniture && keyboard != null && keyboard.deleteKey.wasPressedThisFrame)
            {
                TryRemoveFurniture();
            }

            UpdatePlacementPreview();

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (IsMovingFurniture)
                {
                    TryConfirmFurnitureMove();
                }
                else
                {
                    TryPlaceFurniture();
                }

                UpdatePlacementPreview();
            }
        }

        private void OnDestroy()
        {
            DestroyPreviewVisual();
        }

        private void EnterPlacementMode()
        {
            placementModeActive = true;
            EnsurePreviewVisualForSelectedDefinition();
        }

        private void ExitPlacementMode()
        {
            placementModeActive = false;
            CancelFurnitureMove();
            cellPointer.RestoreIndicatorMaterial();

            DestroyPreviewVisual();
        }

        private void UpdatePlacementPreview()
        {
            if (previewVisual == null)
            {
                cellPointer.RestoreIndicatorMaterial();
                return;
            }

            if (!cellPointer.TryGetPointedCell(out Vector2Int anchorCell))
            {
                previewVisual.SetActive(false);
                return;
            }

            FurnitureDefinition definition = null;
            if (!IsMovingFurniture && !TryGetSelectedFurnitureDefinition(out definition))
            {
                previewVisual.SetActive(false);
                cellPointer.RestoreIndicatorMaterial();
                return;
            }

            int previewWidth = IsMovingFurniture ? movingFurniture.width : definition.Width;
            int previewDepth = IsMovingFurniture ? movingFurniture.depth : definition.Depth;
            int previewRotation = GetPreviewRotationQuarterTurns();
            string ignoredFurnitureId = IsMovingFurniture ? movingFurniture.id : null;
            bool isValid = IsFootprintAvailable(
                anchorCell,
                previewWidth,
                previewDepth,
                previewRotation,
                ignoredFurnitureId);

            cellPointer.SetIndicatorMaterial(isValid ? null : occupiedCellMaterial);
            SetVisualRotation(previewVisual, previewRotation);
            if (!PositionVisualOnFootprint(
                    previewVisual,
                    anchorCell,
                    previewWidth,
                    previewDepth,
                    previewRotation))
            {
                previewVisual.SetActive(false);
                return;
            }

            SetPreviewMaterial(isValid);
            previewVisual.SetActive(true);
        }

        private bool TryPlaceFurniture()
        {
            if (!TryGetSelectedFurnitureDefinition(out FurnitureDefinition definition))
            {
                return false;
            }

            int rotationQuarterTurns = nextFurnitureRotationQuarterTurns;
            if (!cellPointer.TryGetPointedCell(out Vector2Int anchorCell)
                || !IsFootprintAvailable(
                    anchorCell,
                    definition.Width,
                    definition.Depth,
                    rotationQuarterTurns))
            {
                return false;
            }

            PlacedFurnitureState furniture = new(
                Guid.NewGuid().ToString("N"),
                anchorCell,
                definition.FurnitureTypeId,
                definition.Width,
                definition.Depth,
                rotationQuarterTurns);
            GameObject visual = Instantiate(definition.Prefab);
            SetVisualRotation(visual, rotationQuarterTurns);
            if (!TryGetVisualBounds(visual, out _))
            {
                Debug.LogError($"{definition.name} requires a prefab with a Renderer to align it with the ground.", visual);
                Destroy(visual);
                return false;
            }

            if (!TryRegisterFurniture(furniture))
            {
                Destroy(visual);
                return false;
            }

            visualsByFurnitureId.Add(furniture.id, visual);
            if (!PositionVisualOnFootprint(
                    visual,
                    anchorCell,
                    furniture.width,
                    furniture.depth,
                    furniture.rotationQuarterTurns))
            {
                visualsByFurnitureId.Remove(furniture.id);
                UnregisterFurniture(furniture);
                Destroy(visual);
                return false;
            }

            visual.name = $"{furniture.typeId}_{furniture.id}";
            return true;
        }

        private bool TryRemoveFurniture()
        {
            if (!cellPointer.TryGetPointedCell(out Vector2Int cell)
                || !occupancyByCell.TryGetValue(cell, out string furnitureId)
                || !furnitureById.TryGetValue(furnitureId, out PlacedFurnitureState furniture))
            {
                return false;
            }

            UnregisterFurniture(furniture);

            if (visualsByFurnitureId.TryGetValue(furniture.id, out GameObject visual))
            {
                visualsByFurnitureId.Remove(furniture.id);

                if (visual != null)
                {
                    Destroy(visual);
                }
            }

            return true;
        }

        private bool TryStartFurnitureMove()
        {
            if (!cellPointer.TryGetPointedCell(out Vector2Int cell)
                || !occupancyByCell.TryGetValue(cell, out string furnitureId)
                || !furnitureById.TryGetValue(furnitureId, out PlacedFurnitureState furniture)
                || !visualsByFurnitureId.TryGetValue(furniture.id, out GameObject visual)
                || visual == null)
            {
                return false;
            }

            movingFurniture = furniture;
            movingFurnitureRotationQuarterTurns = furniture.rotationQuarterTurns;
            ReplacePreviewVisual(visual);
            return true;
        }

        private bool TryConfirmFurnitureMove()
        {
            if (movingFurniture == null
                || !cellPointer.TryGetPointedCell(out Vector2Int destinationAnchor)
                || !furnitureById.TryGetValue(movingFurniture.id, out PlacedFurnitureState furniture)
                || furniture != movingFurniture
                || !visualsByFurnitureId.TryGetValue(movingFurniture.id, out GameObject visual)
                || visual == null
                || !IsFootprintAvailable(
                    destinationAnchor,
                    movingFurniture.width,
                    movingFurniture.depth,
                    movingFurnitureRotationQuarterTurns,
                    movingFurniture.id)
                || !TryGetVisualBounds(visual, out _))
            {
                return false;
            }

            Vector2Int originalAnchor = movingFurniture.anchorCell;
            int originalFurnitureRotation = movingFurniture.rotationQuarterTurns;
            Vector3 originalPosition = visual.transform.position;
            Quaternion originalVisualRotation = visual.transform.rotation;
            ApplyFurniturePlacement(
                movingFurniture,
                destinationAnchor,
                movingFurnitureRotationQuarterTurns);
            SetVisualRotation(visual, movingFurnitureRotationQuarterTurns);
            if (!PositionVisualOnFootprint(
                    visual,
                    destinationAnchor,
                    movingFurniture.width,
                    movingFurniture.depth,
                    movingFurnitureRotationQuarterTurns))
            {
                RestoreFurniturePlacement(movingFurniture, originalAnchor, originalFurnitureRotation);
                visual.transform.position = originalPosition;
                visual.transform.rotation = originalVisualRotation;
                return false;
            }

            CancelFurnitureMove();
            return true;
        }

        private void CancelFurnitureMove()
        {
            movingFurniture = null;
            movingFurnitureRotationQuarterTurns = 0;

            if (placementModeActive)
            {
                EnsurePreviewVisualForSelectedDefinition();
            }
        }

        private void RotatePreview()
        {
            if (IsMovingFurniture)
            {
                movingFurnitureRotationQuarterTurns = GetNextQuarterTurn(movingFurnitureRotationQuarterTurns);
                return;
            }

            nextFurnitureRotationQuarterTurns = GetNextQuarterTurn(nextFurnitureRotationQuarterTurns);
        }

        private int GetPreviewRotationQuarterTurns()
        {
            return IsMovingFurniture
                ? movingFurnitureRotationQuarterTurns
                : nextFurnitureRotationQuarterTurns;
        }

        private void TrySelectFurnitureDefinitionFromKeyboard(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                TrySelectFurnitureDefinition(0);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                TrySelectFurnitureDefinition(1);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                TrySelectFurnitureDefinition(2);
            }
        }

        private void TrySelectFurnitureDefinition(int index)
        {
            if (!TryGetValidFurnitureDefinitionAtIndex(index, out FurnitureDefinition definition))
            {
                return;
            }

            selectedFurnitureDefinition = definition;
            if (placementModeActive)
            {
                EnsurePreviewVisualForSelectedDefinition();
            }
        }

        private void SelectFirstValidFurnitureDefinition()
        {
            selectedFurnitureDefinition = null;

            for (int index = 0; index < furnitureDefinitions.Count; index++)
            {
                if (TryGetValidFurnitureDefinitionAtIndex(index, out FurnitureDefinition definition))
                {
                    selectedFurnitureDefinition = definition;
                    return;
                }
            }
        }

        private bool TryGetSelectedFurnitureDefinition(out FurnitureDefinition definition)
        {
            definition = null;
            if (selectedFurnitureDefinition == null)
            {
                return false;
            }

            int index = furnitureDefinitions.IndexOf(selectedFurnitureDefinition);
            return index >= 0 && TryGetValidFurnitureDefinitionAtIndex(index, out definition);
        }

        private bool TryGetValidFurnitureDefinitionAtIndex(int index, out FurnitureDefinition definition)
        {
            definition = null;
            if (index < 0 || index >= furnitureDefinitions.Count)
            {
                return false;
            }

            FurnitureDefinition candidate = furnitureDefinitions[index];
            if (candidate == null || !candidate.IsValid(out _))
            {
                return false;
            }

            for (int otherIndex = 0; otherIndex < furnitureDefinitions.Count; otherIndex++)
            {
                if (otherIndex != index
                    && furnitureDefinitions[otherIndex] != null
                    && furnitureDefinitions[otherIndex].FurnitureTypeId == candidate.FurnitureTypeId)
                {
                    return false;
                }
            }

            definition = candidate;
            return true;
        }

        private void ValidateFurnitureDefinitions()
        {
            Dictionary<string, int> definitionIndexesById = new();

            for (int index = 0; index < furnitureDefinitions.Count; index++)
            {
                FurnitureDefinition definition = furnitureDefinitions[index];
                if (definition == null)
                {
                    Debug.LogWarning($"Furniture definition at index {index} is not assigned.", this);
                    continue;
                }

                if (!definition.IsValid(out string validationMessage))
                {
                    Debug.LogWarning(
                        $"Furniture definition '{definition.name}' at index {index} is invalid: {validationMessage}",
                        this);
                    continue;
                }

                if (definitionIndexesById.TryGetValue(definition.FurnitureTypeId, out int firstIndex))
                {
                    Debug.LogWarning(
                        $"Furniture Type Id '{definition.FurnitureTypeId}' is duplicated at indexes {firstIndex} and {index}; both entries are ignored.",
                        this);
                    continue;
                }

                definitionIndexesById.Add(definition.FurnitureTypeId, index);
            }
        }

        private void ValidateProductDefinitions()
        {
            Dictionary<string, int> definitionIndexesById = new();

            for (int index = 0; index < productDefinitions.Count; index++)
            {
                ProductDefinition definition = productDefinitions[index];
                if (definition == null)
                {
                    Debug.LogWarning($"Product definition at index {index} is not assigned.", this);
                    continue;
                }

                if (!definition.IsValid(out string validationMessage))
                {
                    Debug.LogWarning(
                        $"Product definition '{definition.name}' at index {index} is invalid: {validationMessage}",
                        this);
                    continue;
                }

                if (definitionIndexesById.TryGetValue(definition.ProductId, out int firstIndex))
                {
                    Debug.LogWarning(
                        $"Product Id '{definition.ProductId}' is duplicated at indexes {firstIndex} and {index}; both entries are ignored.",
                        this);
                    continue;
                }

                definitionIndexesById.Add(definition.ProductId, index);
            }
        }

        private bool TryGetDisplaySlotDefinition(
            string furnitureId,
            string slotId,
            out DisplaySlotDefinition slotDefinition)
        {
            slotDefinition = null;
            if (string.IsNullOrWhiteSpace(furnitureId)
                || string.IsNullOrWhiteSpace(slotId)
                || !furnitureById.TryGetValue(furnitureId, out PlacedFurnitureState furniture)
                || !TryGetFurnitureDefinition(furniture, out FurnitureDefinition furnitureDefinition))
            {
                return false;
            }

            foreach (DisplaySlotDefinition candidate in furnitureDefinition.DisplaySlots)
            {
                if (candidate != null && candidate.SlotId == slotId)
                {
                    slotDefinition = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetFurnitureDefinition(
            PlacedFurnitureState furniture,
            out FurnitureDefinition furnitureDefinition)
        {
            furnitureDefinition = null;
            if (furniture == null)
            {
                return false;
            }

            for (int index = 0; index < furnitureDefinitions.Count; index++)
            {
                if (TryGetValidFurnitureDefinitionAtIndex(index, out FurnitureDefinition candidate)
                    && candidate.FurnitureTypeId == furniture.typeId)
                {
                    furnitureDefinition = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool IsConfiguredProductDefinition(ProductDefinition productDefinition)
        {
            return productDefinition != null
                && productDefinition.IsValid(out _)
                && TryGetConfiguredProductDefinition(productDefinition.ProductId, out ProductDefinition configuredProduct)
                && configuredProduct == productDefinition;
        }

        private bool TryGetConfiguredProductDefinition(
            string productId,
            out ProductDefinition productDefinition)
        {
            productDefinition = null;
            if (string.IsNullOrWhiteSpace(productId))
            {
                return false;
            }

            for (int index = 0; index < productDefinitions.Count; index++)
            {
                ProductDefinition candidate = productDefinitions[index];
                if (candidate == null || candidate.ProductId != productId)
                {
                    continue;
                }

                if (!candidate.IsValid(out _) || productDefinition != null)
                {
                    productDefinition = null;
                    return false;
                }

                productDefinition = candidate;
            }

            return productDefinition != null;
        }

        private void EnsurePreviewVisualForSelectedDefinition()
        {
            if (!TryGetSelectedFurnitureDefinition(out FurnitureDefinition definition))
            {
                DestroyPreviewVisual();
                return;
            }

            if (previewVisual != null && previewFurnitureDefinition == definition)
            {
                return;
            }

            ReplacePreviewVisual(definition.Prefab);
            previewFurnitureDefinition = definition;
        }

        private void ReplacePreviewVisual(GameObject source)
        {
            DestroyPreviewVisual();
            previewVisual = Instantiate(source);
            previewUsesValidMaterial = null;
            DisablePreviewColliders();
            previewVisual.SetActive(false);
        }

        private void DestroyPreviewVisual()
        {
            if (previewVisual != null)
            {
                Destroy(previewVisual);
                previewVisual = null;
            }

            previewFurnitureDefinition = null;
            previewUsesValidMaterial = null;
        }

        private static int GetNextQuarterTurn(int rotationQuarterTurns)
        {
            return (rotationQuarterTurns + 1) % QuarterTurnCount;
        }

        private bool TryRegisterFurniture(PlacedFurnitureState furniture)
        {
            if (furnitureById.ContainsKey(furniture.id)
                || !IsFootprintAvailable(
                    furniture.anchorCell,
                    furniture.width,
                    furniture.depth,
                    furniture.rotationQuarterTurns))
            {
                return false;
            }

            furnitureById.Add(furniture.id, furniture);
            AddFurnitureOccupancy(furniture);
            return true;
        }

        private void UnregisterFurniture(PlacedFurnitureState furniture)
        {
            RemoveFurnitureOccupancy(furniture);
            furnitureById.Remove(furniture.id);
            displaySlotAssignments.RemoveFurniture(furniture.id);
        }

        private void ApplyFurniturePlacement(
            PlacedFurnitureState furniture,
            Vector2Int anchorCell,
            int rotationQuarterTurns)
        {
            RemoveFurnitureOccupancy(furniture);
            furniture.anchorCell = anchorCell;
            furniture.rotationQuarterTurns = rotationQuarterTurns;
            AddFurnitureOccupancy(furniture);
        }

        private void RestoreFurniturePlacement(
            PlacedFurnitureState furniture,
            Vector2Int anchorCell,
            int rotationQuarterTurns)
        {
            RemoveFurnitureOccupancy(furniture);
            furniture.anchorCell = anchorCell;
            furniture.rotationQuarterTurns = rotationQuarterTurns;
            AddFurnitureOccupancy(furniture);
        }

        private bool IsFootprintAvailable(
            Vector2Int anchorCell,
            int furnitureWidth,
            int furnitureDepth,
            int rotationQuarterTurns,
            string ignoredFurnitureId = null)
        {
            foreach (Vector2Int cell in GetOccupiedCells(
                         anchorCell,
                         furnitureWidth,
                         furnitureDepth,
                         rotationQuarterTurns))
            {
                if (!constructionGrid.IsInside(cell)
                    || (occupancyByCell.TryGetValue(cell, out string occupantId)
                        && occupantId != ignoredFurnitureId))
                {
                    return false;
                }
            }

            return true;
        }

        private void AddFurnitureOccupancy(PlacedFurnitureState furniture)
        {
            foreach (Vector2Int cell in GetOccupiedCells(
                         furniture.anchorCell,
                         furniture.width,
                         furniture.depth,
                         furniture.rotationQuarterTurns))
            {
                occupancyByCell.Add(cell, furniture.id);
            }
        }

        private void RemoveFurnitureOccupancy(PlacedFurnitureState furniture)
        {
            foreach (Vector2Int cell in GetOccupiedCells(
                         furniture.anchorCell,
                         furniture.width,
                         furniture.depth,
                         furniture.rotationQuarterTurns))
            {
                if (occupancyByCell.TryGetValue(cell, out string occupantId)
                    && occupantId == furniture.id)
                {
                    occupancyByCell.Remove(cell);
                }
            }
        }

        private static IEnumerable<Vector2Int> GetOccupiedCells(
            Vector2Int anchorCell,
            int furnitureWidth,
            int furnitureDepth,
            int rotationQuarterTurns)
        {
            GetFootprintDimensions(
                furnitureWidth,
                furnitureDepth,
                rotationQuarterTurns,
                out int footprintWidth,
                out int footprintDepth);

            for (int x = 0; x < footprintWidth; x++)
            {
                for (int z = 0; z < footprintDepth; z++)
                {
                    yield return new Vector2Int(anchorCell.x + x, anchorCell.y + z);
                }
            }
        }

        private static void GetFootprintDimensions(
            int furnitureWidth,
            int furnitureDepth,
            int rotationQuarterTurns,
            out int footprintWidth,
            out int footprintDepth)
        {
            if (rotationQuarterTurns % 2 == 0)
            {
                footprintWidth = furnitureWidth;
                footprintDepth = furnitureDepth;
                return;
            }

            footprintWidth = furnitureDepth;
            footprintDepth = furnitureWidth;
        }

        private bool PositionVisualOnFootprint(
            GameObject visual,
            Vector2Int anchorCell,
            int furnitureWidth,
            int furnitureDepth,
            int rotationQuarterTurns)
        {
            float groundHeight = groundCollider.bounds.max.y;
            visual.transform.position = GetFootprintCenter(
                anchorCell,
                furnitureWidth,
                furnitureDepth,
                rotationQuarterTurns,
                groundHeight);

            if (!TryGetVisualBounds(visual, out Bounds visualBounds))
            {
                return false;
            }

            visual.transform.position += Vector3.up * (groundHeight - visualBounds.min.y);
            return true;
        }

        private Vector3 GetFootprintCenter(
            Vector2Int anchorCell,
            int furnitureWidth,
            int furnitureDepth,
            int rotationQuarterTurns,
            float worldHeight)
        {
            GetFootprintDimensions(
                furnitureWidth,
                furnitureDepth,
                rotationQuarterTurns,
                out int footprintWidth,
                out int footprintDepth);

            Vector3 anchorCenter = constructionGrid.GetCellCenter(anchorCell, worldHeight);
            return anchorCenter + new Vector3(
                (footprintWidth - 1) * constructionGrid.CellSize * 0.5f,
                0f,
                (footprintDepth - 1) * constructionGrid.CellSize * 0.5f);
        }

        private static void SetVisualRotation(GameObject visual, int rotationQuarterTurns)
        {
            visual.transform.rotation = Quaternion.Euler(0f, rotationQuarterTurns * 90f, 0f);
        }

        private void DisablePreviewColliders()
        {
            foreach (Collider collider in previewVisual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private void SetPreviewMaterial(bool isValid)
        {
            if (previewVisual == null || previewUsesValidMaterial == isValid)
            {
                return;
            }

            Material previewMaterial = isValid ? validPreviewMaterial : invalidPreviewMaterial;
            if (previewMaterial == null)
            {
                return;
            }

            foreach (Renderer renderer in previewVisual.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    materials[index] = previewMaterial;
                }

                renderer.sharedMaterials = materials;
            }

            previewUsesValidMaterial = isValid;
        }

        private static bool TryGetVisualBounds(GameObject visual, out Bounds bounds)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return true;
        }

        [Serializable]
        private sealed class PlacedFurnitureState
        {
            public readonly string id;
            public Vector2Int anchorCell;
            public readonly string typeId;
            public readonly int width;
            public readonly int depth;
            public int rotationQuarterTurns;

            public PlacedFurnitureState(
                string id,
                Vector2Int anchorCell,
                string typeId,
                int width,
                int depth,
                int rotationQuarterTurns)
            {
                this.id = id;
                this.anchorCell = anchorCell;
                this.typeId = typeId;
                this.width = width;
                this.depth = depth;
                this.rotationQuarterTurns = rotationQuarterTurns;
            }
        }
    }

    /// <summary>
    /// Holds runtime product assignments for display slots on placed furniture instances.
    /// </summary>
    public sealed class FurnitureDisplaySlotAssignments
    {
        private readonly Dictionary<string, Dictionary<string, string>> productIdsBySlotByFurnitureId = new();

        public bool TryGetProductId(string furnitureId, string slotId, out string productId)
        {
            productId = null;
            return !string.IsNullOrWhiteSpace(furnitureId)
                && !string.IsNullOrWhiteSpace(slotId)
                && productIdsBySlotByFurnitureId.TryGetValue(furnitureId, out Dictionary<string, string> productIdsBySlot)
                && productIdsBySlot.TryGetValue(slotId, out productId);
        }

        public void SetProductId(string furnitureId, string slotId, string productId)
        {
            if (string.IsNullOrWhiteSpace(furnitureId) || string.IsNullOrWhiteSpace(slotId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(productId))
            {
                RemoveProductId(furnitureId, slotId);
                return;
            }

            if (!productIdsBySlotByFurnitureId.TryGetValue(furnitureId, out Dictionary<string, string> productIdsBySlot))
            {
                productIdsBySlot = new Dictionary<string, string>();
                productIdsBySlotByFurnitureId.Add(furnitureId, productIdsBySlot);
            }

            productIdsBySlot[slotId] = productId;
        }

        public void RemoveProductId(string furnitureId, string slotId)
        {
            if (string.IsNullOrWhiteSpace(furnitureId)
                || string.IsNullOrWhiteSpace(slotId)
                || !productIdsBySlotByFurnitureId.TryGetValue(furnitureId, out Dictionary<string, string> productIdsBySlot))
            {
                return;
            }

            productIdsBySlot.Remove(slotId);
            if (productIdsBySlot.Count == 0)
            {
                productIdsBySlotByFurnitureId.Remove(furnitureId);
            }
        }

        public void RemoveFurniture(string furnitureId)
        {
            if (!string.IsNullOrWhiteSpace(furnitureId))
            {
                productIdsBySlotByFurnitureId.Remove(furnitureId);
            }
        }
    }
}
