using System.Linq;
using MaterialCityTycoon.Commerce;
using NUnit.Framework;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class StoreStorageLocationsTests
    {
        [Test]
        public void RegistersAndQueriesLocations()
        {
            StoreStorageLocations registry = new();
            StorageLocation indoor = new("indoor-1", "Depósito", StorageLocationType.Indoor);
            StorageLocation outdoor = new("outdoor-1", "Pátio", StorageLocationType.OutdoorBulk);

            Assert.That(registry.TryRegister(indoor), Is.True);
            Assert.That(registry.TryRegister(outdoor), Is.True);
            Assert.That(registry.TryGet("indoor-1", out StorageLocation found), Is.True);
            Assert.That(found, Is.SameAs(indoor));
            Assert.That(found.DisplayName, Is.EqualTo("Depósito"));
            Assert.That(found.Type, Is.EqualTo(StorageLocationType.Indoor));
            Assert.That(registry.Locations, Is.EquivalentTo(new[] { indoor, outdoor }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void InvalidIdsAreRejectedWithoutChangingRegistry(string id)
        {
            StoreStorageLocations registry = new();
            StorageLocation existing = new("existing", "Existente", StorageLocationType.Indoor);
            Assert.That(registry.TryRegister(existing), Is.True);

            Assert.That(registry.TryRegister(new StorageLocation(id, "Inválido", StorageLocationType.Indoor)), Is.False);
            Assert.That(registry.TryGet(id, out _), Is.False);
            Assert.That(registry.TryRemove(id), Is.False);
            Assert.That(registry.Locations.Single(), Is.SameAs(existing));
        }

        [Test]
        public void DuplicateAndInvalidLocationsDoNotReplaceExistingLocation()
        {
            StoreStorageLocations registry = new();
            StorageLocation existing = new("same", "Existente", StorageLocationType.Indoor);
            Assert.That(registry.TryRegister(existing), Is.True);

            Assert.That(registry.TryRegister(null), Is.False);
            Assert.That(registry.TryRegister(new StorageLocation("same", "Outro", StorageLocationType.OutdoorBulk)), Is.False);
            Assert.That(registry.TryRegister(new StorageLocation("bad-name", " ", StorageLocationType.Indoor)), Is.False);
            Assert.That(registry.TryRegister(new StorageLocation("bad-type", "Inválido", (StorageLocationType)99)), Is.False);
            Assert.That(registry.TryGet("missing", out _), Is.False);
            Assert.That(registry.TryRemove("missing"), Is.False);
            Assert.That(registry.Locations.Single(), Is.SameAs(existing));
        }

        [Test]
        public void RemovingLocationDoesNotAffectOtherLocations()
        {
            StoreStorageLocations registry = new();
            StorageLocation first = new("first", "Primeiro", StorageLocationType.Indoor);
            StorageLocation second = new("second", "Segundo", StorageLocationType.OutdoorBulk);
            Assert.That(registry.TryRegister(first), Is.True);
            Assert.That(registry.TryRegister(second), Is.True);

            Assert.That(registry.TryRemove(first.Id), Is.True);
            Assert.That(registry.TryGet(first.Id, out _), Is.False);
            Assert.That(registry.TryRemove(first.Id), Is.False);
            Assert.That(registry.Locations.Single(), Is.SameAs(second));
        }

        [Test]
        public void SeparateStoresHaveIndependentRegistries()
        {
            StoreStorageLocations firstStore = new();
            StoreStorageLocations secondStore = new();
            StorageLocation first = new("location", "Loja A", StorageLocationType.Indoor);
            StorageLocation second = new("location", "Loja B", StorageLocationType.OutdoorBulk);
            Assert.That(firstStore.TryRegister(first), Is.True);
            Assert.That(secondStore.Locations, Is.Empty);
            Assert.That(secondStore.TryRegister(second), Is.True);

            Assert.That(firstStore.TryRemove("location"), Is.True);
            Assert.That(firstStore.Locations, Is.Empty);
            Assert.That(secondStore.TryGet("location", out StorageLocation found), Is.True);
            Assert.That(found, Is.SameAs(second));
        }
    }
}
