using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public static class CreateRaceSceneHierarchy
{
    [MenuItem("Tools/Create RaceScene Hierarchy (ScreenSpace-Camera 2160x1080)")]
    public static void CreateRaceScene()
    {
        // Ensure Scenes folder exists
        if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");

        // Create new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // EventSystem
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Main Camera
        var camGO = new GameObject("MainCamera", typeof(Camera));
        var cam = camGO.GetComponent<Camera>();
        cam.tag = "MainCamera";
        camGO.transform.position = new Vector3(0f, 0f, -100f);
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        // Canvas Main (Screen Space - Camera) with reference resolution 2160x1080
        var canvasMainGO = new GameObject("Canvas_Main");
        var canvas = canvasMainGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.sortingOrder = 0;
        var scaler = canvasMainGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2160, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasMainGO.AddComponent<GraphicRaycaster>();

        // BackgroundLayer (single background)
        var backgroundLayer = new GameObject("BackgroundLayer", typeof(RectTransform));
        backgroundLayer.transform.SetParent(canvasMainGO.transform, false);

        // Single initial background tile (UI Image + Scroller). BackgroundSpawner will clone it as needed.
        var bg1 = CreateImageWithScroller("Background_1", backgroundLayer.transform);
        var sc1 = bg1.GetComponent<Scroller>();
        sc1.speed = 100f;

        // mark bg1 as initial background tile
        bg1.tag = "BackgroundTile";

        // add trigger + spawner to manage clones
        backgroundLayer.AddComponent<BackgroundUITrigger>();
        var spawner = backgroundLayer.AddComponent<BackgroundSpawner>();
        spawner.initialTile = bg1.GetComponent<RectTransform>();
        spawner.tilesParent = backgroundLayer.transform;

        // Create a placeholder Finish object under BackgroundLayer (designer can replace visuals/prefab)
        var finishGO = new GameObject("Finish", typeof(RectTransform));
        finishGO.transform.SetParent(backgroundLayer.transform, false);
        // Put it off-camera to the right by default (designer can adjust)
        // This tool can't know exact camera bounds; this is just a starting point.
        finishGO.transform.position = new Vector3(2000f, 0f, 0f);

        // SpawnArea (RectTransform)  <-- vùng spawn (spawn theo local Y)
        var spawnArea = new GameObject("SpawnArea", typeof(RectTransform));
        spawnArea.transform.SetParent(canvasMainGO.transform, false);
        var rtSpawn = spawnArea.GetComponent<RectTransform>();
        rtSpawn.sizeDelta = new Vector2(200, 800);

        // Ducks parent
        var ducks = new GameObject("Ducks");
        ducks.transform.SetParent(canvasMainGO.transform, false);

        // LeaderboardUI (Dynamic List - ScrollView)
        // This creates a ready-to-wire hierarchy. You still need to create/assign a rowPrefab asset in the Inspector.
        var lbGO = new GameObject("LeaderboardUI", typeof(RectTransform));
        lbGO.transform.SetParent(canvasMainGO.transform, false);
        var lbScript = lbGO.AddComponent<LeaderboardUI>();

        // Create a ScrollView structure under LeaderboardUI
        var scrollView = CreateScrollView("LeaderboardScrollView", lbGO.transform);
        var content = scrollView.content;
        lbScript.contentRoot = content;

        // Add layout components to Content
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 8f;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Create a SAMPLE row GameObject in-scene (NOT a prefab asset) to show expected structure.
        // Designers can turn it into a prefab and assign to LeaderboardUI.rowPrefab.
        var sampleRow = CreateLeaderboardRowSample("Row_Sample (MakePrefab)", content);
        // do not assign rowPrefab automatically (expects a prefab asset)

        // Canvas_Loading (separate Canvas - overlay) - ScreenSpace - Camera using same camera but higher order
        var canvasLoadingGO = new GameObject("Canvas_Loading");
        var canvasLoading = canvasLoadingGO.AddComponent<Canvas>();
        canvasLoading.renderMode = RenderMode.ScreenSpaceCamera;
        canvasLoading.worldCamera = cam;
        canvasLoading.sortingOrder = 100; // overlay
        var loadingScaler = canvasLoadingGO.AddComponent<CanvasScaler>();
        loadingScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        loadingScaler.referenceResolution = new Vector2(2160, 1080);
        canvasLoadingGO.AddComponent<GraphicRaycaster>();

        var loadingPanel = new GameObject("LoadingPanel", typeof(RectTransform));
        loadingPanel.transform.SetParent(canvasLoadingGO.transform, false);
        var loadingImage = loadingPanel.AddComponent<Image>();
        loadingImage.color = new Color(0f, 0f, 0f, 0.6f);
        var loadingText = CreateTMPText("LoadingText", loadingPanel.transform, "Loading...");
        loadingText.fontSize = 48;
        loadingText.alignment = TextAlignmentOptions.Center;
        // hide loading panel initially
        loadingPanel.SetActive(false);

        // RaceController root
        var rcGO = new GameObject("RaceController");
        var rc = rcGO.AddComponent<RaceController>();

        // create UI controls and assign to RaceController
        rc.countdownText = CreateTMPText("CountdownText", canvasMainGO.transform, "00:00");
        rc.startButton = CreateButton("StartButton", canvasMainGO.transform, "Start").GetComponent<Button>();
        rc.pauseButton = CreateButton("PauseButton", canvasMainGO.transform, "Pause").GetComponent<Button>();
        rc.continueButton = CreateButton("ContinueButton", canvasMainGO.transform, "Continue").GetComponent<Button>();
        rc.clearButton = CreateButton("ClearButton", canvasMainGO.transform, "Clear").GetComponent<Button>();
        rc.backButton = CreateButton("BackButton", canvasMainGO.transform, "Back").GetComponent<Button>();

        // spawn / duck parent and loading panel
        rc.spawnArea = rtSpawn;
        rc.duckParent = ducks.transform;
        rc.loadingPanel = loadingPanel;
        // wire finish transform (designer can replace)
        rc.finishTransform = finishGO.transform;

        // create a minimal RaceConfig singleton so scene can run and user can edit in SetupScene
        var configGO = new GameObject("RaceConfig");
        var cfg = configGO.AddComponent<RaceConfig>();
        cfg.duckCount = 5;
        cfg.durationSeconds = 15;
        configGO.SetActive(true);

        if (GameObject.FindObjectOfType<EventSystem>() == null)
        {
            es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        // Save scene
        string path = "Assets/Scenes/RaceScene.unity";
        bool ok = EditorSceneManager.SaveScene(scene, path);
        if (ok)
            Debug.Log("RaceScene created at " + path + ". Assign duckPrefab in RaceConfig. Replace the placeholder Finish object under BackgroundLayer with your finish prefab/visuals if needed.");
        else
            Debug.LogError("Failed to save RaceScene.");
    }

    private static GameObject CreateImageWithScroller(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = Color.white;
        var sc = go.AddComponent<Scroller>();
        sc.speed = 100f;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1600, 300); // default tile size
        return go;
    }

    private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string text)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 24;
        tmp.color = Color.white;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, 80);
        return tmp;
    }

    private static GameObject CreateButton(string name, Transform parent, string label)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.8f, 0.8f, 0.8f);
        go.AddComponent<Button>();

        var txt = CreateTMPText(name + "_Label", go.transform, label);
        txt.alignment = TextAlignmentOptions.Center;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 48);
        return go;
    }

    private static (RectTransform root, RectTransform viewport, RectTransform content) CreateScrollView(string name, Transform parent)
    {
        var rootGO = new GameObject(name, typeof(RectTransform));
        rootGO.transform.SetParent(parent, false);
        var rootRT = rootGO.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(600, 500);

        var img = rootGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.35f);

        var scrollRect = rootGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        // Viewport
        var viewportGO = new GameObject("Viewport", typeof(RectTransform));
        viewportGO.transform.SetParent(rootGO.transform, false);
        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(10, 10);
        viewportRT.offsetMax = new Vector2(-10, -10);

        var viewportImage = viewportGO.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.05f);
        viewportGO.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 0);

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;

        return (rootRT, viewportRT, contentRT);
    }

    private static GameObject CreateLeaderboardRowSample(string name, Transform parent)
    {
        var rowGO = new GameObject(name, typeof(RectTransform));
        rowGO.transform.SetParent(parent, false);
        var rt = rowGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 40);

        var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 12f;

        var row = rowGO.AddComponent<LeaderboardRow>();

        var rank = CreateTMPText("Rank", rowGO.transform, "1");
        rank.rectTransform.sizeDelta = new Vector2(60, 40);

        var nameText = CreateTMPText("Name", rowGO.transform, "DuckName");
        nameText.rectTransform.sizeDelta = new Vector2(260, 40);

        var unit = CreateTMPText("Unit", rowGO.transform, "10");
        unit.rectTransform.sizeDelta = new Vector2(60, 40);

        row.rankText = rank;
        row.nameText = nameText;
        row.unitText = unit;

        return rowGO;
    }
}