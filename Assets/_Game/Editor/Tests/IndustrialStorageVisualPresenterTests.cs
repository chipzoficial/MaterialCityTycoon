using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class IndustrialStorageVisualPresenterTests
    {
        private GameObject first;
        private GameObject second;
        private StoreInventory reserve;
        private StoreShelfInventory inventory;
        private StorageCapacityDefinition capacity;
        private ProductDefinition product;

        [SetUp]
        public void SetUp()
        {
            capacity = AssetDatabase.LoadAssetAtPath<StorageCapacityDefinition>("Assets/_Game/ScriptableObjects/Furniture/DEF_EstanteDeposito_StorageCapacity.asset");
            Assert.That(capacity, Is.Not.Null);
            product = capacity.Product;
            Assert.That(product.DisplayPrefab, Is.Not.Null);
            var locations = new StoreStorageLocations();
            locations.TryRegister(new StorageLocation("a", "A", StorageLocationType.Indoor));
            locations.TryRegister(new StorageLocation("b", "B", StorageLocationType.Indoor));
            reserve = new StoreInventory();
            inventory = new StoreShelfInventory(reserve, locations, _ => capacity);
            first = new GameObject("shelf A");
            second = new GameObject("shelf B");
            first.AddComponent<IndustrialStorageVisualPresenter>().Initialize(inventory, "a", capacity);
            second.AddComponent<IndustrialStorageVisualPresenter>().Initialize(inventory, "b", capacity);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        private static int Count(GameObject root, string level)
        {
            Transform container = root.transform.Find("Storage_" + level);
            return container == null ? 0 : container.childCount;
        }

        [Test]
        public void VisualCountsTrackAllLevelsAndIndependentShelves()
        {
            Assert.That(Count(first, "storage_bottom"), Is.Zero);
            reserve.TryAddUnits(product, 100);
            Assert.That(inventory.TryTransferToStorage("a", "storage_bottom", product, 24), Is.True);
            Assert.That(inventory.TryTransferToStorage("a", "storage_middle", product, 24), Is.True);
            Assert.That(inventory.TryTransferToStorage("a", "storage_top", product, 18), Is.True);
            Assert.That(inventory.TryTransferToStorage("b", "storage_bottom", product, 3), Is.True);
            Assert.That(Count(first, "storage_bottom"), Is.EqualTo(24));
            Assert.That(Count(first, "storage_middle"), Is.EqualTo(24));
            Assert.That(Count(first, "storage_top"), Is.EqualTo(18));
            Assert.That(Count(second, "storage_bottom"), Is.EqualTo(3));
            Assert.That(inventory.TryTransferToStorage("a", "storage_top", product, 1), Is.False);
            Assert.That(inventory.TryReturnToReserve("a", "storage_bottom", product, 20), Is.True);
            Assert.That(Count(first, "storage_bottom"), Is.EqualTo(4));
            Assert.That(Count(second, "storage_bottom"), Is.EqualTo(3));
            Assert.That(reserve.GetQuantity(product), Is.EqualTo(51));
            Assert.That(inventory.GetTotalStored(product), Is.EqualTo(49));
        }

        [Test]
        public void InitializationReflectsExistingAllocationAndUsesThreeByTwoLayers()
        {
            reserve.TryAddUnits(product, 6);
            Assert.That(inventory.TryTransferToStorage("a", "storage_bottom", product, 6), Is.True);
            GameObject lateShelf = new("late shelf");
            try
            {
                lateShelf.AddComponent<IndustrialStorageVisualPresenter>().Initialize(inventory, "a", capacity);
                Transform container = lateShelf.transform.Find("Storage_storage_bottom");
                Assert.That(container.childCount, Is.EqualTo(6));
                Vector3 start = container.GetChild(0).localPosition;
                Assert.That(container.GetChild(2).localPosition.x - start.x,
                    Is.EqualTo(2 * (capacity.OrientedProductSize.x + capacity.Spacing)).Within(0.0001f));
                Assert.That(container.GetChild(3).localPosition.z - start.z,
                    Is.EqualTo(capacity.OrientedProductSize.z + capacity.Spacing).Within(0.0001f));
                Assert.That(container.GetChild(5).localPosition.y, Is.EqualTo(start.y).Within(0.0001f));
                Assert.That(reserve.GetQuantity(product), Is.Zero);
                Assert.That(inventory.GetTotalStored(product), Is.EqualTo(6));
            }
            finally { Object.DestroyImmediate(lateShelf); }
        }

        [Test]
        public void ArrangementAndRotationFollowShelfAndDestroyWithIt()
        {
            reserve.TryAddUnits(product, 7);
            Assert.That(inventory.TryTransferToStorage("a", "storage_bottom", product, 7), Is.True);
            Transform container = first.transform.Find("Storage_storage_bottom");
            Transform bag = container.GetChild(0);
            Vector3 local = bag.localPosition;
            Vector3 seventh = container.GetChild(6).localPosition;
            Assert.That(seventh.y - local.y, Is.EqualTo(capacity.OrientedProductSize.y + capacity.Spacing).Within(0.0001f));
            Assert.That(Quaternion.Angle(bag.localRotation, Quaternion.Euler(0, 90, 0) * product.DisplayPrefab.transform.localRotation), Is.LessThan(0.01f));
            foreach (Collider collider in first.GetComponentsInChildren<Collider>(true)) Assert.That(collider.enabled, Is.False);
            first.transform.SetPositionAndRotation(new Vector3(3, 0, 4), Quaternion.Euler(0, 90, 0));
            Assert.That(bag.localPosition, Is.EqualTo(local));
            Assert.That(Vector3.Distance(bag.position, first.transform.TransformPoint(local)), Is.LessThan(0.0001f));
            Assert.That(inventory.GetQuantity("a", "storage_bottom", product), Is.EqualTo(7));
            Assert.That(reserve.GetQuantity(product), Is.Zero);
            Assert.That(inventory.TryReturnToReserve("a", "storage_bottom", product, 7), Is.True);
            Assert.That(Count(first, "storage_bottom"), Is.Zero);
            Object.DestroyImmediate(first);
            Assert.That(bag == null, Is.True);
            Assert.That(inventory.GetTotalStored(product), Is.Zero);
        }
    }
}
