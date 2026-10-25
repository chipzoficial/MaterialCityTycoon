using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;

namespace MaterialCityTycoon.Building
{
    /// <summary>A snapshot for one slot; warehouse stock is not reserved by this result.</summary>
    public readonly struct ReplenishmentNeed
    {
        public string FurnitureId { get; }
        public string SlotId { get; }
        public string ProductId { get; }
        public int Capacity { get; }
        public int Displayed { get; }
        public int Required => Capacity - Displayed;
        public long StoredAvailable { get; }
        public int PotentiallyFulfilled => (int)Math.Min(Required, StoredAvailable);

        internal ReplenishmentNeed(string furnitureId, string slotId, string productId, int capacity, int displayed, long storedAvailable)
        {
            FurnitureId = furnitureId; SlotId = slotId; ProductId = productId;
            Capacity = capacity; Displayed = displayed; StoredAvailable = storedAvailable;
        }
    }

    /// <summary>Calculates current needs for one store on demand; never mutates stock.</summary>
    public sealed class ReplenishmentNeedsService
    {
        private readonly FurniturePlacementController store;

        public ReplenishmentNeedsService(FurniturePlacementController store)
        {
            this.store = store != null ? store : throw new ArgumentNullException(nameof(store));
        }

        public IReadOnlyList<ReplenishmentNeed> GetNeeds()
        {
            List<ReplenishmentNeed> needs = new();
            List<string> ids = new();
            Dictionary<string, long> availability = new(StringComparer.Ordinal);
            store.GetPlacedFurnitureIds(ids);
            foreach (string id in ids)
            {
                if (!store.TryGetPlacedFurnitureDefinition(id, out FurnitureDefinition furniture)) continue;
                foreach (DisplaySlotDefinition slot in furniture.DisplaySlots)
                {
                    if (slot == null || !slot.IsValid(out _) || !slot.HasConfiguredCapacity
                        || !store.TryGetAssignedProductForDisplaySlot(id, slot.SlotId, out ProductDefinition product)
                        || !DisplaySlotCompatibility.IsCompatible(product, slot)) continue;
                    int displayed = 0;
                    if (store.TryGetDisplayedInventory(id, slot.SlotId, out string storedProductId, out int quantity))
                    {
                        if (storedProductId != product.ProductId || quantity < 0) continue;
                        displayed = quantity;
                    }
                    if (displayed >= slot.CapacityUnits) continue;
                    if (!availability.TryGetValue(product.ProductId, out long available))
                    {
                        available = GetStoredAvailability(product);
                        availability.Add(product.ProductId, available);
                    }
                    needs.Add(new ReplenishmentNeed(id, slot.SlotId, product.ProductId, slot.CapacityUnits, displayed, available));
                }
            }
            return needs.AsReadOnly();
        }

        private long GetStoredAvailability(ProductDefinition product)
        {
            long total = 0;
            foreach (StorageLocation location in store.StorageLocations)
            {
                if (!store.TryGetFurnitureStorageLocation(location.Id, out _)
                    || !store.TryGetPlacedFurnitureDefinition(location.Id, out FurnitureDefinition furniture)) continue;
                StorageCapacityDefinition capacity = furniture.StorageCapacity;
                if (capacity == null || capacity.Product != product || !capacity.IsValid()) continue;
                foreach (StorageLevelDefinition level in capacity.Levels)
                {
                    if (!capacity.TryGetCapacity(level.LevelId, product, out int limit)) continue;
                    int quantity = store.ShelfInventory.GetQuantity(location.Id, level.LevelId, product);
                    if (quantity >= 0 && quantity <= limit) total += quantity;
                }
            }
            return total;
        }
    }
}
