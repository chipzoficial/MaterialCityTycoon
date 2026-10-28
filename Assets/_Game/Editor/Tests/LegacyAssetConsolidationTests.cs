using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class LegacyAssetConsolidationTests
    {
        private const string LegacyShelfReviewPath = "Assets/_Game/Prefabs/Building/Review/M0.5c.2/PrototypeShelf.prefab";
        private const string ConsolidatedMortarMaterialPath = "Assets/_Game/Art/Materials/Products/MAT_SacoArgamassa20kg.mat";
        private const string InactiveMortarMaterialReviewPath = "Assets/_Game/Art/Materials/Products/Review/M0.5c.2/MAT_SacoArgamassa20kg.mat";
        private const string MortarProductPrefabPath = "Assets/_Game/Prefabs/Products/PF_SacoArgamassa20kg.prefab";

        [Test]
        public void ConsolidationPreservesTheActiveMortarMaterialAndItsRendererReference()
        {
            RequireConsolidatedLayout();

            Material activeMaterial = AssetDatabase.LoadAssetAtPath<Material>(ConsolidatedMortarMaterialPath);
            Assert.That(activeMaterial, Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(ConsolidatedMortarMaterialPath), Is.EqualTo("413081e4221154c4c97363c40340acbc"));
            Assert.That(activeMaterial.GetTexture("_BaseMap"), Is.Not.Null);

            GameObject productPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MortarProductPrefabPath);
            Assert.That(productPrefab, Is.Not.Null);
            Assert.That(productPrefab.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer.sharedMaterials.Any(material => material == activeMaterial)), Is.True);
            Assert.That(AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Art/Textures/Products/Materials/TEX_SacoArgamassa20kg.mat"), Is.Null);
        }

        [Test]
        public void ConsolidationIsReversibleAndKeepsUnusedAssetsInReviewFolders()
        {
            RequireConsolidatedLayout();

            GameObject legacyShelf = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyShelfReviewPath);
            Material inactiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(InactiveMortarMaterialReviewPath);

            Assert.That(legacyShelf, Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(LegacyShelfReviewPath), Is.EqualTo("c2ad3e6f69620de43a443b92ddb73e6a"));
            Assert.That(inactiveMaterial, Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(InactiveMortarMaterialReviewPath), Is.EqualTo("db023c4649381e84f9aff0860415a935"));
            Assert.That(AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Prefabs/Building/PrototypeShelf.prefab"), Is.Null);
        }

        private static void RequireConsolidatedLayout()
        {
            if (AssetDatabase.AssetPathToGUID(ConsolidatedMortarMaterialPath) != "413081e4221154c4c97363c40340acbc")
            {
                Assert.Ignore("Run Tools > Material City Tycoon > Consolidate Legacy Assets (M0.5c.2) before validating the consolidated layout.");
            }
        }
    }
}
