using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Places prototype furniture footprints on the construction grid.
    /// </summary>
    public sealed class FurniturePlacementController : MonoBehaviour
    {
        private const int QuarterTurnCount = 4;
        private const string IndustrialStorageShelfTypeId = "industrial_storage_shelf";

        [Header("References")]
        [SerializeField] private ConstructionGrid constructionGrid;
        [SerializeField] private GridCellPointer cellPointer;
        [SerializeField] private WallConstructionController walls;

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
        private readonly FurnitureDisplaySlotInventory displaySlotInventory = new();
        private readonly StoreInventory storeInventory = new();
        private StoreSalesService sales;
        private readonly StoreStorageLocations storageLocations = new();
        private StoreShelfInventory shelfInventory;
        private Collider groundCollider;
        private bool placementModeActive;
        private GameObject previewVisual;
        private FurnitureDefinition previewFurnitureDefinition;
        private FurnitureDefinition selectedFurnitureDefinition;
        private bool? previewUsesValidMaterial;
        [NonSerialized] private PlacedFurnitureState movingFurniture;
        private int nextFurnitureRotationQuarterTurns;
        private int movingFurnitureRotationQuarterTurns;
        private FurniturePlacementPreviewOverlay cellPreviewOverlay;
        private readonly List<PlacementPreviewCell> previewCells = new();

        /// <summary>
        /// Gets whether construction input is currently active.
        /// </summary>
        public bool IsPlacementModeActive => placementModeActive;
        public bool IsMovingFurnitureActive => IsMovingFurniture;
        public bool IsWallModeActive => walls != null && walls.IsActive;
        public FurnitureDefinition SelectedFurnitureDefinition => selectedFurnitureDefinition;

        public void SetPlacementMode(bool active)
        {
            if (active == placementModeActive || !enabled) return;
            if (active) EnterPlacementMode(); else ExitPlacementMode();
        }

        public bool SelectFurnitureDefinition(FurnitureDefinition definition)
        {
            if (IsMovingFurniture || definition == null) return false;
            int index = furnitureDefinitions.IndexOf(definition);
            if (!TryGetValidFurnitureDefinitionAtIndex(index, out _)) return false;
            TrySelectFurnitureDefinition(index);
            return true;
        }

        public bool SelectWallTool()
        {
            if (!enabled || walls == null || !walls.CanOperate) return false;
            placementModeActive = true;
            movingFurniture = null;
            movingFurnitureRotationQuarterTurns = 0;
            DestroyPreviewVisual();
            DisposeCellPreview();
            if (cellPointer != null) cellPointer.RestoreIndicatorMaterial();
            walls.SetActive(true);
            return true;
        }

        public void GetBuildableFurniture(List<FurnitureDefinition> result)
        {
            if (result == null) return;
            result.Clear();
            for (int i = 0; i < furnitureDefinitions.Count; i++)
                if (TryGetValidFurnitureDefinitionAtIndex(i, out FurnitureDefinition definition)) result.Add(definition);
        }

        /// <summary>
        /// Gets the store's reserve inventory. Displayed units are tracked separately.
        /// </summary>
        public StoreInventory StoreInventory => storeInventory;

        /// <summary>Sales and cash for this store instance.</summary>
        public StoreSalesService Sales => sales ??= new StoreSalesService(displaySlotInventory, ResolveSaleProduct);

        private ProductDefinition ResolveSaleProduct(string furnitureId, string slotId)
        {
            return TryGetDisplaySlotDefinition(furnitureId, slotId, out DisplaySlotDefinition slot)
                && TryGetAssignedProductForDisplaySlot(furnitureId, slotId, out ProductDefinition product)
                && DisplaySlotCompatibility.IsCompatible(product, slot) ? product : null;
        }

        /// <summary>Storage infrastructure belonging to this store.</summary>
        public IReadOnlyCollection<StorageLocation> StorageLocations => storageLocations.Locations;

        /// <summary>Units allocated to industrial shelves, separate from the free reserve.</summary>
        public StoreShelfInventory ShelfInventory => shelfInventory ??= new StoreShelfInventory(
            storeInventory, storageLocations, ResolveStorageCapacity);

        private StorageCapacityDefinition ResolveStorageCapacity(string furnitureId)
        {
            return TryGetFurnitureStorageLocation(furnitureId, out _)
                && TryGetPlacedFurnitureDefinition(furnitureId, out FurnitureDefinition definition)
                ? definition.StorageCapacity : null;
        }

        /// <summary>Gets the storage location associated with an industrial shelf's FurnitureId.</summary>
        public bool TryGetFurnitureStorageLocation(string furnitureId, out StorageLocation location)
        {
            location = null;
            return !string.IsNullOrWhiteSpace(furnitureId)
                && furnitureById.TryGetValue(furnitureId, out PlacedFurnitureState furniture)
                && furniture.typeId == IndustrialStorageShelfTypeId
                && storageLocations.TryGet(furnitureId, out location);
        }

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
                || string.IsNullOrWhiteSpace(occupantId)
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
        /// Copies the valid configured product definitions into the supplied list.
        /// </summary>
        public void GetConfiguredProductDefinitions(List<ProductDefinition> configuredProducts)
        {
            if (configuredProducts == null)
            {
                return;
            }

            configuredProducts.Clear();
            for (int index = 0; index < productDefinitions.Count; index++)
            {
                ProductDefinition productDefinition = productDefinitions[index];
                if (IsConfiguredProductDefinition(productDefinition))
                {
                    configuredProducts.Add(productDefinition);
                }
            }
        }

        /// <summary>
        /// Copies the identifiers of currently placed furniture into the supplied list.
        /// </summary>
        public void GetPlacedFurnitureIds(List<string> furnitureIds)
        {
            if (furnitureIds == null)
            {
                return;
            }

            furnitureIds.Clear();
            foreach (string furnitureId in furnitureById.Keys)
            {
                furnitureIds.Add(furnitureId);
            }

            furnitureIds.Sort(StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets the definition associated with an existing placed furniture instance.
        /// </summary>
        public bool TryGetPlacedFurnitureDefinition(
            string furnitureId,
            out FurnitureDefinition furnitureDefinition)
        {
            furnitureDefinition = null;
            return !string.IsNullOrWhiteSpace(furnitureId)
                && furnitureById.TryGetValue(furnitureId, out PlacedFurnitureState furniture)
                && TryGetFurnitureDefinition(furniture, out furnitureDefinition);
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

            if (displaySlotInventory.TryGetQuantity(furnitureId, slotId, out string displayedProductId, out int displayedQuantity)
                && displayedQuantity > 0
                && (productDefinition == null || productDefinition.ProductId != displayedProductId))
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

        /// <summary>
        /// Gets the number of units currently displayed in a placed furniture slot.
        /// </summary>
        public int GetDisplayedQuantity(string furnitureId, string slotId)
        {
            return displaySlotInventory.TryGetQuantity(furnitureId, slotId, out _, out int quantity)
                ? quantity
                : 0;
        }

        /// <summary>Gets the product and quantity recorded in a non-empty display slot.</summary>
        public bool TryGetDisplayedInventory(string furnitureId, string slotId, out string productId, out int quantity)
        {
            return displaySlotInventory.TryGetQuantity(furnitureId, slotId, out productId, out quantity);
        }

        /// <summary>
        /// Transfers compatible assigned product units from the store reserve to a display slot.
        /// </summary>
        public bool TryTransferReserveToDisplay(string furnitureId, string slotId, int units)
        {
            if (!TryGetAssignedProductForDisplaySlot(furnitureId, slotId, out ProductDefinition productDefinition)
                || !TryGetPlacedFurnitureDefinition(furnitureId, out FurnitureDefinition furnitureDefinition)
                || !TryGetDisplaySlotDefinition(furnitureId, slotId, out DisplaySlotDefinition slotDefinition))
            {
                return false;
            }

            int capacity = furnitureDefinition.FurnitureTypeId == "prototype_shelf"
                && productDefinition.ProductId == "argamassa"
                ? slotDefinition.CapacityUnits
                : 0;
            return displaySlotInventory.TryTransferFromReserve(
                storeInventory,
                furnitureId,
                slotId,
                productDefinition.ProductId,
                units,
                capacity);
        }

        /// <summary>
        /// Transfers displayed product units from a slot back to the store reserve.
        /// </summary>
        public bool TryTransferDisplayToReserve(string furnitureId, string slotId, int units)
        {
            return TryGetAssignedProductForDisplaySlot(furnitureId, slotId, out ProductDefinition productDefinition)
                && displaySlotInventory.TryTransferToReserve(
                    storeInventory,
                    furnitureId,
                    slotId,
                    productDefinition.ProductId,
                    units);
        }

        /// <summary>Moves allocated units directly to a compatible commercial shelf slot.</summary>
        public bool TryRestockFromStorage(string storageId, string levelId, string furnitureId, string slotId,
            ProductDefinition product, int units)
        {
            if (product == null || product.ProductId != "argamassa"
                || !TryGetFurnitureStorageLocation(storageId, out _)
                || !TryGetPlacedFurnitureDefinition(furnitureId, out FurnitureDefinition furniture)
                || furniture.FurnitureTypeId != "prototype_shelf"
                || !TryGetDisplaySlotDefinition(furnitureId, slotId, out DisplaySlotDefinition slot)
                || !slot.HasConfiguredCapacity
                || !TryGetAssignedProductForDisplaySlot(furnitureId, slotId, out ProductDefinition assigned)
                || assigned != product || !DisplaySlotCompatibility.IsCompatible(product, slot)) return false;
            return ShelfInventory.TryTransferToDisplay(storageId, levelId, product, units,
                displaySlotInventory, furnitureId, slotId, slot.CapacityUnits);
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

            if (IsWallModeActive)
            {
                if (keyboard != null) TrySelectFurnitureDefinitionFromKeyboard(keyboard);
                if (IsWallModeActive) return;
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

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                && !IsMouseOverUi(Mouse.current))
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

        internal static bool IsMouseOverUi(Mouse mouse)
        {
            if (EventSystem.current == null) return false;
            PointerEventData pointer = new(EventSystem.current) { position = mouse.position.ReadValue() };
            List<RaycastResult> hits = new();
            EventSystem.current.RaycastAll(pointer, hits);
            foreach (RaycastResult hit in hits)
                if (hit.module is GraphicRaycaster) return true;
            return false;
        }

        private void OnDisable()
        {
            DisposeCellPreview();
        }

        private void OnDestroy()
        {
            DestroyPreviewVisual();
            DisposeCellPreview();
        }

        private void EnterPlacementMode()
        {
            movingFurniture = null;
            movingFurnitureRotationQuarterTurns = 0;
            placementModeActive = true;
            EnsurePreviewVisualForSelectedDefinition();
        }

        private void ExitPlacementMode()
        {
            placementModeActive = false;
            if (walls != null) walls.SetActive(false);
            CancelFurnitureMove();
            cellPointer.RestoreIndicatorMaterial();

            DestroyPreviewVisual();
            DisposeCellPreview();
        }

        private void UpdatePlacementPreview()
        {
            if (previewVisual == null)
            {
                HideCellPreview();
                cellPointer.RestoreIndicatorMaterial();
                return;
            }

            if (!cellPointer.TryGetPointedCell(out Vector2Int anchorCell))
            {
                HideCellPreview();
                previewVisual.SetActive(false);
                return;
            }

            FurnitureDefinition definition = null;
            if (!(IsMovingFurniture
                    ? TryGetFurnitureDefinition(movingFurniture, out definition)
                    : TryGetSelectedFurnitureDefinition(out definition)))
            {
                HideCellPreview();
                previewVisual.SetActive(false);
                cellPointer.RestoreIndicatorMaterial();
                return;
            }

            int previewWidth = IsMovingFurniture ? movingFurniture.width : definition.Width;
            int previewDepth = IsMovingFurniture ? movingFurniture.depth : definition.Depth;
            int previewRotation = GetPreviewRotationQuarterTurns();
            string ignoredFurnitureId = IsMovingFurniture ? movingFurniture.id : null;
            bool isValid = EvaluatePlacement(
                anchorCell,
                previewWidth,
                previewDepth,
                previewRotation,
                definition,
                ignoredFurnitureId,
                previewCells);

            cellPointer.SetIndicatorMaterial(isValid ? null : occupiedCellMaterial);
            SetVisualRotation(previewVisual, previewRotation);
            if (!PositionVisualOnFootprint(
                    previewVisual,
                    anchorCell,
                    previewWidth,
                    previewDepth,
                    previewRotation))
            {
                HideCellPreview();
                previewVisual.SetActive(false);
                return;
            }

            SetPreviewMaterial(isValid);
            previewVisual.SetActive(true);
            cellPreviewOverlay ??= new FurniturePlacementPreviewOverlay();
            cellPreviewOverlay.Show(constructionGrid, groundCollider.bounds.max.y, previewCells);
        }

        private void HideCellPreview()
        {
            previewCells.Clear();
            cellPreviewOverlay?.Hide();
        }

        private void DisposeCellPreview()
        {
            previewCells.Clear();
            cellPreviewOverlay?.Dispose();
            cellPreviewOverlay = null;
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
                    rotationQuarterTurns,
                    definition))
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
            if (furniture.typeId == "prototype_shelf")
            {
                visual.AddComponent<ShelfProductVisualPresenter>().Initialize(this, furniture.id, definition);
            }
            else if (furniture.typeId == IndustrialStorageShelfTypeId && definition.StorageCapacity != null)
            {
                visual.AddComponent<IndustrialStorageVisualPresenter>().Initialize(ShelfInventory, furniture.id, definition.StorageCapacity);
            }

            return true;
        }

        private bool TryRemoveFurniture()
        {
            if (!cellPointer.TryGetPointedCell(out Vector2Int cell)
                || !occupancyByCell.TryGetValue(cell, out string furnitureId)
                || string.IsNullOrWhiteSpace(furnitureId)
                || !furnitureById.TryGetValue(furnitureId, out PlacedFurnitureState furniture))
            {
                return false;
            }

            if (ShelfInventory.HasStoredUnits(furniture.id))
            {
                Debug.LogWarning($"Storage shelf '{furniture.id}' must be emptied before removal.", this);
                return false;
            }

            if (!TryReturnDisplayedUnitsToReserve(furniture.id))
            {
                Debug.LogWarning(
                    $"Furniture '{furniture.id}' could not be removed because its displayed inventory could not be returned to the store reserve.",
                    this);
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
                || string.IsNullOrWhiteSpace(furnitureId)
                || !furnitureById.TryGetValue(furnitureId, out PlacedFurnitureState furniture)
                || string.IsNullOrWhiteSpace(furniture.id)
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
            if (movingFurniture == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(movingFurniture.id)
                || !furnitureById.TryGetValue(movingFurniture.id, out PlacedFurnitureState furniture)
                || furniture != movingFurniture
                || !visualsByFurnitureId.TryGetValue(movingFurniture.id, out GameObject visual)
                || visual == null)
            {
                Debug.LogWarning("Furniture move canceled because its registered state or visual is invalid.", this);
                CancelFurnitureMove();
                return false;
            }

            if (!TryGetFurnitureDefinition(movingFurniture, out FurnitureDefinition moveDefinition)
                || !cellPointer.TryGetPointedCell(out Vector2Int destinationAnchor)
                || !IsFootprintAvailable(
                    destinationAnchor,
                    movingFurniture.width,
                    movingFurniture.depth,
                    movingFurnitureRotationQuarterTurns,
                    moveDefinition,
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
            else if (keyboard.digit4Key.wasPressedThisFrame)
            {
                TrySelectFurnitureDefinition(3);
            }
        }

        private void TrySelectFurnitureDefinition(int index)
        {
            if (!TryGetValidFurnitureDefinitionAtIndex(index, out FurnitureDefinition definition))
            {
                return;
            }

            if (walls != null) walls.SetActive(false);

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
            if (furniture == null || string.IsNullOrWhiteSpace(furniture.id)
                || furnitureById.ContainsKey(furniture.id)
                || !TryGetFurnitureDefinition(furniture, out FurnitureDefinition definition)
                || !IsFootprintAvailable(
                    furniture.anchorCell,
                    furniture.width,
                    furniture.depth,
                    furniture.rotationQuarterTurns,
                    definition))
            {
                return false;
            }

            if (furniture.typeId == IndustrialStorageShelfTypeId
                && !storageLocations.TryRegister(new StorageLocation(furniture.id,
                    string.IsNullOrWhiteSpace(definition.DisplayName) ? "Estante de Depósito" : definition.DisplayName,
                    StorageLocationType.Indoor)))
            {
                Debug.LogWarning($"Could not register storage location for furniture '{furniture.id}'.", this);
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
            if (furniture.typeId == IndustrialStorageShelfTypeId)
            {
                storageLocations.TryRemove(furniture.id);
            }
        }

        private bool TryReturnDisplayedUnitsToReserve(string furnitureId)
        {
            return displaySlotInventory.TryReturnAllToReserve(storeInventory, furnitureId);
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
            FurnitureDefinition definition,
            string ignoredFurnitureId = null)
        {
            return EvaluatePlacement(anchorCell, furnitureWidth, furnitureDepth, rotationQuarterTurns,
                definition, ignoredFurnitureId, null);
        }

        private bool EvaluatePlacement(
            Vector2Int anchorCell, int furnitureWidth, int furnitureDepth, int rotationQuarterTurns,
            FurnitureDefinition definition, string ignoredFurnitureId, List<PlacementPreviewCell> cells)
        {
            cells?.Clear();
            HashSet<Vector2Int> proposedFootprint = new();
            bool valid = true;
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
                    valid = false;
                }

                proposedFootprint.Add(cell);
            }

            HashSet<Vector2Int> otherAccess = new();
            foreach (PlacedFurnitureState other in furnitureById.Values)
            {
                if (other.id == ignoredFurnitureId)
                {
                    continue;
                }

                if (!TryGetFurnitureDefinition(other, out FurnitureDefinition otherDefinition))
                {
                    valid = false;
                    continue;
                }

                foreach (Vector2Int cell in FurnitureAccessCells.GetCells(other.anchorCell, other.width,
                             other.depth, other.rotationQuarterTurns, otherDefinition.AccessCellOffsets))
                {
                    otherAccess.Add(cell);
                    if (proposedFootprint.Contains(cell))
                    {
                        valid = false;
                    }
                }
            }

            foreach (Vector2Int cell in FurnitureAccessCells.GetCells(anchorCell, furnitureWidth,
                         furnitureDepth, rotationQuarterTurns, definition.AccessCellOffsets))
            {
                bool blocked = !constructionGrid.IsInside(cell) || proposedFootprint.Contains(cell)
                    || (occupancyByCell.TryGetValue(cell, out string occupantId) && occupantId != ignoredFurnitureId);
                if (blocked) valid = false;
                cells?.Add(new PlacementPreviewCell(cell, blocked ? PlacementCellKind.BlockedAccess
                    : otherAccess.Contains(cell) ? PlacementCellKind.SharedAccess : PlacementCellKind.FreeAccess));
            }

            if (cells != null)
            {
                foreach (Vector2Int cell in proposedFootprint)
                {
                    cells.Add(new PlacementPreviewCell(cell, valid ? PlacementCellKind.ValidFootprint : PlacementCellKind.InvalidFootprint));
                }
            }

            return valid;
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

    /// <summary>
    /// Holds the mutable product quantities currently displayed by placed furniture slots.
    /// </summary>
    public sealed class FurnitureDisplaySlotInventory
    {
        private readonly Dictionary<string, Dictionary<string, DisplayedSlotInventoryEntry>> entriesBySlotByFurnitureId = new();

        public bool TryGetQuantity(
            string furnitureId,
            string slotId,
            out string productId,
            out int quantity)
        {
            productId = null;
            quantity = 0;
            return IsKeyValid(furnitureId, slotId)
                && entriesBySlotByFurnitureId.TryGetValue(furnitureId, out Dictionary<string, DisplayedSlotInventoryEntry> entriesBySlot)
                && entriesBySlot.TryGetValue(slotId, out DisplayedSlotInventoryEntry entry)
                && AssignOutput(entry, out productId, out quantity);
        }

        public bool TryTransferFromReserve(
            StoreInventory reserveInventory,
            string furnitureId,
            string slotId,
            string productId,
            int units,
            int capacityUnits = 0)
        {
            if (reserveInventory == null
                || capacityUnits < 0
                || !CanAddUnits(furnitureId, slotId, productId, units)
                || !reserveInventory.CanRemoveUnits(productId, units))
            {
                return false;
            }

            if (capacityUnits > 0)
            {
                TryGetQuantity(furnitureId, slotId, out _, out int displayedQuantity);
                if (displayedQuantity >= capacityUnits || units > capacityUnits - displayedQuantity)
                {
                    return false;
                }
            }

            if (!reserveInventory.TryRemoveUnits(productId, units))
            {
                return false;
            }

            if (TryAddUnits(furnitureId, slotId, productId, units))
            {
                return true;
            }

            reserveInventory.TryAddUnits(productId, units);
            return false;
        }

        public bool TryTransferToReserve(
            StoreInventory reserveInventory,
            string furnitureId,
            string slotId,
            string productId,
            int units)
        {
            if (reserveInventory == null
                || !CanRemoveUnits(furnitureId, slotId, productId, units)
                || !reserveInventory.CanAddUnits(productId, units))
            {
                return false;
            }

            if (!TryRemoveUnits(furnitureId, slotId, productId, units))
            {
                return false;
            }

            if (reserveInventory.TryAddUnits(productId, units))
            {
                return true;
            }

            TryAddUnits(furnitureId, slotId, productId, units);
            return false;
        }

        public void GetEntriesForFurniture(string furnitureId, List<DisplayedSlotInventoryEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            entries.Clear();
            if (string.IsNullOrWhiteSpace(furnitureId)
                || !entriesBySlotByFurnitureId.TryGetValue(furnitureId, out Dictionary<string, DisplayedSlotInventoryEntry> entriesBySlot))
            {
                return;
            }

            foreach (DisplayedSlotInventoryEntry entry in entriesBySlot.Values)
            {
                entries.Add(entry);
            }
        }

        public bool TryReturnAllToReserve(StoreInventory reserveInventory, string furnitureId)
        {
            if (reserveInventory == null || string.IsNullOrWhiteSpace(furnitureId))
            {
                return false;
            }

            List<DisplayedSlotInventoryEntry> displayedEntries = new();
            GetEntriesForFurniture(furnitureId, displayedEntries);
            if (displayedEntries.Count == 0)
            {
                return true;
            }

            Dictionary<string, int> totalUnitsByProductId = new(StringComparer.Ordinal);
            foreach (DisplayedSlotInventoryEntry entry in displayedEntries)
            {
                int currentTotal = totalUnitsByProductId.TryGetValue(entry.ProductId, out int total)
                    ? total
                    : 0;
                if (currentTotal > int.MaxValue - entry.Quantity)
                {
                    return false;
                }

                totalUnitsByProductId[entry.ProductId] = currentTotal + entry.Quantity;
            }

            foreach (KeyValuePair<string, int> total in totalUnitsByProductId)
            {
                if (!reserveInventory.CanAddUnits(total.Key, total.Value))
                {
                    return false;
                }
            }

            int returnedEntryCount = 0;
            foreach (DisplayedSlotInventoryEntry entry in displayedEntries)
            {
                if (!TryTransferToReserve(
                        reserveInventory,
                        entry.FurnitureId,
                        entry.SlotId,
                        entry.ProductId,
                        entry.Quantity))
                {
                    for (int index = 0; index < returnedEntryCount; index++)
                    {
                        DisplayedSlotInventoryEntry returnedEntry = displayedEntries[index];
                        TryTransferFromReserve(
                            reserveInventory,
                            returnedEntry.FurnitureId,
                            returnedEntry.SlotId,
                            returnedEntry.ProductId,
                            returnedEntry.Quantity);
                    }

                    return false;
                }

                returnedEntryCount++;
            }

            return true;
        }

        internal bool CanAddUnits(string furnitureId, string slotId, string productId, int units)
        {
            if (!IsKeyValid(furnitureId, slotId)
                || string.IsNullOrWhiteSpace(productId)
                || units <= 0)
            {
                return false;
            }

            return !TryGetQuantity(furnitureId, slotId, out string existingProductId, out int existingQuantity)
                || (existingProductId == productId && existingQuantity <= int.MaxValue - units);
        }

        private bool CanRemoveUnits(string furnitureId, string slotId, string productId, int units)
        {
            return units > 0
                && TryGetQuantity(furnitureId, slotId, out string existingProductId, out int existingQuantity)
                && existingProductId == productId
                && units <= existingQuantity;
        }

        internal bool TryAddUnits(string furnitureId, string slotId, string productId, int units)
        {
            if (!CanAddUnits(furnitureId, slotId, productId, units))
            {
                return false;
            }

            int updatedQuantity = TryGetQuantity(furnitureId, slotId, out _, out int currentQuantity)
                ? currentQuantity + units
                : units;
            if (!entriesBySlotByFurnitureId.TryGetValue(furnitureId, out Dictionary<string, DisplayedSlotInventoryEntry> entriesBySlot))
            {
                entriesBySlot = new Dictionary<string, DisplayedSlotInventoryEntry>();
                entriesBySlotByFurnitureId.Add(furnitureId, entriesBySlot);
            }

            entriesBySlot[slotId] = new DisplayedSlotInventoryEntry(furnitureId, slotId, productId, updatedQuantity);
            return true;
        }

        internal bool TryRemoveUnits(string furnitureId, string slotId, string productId, int units)
        {
            if (!CanRemoveUnits(furnitureId, slotId, productId, units)
                || !entriesBySlotByFurnitureId.TryGetValue(furnitureId, out Dictionary<string, DisplayedSlotInventoryEntry> entriesBySlot)
                || !entriesBySlot.TryGetValue(slotId, out DisplayedSlotInventoryEntry entry))
            {
                return false;
            }

            int remainingQuantity = entry.Quantity - units;
            if (remainingQuantity == 0)
            {
                entriesBySlot.Remove(slotId);
                if (entriesBySlot.Count == 0)
                {
                    entriesBySlotByFurnitureId.Remove(furnitureId);
                }
            }
            else
            {
                entriesBySlot[slotId] = new DisplayedSlotInventoryEntry(
                    furnitureId,
                    slotId,
                    productId,
                    remainingQuantity);
            }

            return true;
        }

        private static bool IsKeyValid(string furnitureId, string slotId)
        {
            return !string.IsNullOrWhiteSpace(furnitureId)
                && !string.IsNullOrWhiteSpace(slotId);
        }

        private static bool AssignOutput(DisplayedSlotInventoryEntry entry, out string productId, out int quantity)
        {
            productId = entry.ProductId;
            quantity = entry.Quantity;
            return true;
        }
    }

    public readonly struct DisplayedSlotInventoryEntry
    {
        public DisplayedSlotInventoryEntry(string furnitureId, string slotId, string productId, int quantity)
        {
            FurnitureId = furnitureId;
            SlotId = slotId;
            ProductId = productId;
            Quantity = quantity;
        }

        public string FurnitureId { get; }
        public string SlotId { get; }
        public string ProductId { get; }
        public int Quantity { get; }
    }
}
