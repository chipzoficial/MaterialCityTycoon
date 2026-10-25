using System.Linq;
using MaterialCityTycoon.Building;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MaterialCityTycoon.EditorTools
{
    public static class ConfigureBuildHudMenu
    {
        private const string ScenePath = "Assets/_Game/Scenes/StorePrototype.unity";
        private static readonly Color Surface = new(0.10f, 0.11f, 0.13f, 0.97f);

        [MenuItem("Tools/Material City Tycoon/Configure Build HUD")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Exit Play Mode before configuring the build HUD."); return; }
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("Open StorePrototype before configuring the build HUD."); return; }
            var controllers = Resources.FindObjectsOfTypeAll<FurniturePlacementController>().Where(c => c.gameObject.scene == scene).ToArray();
            var huds = Resources.FindObjectsOfTypeAll<BuildHud>().Where(c => c.gameObject.scene == scene).ToArray();
            var systems = Resources.FindObjectsOfTypeAll<EventSystem>().Where(c => c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded).ToArray();
            Sprite hammer = Icon("hammer"), grid = Icon("layout-grid"), warehouse = Icon("warehouse"), close = Icon("x");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Shader iconShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Art/UI/BuildHudIcon.shader");
            if (controllers.Length != 1 || huds.Length > 1 || systems.Length > 1 || hammer == null || grid == null || warehouse == null || close == null || font == null || iconShader == null)
            { Debug.LogError("Build HUD requires one placement controller, at most one HUD and EventSystem, and the four Lucide sprites."); return; }
            if (systems.Length == 1 && systems[0].GetComponent<InputSystemUIInputModule>() == null && systems[0].GetComponent<BaseInputModule>() != null)
            { Debug.LogError("The existing EventSystem must use InputSystemUIInputModule.", systems[0]); return; }
            if (huds.Length == 1)
            {
                SerializedObject existing = new(huds[0]);
                SerializedProperty buttons = existing.FindProperty("furnitureButtons");
                if (existing.FindProperty("catalog").objectReferenceValue == null || buttons.arraySize != 4
                    || existing.FindProperty("buildButton").objectReferenceValue == null || existing.FindProperty("closeButton").objectReferenceValue == null
                    || existing.FindProperty("displayCategoryButton").objectReferenceValue == null || existing.FindProperty("storageCategoryButton").objectReferenceValue == null
                    || Enumerable.Range(0, 4).Any(i => buttons.GetArrayElementAtIndex(i).objectReferenceValue == null))
                { Debug.LogError("The existing Build HUD has incomplete references; repair it before configuring.", huds[0]); return; }
                EnsureEventSystem(systems, scene);
                Undo.RecordObject(huds[0], "Configure Build HUD");
                existing.FindProperty("placement").objectReferenceValue = controllers[0];
                existing.FindProperty("iconShader").objectReferenceValue = iconShader;
                existing.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("Build HUD is already configured. Save the scene manually.", huds[0]);
                return;
            }
            if (Resources.FindObjectsOfTypeAll<Transform>().Any(t => t.gameObject.scene == scene && t.name == "BuildHUD"))
            { Debug.LogError("An object named BuildHUD already exists without a configured BuildHud component."); return; }
            EnsureEventSystem(systems, scene);

            FurnitureInspectionPanel inspection = Resources.FindObjectsOfTypeAll<FurnitureInspectionPanel>().FirstOrDefault(p => p.gameObject.scene == scene);
            Canvas canvas = inspection == null ? null : inspection.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                GameObject canvasObject = Ui("BuildHUDCanvas", null);
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                canvas = Undo.AddComponent<Canvas>(canvasObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                Undo.AddComponent<GraphicRaycaster>(canvasObject);
            }
            GameObject root = Ui("BuildHUD", canvas.transform);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one; rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            BuildHud hud = Undo.AddComponent<BuildHud>(root);
            Button build = Button("BuildButton", root.transform, font, "Construir", hammer, new Vector2(24, 24), new Vector2(164, 52));
            build.image.color = Surface;
            GameObject panel = Ui("BuildCatalog", root.transform);
            SetRect((RectTransform)panel.transform, new Vector2(24, 88), new Vector2(370, 330));
            Undo.AddComponent<Image>(panel).color = Surface;
            Text title = Label("Title", panel.transform, font, "Construir", 24, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(18, 276), new Vector2(270, 40));
            Button closeButton = Button("CloseButton", panel.transform, font, "", close, new Vector2(314, 276), new Vector2(40, 40));
            Button display = Button("DisplayCategory", panel.transform, font, "Exposição", grid, new Vector2(18, 218), new Vector2(160, 44));
            Button storage = Button("StorageCategory", panel.transform, font, "Depósito", warehouse, new Vector2(185, 218), new Vector2(165, 44));
            Button[] furniture = new Button[4];
            for (int i = 0; i < 4; i++) furniture[i] = Button("Furniture" + i, panel.transform, font, "", null,
                new Vector2(18, 159 - (i == 3 ? 0 : i) * 51), new Vector2(332, 44));
            SerializedObject data = new(hud);
            data.FindProperty("placement").objectReferenceValue = controllers[0];
            data.FindProperty("iconShader").objectReferenceValue = iconShader;
            data.FindProperty("catalog").objectReferenceValue = panel;
            data.FindProperty("buildButton").objectReferenceValue = build;
            data.FindProperty("closeButton").objectReferenceValue = closeButton;
            data.FindProperty("displayCategoryButton").objectReferenceValue = display;
            data.FindProperty("storageCategoryButton").objectReferenceValue = storage;
            SerializedProperty list = data.FindProperty("furnitureButtons"); list.arraySize = 4;
            for (int i = 0; i < 4; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = furniture[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            panel.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Build HUD configured. Save StorePrototype manually.", hud);
        }

        private static void EnsureEventSystem(EventSystem[] systems, Scene scene)
        {
            if (systems.Length == 0)
            {
                GameObject es = new("EventSystem"); Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
                SceneManager.MoveGameObjectToScene(es, scene);
                Undo.AddComponent<EventSystem>(es); Undo.AddComponent<InputSystemUIInputModule>(es);
            }
            else if (systems[0].GetComponent<InputSystemUIInputModule>() == null)
                Undo.AddComponent<InputSystemUIInputModule>(systems[0].gameObject);
        }

        private static Sprite Icon(string name) => AssetDatabase.LoadAllAssetsAtPath($"Assets/_Game/Art/UI/Icons/{name}.png").OfType<Sprite>().FirstOrDefault();
        private static GameObject Ui(string name, Transform parent)
        {
            GameObject obj = new(name, typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
            if (parent != null) obj.transform.SetParent(parent, false);
            return obj;
        }
        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        private static Text Label(string name, Transform parent, Font font, string text, int size, TextAnchor alignment)
        {
            Text label = Undo.AddComponent<Text>(Ui(name, parent)); label.font = font; label.fontSize = size;
            label.text = text; label.color = Color.white; label.alignment = alignment; label.raycastTarget = false;
            return label;
        }
        private static Button Button(string name, Transform parent, Font font, string text, Sprite icon, Vector2 position, Vector2 size)
        {
            GameObject obj = Ui(name, parent); SetRect((RectTransform)obj.transform, position, size);
            Image background = Undo.AddComponent<Image>(obj); background.color = new Color(1, 1, 1, 0.02f);
            Button button = Undo.AddComponent<Button>(obj); button.targetGraphic = background;
            if (icon != null)
            {
                Image image = Undo.AddComponent<Image>(Ui("Icon", obj.transform)); image.sprite = icon; image.preserveAspect = true; image.raycastTarget = false;
                SetRect(image.rectTransform, new Vector2(text.Length == 0 ? (size.x - 22) * 0.5f : 12, (size.y - 22) * 0.5f), new Vector2(22, 22));
            }
            if (text.Length > 0 || icon == null)
            {
                Text label = Label("Label", obj.transform, font, text, 18, TextAnchor.MiddleLeft);
                RectTransform rect = label.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(icon == null ? 14 : 43, 0); rect.offsetMax = new Vector2(-8, 0);
            }
            return button;
        }
    }
}
