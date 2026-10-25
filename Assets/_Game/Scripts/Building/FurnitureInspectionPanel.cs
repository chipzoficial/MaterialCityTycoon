using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;
using UnityEngine;
using UnityEngine.UI;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Presents static slot information and runtime product assignments for one selected furniture instance.
    /// </summary>
    public sealed class FurnitureInspectionPanel : MonoBehaviour
    {
        private const float ReadOnlyRowHeight = 58f;
        private const string CommercialShelfFurnitureTypeId = "prototype_shelf";
        private const float AssignableRowBaseHeight = 80f;
        private const float AssignableInventoryRowBaseHeight = 124f;
        private const float ProductOptionHeight = 30f;
        private const float ProductOptionSpacing = 4f;
        private const float SlotRowSpacing = 8f;

        [Header("References")]
        [SerializeField] private Text furnitureNameText;
        [SerializeField] private RectTransform slotsContent;
        [SerializeField] private Button closeButton;
        [SerializeField] private ScrollRect slotsScrollRect;

        private readonly List<SlotRowView> slotRows = new();
        private FurniturePlacementController furniturePlacementController;
        private string selectedFurnitureId;
        private FurnitureDefinition selectedFurnitureDefinition;

        public bool IsOpen => gameObject.activeSelf;
        public string SelectedFurnitureId => selectedFurnitureId;

        private void Awake()
        {
            if (furnitureNameText == null
                || slotsContent == null
                || closeButton == null
                || slotsScrollRect == null
                || slotsScrollRect.viewport == null)
            {
                Debug.LogError(
                    "FurnitureInspectionPanel requires labels, a slots content rect, a close button, and a slots scroll rect.",
                    this);
                enabled = false;
                return;
            }

            closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
        }

        private void Update()
        {
            if (!IsOpen
                || furniturePlacementController == null
                || !furniturePlacementController.ContainsFurniture(selectedFurnitureId))
            {
                return;
            }

            RefreshInventoryBalances();
        }

        public void Show(
            string furnitureId,
            FurnitureDefinition definition,
            FurniturePlacementController placementController)
        {
            if (string.IsNullOrWhiteSpace(furnitureId) || definition == null || placementController == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            selectedFurnitureId = furnitureId;
            selectedFurnitureDefinition = definition;
            furniturePlacementController = placementController;
            furnitureNameText.text = definition.DisplayName;
            PopulateSlotRows();
        }

        public void Hide()
        {
            selectedFurnitureId = null;
            selectedFurnitureDefinition = null;
            furniturePlacementController = null;
            CloseProductOptions();
            gameObject.SetActive(false);
        }

        private void PopulateSlotRows()
        {
            IReadOnlyList<DisplaySlotDefinition> slots = selectedFurnitureDefinition.DisplaySlots;
            int receivedSlotCount = slots?.Count ?? 0;
            int createdRowCount = 0;
            int activeRowCount = 0;
            bool canAssignProducts = SupportsProductAssignments(selectedFurnitureDefinition);

            for (int index = 0; index < receivedSlotCount; index++)
            {
                DisplaySlotDefinition slot = slots[index];
                if (slot == null)
                {
                    continue;
                }

                SlotRowView row = GetOrCreateSlotRow(activeRowCount, ref createdRowCount);
                row.slotId = slot.SlotId;
                row.slotLabel.text = $"{slot.DisplayName}\n{FormatDisplayModes(slot.AllowedDisplayModes)}";
                row.allowsProductAssignment = canAssignProducts;
                row.showsInventoryBalance = ShouldShowInventoryBalance(selectedFurnitureDefinition);
                row.optionsExpanded = false;
                row.rowObject.SetActive(true);

                ConfigureProductSelector(row);
                activeRowCount++;
            }

            for (int index = activeRowCount; index < slotRows.Count; index++)
            {
                slotRows[index].rowObject.SetActive(false);
            }

            RefreshSlotRowsLayout();
            slotsScrollRect.verticalNormalizedPosition = 1f;
            Debug.Log(
                $"Furniture inspection received {receivedSlotCount} slots for '{selectedFurnitureDefinition.DisplayName}' and created {createdRowCount} new visual rows ({activeRowCount} active).",
                this);
        }

        private SlotRowView GetOrCreateSlotRow(int index, ref int createdRowCount)
        {
            if (index < slotRows.Count)
            {
                return slotRows[index];
            }

            GameObject rowObject = CreateUiObject($"SlotRow_{index + 1:00}", slotsContent);
            SlotRowView row = new(rowObject);
            row.slotLabel = CreateText("SlotLabel", rowObject.transform, 18, new Color(0.92f, 0.95f, 0.97f, 1f));
            row.productButton = CreateButton(
                "ProductSelector",
                rowObject.transform,
                new Color(0.18f, 0.26f, 0.32f, 1f),
                out row.productButtonLabel);
            row.inventoryLabel = CreateText(
                "InventoryBalance",
                rowObject.transform,
                15,
                new Color(0.72f, 0.8f, 0.86f, 1f));
            row.inventoryLabel.alignment = TextAnchor.MiddleLeft;
            row.productOptions = CreateUiObject("ProductOptions", rowObject.transform).GetComponent<RectTransform>();

            row.productButton.onClick.AddListener(() => ToggleProductOptions(row));
            slotRows.Add(row);
            createdRowCount++;
            return row;
        }

        private void ConfigureProductSelector(SlotRowView row)
        {
            row.productButton.gameObject.SetActive(row.allowsProductAssignment);
            row.productOptions.gameObject.SetActive(false);
            row.compatibleProducts.Clear();

            if (!row.allowsProductAssignment)
            {
                return;
            }

            furniturePlacementController.GetCompatibleProductsForDisplaySlot(
                selectedFurnitureId,
                row.slotId,
                row.compatibleProducts);
            UpdateProductButtonLabel(row);
            UpdateInventoryBalance(row);
            ConfigureProductOptions(row);
        }

        private void UpdateProductButtonLabel(SlotRowView row)
        {
            string productName = "Nenhum produto";
            if (furniturePlacementController.TryGetAssignedProductForDisplaySlot(
                    selectedFurnitureId,
                    row.slotId,
                    out ProductDefinition productDefinition))
            {
                productName = productDefinition.DisplayName;
            }

            row.productButtonLabel.text = $"Produto: {productName}";
        }

        private void RefreshInventoryBalances()
        {
            foreach (SlotRowView row in slotRows)
            {
                if (row.rowObject.activeSelf && row.showsInventoryBalance)
                {
                    UpdateInventoryBalance(row);
                }
            }
        }

        private void UpdateInventoryBalance(SlotRowView row)
        {
            row.inventoryLabel.gameObject.SetActive(row.showsInventoryBalance);
            if (!row.showsInventoryBalance)
            {
                return;
            }

            if (!furniturePlacementController.TryGetAssignedProductForDisplaySlot(
                    selectedFurnitureId,
                    row.slotId,
                    out ProductDefinition productDefinition))
            {
                row.inventoryLabel.text = "Sem produto atribuído";
                return;
            }

            int displayedQuantity = furniturePlacementController.GetDisplayedQuantity(selectedFurnitureId, row.slotId);
            int reserveQuantity = furniturePlacementController.StoreInventory.GetQuantity(productDefinition);
            row.inventoryLabel.text = $"Exposição: {displayedQuantity} unidades\nReserva: {reserveQuantity} unidades";
        }

        private void ConfigureProductOptions(SlotRowView row)
        {
            int requiredOptionCount = row.compatibleProducts.Count + 1;
            for (int index = 0; index < requiredOptionCount; index++)
            {
                ProductDefinition productDefinition = index == 0 ? null : row.compatibleProducts[index - 1];
                Button optionButton = GetOrCreateProductOption(row, index, out Text optionLabel);
                optionLabel.text = productDefinition == null ? "Nenhum produto" : productDefinition.DisplayName;
                optionButton.onClick.RemoveAllListeners();
                optionButton.onClick.AddListener(() => SelectProduct(row, productDefinition));
                optionButton.gameObject.SetActive(true);
            }

            for (int index = requiredOptionCount; index < row.productOptionsButtons.Count; index++)
            {
                row.productOptionsButtons[index].gameObject.SetActive(false);
            }
        }

        private Button GetOrCreateProductOption(SlotRowView row, int index, out Text optionLabel)
        {
            if (index < row.productOptionsButtons.Count)
            {
                Button button = row.productOptionsButtons[index];
                optionLabel = button.GetComponentInChildren<Text>();
                return button;
            }

            Button optionButton = CreateButton(
                $"ProductOption_{index + 1:00}",
                row.productOptions,
                new Color(0.13f, 0.2f, 0.25f, 1f),
                out optionLabel);
            row.productOptionsButtons.Add(optionButton);
            return optionButton;
        }

        private void ToggleProductOptions(SlotRowView selectedRow)
        {
            if (!selectedRow.allowsProductAssignment)
            {
                return;
            }

            bool shouldExpand = !selectedRow.optionsExpanded;
            CloseProductOptions();
            selectedRow.optionsExpanded = shouldExpand;
            RefreshSlotRowsLayout();
            EnsureRowIsVisible(selectedRow);
        }

        private void SelectProduct(SlotRowView row, ProductDefinition productDefinition)
        {
            if (furniturePlacementController.TryAssignProductToDisplaySlot(
                    selectedFurnitureId,
                    row.slotId,
                    productDefinition))
            {
                UpdateProductButtonLabel(row);
            }

            CloseProductOptions();
            RefreshSlotRowsLayout();
        }

        private void CloseProductOptions()
        {
            foreach (SlotRowView row in slotRows)
            {
                row.optionsExpanded = false;
                if (row.productOptions != null)
                {
                    row.productOptions.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshSlotRowsLayout()
        {
            float verticalOffset = 0f;
            int activeRowCount = 0;
            foreach (SlotRowView row in slotRows)
            {
                if (!row.rowObject.activeSelf)
                {
                    continue;
                }

                float productOptionsHeight = row.allowsProductAssignment && row.optionsExpanded
                    ? GetProductOptionsHeight(row)
                    : 0f;
                float baseRowHeight = GetBaseRowHeight(row);
                float rowHeight = row.allowsProductAssignment
                    ? baseRowHeight + productOptionsHeight
                    : ReadOnlyRowHeight;

                ConfigureRowRect(row, verticalOffset, rowHeight, baseRowHeight, productOptionsHeight);
                verticalOffset += rowHeight + SlotRowSpacing;
                activeRowCount++;
            }

            Canvas.ForceUpdateCanvases();
            float contentHeight = Mathf.Max(
                slotsScrollRect.viewport.rect.height,
                Mathf.Max(0f, verticalOffset - SlotRowSpacing));
            slotsContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            Canvas.ForceUpdateCanvases();

            LogSlotLayout(activeRowCount);
        }

        private static float GetProductOptionsHeight(SlotRowView row)
        {
            int optionCount = row.compatibleProducts.Count + 1;
            return optionCount * ProductOptionHeight + Mathf.Max(0, optionCount - 1) * ProductOptionSpacing;
        }

        private static float GetBaseRowHeight(SlotRowView row)
        {
            if (!row.allowsProductAssignment)
            {
                return ReadOnlyRowHeight;
            }

            return row.showsInventoryBalance
                ? AssignableInventoryRowBaseHeight
                : AssignableRowBaseHeight;
        }

        private static void ConfigureRowRect(
            SlotRowView row,
            float verticalOffset,
            float rowHeight,
            float baseRowHeight,
            float productOptionsHeight)
        {
            RectTransform rowRect = row.rowObject.GetComponent<RectTransform>();
            SetTopStretchRect(rowRect, verticalOffset, rowHeight);
            SetTopStretchRect(row.slotLabel.rectTransform, 0f, 38f);
            SetTopStretchRect(row.productButton.GetComponent<RectTransform>(), 42f, 30f);
            row.inventoryLabel.gameObject.SetActive(row.showsInventoryBalance);
            if (row.showsInventoryBalance)
            {
                SetTopStretchRect(row.inventoryLabel.rectTransform, 76f, 42f);
            }

            bool showOptions = row.allowsProductAssignment && row.optionsExpanded;
            row.productOptions.gameObject.SetActive(showOptions);
            if (!showOptions)
            {
                return;
            }

            SetTopStretchRect(row.productOptions, baseRowHeight, productOptionsHeight);
            for (int index = 0; index < row.productOptionsButtons.Count; index++)
            {
                Button optionButton = row.productOptionsButtons[index];
                if (optionButton.gameObject.activeSelf)
                {
                    SetTopStretchRect(
                        optionButton.GetComponent<RectTransform>(),
                        index * (ProductOptionHeight + ProductOptionSpacing),
                        ProductOptionHeight);
                }
            }
        }

        private void LogSlotLayout(int activeRowCount)
        {
            Debug.Log(
                $"Furniture inspection layout: viewport={slotsScrollRect.viewport.rect}, content={slotsContent.rect}, activeRows={activeRowCount}.",
                this);
        }

        private void EnsureRowIsVisible(SlotRowView row)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rowRect = row.rowObject.GetComponent<RectTransform>();
            float rowTop = -rowRect.anchoredPosition.y;
            float rowBottom = rowTop + rowRect.rect.height;
            float viewportHeight = slotsScrollRect.viewport.rect.height;
            float currentViewportTop = slotsContent.anchoredPosition.y;
            float currentViewportBottom = currentViewportTop + viewportHeight;
            float maximumScrollOffset = Mathf.Max(0f, slotsContent.rect.height - viewportHeight);

            if (rowBottom > currentViewportBottom)
            {
                slotsContent.anchoredPosition = new Vector2(
                    slotsContent.anchoredPosition.x,
                    Mathf.Min(rowBottom - viewportHeight, maximumScrollOffset));
            }
            else if (rowTop < currentViewportTop)
            {
                slotsContent.anchoredPosition = new Vector2(
                    slotsContent.anchoredPosition.x,
                    Mathf.Max(rowTop, 0f));
            }
        }

        private Text CreateText(string name, Transform parent, int fontSize, Color color)
        {
            GameObject textObject = CreateUiObject(name, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = furnitureNameText.font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string name, Transform parent, Color color, out Text label)
        {
            GameObject buttonObject = CreateUiObject(name, parent);
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            label = CreateText("Label", buttonObject.transform, 16, Color.white);
            label.alignment = TextAnchor.MiddleLeft;
            SetStretchRect(label.rectTransform, new Vector2(10f, 2f), new Vector2(-10f, -2f));
            return button;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetTopStretchRect(RectTransform rectTransform, float topOffset, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0f, -topOffset);
            rectTransform.sizeDelta = new Vector2(0f, height);
        }

        private static void SetStretchRect(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static string FormatDisplayModes(IReadOnlyList<ProductDisplayMode> displayModes)
        {
            if (displayModes == null || displayModes.Count == 0)
            {
                return "Sem modo de exposição";
            }

            List<string> labels = new(displayModes.Count);
            for (int index = 0; index < displayModes.Count; index++)
            {
                labels.Add(displayModes[index] switch
                {
                    ProductDisplayMode.Stack => "Empilhamento",
                    ProductDisplayMode.Hang => "Gancho",
                    ProductDisplayMode.Bin => "Caixa organizadora",
                    ProductDisplayMode.Pallet => "Palete",
                    ProductDisplayMode.Bulk => "A granel",
                    ProductDisplayMode.Showcase => "Vitrine",
                    _ => displayModes[index].ToString()
                });
            }

            return string.Join(", ", labels);
        }

        private static bool SupportsProductAssignments(FurnitureDefinition definition)
        {
            return definition != null
                && definition.IsValid(out _)
                && definition.DisplaySlots.Count > 0;
        }

        private static bool ShouldShowInventoryBalance(FurnitureDefinition definition)
        {
            return definition != null
                && definition.FurnitureTypeId == CommercialShelfFurnitureTypeId;
        }

        private sealed class SlotRowView
        {
            public SlotRowView(GameObject rowObject)
            {
                this.rowObject = rowObject;
            }

            public readonly GameObject rowObject;
            public readonly List<ProductDefinition> compatibleProducts = new();
            public readonly List<Button> productOptionsButtons = new();
            public string slotId;
            public Text slotLabel;
            public Button productButton;
            public Text productButtonLabel;
            public Text inventoryLabel;
            public RectTransform productOptions;
            public bool allowsProductAssignment;
            public bool showsInventoryBalance;
            public bool optionsExpanded;
        }
    }
}
