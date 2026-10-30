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
        private readonly Dictionary<WallEdge, GameObject> doorVisuals = new();
        private readonly Dictionary<WallEdge, float> doorBaseHeights = new();
        private readonly Dictionary<Vector2Int, GameObject> jointVisuals = new();
        private readonly List<WallEdge> draggedEdges = new();
        private readonly List<GameObject> previewVisuals = new();
        private readonly List<GameObject> previewJointVisuals = new();
        private readonly List<Vector2Int> affectedJunctions = new();
        private WallEdges edges;
        private Material wallMaterial, validMaterial, invalidMaterial;
        private WallEdge dragStartEdge;
        private Vector2Int dragStartVertex;
        private Vector3 dragStartGridPoint;
        private Vector2 dragStartScreenPosition;
        private float dragBaseHeight;
        private bool dragging;
        private bool dragExceededThreshold;
        private bool dragIsValid;
        private bool doorToolActive;
        private GameObject doorPreviewVisual;
        private const float WallHeight = 3f;
        private const float DoorOpeningHeight = 2.1f;
        private const float Thickness = 0.15f;
        private const float DoorPreviewOutlineThickness = 0.035f;
        private const float DoorPreviewOutlineDepth = Thickness + 0.01f;
        private const float DragThresholdPixels = 8f;
        public bool IsActive { get; private set; }
        public bool IsWallToolActive => IsActive && !doorToolActive;
        public bool IsDoorToolActive => IsActive && doorToolActive;
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

        public bool IsPassageBlocked(WallEdge edge)
        {
            if (edges == null) return true;
            if (edges.IsExternalEdge(edge)) return !edges.IsExternalPassageOpen(edge);
            return !edges.TryGetAdjacentCells(edge, out Vector2Int first, out Vector2Int second)
                || edges.IsPassageBlocked(first, second);
        }

        public bool IsDoorwayCell(Vector2Int cell) => edges != null && edges.IsDoorwayCell(cell);
        public bool IsDiagonalCellBlocked(Vector2Int cell) => edges != null && edges.ContainsDiagonalAtCell(cell);

        public void SetWallToolActive(bool active)
        {
            doorToolActive = false;
            SetActive(active);
        }

        public void SetDoorToolActive(bool active)
        {
            doorToolActive = active;
            SetActive(active);
        }

        public void SetActive(bool active)
        {
            IsActive = active && CanOperate;
            if (cellPointer != null) cellPointer.SetIndicatorSuppressed(IsActive);
            if (!IsActive)
            {
                doorToolActive = false;
                CancelDrag();
                HideDoorPreview();
            }
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

            if (doorToolActive)
            {
                UpdateDoorTool(mouse, keyboard);
                return;
            }

            if (dragging && keyboard != null
                && (keyboard.escapeKey.wasPressedThisFrame || keyboard.bKey.wasPressedThisFrame
                    || keyboard.digit1Key.wasPressedThisFrame || keyboard.digit2Key.wasPressedThisFrame
                    || keyboard.digit3Key.wasPressedThisFrame || keyboard.digit4Key.wasPressedThisFrame))
            {
                CancelDrag();
                return;
            }

            bool hasGroundPoint = TryGetGridPoint(mouse, out Vector3 gridPoint);

            if (dragging)
            {
                if (hasGroundPoint)
                {
                    if (HasExceededDragThreshold(dragStartScreenPosition, mouse.position.ReadValue()))
                    {
                        dragExceededThreshold = true;
                        UpdateDrag(gridPoint);
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

            if (!hasGroundPoint)
            {
                HidePreviewVisuals();
                return;
            }

            if (keyboard != null && keyboard.deleteKey.wasPressedThisFrame
                && edges.TryFindNearestExisting(gridPoint, out WallEdge edge) && edges.IsDoor(edge))
            {
                RestoreWallFromDoor(edge, GetGroundSurfaceHeight());
            }
            else if (keyboard != null && keyboard.deleteKey.wasPressedThisFrame
                && edges.TryFindNearestExisting(gridPoint, out edge) && edges.Remove(edge))
            {
                if (visuals.TryGetValue(edge, out GameObject old))
                {
                    visuals.Remove(edge);
                    Destroy(old);
                }

                RefreshFinalJunctionVisuals(new[] { edge }, GetGroundSurfaceHeight());
            }

            if (mouse.leftButton.wasPressedThisFrame
                && edges.TryFindNearest(gridPoint, out WallEdge startEdge)
                && edges.TryFindNearestVertex(gridPoint, out Vector2Int startVertex))
            {
                dragging = true;
                dragStartEdge = startEdge;
                dragStartVertex = startVertex;
                dragStartGridPoint = gridPoint;
                dragStartScreenPosition = mouse.position.ReadValue();
                dragBaseHeight = GetGroundSurfaceHeight();
                dragExceededThreshold = false;
                dragIsValid = false;
                draggedEdges.Clear();
                ShowHoveredEdge(dragStartEdge, dragBaseHeight);
            }
            else if (edges.TryFindNearest(gridPoint, out WallEdge hoveredEdge))
            {
                ShowHoveredEdge(hoveredEdge, GetGroundSurfaceHeight());
            }
        }

        private bool TryGetGridPoint(Mouse mouse, out Vector3 gridPoint)
        {
            gridPoint = default;
            if (FurniturePlacementController.IsMouseOverUi(mouse)) return false;
            Ray ray = inputCamera.ScreenPointToRay(mouse.position.ReadValue());
            return TryProjectRayToGridPlane(ray, out gridPoint);
        }

        /// <summary>
        /// Projects directly onto the horizontal construction surface instead of raycasting the
        /// Ground box collider. Near an exterior border, a collider raycast can hit a vertical
        /// side face and produce a fractional height that is not part of the construction grid.
        /// </summary>
        private bool TryProjectRayToGridPlane(Ray ray, out Vector3 gridPoint)
        {
            gridPoint = default;
            Plane gridPlane = new(Vector3.up, new Vector3(0f, GetGroundSurfaceHeight(), 0f));
            if (!gridPlane.Raycast(ray, out float distance) || distance > 500f)
            {
                return false;
            }

            gridPoint = ray.GetPoint(distance);
            return edges != null && edges.TryFindNearest(gridPoint, out _);
        }

        private float GetGroundSurfaceHeight() => ground.bounds.max.y;

        private void UpdateDoorTool(Mouse mouse, Keyboard keyboard)
        {
            if (!TryGetGridPoint(mouse, out Vector3 gridPoint)
                || !edges.TryFindNearest(gridPoint, out WallEdge edge))
            {
                HideDoorPreview();
                return;
            }

            if (edges.IsDoor(edge))
            {
                if (keyboard != null && keyboard.deleteKey.wasPressedThisFrame)
                {
                    RestoreWallFromDoor(edge, GetGroundSurfaceHeight());
                    ShowDoorPreview(edge, GetGroundSurfaceHeight(), CanInstallDoor(edge));
                }
                else
                {
                    HideDoorPreview();
                }
                return;
            }

            bool isValid = CanInstallDoor(edge);
            ShowDoorPreview(edge, GetGroundSurfaceHeight(), isValid);
            if (mouse.leftButton.wasPressedThisFrame && isValid && edges.TryInstallDoor(edge))
            {
                float baseHeight = GetWallBaseHeight(edge, GetGroundSurfaceHeight());
                doorBaseHeights[edge] = baseHeight;
                if (visuals.TryGetValue(edge, out GameObject wall))
                {
                    visuals.Remove(edge);
                    DestroyVisual(wall);
                }

                doorVisuals[edge] = CreateDoorVisual(edge, baseHeight, wallMaterial);
                ShowDoorPreview(edge, baseHeight, false);
            }
        }

        private bool CanInstallDoor(WallEdge edge)
        {
            if (!edges.CanInstallDoor(edge))
            {
                return false;
            }

            if (edges.TryGetAdjacentCells(edge, out Vector2Int firstCell, out Vector2Int secondCell))
            {
                return placementController == null || placementController.AreDoorwayCellsClear(firstCell, secondCell);
            }

            return edges.TryGetExternalDoorInteriorCell(edge, out Vector2Int interiorCell)
                && (placementController == null || placementController.IsDoorwayCellClear(interiorCell));
        }

        private void RestoreWallFromDoor(WallEdge edge, float fallbackBaseHeight)
        {
            if (!edges.RemoveDoor(edge)) return;

            float baseHeight = doorBaseHeights.TryGetValue(edge, out float stored) ? stored : fallbackBaseHeight;
            doorBaseHeights.Remove(edge);
            if (doorVisuals.TryGetValue(edge, out GameObject door))
            {
                doorVisuals.Remove(edge);
                DestroyVisual(door);
            }

            visuals[edge] = CreateWallVisual(edge, baseHeight, wallMaterial);
        }

        private void UpdateDrag(Vector3 gridPoint)
        {
            if (!edges.TryFindNearestVertex(gridPoint, out Vector2Int endVertex))
            {
                draggedEdges.Clear();
                dragIsValid = false;
                HidePreviewVisuals();
                return;
            }

            Vector3 worldDelta = gridPoint - dragStartGridPoint;
            WallEdgeOrientation orientation = WallEdges.GetSnappedOrientation(new Vector2(worldDelta.x, worldDelta.z));
            if (orientation == WallEdgeOrientation.DiagonalAscending || orientation == WallEdgeOrientation.DiagonalDescending)
            {
                bool ascending = orientation == WallEdgeOrientation.DiagonalAscending;
                if (!edges.TryFindNearestDiagonal(dragStartGridPoint, ascending, out WallEdge diagonalStart)
                    || !edges.TryFindNearestDiagonal(gridPoint, ascending, out WallEdge diagonalEnd)
                    || !edges.GetDiagonalLineFromDirection(diagonalStart, new Vector2Int(diagonalEnd.X, diagonalEnd.Z), draggedEdges))
                {
                    draggedEdges.Clear();
                }
            }
            else
            {
                if (dragStartEdge.Orientation == orientation)
                {
                    // Preserve the initially highlighted edge when the drag keeps its axis.
                    edges.GetStraightLineFromEdge(dragStartEdge, dragStartVertex, endVertex, draggedEdges);
                }
                else
                {
                    edges.GetOrthogonalLine(dragStartVertex, endVertex,
                        orientation == WallEdgeOrientation.AlongX, draggedEdges);
                }
            }
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
                RefreshFinalJunctionVisuals(draggedEdges, dragBaseHeight);
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
                RefreshFinalJunctionVisuals(draggedEdges, dragBaseHeight);
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
            ShowPreviewJunctionVisuals(draggedEdges, baseHeight, material);
        }

        private void ShowHoveredEdge(WallEdge edge, float baseHeight)
        {
            EnsureMaterials();
            GameObject visual = GetPreviewVisual(0);
            Position(visual, edge, baseHeight);
            draggedEdges.Clear();
            draggedEdges.Add(edge);
            visual.GetComponent<Renderer>().sharedMaterial = CanAddWalls(draggedEdges) ? validMaterial : invalidMaterial;
            ShowPreviewJunctionVisuals(draggedEdges, baseHeight, visual.GetComponent<Renderer>().sharedMaterial);
            draggedEdges.Clear();
            visual.SetActive(true);

            for (int index = 1; index < previewVisuals.Count; index++) previewVisuals[index].SetActive(false);
        }

        private bool CanAddWalls(IReadOnlyList<WallEdge> candidates)
        {
            return edges.CanAddRange(candidates)
                && (placementController == null || (placementController.AreDiagonalCellsClear(candidates)
                    && placementController.AreExistingFurnitureAccessesAvailable(candidates)));
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
            visual.name = name ?? $"Wall_{edge.Orientation}_{edge.X}_{edge.Z}";
            visual.transform.SetParent(transform, false);
            visual.GetComponent<Collider>().enabled = !isPreview;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            Position(visual, edge, baseHeight);
            return visual;
        }

        private GameObject CreateDoorVisual(WallEdge edge, float baseHeight, Material material, string name = null, bool isPreview = false)
        {
            GameObject root = new(name ?? $"Door_{(edge.AlongX ? "X" : "Z")}_{edge.X}_{edge.Z}");
            root.transform.SetParent(transform, false);
            root.transform.position = edges.Center(edge, baseHeight, 0f);
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            if (isPreview)
            {
                CreateDoorOpeningOutline(root, edge);
            }
            else
            {
                GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "Door Lintel";
                lintel.transform.SetParent(root.transform, false);
                lintel.transform.localPosition = Vector3.up * (DoorOpeningHeight + (WallHeight - DoorOpeningHeight) * 0.5f);
                lintel.transform.localRotation = Quaternion.identity;
                lintel.transform.localScale = edge.AlongX
                    ? new Vector3(grid.CellSize, WallHeight - DoorOpeningHeight, Thickness)
                    : new Vector3(Thickness, WallHeight - DoorOpeningHeight, grid.CellSize);
                lintel.GetComponent<Collider>().enabled = true;
                lintel.GetComponent<Renderer>().sharedMaterial = material;
            }
            return root;
        }

        private void ShowDoorPreview(WallEdge edge, float baseHeight, bool isValid)
        {
            EnsureMaterials();
            if (doorPreviewVisual == null)
            {
                doorPreviewVisual = CreateDoorVisual(edge, baseHeight, isValid ? validMaterial : invalidMaterial, "Door Preview", true);
            }
            else
            {
                PositionDoor(doorPreviewVisual, edge, baseHeight);
            }

            Material material = isValid ? validMaterial : invalidMaterial;
            foreach (Renderer renderer in doorPreviewVisual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }

            doorPreviewVisual.SetActive(true);
        }

        private void HideDoorPreview()
        {
            if (doorPreviewVisual != null) doorPreviewVisual.SetActive(false);
        }

        private float GetWallBaseHeight(WallEdge edge, float fallback)
        {
            return visuals.TryGetValue(edge, out GameObject visual) && visual != null
                ? visual.transform.position.y - WallHeight * 0.5f
                : fallback;
        }

        private void RefreshFinalJunctionVisuals(IReadOnlyList<WallEdge> affectedEdges, float fallbackBaseHeight)
        {
            foreach (WallEdge edge in affectedEdges)
            {
                if (edge.IsDiagonal) continue;
                RefreshFinalJunctionVisual(new Vector2Int(edge.X, edge.Z), fallbackBaseHeight);
                RefreshFinalJunctionVisual(edge.AlongX
                    ? new Vector2Int(edge.X + 1, edge.Z)
                    : new Vector2Int(edge.X, edge.Z + 1), fallbackBaseHeight);
            }
        }

        private void RefreshFinalJunctionVisual(Vector2Int vertex, float fallbackBaseHeight)
        {
            if (edges.GetConnectionCountAtVertex(vertex) < 2)
            {
                if (jointVisuals.TryGetValue(vertex, out GameObject obsolete))
                {
                    jointVisuals.Remove(vertex);
                    DestroyVisual(obsolete);
                }
                return;
            }

            float baseHeight = GetJunctionBaseHeight(vertex, fallbackBaseHeight);
            if (!jointVisuals.TryGetValue(vertex, out GameObject joint))
            {
                jointVisuals.Add(vertex, CreateWallJointVisual(vertex, baseHeight, wallMaterial));
            }
            else
            {
                PositionJoint(joint, vertex, baseHeight);
            }
        }

        private float GetJunctionBaseHeight(Vector2Int vertex, float fallback)
        {
            WallEdge[] incident =
            {
                new WallEdge(vertex.x - 1, vertex.y, true), new WallEdge(vertex.x, vertex.y, true),
                new WallEdge(vertex.x, vertex.y - 1, false), new WallEdge(vertex.x, vertex.y, false)
            };
            foreach (WallEdge edge in incident)
            {
                if (visuals.TryGetValue(edge, out GameObject visual) && visual != null)
                {
                    return visual.transform.position.y - WallHeight * 0.5f;
                }
            }

            return fallback;
        }

        private void ShowPreviewJunctionVisuals(IReadOnlyList<WallEdge> candidates, float baseHeight, Material material)
        {
            edges.GetAffectedJunctionVertices(candidates, affectedJunctions);
            for (int index = 0; index < affectedJunctions.Count; index++)
            {
                GameObject joint = GetPreviewJointVisual(index);
                PositionJoint(joint, affectedJunctions[index], baseHeight);
                joint.GetComponent<Renderer>().sharedMaterial = material;
                joint.SetActive(true);
            }

            for (int index = affectedJunctions.Count; index < previewJointVisuals.Count; index++) previewJointVisuals[index].SetActive(false);
        }

        private GameObject GetPreviewJointVisual(int index)
        {
            while (previewJointVisuals.Count <= index)
            {
                GameObject preview = CreateWallJointVisual(default, 0f, validMaterial, "Wall Preview Joint", true);
                preview.SetActive(false);
                previewJointVisuals.Add(preview);
            }

            return previewJointVisuals[index];
        }

        private GameObject CreateWallJointVisual(Vector2Int vertex, float baseHeight, Material material, string name = "Wall Joint", bool isPreview = false)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = name;
            visual.transform.SetParent(transform, false);
            visual.GetComponent<Collider>().enabled = !isPreview;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            PositionJoint(visual, vertex, baseHeight);
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
            if (edge.IsDiagonal)
            {
                visual.transform.rotation = Quaternion.Euler(0f, edge.IsAscendingDiagonal ? 45f : -45f, 0f);
                visual.transform.localScale = new Vector3(Mathf.Sqrt(2f) * grid.CellSize, WallHeight, Thickness);
                return;
            }

            visual.transform.rotation = Quaternion.identity;
            visual.transform.localScale = edge.AlongX ? new Vector3(grid.CellSize, WallHeight, Thickness) : new Vector3(Thickness, WallHeight, grid.CellSize);
        }

        private void PositionJoint(GameObject visual, Vector2Int vertex, float baseHeight)
        {
            visual.transform.position = edges.GetVertexPosition(vertex, baseHeight) + Vector3.up * (WallHeight * 0.5f);
            visual.transform.rotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(Thickness, WallHeight, Thickness);
        }

        private void PositionDoor(GameObject visual, WallEdge edge, float baseHeight)
        {
            visual.transform.position = edges.Center(edge, baseHeight, 0f);
            visual.transform.rotation = Quaternion.identity;
            Transform lintel = visual.transform.Find("Door Lintel");
            if (lintel != null)
            {
                lintel.localPosition = Vector3.up * (DoorOpeningHeight + (WallHeight - DoorOpeningHeight) * 0.5f);
                lintel.localRotation = Quaternion.identity;
                lintel.localScale = edge.AlongX
                    ? new Vector3(grid.CellSize, WallHeight - DoorOpeningHeight, Thickness)
                    : new Vector3(Thickness, WallHeight - DoorOpeningHeight, grid.CellSize);
            }

            PositionDoorOpeningOutline(visual.transform, edge);
        }

        private void CreateDoorOpeningOutline(GameObject root, WallEdge edge)
        {
            CreateDoorOpeningOutlineSide(root.transform, "Door Opening Left");
            CreateDoorOpeningOutlineSide(root.transform, "Door Opening Right");
            CreateDoorOpeningOutlineSide(root.transform, "Door Opening Header");
            PositionDoorOpeningOutline(root.transform, edge);
        }

        private static void CreateDoorOpeningOutlineSide(Transform parent, string name)
        {
            GameObject side = GameObject.CreatePrimitive(PrimitiveType.Cube);
            side.name = name;
            side.transform.SetParent(parent, false);
            side.GetComponent<Collider>().enabled = false;
        }

        private void PositionDoorOpeningOutline(Transform root, WallEdge edge)
        {
            Transform left = root.Find("Door Opening Left");
            Transform right = root.Find("Door Opening Right");
            Transform header = root.Find("Door Opening Header");
            if (left == null || right == null || header == null) return;

            float sideOffset = grid.CellSize * 0.5f - DoorPreviewOutlineThickness * 0.5f;
            Vector3 scale = edge.AlongX
                ? new Vector3(DoorPreviewOutlineThickness, DoorOpeningHeight, DoorPreviewOutlineDepth)
                : new Vector3(DoorPreviewOutlineDepth, DoorOpeningHeight, DoorPreviewOutlineThickness);
            Vector3 axis = edge.AlongX ? Vector3.right : Vector3.forward;
            Vector3 height = Vector3.up * (DoorOpeningHeight * 0.5f);
            left.localPosition = height - axis * sideOffset;
            right.localPosition = height + axis * sideOffset;
            left.localRotation = Quaternion.identity;
            right.localRotation = Quaternion.identity;
            left.localScale = scale;
            right.localScale = scale;
            header.localPosition = Vector3.up * (DoorOpeningHeight - DoorPreviewOutlineThickness * 0.5f);
            header.localRotation = Quaternion.identity;
            header.localScale = edge.AlongX
                ? new Vector3(grid.CellSize, DoorPreviewOutlineThickness, DoorPreviewOutlineDepth)
                : new Vector3(DoorPreviewOutlineDepth, DoorPreviewOutlineThickness, grid.CellSize);
        }

        private static void DestroyVisual(Object visual)
        {
            if (visual == null) return;
            if (Application.isPlaying) Destroy(visual);
            else DestroyImmediate(visual);
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

            foreach (GameObject preview in previewJointVisuals)
            {
                if (preview != null) preview.SetActive(false);
            }

            HideDoorPreview();
        }

        private void OnDisable() { SetActive(false); }
        private void OnDestroy()
        {
            if (doorPreviewVisual != null) DestroyVisual(doorPreviewVisual);
            if (wallMaterial != null) Destroy(wallMaterial);
            if (validMaterial != null) Destroy(validMaterial);
            if (invalidMaterial != null) Destroy(invalidMaterial);
        }
    }
}
