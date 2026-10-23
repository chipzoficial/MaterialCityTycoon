using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Places one-cell prototype furniture on the currently pointed grid cell.
    /// </summary>
    public sealed class FurniturePlacementController : MonoBehaviour
    {
        private const int QuarterTurnCount = 4;

        [Header("References")]
        [SerializeField] private ConstructionGrid constructionGrid;
        [SerializeField] private GridCellPointer cellPointer;
        [SerializeField] private GameObject prototypeShelfPrefab;

        [Header("Prototype Furniture")]
        [SerializeField] private string furnitureTypeId = "PrototypeShelf";

        [Header("Placement Preview")]
        [SerializeField] private Material occupiedCellMaterial;
        [SerializeField] private Material validPreviewMaterial;
        [SerializeField] private Material invalidPreviewMaterial;

        private readonly Dictionary<Vector2Int, PlacedFurnitureState> furnitureByCell = new();
        private readonly Dictionary<string, GameObject> visualsByFurnitureId = new();
        private Collider groundCollider;
        private bool placementModeActive;
        private GameObject previewVisual;
        private bool? previewUsesValidMaterial;
        private PlacedFurnitureState movingFurniture;
        private Vector2Int movingFurnitureOriginCell;
        private int nextFurnitureRotationQuarterTurns;
        private int movingFurnitureRotationQuarterTurns;

        private bool IsMovingFurniture => movingFurniture != null;

        private void Awake()
        {
            if (constructionGrid == null || cellPointer == null || prototypeShelfPrefab == null)
            {
                Debug.LogError("FurniturePlacementController requires a grid, cell pointer, and prototype shelf prefab.", this);
                enabled = false;
                return;
            }

            groundCollider = constructionGrid.GetComponent<Collider>();
            if (groundCollider == null)
            {
                Debug.LogError("FurniturePlacementController requires a Collider on the grid ground.", this);
                enabled = false;
            }
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
            if (previewVisual != null)
            {
                Destroy(previewVisual);
            }
        }

        private void EnterPlacementMode()
        {
            placementModeActive = true;
            previewVisual = Instantiate(prototypeShelfPrefab);
            previewUsesValidMaterial = null;
            DisablePreviewColliders();
            previewVisual.SetActive(false);
        }

        private void ExitPlacementMode()
        {
            placementModeActive = false;
            CancelFurnitureMove();
            cellPointer.RestoreIndicatorMaterial();

            if (previewVisual != null)
            {
                Destroy(previewVisual);
                previewVisual = null;
            }
        }

        private void UpdatePlacementPreview()
        {
            if (!cellPointer.TryGetPointedCell(out Vector2Int cell))
            {
                previewVisual.SetActive(false);
                return;
            }

            bool isValid = IsCellAvailable(cell, movingFurniture);
            cellPointer.SetIndicatorMaterial(isValid ? null : occupiedCellMaterial);
            SetVisualRotation(previewVisual, GetPreviewRotationQuarterTurns());
            if (!PositionVisualOnCell(previewVisual, cell))
            {
                previewVisual.SetActive(false);
                return;
            }

            SetPreviewMaterial(isValid);
            previewVisual.SetActive(true);
        }

        private bool TryPlaceFurniture()
        {
            if (!cellPointer.TryGetPointedCell(out Vector2Int cell)
                || !IsCellAvailable(cell))
            {
                return false;
            }

            GameObject visual = Instantiate(prototypeShelfPrefab);
            SetVisualRotation(visual, nextFurnitureRotationQuarterTurns);
            if (!PositionVisualOnCell(visual, cell))
            {
                Debug.LogError("PrototypeShelf requires a Renderer to align it with the ground.", visual);
                Destroy(visual);
                return false;
            }

            PlacedFurnitureState furniture = new(
                Guid.NewGuid().ToString("N"),
                cell,
                furnitureTypeId,
                nextFurnitureRotationQuarterTurns);
            furnitureByCell.Add(cell, furniture);
            visualsByFurnitureId.Add(furniture.id, visual);
            visual.name = $"{furniture.typeId}_{furniture.id}";
            return true;
        }

        private bool TryRemoveFurniture()
        {
            if (!cellPointer.TryGetPointedCell(out Vector2Int cell)
                || !furnitureByCell.TryGetValue(cell, out PlacedFurnitureState furniture))
            {
                return false;
            }

            furnitureByCell.Remove(cell);

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
                || !furnitureByCell.TryGetValue(cell, out PlacedFurnitureState furniture)
                || !visualsByFurnitureId.TryGetValue(furniture.id, out GameObject visual)
                || visual == null)
            {
                return false;
            }

            movingFurniture = furniture;
            movingFurnitureOriginCell = cell;
            movingFurnitureRotationQuarterTurns = furniture.rotationQuarterTurns;
            return true;
        }

        private bool TryConfirmFurnitureMove()
        {
            if (movingFurniture == null
                || !cellPointer.TryGetPointedCell(out Vector2Int destination)
                || !IsCellAvailable(destination, movingFurniture)
                || !furnitureByCell.TryGetValue(movingFurnitureOriginCell, out PlacedFurnitureState furniture)
                || furniture != movingFurniture
                || !visualsByFurnitureId.TryGetValue(movingFurniture.id, out GameObject visual)
                || visual == null)
            {
                return false;
            }

            if (!TryGetVisualBounds(visual, out _))
            {
                return false;
            }

            Vector3 originalPosition = visual.transform.position;
            Quaternion originalRotation = visual.transform.rotation;
            SetVisualRotation(visual, movingFurnitureRotationQuarterTurns);
            if (!PositionVisualOnCell(visual, destination))
            {
                visual.transform.position = originalPosition;
                visual.transform.rotation = originalRotation;
                return false;
            }

            if (destination != movingFurnitureOriginCell)
            {
                furnitureByCell.Remove(movingFurnitureOriginCell);
                furnitureByCell.Add(destination, movingFurniture);
                movingFurniture.cell = destination;
            }

            movingFurniture.rotationQuarterTurns = movingFurnitureRotationQuarterTurns;
            CancelFurnitureMove();
            return true;
        }

        private void CancelFurnitureMove()
        {
            movingFurniture = null;
            movingFurnitureRotationQuarterTurns = 0;
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

        private static int GetNextQuarterTurn(int rotationQuarterTurns)
        {
            return (rotationQuarterTurns + 1) % QuarterTurnCount;
        }

        private bool IsCellAvailable(Vector2Int cell, PlacedFurnitureState ignoredFurniture = null)
        {
            return constructionGrid.IsInside(cell)
                && (!furnitureByCell.TryGetValue(cell, out PlacedFurnitureState occupant)
                    || occupant == ignoredFurniture);
        }

        private bool PositionVisualOnCell(GameObject visual, Vector2Int cell)
        {
            float groundHeight = groundCollider.bounds.max.y;
            visual.transform.position = constructionGrid.GetCellCenter(cell, groundHeight);

            if (!TryGetVisualBounds(visual, out Bounds visualBounds))
            {
                return false;
            }

            visual.transform.position += Vector3.up * (groundHeight - visualBounds.min.y);
            return true;
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
            public Vector2Int cell;
            public readonly string typeId;
            public int rotationQuarterTurns;

            public PlacedFurnitureState(
                string id,
                Vector2Int cell,
                string typeId,
                int rotationQuarterTurns)
            {
                this.id = id;
                this.cell = cell;
                this.typeId = typeId;
                this.rotationQuarterTurns = rotationQuarterTurns;
            }
        }
    }
}
