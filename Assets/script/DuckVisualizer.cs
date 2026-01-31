using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase 2: debug visualization.
/// - Tier tint (Image/Renderer)
/// - Runtime-generated stamina bar (world-space canvas)
/// </summary>
public sealed class DuckVisualizer : MonoBehaviour
{
    [Header("Optional renderers")]
    [Tooltip("If duck is a UI prefab, assign (or auto-find) the Image to tint.")]
    [SerializeField] private Image uiImage;

    [Tooltip("If duck is a world object, assign (or auto-find) the Renderer to tint.")]
    [SerializeField] private Renderer worldRenderer;

    [Header("Stamina Bar Settings")]
    [Tooltip("If false, stamina bar is hidden/disabled (for performance).")]
    public bool showStaminaBar = true;

    [Tooltip("Local offset above duck (world space).")]
    public Vector3 staminaBarOffset = new Vector3(0f, 1.0f, 0f);

    [Tooltip("Bar size in world units (used as RectTransform size; camera scaling may vary).")]
    public Vector2 staminaBarSize = new Vector2(1.2f, 0.18f);

    [Range(0.001f, 0.2f)]
    public float staminaBarBorder = 0.02f;

    private GameObject staminaRoot;
    private Image staminaFillImage;

    private void Awake()
    {
        if (uiImage == null) uiImage = GetComponentInChildren<Image>();
        if (worldRenderer == null) worldRenderer = GetComponentInChildren<Renderer>();

        // Create bar now (so toggling doesn't allocate later), but respect showStaminaBar.
        InitializeStaminaBar();
        SetStaminaBarVisible(showStaminaBar);
    }

    private void LateUpdate()
    {
        // Keep UI stuck above duck.
        if (staminaRoot != null && staminaRoot.activeSelf)
        {
            staminaRoot.transform.position = transform.position + staminaBarOffset;
        }
    }

    private void OnDestroy()
    {
        // Ensure we don't leak runtime-generated UI.
        if (staminaRoot != null)
        {
            Destroy(staminaRoot);
            staminaRoot = null;
            staminaFillImage = null;
        }
    }

    public void ApplyTierColor(DuckStats.Tier tier)
    {
        Color c = GetTierColor(tier);

        if (uiImage != null)
        {
            uiImage.color = c;
        }

        if (worldRenderer != null)
        {
            // Use material instance to avoid changing shared material.
            var mat = worldRenderer.material;
            if (mat != null) mat.color = c;
        }
    }

    public void InitializeStaminaBar()
    {
        if (staminaRoot != null) return;

        // Create root
        staminaRoot = new GameObject("StaminaBar");
        staminaRoot.transform.SetParent(null, worldPositionStays: true);
        staminaRoot.transform.position = transform.position + staminaBarOffset;
        staminaRoot.transform.rotation = Quaternion.identity;

        // World-space canvas
        var canvas = staminaRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1000;

        // Needed for UI
        staminaRoot.AddComponent<CanvasScaler>();
        staminaRoot.AddComponent<GraphicRaycaster>();

        var canvasRt = staminaRoot.GetComponent<RectTransform>();
        canvasRt.sizeDelta = staminaBarSize;

        // Background
        var bgGo = new GameObject("BG");
        bgGo.transform.SetParent(staminaRoot.transform, false);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // Fill
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(staminaRoot.transform, false);
        staminaFillImage = fillGo.AddComponent<Image>();
        staminaFillImage.type = Image.Type.Filled;
        staminaFillImage.fillMethod = Image.FillMethod.Horizontal;
        staminaFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        staminaFillImage.fillAmount = 1f;
        staminaFillImage.color = GetStaminaColor(1f);

        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(staminaBarBorder, staminaBarBorder);
        fillRt.offsetMax = new Vector2(-staminaBarBorder, -staminaBarBorder);
    }

    public void UpdateStaminaBar(float stamina01)
    {
        if (!showStaminaBar) return;
        if (staminaRoot == null) InitializeStaminaBar();
        if (staminaRoot != null && !staminaRoot.activeSelf) return;
        if (staminaFillImage == null) return;

        stamina01 = Mathf.Clamp01(stamina01);
        staminaFillImage.fillAmount = stamina01;
        staminaFillImage.color = GetStaminaColor(stamina01);
    }

    /// <summary>
    /// Toggle stamina bar visibility for this duck.
    /// When hidden, the root GameObject is disabled for performance.
    /// </summary>
    public void SetStaminaBarVisible(bool visible)
    {
        showStaminaBar = visible;
        if (staminaRoot == null) InitializeStaminaBar();
        if (staminaRoot != null) staminaRoot.SetActive(visible);
    }

    /// <summary>
    /// Toggle stamina bars for all ducks currently active in the scene.
    /// </summary>
    public static void SetAllStaminaBarsVisible(bool visible)
    {
        foreach (var visualizer in FindObjectsOfType<DuckVisualizer>())
        {
            visualizer.SetStaminaBarVisible(visible);
        }
    }

    private static Color GetStaminaColor(float stamina01)
    {
        // Green -> Yellow -> Red
        stamina01 = Mathf.Clamp01(stamina01);
        if (stamina01 >= 0.5f)
        {
            float t = Mathf.InverseLerp(0.5f, 1.0f, stamina01);
            return Color.Lerp(Color.yellow, Color.green, t);
        }
        else
        {
            float t = Mathf.InverseLerp(0.0f, 0.5f, stamina01);
            return Color.Lerp(Color.red, Color.yellow, t);
        }
    }

    private static Color GetTierColor(DuckStats.Tier tier)
    {
        switch (tier)
        {
            case DuckStats.Tier.Slow: return Color.gray;
            case DuckStats.Tier.Average: return Color.white;
            case DuckStats.Tier.Fast: return Color.yellow;
            case DuckStats.Tier.VeryFast: return Color.red;
            default: return Color.white;
        }
    }
}
