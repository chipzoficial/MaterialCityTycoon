using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Renders the existing construction grid as a single procedural mesh.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ConstructionGridOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ConstructionGrid constructionGrid;
        [SerializeField] private Material lineMaterial;

        [Header("Appearance")]
        [SerializeField, Min(0.001f)] private float lineThickness = 0.03f;
        [SerializeField, Min(0f)] private float heightOffset = 0.02f;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Collider gridCollider;
        private Mesh generatedMesh;
        private GridConfiguration cachedConfiguration;
        private bool hasCachedConfiguration;

        private void Awake()
        {
            if (constructionGrid == null)
            {
                Debug.LogError("ConstructionGridOverlay requires a ConstructionGrid reference.", this);
                enabled = false;
                return;
            }

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            gridCollider = constructionGrid.GetComponent<Collider>();
            meshRenderer.sharedMaterial = lineMaterial;

            generatedMesh = new Mesh { name = "Construction Grid Overlay" };
            meshFilter.sharedMesh = generatedMesh;
            RebuildMesh();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.gKey.wasPressedThisFrame)
            {
                meshRenderer.enabled = !meshRenderer.enabled;
            }

            if (HasGridConfigurationChanged())
            {
                RebuildMesh();
            }
        }

        private void OnValidate()
        {
            lineThickness = Mathf.Max(0.001f, lineThickness);
            heightOffset = Mathf.Max(0f, heightOffset);

            if (Application.isPlaying && generatedMesh != null)
            {
                meshRenderer.sharedMaterial = lineMaterial;
                RebuildMesh();
            }
        }

        private void OnDestroy()
        {
            if (generatedMesh != null)
            {
                Destroy(generatedMesh);
            }
        }

        private bool HasGridConfigurationChanged()
        {
            GridConfiguration current = GetCurrentConfiguration();
            return !hasCachedConfiguration || !current.Equals(cachedConfiguration);
        }

        private void RebuildMesh()
        {
            GridConfiguration configuration = GetCurrentConfiguration();
            float minimumX = constructionGrid.GetCellCenter(Vector2Int.zero, configuration.surfaceHeight).x
                - configuration.cellSize * 0.5f;
            float minimumZ = constructionGrid.GetCellCenter(Vector2Int.zero, configuration.surfaceHeight).z
                - configuration.cellSize * 0.5f;
            float maximumX = minimumX + configuration.width * configuration.cellSize;
            float maximumZ = minimumZ + configuration.height * configuration.cellSize;

            int lineCount = configuration.width + configuration.height + 2;
            List<Vector3> vertices = new(lineCount * 4);
            List<int> triangles = new(lineCount * 6);

            for (int x = 0; x <= configuration.width; x++)
            {
                float positionX = minimumX + x * configuration.cellSize;
                AddLine(
                    new Vector3(positionX, configuration.surfaceHeight, minimumZ),
                    new Vector3(positionX, configuration.surfaceHeight, maximumZ),
                    vertices,
                    triangles);
            }

            for (int z = 0; z <= configuration.height; z++)
            {
                float positionZ = minimumZ + z * configuration.cellSize;
                AddLine(
                    new Vector3(minimumX, configuration.surfaceHeight, positionZ),
                    new Vector3(maximumX, configuration.surfaceHeight, positionZ),
                    vertices,
                    triangles);
            }

            generatedMesh.Clear();
            generatedMesh.SetVertices(vertices);
            generatedMesh.SetTriangles(triangles, 0);
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();

            cachedConfiguration = configuration;
            hasCachedConfiguration = true;
        }

        private void AddLine(Vector3 worldStart, Vector3 worldEnd, List<Vector3> vertices, List<int> triangles)
        {
            Vector3 direction = (worldEnd - worldStart).normalized;
            Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x) * (lineThickness * 0.5f);
            int firstVertex = vertices.Count;

            vertices.Add(transform.InverseTransformPoint(worldStart - perpendicular));
            vertices.Add(transform.InverseTransformPoint(worldStart + perpendicular));
            vertices.Add(transform.InverseTransformPoint(worldEnd + perpendicular));
            vertices.Add(transform.InverseTransformPoint(worldEnd - perpendicular));

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 1);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 3);
        }

        private GridConfiguration GetCurrentConfiguration()
        {
            float surfaceHeight = gridCollider != null
                ? gridCollider.bounds.max.y + heightOffset
                : constructionGrid.transform.position.y + heightOffset;

            return new GridConfiguration(
                constructionGrid.Width,
                constructionGrid.Height,
                constructionGrid.CellSize,
                constructionGrid.transform.position,
                surfaceHeight,
                lineThickness);
        }

        private readonly struct GridConfiguration
        {
            public readonly int width;
            public readonly int height;
            public readonly float cellSize;
            public readonly Vector3 gridPosition;
            public readonly float surfaceHeight;
            public readonly float thickness;

            public GridConfiguration(
                int width,
                int height,
                float cellSize,
                Vector3 gridPosition,
                float surfaceHeight,
                float thickness)
            {
                this.width = width;
                this.height = height;
                this.cellSize = cellSize;
                this.gridPosition = gridPosition;
                this.surfaceHeight = surfaceHeight;
                this.thickness = thickness;
            }

            public bool Equals(GridConfiguration other)
            {
                return width == other.width
                    && height == other.height
                    && Mathf.Approximately(cellSize, other.cellSize)
                    && gridPosition == other.gridPosition
                    && Mathf.Approximately(surfaceHeight, other.surfaceHeight)
                    && Mathf.Approximately(thickness, other.thickness);
            }
        }
    }
}
