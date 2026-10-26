using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;

namespace MaterialCityTycoon.Building
{
    public readonly struct ReplenishmentPlanLine
    {
        public string StorageId { get; }
        public string LevelId { get; }
        public string FurnitureId { get; }
        public string SlotId { get; }
        public string ProductId { get; }
        public int Quantity { get; }

        internal ReplenishmentPlanLine(string storageId, string levelId, ReplenishmentNeed need, int quantity)
        {
            StorageId = storageId; LevelId = levelId; FurnitureId = need.FurnitureId;
            SlotId = need.SlotId; ProductId = need.ProductId; Quantity = quantity;
        }
    }

    public readonly struct UnmetReplenishmentNeed
    {
        public string FurnitureId { get; }
        public string SlotId { get; }
        public string ProductId { get; }
        public int Quantity { get; }

        internal UnmetReplenishmentNeed(ReplenishmentNeed need, int quantity)
        {
            FurnitureId = need.FurnitureId; SlotId = need.SlotId; ProductId = need.ProductId; Quantity = quantity;
        }
    }

    /// <summary>Immutable snapshot only: no stock is reserved. Revalidate before any future execution.</summary>
    public sealed class ReplenishmentPlan
    {
        public IReadOnlyList<ReplenishmentPlanLine> Lines { get; }
        public IReadOnlyList<UnmetReplenishmentNeed> UnmetNeeds { get; }
        internal ReplenishmentPlan(List<ReplenishmentPlanLine> lines, List<UnmetReplenishmentNeed> unmet)
        {
            Lines = lines.AsReadOnly(); UnmetNeeds = unmet.AsReadOnly();
        }
    }

    /// <summary>Plans one store using ordinal FurnitureId/SlotId and StorageId/LevelId order.</summary>
    public sealed class ReplenishmentPlanService
    {
        private readonly FurniturePlacementController store;
        public ReplenishmentPlanService(FurniturePlacementController store)
        {
            this.store = store != null ? store : throw new ArgumentNullException(nameof(store));
        }

        public ReplenishmentPlan CreatePlan()
        {
            var needs = new List<ReplenishmentNeed>(new ReplenishmentNeedsService(store).GetNeeds());
            needs.Sort((a, b) => CompareKeys(a.FurnitureId, a.SlotId, b.FurnitureId, b.SlotId));
            List<Source> sources = GetSources();
            List<ReplenishmentPlanLine> lines = new();
            List<UnmetReplenishmentNeed> unmet = new();
            foreach (ReplenishmentNeed need in needs)
            {
                int remaining = need.Required;
                if (store.TryGetAssignedProductForDisplaySlot(need.FurnitureId, need.SlotId, out ProductDefinition product))
                {
                    foreach (Source source in sources)
                    {
                        if (remaining == 0) break;
                        if (source.Product != product || source.Remaining == 0) continue;
                        int amount = Math.Min(remaining, source.Remaining);
                        lines.Add(new ReplenishmentPlanLine(source.StorageId, source.LevelId, need, amount));
                        source.Remaining -= amount;
                        remaining -= amount;
                    }
                }
                if (remaining > 0) unmet.Add(new UnmetReplenishmentNeed(need, remaining));
            }
            return new ReplenishmentPlan(lines, unmet);
        }

        private List<Source> GetSources()
        {
            List<Source> sources = new();
            foreach (StorageLocation location in store.StorageLocations)
            {
                if (!store.TryGetFurnitureStorageLocation(location.Id, out _)
                    || !store.TryGetPlacedFurnitureDefinition(location.Id, out FurnitureDefinition furniture)) continue;
                StorageCapacityDefinition capacity = furniture.StorageCapacity;
                if (capacity == null || !capacity.IsValid()) continue;
                foreach (StorageLevelDefinition level in capacity.Levels)
                {
                    ProductDefinition product = capacity.Product;
                    if (!capacity.TryGetCapacity(level.LevelId, product, out int limit)) continue;
                    int quantity = store.ShelfInventory.GetQuantity(location.Id, level.LevelId, product);
                    if (quantity > 0 && quantity <= limit) sources.Add(new Source(location.Id, level.LevelId, product, quantity));
                }
            }
            sources.Sort((a, b) => CompareKeys(a.StorageId, a.LevelId, b.StorageId, b.LevelId));
            return sources;
        }

        private static int CompareKeys(string a, string b, string c, string d)
        {
            int comparison = StringComparer.Ordinal.Compare(a, c);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(b, d);
        }

        private sealed class Source
        {
            public readonly string StorageId;
            public readonly string LevelId;
            public readonly ProductDefinition Product;
            public int Remaining;
            public Source(string storageId, string levelId, ProductDefinition product, int quantity)
            { StorageId = storageId; LevelId = levelId; Product = product; Remaining = quantity; }
        }
    }
}
