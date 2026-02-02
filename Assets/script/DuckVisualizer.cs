using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase 2: debug visualization.
/// - Tier tint (Image/Renderer)
/// - Runtime-generated stamina bar (world-space canvas)
/// - Visual-only bobbing effect (local Y axis)
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

    // --- Visual bobbing (visual-only) ---
    [Header("Visual Bobbing")]
    [Tooltip("Total bob height in local units (peak-to-peak).")]
    [SerializeField] private float bobHeight = 0.15f;

    [Tooltip("Duration of a full up-and-down cycle in seconds.")]
    [SerializeField] private float bobDuration = 2.0f;

    // internal bobbing state
    private Transform bobbingTarget;
    private Vector3 bobbingBaseLocalPos;

    // per-instance deterministic phase offset (seconds)
    private float bobPhaseOffset = 0f;

    private GameObject staminaRoot;
    private Image staminaFillImage;

    private void Awake()
    {
        if (uiImage == null) uiImage = GetComponentInChildren<Image>();
        if (worldRenderer == null) worldRenderer = GetComponentInChildren<Renderer>();

        // Choose a transform to apply bobbing to. Prefer the renderer or UI element so
        // we don't accidentally move the logical root that movement/physics rely on.
        if (uiImage != null)
            bobbingTarget = uiImage.transform;
        else if (worldRenderer != null)
            bobbingTarget = worldRenderer.transform;
        else if (transform.childCount > 0)
            bobbingTarget = transform.GetChild(0);
        else
            bobbingTarget = transform; // fallback

        bobbingBaseLocalPos = bobbingTarget != null ? bobbingTarget.localPosition : Vector3.zero;

        // Create bar now (so toggling doesn't allocate later), but respect showStaminaBar.
        InitializeStaminaBar();
        SetStaminaBarVisible(showStaminaBar);

        // Compute a per-instance deterministic phase offset so ducks don't bob in sync.
        // Use System.Random seeded with the instance ID to avoid modifying Unity's global RNG.
        try
        {
            var rnd = new System.Random(gameObject.GetInstanceID());
            // Uniform in [0, bobDuration)
            bobPhaseOffset = (float)(rnd.NextDouble() * (double)bobDuration);
        }
        catch
        {
            bobPhaseOffset = 0f;
        }
    }

    private void LateUpdate()
    {
        // Keep UI stuck above duck.
        if (staminaRoot != null && staminaRoot.activeSelf)
        {
            staminaRoot.transform.position = transform.position + staminaBarOffset;
        }

        // Apply smooth visual-only bobbing on local Y axis.
        if (bobbingTarget != null && bobHeight > 0f && bobDuration > 0f)
        {
            // PingPong with length = bobDuration/2 gives a triangular wave with full period = bobDuration
            float halfPeriod = bobDuration * 0.5f;
            if (halfPeriod > 0f)
            {
                // Apply per-instance phase offset so each duck starts at a different point in the cycle.
                float timeWithOffset = Time.time + bobPhaseOffset;
                float phase = Mathf.PingPong(timeWithOffset, halfPeriod) / halfPeriod; // 0 -> 1 -> 0 over full cycle
                float eased = EaseInOutCubic(phase); // cubic ease-in-out (slow at ends, faster in middle)

                float amplitude = bobHeight * 0.5f; // peak offset magnitude
                float offsetY = (eased * 2f - 1f) * amplitude; // map 0..1..0 -> -1..1..-1 then scale

                Vector3 lp = bobbingBaseLocalPos;
                lp.y += offsetY;
                bobbingTarget.localPosition = lp;
            }
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

    // Cubic ease-in-out: slow at ends, faster in middle.
    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        if (t < 0.5f)
            return 4f * t * t * t;
        else
            return 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
