using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialCityTycoon.Commerce
{
    public enum ProductSellingUnit
    {
        Unit,
        Package,
        Box,
        Bag,
        Meter,
        CubicMeter
    }

    public enum ProductDisplayMode
    {
        Stack,
        Hang,
        Bin,
        Pallet,
        Bulk,
        Showcase
    }

    [CreateAssetMenu(fileName = "ProductDefinition", menuName = "Material City Tycoon/Product Definition")]
    public sealed class ProductDefinition : ScriptableObject
    {
        [SerializeField] private string productId;
        [SerializeField] private string displayName;
        [SerializeField] private string categoryId;
        [SerializeField] private ProductSellingUnit sellingUnit = ProductSellingUnit.Unit;
        [SerializeField] private ProductDisplayMode[] allowedDisplayModes = Array.Empty<ProductDisplayMode>();

        public string ProductId => productId;
        public string DisplayName => displayName;
        public string CategoryId => categoryId;
        public ProductSellingUnit SellingUnit => sellingUnit;
        public IReadOnlyList<ProductDisplayMode> AllowedDisplayModes => allowedDisplayModes;

        public bool IsValid(out string validationMessage)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                validationMessage = "Product Type Id is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                validationMessage = "Display Name is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(categoryId))
            {
                validationMessage = "Category Id is required.";
                return false;
            }

            if (allowedDisplayModes == null || allowedDisplayModes.Length == 0)
            {
                validationMessage = "At least one allowed display mode is required.";
                return false;
            }

            for (int index = 0; index < allowedDisplayModes.Length; index++)
            {
                for (int otherIndex = index + 1; otherIndex < allowedDisplayModes.Length; otherIndex++)
                {
                    if (allowedDisplayModes[index] != allowedDisplayModes[otherIndex])
                    {
                        continue;
                    }

                    validationMessage = "Allowed display modes cannot contain duplicates.";
                    return false;
                }
            }

            validationMessage = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            allowedDisplayModes ??= Array.Empty<ProductDisplayMode>();
        }
    }
}
