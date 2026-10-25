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
