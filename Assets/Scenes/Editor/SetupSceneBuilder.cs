#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public static class SetupSceneBuilder
{
    private const string MenuPath = "Tools/Duck Race/Generate Setup Hierarchy";

    [MenuItem(MenuPath)]
    public static void GenerateSetupHierarchy()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        // Root: System_Managers
        GameObject systemManagers = FindOrCreateRoot("System_Managers");
        CreateChildEmpty(systemManagers.transform, "SetupUIManager");
        CreateChildEmpty(systemManagers.transform, "DataManager");
        CreateChildEmpty(systemManagers.transform, "TransitionManager");

        // Canvas_MainUI
        GameObject canvasMain = FindOrCreateCanvas("Canvas_MainUI");
        Canvas canvasMainComp = canvasMain.GetComponent<Canvas>();
        canvasMainComp.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler canvasMainScaler = canvasMain.GetComponent<CanvasScaler>();
        canvasMainScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // === TOP INFO BOARDS ===
        GameObject topInfoBoards = CreateUIGroup("Top_InfoBoards", canvasMain.transform);

        GameObject guiTime = CreateUIImage("GUI_Time", topInfoBoards.transform);
        CreateTMPText("Text_TimeValue", guiTime.transform, "00:00");

        GameObject guiDucks = CreateUIImage("GUI_Ducks", topInfoBoards.transform);
        CreateTMPText("Text_DuckCount", guiDucks.transform, "0");

        // === MODE TOGGLES ===
        GameObject modeToggles = CreateUIGroup("Mode_Toggles", canvasMain.transform);

        CreateUIButtonWithText("Btn_ModeTime", modeToggles.transform, "Time");
        CreateUIButtonWithText("Btn_ModeDucks", modeToggles.transform, "Ducks");

        // === KEYPAD CENTER ===
        GameObject keypadCenter = CreateUIImage("Keypad_Center", canvasMain.transform);
        GridLayoutGroup grid = keypadCenter.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = keypadCenter.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
        }

        // Create buttons 0-9 and ClearBoard
        for (int i = 0; i <= 9; i++)
        {
            string btnName = $"Btn_{i}";
            string label = i.ToString();
            CreateUIButtonWithText(btnName, keypadCenter.transform, label);
        }
        CreateUIButtonWithText("Btn_ClearBoard", keypadCenter.transform, "C");

        // === PANEL NAME MANAGEMENT ===
        GameObject panelNameManagement = CreateUIImage("Panel_NameManagement", canvasMain.transform);

        // ScrollView_NameList (ScrollRect, Image, Mask) -> Viewport -> Content (VerticalLayoutGroup)
        GameObject scrollViewNameList = CreateUIImage("ScrollView_NameList", panelNameManagement.transform);
        ScrollRect scrollRect = scrollViewNameList.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = scrollViewNameList.AddComponent<ScrollRect>();
        }
        Mask rootMask = scrollViewNameList.GetComponent<Mask>();
        if (rootMask == null)
        {
            rootMask = scrollViewNameList.AddComponent<Mask>();
            rootMask.showMaskGraphic = false;
        }

        GameObject viewport = CreateUIGroup("Viewport", scrollViewNameList.transform);
        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
        {
            viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        }
        Mask viewportMask = viewport.GetComponent<Mask>();
        if (viewportMask == null)
        {
            viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
        }
        scrollRect.viewport = viewport.GetComponent<RectTransform>();

        GameObject content = CreateUIGroup("Content", viewport.transform);
        VerticalLayoutGroup verticalLayout = content.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout == null)
        {
            verticalLayout = content.AddComponent<VerticalLayoutGroup>();
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
        }
        scrollRect.content = content.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // InputField_DuckName (TMP_InputField, Image)
        GameObject inputFieldDuckName = CreateTMPInputField("InputField_DuckName", panelNameManagement.transform);

        // Btn_Done (Button, Image, TMP_Text)
        CreateUIButtonWithText("Btn_Done", panelNameManagement.transform, "Done");

        // Btn_ClearAll (Button, Image, TMP_Text)
        CreateUIButtonWithText("Btn_ClearAll", panelNameManagement.transform, "Clear All");

        // === BOTTOM ACTION AREA ===
        GameObject bottomActionArea = CreateUIGroup("Bottom_ActionArea", canvasMain.transform);

        // Btn_ToggleState (Button, Image, TMP_Text)
        CreateUIButtonWithText("Btn_ToggleState", bottomActionArea.transform, "Toggle State");

        // Btn_StartRace (Button, Image, TMP_Text)
        CreateUIButtonWithText("Btn_StartRace", bottomActionArea.transform, "Start Race!");

        // === TRANSITION OBJECTS (INSIDE Canvas_MainUI) ===
        // Flag_Image and its helper points now live directly under Canvas_MainUI
        // so the flag is a regular UI GameObject inside the main canvas, not a
        // separate transition canvas.

        CreateUIImage("Flag_Image", canvasMain.transform);
        CreateUIGroup("Pos_OffScreen", canvasMain.transform);
        CreateUIGroup("Target_A", canvasMain.transform);
        // Target_B is no longer required for layout, but can be added manually
        // if desired. The TransitionManager will also safely destroy any
        // assigned Target_B and Pos_OffScreen at runtime after the move.

        // === EVENT SYSTEM ===
        EnsureEventSystem();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    #region Helpers

    private static GameObject FindOrCreateRoot(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        }
        return obj;
    }

    private static GameObject FindOrCreateCanvas(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj != null)
        {
            if (obj.GetComponent<RectTransform>() == null)
                obj.AddComponent<RectTransform>();
            if (obj.GetComponent<Canvas>() == null)
                obj.AddComponent<Canvas>();
            if (obj.GetComponent<CanvasScaler>() == null)
                obj.AddComponent<CanvasScaler>();
            if (obj.GetComponent<GraphicRaycaster>() == null)
                obj.AddComponent<GraphicRaycaster>();
            return obj;
        }

        obj = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        return obj;
    }

    private static GameObject CreateChildEmpty(Transform parent, string name)
    {
        if (parent == null) return null;

        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject go = new GameObject(name);
        SetParent(go, parent);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static GameObject CreateUIGroup(string name, Transform parent)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        if (existing != null)
            return existing.gameObject;

        GameObject go = new GameObject(name, typeof(RectTransform));
        SetParent(go, parent);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static GameObject CreateUIImage(string name, Transform parent)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        if (existing != null)
        {
            if (existing.GetComponent<RectTransform>() == null)
                existing.gameObject.AddComponent<RectTransform>();
            if (existing.GetComponent<CanvasRenderer>() == null)
                existing.gameObject.AddComponent<CanvasRenderer>();
            if (existing.GetComponent<Image>() == null)
                existing.gameObject.AddComponent<Image>();
            return existing.gameObject;
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        SetParent(go, parent);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static GameObject CreateTMPText(string name, Transform parent, string defaultText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        SetParent(go, parent);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static GameObject CreateUIButtonWithText(string name, Transform parent, string label)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
            if (go.GetComponent<RectTransform>() == null)
                go.AddComponent<RectTransform>();
            if (go.GetComponent<CanvasRenderer>() == null)
                go.AddComponent<CanvasRenderer>();
            if (go.GetComponent<Image>() == null)
                go.AddComponent<Image>();
            if (go.GetComponent<Button>() == null)
                go.AddComponent<Button>();
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            SetParent(go, parent);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }

        Image img = go.GetComponent<Image>();
        img.color = Color.white;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        Transform textChild = go.transform.Find("Text");
        GameObject textGO;
        if (textChild != null)
        {
            textGO = textChild.gameObject;
        }
        else
        {
            textGO = CreateTMPText("Text", go.transform, label);
        }

        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.raycastTarget = false;

        return go;
    }

    private static GameObject CreateTMPInputField(string name, Transform parent)
    {
        GameObject go = CreateUIImage(name, parent);

        TMP_InputField input = go.GetComponent<TMP_InputField>();
        if (input == null)
        {
            input = go.AddComponent<TMP_InputField>();
        }

        // Text Area
        Transform existingTextArea = go.transform.Find("Text Area");
        GameObject textAreaGO;
        if (existingTextArea != null)
        {
            textAreaGO = existingTextArea.gameObject;
        }
        else
        {
            textAreaGO = new GameObject("Text Area", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            SetParent(textAreaGO, go.transform);
            Undo.RegisterCreatedObjectUndo(textAreaGO, "Create Text Area");
        }

        RectTransform textAreaRect = textAreaGO.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = Vector2.zero;
        textAreaRect.offsetMax = Vector2.zero;

        Mask textAreaMask = textAreaGO.GetComponent<Mask>();
        textAreaMask.showMaskGraphic = false;

        Image textAreaImage = textAreaGO.GetComponent<Image>();
        textAreaImage.color = new Color(1f, 1f, 1f, 0.01f);

        // Text
        Transform existingText = textAreaGO.transform.Find("Text");
        GameObject textGO;
        if (existingText != null)
        {
            textGO = existingText.gameObject;
        }
        else
        {
            textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            SetParent(textGO, textAreaGO.transform);
            Undo.RegisterCreatedObjectUndo(textGO, "Create TMP Input Text");
        }

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(5f, 5f);
        textRect.offsetMax = new Vector2(-5f, -5f);

        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = string.Empty;
        tmp.enableWordWrapping = false;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        input.textViewport = textAreaRect;
        input.textComponent = tmp;

        return go;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
    }

    private static void SetParent(GameObject child, Transform parent)
    {
        if (parent == null)
            return;

        GameObjectUtility.SetParentAndAlign(child, parent.gameObject);
    }

    #endregion
}
#endif
