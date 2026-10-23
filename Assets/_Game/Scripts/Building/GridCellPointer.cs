using UnityEngine;
using UnityEngine.InputSystem;

namespace MaterialCityTycoon.Building
{
    /// <summary>
    /// Raycasts the mouse pointer onto the ground and positions an indicator on the
    /// corresponding construction grid cell.
    /// </summary>
    public sealed class GridCellPointer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ConstructionGrid constructionGrid;
        [SerializeField] private UnityEngine.Camera inputCamera;
        [SerializeField] private Transform cellIndicator;
        [SerializeField] private Collider groundCollider;

        [Header("Raycast")]
        [SerializeField] private LayerMask groundLayers = 1;
        [SerializeField, Min(0.01f)] private float raycastDistance = 500f;
        [SerializeField, Min(0f)] private float indicatorHeightOffset = 0.02f;

        private bool hasPointedCell;
        private Vector2Int pointedCell;
        private readonly RaycastHit[] raycastHits = new RaycastHit[8];
        private Renderer[] indicatorRenderers;
        private Material[][] originalIndicatorMaterials;
        private Material overrideIndicatorMaterial;

        public bool TryGetPointedCell(out Vector2Int cell)
        {
            cell = pointedCell;
            return hasPointedCell;
        }

        private void Awake()
        {
            if (constructionGrid == null || inputCamera == null || cellIndicator == null)
            {
                Debug.LogError("GridCellPointer requires a grid, camera, and cell indicator.", this);
                enabled = false;
                return;
            }

            if (groundCollider == null)
            {
                groundCollider = constructionGrid.GetComponent<Collider>();
            }

            if (groundCollider == null)
            {
                Debug.LogError("GridCellPointer requires a Collider on the grid ground.", this);
                enabled = false;
                return;
            }

            cellIndicator.gameObject.SetActive(false);
            CacheIndicatorMaterials();
        }

        public void SetIndicatorMaterial(Material material)
        {
            if (material == null)
            {
                RestoreIndicatorMaterial();
                return;
            }

            if (overrideIndicatorMaterial == material)
            {
                return;
            }

            for (int index = 0; index < indicatorRenderers.Length; index++)
            {
                Material[] materials = indicatorRenderers[index].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    materials[materialIndex] = material;
                }

                indicatorRenderers[index].sharedMaterials = materials;
            }

            overrideIndicatorMaterial = material;
        }

        public void RestoreIndicatorMaterial()
        {
            if (overrideIndicatorMaterial == null)
            {
                return;
            }

            for (int index = 0; index < indicatorRenderers.Length; index++)
            {
                indicatorRenderers[index].sharedMaterials = originalIndicatorMaterials[index];
            }

            overrideIndicatorMaterial = null;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                HideIndicator();
                return;
            }

            Ray ray = inputCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (TryGetGroundHit(ray, out RaycastHit hit)
                && constructionGrid.TryGetCell(hit.point, out Vector2Int cell))
            {
                pointedCell = cell;
                hasPointedCell = true;
                cellIndicator.position = constructionGrid.GetCellCenter(
                    cell,
                    hit.point.y + indicatorHeightOffset);
                cellIndicator.gameObject.SetActive(true);
                return;
            }

            HideIndicator();
        }

        private bool TryGetGroundHit(Ray ray, out RaycastHit groundHit)
        {
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                raycastHits,
                raycastDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            for (int index = 0; index < hitCount; index++)
            {
                if (raycastHits[index].collider == groundCollider)
                {
                    groundHit = raycastHits[index];
                    return true;
                }
            }

            groundHit = default;
            return false;
        }

        private void CacheIndicatorMaterials()
        {
            indicatorRenderers = cellIndicator.GetComponentsInChildren<Renderer>(true);
            originalIndicatorMaterials = new Material[indicatorRenderers.Length][];

            for (int index = 0; index < indicatorRenderers.Length; index++)
            {
                originalIndicatorMaterials[index] = indicatorRenderers[index].sharedMaterials;
            }
        }

        private void HideIndicator()
        {
            hasPointedCell = false;

            if (cellIndicator != null)
            {
                cellIndicator.gameObject.SetActive(false);
            }
        }

        private void OnValidate()
        {
            raycastDistance = Mathf.Max(0.01f, raycastDistance);
            indicatorHeightOffset = Mathf.Max(0f, indicatorHeightOffset);
        }
    }
}
