using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class FurnitureVisualDimensionsTests
    {
        [Test]
        public void CommercialShelfConfigurationKeepsFootprintAndProducesOnePointEightMetres()
        {
            WithPrefabContents("Assets/_Game/Prefabs/Building/PF_CommercialShelf.prefab", root =>
            {
                Assert.That(ConfigureFurnitureVisualDimensionsMenu.ConfigureCommercialShelf(root), Is.True);
                Bounds meshBounds = ConfigureFurnitureVisualDimensionsMenu.GetCombinedMeshBounds(root);
                AssertBounds(meshBounds, 0.8f, 1.8f, 1.8f);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(7));
                Assert.That((CombinedColliderBounds(root).size - meshBounds.size).sqrMagnitude, Is.LessThan(0.000001f));
                Assert.That(root.transform.Find("Shelf_01").localPosition.y, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(root.transform.Find("Shelf_02").localPosition.y, Is.EqualTo(0.9f).Within(0.0001f));
                Assert.That(root.transform.Find("Shelf_03").localPosition.y, Is.EqualTo(1.6f).Within(0.0001f));
            });
        }

        [Test]
        public void ToolDisplayConfigurationKeepsBaseAndProducesOnePointEightMetres()
        {
            WithPrefabContents("Assets/_Game/Prefabs/Building/PF_ToolDisplay.prefab", root =>
            {
                Assert.That(ConfigureFurnitureVisualDimensionsMenu.ConfigureToolDisplay(root), Is.True);
                Bounds meshBounds = ConfigureFurnitureVisualDimensionsMenu.GetCombinedMeshBounds(root);
                AssertBounds(meshBounds, 0.8f, 1.8f, 0.8f);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(2));
                Assert.That((CombinedColliderBounds(root).size - meshBounds.size).sqrMagnitude, Is.LessThan(0.000001f));
                Assert.That(root.transform.Find("Base_01").GetComponent<MeshRenderer>().bounds.min.y, Is.EqualTo(0f).Within(0.0001f));
            });
        }

        [Test]
        public void PartsBinRackConfigurationRestoresItsEstablishedOnePointFiveMetreHeight()
        {
            WithPrefabContents("Assets/_Game/Prefabs/Building/PF_PartsBinRack.prefab", root =>
            {
                int rendererCount = root.GetComponentsInChildren<MeshRenderer>(true).Length;
                Assert.That(ConfigureFurnitureVisualDimensionsMenu.ConfigurePartsBinRack(root), Is.True);
                Bounds bounds = ConfigureFurnitureVisualDimensionsMenu.GetCombinedMeshBounds(root);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(bounds.size.y, Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(rendererCount));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            });
        }

        [Test]
        public void AdjustedDisplaySlotAnchorsRemainOnTheirPhysicalSurfaces()
        {
            FurnitureDefinition shelf = Object.Instantiate(AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(
                "Assets/_Game/ScriptableObjects/Furniture/DEF_CommercialShelf.asset"));
            FurnitureDefinition tool = Object.Instantiate(AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(
                "Assets/_Game/ScriptableObjects/Furniture/DEF_ToolDisplay.asset"));
            try
            {
                Assert.That(ConfigureFurnitureVisualDimensionsMenu.ConfigureDisplaySlotPositions(shelf,
                    new Dictionary<string, Vector3>
                    {
                        ["shelf_bottom"] = new Vector3(0f, 0.23f, 0f),
                        ["shelf_middle"] = new Vector3(0f, 0.93f, 0f),
                        ["shelf_top"] = new Vector3(0f, 1.63f, 0f)
                    }), Is.True);
                Assert.That(ConfigureFurnitureVisualDimensionsMenu.ConfigureDisplaySlotPositions(tool,
                    new Dictionary<string, Vector3> { ["tool_panel_main"] = new Vector3(0f, 0.975f, 0.03f) }), Is.True);

                Assert.That(FindSlot(shelf, "shelf_bottom").LocalDisplayPosition.y, Is.EqualTo(0.23f).Within(0.0001f));
                Assert.That(FindSlot(shelf, "shelf_middle").LocalDisplayPosition.y, Is.EqualTo(0.93f).Within(0.0001f));
                Assert.That(FindSlot(shelf, "shelf_top").LocalDisplayPosition.y, Is.EqualTo(1.63f).Within(0.0001f));
                Assert.That(FindSlot(tool, "tool_panel_main").LocalDisplayPosition.y, Is.EqualTo(0.975f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(shelf);
                Object.DestroyImmediate(tool);
            }
        }

        [Test]
        public void IndustrialStorageShelfStaysAtTwoMetresUntilItsFbxIsCorrectedInBlender()
        {
            WithPrefabContents("Assets/_Game/Prefabs/Storage/PF_IndustrialStorageShelf.prefab", root =>
            {
                Bounds bounds = ConfigureFurnitureVisualDimensionsMenu.GetCombinedMeshBounds(root);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(bounds.size.y, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(bounds.size.y, Is.Not.EqualTo(ConfigureFurnitureVisualDimensionsMenu.IndustrialStorageShelfHeight));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            });
        }

        private static void WithPrefabContents(string path, System.Action<GameObject> action)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { action(root); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AssertBounds(Bounds bounds, float width, float height, float depth)
        {
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(bounds.size.x, Is.EqualTo(width).Within(0.0001f));
            Assert.That(bounds.size.y, Is.EqualTo(height).Within(0.0001f));
            Assert.That(bounds.size.z, Is.EqualTo(depth).Within(0.0001f));
        }

        private static Bounds CombinedColliderBounds(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders, Is.Not.Empty);
            Bounds result = colliders[0].bounds;
            for (int index = 1; index < colliders.Length; index++) result.Encapsulate(colliders[index].bounds);
            return result;
        }

        private static DisplaySlotDefinition FindSlot(FurnitureDefinition definition, string slotId)
        {
            foreach (DisplaySlotDefinition slot in definition.DisplaySlots)
            {
                if (slot.SlotId == slotId) return slot;
            }

            Assert.Fail($"Slot '{slotId}' was not found.");
            return null;
        }
    }
}
