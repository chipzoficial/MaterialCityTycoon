using MaterialCityTycoon.Building;
using NUnit.Framework;
using UnityEditor;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class FurnitureTypeIdMigrationTests
    {
        [TestCase("DEF_CommercialShelf.asset", FurnitureTypeIds.CommercialShelf, "100ba2357147b4f4287c50172fed04a3")]
        [TestCase("DEF_ToolDisplay.asset", FurnitureTypeIds.ToolDisplay, "dbdfcfeeccdd7a345a96199a875d750b")]
        [TestCase("DEF_PartsBinRack.asset", FurnitureTypeIds.PartsBinRack, "f117e5bdb80adcd40ba26570853545ad")]
        [TestCase("DEF_IndustrialStorageShelf.asset", FurnitureTypeIds.IndustrialStorageShelf, "e33de7bceebcc884e81642d2262a9aa0")]
        public void DefinitionsUseStableTypeIdsWithoutChangingAssetGuids(
            string assetFileName,
            string expectedTypeId,
            string expectedGuid)
        {
            string path = $"Assets/_Game/ScriptableObjects/Furniture/{assetFileName}";
            FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path);

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.FurnitureTypeId, Is.EqualTo(expectedTypeId));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(expectedGuid));
        }

        [Test]
        public void BuildCatalogDefinitionsHaveUniqueStableTypeIdsAndPreserveSlots()
        {
            string[] files =
            {
                "DEF_CommercialShelf.asset",
                "DEF_ToolDisplay.asset",
                "DEF_PartsBinRack.asset",
                "DEF_IndustrialStorageShelf.asset"
            };

            var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (string file in files)
            {
                FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(
                    $"Assets/_Game/ScriptableObjects/Furniture/{file}");
                Assert.That(definition, Is.Not.Null);
                Assert.That(ids.Add(definition.FurnitureTypeId), Is.True, $"Duplicate Furniture Type ID: {definition.FurnitureTypeId}");
            }

            FurnitureDefinition partsBinRack = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(
                "Assets/_Game/ScriptableObjects/Furniture/DEF_PartsBinRack.asset");
            Assert.That(partsBinRack.DisplaySlots.Count, Is.EqualTo(25));
        }
    }
}
