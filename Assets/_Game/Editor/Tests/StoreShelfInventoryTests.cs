using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using NUnit.Framework;
using UnityEditor;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class StoreShelfInventoryTests
    {
        private ProductDefinition product;
        private StorageCapacityDefinition capacity;
        private StoreInventory reserve;
        private StoreStorageLocations locations;
        private StoreShelfInventory inventory;

        [SetUp]
        public void SetUp()
        {
            product = AssetDatabase.LoadAssetAtPath<ProductDefinition>("Assets/_Game/ScriptableObjects/Products/DEF_Argamassa.asset");
            capacity = AssetDatabase.LoadAssetAtPath<StorageCapacityDefinition>("Assets/_Game/ScriptableObjects/Furniture/DEF_IndustrialStorageShelfCapacity.asset");
            Assert.That(product, Is.Not.Null);
            Assert.That(capacity, Is.Not.Null);
            reserve = new StoreInventory();
            locations = new StoreStorageLocations();
            Assert.That(locations.TryRegister(new StorageLocation("a", "A", StorageLocationType.Indoor)), Is.True);
            Assert.That(locations.TryRegister(new StorageLocation("b", "B", StorageLocationType.Indoor)), Is.True);
            inventory = new StoreShelfInventory(reserve, locations, _ => capacity);
        }

        [Test]
        public void TransfersConserveTotalAndLevelsAndShelvesAreIndependent()
        {
            Assert.That(reserve.TryAddUnits(product, 100), Is.True);
            Assert.That(inventory.TryTransferToStorage("a", "storage_bottom", product, 24), Is.True);
            Assert.That(reserve.GetQuantity(product), Is.EqualTo(76));
            Assert.That(inventory.TryTransferToStorage("a", "storage_middle", product, 24), Is.True);
            Assert.That(inventory.TryTransferToStorage("a", "storage_top", product, 18), Is.True);
            Assert.That(inventory.TryTransferToStorage("b", "storage_bottom", product, 3), Is.True);
            Assert.That(inventory.GetQuantity("a", "storage_bottom", product), Is.EqualTo(24));
            Assert.That(inventory.GetQuantity("a", "storage_middle", product), Is.EqualTo(24));
            Assert.That(inventory.GetQuantity("a", "storage_top", product), Is.EqualTo(18));
            Assert.That(inventory.GetQuantity("b", "storage_bottom", product), Is.EqualTo(3));
            Assert.That(inventory.TryReturnToReserve("a", "storage_bottom", product, 10), Is.True);
            Assert.That(inventory.GetQuantity("a", "storage_bottom", product), Is.EqualTo(14));
            Assert.That(reserve.GetQuantity(product) + inventory.GetTotalStored(product), Is.EqualTo(100));
        }

        [TestCase("storage_bottom", 24)]
        [TestCase("storage_middle", 24)]
        [TestCase("storage_top", 18)]
        public void CapacityAndInvalidOperationsNeverChangeBalances(string level, int max)
        {
            Assert.That(reserve.TryAddUnits(product, 100), Is.True);
            Assert.That(inventory.TryTransferToStorage("a", level, product, max + 1), Is.False);
            Assert.That(inventory.TryTransferToStorage("a", level, product, max), Is.True);
            Assert.That(inventory.TryTransferToStorage("a", level, product, 1), Is.False);
            Assert.That(inventory.TryTransferToStorage("missing", level, product, 1), Is.False);
            Assert.That(inventory.TryTransferToStorage(null, level, product, 1), Is.False);
            Assert.That(inventory.TryTransferToStorage("a", "missing", product, 1), Is.False);
            Assert.That(inventory.TryTransferToStorage("a", null, product, 1), Is.False);
            Assert.That(inventory.TryTransferToStorage("a", level, null, 1), Is.False);
            Assert.That(inventory.TryTransferToStorage("b", level, product, 0), Is.False);
            Assert.That(inventory.TryTransferToStorage("b", level, product, -1), Is.False);
            Assert.That(inventory.TryTransferToStorage("b", level, product, 101), Is.False);
            Assert.That(inventory.TryReturnToReserve("a", level, product, max + 1), Is.False);
            Assert.That(inventory.TryReturnToReserve("a", level, product, 0), Is.False);
            Assert.That(inventory.TryReturnToReserve("a", level, product, -1), Is.False);
            Assert.That(inventory.GetQuantity("a", level, product), Is.EqualTo(max));
            Assert.That(reserve.GetQuantity(product), Is.EqualTo(100 - max));
        }

        [Test]
        public void InsufficientReserveDoesNotAllocateOrChangeBalance()
        {
            Assert.That(reserve.TryAddUnits(product, 2), Is.True);
            Assert.That(inventory.TryTransferToStorage("a", "storage_bottom", product, 3), Is.False);
            Assert.That(reserve.GetQuantity(product), Is.EqualTo(2));
            Assert.That(inventory.HasStoredUnits("a"), Is.False);
        }

        [Test]
        public void ReturnRejectsOverflowAndClearsEmptyAllocation()
        {
            Assert.That(reserve.TryAddUnits(product, 1), Is.True);
            Assert.That(inventory.TryTransferToStorage("a", "storage_bottom", product, 1), Is.True);
            Assert.That(reserve.TryAddUnits(product, int.MaxValue), Is.True);
            Assert.That(inventory.TryReturnToReserve("a", "storage_bottom", product, 1), Is.False);
            Assert.That(inventory.HasStoredUnits("a"), Is.True);
            Assert.That(reserve.TryRemoveUnits(product, 1), Is.True);
            Assert.That(inventory.TryReturnToReserve("a", "storage_bottom", product, 1), Is.True);
            Assert.That(inventory.HasStoredUnits("a"), Is.False);
        }

        [Test]
        public void StoresAreIsolatedAndIncompatibleProductsAreRejected()
        {
            ProductDefinition other = AssetDatabase.LoadAssetAtPath<ProductDefinition>("Assets/_Game/ScriptableObjects/Products/DEF_Martelo.asset");
            Assert.That(other, Is.Not.Null);
            StoreInventory secondReserve = new();
            StoreStorageLocations secondLocations = new();
            secondLocations.TryRegister(new StorageLocation("a", "A", StorageLocationType.Indoor));
            StoreShelfInventory second = new(secondReserve, secondLocations, _ => capacity);
            reserve.TryAddUnits(product, 5);
            reserve.TryAddUnits(other, 1);
            Assert.That(inventory.TryTransferToStorage("a", "storage_bottom", other, 1), Is.False);
            Assert.That(inventory.TryTransferToStorage("a", "storage_bottom", product, 5), Is.True);
            Assert.That(second.GetTotalStored(product), Is.Zero);
            Assert.That(secondReserve.GetQuantity(product), Is.Zero);
            Assert.That(reserve.GetQuantity(other), Is.EqualTo(1));
        }
    }
}
