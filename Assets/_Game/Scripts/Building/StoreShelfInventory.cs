using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;

namespace MaterialCityTycoon.Building
{
    /// <summary>Store-owned allocations; the reserve contains only unallocated units.</summary>
    public sealed class StoreShelfInventory
    {
        /// <summary>Raised after an allocation has been fully updated.</summary>
        public event Action<string, string> Changed;
        private sealed class Entry
        {
            public string ProductId;
            public int Quantity;
        }

        private readonly StoreInventory reserve;
        private readonly StoreStorageLocations locations;
        private readonly Func<string, StorageCapacityDefinition> resolveCapacity;
        private readonly Dictionary<string, Dictionary<string, Entry>> allocations = new(StringComparer.Ordinal);

        public StoreShelfInventory(StoreInventory reserve, StoreStorageLocations locations,
            Func<string, StorageCapacityDefinition> resolveCapacity)
        {
            this.reserve = reserve ?? throw new ArgumentNullException(nameof(reserve));
            this.locations = locations ?? throw new ArgumentNullException(nameof(locations));
            this.resolveCapacity = resolveCapacity ?? throw new ArgumentNullException(nameof(resolveCapacity));
        }

        public int GetQuantity(string locationId, string levelId, ProductDefinition product)
        {
            return IsValidProduct(product) && TryGetEntry(locationId, levelId, out Entry entry)
                && entry.ProductId == product.ProductId ? entry.Quantity : 0;
        }

        public long GetTotalStored(ProductDefinition product)
        {
            if (!IsValidProduct(product)) return 0;
            long total = 0;
            foreach (Dictionary<string, Entry> levels in allocations.Values)
                foreach (Entry entry in levels.Values)
                    if (entry.ProductId == product.ProductId) total += entry.Quantity;
            return total;
        }

        public bool HasStoredUnits(string locationId)
        {
            return !string.IsNullOrWhiteSpace(locationId)
                && allocations.TryGetValue(locationId, out Dictionary<string, Entry> levels)
                && levels.Count > 0;
        }

        public bool TryTransferToStorage(string locationId, string levelId, ProductDefinition product, int units)
        {
            if (!locations.TryGet(locationId, out _) || !IsValidProduct(product) || units <= 0)
                return false;
            StorageCapacityDefinition capacity = resolveCapacity(locationId);
            if (capacity == null || !capacity.TryGetCapacity(levelId, product, out int limit)) return false;
            bool exists = TryGetEntry(locationId, levelId, out Entry entry);
            int current = exists ? entry.Quantity : 0;
            if ((exists && entry.ProductId != product.ProductId) || units > limit - current
                || !reserve.CanRemoveUnits(product.ProductId, units)) return false;
            if (!reserve.TryRemoveUnits(product.ProductId, units)) return false;
            if (!allocations.TryGetValue(locationId, out Dictionary<string, Entry> levels))
            {
                levels = new Dictionary<string, Entry>(StringComparer.Ordinal);
                allocations.Add(locationId, levels);
            }
            if (!exists)
            {
                entry = new Entry { ProductId = product.ProductId };
                levels.Add(levelId, entry);
            }
            entry.Quantity = current + units;
            Changed?.Invoke(locationId, levelId);
            return true;
        }

        public bool TryReturnToReserve(string locationId, string levelId, ProductDefinition product, int units)
        {
            if (!locations.TryGet(locationId, out _) || !IsValidProduct(product) || units <= 0
                || !TryGetEntry(locationId, levelId, out Entry entry) || entry.ProductId != product.ProductId
                || units > entry.Quantity || !reserve.CanAddUnits(product.ProductId, units)) return false;
            if (!reserve.TryAddUnits(product.ProductId, units)) return false;
            entry.Quantity -= units;
            if (entry.Quantity == 0)
            {
                Dictionary<string, Entry> levels = allocations[locationId];
                levels.Remove(levelId);
                if (levels.Count == 0) allocations.Remove(locationId);
            }
            Changed?.Invoke(locationId, levelId);
            return true;
        }

        // Caller validates the destination's furniture, slot, assignment and compatibility.
        internal bool TryTransferToDisplay(string locationId, string levelId, ProductDefinition product, int units,
            FurnitureDisplaySlotInventory display, string furnitureId, string slotId, int capacityUnits)
        {
            if (!locations.TryGet(locationId, out _) || !IsValidProduct(product) || units <= 0 || display == null
                || capacityUnits <= 0 || !TryGetEntry(locationId, levelId, out Entry entry)
                || entry.ProductId != product.ProductId || units > entry.Quantity) return false;
            StorageCapacityDefinition capacity = resolveCapacity(locationId);
            if (capacity == null || !capacity.TryGetCapacity(levelId, product, out int sourceLimit)
                || entry.Quantity > sourceLimit
                || !display.CanAddUnits(furnitureId, slotId, product.ProductId, units)) return false;
            display.TryGetQuantity(furnitureId, slotId, out _, out int current);
            if (current > capacityUnits || units > capacityUnits - current) return false;

            // No callbacks or other fallible API calls occur between these two mutations.
            if (!display.TryAddUnits(furnitureId, slotId, product.ProductId, units)) return false;
            entry.Quantity -= units;
            if (entry.Quantity == 0)
            {
                Dictionary<string, Entry> levels = allocations[locationId];
                levels.Remove(levelId);
                if (levels.Count == 0) allocations.Remove(locationId);
            }
            if (Changed != null)
                foreach (Action<string, string> listener in Changed.GetInvocationList())
                    try { listener(locationId, levelId); }
                    catch (Exception error) { UnityEngine.Debug.LogException(error); }
            return true;
        }

        private bool TryGetEntry(string locationId, string levelId, out Entry entry)
        {
            entry = null;
            return !string.IsNullOrWhiteSpace(locationId) && !string.IsNullOrWhiteSpace(levelId)
                && allocations.TryGetValue(locationId, out Dictionary<string, Entry> levels)
                && levels.TryGetValue(levelId, out entry);
        }

        private static bool IsValidProduct(ProductDefinition product) => product != null && product.IsValid(out _);
    }
}
