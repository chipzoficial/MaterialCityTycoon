using System;
using System.Collections.Generic;

namespace MaterialCityTycoon.Commerce
{
    /// <summary>Describes the kind of infrastructure, not a stock balance or capacity.</summary>
    public enum StorageLocationType
    {
        Indoor,
        OutdoorBulk
    }

    /// <summary>A single storage location belonging to one store.</summary>
    public sealed class StorageLocation
    {
        public string Id { get; }
        public string DisplayName { get; }
        public StorageLocationType Type { get; }

        public StorageLocation(string id, string displayName, StorageLocationType type)
        {
            Id = id;
            DisplayName = displayName;
            Type = type;
        }
    }

    /// <summary>
    /// Registers one store's infrastructure. StoreInventory remains the reserve balance;
    /// its units are not yet physically allocated to these locations.
    /// </summary>
    public sealed class StoreStorageLocations
    {
        private readonly Dictionary<string, StorageLocation> locationsById = new(StringComparer.Ordinal);

        public IReadOnlyCollection<StorageLocation> Locations => locationsById.Values;

        public bool TryRegister(StorageLocation location)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.Id)
                || string.IsNullOrWhiteSpace(location.DisplayName)
                || !Enum.IsDefined(typeof(StorageLocationType), location.Type)
                || locationsById.ContainsKey(location.Id))
            {
                return false;
            }

            locationsById.Add(location.Id, location);
            return true;
        }

        public bool TryGet(string id, out StorageLocation location)
        {
            location = null;
            return !string.IsNullOrWhiteSpace(id) && locationsById.TryGetValue(id, out location);
        }

        public bool TryRemove(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && locationsById.Remove(id);
        }
    }
}
