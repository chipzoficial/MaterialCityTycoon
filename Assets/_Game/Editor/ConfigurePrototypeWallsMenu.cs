using System.Linq;
using MaterialCityTycoon.Building;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MaterialCityTycoon.EditorTools
{
    public static class ConfigurePrototypeWallsMenu
    {
        [MenuItem("Tools/Material City Tycoon/Configure Prototype Walls")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play Mode before configuring walls."); return; }
            Scene scene = SceneManager.GetSceneByPath("Assets/_Game/Scenes/StorePrototype.unity");
            if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("Open StorePrototype before configuring walls."); return; }
            var placements = Resources.FindObjectsOfTypeAll<FurniturePlacementController>().Where(x => x.gameObject.scene == scene).ToArray();
            var huds = Resources.FindObjectsOfTypeAll<BuildHud>().Where(x => x.gameObject.scene == scene).ToArray();
            var walls = Resources.FindObjectsOfTypeAll<WallConstructionController>().Where(x => x.gameObject.scene == scene).ToArray();
            if (placements.Length != 1 || huds.Length != 1 || walls.Length > 1)
            { Debug.LogError("Expected one placement controller, one Build HUD, and at most one wall controller."); return; }
            SerializedObject placement = new(placements[0]), hud = new(huds[0]);
            var grid = placement.FindProperty("constructionGrid").objectReferenceValue as ConstructionGrid;
            var pointer = placement.FindProperty("cellPointer").objectReferenceValue as GridCellPointer;
            var catalog = hud.FindProperty("catalog").objectReferenceValue as GameObject;
            var display = hud.FindProperty("displayCategoryButton").objectReferenceValue as Button;
            var storage = hud.FindProperty("storageCategoryButton").objectReferenceValue as Button;
            UnityEngine.Camera camera = pointer == null ? null : new SerializedObject(pointer).FindProperty("inputCamera").objectReferenceValue as UnityEngine.Camera;
            Collider ground = grid == null ? null : grid.GetComponent<Collider>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (grid == null || ground == null || camera == null || catalog == null || display == null || storage == null || font == null
                || (walls.Length == 1 && (walls[0].transform.parent != null || walls[0].transform.localScale != Vector3.one)))
            { Debug.LogError("Build HUD, ground, camera or wall controller configuration is incomplete."); return; }
            Transform categoryExisting = catalog.transform.Find("StructureCategory");
            Transform wallExisting = catalog.transform.Find("WallButton");
            if ((categoryExisting != null && categoryExisting.GetComponent<Button>() == null)
                || (wallExisting != null && wallExisting.GetComponent<Button>() == null))
            { Debug.LogError("A conflicting StructureCategory or WallButton object exists."); return; }
            WallConstructionController controller;
            if (walls.Length == 0)
            {
                GameObject obj = new("WallConstruction");
                Undo.RegisterCreatedObjectUndo(obj, "Create wall construction");
                SceneManager.MoveGameObjectToScene(obj, scene);
                controller = Undo.AddComponent<WallConstructionController>(obj);
            }
            else controller = walls[0];
            Undo.RecordObject(controller, "Configure walls");
            SerializedObject data = new(controller);
            data.FindProperty("grid").objectReferenceValue = grid;
            data.FindProperty("ground").objectReferenceValue = ground;
            data.FindProperty("inputCamera").objectReferenceValue = camera;
            data.ApplyModifiedProperties();
            Undo.RecordObject(placements[0], "Configure walls");
            placement.FindProperty("walls").objectReferenceValue = controller;
            placement.ApplyModifiedProperties();
            Button structure = categoryExisting != null ? categoryExisting.GetComponent<Button>() : CreateButton("StructureCategory", catalog.transform, font, "Estrutura");
            Button wall = wallExisting != null ? wallExisting.GetComponent<Button>() : CreateButton("WallButton", catalog.transform, font, "Parede");
            SetRect(display.GetComponent<RectTransform>(), new Vector2(18, 218), new Vector2(105, 44));
            SetRect(storage.GetComponent<RectTransform>(), new Vector2(130, 218), new Vector2(105, 44));
            SetRect(structure.GetComponent<RectTransform>(), new Vector2(242, 218), new Vector2(108, 44));
            SetRect(wall.GetComponent<RectTransform>(), new Vector2(18, 159), new Vector2(332, 44));
            // Existing category icons take too much horizontal space in the compact three-tab row.
            foreach (Button button in new[] { display, storage })
            {
                Transform icon = button.transform.Find("Icon");
                Transform label = button.transform.Find("Label");
                if (icon != null) { Undo.RecordObject(icon.gameObject, "Configure category"); icon.gameObject.SetActive(false); }
                if (label is RectTransform rect) { Undo.RecordObject(rect, "Configure category"); rect.offsetMin = new Vector2(8, 0); rect.offsetMax = new Vector2(-4, 0); }
            }
            Undo.RecordObject(huds[0], "Configure walls HUD");
            hud.FindProperty("structureCategoryButton").objectReferenceValue = structure;
            hud.FindProperty("wallButton").objectReferenceValue = wall;
            hud.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Prototype walls configured. Save StorePrototype manually.", controller);
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label)
        {
            GameObject obj = new(name, typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
            obj.transform.SetParent(parent, false);
            Image image = Undo.AddComponent<Image>(obj); image.color = new Color(1f, 1f, 1f, 0.02f);
            Button button = Undo.AddComponent<Button>(obj); button.targetGraphic = image;
            GameObject textObject = new("Label", typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(textObject, "Create label");
            textObject.transform.SetParent(obj.transform, false);
            Text text = Undo.AddComponent<Text>(textObject); text.font = font; text.text = label; text.fontSize = 18;
            text.color = Color.white; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            RectTransform rect = text.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8, 0); rect.offsetMax = new Vector2(-4, 0);
            return button;
        }
        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            Undo.RecordObject(rect, "Arrange build categories");
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = size;
        }
    }
}
