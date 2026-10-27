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
    public static class ConfigureFurnitureInspectionPanelMenu
    {
        private const string StoreScenePath = "Assets/_Game/Scenes/Store.unity";
        private const string UserInterfaceRootName = "FurnitureInspectionUI";
        private const string PanelName = "FurnitureInspectionPanel";
        private const string SlotsContentName = "SlotsContent";

        [MenuItem("Tools/Material City Tycoon/Configure Furniture Inspection Panel")]
        public static void ConfigureFurnitureInspectionPanel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before configuring the furniture inspection panel.");
                return;
            }

            if (!TryGetStoreScene(out Scene scene)
                || !TryGetSingleComponent(scene, out FurniturePlacementController placementController)
                || !TryGetSingleComponent(scene, out GridCellPointer cellPointer)
                || !TryGetCompatibleEventSystem(scene, out EventSystem eventSystem)
                || !TryGetBuiltinFont(out Font font)
                || !TryGetOrCreatePanel(scene, font, out FurnitureInspectionPanel panel))
            {
                return;
            }

            FurnitureInspectionController inspectionController = placementController.GetComponent<FurnitureInspectionController>();
            if (inspectionController == null)
            {
                inspectionController = Undo.AddComponent<FurnitureInspectionController>(placementController.gameObject);
            }

            ConfigureInspectionController(inspectionController, cellPointer, placementController, panel);
            EnsureInputSystemModule(eventSystem, scene);

            EditorUtility.SetDirty(inspectionController);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                "Furniture inspection panel configured with independent scrollable slot rows. Store was marked dirty; save the scene manually when ready.",
                panel);
        }

        private static bool TryGetStoreScene(out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(StoreScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                return true;
            }

            Debug.LogError($"Open '{StoreScenePath}' before running this command.");
            return false;
        }

        private static bool TryGetSingleComponent<T>(Scene scene, out T component)
            where T : Component
        {
            component = null;
            foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
            {
                if (candidate.gameObject.scene != scene)
                {
                    continue;
                }

                if (component != null)
                {
                    Debug.LogError(
                        $"More than one {typeof(T).Name} was found in '{scene.path}'. Configure the intended object manually.",
                        component);
                    return false;
                }

                component = candidate;
            }

            if (component != null)
            {
                return true;
            }

            Debug.LogError($"No {typeof(T).Name} was found in '{scene.path}'.");
            return false;
        }

        private static bool TryGetCompatibleEventSystem(Scene scene, out EventSystem eventSystem)
        {
            eventSystem = null;
            foreach (EventSystem candidate in Resources.FindObjectsOfTypeAll<EventSystem>())
            {
                if (candidate.gameObject.scene != scene)
                {
                    continue;
                }

                if (eventSystem != null)
                {
                    Debug.LogError(
                        $"More than one EventSystem was found in '{scene.path}'. Configure the intended EventSystem manually.",
                        eventSystem);
                    return false;
                }

                eventSystem = candidate;
            }

            if (eventSystem == null || eventSystem.GetComponent<InputSystemUIInputModule>() != null)
            {
                return true;
            }

            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                return true;
            }

            Debug.LogError(
                "The existing EventSystem does not use InputSystemUIInputModule. Replace its input module manually before configuring this panel.",
                eventSystem);
            return false;
        }

        private static bool TryGetBuiltinFont(out Font font)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return true;
            }

            Debug.LogError("The built-in UI font could not be loaded.");
            return false;
        }

        private static bool TryGetOrCreatePanel(Scene scene, Font font, out FurnitureInspectionPanel panel)
        {
            GameObject userInterfaceRoot = FindRootGameObject(scene, UserInterfaceRootName);
            if (userInterfaceRoot == null)
            {
                panel = CreatePanelHierarchy(scene, font);
                return panel != null;
            }

            Canvas canvas = userInterfaceRoot.GetComponent<Canvas>();
            panel = userInterfaceRoot.GetComponentInChildren<FurnitureInspectionPanel>(true);
            if (canvas != null
                && panel != null
                && TryConfigureExistingPanel(userInterfaceRoot, panel, font))
            {
                return true;
            }

            Debug.LogError(
                $"'{UserInterfaceRootName}' already exists but is not a furniture inspection UI created by this tool.",
                userInterfaceRoot);
            return false;
        }

        private static GameObject FindRootGameObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    return root;
                }
            }

            return null;
        }

        private static bool TryConfigureExistingPanel(
            GameObject userInterfaceRoot,
            FurnitureInspectionPanel panel,
            Font font)
        {
            if (!TryGetDirectChild(panel.transform, "FurnitureName", out Transform furnitureNameTransform)
                || !TryGetDirectChild(panel.transform, "SlotsLabel", out Transform slotsLabelTransform)
                || !TryGetDirectChild(panel.transform, "CloseButton", out Transform closeButtonTransform)
                || !TryGetDirectChild(panel.transform, "SlotsViewport", out Transform viewportTransform)
                || !TryGetSlotsContent(viewportTransform, out RectTransform slotsContent))
            {
                return false;
            }

            Text furnitureNameText = furnitureNameTransform.GetComponent<Text>();
            Text slotsLabel = slotsLabelTransform.GetComponent<Text>();
            Button closeButton = closeButtonTransform.GetComponent<Button>();
            ScrollRect slotsScrollRect = viewportTransform.GetComponent<ScrollRect>();
            if (furnitureNameText == null
                || slotsLabel == null
                || closeButton == null
                || slotsScrollRect == null)
            {
                Debug.LogError(
                    "The existing furniture inspection panel is missing one or more required UI components.",
                    panel);
                return false;
            }

            Undo.RecordObject(userInterfaceRoot, "Update Furniture Inspection Panel");
            Undo.RecordObject(panel, "Update Furniture Inspection Panel");
            ConfigurePanelVisuals(
                userInterfaceRoot,
                panel,
                furnitureNameText,
                slotsLabel,
                closeButton,
                slotsScrollRect,
                slotsContent,
                font);
            ConfigurePanelReferences(panel, furnitureNameText, slotsContent, closeButton, slotsScrollRect);
            return true;
        }

        private static bool TryGetSlotsContent(Transform viewportTransform, out RectTransform slotsContent)
        {
            if (!TryGetDirectChild(viewportTransform, SlotsContentName, out Transform contentTransform))
            {
                if (!TryGetDirectChild(viewportTransform, "Slots", out contentTransform))
                {
                    slotsContent = null;
                    return false;
                }

                Undo.RecordObject(contentTransform.gameObject, "Migrate Furniture Inspection Slot Content");
                contentTransform.name = SlotsContentName;
            }

            slotsContent = contentTransform as RectTransform;
            if (slotsContent == null)
            {
                Debug.LogError("Furniture inspection slot content must use a RectTransform.", contentTransform);
                return false;
            }

            Text legacySlotsText = slotsContent.GetComponent<Text>();
            if (legacySlotsText != null)
            {
                Undo.DestroyObjectImmediate(legacySlotsText);
            }

            ContentSizeFitter legacySizeFitter = slotsContent.GetComponent<ContentSizeFitter>();
            if (legacySizeFitter != null)
            {
                Undo.DestroyObjectImmediate(legacySizeFitter);
            }

            return true;
        }

        private static bool TryGetDirectChild(Transform parent, string childName, out Transform child)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                child = parent.GetChild(index);
                if (child.name == childName)
                {
                    return true;
                }
            }

            child = null;
            return false;
        }

        private static FurnitureInspectionPanel CreatePanelHierarchy(Scene scene, Font font)
        {
            GameObject root = CreateUiObject(UserInterfaceRootName, null);
            SceneManager.MoveGameObjectToScene(root, scene);

            Canvas canvas = Undo.AddComponent<Canvas>(root);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler canvasScaler = Undo.AddComponent<CanvasScaler>(root);
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            Undo.AddComponent<GraphicRaycaster>(root);

            GameObject panelObject = CreateUiObject(PanelName, root.transform);
            Image panelImage = Undo.AddComponent<Image>(panelObject);
            panelImage.color = new Color(0.08f, 0.12f, 0.16f, 0.94f);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            SetRect(panelRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(420f, 560f));
            FurnitureInspectionPanel panel = Undo.AddComponent<FurnitureInspectionPanel>(panelObject);

            Text furnitureNameText = CreateText("FurnitureName", panelObject.transform, font, 26, TextAnchor.UpperLeft);
            furnitureNameText.color = Color.white;
            SetRect(
                furnitureNameText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(20f, -18f),
                new Vector2(-134f, 38f));

            Text slotsLabel = CreateText("SlotsLabel", panelObject.transform, font, 18, TextAnchor.UpperLeft);
            slotsLabel.text = "Áreas de exposição";
            slotsLabel.color = new Color(0.72f, 0.8f, 0.86f, 1f);
            SetRect(
                slotsLabel.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(20f, -66f),
                new Vector2(-40f, 28f));

            Button closeButton = CreateCloseButton(panelObject.transform, font);

            GameObject viewportObject = CreateUiObject("SlotsViewport", panelObject.transform);
            Image viewportImage = Undo.AddComponent<Image>(viewportObject);
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            Undo.AddComponent<RectMask2D>(viewportObject);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            SetStretchRect(viewportRect, new Vector2(20f, 20f), new Vector2(-20f, -102f));

            ScrollRect scrollRect = Undo.AddComponent<ScrollRect>(viewportObject);
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = viewportRect;
            scrollRect.scrollSensitivity = 28f;

            GameObject contentObject = CreateUiObject(SlotsContentName, viewportObject.transform);
            RectTransform slotsContent = contentObject.GetComponent<RectTransform>();
            ConfigureSlotsContentRect(slotsContent, viewportRect.rect.height);
            scrollRect.content = slotsContent;

            ConfigurePanelReferences(panel, furnitureNameText, slotsContent, closeButton, scrollRect);
            return panel;
        }

        private static void ConfigurePanelVisuals(
            GameObject userInterfaceRoot,
            FurnitureInspectionPanel panel,
            Text furnitureNameText,
            Text slotsLabel,
            Button closeButton,
            ScrollRect slotsScrollRect,
            RectTransform slotsContent,
            Font font)
        {
            CanvasScaler canvasScaler = userInterfaceRoot.GetComponent<CanvasScaler>();
            if (canvasScaler != null)
            {
                Undo.RecordObject(canvasScaler, "Update Furniture Inspection Panel");
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            Undo.RecordObject(panel.transform, "Update Furniture Inspection Panel");
            SetRect(
                panel.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -24f),
                new Vector2(420f, 560f));

            ConfigureText(furnitureNameText, font, 26, Color.white);
            SetRect(
                furnitureNameText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(20f, -18f),
                new Vector2(-134f, 38f));

            ConfigureText(slotsLabel, font, 18, new Color(0.72f, 0.8f, 0.86f, 1f));
            slotsLabel.text = "Áreas de exposição";
            SetRect(
                slotsLabel.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(20f, -66f),
                new Vector2(-40f, 28f));

            RectTransform viewportRect = slotsScrollRect.viewport;
            Undo.RecordObject(viewportRect, "Update Furniture Inspection Panel");
            SetStretchRect(viewportRect, new Vector2(20f, 20f), new Vector2(-20f, -102f));
            ConfigureViewport(viewportRect);

            Undo.RecordObject(slotsContent, "Update Furniture Inspection Panel");
            ConfigureSlotsContentRect(slotsContent, viewportRect.rect.height);
            slotsScrollRect.content = slotsContent;
            slotsScrollRect.viewport = viewportRect;
            slotsScrollRect.horizontal = false;
            slotsScrollRect.vertical = true;
            slotsScrollRect.scrollSensitivity = 28f;

            RectTransform closeButtonRect = closeButton.GetComponent<RectTransform>();
            Undo.RecordObject(closeButtonRect, "Update Furniture Inspection Panel");
            SetRect(
                closeButtonRect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-16f, -16f),
                new Vector2(100f, 44f));
            Text closeButtonLabel = closeButton.GetComponentInChildren<Text>();
            if (closeButtonLabel != null)
            {
                ConfigureText(closeButtonLabel, font, 17, Color.white);
                closeButtonLabel.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static void ConfigureViewport(RectTransform viewportRect)
        {
            Image image = viewportRect.GetComponent<Image>();
            if (image == null)
            {
                image = Undo.AddComponent<Image>(viewportRect.gameObject);
            }

            Undo.RecordObject(image, "Update Furniture Inspection Panel");
            image.color = Color.clear;
            image.raycastTarget = true;

            Mask legacyMask = viewportRect.GetComponent<Mask>();
            if (legacyMask != null)
            {
                Undo.DestroyObjectImmediate(legacyMask);
            }

            if (viewportRect.GetComponent<RectMask2D>() == null)
            {
                Undo.AddComponent<RectMask2D>(viewportRect.gameObject);
            }
        }

        private static void ConfigureSlotsContentRect(RectTransform slotsContent, float viewportHeight)
        {
            slotsContent.anchorMin = new Vector2(0f, 1f);
            slotsContent.anchorMax = new Vector2(1f, 1f);
            slotsContent.pivot = new Vector2(0.5f, 1f);
            slotsContent.anchoredPosition = Vector2.zero;
            slotsContent.sizeDelta = new Vector2(0f, Mathf.Max(1f, viewportHeight));
        }

        private static void ConfigureText(Text text, Font font, int fontSize, Color color)
        {
            Undo.RecordObject(text, "Update Furniture Inspection Panel");
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject gameObject = CreateUiObject(name, parent);
            Text text = Undo.AddComponent<Text>(gameObject);
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateCloseButton(Transform parent, Font font)
        {
            GameObject buttonObject = CreateUiObject("CloseButton", parent);
            Image image = Undo.AddComponent<Image>(buttonObject);
            image.color = new Color(0.18f, 0.26f, 0.32f, 1f);
            Button button = Undo.AddComponent<Button>(buttonObject);
            SetRect(
                buttonObject.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-16f, -16f),
                new Vector2(100f, 44f));

            Text label = CreateText("Label", buttonObject.transform, font, 17, TextAnchor.MiddleCenter);
            label.text = "Fechar";
            label.color = Color.white;
            label.raycastTarget = false;
            SetStretchRect(label.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static void ConfigureInspectionController(
            FurnitureInspectionController inspectionController,
            GridCellPointer cellPointer,
            FurniturePlacementController placementController,
            FurnitureInspectionPanel panel)
        {
            Undo.RecordObject(inspectionController, "Configure Furniture Inspection Controller");
            SerializedObject serializedController = new(inspectionController);
            serializedController.FindProperty("cellPointer").objectReferenceValue = cellPointer;
            serializedController.FindProperty("furniturePlacementController").objectReferenceValue = placementController;
            serializedController.FindProperty("inspectionPanel").objectReferenceValue = panel;
            serializedController.ApplyModifiedProperties();
        }

        private static void ConfigurePanelReferences(
            FurnitureInspectionPanel panel,
            Text furnitureNameText,
            RectTransform slotsContent,
            Button closeButton,
            ScrollRect slotsScrollRect)
        {
            Undo.RecordObject(panel, "Configure Furniture Inspection Panel");
            SerializedObject serializedPanel = new(panel);
            serializedPanel.FindProperty("furnitureNameText").objectReferenceValue = furnitureNameText;
            serializedPanel.FindProperty("slotsContent").objectReferenceValue = slotsContent;
            serializedPanel.FindProperty("closeButton").objectReferenceValue = closeButton;
            serializedPanel.FindProperty("slotsScrollRect").objectReferenceValue = slotsScrollRect;
            serializedPanel.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);
        }

        private static void EnsureInputSystemModule(EventSystem eventSystem, Scene scene)
        {
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new("EventSystem");
                Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
                SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
                eventSystem = Undo.AddComponent<EventSystem>(eventSystemObject);
                Undo.AddComponent<InputSystemUIInputModule>(eventSystemObject);
                return;
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
            }
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private static void SetStretchRect(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }
    }
}
