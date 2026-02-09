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
