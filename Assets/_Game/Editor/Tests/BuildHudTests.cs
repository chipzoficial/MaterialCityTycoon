using System.Collections.Generic;
using System.Reflection;
using MaterialCityTycoon.Building;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MaterialCityTycoon.EditorTests
{
    public sealed class BuildHudTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject owner;
        private FurniturePlacementController placement;
        private BuildHud hud;
        private GameObject catalog;
        private Button[] furniture;
        private Button display;
        private Button storage;
        private Button structure;
        private Button wall;
        private Button door;
        private Button floor;
        private readonly List<FurnitureDefinition> definitions = new();
        private readonly List<GameObject> temporaryPrefabs = new();

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("hud test"); owner.SetActive(false);
            placement = owner.AddComponent<FurniturePlacementController>();
            var configured = (List<FurnitureDefinition>)typeof(FurniturePlacementController).GetField("furnitureDefinitions", Private).GetValue(placement);
            string[] names = { "DEF_CommercialShelf", "DEF_ToolDisplay", "DEF_PartsBinRack", "DEF_IndustrialStorageShelf" };
            foreach (string name in names)
            {
                FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>($"Assets/_Game/ScriptableObjects/Furniture/{name}.asset");
                Assert.That(definition, Is.Not.Null);
                configured.Add(definition); definitions.Add(definition);
            }
            foreach (string id in new[] { FurnitureTypeIds.PalletBagDisplay, FurnitureTypeIds.DoubleSidedGondola })
            {
                FurnitureDefinition definition = ScriptableObject.CreateInstance<FurnitureDefinition>();
                GameObject prefab = new(id + " prefab");
                temporaryPrefabs.Add(prefab);
                SerializedObject serialized = new(definition);
                serialized.FindProperty("furnitureTypeId").stringValue = id;
                serialized.FindProperty("displayName").stringValue = id;
                serialized.FindProperty("prefab").objectReferenceValue = prefab;
                serialized.FindProperty("width").intValue = 1;
                serialized.FindProperty("depth").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                configured.Add(definition); definitions.Add(definition);
            }
            hud = owner.AddComponent<BuildHud>();
            catalog = new GameObject("catalog"); catalog.transform.SetParent(owner.transform);
            Button build = MakeButton("build"), close = MakeButton("close");
            display = MakeButton("display"); storage = MakeButton("storage");
            structure = MakeButton("structure"); wall = MakeButton("wall"); door = MakeButton("door"); floor = MakeButton("floor");
            furniture = new[] { MakeButton("one"), MakeButton("two"), MakeButton("three"), MakeButton("four"), MakeButton("five"), MakeButton("six") };
            Set("placement", placement); Set("catalog", catalog); Set("buildButton", build); Set("closeButton", close);
            Set("displayCategoryButton", display); Set("storageCategoryButton", storage); Set("structureCategoryButton", structure);
            Set("furnitureButtons", furniture); Set("wallButton", wall); Set("doorButton", door); Set("floorButton", floor);
            typeof(BuildHud).GetMethod("Awake", Private).Invoke(hud, null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (FurnitureDefinition definition in definitions)
                if (definition != null && !AssetDatabase.Contains(definition)) Object.DestroyImmediate(definition);
            foreach (GameObject prefab in temporaryPrefabs) if (prefab != null) Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(owner); definitions.Clear(); temporaryPrefabs.Clear();
        }

        [Test]
        public void CategoriesAndButtonsSelectRealDefinitions()
        {
            Assert.That(catalog.activeSelf, Is.False);
            Assert.That(furniture[0].gameObject.activeSelf, Is.True);
            Assert.That(furniture[3].gameObject.activeSelf, Is.False);
            Assert.That(furniture[4].gameObject.activeSelf, Is.True);
            Assert.That(furniture[5].gameObject.activeSelf, Is.True);
            foreach (int index in new[] { 0, 1, 2, 4, 5 })
            {
                furniture[index].onClick.Invoke();
                Assert.That(placement.SelectedFurnitureDefinition, Is.SameAs(definitions[index]));
            }
            storage.onClick.Invoke();
            Assert.That(furniture[0].gameObject.activeSelf, Is.False);
            Assert.That(furniture[3].gameObject.activeSelf, Is.True);
            furniture[3].onClick.Invoke();
            Assert.That(placement.SelectedFurnitureDefinition, Is.SameAs(definitions[3]));
            display.onClick.Invoke();
            Assert.That(furniture[0].gameObject.activeSelf, Is.True);
            Assert.That(placement.SelectFurnitureDefinition(null), Is.False);
        }

        [Test]
        public void KeyboardSelectionChangesDisplayedCategory()
        {
            placement.SelectFurnitureDefinition(definitions[3]);
            typeof(BuildHud).GetMethod("Update", Private).Invoke(hud, null);
            Assert.That(furniture[3].gameObject.activeSelf, Is.True);
            Assert.That(furniture[0].gameObject.activeSelf, Is.False);
        }

        [Test]
        public void StructureCategoryDisplaysWallDoorAndFloorTools()
        {
            structure.onClick.Invoke();
            Assert.That(wall.gameObject.activeSelf, Is.True);
            Assert.That(door.gameObject.activeSelf, Is.True);
            Assert.That(floor.gameObject.activeSelf, Is.True);
            Assert.That(furniture[0].gameObject.activeSelf, Is.False);
        }

        [Test]
        public void WallAndFurnitureToolsAreMutuallyExclusive()
        {
            WallConstructionController walls = owner.AddComponent<WallConstructionController>();
            FloorConstructionController floors = owner.AddComponent<FloorConstructionController>();
            ConstructionGrid grid = owner.AddComponent<ConstructionGrid>();
            Collider ground = owner.AddComponent<BoxCollider>();
            UnityEngine.Camera camera = owner.AddComponent<UnityEngine.Camera>();
            typeof(WallConstructionController).GetField("grid", Private).SetValue(walls, grid);
            typeof(WallConstructionController).GetField("ground", Private).SetValue(walls, ground);
            typeof(WallConstructionController).GetField("inputCamera", Private).SetValue(walls, camera);
            typeof(FloorConstructionController).GetField("grid", Private).SetValue(floors, grid);
            typeof(FloorConstructionController).GetField("ground", Private).SetValue(floors, ground);
            typeof(FloorConstructionController).GetField("inputCamera", Private).SetValue(floors, camera);
            typeof(FurniturePlacementController).GetField("walls", Private).SetValue(placement, walls);
            typeof(FurniturePlacementController).GetField("floors", Private).SetValue(placement, floors);
            Assert.That(placement.SelectWallTool(), Is.True);
            Assert.That(placement.IsPlacementModeActive, Is.True);
            Assert.That(placement.IsWallModeActive, Is.True);
            door.onClick.Invoke();
            Assert.That(placement.IsDoorToolActive, Is.True);
            Assert.That(placement.IsWallModeActive, Is.False);
            floor.onClick.Invoke();
            Assert.That(placement.IsFloorToolActive, Is.True);
            Assert.That(placement.IsDoorToolActive, Is.False);
            Assert.That(placement.SelectFurnitureDefinition(definitions[0]), Is.True);
            Assert.That(placement.IsWallModeActive, Is.False);
            Assert.That(placement.IsFloorToolActive, Is.False);
            Assert.That(placement.SelectedFurnitureDefinition, Is.SameAs(definitions[0]));
            var previewField = typeof(FurniturePlacementController).GetField("previewVisual", Private);
            Object.DestroyImmediate((GameObject)previewField.GetValue(placement));
            previewField.SetValue(placement, null);
        }

        private Button MakeButton(string name)
        {
            GameObject obj = new(name, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(owner.transform);
            Button button = obj.GetComponent<Button>(); button.targetGraphic = obj.GetComponent<Image>(); return button;
        }
        private void Set(string name, object value) => typeof(BuildHud).GetField(name, Private).SetValue(hud, value);
    }
}
