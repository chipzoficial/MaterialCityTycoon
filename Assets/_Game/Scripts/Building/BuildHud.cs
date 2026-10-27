using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MaterialCityTycoon.Building
{
    /// <summary>Compact build catalog backed by the existing placement controller.</summary>
    public sealed class BuildHud : MonoBehaviour
    {
        [SerializeField] private FurniturePlacementController placement;
        [SerializeField] private GameObject catalog;
        [SerializeField] private Button buildButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button displayCategoryButton;
        [SerializeField] private Button storageCategoryButton;
        [SerializeField] private Button structureCategoryButton;
        [SerializeField] private Button wallButton;
        [SerializeField] private Button[] furnitureButtons;
        [SerializeField] private Shader iconShader;
        private Material iconMaterial;
        private readonly List<FurnitureDefinition> definitions = new();
        private readonly FurnitureDefinition[] items = new FurnitureDefinition[4];
        private static readonly string[] Ids =
        {
            FurnitureTypeIds.CommercialShelf,
            FurnitureTypeIds.ToolDisplay,
            FurnitureTypeIds.PartsBinRack,
            FurnitureTypeIds.IndustrialStorageShelf
        };
        private static readonly Color Normal = new(1f, 1f, 1f, 0.02f);
        private static readonly Color Selected = new(0.87f, 0.34f, 0.08f, 1f);
        private bool storageCategory;
        private bool structureCategory;
        private bool previousActive;
        private FurnitureDefinition previousSelection;
        private bool previousMoving;
        private bool previousWall;

        private void Awake()
        {
            if (placement == null || catalog == null || buildButton == null || closeButton == null
                || displayCategoryButton == null || storageCategoryButton == null || furnitureButtons == null
                || furnitureButtons.Length != 4 || System.Array.Exists(furnitureButtons, button => button == null))
            { Debug.LogError("Build HUD references are incomplete.", this); enabled = false; return; }
            placement.GetBuildableFurniture(definitions);
            if (iconShader != null)
            {
                iconMaterial = new Material(iconShader);
                foreach (Image image in GetComponentsInChildren<Image>(true))
                    if (image.name == "Icon") image.material = iconMaterial;
            }
            foreach (FurnitureDefinition definition in definitions)
                for (int i = 0; i < Ids.Length; i++)
                    if (definition.FurnitureTypeId == Ids[i]) items[i] = definition;
            buildButton.onClick.AddListener(Toggle);
            closeButton.onClick.AddListener(Close);
            displayCategoryButton.onClick.AddListener(() => SetCategory(false));
            storageCategoryButton.onClick.AddListener(() => SetCategory(true));
            if (structureCategoryButton != null) structureCategoryButton.onClick.AddListener(() => { structureCategory = true; Refresh(); });
            if (wallButton != null) wallButton.onClick.AddListener(() => { placement.SelectWallTool(); Refresh(); });
            for (int i = 0; i < furnitureButtons.Length; i++)
            {
                int index = i;
                furnitureButtons[i].onClick.AddListener(() => Select(index));
                Text label = furnitureButtons[i].GetComponentInChildren<Text>(true);
                if (label != null) label.text = items[i] != null ? items[i].DisplayName : "Indisponível";
            }
            Refresh();
        }

        private void Update()
        {
            if (previousWall && !placement.IsWallModeActive && placement.IsPlacementModeActive)
            {
                structureCategory = false;
                storageCategory = placement.SelectedFurnitureDefinition != null && placement.SelectedFurnitureDefinition.FurnitureTypeId == Ids[3];
            }
            if (!placement.IsWallModeActive && previousSelection != placement.SelectedFurnitureDefinition && placement.SelectedFurnitureDefinition != null)
                storageCategory = placement.SelectedFurnitureDefinition.FurnitureTypeId == Ids[3];
            if (previousActive != placement.IsPlacementModeActive || previousSelection != placement.SelectedFurnitureDefinition
                || previousMoving != placement.IsMovingFurnitureActive || previousWall != placement.IsWallModeActive) Refresh();
        }

        private void OnDestroy()
        {
            if (iconMaterial != null) Destroy(iconMaterial);
        }

        private void Toggle() { placement.SetPlacementMode(!placement.IsPlacementModeActive); Refresh(); }
        private void Close() { placement.SetPlacementMode(false); Refresh(); }
        private void SetCategory(bool storage) { structureCategory = false; storageCategory = storage; Refresh(); }
        private void Select(int index)
        {
            if (items[index] != null) placement.SelectFurnitureDefinition(items[index]);
            Refresh();
        }

        private void Refresh()
        {
            previousActive = placement.IsPlacementModeActive;
            previousSelection = placement.SelectedFurnitureDefinition;
            previousMoving = placement.IsMovingFurnitureActive;
            previousWall = placement.IsWallModeActive;
            catalog.SetActive(previousActive);
            buildButton.image.color = previousActive ? Selected : new Color(0.10f, 0.11f, 0.13f, 0.97f);
            displayCategoryButton.image.color = !structureCategory && !storageCategory ? Selected : Normal;
            storageCategoryButton.image.color = !structureCategory && storageCategory ? Selected : Normal;
            if (structureCategoryButton != null) structureCategoryButton.image.color = structureCategory ? Selected : Normal;
            if (wallButton != null) { wallButton.gameObject.SetActive(structureCategory); wallButton.image.color = previousWall ? Selected : Normal; }
            for (int i = 0; i < furnitureButtons.Length; i++)
            {
                Button button = furnitureButtons[i];
                button.gameObject.SetActive(!structureCategory && (storageCategory ? i == 3 : i < 3));
                button.interactable = items[i] != null && !previousMoving;
                button.image.color = !previousWall && items[i] != null && items[i] == previousSelection ? Selected : Normal;
            }
        }
    }
}
