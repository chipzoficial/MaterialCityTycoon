using System.Collections.Generic;
using System.Linq;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using MaterialCityTycoon.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class PartsBinRackSlotLayoutTests
    {
        [Test]
        public void FiveByFiveLayoutUsesStableBottomToTopIdsAndBinCompatibility()
        {
            ProductDefinition screws = AssetDatabase.LoadAssetAtPath<ProductDefinition>(
                "Assets/_Game/ScriptableObjects/Products/DEF_Parafusos.asset");
            Assert.That(screws, Is.Not.Null);
            FurnitureDefinition definition = ScriptableObject.CreateInstance<FurnitureDefinition>();
            try
            {
                SerializedProperty slots = new SerializedObject(definition).FindProperty("displaySlots");
                slots.arraySize = PartsBinRackSlotLayout.Rows * PartsBinRackSlotLayout.Columns;
                List<string> ids = new();
                for (int row = 0; row < PartsBinRackSlotLayout.Rows; row++)
                for (int column = 0; column < PartsBinRackSlotLayout.Columns; column++)
                {
                    int index = row * PartsBinRackSlotLayout.Columns + column;
                    SerializedProperty slot = slots.GetArrayElementAtIndex(index);
                    string id = PartsBinRackSlotLayout.GetSlotId(row, column);
                    slot.FindPropertyRelative("slotId").stringValue = id;
                    slot.FindPropertyRelative("displayName").stringValue = PartsBinRackSlotLayout.GetDisplayName(row, column);
                    SerializedProperty modes = slot.FindPropertyRelative("allowedDisplayModes");
                    modes.arraySize = 1;
                    modes.GetArrayElementAtIndex(0).enumValueIndex = (int)ProductDisplayMode.Bin;
                    ids.Add(id);
                }
                slots.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(ids, Is.Unique);
                Assert.That(ids.First(), Is.EqualTo("bin_r01_c01"));
                Assert.That(ids[19], Is.EqualTo("bin_r04_c05"));
                Assert.That(ids.Last(), Is.EqualTo("bin_r05_c05"));
                Assert.That(definition.DisplaySlots.Count, Is.EqualTo(25));
                foreach (DisplaySlotDefinition slot in definition.DisplaySlots)
                    Assert.That(DisplaySlotCompatibility.IsCompatible(screws, slot), Is.True);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void NewOrganizerModelContainsTwentyFiveNamedDrawers()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Art/Models/Storage/MCT_OrganizadorMiudezas.fbx");
            Assert.That(model, Is.Not.Null);
            MeshRenderer[] drawers = model.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("Caixa_Organizadora_", System.StringComparison.Ordinal)).ToArray();
            Assert.That(drawers.Length, Is.EqualTo(25));
            Assert.That(drawers.Select(renderer => renderer.name), Is.Unique);
            Bounds drawerBounds = GetBounds(model.transform, drawers);
            MeshRenderer[] allRenderers = model.GetComponentsInChildren<MeshRenderer>(true);
            Bounds modelBounds = GetBounds(model.transform, allRenderers);
            TestContext.WriteLine($"New organizer FBX drawer bounds in Unity: X={drawerBounds.size.x:F3} m, Y={drawerBounds.size.y:F3} m, Z={drawerBounds.size.z:F3} m; center={drawerBounds.center}. Full mesh bounds: X={modelBounds.size.x:F3} m, Y={modelBounds.size.y:F3} m, Z={modelBounds.size.z:F3} m; center={modelBounds.center}.");
        }

        [Test]
        public void NestedOrganizerPrefabConfiguresTheTwentyFiveNamedDrawersWithoutLegacyDuplicates()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Building/PF_PartsBinRack.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                prefab.GetComponentsInChildren<MeshRenderer>(true)
                    .Count(renderer => renderer.name.StartsWith("Caixa_Organizadora_", System.StringComparison.Ordinal)),
                Is.EqualTo(25),
                "The saved prefab must expose all twenty-five v2 drawers through its nested FBX hierarchy.");

            bool wasConfigured = ConfigurePartsBinRackSlotsMenu.TryGetBinSlotConfigurations(
                prefab,
                out List<ConfigurePartsBinRackSlotsMenu.BinSlotConfiguration> configurations,
                out int rowCount,
                out int columnCount);

            Assert.That(wasConfigured, Is.True);
            Assert.That(rowCount, Is.EqualTo(5));
            Assert.That(columnCount, Is.EqualTo(5));
            Assert.That(configurations.Count, Is.EqualTo(25));
            Assert.That(configurations[0].slotId, Is.EqualTo("bin_r01_c01"));
            Assert.That(configurations[19].slotId, Is.EqualTo("bin_r04_c05"));
            Assert.That(configurations[24].slotId, Is.EqualTo("bin_r05_c05"));
        }

        [Test]
        public void ExistingOrganizerDefinitionPreservesConfiguredTypeIdFootprintAndAccess()
        {
            FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(
                "Assets/_Game/ScriptableObjects/Furniture/DEF_PartsBinRack.asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.FurnitureTypeId, Is.EqualTo(FurnitureTypeIds.PartsBinRack));
            Assert.That(definition.Width, Is.EqualTo(2));
            Assert.That(definition.Depth, Is.EqualTo(1));
            Assert.That(definition.AccessCellOffsets, Is.EquivalentTo(new[] { new Vector2Int(0, 1), new Vector2Int(1, 1) }));
            HashSet<string> ids = new(definition.DisplaySlots.Select(slot => slot.SlotId));
            for (int row = 0; row < 4; row++)
            for (int column = 0; column < PartsBinRackSlotLayout.Columns; column++)
                Assert.That(ids.Contains(PartsBinRackSlotLayout.GetSlotId(row, column)), Is.True);
        }

        private static Bounds GetBounds(Transform root, MeshRenderer[] renderers)
        {
            Bounds result = TransformBounds(root, renderers[0]);
            for (int index = 1; index < renderers.Length; index++) result.Encapsulate(TransformBounds(root, renderers[index]));
            return result;
        }
        private static Bounds TransformBounds(Transform root, Renderer renderer)
        {
            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = root.worldToLocalMatrix * renderer.localToWorldMatrix;
            Bounds result = new(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
            return result;
        }
    }
}
