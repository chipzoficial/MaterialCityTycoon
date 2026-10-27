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
        private readonly List<FurnitureDefinition> definitions = new();

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("hud test"); owner.SetActive(false);
            placement = owner.AddComponent<FurniturePlacementController>();
            var configured = (List<FurnitureDefinition>)typeof(FurniturePlacementController).GetField("furnitureDefinitions", Private).GetValue(placement);
            string[] names = { "DEF_PrototypeShelf", "DEF_PrototypeToolDisplay", "DEF_PrototypePartsBinRack", "DEF_EstanteDeposito" };
            foreach (string name in names)
            {
                FurnitureDefinition definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>($"Assets/_Game/ScriptableObjects/Furniture/{name}.asset");
                Assert.That(definition, Is.Not.Null);
                configured.Add(definition); definitions.Add(definition);
            }
            hud = owner.AddComponent<BuildHud>();
            catalog = new GameObject("catalog"); catalog.transform.SetParent(owner.transform);
            Button build = MakeButton("build"), close = MakeButton("close");
            display = MakeButton("display"); storage = MakeButton("storage");
            furniture = new[] { MakeButton("one"), MakeButton("two"), MakeButton("three"), MakeButton("four") };
            Set("placement", placement); Set("catalog", catalog); Set("buildButton", build); Set("closeButton", close);
            Set("displayCategoryButton", display); Set("storageCategoryButton", storage); Set("furnitureButtons", furniture);
            typeof(BuildHud).GetMethod("Awake", Private).Invoke(hud, null);
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(owner); definitions.Clear(); }

        [Test]
        public void CategoriesAndButtonsSelectRealDefinitions()
        {
            Assert.That(catalog.activeSelf, Is.False);
            Assert.That(furniture[0].gameObject.activeSelf, Is.True);
            Assert.That(furniture[3].gameObject.activeSelf, Is.False);
            for (int i = 0; i < 3; i++)
            {
                furniture[i].onClick.Invoke();
                Assert.That(placement.SelectedFurnitureDefinition, Is.SameAs(definitions[i]));
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
        public void WallAndFurnitureToolsAreMutuallyExclusive()
        {
            WallConstructionController walls = owner.AddComponent<WallConstructionController>();
            ConstructionGrid grid = owner.AddComponent<ConstructionGrid>();
            Collider ground = owner.AddComponent<BoxCollider>();
            UnityEngine.Camera camera = owner.AddComponent<UnityEngine.Camera>();
            typeof(WallConstructionController).GetField("grid", Private).SetValue(walls, grid);
            typeof(WallConstructionController).GetField("ground", Private).SetValue(walls, ground);
            typeof(WallConstructionController).GetField("inputCamera", Private).SetValue(walls, camera);
            typeof(FurniturePlacementController).GetField("walls", Private).SetValue(placement, walls);
            Assert.That(placement.SelectWallTool(), Is.True);
            Assert.That(placement.IsPlacementModeActive, Is.True);
            Assert.That(placement.IsWallModeActive, Is.True);
            Assert.That(placement.SelectFurnitureDefinition(definitions[0]), Is.True);
            Assert.That(placement.IsWallModeActive, Is.False);
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
