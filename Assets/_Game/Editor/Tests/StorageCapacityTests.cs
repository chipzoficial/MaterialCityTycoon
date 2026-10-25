using System.Linq;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class StorageCapacityTests
    {
        private StorageCapacityDefinition definition;
        private ProductDefinition product;

        [SetUp]
        public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<StorageCapacityDefinition>();
            product = AssetDatabase.LoadAssetAtPath<ProductDefinition>("Assets/_Game/ScriptableObjects/Products/DEF_Argamassa.asset");
            Assert.That(product, Is.Not.Null);
            SerializedObject data = new(definition);
            data.FindProperty("product").objectReferenceValue = product;
            data.FindProperty("orientedProductSize").vector3Value = new Vector3(0.6f, 0.12f, 0.4f);
            data.FindProperty("productYawDegrees").floatValue = 90f;
            data.FindProperty("spacing").floatValue = 0.01f;
            SerializedProperty levels = data.FindProperty("levels");
            levels.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("levelId").stringValue = $"level_{i}";
                level.FindPropertyRelative("displayName").stringValue = $"Nível {i}";
                level.FindPropertyRelative("localSupportCenter").vector3Value = new Vector3(0, i, 0);
                level.FindPropertyRelative("usableArea").vector2Value = new Vector2(1.86f, 0.86f);
                level.FindPropertyRelative("usableHeight").floatValue = i == 2 ? 0.4388f : 0.5876f;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(definition);

        [Test]
        public void ThreeUniqueIndependentLevelsProvideConsistentCapacity()
        {
            Assert.That(definition.IsValid(), Is.True);
            Assert.That(definition.Levels.Count, Is.EqualTo(3));
            Assert.That(definition.Levels.Select(level => level.LevelId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(definition.TryGetCapacity("level_0", product, out int bottom), Is.True);
            Assert.That(definition.TryGetCapacity("level_1", product, out int middle), Is.True);
            Assert.That(definition.TryGetCapacity("level_2", product, out int top), Is.True);
            Assert.That(new[] { bottom, middle, top }, Is.EqualTo(new[] { 24, 24, 18 }));
            Assert.That(definition.TryGetTotalCapacity(product, out int total), Is.True);
            Assert.That(total, Is.EqualTo(bottom + middle + top));
            Assert.That(definition.TryGetCapacity("missing", product, out _), Is.False);
            Assert.That(definition.TryGetCapacity("level_0", null, out _), Is.False);
        }

        [Test]
        public void InvalidPhysicalDataAndDuplicateIdsAreRejected()
        {
            Assert.That(StoragePacking.TryGetCapacity(new StorageLevelDefinition("a", "A", Vector3.zero, new Vector2(-1, 1), 1), Vector3.one, 0, out _), Is.False);
            Assert.That(StoragePacking.TryGetCapacity(new StorageLevelDefinition("a", "A", Vector3.zero, Vector2.one, 1), new Vector3(float.NaN, 1, 1), 0, out _), Is.False);
            Assert.That(StoragePacking.TryGetCapacity(new StorageLevelDefinition("a", "A", Vector3.zero, Vector2.one, 1), Vector3.one, -1, out _), Is.False);
            Assert.That(StoragePacking.TryGetCapacity(new StorageLevelDefinition("a", "A", Vector3.zero, Vector2.one, 1), Vector3.one * 0.000001f, 0, out _), Is.False);
            SerializedObject data = new(definition);
            data.FindProperty("levels").GetArrayElementAtIndex(1).FindPropertyRelative("levelId").stringValue = "level_0";
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(definition.IsValid(), Is.False);
            Assert.That(definition.TryGetTotalCapacity(product, out _), Is.False);
        }

        [Test]
        public void CapacityDoesNotChangeStoreInventory()
        {
            StoreInventory reserve = new();
            Assert.That(reserve.TryAddUnits(product, 5), Is.True);
            Assert.That(definition.TryGetTotalCapacity(product, out int total), Is.True);
            Assert.That(total, Is.EqualTo(66));
            Assert.That(reserve.GetQuantity(product), Is.EqualTo(5));
            Assert.That(reserve.TryRemoveUnits(product, 5), Is.True);
            Assert.That(definition.TryGetTotalCapacity(product, out int after), Is.True);
            Assert.That(after, Is.EqualTo(total));
        }

        [Test]
        public void ConfiguredIndustrialShelfExposesMeasuredCapacity()
        {
            FurnitureDefinition shelf = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>("Assets/_Game/ScriptableObjects/Furniture/DEF_EstanteDeposito.asset");
            Assert.That(shelf, Is.Not.Null);
            Assert.That(shelf.StorageCapacity, Is.Not.Null);
            Assert.That(shelf.StorageCapacity.IsValid(), Is.True);
            Assert.That(shelf.StorageCapacity.ProductYawDegrees, Is.EqualTo(90f));
            Assert.That(shelf.StorageCapacity.Levels.Select(level => level.LevelId),
                Is.EqualTo(new[] { "storage_bottom", "storage_middle", "storage_top" }));
            Assert.That(shelf.StorageCapacity.TryGetTotalCapacity(product, out int total), Is.True);
            Assert.That(total, Is.EqualTo(66));
        }
    }
}
