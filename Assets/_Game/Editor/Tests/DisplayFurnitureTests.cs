using System.Collections.Generic;
using System.Linq;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class DisplayFurnitureTests
    {
        private const string FurnitureFolder = "Assets/_Game/ScriptableObjects/Furniture";

        [Test]
        public void NewDisplayFurnitureDefinitionsHaveUniqueIdsValidPrefabsAndExpectedFootprints()
        {
            FurnitureDefinition pallet = Load("DEF_PalletBagDisplay");
            FurnitureDefinition gondola = Load("DEF_DoubleSidedGondola");

            Assert.That(pallet.IsValid(out string palletMessage), Is.True, palletMessage);
            Assert.That(gondola.IsValid(out string gondolaMessage), Is.True, gondolaMessage);
            Assert.That(pallet.FurnitureTypeId, Is.EqualTo(FurnitureTypeIds.PalletBagDisplay));
            Assert.That(gondola.FurnitureTypeId, Is.EqualTo(FurnitureTypeIds.DoubleSidedGondola));
            Assert.That(pallet.FurnitureTypeId, Is.Not.EqualTo(gondola.FurnitureTypeId));
            Assert.That(pallet.Width, Is.EqualTo(1));
            Assert.That(pallet.Depth, Is.EqualTo(1));
            Assert.That(gondola.Width, Is.EqualTo(2));
            Assert.That(gondola.Depth, Is.EqualTo(1));
            Assert.That(pallet.Prefab.GetComponentsInChildren<MeshRenderer>(true), Is.Not.Empty);
            Assert.That(gondola.Prefab.GetComponentsInChildren<MeshRenderer>(true), Is.Not.Empty);
            Assert.That(pallet.Prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(gondola.Prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void PalletAndGondolaSlotsUseIndependentConfiguredStackCapacities()
        {
            FurnitureDefinition pallet = Load("DEF_PalletBagDisplay");
            FurnitureDefinition gondola = Load("DEF_DoubleSidedGondola");

            Assert.That(pallet.DisplaySlots.Count, Is.EqualTo(1));
            Assert.That(pallet.DisplaySlots[0].SlotId, Is.EqualTo("pallet_bags"));
            Assert.That(pallet.DisplaySlots[0].CapacityUnits, Is.EqualTo(40));
            Assert.That(pallet.DisplaySlots[0].AllowedDisplayModes.Single(), Is.EqualTo(ProductDisplayMode.Stack));
            PalletBagDisplayLayoutDefinition palletLayout = pallet.DisplaySlots[0].PalletBagDisplayLayout;
            Assert.That(palletLayout, Is.Not.Null);
            Assert.That(palletLayout.IsConfigured, Is.True);
            Assert.That(palletLayout.SurfaceSize, Is.EqualTo(new Vector2(1.20f, 1.00f)));
            Assert.That(palletLayout.SurfaceHeight, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(palletLayout.Spacing, Is.Zero);
            Assert.That(gondola.DisplaySlots.Count, Is.EqualTo(4));
            Assert.That(gondola.DisplaySlots.Select(slot => slot.SlotId), Is.EquivalentTo(new[]
            {
                "gondola_front_lower", "gondola_front_upper", "gondola_back_lower", "gondola_back_upper"
            }));
            Assert.That(gondola.DisplaySlots.All(slot => slot.CapacityUnits == 4 && slot.AllowedDisplayModes.Single() == ProductDisplayMode.Stack), Is.True);
        }

        [Test]
        public void StackProductsAreCompatibleAndNonStackProductsAreRejected()
        {
            FurnitureDefinition pallet = Load("DEF_PalletBagDisplay");
            FurnitureDefinition gondola = Load("DEF_DoubleSidedGondola");
            ProductDefinition mortar = Product("DEF_Argamassa");
            ProductDefinition cement = Product("DEF_Cimento50kg");
            ProductDefinition paint = Product("DEF_TintaAcrilica18L");
            ProductDefinition tapeMeasure = Product("DEF_Trena5m");
            ProductDefinition wallPlugs = Product("DEF_BuchasPlasticasN8");

            Assert.That(DisplaySlotCompatibility.IsCompatible(mortar, pallet.DisplaySlots[0]), Is.True);
            Assert.That(DisplaySlotCompatibility.IsCompatible(cement, pallet.DisplaySlots[0]), Is.True);
            Assert.That(DisplaySlotCompatibility.IsCompatible(paint, gondola.DisplaySlots[0]), Is.True);
            Assert.That(DisplaySlotCompatibility.IsCompatible(tapeMeasure, gondola.DisplaySlots[0]), Is.False);
            Assert.That(DisplaySlotCompatibility.IsCompatible(wallPlugs, pallet.DisplaySlots[0]), Is.False);
        }

        [Test]
        public void DisplaySlotInventoryHonoursEachNewFurnitureCapacityWithoutPartialTransfers()
        {
            FurnitureDefinition pallet = Load("DEF_PalletBagDisplay");
            FurnitureDefinition gondola = Load("DEF_DoubleSidedGondola");
            ProductDefinition cement = Product("DEF_Cimento50kg");
            ProductDefinition paint = Product("DEF_TintaAcrilica18L");
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory display = new();

            Assert.That(reserve.TryAddUnits(cement, 41), Is.True);
            Assert.That(reserve.TryAddUnits(paint, 10), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "pallet-a", "pallet_bags", cement.ProductId, 40, pallet.DisplaySlots[0].CapacityUnits), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "pallet-a", "pallet_bags", cement.ProductId, 1, pallet.DisplaySlots[0].CapacityUnits), Is.False);
            Assert.That(display.TryTransferFromReserve(reserve, "gondola-a", "gondola_front_lower", paint.ProductId, 4, gondola.DisplaySlots[0].CapacityUnits), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "gondola-a", "gondola_front_lower", paint.ProductId, 1, gondola.DisplaySlots[0].CapacityUnits), Is.False);
            Assert.That(reserve.GetQuantity(cement), Is.EqualTo(1));
            Assert.That(reserve.GetQuantity(paint), Is.EqualTo(6));
        }

        [Test]
        public void BothFurnitureTypesKeepAlternativeAccessFacesAcrossAllRotations()
        {
            foreach (FurnitureDefinition definition in new[] { Load("DEF_PalletBagDisplay"), Load("DEF_DoubleSidedGondola") })
            {
                List<IReadOnlyList<Vector2Int>> groups = definition.GetAccessCellGroups().ToList();
                Assert.That(groups.Count, Is.EqualTo(2));
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    HashSet<Vector2Int> first = FurnitureAccessCells.GetCells(Vector2Int.zero, definition.Width, definition.Depth, rotation, groups[0]).ToHashSet();
                    HashSet<Vector2Int> second = FurnitureAccessCells.GetCells(Vector2Int.zero, definition.Width, definition.Depth, rotation, groups[1]).ToHashSet();
                    int expectedCellsPerFace = definition.FurnitureTypeId == FurnitureTypeIds.PalletBagDisplay ? 1 : 2;
                    Assert.That(first.Count, Is.EqualTo(expectedCellsPerFace));
                    Assert.That(second.Count, Is.EqualTo(expectedCellsPerFace));
                    Assert.That(first.Overlaps(second), Is.False);
                }
            }
        }

        private static FurnitureDefinition Load(string name)
        {
            FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>($"{FurnitureFolder}/{name}.asset");
            Assert.That(definition, Is.Not.Null, name);
            return definition;
        }

        private static ProductDefinition Product(string name)
        {
            ProductDefinition product = AssetDatabase.LoadAssetAtPath<ProductDefinition>($"Assets/_Game/ScriptableObjects/Products/{name}.asset");
            Assert.That(product, Is.Not.Null, name);
            return product;
        }
    }
}
