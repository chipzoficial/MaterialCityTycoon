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

        [Header("Raycast")]
        [SerializeField] private LayerMask groundLayers = 1;
        [SerializeField, Min(0.01f)] private float raycastDistance = 500f;
        [SerializeField, Min(0f)] private float indicatorHeightOffset = 0.02f;

        private bool hasPointedCell;
        private Vector2Int pointedCell;

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

            cellIndicator.gameObject.SetActive(false);
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
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, groundLayers, QueryTriggerInteraction.Ignore)
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
