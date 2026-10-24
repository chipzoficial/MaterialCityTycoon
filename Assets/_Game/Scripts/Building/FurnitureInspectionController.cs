using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Selects placed furniture from the current grid cell while construction mode is inactive.
    /// </summary>
    public sealed class FurnitureInspectionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridCellPointer cellPointer;
        [SerializeField] private FurniturePlacementController furniturePlacementController;
        [SerializeField] private FurnitureInspectionPanel inspectionPanel;

        private void Awake()
        {
            if (cellPointer == null
                || furniturePlacementController == null
                || inspectionPanel == null)
            {
                Debug.LogError(
                    "FurnitureInspectionController requires a cell pointer, placement controller, and inspection panel.",
                    this);
                enabled = false;
                return;
            }

            inspectionPanel.Hide();
        }

        private void Update()
        {
            if (furniturePlacementController.IsPlacementModeActive)
            {
                inspectionPanel.Hide();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                inspectionPanel.Hide();
                return;
            }

            if (inspectionPanel.IsOpen
                && !furniturePlacementController.ContainsFurniture(inspectionPanel.SelectedFurnitureId))
            {
                inspectionPanel.Hide();
            }

            if (Mouse.current == null
                || !Mouse.current.leftButton.wasPressedThisFrame
                || IsPointerOverUserInterface())
            {
                return;
            }

            if (cellPointer.TryGetPointedCell(out Vector2Int cell)
                && furniturePlacementController.TryGetFurnitureAtCell(
                    cell,
                    out string furnitureId,
                    out FurnitureDefinition definition))
            {
                inspectionPanel.Show(furnitureId, definition);
                return;
            }

            inspectionPanel.Hide();
        }

        private static bool IsPointerOverUserInterface()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void OnDisable()
        {
            if (inspectionPanel != null)
            {
                inspectionPanel.Hide();
            }
        }
    }
}
