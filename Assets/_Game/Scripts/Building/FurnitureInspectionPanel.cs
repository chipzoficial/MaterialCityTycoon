using System.Collections.Generic;
using MaterialCityTycoon.Commerce;
using UnityEngine;
using UnityEngine.UI;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Presents the static display-slot information for one selected furniture instance.
    /// </summary>
    public sealed class FurnitureInspectionPanel : MonoBehaviour
    {
        private const float SlotRowHeight = 58f;
        private const float SlotRowSpacing = 8f;

        [Header("References")]
        [SerializeField] private Text furnitureNameText;
        [SerializeField] private RectTransform slotsContent;
        [SerializeField] private Button closeButton;
        [SerializeField] private ScrollRect slotsScrollRect;

        private readonly List<Text> slotRows = new();

        public bool IsOpen => gameObject.activeSelf;
        public string SelectedFurnitureId { get; private set; }

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

        public void Show(string furnitureId, FurnitureDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(furnitureId) || definition == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            SelectedFurnitureId = furnitureId;
            furnitureNameText.text = definition.DisplayName;
            PopulateSlotRows(definition.DisplaySlots, definition.DisplayName);
        }

        public void Hide()
        {
            SelectedFurnitureId = null;
            gameObject.SetActive(false);
        }

        private void PopulateSlotRows(IReadOnlyList<DisplaySlotDefinition> slots, string furnitureName)
        {
            int receivedSlotCount = slots?.Count ?? 0;
            int createdRowCount = 0;
            int activeRowCount = 0;

            for (int index = 0; index < receivedSlotCount; index++)
            {
                DisplaySlotDefinition slot = slots[index];
                if (slot == null)
                {
                    continue;
                }

                Text row = GetOrCreateSlotRow(activeRowCount, ref createdRowCount);
                row.text = $"{slot.DisplayName}\n{FormatDisplayModes(slot.AllowedDisplayModes)}";
                row.gameObject.SetActive(true);
                ConfigureSlotRowRect(row.rectTransform, activeRowCount);
                activeRowCount++;
            }

            for (int index = activeRowCount; index < slotRows.Count; index++)
            {
                slotRows[index].gameObject.SetActive(false);
            }

            Canvas.ForceUpdateCanvases();
            float rowsHeight = activeRowCount == 0
                ? SlotRowHeight
                : activeRowCount * SlotRowHeight + (activeRowCount - 1) * SlotRowSpacing;
            float contentHeight = Mathf.Max(slotsScrollRect.viewport.rect.height, rowsHeight);
            slotsContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            slotsScrollRect.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();

            Debug.Log(
                $"Furniture inspection received {receivedSlotCount} slots for '{furnitureName}' and created {createdRowCount} new visual rows ({activeRowCount} active).",
                this);
            LogSlotLayout(activeRowCount);
        }

        private Text GetOrCreateSlotRow(int index, ref int createdRowCount)
        {
            if (index < slotRows.Count)
            {
                return slotRows[index];
            }

            GameObject rowObject = new($"SlotRow_{index + 1:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            rowObject.transform.SetParent(slotsContent, false);

            Text row = rowObject.GetComponent<Text>();
            row.font = furnitureNameText.font;
            row.fontSize = 18;
            row.color = new Color(0.92f, 0.95f, 0.97f, 1f);
            row.alignment = TextAnchor.UpperLeft;
            row.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.verticalOverflow = VerticalWrapMode.Truncate;
            row.supportRichText = false;
            row.raycastTarget = false;

            slotRows.Add(row);
            createdRowCount++;
            return row;
        }

        private static void ConfigureSlotRowRect(RectTransform rowRect, int index)
        {
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -index * (SlotRowHeight + SlotRowSpacing));
            rowRect.sizeDelta = new Vector2(0f, SlotRowHeight);
        }

        private void LogSlotLayout(int activeRowCount)
        {
            Rect viewportRect = slotsScrollRect.viewport.rect;
            Rect contentRect = slotsContent.rect;
            string firstRowRect = activeRowCount > 0
                ? slotRows[0].rectTransform.rect.ToString()
                : "none";

            Debug.Log(
                $"Furniture inspection layout: viewport={viewportRect}, content={contentRect}, firstRow={firstRowRect}, activeRows={activeRowCount}.",
                this);
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
    }
}
