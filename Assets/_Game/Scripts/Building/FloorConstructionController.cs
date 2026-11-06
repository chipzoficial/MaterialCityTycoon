using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MaterialCityTycoon.Building
{
    /// <summary>Builds a provisional surface covering on construction-grid cells.</summary>
    public sealed class FloorConstructionController : MonoBehaviour
    {
        [SerializeField] private ConstructionGrid grid;
        [SerializeField] private UnityEngine.Camera inputCamera;
        [SerializeField] private Collider ground;
        [SerializeField] private GridCellPointer cellPointer;

        private readonly FloorCells floors = new();
        private readonly Dictionary<Vector2Int, GameObject> visuals = new();
        private readonly List<Vector2Int> selectedCells = new();
        private readonly List<GameObject> previewVisuals = new();
        private Material floorMaterial;
        private Material validMaterial;
        private Material invalidMaterial;
        private Vector2Int dragStartCell;
        private bool dragging;
        private bool removing;
        private bool selectionIsValid;

        private const float FloorThickness = 0.02f;
        private const float SurfaceOffset = 0.002f;

        public bool IsActive { get; private set; }
        public bool CanOperate => grid != null && inputCamera != null && ground != null;
        public FloorCells Floors => floors;

        public void SetCellPointer(GridCellPointer pointer)
        {
            if (cellPointer == pointer) return;
            if (cellPointer != null) cellPointer.SetIndicatorSuppressed(false);
            cellPointer = pointer;
            if (IsActive && cellPointer != null) cellPointer.SetIndicatorSuppressed(true);
        }

        public void SetActive(bool active)
        {
            IsActive = active && CanOperate;
            if (cellPointer != null) cellPointer.SetIndicatorSuppressed(IsActive);
            if (!IsActive) CancelSelection();
        }

        public bool TryBuildFloors(IReadOnlyList<Vector2Int> cells)
        {
            if (!AreCellsInsideGrid(cells) || !floors.CanAddRange(cells)) return false;
            EnsureMaterials();
            if (floorMaterial == null) return false;
            if (!floors.TryAddRange(cells)) return false;
            foreach (Vector2Int cell in cells) visuals.Add(cell, CreateFloorVisual(cell, floorMaterial, false));
            return true;
        }

        public bool TryRemoveFloors(IReadOnlyList<Vector2Int> cells)
        {
            if (!floors.TryRemoveRange(cells)) return false;
            foreach (Vector2Int cell in cells)
            {
                if (!visuals.TryGetValue(cell, out GameObject visual)) continue;
                visuals.Remove(cell);
                DestroyVisual(visual);
            }

            return true;
        }

        private void Update()
        {
            if (!IsActive || Mouse.current == null)
            {
                CancelSelection();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelSelection();
                return;
            }

            Mouse mouse = Mouse.current;
            if (!TryGetPointedCell(mouse, out Vector2Int cell))
            {
                if (dragging)
                {
                    selectionIsValid = false;
                    HidePreviewVisuals();
                    if (mouse.leftButton.wasReleasedThisFrame) CancelSelection();
                }
                else HidePreviewVisuals();
                return;
            }

            if (!dragging && keyboard != null && keyboard.deleteKey.wasPressedThisFrame)
            {
                TryRemoveFloors(new[] { cell });
                return;
            }

            if (!dragging && mouse.leftButton.wasPressedThisFrame)
            {
                dragging = true;
                removing = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                dragStartCell = cell;
                UpdateSelection(cell);
                return;
            }

            if (!dragging) return;

            UpdateSelection(cell);
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (selectionIsValid)
                {
                    if (removing) TryRemoveFloors(selectedCells);
                    else TryBuildFloors(selectedCells);
                }

                CancelSelection();
            }
        }

        private void UpdateSelection(Vector2Int endCell)
        {
            FloorCells.GetRectangle(dragStartCell, endCell, selectedCells);
            selectionIsValid = removing
                ? floors.CanRemoveRange(selectedCells)
                : AreCellsInsideGrid(selectedCells) && floors.CanAddRange(selectedCells);
            EnsureMaterials();
            ShowPreviewVisuals(selectionIsValid ? validMaterial : invalidMaterial);
        }

        private bool TryGetPointedCell(Mouse mouse, out Vector2Int cell)
        {
            cell = default;
            if (FurniturePlacementController.IsMouseOverUi(mouse)) return false;
            Ray ray = inputCamera.ScreenPointToRay(mouse.position.ReadValue());
            Plane plane = new(Vector3.up, new Vector3(0f, GetGroundSurfaceHeight(), 0f));
            return plane.Raycast(ray, out float distance) && distance <= 500f
                && grid.TryGetCell(ray.GetPoint(distance), out cell);
        }

        private bool AreCellsInsideGrid(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0 || grid == null) return false;
            foreach (Vector2Int cell in cells)
                if (!grid.IsInside(cell)) return false;
            return true;
        }

        private void ShowPreviewVisuals(Material material)
        {
            EnsureMaterials();
            Material effectiveMaterial = material ?? invalidMaterial;
            for (int index = 0; index < selectedCells.Count; index++)
            {
                GameObject visual = GetPreviewVisual(index);
                PositionFloorVisual(visual, selectedCells[index]);
                visual.GetComponent<Renderer>().sharedMaterial = effectiveMaterial;
                visual.SetActive(true);
            }

            for (int index = selectedCells.Count; index < previewVisuals.Count; index++)
                previewVisuals[index].SetActive(false);
        }

        private GameObject GetPreviewVisual(int index)
        {
            while (previewVisuals.Count <= index)
            {
                GameObject visual = CreateFloorVisual(default, validMaterial, true);
                visual.name = "Floor Preview Cell";
                visual.SetActive(false);
                previewVisuals.Add(visual);
            }

            return previewVisuals[index];
        }

        private GameObject CreateFloorVisual(Vector2Int cell, Material material, bool isPreview)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = isPreview ? "Floor Preview Cell" : $"Floor_{cell.x}_{cell.y}";
            visual.transform.SetParent(transform, false);
            Collider collider = visual.GetComponent<Collider>();
            collider.enabled = false;
            DestroyVisual(collider);
            visual.GetComponent<Renderer>().sharedMaterial = material;
            PositionFloorVisual(visual, cell);
            return visual;
        }

        private void PositionFloorVisual(GameObject visual, Vector2Int cell)
        {
            float surfaceHeight = GetGroundSurfaceHeight() + SurfaceOffset;
            visual.transform.position = grid.GetCellCenter(cell, surfaceHeight + FloorThickness * 0.5f);
            visual.transform.rotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(grid.CellSize, FloorThickness, grid.CellSize);
        }

        private float GetGroundSurfaceHeight() => ground.bounds.max.y;

        private void EnsureMaterials()
        {
            if (floorMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP Lit shader is required for provisional floors.", this);
                SetActive(false);
                return;
            }

            floorMaterial = new Material(shader) { color = new Color(0.34f, 0.36f, 0.38f, 1f) };
            validMaterial = CreatePreviewMaterial(shader, new Color(0.20f, 0.85f, 0.30f, 0.55f));
            invalidMaterial = CreatePreviewMaterial(shader, new Color(0.90f, 0.20f, 0.20f, 0.55f));
        }

        private static Material CreatePreviewMaterial(Shader shader, Color color)
        {
            Material material = new(shader) { color = color };
            material.SetFloat("_Surface", 1f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }

        private void CancelSelection()
        {
            dragging = false;
            removing = false;
            selectionIsValid = false;
            selectedCells.Clear();
            HidePreviewVisuals();
        }

        private void HidePreviewVisuals()
        {
            foreach (GameObject visual in previewVisuals)
                if (visual != null) visual.SetActive(false);
        }

        private static void DestroyVisual(Object visual)
        {
            if (visual == null) return;
            if (Application.isPlaying) Destroy(visual);
            else DestroyImmediate(visual);
        }

        private void OnDisable() => SetActive(false);
        private void OnDestroy()
        {
            if (floorMaterial != null) Destroy(floorMaterial);
            if (validMaterial != null) Destroy(validMaterial);
            if (invalidMaterial != null) Destroy(invalidMaterial);
        }
    }
}
