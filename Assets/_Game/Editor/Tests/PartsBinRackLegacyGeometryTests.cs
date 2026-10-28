using System.Linq;
using MaterialCityTycoon.Building;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class PartsBinRackLegacyGeometryTests
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Building/PF_PartsBinRack.prefab";
        private const string DefinitionPath = "Assets/_Game/ScriptableObjects/Furniture/DEF_PartsBinRack.asset";
        private const string ModelPath = "Assets/_Game/Art/Models/Storage/MCT_OrganizadorMiudezas.fbx";
        private const string PrefabGuid = "b2eb564179da19446a9d8959a71f256b";

        [Test]
        public void CleanedPrefabKeepsTheNestedTwentyFiveDrawerModelWithoutLegacyBins()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(PrefabPath), Is.EqualTo(PrefabGuid));

            Transform[] legacyGroups = prefab.transform.Cast<Transform>()
                .Where(child => child.name == "Bins" || child.name.StartsWith("Bins (", System.StringComparison.Ordinal))
                .ToArray();
            Assert.That(legacyGroups, Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Transform>(true)
                .Count(transform => transform.name.StartsWith("PB_DrawerCabinet_Bin", System.StringComparison.Ordinal)), Is.EqualTo(0));

            MeshRenderer[] drawers = prefab.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("Caixa_Organizadora_", System.StringComparison.Ordinal))
                .ToArray();
            Assert.That(drawers.Length, Is.EqualTo(25));
            Assert.That(drawers.Select(renderer => renderer.name), Is.Unique);

            Transform modelRoot = prefab.transform.Cast<Transform>().SingleOrDefault(child =>
            {
                Object source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                return source != null && AssetDatabase.GetAssetPath(source) == ModelPath;
            });
            Assert.That(modelRoot, Is.Not.Null);
        }

        [Test]
        public void DefinitionKeepsTheTwentyFiveStableBinSlots()
        {
            FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(DefinitionPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.FurnitureTypeId, Is.EqualTo(FurnitureTypeIds.PartsBinRack));
            Assert.That(definition.DisplaySlots.Count, Is.EqualTo(25));
            Assert.That(definition.DisplaySlots.Select(slot => slot.SlotId), Is.Unique);
            Assert.That(definition.DisplaySlots.First().SlotId, Is.EqualTo("bin_r01_c01"));
            Assert.That(definition.DisplaySlots.Last().SlotId, Is.EqualTo("bin_r05_c05"));
        }
    }
}
