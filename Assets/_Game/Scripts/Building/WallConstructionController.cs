using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MaterialCityTycoon.Building
{
    /// <summary>Places one visual wall per occupied edge, without changing furniture occupancy.</summary>
    public sealed class WallConstructionController : MonoBehaviour
    {
        [SerializeField] private ConstructionGrid grid;
        [SerializeField] private UnityEngine.Camera inputCamera;
        [SerializeField] private Collider ground;
        [SerializeField] private GridCellPointer cellPointer;
        private FurniturePlacementController placementController;
        private readonly Dictionary<WallEdge, GameObject> visuals = new();
        private readonly List<WallEdge> draggedEdges = new();
        private readonly List<GameObject> previewVisuals = new();
        private WallEdges edges;
        private Material wallMaterial, validMaterial, invalidMaterial;
        private WallEdge dragStartEdge;
        private Vector2Int dragStartVertex;
        private Vector2 dragStartScreenPosition;
        private float dragBaseHeight;
        private bool dragging;
        private bool dragExceededThreshold;
        private bool dragIsValid;
        private const float WallHeight = 3f;
        private const float Thickness = 0.12f;
        private const float DragThresholdPixels = 8f;
        public bool IsActive { get; private set; }
        public bool CanOperate => grid != null && inputCamera != null && ground != null;

        public void SetCellPointer(GridCellPointer pointer)
        {
            if (cellPointer == pointer)
            {
                return;
            }

            if (cellPointer != null)
            {
                cellPointer.SetIndicatorSuppressed(false);
            }

            cellPointer = pointer;
            if (IsActive && cellPointer != null)
            {
                cellPointer.SetIndicatorSuppressed(true);
            }
        }

        public void SetPlacementController(FurniturePlacementController controller)
        {
            placementController = controller;
        }

        public bool ContainsEdge(WallEdge edge)
        {
            return edges != null && edges.Contains(edge);
        }

        public void SetActive(bool active)
        {
            IsActive = active && CanOperate;
            if (cellPointer != null) cellPointer.SetIndicatorSuppressed(IsActive);
            if (!IsActive) CancelDrag();
        }

        private void Update()
        {
            if (!IsActive || Mouse.current == null)
            {
                CancelDrag();
                return;
            }

            edges ??= new WallEdges(grid);
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (dragging && keyboard != null
                && (keyboard.escapeKey.wasPressedThisFrame || keyboard.bKey.wasPressedThisFrame
                    || keyboard.digit1Key.wasPressedThisFrame || keyboard.digit2Key.wasPressedThisFrame
                    || keyboard.digit3Key.wasPressedThisFrame || keyboard.digit4Key.wasPressedThisFrame))
            {
                CancelDrag();
                return;
            }

            bool hasGroundHit = TryGetGroundHit(mouse, out RaycastHit hit);

            if (dragging)
            {
                if (hasGroundHit)
                {
                    if (HasExceededDragThreshold(dragStartScreenPosition, mouse.position.ReadValue()))
                    {
                        dragExceededThreshold = true;
                        UpdateDrag(hit);
                    }
                    else
                    {
                        ShowHoveredEdge(dragStartEdge, dragBaseHeight);
                    }
                }
                else
                {
                    // Releasing over UI or outside the ground must never become a simple click.
                    dragExceededThreshold = true;
                    dragIsValid = false;
                    HidePreviewVisuals();
                }

                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    if (dragExceededThreshold)
                    {
                        TryConfirmDrag();
                    }
                    else
                    {
                        TryConfirmClick();
                    }
                }

                return;
            }

            if (!hasGroundHit)
            {
                HidePreviewVisuals();
                return;
            }

            if (keyboard != null && keyboard.deleteKey.wasPressedThisFrame
                && edges.TryFindNearest(hit.point, out WallEdge edge) && edges.Remove(edge)
                && visuals.TryGetValue(edge, out GameObject old))
            {
                visuals.Remove(edge);
                Destroy(old);
            }

            if (mouse.leftButton.wasPressedThisFrame
                && edges.TryFindNearest(hit.point, out WallEdge startEdge)
                && edges.TryFindNearestVertex(hit.point, out Vector2Int startVertex))
            {
                dragging = true;
                dragStartEdge = startEdge;
                dragStartVertex = startVertex;
                dragStartScreenPosition = mouse.position.ReadValue();
                dragBaseHeight = hit.point.y;
                dragExceededThreshold = false;
                dragIsValid = false;
                draggedEdges.Clear();
                ShowHoveredEdge(dragStartEdge, dragBaseHeight);
            }
            else if (edges.TryFindNearest(hit.point, out WallEdge hoveredEdge))
            {
                ShowHoveredEdge(hoveredEdge, hit.point.y);
            }
        }

        private bool TryGetGroundHit(Mouse mouse, out RaycastHit hit)
        {
            hit = default;
            if (FurniturePlacementController.IsMouseOverUi(mouse)) return false;
            Ray ray = inputCamera.ScreenPointToRay(mouse.position.ReadValue());
            return ground.Raycast(ray, out hit, 500f);
        }

        private void UpdateDrag(RaycastHit hit)
        {
            if (!edges.TryFindNearestVertex(hit.point, out Vector2Int endVertex))
            {
                draggedEdges.Clear();
                dragIsValid = false;
                HidePreviewVisuals();
                return;
            }

            edges.GetStraightLineFromEdge(dragStartEdge, dragStartVertex, endVertex, draggedEdges);
            if (draggedEdges.Count == 0)
            {
                dragIsValid = false;
                ShowHoveredEdge(dragStartEdge, dragBaseHeight);
                return;
            }

            dragIsValid = CanAddWalls(draggedEdges);
            ShowPreviewVisuals(dragBaseHeight, dragIsValid);
        }

        private static bool HasExceededDragThreshold(Vector2 start, Vector2 current)
        {
            return (current - start).sqrMagnitude >= DragThresholdPixels * DragThresholdPixels;
        }

        private void TryConfirmClick()
        {
            draggedEdges.Clear();
            draggedEdges.Add(dragStartEdge);
            if (CanAddWalls(draggedEdges) && edges.TryAddRange(draggedEdges))
            {
                visuals.Add(dragStartEdge, CreateWallVisual(dragStartEdge, dragBaseHeight, wallMaterial));
            }

            CancelDrag();
        }

        private void TryConfirmDrag()
        {
            if (dragIsValid && CanAddWalls(draggedEdges) && edges.TryAddRange(draggedEdges))
            {
                foreach (WallEdge edge in draggedEdges)
                {
                    visuals.Add(edge, CreateWallVisual(edge, dragBaseHeight, wallMaterial));
                }
            }

            CancelDrag();
        }

        private void ShowPreviewVisuals(float baseHeight, bool isValid)
        {
            EnsureMaterials();
            Material material = isValid ? validMaterial : invalidMaterial;
            for (int index = 0; index < draggedEdges.Count; index++)
            {
                GameObject visual = GetPreviewVisual(index);
                Position(visual, draggedEdges[index], baseHeight);
                visual.GetComponent<Renderer>().sharedMaterial = material;
                visual.SetActive(true);
            }

            for (int index = draggedEdges.Count; index < previewVisuals.Count; index++) previewVisuals[index].SetActive(false);
        }

        private void ShowHoveredEdge(WallEdge edge, float baseHeight)
        {
            EnsureMaterials();
            GameObject visual = GetPreviewVisual(0);
            Position(visual, edge, baseHeight);
            draggedEdges.Clear();
            draggedEdges.Add(edge);
            visual.GetComponent<Renderer>().sharedMaterial = CanAddWalls(draggedEdges) ? validMaterial : invalidMaterial;
            draggedEdges.Clear();
            visual.SetActive(true);

            for (int index = 1; index < previewVisuals.Count; index++) previewVisuals[index].SetActive(false);
        }

        private bool CanAddWalls(IReadOnlyList<WallEdge> candidates)
        {
            return edges.CanAddRange(candidates)
                && (placementController == null || placementController.AreExistingFurnitureAccessesAvailable(candidates));
        }

        private GameObject GetPreviewVisual(int index)
        {
            while (previewVisuals.Count <= index)
            {
                GameObject preview = CreateWallVisual(default, 0f, validMaterial, "Wall Preview Segment", true);
                preview.SetActive(false);
                previewVisuals.Add(preview);
            }

            return previewVisuals[index];
        }

        private GameObject CreateWallVisual(WallEdge edge, float baseHeight, Material material, string name = null, bool isPreview = false)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = name ?? $"Wall_{(edge.AlongX ? "X" : "Z")}_{edge.X}_{edge.Z}";
            visual.transform.SetParent(transform, false);
            visual.GetComponent<Collider>().enabled = !isPreview;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            Position(visual, edge, baseHeight);
            return visual;
        }

        private void EnsureMaterials()
        {
            if (wallMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP Lit shader is required for prototype walls.", this);
                SetActive(false);
                return;
            }

            wallMaterial = new Material(shader) { color = new Color(0.72f, 0.72f, 0.68f) };
            validMaterial = new Material(shader) { color = new Color(0.2f, 0.85f, 0.3f) };
            invalidMaterial = new Material(shader) { color = new Color(0.9f, 0.2f, 0.2f) };
        }

        private void Position(GameObject visual, WallEdge edge, float baseHeight)
        {
            visual.transform.position = edges.Center(edge, baseHeight, WallHeight);
            visual.transform.rotation = Quaternion.identity;
            visual.transform.localScale = edge.AlongX ? new Vector3(grid.CellSize, WallHeight, Thickness) : new Vector3(Thickness, WallHeight, grid.CellSize);
        }

        private void CancelDrag()
        {
            dragging = false;
            dragExceededThreshold = false;
            dragIsValid = false;
            draggedEdges.Clear();
            HidePreviewVisuals();
        }

        private void HidePreviewVisuals()
        {
            foreach (GameObject preview in previewVisuals)
            {
                if (preview != null) preview.SetActive(false);
            }
        }

        private void OnDisable() { SetActive(false); }
        private void OnDestroy()
        {
            if (wallMaterial != null) Destroy(wallMaterial);
            if (validMaterial != null) Destroy(validMaterial);
            if (invalidMaterial != null) Destroy(invalidMaterial);
        }
    }
}
