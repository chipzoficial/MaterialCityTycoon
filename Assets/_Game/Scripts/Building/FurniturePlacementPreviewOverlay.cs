using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaterialCityTycoon.Building
{
    public enum PlacementCellKind { ValidFootprint, InvalidFootprint, FreeAccess, SharedAccess, BlockedAccess }

    public readonly struct PlacementPreviewCell
    {
        public readonly Vector2Int Cell;
        public readonly PlacementCellKind Kind;

        public PlacementPreviewCell(Vector2Int cell, PlacementCellKind kind)
        {
            Cell = cell;
            Kind = kind;
        }
    }

    /// <summary>Owns a single collider-free runtime mesh and its temporary materials.</summary>
    internal sealed class FurniturePlacementPreviewOverlay
    {
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material[] materials;
        private readonly List<PlacementPreviewCell> previous = new();
        private Vector3 lastGridPosition;
        private float lastCellSize;
        private float lastHeight;
        private int lastWidth;
        private int lastDepth;
        private bool hasGeometry;

        public FurniturePlacementPreviewOverlay()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("The URP Unlit shader is unavailable; construction cell preview cannot be displayed.");
                return;
            }

            root = new GameObject("Furniture Cell Preview");
            MeshFilter filter = root.AddComponent<MeshFilter>();
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            mesh = new Mesh { name = "Furniture Cell Preview" };
            filter.sharedMesh = mesh;
            Color[] colors = { new(0.1f, 0.9f, 0.25f, 0.32f), new(1f, 0.12f, 0.1f, 0.4f),
                new(0.05f, 0.85f, 1f, 0.36f), new(0.1f, 0.48f, 1f, 0.48f), new(1f, 0.12f, 0.1f, 0.4f) };
            materials = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                Material material = new(shader) { name = "Furniture Preview (runtime)", renderQueue = (int)RenderQueue.Transparent };
                material.SetColor("_BaseColor", colors[i]);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
            root.SetActive(false);
        }

        public void Show(ConstructionGrid grid, float groundHeight, IReadOnlyList<PlacementPreviewCell> cells)
        {
            if (root == null) return;
            float height = groundHeight + 0.035f;
            bool changed = !hasGeometry || lastGridPosition != grid.transform.position
                || lastWidth != grid.Width || lastDepth != grid.Height
                || !Mathf.Approximately(lastCellSize, grid.CellSize) || !Mathf.Approximately(lastHeight, height)
                || previous.Count != cells.Count;
            for (int i = 0; !changed && i < cells.Count; i++)
            {
                changed = previous[i].Cell != cells[i].Cell || previous[i].Kind != cells[i].Kind;
            }

            if (changed)
            {
                List<Vector3> vertices = new(cells.Count * 4);
                List<int>[] triangles = new List<int>[materials.Length];
                for (int i = 0; i < triangles.Length; i++) triangles[i] = new List<int>();
                float half = grid.CellSize * 0.46f;
                foreach (PlacementPreviewCell cell in cells)
                {
                    Vector3 center = grid.GetCellCenter(cell.Cell, height);
                    int start = vertices.Count;
                    vertices.Add(center + new Vector3(-half, 0f, -half));
                    vertices.Add(center + new Vector3(-half, 0f, half));
                    vertices.Add(center + new Vector3(half, 0f, half));
                    vertices.Add(center + new Vector3(half, 0f, -half));
                    List<int> indices = triangles[(int)cell.Kind];
                    indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                    indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
                }

                mesh.Clear();
                mesh.SetVertices(vertices);
                mesh.subMeshCount = materials.Length;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                previous.Clear();
                for (int i = 0; i < cells.Count; i++) previous.Add(cells[i]);
                lastGridPosition = grid.transform.position;
                lastCellSize = grid.CellSize;
                lastHeight = height;
                lastWidth = grid.Width;
                lastDepth = grid.Height;
                hasGeometry = true;
            }

            root.SetActive(cells.Count > 0);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            if (mesh != null) Object.Destroy(mesh);
            if (materials != null)
            {
                foreach (Material material in materials) Object.Destroy(material);
            }
        }
    }
}
