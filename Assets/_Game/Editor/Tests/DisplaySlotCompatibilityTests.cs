using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class DisplaySlotCompatibilityTests
    {
        private const string MortarDefinitionPath = "Assets/_Game/ScriptableObjects/Products/DEF_Argamassa.asset";
        private const string HammerDefinitionPath = "Assets/_Game/ScriptableObjects/Products/DEF_Martelo.asset";
        private const string ScrewsDefinitionPath = "Assets/_Game/ScriptableObjects/Products/DEF_Parafusos.asset";
        private const string ShelfDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_CommercialShelf.asset";
        private const string ToolDisplayDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_ToolDisplay.asset";
        private const string PartsBinRackDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_PartsBinRack.asset";

        private readonly List<ScriptableObject> temporaryDefinitions = new();

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject definition in temporaryDefinitions)
            {
                Object.DestroyImmediate(definition);
            }

            temporaryDefinitions.Clear();
        }

        [Test]
        public void InitialDefinitionsMatchTheirExpectedSlotModes()
        {
            ProductDefinition mortar = LoadRequiredAsset<ProductDefinition>(MortarDefinitionPath);
            ProductDefinition hammer = LoadRequiredAsset<ProductDefinition>(HammerDefinitionPath);
            ProductDefinition screws = LoadRequiredAsset<ProductDefinition>(ScrewsDefinitionPath);
            DisplaySlotDefinition stackSlot = FindRequiredSlot(
                LoadRequiredAsset<FurnitureDefinition>(ShelfDefinitionPath),
                "shelf_bottom");
            DisplaySlotDefinition hangSlot = FindRequiredSlot(
                LoadRequiredAsset<FurnitureDefinition>(ToolDisplayDefinitionPath),
                "tool_panel_main");
            DisplaySlotDefinition binSlot = FindRequiredSlot(
                LoadRequiredAsset<FurnitureDefinition>(PartsBinRackDefinitionPath),
                "bin_r01_c01");

            Assert.That(DisplaySlotCompatibility.IsCompatible(mortar, stackSlot), Is.True);
            Assert.That(DisplaySlotCompatibility.IsCompatible(mortar, hangSlot), Is.False);
            Assert.That(DisplaySlotCompatibility.IsCompatible(mortar, binSlot), Is.False);

            Assert.That(DisplaySlotCompatibility.IsCompatible(hammer, stackSlot), Is.False);
            Assert.That(DisplaySlotCompatibility.IsCompatible(hammer, hangSlot), Is.True);
            Assert.That(DisplaySlotCompatibility.IsCompatible(hammer, binSlot), Is.False);

            Assert.That(DisplaySlotCompatibility.IsCompatible(screws, stackSlot), Is.False);
            Assert.That(DisplaySlotCompatibility.IsCompatible(screws, hangSlot), Is.False);
            Assert.That(DisplaySlotCompatibility.IsCompatible(screws, binSlot), Is.True);
        }

        [Test]
        public void ProductWithMultipleModesIsCompatibleWhenAnyModeIntersects()
        {
            ProductDefinition product = CreateTemporaryProduct(ProductDisplayMode.Stack, ProductDisplayMode.Hang);
            DisplaySlotDefinition hangSlot = CreateTemporarySlot(ProductDisplayMode.Hang);
            DisplaySlotDefinition binSlot = CreateTemporarySlot(ProductDisplayMode.Bin);

            Assert.That(DisplaySlotCompatibility.IsCompatible(product, hangSlot), Is.True);
            Assert.That(DisplaySlotCompatibility.IsCompatible(product, binSlot), Is.False);
        }

        [Test]
        public void NullOrInvalidDefinitionsAreIncompatible()
        {
            ProductDefinition validProduct = CreateTemporaryProduct(ProductDisplayMode.Stack);
            DisplaySlotDefinition validSlot = CreateTemporarySlot(ProductDisplayMode.Stack);
            ProductDefinition invalidProduct = ScriptableObject.CreateInstance<ProductDefinition>();
            temporaryDefinitions.Add(invalidProduct);
            DisplaySlotDefinition invalidSlot = new DisplaySlotDefinition();

            Assert.That(DisplaySlotCompatibility.IsCompatible(null, validSlot), Is.False);
            Assert.That(DisplaySlotCompatibility.IsCompatible(validProduct, null), Is.False);
            Assert.That(DisplaySlotCompatibility.IsCompatible(invalidProduct, validSlot), Is.False);
            Assert.That(DisplaySlotCompatibility.IsCompatible(validProduct, invalidSlot), Is.False);
        }

        private TDefinition LoadRequiredAsset<TDefinition>(string assetPath)
            where TDefinition : Object
        {
            TDefinition definition = AssetDatabase.LoadAssetAtPath<TDefinition>(assetPath);
            Assert.That(definition, Is.Not.Null, $"Required asset was not found at '{assetPath}'.");
            return definition;
        }

        private static DisplaySlotDefinition FindRequiredSlot(FurnitureDefinition furnitureDefinition, string slotId)
        {
            foreach (DisplaySlotDefinition slot in furnitureDefinition.DisplaySlots)
            {
                if (slot != null && slot.SlotId == slotId)
                {
                    return slot;
                }
            }

            Assert.Fail($"Display slot '{slotId}' was not found in '{furnitureDefinition.name}'.");
            return null;
        }

        private ProductDefinition CreateTemporaryProduct(params ProductDisplayMode[] displayModes)
        {
            ProductDefinition product = ScriptableObject.CreateInstance<ProductDefinition>();
            temporaryDefinitions.Add(product);

            SerializedObject serializedProduct = new(product);
            serializedProduct.FindProperty("productId").stringValue = "test_product";
            serializedProduct.FindProperty("displayName").stringValue = "Test Product";
            serializedProduct.FindProperty("categoryId").stringValue = "test_category";
            SerializedProperty allowedDisplayModes = serializedProduct.FindProperty("allowedDisplayModes");
            allowedDisplayModes.arraySize = displayModes.Length;

            for (int index = 0; index < displayModes.Length; index++)
            {
                allowedDisplayModes.GetArrayElementAtIndex(index).enumValueIndex = (int)displayModes[index];
            }

            serializedProduct.ApplyModifiedPropertiesWithoutUndo();
            return product;
        }

        private DisplaySlotDefinition CreateTemporarySlot(params ProductDisplayMode[] displayModes)
        {
            FurnitureDefinition holder = ScriptableObject.CreateInstance<FurnitureDefinition>();
            temporaryDefinitions.Add(holder);

            SerializedObject serializedHolder = new(holder);
            SerializedProperty displaySlots = serializedHolder.FindProperty("displaySlots");
            displaySlots.arraySize = 1;
            SerializedProperty slot = displaySlots.GetArrayElementAtIndex(0);
            slot.FindPropertyRelative("slotId").stringValue = "test_slot";
            slot.FindPropertyRelative("displayName").stringValue = "Test Slot";
            SerializedProperty allowedDisplayModes = slot.FindPropertyRelative("allowedDisplayModes");
            allowedDisplayModes.arraySize = displayModes.Length;

            for (int index = 0; index < displayModes.Length; index++)
            {
                allowedDisplayModes.GetArrayElementAtIndex(index).enumValueIndex = (int)displayModes[index];
            }

            serializedHolder.ApplyModifiedPropertiesWithoutUndo();
            return holder.DisplaySlots[0];
        }
    }

    public sealed class FurnitureDisplaySlotAssignmentsTests
    {
        [Test]
        public void AssignmentsAreIndependentForDifferentFurnitureInstances()
        {
            FurnitureDisplaySlotAssignments assignments = new();
            assignments.SetProductId("shelf_a", "shelf_bottom", "argamassa");
            assignments.SetProductId("shelf_b", "shelf_bottom", "argamassa");
            assignments.SetProductId("shelf_b", "shelf_middle", "martelo");

            Assert.That(assignments.TryGetProductId("shelf_a", "shelf_bottom", out string firstProduct), Is.True);
            Assert.That(firstProduct, Is.EqualTo("argamassa"));
            Assert.That(assignments.TryGetProductId("shelf_a", "shelf_middle", out _), Is.False);
            Assert.That(assignments.TryGetProductId("shelf_b", "shelf_middle", out string secondProduct), Is.True);
            Assert.That(secondProduct, Is.EqualTo("martelo"));
        }

        [Test]
        public void RemovingOneSlotAssignmentPreservesOtherAssignments()
        {
            FurnitureDisplaySlotAssignments assignments = new();
            assignments.SetProductId("shelf_a", "shelf_bottom", "argamassa");
            assignments.SetProductId("shelf_a", "shelf_middle", "argamassa");

            assignments.RemoveProductId("shelf_a", "shelf_bottom");

            Assert.That(assignments.TryGetProductId("shelf_a", "shelf_bottom", out _), Is.False);
            Assert.That(assignments.TryGetProductId("shelf_a", "shelf_middle", out string productId), Is.True);
            Assert.That(productId, Is.EqualTo("argamassa"));
        }

        [Test]
        public void RemovingFurnitureClearsOnlyItsAssignments()
        {
            FurnitureDisplaySlotAssignments assignments = new();
            assignments.SetProductId("shelf_a", "shelf_bottom", "argamassa");
            assignments.SetProductId("shelf_b", "shelf_bottom", "argamassa");

            assignments.RemoveFurniture("shelf_a");

            Assert.That(assignments.TryGetProductId("shelf_a", "shelf_bottom", out _), Is.False);
            Assert.That(assignments.TryGetProductId("shelf_b", "shelf_bottom", out string productId), Is.True);
            Assert.That(productId, Is.EqualTo("argamassa"));
        }
    }

    public sealed class StoreInventoryTests
    {
        [Test]
        public void NewInventoryStartsEmptyAndTracksProductsIndependently()
        {
            StoreInventory inventory = new();

            Assert.That(inventory.GetQuantity("argamassa"), Is.Zero);
            Assert.That(inventory.GetQuantity("martelo"), Is.Zero);
            Assert.That(inventory.TryAddUnits("argamassa", 8), Is.True);
            Assert.That(inventory.GetQuantity("argamassa"), Is.EqualTo(8));
            Assert.That(inventory.GetQuantity("martelo"), Is.Zero);
        }

        [Test]
        public void AddAndRemoveUnitsUpdatesTheBalance()
        {
            StoreInventory inventory = new();

            Assert.That(inventory.TryAddUnits("parafusos", 12), Is.True);
            Assert.That(inventory.TryRemoveUnits("parafusos", 5), Is.True);
            Assert.That(inventory.GetQuantity("parafusos"), Is.EqualTo(7));
            Assert.That(inventory.TryRemoveUnits("parafusos", 7), Is.True);
            Assert.That(inventory.GetQuantity("parafusos"), Is.Zero);
        }

        [Test]
        public void InvalidOperationsPreserveExistingBalance()
        {
            StoreInventory inventory = new();
            Assert.That(inventory.TryAddUnits("argamassa", 10), Is.True);

            Assert.That(inventory.TryAddUnits("argamassa", 0), Is.False);
            Assert.That(inventory.TryAddUnits("argamassa", -1), Is.False);
            Assert.That(inventory.TryAddUnits("", 1), Is.False);
            Assert.That(inventory.TryRemoveUnits("argamassa", 11), Is.False);
            Assert.That(inventory.TryRemoveUnits("argamassa", 0), Is.False);
            Assert.That(inventory.TryRemoveUnits("argamassa", -1), Is.False);
            Assert.That(inventory.TryRemoveUnits("", 1), Is.False);
            Assert.That(inventory.GetQuantity("argamassa"), Is.EqualTo(10));
        }

        [Test]
        public void OverflowAndInvalidProductDefinitionsAreRejectedWithoutMutation()
        {
            StoreInventory inventory = new();
            Assert.That(inventory.TryAddUnits("argamassa", int.MaxValue), Is.True);
            Assert.That(inventory.TryAddUnits("argamassa", 1), Is.False);
            Assert.That(inventory.GetQuantity("argamassa"), Is.EqualTo(int.MaxValue));

            ProductDefinition invalidProduct = ScriptableObject.CreateInstance<ProductDefinition>();
            try
            {
                Assert.That(inventory.TryAddUnits(invalidProduct, 1), Is.False);
                Assert.That(inventory.TryRemoveUnits(invalidProduct, 1), Is.False);
                Assert.That(inventory.GetQuantity(invalidProduct), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(invalidProduct);
            }
        }

        [Test]
        public void SeparateInventoriesDoNotShareState()
        {
            StoreInventory firstInventory = new();
            StoreInventory secondInventory = new();

            Assert.That(firstInventory.TryAddUnits("martelo", 3), Is.True);
            Assert.That(firstInventory.GetQuantity("martelo"), Is.EqualTo(3));
            Assert.That(secondInventory.GetQuantity("martelo"), Is.Zero);
        }
    }

    public sealed class FurnitureMoveStateRegressionTests
    {
        private const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        [Test]
        public void EnteringConstructionClearsStaleMoveWithNullId()
        {
            GameObject owner = new("MoveStateTest");
            owner.SetActive(false);
            try
            {
                FurniturePlacementController controller = owner.AddComponent<FurniturePlacementController>();
                var field = typeof(FurniturePlacementController).GetField("movingFurniture", Flags);
                Assert.That(field.IsNotSerialized, Is.True);
                field.SetValue(controller, CreateMoveState(null));

                typeof(FurniturePlacementController).GetMethod("EnterPlacementMode", Flags).Invoke(controller, null);

                Assert.That(controller.IsPlacementModeActive, Is.True);
                Assert.That(field.GetValue(controller), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(null)]
        [TestCase("missing-id")]
        public void InvalidMoveIsCanceledWithoutLookingUpNullKey(string furnitureId)
        {
            GameObject owner = new("MoveStateTest");
            owner.SetActive(false);
            try
            {
                FurniturePlacementController controller = owner.AddComponent<FurniturePlacementController>();
                var field = typeof(FurniturePlacementController).GetField("movingFurniture", Flags);
                field.SetValue(controller, CreateMoveState(furnitureId));
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                    "Furniture move canceled because its registered state or visual is invalid.");

                object result = typeof(FurniturePlacementController).GetMethod("TryConfirmFurnitureMove", Flags)
                    .Invoke(controller, null);

                Assert.That(result, Is.False);
                Assert.That(field.GetValue(controller), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static object CreateMoveState(string furnitureId)
        {
            System.Type type = typeof(FurniturePlacementController).GetNestedType("PlacedFurnitureState", Flags);
            return System.Activator.CreateInstance(type, furnitureId, Vector2Int.zero, "prototype_shelf", 1, 2, 0);
        }
    }

    public sealed class FurnitureDisplaySlotInventoryTests
    {
        [Test]
        public void PlacementControllerUsesConfiguredShelfCapacityForMortar()
        {
            FurnitureDefinition originalFurniture = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(
                "Assets/_Game/ScriptableObjects/Furniture/DEF_CommercialShelf.asset");
            ProductDefinition originalProduct = AssetDatabase.LoadAssetAtPath<ProductDefinition>(
                "Assets/_Game/ScriptableObjects/Products/DEF_Argamassa.asset");
            Assert.That(originalFurniture, Is.Not.Null);
            Assert.That(originalProduct, Is.Not.Null);
            FurnitureDefinition furniture = Object.Instantiate(originalFurniture);
            ProductDefinition product = Object.Instantiate(originalProduct);
            GameObject owner = new("CapacityTestController");
            owner.SetActive(false);
            try
            {
                SerializedObject serializedFurniture = new(furniture);
                SerializedProperty slots = serializedFurniture.FindProperty("displaySlots");
                for (int index = 0; index < slots.arraySize; index++)
                {
                    slots.GetArrayElementAtIndex(index).FindPropertyRelative("capacityUnits").intValue = 12;
                }

                serializedFurniture.ApplyModifiedPropertiesWithoutUndo();
                FurniturePlacementController controller = owner.AddComponent<FurniturePlacementController>();
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                ((List<FurnitureDefinition>)typeof(FurniturePlacementController).GetField("furnitureDefinitions", flags).GetValue(controller)).Add(furniture);
                ((List<ProductDefinition>)typeof(FurniturePlacementController).GetField("productDefinitions", flags).GetValue(controller)).Add(product);
                System.Collections.IDictionary states = (System.Collections.IDictionary)typeof(FurniturePlacementController).GetField("furnitureById", flags).GetValue(controller);
                System.Type stateType = typeof(FurniturePlacementController).GetNestedType("PlacedFurnitureState", System.Reflection.BindingFlags.NonPublic);
                states.Add("shelf_a", System.Activator.CreateInstance(stateType, "shelf_a", Vector2Int.zero, "prototype_shelf", 1, 2, 0));

                Assert.That(controller.StoreInventory.TryAddUnits(product, 14), Is.True);
                Assert.That(controller.TryAssignProductToDisplaySlot("shelf_a", "shelf_top", product), Is.True);
                Assert.That(controller.TryTransferReserveToDisplay("shelf_a", "shelf_top", 12), Is.True);
                Assert.That(controller.TryTransferReserveToDisplay("shelf_a", "shelf_top", 1), Is.False);
                Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(2));
                Assert.That(controller.GetDisplayedQuantity("shelf_a", "shelf_top"), Is.EqualTo(12));
                Assert.That(controller.TryTransferDisplayToReserve("shelf_a", "shelf_top", 2), Is.True);
                Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(4));
                Assert.That(controller.GetDisplayedQuantity("shelf_a", "shelf_top"), Is.EqualTo(10));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(furniture);
                Object.DestroyImmediate(product);
            }
        }

        [Test]
        public void ConfiguredCapacityRejectsWholeTransferAndPreservesTotal()
        {
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory display = new();
            Assert.That(reserve.TryAddUnits("argamassa", 20), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "a", "top", "argamassa", 10, 12), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "a", "top", "argamassa", 3, 12), Is.False);
            Assert.That(reserve.GetQuantity("argamassa"), Is.EqualTo(10));
            Assert.That(display.TryGetQuantity("a", "top", out _, out int exposed), Is.True);
            Assert.That(exposed, Is.EqualTo(10));
            Assert.That(display.TryTransferFromReserve(reserve, "a", "top", "argamassa", 2, 12), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "a", "top", "argamassa", 1, 12), Is.False);
            Assert.That(display.TryTransferToReserve(reserve, "a", "top", "argamassa", 4), Is.True);
            Assert.That(display.TryGetQuantity("a", "top", out _, out exposed), Is.True);
            Assert.That(exposed, Is.EqualTo(8));
            Assert.That(reserve.GetQuantity("argamassa") + exposed, Is.EqualTo(20));
        }

        [Test]
        public void CapacityIsPerSlotAndFurnitureAndRemovalReturnsAllUnits()
        {
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory display = new();
            Assert.That(reserve.TryAddUnits("argamassa", 30), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "a", "top", "argamassa", 12, 12), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "a", "bottom", "argamassa", 4, 12), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "b", "top", "argamassa", 3, 12), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "a", "top", "argamassa", 1, 12), Is.False);
            Assert.That(display.TryReturnAllToReserve(reserve, "a"), Is.True);
            Assert.That(display.TryGetQuantity("a", "top", out _, out _), Is.False);
            Assert.That(display.TryGetQuantity("a", "bottom", out _, out _), Is.False);
            Assert.That(display.TryGetQuantity("b", "top", out _, out int other), Is.True);
            Assert.That(other, Is.EqualTo(3));
            Assert.That(reserve.GetQuantity("argamassa") + other, Is.EqualTo(30));
        }

        [Test]
        public void UnconfiguredCapacityAndLegacyOverCapacityRemainSafe()
        {
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory display = new();
            Assert.That(reserve.TryAddUnits("argamassa", 20), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "a", "top", "argamassa", 13), Is.True);
            Assert.That(display.TryTransferFromReserve(reserve, "a", "top", "argamassa", 1, 12), Is.False);
            Assert.That(display.TryTransferFromReserve(reserve, "b", "top", "argamassa", 1, -1), Is.False);
            Assert.That(display.TryTransferToReserve(reserve, "a", "top", "argamassa", 2), Is.True);
            Assert.That(display.TryGetQuantity("a", "top", out _, out int exposed), Is.True);
            Assert.That(exposed, Is.EqualTo(11));
            Assert.That(reserve.GetQuantity("argamassa") + exposed, Is.EqualTo(20));
        }

        [Test]
        public void TransfersUnitsBetweenReserveAndDisplayWithoutChangingTheTotal()
        {
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory displayInventory = new();
            Assert.That(reserve.TryAddUnits("argamassa", 12), Is.True);

            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "shelf_a", "shelf_bottom", "argamassa", 5),
                Is.True);
            Assert.That(reserve.GetQuantity("argamassa"), Is.EqualTo(7));
            Assert.That(displayInventory.TryGetQuantity("shelf_a", "shelf_bottom", out string productId, out int quantity), Is.True);
            Assert.That(productId, Is.EqualTo("argamassa"));
            Assert.That(quantity, Is.EqualTo(5));

            Assert.That(
                displayInventory.TryTransferToReserve(reserve, "shelf_a", "shelf_bottom", "argamassa", 5),
                Is.True);
            Assert.That(reserve.GetQuantity("argamassa"), Is.EqualTo(12));
            Assert.That(displayInventory.TryGetQuantity("shelf_a", "shelf_bottom", out _, out _), Is.False);
        }

        [Test]
        public void InsufficientAndInvalidTransfersDoNotPartiallyChangeBalances()
        {
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory displayInventory = new();
            Assert.That(reserve.TryAddUnits("martelo", 3), Is.True);

            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "display_a", "panel", "martelo", 4),
                Is.False);
            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "display_a", "panel", "martelo", 0),
                Is.False);
            Assert.That(reserve.GetQuantity("martelo"), Is.EqualTo(3));
            Assert.That(displayInventory.TryGetQuantity("display_a", "panel", out _, out _), Is.False);

            Assert.That(
                displayInventory.TryTransferToReserve(reserve, "display_a", "panel", "martelo", 1),
                Is.False);
            Assert.That(reserve.GetQuantity("martelo"), Is.EqualTo(3));
        }

        [Test]
        public void SlotsAndFurnitureInstancesKeepIndependentDisplayedBalances()
        {
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory displayInventory = new();
            Assert.That(reserve.TryAddUnits("parafusos", 10), Is.True);

            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "rack_a", "bin_r01_c01", "parafusos", 2),
                Is.True);
            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "rack_a", "bin_r01_c02", "parafusos", 3),
                Is.True);
            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "rack_b", "bin_r01_c01", "parafusos", 1),
                Is.True);

            Assert.That(displayInventory.TryGetQuantity("rack_a", "bin_r01_c01", out _, out int firstQuantity), Is.True);
            Assert.That(firstQuantity, Is.EqualTo(2));
            Assert.That(displayInventory.TryGetQuantity("rack_a", "bin_r01_c02", out _, out int secondQuantity), Is.True);
            Assert.That(secondQuantity, Is.EqualTo(3));
            Assert.That(displayInventory.TryGetQuantity("rack_b", "bin_r01_c01", out _, out int thirdQuantity), Is.True);
            Assert.That(thirdQuantity, Is.EqualTo(1));
            Assert.That(reserve.GetQuantity("parafusos"), Is.EqualTo(4));
        }

        [Test]
        public void ADisplayedSlotCannotSilentlyConvertToAnotherProduct()
        {
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory displayInventory = new();
            Assert.That(reserve.TryAddUnits("argamassa", 4), Is.True);
            Assert.That(reserve.TryAddUnits("martelo", 4), Is.True);
            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "shelf_a", "shelf_bottom", "argamassa", 4),
                Is.True);

            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "shelf_a", "shelf_bottom", "martelo", 1),
                Is.False);
            Assert.That(reserve.GetQuantity("argamassa"), Is.Zero);
            Assert.That(reserve.GetQuantity("martelo"), Is.EqualTo(4));
            Assert.That(displayInventory.TryGetQuantity("shelf_a", "shelf_bottom", out string productId, out int quantity), Is.True);
            Assert.That(productId, Is.EqualTo("argamassa"));
            Assert.That(quantity, Is.EqualTo(4));
        }

        [Test]
        public void DisplayOverflowIsRejectedWithoutRemovingFromReserve()
        {
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory displayInventory = new();
            Assert.That(reserve.TryAddUnits("argamassa", int.MaxValue), Is.True);
            Assert.That(
                displayInventory.TryTransferFromReserve(
                    reserve,
                    "shelf_a",
                    "shelf_bottom",
                    "argamassa",
                    int.MaxValue),
                Is.True);
            Assert.That(reserve.TryAddUnits("argamassa", 1), Is.True);

            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "shelf_a", "shelf_bottom", "argamassa", 1),
                Is.False);
            Assert.That(reserve.GetQuantity("argamassa"), Is.EqualTo(1));
            Assert.That(displayInventory.TryGetQuantity("shelf_a", "shelf_bottom", out _, out int quantity), Is.True);
            Assert.That(quantity, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ReturningFurnitureDisplayInventoryRestoresReserveWithoutAffectingOtherFurniture()
        {
            StoreInventory reserve = new();
            FurnitureDisplaySlotInventory displayInventory = new();
            Assert.That(reserve.TryAddUnits("parafusos", 9), Is.True);
            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "rack_a", "bin_r01_c01", "parafusos", 2),
                Is.True);
            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "rack_a", "bin_r01_c02", "parafusos", 3),
                Is.True);
            Assert.That(
                displayInventory.TryTransferFromReserve(reserve, "rack_b", "bin_r01_c01", "parafusos", 1),
                Is.True);

            Assert.That(displayInventory.TryReturnAllToReserve(reserve, "rack_a"), Is.True);
            Assert.That(reserve.GetQuantity("parafusos"), Is.EqualTo(8));
            Assert.That(displayInventory.TryGetQuantity("rack_a", "bin_r01_c01", out _, out _), Is.False);
            Assert.That(displayInventory.TryGetQuantity("rack_a", "bin_r01_c02", out _, out _), Is.False);
            Assert.That(displayInventory.TryGetQuantity("rack_b", "bin_r01_c01", out _, out int remainingQuantity), Is.True);
            Assert.That(remainingQuantity, Is.EqualTo(1));
        }
    }
}
