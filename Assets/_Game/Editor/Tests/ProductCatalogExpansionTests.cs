using System;
using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class ProductCatalogExpansionTests
    {
        private const string ProductFolder = "Assets/_Game/ScriptableObjects/Products";
        private const string CommercialShelfPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_CommercialShelf.asset";
        private const string ToolDisplayPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_ToolDisplay.asset";
        private const string PartsBinRackPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_PartsBinRack.asset";
        private const string IndustrialShelfPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_IndustrialStorageShelf.asset";

        [Test]
        public void ExpandedCatalogHasUniqueValidDefinitionsWithProvisionalVisualsAndPrices()
        {
            ProductDefinition[] expected =
            {
                LoadProduct("DEF_Argamassa"),
                LoadProduct("DEF_Martelo"),
                LoadProduct("DEF_Parafusos"),
                LoadProduct("DEF_Cimento50kg"),
                LoadProduct("DEF_TintaAcrilica18L"),
                LoadProduct("DEF_Trena5m"),
                LoadProduct("DEF_BuchasPlasticasN8")
            };
            HashSet<string> ids = new(StringComparer.Ordinal);

            foreach (ProductDefinition product in expected)
            {
                Assert.That(product, Is.Not.Null);
                Assert.That(product.IsValid(out string message), Is.True, message);
                Assert.That(ids.Add(product.ProductId), Is.True, $"Duplicate product id '{product.ProductId}'.");
            }

            foreach (ProductDefinition product in new[]
                     {
                         LoadProduct("DEF_Cimento50kg"), LoadProduct("DEF_TintaAcrilica18L"),
                         LoadProduct("DEF_Trena5m"), LoadProduct("DEF_BuchasPlasticasN8")
                     })
            {
                Assert.That(product.SalePriceCents, Is.GreaterThan(0));
                Assert.That(product.DisplayPrefab, Is.Not.Null);
                Assert.That(product.DisplayPrefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
        }

        [Test]
        public void NewProductsMatchOnlyTheirConfiguredDisplayModes()
        {
            FurnitureDefinition commercialShelf = LoadFurniture(CommercialShelfPath);
            FurnitureDefinition toolDisplay = LoadFurniture(ToolDisplayPath);
            FurnitureDefinition partsBinRack = LoadFurniture(PartsBinRackPath);
            ProductDefinition cement = LoadProduct("DEF_Cimento50kg");
            ProductDefinition paint = LoadProduct("DEF_TintaAcrilica18L");
            ProductDefinition tapeMeasure = LoadProduct("DEF_Trena5m");
            ProductDefinition wallPlugs = LoadProduct("DEF_BuchasPlasticasN8");

            Assert.That(DisplaySlotCompatibility.IsCompatible(cement, commercialShelf.DisplaySlots[0]), Is.True);
            Assert.That(DisplaySlotCompatibility.IsCompatible(paint, commercialShelf.DisplaySlots[1]), Is.True);
            Assert.That(DisplaySlotCompatibility.IsCompatible(tapeMeasure, toolDisplay.DisplaySlots[0]), Is.True);
            Assert.That(DisplaySlotCompatibility.IsCompatible(wallPlugs, partsBinRack.DisplaySlots[0]), Is.True);
            Assert.That(DisplaySlotCompatibility.IsCompatible(tapeMeasure, commercialShelf.DisplaySlots[0]), Is.False);
            Assert.That(DisplaySlotCompatibility.IsCompatible(wallPlugs, toolDisplay.DisplaySlots[0]), Is.False);
            Assert.That(DisplaySlotCompatibility.IsCompatible(paint, partsBinRack.DisplaySlots[0]), Is.False);
            Assert.That(partsBinRack.DisplaySlots.Count, Is.EqualTo(25));
        }

        [Test]
        public void StackProductUsesExistingShelfCapacityAndInventoryTransferRemainsAtomic()
        {
            ProductDefinition cement = LoadProduct("DEF_Cimento50kg");
            FurnitureDefinition commercialShelf = LoadFurniture(CommercialShelfPath);
            DisplaySlotDefinition slot = commercialShelf.DisplaySlots[0];
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory display = new();

            Assert.That(slot.HasConfiguredCapacity, Is.True);
            Assert.That(reserve.TryAddUnits(cement, slot.CapacityUnits + 1), Is.True);
            Assert.That(display.TryTransferFromReserve(
                reserve, "shelf-a", slot.SlotId, cement.ProductId, slot.CapacityUnits, slot.CapacityUnits), Is.True);
            Assert.That(display.TryTransferFromReserve(
                reserve, "shelf-a", slot.SlotId, cement.ProductId, 1, slot.CapacityUnits), Is.False);
            Assert.That(reserve.GetQuantity(cement), Is.EqualTo(1));
            Assert.That(display.TryGetQuantity("shelf-a", slot.SlotId, out string productId, out int quantity), Is.True);
            Assert.That(productId, Is.EqualTo(cement.ProductId));
            Assert.That(quantity, Is.EqualTo(slot.CapacityUnits));
        }

        [Test]
        public void NewProductCanParticipateInExistingSalesFlow()
        {
            ProductDefinition wallPlugs = LoadProduct("DEF_BuchasPlasticasN8");
            FurnitureDisplaySlotInventory display = new();
            StoreInventory reserve = new();
            Assert.That(reserve.TryAddUnits(wallPlugs, 4), Is.True);
            Assert.That(display.TryTransferFromReserve(
                reserve, "rack-a", "bin_r01_c01", wallPlugs.ProductId, 2), Is.True);

            StoreSalesService sales = CreateSalesService(display, (furnitureId, slotId) =>
                furnitureId == "rack-a" && slotId == "bin_r01_c01" ? wallPlugs : null);
            Assert.That(sales.TrySell("rack-a", "bin_r01_c01", 1, out string reason), Is.True, reason);
            Assert.That(sales.BalanceCents, Is.EqualTo(wallPlugs.SalePriceCents));
            Assert.That(display.TryGetQuantity("rack-a", "bin_r01_c01", out _, out int quantity), Is.True);
            Assert.That(quantity, Is.EqualTo(1));
        }

        [Test]
        public void IndustrialCapacityRemainsRestrictedToItsMeasuredProduct()
        {
            FurnitureDefinition industrialShelf = LoadFurniture(IndustrialShelfPath);
            ProductDefinition cement = LoadProduct("DEF_Cimento50kg");

            Assert.That(industrialShelf.StorageCapacity, Is.Not.Null);
            Assert.That(industrialShelf.StorageCapacity.TryGetTotalCapacity(cement, out _), Is.False);
        }

        private static ProductDefinition LoadProduct(string assetName)
        {
            return AssetDatabase.LoadAssetAtPath<ProductDefinition>($"{ProductFolder}/{assetName}.asset");
        }

        private static FurnitureDefinition LoadFurniture(string path)
        {
            FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path);
            Assert.That(definition, Is.Not.Null, path);
            return definition;
        }

        private static StoreSalesService CreateSalesService(
            FurnitureDisplaySlotInventory display,
            Func<string, string, ProductDefinition> resolver)
        {
            return (StoreSalesService)Activator.CreateInstance(
                typeof(StoreSalesService),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new object[] { display, resolver },
                null);
        }
    }
}
