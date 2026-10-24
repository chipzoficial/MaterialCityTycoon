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
        private const string ShelfDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_PrototypeShelf.asset";
        private const string ToolDisplayDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_PrototypeToolDisplay.asset";
        private const string PartsBinRackDefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_PrototypePartsBinRack.asset";

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
}
