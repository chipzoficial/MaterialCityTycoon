using UnityEngine;

namespace MaterialCityTycoon.Building
{
    [CreateAssetMenu(
        fileName = "FurnitureDefinition",
        menuName = "Material City Tycoon/Furniture Definition")]
    public sealed class FurnitureDefinition : ScriptableObject
    {
        [SerializeField] private string furnitureTypeId;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(1)] private int width = 1;
        [SerializeField, Min(1)] private int depth = 1;

        public string FurnitureTypeId => furnitureTypeId;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public int Width => width;
        public int Depth => depth;

        public bool IsValid(out string validationMessage)
        {
            if (string.IsNullOrWhiteSpace(furnitureTypeId))
            {
                validationMessage = "Furniture Type Id cannot be empty.";
                return false;
            }

            if (prefab == null)
            {
                validationMessage = "Prefab is not assigned.";
                return false;
            }

            if (width < 1 || depth < 1)
            {
                validationMessage = "Width and Depth must be at least 1.";
                return false;
            }

            validationMessage = null;
            return true;
        }

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            depth = Mathf.Max(1, depth);
        }
    }
}
