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
        [SerializeField] private GameObject displayPrefab;

        public string ProductId => productId;
        public string DisplayName => displayName;
        public string CategoryId => categoryId;
        public ProductSellingUnit SellingUnit => sellingUnit;
        public IReadOnlyList<ProductDisplayMode> AllowedDisplayModes => allowedDisplayModes;
        public GameObject DisplayPrefab => displayPrefab;

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

    /// <summary>
    /// Holds the mutable on-hand quantities for a single store during the current session.
    /// </summary>
    public sealed class StoreInventory
    {
        private readonly Dictionary<string, int> quantitiesByProductId = new(StringComparer.Ordinal);

        public int GetQuantity(string productId)
        {
            return IsProductIdValid(productId)
                && quantitiesByProductId.TryGetValue(productId, out int quantity)
                ? quantity
                : 0;
        }

        public int GetQuantity(ProductDefinition productDefinition)
        {
            return IsProductDefinitionValid(productDefinition)
                ? GetQuantity(productDefinition.ProductId)
                : 0;
        }

        public bool CanAddUnits(string productId, int units)
        {
            return IsProductIdValid(productId)
                && units > 0
                && GetQuantity(productId) <= int.MaxValue - units;
        }

        public bool CanRemoveUnits(string productId, int units)
        {
            return IsProductIdValid(productId)
                && units > 0
                && quantitiesByProductId.TryGetValue(productId, out int currentQuantity)
                && units <= currentQuantity;
        }

        public bool TryAddUnits(string productId, int units)
        {
            if (!CanAddUnits(productId, units))
            {
                return false;
            }

            quantitiesByProductId[productId] = GetQuantity(productId) + units;
            return true;
        }

        public bool TryAddUnits(ProductDefinition productDefinition, int units)
        {
            return IsProductDefinitionValid(productDefinition)
                && TryAddUnits(productDefinition.ProductId, units);
        }

        public bool TryRemoveUnits(string productId, int units)
        {
            if (!CanRemoveUnits(productId, units))
            {
                return false;
            }

            int currentQuantity = quantitiesByProductId[productId];
            int remainingQuantity = currentQuantity - units;
            if (remainingQuantity == 0)
            {
                quantitiesByProductId.Remove(productId);
            }
            else
            {
                quantitiesByProductId[productId] = remainingQuantity;
            }

            return true;
        }

        public bool TryRemoveUnits(ProductDefinition productDefinition, int units)
        {
            return IsProductDefinitionValid(productDefinition)
                && TryRemoveUnits(productDefinition.ProductId, units);
        }

        private static bool IsProductIdValid(string productId)
        {
            return !string.IsNullOrWhiteSpace(productId);
        }

        private static bool IsProductDefinitionValid(ProductDefinition productDefinition)
        {
            return productDefinition != null && productDefinition.IsValid(out _);
        }
    }
}
