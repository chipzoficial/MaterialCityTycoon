using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MaterialCityTycoon.Building;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class FurnitureAccessTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject owner;
        private FurniturePlacementController controller;
        private FurnitureDefinition shelf;
        private FurnitureDefinition tool;
        private FurnitureDefinition rack;
        private FurnitureDefinition storage;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("AccessTest");
            owner.SetActive(false);
            ConstructionGrid grid = owner.AddComponent<ConstructionGrid>();
            controller = owner.AddComponent<FurniturePlacementController>();
            typeof(FurniturePlacementController).GetField("constructionGrid", Flags).SetValue(controller, grid);
            shelf = Definition("DEF_PrototypeShelf", new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(1, 0), new Vector2Int(1, 1));
            tool = Definition("DEF_PrototypeToolDisplay", new Vector2Int(0, -1), new Vector2Int(0, 1));
            rack = Definition("DEF_PrototypePartsBinRack", new Vector2Int(0, 1), new Vector2Int(1, 1));
            storage = Object.Instantiate(rack);
            SerializedObject storageData = new(storage);
            storageData.FindProperty("furnitureTypeId").stringValue = "industrial_storage_shelf";
            storageData.FindProperty("displayName").stringValue = "Estante de Depósito";
            storageData.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/Storage/PF_EstanteDeposito.prefab");
            storageData.FindProperty("displaySlots").arraySize = 0;
            storageData.FindProperty("storageCapacity").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StorageCapacityDefinition>("Assets/_Game/ScriptableObjects/Furniture/DEF_EstanteDeposito_StorageCapacity.asset");
            SerializedProperty storageAccess = storageData.FindProperty("accessCellOffsets");
            Vector2Int[] bilateralAccess = { new(0, 1), new(1, 1), new(0, -1), new(1, -1) };
            storageAccess.arraySize = bilateralAccess.Length;
            for (int i = 0; i < bilateralAccess.Length; i++) storageAccess.GetArrayElementAtIndex(i).vector2IntValue = bilateralAccess[i];
            storageData.ApplyModifiedPropertiesWithoutUndo();
            var definitions = (List<FurnitureDefinition>)typeof(FurniturePlacementController).GetField("furnitureDefinitions", Flags).GetValue(controller);
            definitions.AddRange(new[] { shelf, tool, rack, storage });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(shelf);
            Object.DestroyImmediate(tool);
            Object.DestroyImmediate(rack);
            Object.DestroyImmediate(storage);
        }

        [TestCase(0, 0, 1, 1, 1)]
        [TestCase(1, 1, 0, 1, 1)]
        [TestCase(2, 0, -1, 1, -1)]
        [TestCase(3, -1, 0, -1, 1)]
        public void RackFrontRotatesWithPhysicalFootprint(int rotation, int x1, int z1, int x2, int z2)
        {
            Assert.That(FurnitureAccessCells.GetCells(Vector2Int.zero, 2, 1, rotation, rack.AccessCellOffsets),
                Is.EquivalentTo(new[] { new Vector2Int(x1, z1), new Vector2Int(x2, z2) }));
        }

        [Test]
        public void ThreeTypesHaveExpectedPhysicalAndAccessCells()
        {
            Assert.That(shelf.Width * shelf.Depth, Is.EqualTo(2));
            Assert.That(tool.Width * tool.Depth, Is.EqualTo(1));
            Assert.That(rack.Width * rack.Depth, Is.EqualTo(2));
            Assert.That(FurnitureAccessCells.GetCells(Vector2Int.zero, 1, 2, 0, shelf.AccessCellOffsets),
                Is.EquivalentTo(new[] { new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) }));
            Assert.That(FurnitureAccessCells.GetCells(Vector2Int.zero, 1, 1, 0, tool.AccessCellOffsets),
                Is.EquivalentTo(new[] { new Vector2Int(0, -1), new Vector2Int(0, 1) }));
            Assert.That(FurnitureAccessCells.GetCells(Vector2Int.zero, 2, 1, 0, rack.AccessCellOffsets),
                Is.EquivalentTo(new[] { new Vector2Int(0, 1), new Vector2Int(1, 1) }));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EachTypeCanBePlacedAndItsRotatedAccessBlocksConstruction(int rotation)
        {
            foreach (FurnitureDefinition definition in new[] { shelf, tool, rack })
            {
                Vector2Int anchor = new(8, 8);
                Assert.That(Available(definition, anchor, rotation), Is.True);
                Vector2Int access = FurnitureAccessCells.GetCells(anchor, definition.Width, definition.Depth,
                    rotation, definition.AccessCellOffsets).First();
                object placed = State("placed", definition, anchor, rotation);
                Assert.That(Register(placed), Is.True);
                Assert.That(Available(tool, access), Is.False);
                typeof(FurniturePlacementController).GetMethod("UnregisterFurniture", Flags).Invoke(controller, new[] { placed });
            }
        }

        [Test]
        public void SharedAisleIsAllowedButPhysicalCellsCannotBlockEitherAccess()
        {
            object first = State("first", shelf, new Vector2Int(2, 2));
            Assert.That(Register(first), Is.True);
            Assert.That(Available(shelf, new Vector2Int(4, 2)), Is.True);
            object second = State("second", shelf, new Vector2Int(4, 2));
            Assert.That(Register(second), Is.True);
            object third = State("third", tool, new Vector2Int(3, 1));
            Assert.That(Register(third), Is.True); // Its +Z access shares (3, 2).
            Assert.That(Available(tool, new Vector2Int(3, 2)), Is.False); // X over shared O
            Assert.That(Available(tool, new Vector2Int(2, 1)), Is.False); // O over existing X
            Assert.That(Available(shelf, new Vector2Int(2, 2)), Is.False); // X over X
            Assert.That(Available(shelf, new Vector2Int(0, 4)), Is.False); // O outside grid
            typeof(FurniturePlacementController).GetMethod("UnregisterFurniture", Flags).Invoke(controller, new[] { first });
            typeof(FurniturePlacementController).GetMethod("UnregisterFurniture", Flags).Invoke(controller, new[] { second });
            typeof(FurniturePlacementController).GetMethod("UnregisterFurniture", Flags).Invoke(controller, new[] { third });
            Assert.That(Available(tool, new Vector2Int(3, 2)), Is.True);
        }

        [Test]
        public void MovementIgnoresOwnFootprintAndInvalidCandidatePreservesState()
        {
            object first = State("stable-id", shelf, new Vector2Int(2, 2));
            object second = State("other", shelf, new Vector2Int(6, 2));
            Assert.That(Register(first), Is.True);
            Assert.That(Register(second), Is.True);
            var assignments = (FurnitureDisplaySlotAssignments)typeof(FurniturePlacementController).GetField("displaySlotAssignments", Flags).GetValue(controller);
            var inventory = (FurnitureDisplaySlotInventory)typeof(FurniturePlacementController).GetField("displaySlotInventory", Flags).GetValue(controller);
            assignments.SetProductId("stable-id", "shelf_top", "argamassa");
            Assert.That(controller.StoreInventory.TryAddUnits("argamassa", 3), Is.True);
            Assert.That(inventory.TryTransferFromReserve(controller.StoreInventory, "stable-id", "shelf_top", "argamassa", 3), Is.True);
            Assert.That(Available(shelf, new Vector2Int(2, 3), 0, "stable-id"), Is.True);
            typeof(FurniturePlacementController).GetMethod("ApplyFurniturePlacement", Flags)
                .Invoke(controller, new object[] { first, new Vector2Int(2, 3), 0 });
            Assert.That(Available(shelf, new Vector2Int(5, 2), 0, "stable-id"), Is.False);
            Assert.That(controller.TryGetFurnitureAtCell(new Vector2Int(2, 3), out string id, out _), Is.True);
            Assert.That(id, Is.EqualTo("stable-id"));
            Assert.That(controller.TryGetFurnitureAtCell(new Vector2Int(2, 2), out _, out _), Is.False);
            Assert.That(Available(tool, new Vector2Int(3, 3)), Is.False);
            Assert.That(assignments.TryGetProductId(id, "shelf_top", out string productId), Is.True);
            Assert.That(productId, Is.EqualTo("argamassa"));
            Assert.That(inventory.TryGetQuantity(id, "shelf_top", out _, out int quantity), Is.True);
            Assert.That(quantity, Is.EqualTo(3));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void PreviewCellsUseTheSameRotatedFootprintAndAccessAsValidation(int rotation)
        {
            Vector2Int anchor = new(8, 8);
            List<PlacementPreviewCell> cells = new();
            Assert.That(Evaluate(shelf, anchor, rotation, null, cells), Is.True);
            Assert.That(cells.Count(cell => cell.Kind == PlacementCellKind.ValidFootprint), Is.EqualTo(2));
            Assert.That(cells.Where(cell => cell.Kind == PlacementCellKind.FreeAccess).Select(cell => cell.Cell),
                Is.EquivalentTo(FurnitureAccessCells.GetCells(anchor, shelf.Width, shelf.Depth, rotation, shelf.AccessCellOffsets)));
        }

        [Test]
        public void PreviewDistinguishesSharedBlockedAndAvailableAccess()
        {
            Assert.That(Register(State("first", shelf, new Vector2Int(2, 2))), Is.True);
            List<PlacementPreviewCell> cells = new();
            Assert.That(Evaluate(shelf, new Vector2Int(4, 2), 0, null, cells), Is.True);
            Assert.That(cells.Count(cell => cell.Kind == PlacementCellKind.SharedAccess), Is.EqualTo(2));
            Assert.That(cells.Count(cell => cell.Kind == PlacementCellKind.FreeAccess), Is.EqualTo(2));
            Assert.That(Evaluate(tool, new Vector2Int(2, 1), 0, null, cells), Is.False);
            Assert.That(cells.Any(cell => cell.Cell == new Vector2Int(2, 2) && cell.Kind == PlacementCellKind.BlockedAccess), Is.True);
            Assert.That(cells.Count(cell => cell.Kind == PlacementCellKind.InvalidFootprint), Is.EqualTo(1));
            Assert.That(Evaluate(shelf, new Vector2Int(0, 4), 0, null, cells), Is.False);
            Assert.That(cells.Any(cell => cell.Kind == PlacementCellKind.BlockedAccess), Is.True);
        }

        [Test]
        public void MovingPreviewIgnoresOwnCellsAndClearsWhenHidden()
        {
            Assert.That(Register(State("moving", shelf, new Vector2Int(2, 2))), Is.True);
            Assert.That(Register(State("other", shelf, new Vector2Int(6, 2))), Is.True);
            var cells = (List<PlacementPreviewCell>)typeof(FurniturePlacementController).GetField("previewCells", Flags).GetValue(controller);
            Assert.That(Evaluate(shelf, new Vector2Int(2, 3), 0, "moving", cells), Is.True);
            Assert.That(cells.All(cell => cell.Kind != PlacementCellKind.BlockedAccess), Is.True);
            Assert.That(Evaluate(shelf, new Vector2Int(5, 2), 0, "moving", cells), Is.False);
            Assert.That(cells.Any(cell => cell.Kind == PlacementCellKind.InvalidFootprint), Is.True);
            typeof(FurniturePlacementController).GetMethod("HideCellPreview", Flags).Invoke(controller, null);
            Assert.That(cells, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void IndustrialShelfFitsAndRotatesAccessWithFootprint(int rotation)
        {
            Assert.That(storage.IsValid(out _), Is.True);
            Assert.That(storage.Width, Is.EqualTo(2));
            Assert.That(storage.Depth, Is.EqualTo(1));
            Vector2Int[] expected = rotation % 2 == 0
                ? new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, -1), new Vector2Int(1, -1) }
                : new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(-1, 0), new Vector2Int(-1, 1) };
            Assert.That(FurnitureAccessCells.GetCells(Vector2Int.zero, 2, 1, rotation, storage.AccessCellOffsets), Is.EquivalentTo(expected));
            Vector2Int anchor = new(8, 8);
            Assert.That(Available(storage, anchor, rotation), Is.True);
            object instance = State("storage", storage, anchor, rotation);
            Assert.That(Register(instance), Is.True);
            Vector2Int access = FurnitureAccessCells.GetCells(anchor, 2, 1, rotation, storage.AccessCellOffsets).First();
            Assert.That(Available(tool, access), Is.False);
            typeof(FurniturePlacementController).GetMethod("UnregisterFurniture", Flags).Invoke(controller, new[] { instance });
            Assert.That(Available(tool, access), Is.True);
        }

        [TestCase(2, 3)]
        [TestCase(3, 3)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        public void IndustrialShelfRejectsEachBlockedAccessCell(int x, int z)
        {
            Assert.That(Register(State("obstacle", tool, new Vector2Int(x, z))), Is.True);
            List<PlacementPreviewCell> cells = new();
            Assert.That(Evaluate(storage, new Vector2Int(2, 2), 0, null, cells), Is.False);
            Assert.That(cells.Any(cell => cell.Cell == new Vector2Int(x, z) && cell.Kind == PlacementCellKind.BlockedAccess), Is.True);
        }

        [Test]
        public void IndustrialShelfSharesAisleMovesAndLeavesOtherFurnitureUntouched()
        {
            object first = State("storage", storage, new Vector2Int(2, 2));
            Assert.That(Register(first), Is.True);
            Assert.That(Available(storage, new Vector2Int(2, 4), 2), Is.True);
            object second = State("other", storage, new Vector2Int(2, 4), 2);
            Assert.That(Register(second), Is.True);
            Assert.That(Available(tool, new Vector2Int(2, 3)), Is.False);
            Assert.That(Available(storage, new Vector2Int(6, 6), 1, "storage"), Is.True);
            typeof(FurniturePlacementController).GetMethod("ApplyFurniturePlacement", Flags)
                .Invoke(controller, new object[] { first, new Vector2Int(6, 6), 1 });
            Assert.That(controller.TryGetFurnitureAtCell(new Vector2Int(6, 6), out string id, out _), Is.True);
            Assert.That(id, Is.EqualTo("storage"));
            Assert.That(controller.TryGetFurnitureAtCell(new Vector2Int(2, 4), out string otherId, out _), Is.True);
            Assert.That(otherId, Is.EqualTo("other"));
            Assert.That(Available(storage, new Vector2Int(2, 3), 0, "storage"), Is.False);
            typeof(FurniturePlacementController).GetMethod("UnregisterFurniture", Flags).Invoke(controller, new[] { second });
            Assert.That(Available(storage, new Vector2Int(2, 3), 0, "storage"), Is.True);
        }

        [Test]
        public void StorageLocationsFollowShelfLifecycleWithoutChangingInventory()
        {
            Assert.That(controller.StoreInventory.TryAddUnits("argamassa", 7), Is.True);
            List<PlacementPreviewCell> preview = new();
            Assert.That(Evaluate(storage, new Vector2Int(2, 2), 0, null, preview), Is.True);
            Assert.That(controller.StorageLocations, Is.Empty);
            object first = State("storage-a", storage, new Vector2Int(2, 2));
            object second = State("storage-b", storage, new Vector2Int(2, 4));
            Assert.That(Register(first), Is.True);
            Assert.That(Register(second), Is.True);
            Assert.That(controller.StorageLocations.Count, Is.EqualTo(2));
            Assert.That(controller.TryGetFurnitureStorageLocation("storage-a", out var firstLocation), Is.True);
            Assert.That(firstLocation.Id, Is.EqualTo("storage-a"));
            Assert.That(firstLocation.Type, Is.EqualTo(MaterialCityTycoon.Commerce.StorageLocationType.Indoor));
            Assert.That(controller.TryGetFurnitureStorageLocation("storage-b", out var secondLocation), Is.True);
            Assert.That(firstLocation, Is.Not.SameAs(secondLocation));

            Assert.That(Available(storage, new Vector2Int(6, 6), 1, "storage-a"), Is.True);
            typeof(FurniturePlacementController).GetMethod("ApplyFurniturePlacement", Flags)
                .Invoke(controller, new object[] { first, new Vector2Int(6, 6), 1 });
            Assert.That(controller.TryGetFurnitureStorageLocation("storage-a", out var movedLocation), Is.True);
            Assert.That(movedLocation, Is.SameAs(firstLocation));
            Assert.That(Available(storage, new Vector2Int(2, 4), 2, "storage-a"), Is.False);
            typeof(FurniturePlacementController).GetField("movingFurniture", Flags).SetValue(controller, first);
            typeof(FurniturePlacementController).GetMethod("CancelFurnitureMove", Flags).Invoke(controller, null);
            Assert.That(controller.TryGetFurnitureStorageLocation("storage-a", out var canceledLocation), Is.True);
            Assert.That(canceledLocation, Is.SameAs(firstLocation));

            typeof(FurniturePlacementController).GetMethod("UnregisterFurniture", Flags).Invoke(controller, new[] { first });
            Assert.That(controller.TryGetFurnitureStorageLocation("storage-a", out _), Is.False);
            Assert.That(controller.TryGetFurnitureStorageLocation("storage-b", out var retainedLocation), Is.True);
            Assert.That(retainedLocation, Is.SameAs(secondLocation));
            Assert.That(controller.StoreInventory.GetQuantity("argamassa"), Is.EqualTo(7));
        }

        [Test]
        public void OtherFurnitureDoesNotCreateOrRemoveStorageLocations()
        {
            Assert.That(Register(State("shelf", shelf, new Vector2Int(2, 2))), Is.True);
            Assert.That(Register(State("tool", tool, new Vector2Int(7, 3))), Is.True);
            Assert.That(Register(State("rack", rack, new Vector2Int(11, 3))), Is.True);
            Assert.That(controller.StorageLocations, Is.Empty);
            object storageState = State("storage", storage, new Vector2Int(2, 8));
            Assert.That(Register(storageState), Is.True);
            Assert.That(controller.TryGetFurnitureStorageLocation("shelf", out _), Is.False);
            Assert.That(controller.TryGetFurnitureStorageLocation(null, out _), Is.False);
            object shelfState = ((System.Collections.IDictionary)typeof(FurniturePlacementController).GetField("furnitureById", Flags).GetValue(controller))["shelf"];
            typeof(FurniturePlacementController).GetMethod("UnregisterFurniture", Flags).Invoke(controller, new[] { shelfState });
            Assert.That(controller.StorageLocations.Count, Is.EqualTo(1));
            Assert.That(controller.TryGetFurnitureStorageLocation("storage", out _), Is.True);
        }

        [Test]
        public void FailedRegistrationDoesNotOccupyCellsOrOverwriteStorage()
        {
            var locations = (MaterialCityTycoon.Commerce.StoreStorageLocations)typeof(FurniturePlacementController).GetField("storageLocations", Flags).GetValue(controller);
            var existing = new MaterialCityTycoon.Commerce.StorageLocation("duplicate", "Existente", MaterialCityTycoon.Commerce.StorageLocationType.Indoor);
            Assert.That(locations.TryRegister(existing), Is.True);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "Could not register storage location for furniture 'duplicate'.");
            Assert.That(Register(State("duplicate", storage, new Vector2Int(2, 2))), Is.False);
            Assert.That(Register(State(null, storage, new Vector2Int(2, 2))), Is.False);
            Assert.That(controller.TryGetFurnitureAtCell(new Vector2Int(2, 2), out _, out _), Is.False);
            Assert.That(locations.TryGet("duplicate", out var retained), Is.True);
            Assert.That(retained, Is.SameAs(existing));
            Assert.That(controller.TryGetFurnitureStorageLocation("duplicate", out _), Is.False);
        }

        [Test]
        public void ControllersHaveIndependentStorageRegistries()
        {
            GameObject otherOwner = new("OtherStore");
            otherOwner.SetActive(false);
            try
            {
                var otherController = otherOwner.AddComponent<FurniturePlacementController>();
                typeof(FurniturePlacementController).GetField("constructionGrid", Flags).SetValue(otherController, otherOwner.AddComponent<ConstructionGrid>());
                ((List<FurnitureDefinition>)typeof(FurniturePlacementController).GetField("furnitureDefinitions", Flags).GetValue(otherController)).Add(storage);
                Assert.That(Register(State("same", storage, new Vector2Int(2, 2))), Is.True);
                Assert.That(otherController.StorageLocations, Is.Empty);
                Assert.That((bool)typeof(FurniturePlacementController).GetMethod("TryRegisterFurniture", Flags)
                    .Invoke(otherController, new[] { State("same", storage, new Vector2Int(2, 2)) }), Is.True);
                Assert.That(controller.TryGetFurnitureStorageLocation("same", out var first), Is.True);
                Assert.That(otherController.TryGetFurnitureStorageLocation("same", out var second), Is.True);
                Assert.That(first, Is.Not.SameAs(second));
            }
            finally { Object.DestroyImmediate(otherOwner); }
        }

        [Test]
        public void StoredUnitsSurviveMoveAndCancelAndPreventRemovalUntilReturned()
        {
            var product = AssetDatabase.LoadAssetAtPath<MaterialCityTycoon.Commerce.ProductDefinition>("Assets/_Game/ScriptableObjects/Products/DEF_Argamassa.asset");
            Assert.That(product, Is.Not.Null);
            Assert.That(storage.StorageCapacity, Is.Not.Null);
            object state = State("stocked", storage, new Vector2Int(2, 2));
            Assert.That(Register(state), Is.True);
            Assert.That(controller.StoreInventory.TryAddUnits(product, 10), Is.True);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("stocked", "storage_bottom", product, 4), Is.True);
            Assert.That(Available(storage, new Vector2Int(6, 6), 1, "stocked"), Is.True);
            typeof(FurniturePlacementController).GetMethod("ApplyFurniturePlacement", Flags)
                .Invoke(controller, new object[] { state, new Vector2Int(6, 6), 1 });
            typeof(FurniturePlacementController).GetField("movingFurniture", Flags).SetValue(controller, state);
            typeof(FurniturePlacementController).GetMethod("CancelFurnitureMove", Flags).Invoke(controller, null);
            Assert.That(controller.ShelfInventory.GetQuantity("stocked", "storage_bottom", product), Is.EqualTo(4));
            GridCellPointer pointer = owner.AddComponent<GridCellPointer>();
            typeof(GridCellPointer).GetField("hasPointedCell", Flags).SetValue(pointer, true);
            typeof(GridCellPointer).GetField("pointedCell", Flags).SetValue(pointer, new Vector2Int(6, 6));
            typeof(FurniturePlacementController).GetField("cellPointer", Flags).SetValue(controller, pointer);
            var remove = typeof(FurniturePlacementController).GetMethod("TryRemoveFurniture", Flags);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "Storage shelf 'stocked' must be emptied before removal.");
            Assert.That((bool)remove.Invoke(controller, null), Is.False);
            Assert.That(controller.ContainsFurniture("stocked"), Is.True);
            Assert.That(controller.TryGetFurnitureStorageLocation("stocked", out _), Is.True);
            Assert.That(controller.TryGetFurnitureAtCell(new Vector2Int(6, 6), out _, out _), Is.True);
            Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(6));
            Assert.That(controller.ShelfInventory.TryReturnToReserve("stocked", "storage_bottom", product, 4), Is.True);
            Assert.That((bool)remove.Invoke(controller, null), Is.True);
            Assert.That(controller.ContainsFurniture("stocked"), Is.False);
            Assert.That(controller.TryGetFurnitureStorageLocation("stocked", out _), Is.False);
            Assert.That(controller.ShelfInventory.HasStoredUnits("stocked"), Is.False);
            Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(10));
        }

        [Test]
        public void RestockingConservesStockAndUpdatesBothVisuals()
        {
            var product = PrepareRestocking();
            Assert.That(controller.StoreInventory.TryAddUnits(product, 30), Is.True);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 24), Is.True);
            GameObject storageVisual = new("storage visual");
            GameObject shelfVisual = Object.Instantiate(shelf.Prefab);
            try
            {
                storageVisual.AddComponent<IndustrialStorageVisualPresenter>().Initialize(controller.ShelfInventory, "storage-a", storage.StorageCapacity);
                var presenter = shelfVisual.AddComponent<ShelfProductVisualPresenter>();
                presenter.Initialize(controller, "display-a", shelf);
                Assert.That(storageVisual.transform.Find("Storage_storage_bottom").childCount, Is.EqualTo(24));
                bool notified = false;
                controller.ShelfInventory.Changed += (id, level) =>
                {
                    notified = true;
                    Assert.That(controller.ShelfInventory.GetQuantity(id, level, product), Is.EqualTo(12));
                    Assert.That(controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(12));
                };
                Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 12), Is.True);
                Assert.That(notified, Is.True);
                typeof(ShelfProductVisualPresenter).GetMethod("Update", Flags).Invoke(presenter, null);
                Assert.That(storageVisual.transform.Find("Storage_storage_bottom").childCount, Is.EqualTo(12));
                Assert.That(shelfVisual.transform.Find("Display_shelf_bottom").childCount, Is.EqualTo(12));
                Assert.That(controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(12));
                Assert.That(controller.ShelfInventory.GetQuantity("storage-a", "storage_bottom", product), Is.EqualTo(12));
                Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(6));
                Assert.That(controller.StoreInventory.GetQuantity(product) + controller.ShelfInventory.GetTotalStored(product)
                    + controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(30));
            }
            finally { Object.DestroyImmediate(storageVisual); Object.DestroyImmediate(shelfVisual); }
        }

        [Test]
        public void RestockingRejectsInconsistentDestinationAndUnconfiguredCapacity()
        {
            var product = PrepareRestocking();
            controller.StoreInventory.TryAddUnits(product, 4);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 4), Is.True);
            var data = new SerializedObject(shelf);
            SerializedProperty slot = data.FindProperty("displaySlots").GetArrayElementAtIndex(0);
            slot.FindPropertyRelative("capacityUnits").intValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 1), Is.False);
            slot.FindPropertyRelative("capacityUnits").intValue = 12;
            slot.FindPropertyRelative("allowedDisplayModes").GetArrayElementAtIndex(0).enumValueIndex = (int)MaterialCityTycoon.Commerce.ProductDisplayMode.Hang;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 1), Is.False);
            slot.FindPropertyRelative("allowedDisplayModes").GetArrayElementAtIndex(0).enumValueIndex = (int)MaterialCityTycoon.Commerce.ProductDisplayMode.Stack;
            data.ApplyModifiedPropertiesWithoutUndo();
            var exposed = (FurnitureDisplaySlotInventory)typeof(FurniturePlacementController).GetField("displaySlotInventory", Flags).GetValue(controller);
            controller.StoreInventory.TryAddUnits("other", 1);
            Assert.That(exposed.TryTransferFromReserve(controller.StoreInventory, "display-a", "shelf_bottom", "other", 1), Is.True);
            Assert.That(controller.StoreInventory.TryAddUnits(product, int.MaxValue), Is.True);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 1), Is.False);
            Assert.That(controller.ShelfInventory.GetQuantity("storage-a", "storage_bottom", product), Is.EqualTo(4));
            Assert.That(exposed.TryGetQuantity("display-a", "shelf_bottom", out string storedProduct, out int count), Is.True);
            Assert.That(storedProduct, Is.EqualTo("other"));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(int.MaxValue));
            Assert.That(exposed.TryTransferToReserve(controller.StoreInventory, "display-a", "shelf_bottom", "other", 1), Is.True);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 1), Is.True);
            Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(int.MaxValue));
            Assert.That(controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(1));
        }

        [Test]
        public void RestockingRejectsInvalidCasesAndKeepsStoresIndependent()
        {
            var product = PrepareRestocking();
            var other = AssetDatabase.LoadAssetAtPath<MaterialCityTycoon.Commerce.ProductDefinition>("Assets/_Game/ScriptableObjects/Products/DEF_Martelo.asset");
            controller.StoreInventory.TryAddUnits(product, 50);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 24), Is.True);
            Assert.That(Register(State("storage-b", storage, new Vector2Int(10, 8))), Is.True);
            Assert.That(Register(State("display-b", shelf, new Vector2Int(5, 12))), Is.True);
            Assert.That(controller.TryAssignProductToDisplaySlot("display-b", "shelf_top", product), Is.True);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-b", "storage_middle", product, 6), Is.True);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 5), Is.True);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 8), Is.False);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-b", "shelf_top", product, 20), Is.False);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-b", "shelf_top", product, 0), Is.False);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-b", "shelf_top", product, -1), Is.False);
            Assert.That(controller.TryRestockFromStorage(null, "storage_bottom", "display-b", "shelf_top", product, 1), Is.False);
            Assert.That(controller.TryRestockFromStorage("storage-a", "missing", "display-b", "shelf_top", product, 1), Is.False);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "missing", "shelf_top", product, 1), Is.False);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "missing", product, 1), Is.False);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_middle", product, 1), Is.False);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", other, 1), Is.False);
            Assert.That(controller.TryRestockFromStorage("storage-b", "storage_middle", "display-b", "shelf_top", product, 7), Is.False);
            Assert.That(controller.TryRestockFromStorage("storage-b", "storage_middle", "display-b", "shelf_top", product, 6), Is.True);
            Assert.That(controller.ShelfInventory.GetQuantity("storage-a", "storage_bottom", product), Is.EqualTo(19));
            Assert.That(controller.ShelfInventory.GetQuantity("storage-b", "storage_middle", product), Is.Zero);
            Assert.That(controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(5));
            Assert.That(controller.GetDisplayedQuantity("display-b", "shelf_top"), Is.EqualTo(6));
            Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(20));
            Assert.That(controller.StoreInventory.GetQuantity(product) + controller.ShelfInventory.GetTotalStored(product) + 5 + 6, Is.EqualTo(50));
            Assert.That(controller.TryTransferReserveToDisplay("display-a", "shelf_bottom", 1), Is.True);
            Assert.That(controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(6));
        }

        [Test]
        public void ReplenishmentNeedsReflectDisplayAndStoredStockWithoutChangingBalances()
        {
            var product = PrepareRestocking();
            var service = new ReplenishmentNeedsService(controller);
            controller.StoreInventory.TryAddUnits(product, 30);
            var empty = service.GetNeeds().Single();
            Assert.That(empty.FurnitureId, Is.EqualTo("display-a"));
            Assert.That(empty.SlotId, Is.EqualTo("shelf_bottom"));
            Assert.That(empty.ProductId, Is.EqualTo(product.ProductId));
            Assert.That(empty.Required, Is.EqualTo(12));
            Assert.That(empty.StoredAvailable, Is.Zero);
            Assert.That(empty.PotentiallyFulfilled, Is.Zero);
            Assert.That(controller.TryTransferReserveToDisplay("display-a", "shelf_bottom", 7), Is.True);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 3), Is.True);
            var partial = service.GetNeeds().Single();
            Assert.That(partial.Capacity, Is.EqualTo(12));
            Assert.That(partial.Displayed, Is.EqualTo(7));
            Assert.That(partial.Required, Is.EqualTo(5));
            Assert.That(partial.StoredAvailable, Is.EqualTo(3));
            Assert.That(partial.PotentiallyFulfilled, Is.EqualTo(3));
            Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(20));
            Assert.That(controller.ShelfInventory.GetTotalStored(product), Is.EqualTo(3));
            Assert.That(controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(7));
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 3), Is.True);
            var afterTransfer = service.GetNeeds().Single();
            Assert.That(afterTransfer.Required, Is.EqualTo(2));
            Assert.That(afterTransfer.StoredAvailable, Is.Zero);
            Assert.That(partial.Required, Is.EqualTo(5)); // Results are immutable snapshots.
            Assert.That(controller.TryTransferReserveToDisplay("display-a", "shelf_bottom", 2), Is.True);
            Assert.That(service.GetNeeds(), Is.Empty);
        }

        [Test]
        public void ReplenishmentAvailabilitySumsRegisteredShelvesAndIsStoreLocal()
        {
            var product = PrepareRestocking();
            Assert.That(Register(State("storage-b", storage, new Vector2Int(10, 8))), Is.True);
            controller.StoreInventory.TryAddUnits(product, 40);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 3), Is.True);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-b", "storage_middle", product, 7), Is.True);
            var service = new ReplenishmentNeedsService(controller);
            var need = service.GetNeeds().Single();
            Assert.That(need.StoredAvailable, Is.EqualTo(10));
            Assert.That(need.PotentiallyFulfilled, Is.EqualTo(10));
            Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(30));
            GameObject other = new("other store"); other.SetActive(false);
            try
            {
                var otherController = other.AddComponent<FurniturePlacementController>();
                typeof(FurniturePlacementController).GetField("constructionGrid", Flags).SetValue(otherController, other.AddComponent<ConstructionGrid>());
                ((List<FurnitureDefinition>)typeof(FurniturePlacementController).GetField("furnitureDefinitions", Flags).GetValue(otherController)).Add(shelf);
                ((List<MaterialCityTycoon.Commerce.ProductDefinition>)typeof(FurniturePlacementController).GetField("productDefinitions", Flags).GetValue(otherController)).Add(product);
                Assert.That((bool)typeof(FurniturePlacementController).GetMethod("TryRegisterFurniture", Flags)
                    .Invoke(otherController, new[] { State("other-display", shelf, new Vector2Int(6, 2)) }), Is.True);
                Assert.That(otherController.TryAssignProductToDisplaySlot("other-display", "shelf_bottom", product), Is.True);
                Assert.That(new ReplenishmentNeedsService(otherController).GetNeeds().Single().StoredAvailable, Is.Zero);
                Assert.That(otherController.ShelfInventory.GetTotalStored(product), Is.Zero);
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void ReplenishmentIgnoresUnassignedInvalidIncompatibleOrRemovedSlots()
        {
            var product = PrepareRestocking();
            var service = new ReplenishmentNeedsService(controller);
            Assert.That(service.GetNeeds().Count, Is.EqualTo(1));
            var data = new SerializedObject(shelf);
            var slot = data.FindProperty("displaySlots").GetArrayElementAtIndex(0);
            slot.FindPropertyRelative("capacityUnits").intValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(service.GetNeeds(), Is.Empty);
            slot.FindPropertyRelative("capacityUnits").intValue = 12;
            slot.FindPropertyRelative("allowedDisplayModes").GetArrayElementAtIndex(0).enumValueIndex = (int)MaterialCityTycoon.Commerce.ProductDisplayMode.Hang;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(service.GetNeeds(), Is.Empty);
            slot.FindPropertyRelative("allowedDisplayModes").GetArrayElementAtIndex(0).enumValueIndex = (int)MaterialCityTycoon.Commerce.ProductDisplayMode.Stack;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(controller.TryAssignProductToDisplaySlot("display-a", "shelf_bottom", null), Is.True);
            Assert.That(service.GetNeeds(), Is.Empty);
            Assert.That(controller.TryAssignProductToDisplaySlot("display-a", "shelf_bottom", product), Is.True);
            Assert.That(service.GetNeeds().Count, Is.EqualTo(1));
            object state = ((System.Collections.IDictionary)typeof(FurniturePlacementController).GetField("furnitureById", Flags).GetValue(controller))["display-a"];
            typeof(FurniturePlacementController).GetMethod("UnregisterFurniture", Flags).Invoke(controller, new[] { state });
            Assert.That(service.GetNeeds(), Is.Empty);
        }

        [Test]
        public void PlanSharesTwelveUnitsAcrossTwoTenUnitNeedsWithoutChangingInventory()
        {
            var product = PrepareRestocking();
            Assert.That(Register(State("display-b", shelf, new Vector2Int(5, 12))), Is.True);
            Assert.That(controller.TryAssignProductToDisplaySlot("display-b", "shelf_bottom", product), Is.True);
            Assert.That(controller.StoreInventory.TryAddUnits(product, 20), Is.True);
            Assert.That(controller.TryTransferReserveToDisplay("display-a", "shelf_bottom", 2), Is.True);
            Assert.That(controller.TryTransferReserveToDisplay("display-b", "shelf_bottom", 2), Is.True);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 12), Is.True);
            var service = new ReplenishmentPlanService(controller);
            ReplenishmentPlan first = service.CreatePlan();
            ReplenishmentPlan second = service.CreatePlan();
            Assert.That(first.Lines.Count, Is.EqualTo(2));
            Assert.That(first.Lines.Select(line => line.FurnitureId), Is.EqualTo(new[] { "display-a", "display-b" }));
            Assert.That(first.Lines.Select(line => line.Quantity), Is.EqualTo(new[] { 10, 2 }));
            Assert.That(first.Lines.Sum(line => line.Quantity), Is.EqualTo(12));
            Assert.That(first.UnmetNeeds.Single().FurnitureId, Is.EqualTo("display-b"));
            Assert.That(first.UnmetNeeds.Single().Quantity, Is.EqualTo(8));
            Assert.That(second.Lines.Select(line => line.Quantity), Is.EqualTo(first.Lines.Select(line => line.Quantity)));
            Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(4));
            Assert.That(controller.ShelfInventory.GetQuantity("storage-a", "storage_bottom", product), Is.EqualTo(12));
            Assert.That(controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(2));
            Assert.That(controller.GetDisplayedQuantity("display-b", "shelf_bottom"), Is.EqualTo(2));
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 4), Is.True);
            var updated = service.CreatePlan();
            Assert.That(updated.Lines.Sum(line => line.Quantity), Is.EqualTo(8));
            Assert.That(updated.Lines[0].Quantity, Is.EqualTo(6));
            Assert.That(first.Lines[0].Quantity, Is.EqualTo(10));
        }

        [Test]
        public void PlanCombinesOrderedLevelsAndStorageShelvesWithinCapacity()
        {
            var product = PrepareRestocking();
            Assert.That(Register(State("storage-b", storage, new Vector2Int(10, 8))), Is.True);
            controller.StoreInventory.TryAddUnits(product, 20);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-b", "storage_bottom", product, 7), Is.True);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_middle", product, 4), Is.True);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 3), Is.True);
            ReplenishmentPlan plan = new ReplenishmentPlanService(controller).CreatePlan();
            Assert.That(plan.Lines.Count, Is.EqualTo(3));
            Assert.That(plan.Lines.Select(line => line.StorageId), Is.EqualTo(new[] { "storage-a", "storage-a", "storage-b" }));
            Assert.That(plan.Lines.Select(line => line.LevelId), Is.EqualTo(new[] { "storage_bottom", "storage_middle", "storage_bottom" }));
            Assert.That(plan.Lines.Select(line => line.Quantity), Is.EqualTo(new[] { 3, 4, 5 }));
            Assert.That(plan.Lines.Sum(line => line.Quantity), Is.EqualTo(12));
            Assert.That(plan.UnmetNeeds, Is.Empty);
            Assert.That(controller.ShelfInventory.GetTotalStored(product), Is.EqualTo(14));
        }

        [Test]
        public void PlanSortsSlotsOfSameFurnitureAndDoesNotDoubleCountSource()
        {
            var product = PrepareRestocking();
            Assert.That(controller.TryAssignProductToDisplaySlot("display-a", "shelf_top", product), Is.True);
            Assert.That(controller.TryAssignProductToDisplaySlot("display-a", "shelf_middle", product), Is.True);
            controller.StoreInventory.TryAddUnits(product, 14);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 14), Is.True);
            var plan = new ReplenishmentPlanService(controller).CreatePlan();
            Assert.That(plan.Lines.Select(line => line.SlotId), Is.EqualTo(new[] { "shelf_bottom", "shelf_middle" }));
            Assert.That(plan.Lines.Select(line => line.Quantity), Is.EqualTo(new[] { 12, 2 }));
            Assert.That(plan.UnmetNeeds.Select(need => need.Quantity), Is.EqualTo(new[] { 10, 12 }));
            Assert.That(controller.ShelfInventory.GetTotalStored(product), Is.EqualTo(14));
        }

        [Test]
        public void PlanReportsMissingStockAndIgnoresFullUnassignedAndIncompatibleSlots()
        {
            var product = PrepareRestocking();
            var service = new ReplenishmentPlanService(controller);
            controller.StoreInventory.TryAddUnits(product, 30);
            var empty = service.CreatePlan();
            Assert.That(empty.Lines, Is.Empty);
            Assert.That(empty.UnmetNeeds.Single().Quantity, Is.EqualTo(12));
            Assert.That(controller.TryTransferReserveToDisplay("display-a", "shelf_bottom", 12), Is.True);
            Assert.That(service.CreatePlan().Lines, Is.Empty);
            Assert.That(service.CreatePlan().UnmetNeeds, Is.Empty);
            Assert.That(controller.TryTransferDisplayToReserve("display-a", "shelf_bottom", 12), Is.True);
            Assert.That(controller.TryAssignProductToDisplaySlot("display-a", "shelf_bottom", null), Is.True);
            Assert.That(service.CreatePlan().UnmetNeeds, Is.Empty);
            Assert.That(controller.TryAssignProductToDisplaySlot("display-a", "shelf_bottom", product), Is.True);
            var data = new SerializedObject(shelf);
            data.FindProperty("displaySlots").GetArrayElementAtIndex(0).FindPropertyRelative("allowedDisplayModes")
                .GetArrayElementAtIndex(0).enumValueIndex = (int)MaterialCityTycoon.Commerce.ProductDisplayMode.Hang;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(service.CreatePlan().UnmetNeeds, Is.Empty);
        }

        [Test]
        public void PlansAreIsolatedBetweenStoresEvenWithSameIds()
        {
            var product = PrepareRestocking();
            controller.StoreInventory.TryAddUnits(product, 5);
            controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 5);
            GameObject other = new("other store"); other.SetActive(false);
            try
            {
                var otherController = other.AddComponent<FurniturePlacementController>();
                typeof(FurniturePlacementController).GetField("constructionGrid", Flags).SetValue(otherController, other.AddComponent<ConstructionGrid>());
                ((List<FurnitureDefinition>)typeof(FurniturePlacementController).GetField("furnitureDefinitions", Flags).GetValue(otherController)).Add(shelf);
                ((List<MaterialCityTycoon.Commerce.ProductDefinition>)typeof(FurniturePlacementController).GetField("productDefinitions", Flags).GetValue(otherController)).Add(product);
                Assert.That((bool)typeof(FurniturePlacementController).GetMethod("TryRegisterFurniture", Flags)
                    .Invoke(otherController, new[] { State("display-a", shelf, new Vector2Int(6, 2)) }), Is.True);
                Assert.That(otherController.TryAssignProductToDisplaySlot("display-a", "shelf_bottom", product), Is.True);
                var otherPlan = new ReplenishmentPlanService(otherController).CreatePlan();
                Assert.That(otherPlan.Lines, Is.Empty);
                Assert.That(otherPlan.UnmetNeeds.Single().Quantity, Is.EqualTo(12));
                Assert.That(new ReplenishmentPlanService(controller).CreatePlan().Lines.Single().Quantity, Is.EqualTo(5));
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void StoreDebugBalancesReflectTransfersAndRejectedOperations()
        {
            var product = PrepareRestocking();
            Assert.That(controller.StoreInventory.TryAddUnits(product, 20), Is.True);
            Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 10), Is.True);
            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 4), Is.True);
            Assert.That(controller.TryTransferDisplayToReserve("display-a", "shelf_bottom", 2), Is.True);

            var balances = MaterialCityTycoon.EditorTools.StoreDebugData.GetBalances(controller, product);
            Assert.That(balances.Reserve, Is.EqualTo(12));
            Assert.That(balances.Storage, Is.EqualTo(6));
            Assert.That(balances.Display, Is.EqualTo(2));
            Assert.That(balances.Total, Is.EqualTo(20));

            Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 7), Is.False);
            var unchanged = MaterialCityTycoon.EditorTools.StoreDebugData.GetBalances(controller, product);
            Assert.That(unchanged.Reserve, Is.EqualTo(balances.Reserve));
            Assert.That(unchanged.Storage, Is.EqualTo(balances.Storage));
            Assert.That(unchanged.Display, Is.EqualTo(balances.Display));
            Assert.That(unchanged.Total, Is.EqualTo(balances.Total));

            Assert.That(controller.TryTransferReserveToDisplay("display-a", "shelf_bottom", 2), Is.True);
            var bypass = MaterialCityTycoon.EditorTools.StoreDebugData.GetBalances(controller, product);
            Assert.That(bypass.Reserve, Is.EqualTo(10));
            Assert.That(bypass.Display, Is.EqualTo(4));
            Assert.That(bypass.Total, Is.EqualTo(20));
        }

        [Test]
        public void SalesConsumeOnlyDisplayCreditCashAndUpdateNeeds()
        {
            var product = PrepareRestocking();
            var price = typeof(MaterialCityTycoon.Commerce.ProductDefinition).GetField("salePriceCents", Flags);
            long oldPrice = (long)price.GetValue(product);
            try
            {
                price.SetValue(product, 2500L);
                Assert.That(controller.StoreInventory.TryAddUnits(product, 20), Is.True);
                Assert.That(controller.ShelfInventory.TryTransferToStorage("storage-a", "storage_bottom", product, 8), Is.True);
                Assert.That(controller.TryRestockFromStorage("storage-a", "storage_bottom", "display-a", "shelf_bottom", product, 5), Is.True);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_bottom", 1, out _), Is.True);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_bottom", 2, out _), Is.True);
                Assert.That(controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(2));
                Assert.That(controller.StoreInventory.GetQuantity(product), Is.EqualTo(12));
                Assert.That(controller.ShelfInventory.GetQuantity("storage-a", "storage_bottom", product), Is.EqualTo(3));
                Assert.That(controller.Sales.BalanceCents, Is.EqualTo(7500));
                Assert.That(controller.Sales.History.Count, Is.EqualTo(2));
                var transaction = controller.Sales.History[1];
                Assert.That(transaction.FurnitureId, Is.EqualTo("display-a"));
                Assert.That(transaction.SlotId, Is.EqualTo("shelf_bottom"));
                Assert.That(transaction.ProductId, Is.EqualTo(product.ProductId));
                Assert.That(transaction.Quantity, Is.EqualTo(2));
                Assert.That(transaction.UnitPriceCents, Is.EqualTo(2500));
                Assert.That(transaction.TotalCents, Is.EqualTo(5000));
                Assert.That(new ReplenishmentNeedsService(controller).GetNeeds().Single(n => n.SlotId == "shelf_bottom").Displayed, Is.EqualTo(2));
            }
            finally { price.SetValue(product, oldPrice); }
        }

        [Test]
        public void RejectedSalesLeaveInventoryCashAndHistoryUnchanged()
        {
            var product = PrepareRestocking();
            var price = typeof(MaterialCityTycoon.Commerce.ProductDefinition).GetField("salePriceCents", Flags);
            long oldPrice = (long)price.GetValue(product);
            try
            {
                controller.StoreInventory.TryAddUnits(product, 3);
                controller.TryTransferReserveToDisplay("display-a", "shelf_bottom", 3);
                price.SetValue(product, 2500L);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_bottom", 0, out _), Is.False);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_bottom", -1, out _), Is.False);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_bottom", 4, out _), Is.False);
                Assert.That(controller.Sales.TrySell(null, "shelf_bottom", 1, out _), Is.False);
                Assert.That(controller.Sales.TrySell("display-a", "missing", 1, out _), Is.False);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_top", 1, out _), Is.False);
                price.SetValue(product, 0L);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_bottom", 1, out _), Is.False);
                price.SetValue(product, -1L);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_bottom", 1, out _), Is.False);
                price.SetValue(product, long.MaxValue);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_bottom", 2, out _), Is.False);
                Assert.That(controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(3));
                Assert.That(controller.Sales.BalanceCents, Is.Zero);
                Assert.That(controller.Sales.History, Is.Empty);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_bottom", 1, out _), Is.True);
                Assert.That(controller.Sales.TrySell("display-a", "shelf_bottom", 1, out _), Is.False);
                Assert.That(controller.GetDisplayedQuantity("display-a", "shelf_bottom"), Is.EqualTo(2));
                Assert.That(controller.Sales.BalanceCents, Is.EqualTo(long.MaxValue));
                Assert.That(controller.Sales.History.Count, Is.EqualTo(1));
                var other = new GameObject("Other store");
                other.SetActive(false);
                try { Assert.That(other.AddComponent<FurniturePlacementController>().Sales.BalanceCents, Is.Zero); }
                finally { Object.DestroyImmediate(other); }
            }
            finally { price.SetValue(product, oldPrice); }
        }

        [Test]
        public void WallsDoNotChangeFurnitureOccupancyOrAccessRules()
        {
            var grid = owner.GetComponent<ConstructionGrid>();
            var walls = new WallEdges(grid);
            Assert.That(Register(State("display-a", shelf, new Vector2Int(6, 2))), Is.True);
            WallEdge edge = new(6, 2, true);
            Assert.That(walls.TryAdd(edge), Is.True);
            Assert.That(controller.TryGetFurnitureAtCell(new Vector2Int(6, 2), out string id, out _), Is.True);
            Assert.That(id, Is.EqualTo("display-a"));
            Assert.That(Available(tool, new Vector2Int(6, 2)), Is.False);
            Assert.That(walls.Remove(edge), Is.True);
            Assert.That(Available(tool, new Vector2Int(6, 2)), Is.False);
        }

        private MaterialCityTycoon.Commerce.ProductDefinition PrepareRestocking()
        {
            var product = AssetDatabase.LoadAssetAtPath<MaterialCityTycoon.Commerce.ProductDefinition>("Assets/_Game/ScriptableObjects/Products/DEF_Argamassa.asset");
            Assert.That(product, Is.Not.Null);
            var data = new SerializedObject(shelf);
            SerializedProperty slots = data.FindProperty("displaySlots");
            for (int i = 0; i < slots.arraySize; i++) slots.GetArrayElementAtIndex(i).FindPropertyRelative("capacityUnits").intValue = 12;
            data.ApplyModifiedPropertiesWithoutUndo();
            ((List<MaterialCityTycoon.Commerce.ProductDefinition>)typeof(FurniturePlacementController).GetField("productDefinitions", Flags).GetValue(controller)).Add(product);
            Assert.That(Register(State("storage-a", storage, new Vector2Int(2, 2))), Is.True);
            Assert.That(Register(State("display-a", shelf, new Vector2Int(6, 2))), Is.True);
            Assert.That(controller.TryAssignProductToDisplaySlot("display-a", "shelf_bottom", product), Is.True);
            return product;
        }

        private FurnitureDefinition Definition(string name, params Vector2Int[] offsets)
        {
            FurnitureDefinition asset = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>($"Assets/_Game/ScriptableObjects/Furniture/{name}.asset");
            Assert.That(asset, Is.Not.Null);
            FurnitureDefinition definition = Object.Instantiate(asset);
            SerializedObject serialized = new(definition);
            SerializedProperty property = serialized.FindProperty("accessCellOffsets");
            property.arraySize = offsets.Length;
            for (int i = 0; i < offsets.Length; i++) property.GetArrayElementAtIndex(i).vector2IntValue = offsets[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static object State(string id, FurnitureDefinition definition, Vector2Int anchor, int rotation = 0)
        {
            System.Type type = typeof(FurniturePlacementController).GetNestedType("PlacedFurnitureState", Flags);
            return System.Activator.CreateInstance(type, id, anchor, definition.FurnitureTypeId, definition.Width, definition.Depth, rotation);
        }

        private bool Register(object state) => (bool)typeof(FurniturePlacementController).GetMethod("TryRegisterFurniture", Flags).Invoke(controller, new[] { state });

        private bool Evaluate(FurnitureDefinition definition, Vector2Int anchor, int rotation, string ignoredId, List<PlacementPreviewCell> cells) =>
            (bool)typeof(FurniturePlacementController).GetMethod("EvaluatePlacement", Flags)
                .Invoke(controller, new object[] { anchor, definition.Width, definition.Depth, rotation, definition, ignoredId, cells });

        private bool Available(FurnitureDefinition definition, Vector2Int anchor, int rotation = 0, string ignoredId = null) =>
            (bool)typeof(FurniturePlacementController).GetMethod("IsFootprintAvailable", Flags)
                .Invoke(controller, new object[] { anchor, definition.Width, definition.Depth, rotation, definition, ignoredId });
    }
}
