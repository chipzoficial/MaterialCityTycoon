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
        private readonly Dictionary<WallEdge, GameObject> visuals = new();
        private WallEdges edges;
        private GameObject preview;
        private Material wallMaterial, validMaterial, invalidMaterial;
        private const float Height = 2.8f, Thickness = 0.12f;
        public bool IsActive { get; private set; }
        public bool CanOperate => grid != null && inputCamera != null && ground != null;

        public void SetActive(bool active)
        {
            IsActive = active && CanOperate;
            if (!IsActive && preview != null) preview.SetActive(false);
        }

        private void Update()
        {
            if (!IsActive || Mouse.current == null) { if (preview != null) preview.SetActive(false); return; }
            Mouse mouse = Mouse.current;
            Ray ray = inputCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (FurniturePlacementController.IsMouseOverUi(mouse) || !ground.Raycast(ray, out RaycastHit hit, 500f))
            { if (preview != null) preview.SetActive(false); return; }
            edges ??= new WallEdges(grid);
            if (!edges.TryFindNearest(hit.point, out WallEdge edge))
            { if (preview != null) preview.SetActive(false); return; }
            EnsurePreview();
            if (preview == null) return;
            Position(preview, edge, hit.point.y);
            preview.GetComponent<Renderer>().sharedMaterial = edges.Contains(edge) ? invalidMaterial : validMaterial;
            preview.SetActive(true);
            if (Keyboard.current != null && Keyboard.current.deleteKey.wasPressedThisFrame && edges.Remove(edge))
            {
                if (visuals.TryGetValue(edge, out GameObject old)) { visuals.Remove(edge); Destroy(old); }
                preview.GetComponent<Renderer>().sharedMaterial = validMaterial;
            }
            else if (mouse.leftButton.wasPressedThisFrame && edges.TryAdd(edge))
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{(edge.AlongX ? "X" : "Z")}_{edge.X}_{edge.Z}";
                wall.transform.SetParent(transform, false);
                wall.GetComponent<Collider>().enabled = false;
                wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
                Position(wall, edge, hit.point.y);
                visuals.Add(edge, wall);
                preview.GetComponent<Renderer>().sharedMaterial = invalidMaterial;
            }
        }

        private void EnsurePreview()
        {
            if (preview != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("URP Lit shader is required for prototype walls.", this); SetActive(false); return; }
            wallMaterial = new Material(shader) { color = new Color(0.72f, 0.72f, 0.68f) };
            validMaterial = new Material(shader) { color = new Color(0.2f, 0.85f, 0.3f) };
            invalidMaterial = new Material(shader) { color = new Color(0.9f, 0.2f, 0.2f) };
            preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.name = "Wall Preview";
            preview.transform.SetParent(transform, false);
            preview.GetComponent<Collider>().enabled = false;
        }

        private void Position(GameObject visual, WallEdge edge, float baseHeight)
        {
            visual.transform.position = edges.Center(edge, baseHeight, Height);
            visual.transform.rotation = Quaternion.identity;
            visual.transform.localScale = edge.AlongX ? new Vector3(grid.CellSize, Height, Thickness) : new Vector3(Thickness, Height, grid.CellSize);
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
