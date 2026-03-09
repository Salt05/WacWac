using UnityEngine;

/// <summary>
/// DuckMover (legacy compatibility wrapper).
///
/// In the new Progress-based design, movement is fully handled by DuckBrain.
/// DuckMover now only provides:
/// - Accessors for world X (for LeaderboardUI, DuckSystemTester).
/// - Simple DuckStats container (tier/personality) for logging / analysis.
/// - Optional helpers that keep old public APIs but no longer drive motion.
/// </summary>
public class DuckMover : MonoBehaviour
{

    [Header("Stats (for analysis / UI only)")]
    [SerializeField] private DuckStats stats = new DuckStats();

    /// <summary>Optional link to the new Progress-based brain component.</summary>
    public DuckBrain brain;

    // cached index in hierarchy (lane index)
    private int duckIndex = -1;

    // initial transform for ResetToInitial
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    // legacy finish-time tracking (used only by analysis helpers)
    private float timeReachedFinish = -1f;

    private void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        duckIndex = transform.GetSiblingIndex();
        if (stats == null) stats = new DuckStats();
        stats.duckIndex = duckIndex;

        // assign a stable-but-simple random tier/personality for distribution tests
        // (no gameplay effect in the new Progress-based system)
        if (Application.isPlaying)
        {
            float t = Random.value;
            if (t < 0.20f) stats.tier = DuckStats.Tier.Slow;
            else if (t < 0.70f) stats.tier = DuckStats.Tier.Average;
            else if (t < 0.95f) stats.tier = DuckStats.Tier.Fast;
            else stats.tier = DuckStats.Tier.VeryFast;

            int p = Random.Range(0, 4);
            stats.personality = (DuckStats.Personality)p;
        }

        if (brain == null) brain = GetComponent<DuckBrain>();
    }

    /// <summary>
    /// World X helper used by LeaderboardUI and testers.
    /// </summary>
    public float GetWorldX() => transform.position.x;

    /// <summary>
    /// Tier used by DuckSystemTester for distribution logging.
    /// </summary>
    public DuckStats.Tier GetTier() => stats != null ? stats.tier : DuckStats.Tier.Average;

    /// <summary>
    /// Personality used by DuckSystemTester for distribution logging.
    /// </summary>
    public DuckStats.Personality GetPersonality() => stats != null ? stats.personality : DuckStats.Personality.Steady;

    /// <summary>
    /// Stamina accessor kept for potential UI usage. In the new system this is
    /// purely cosmetic and does not affect movement.
    /// </summary>
    public float GetStamina01() => stats != null ? stats.stamina01 : 1f;

    public int GetDuckIndex() => duckIndex;

    public float GetTimeReachedFinish() => timeReachedFinish;

    /// <summary>
    /// Legacy helper: mark this duck as having reached finish "now".
    /// Used only for analytics / debugging.
    /// </summary>
    public void ForceMarkReachedFinishNow()
    {
        if (timeReachedFinish >= 0f) return;
        timeReachedFinish = Time.time;
    }

    /// <summary>
    /// Reset transform to the initial pose captured at Awake.
    /// </summary>
    public void ResetToInitial()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        timeReachedFinish = -1f;
    }

    /// <summary>
    /// Legacy API compatibility stubs.
    /// These are no-ops or simple helpers so older UI / tools won't break.
    /// Movement is owned by DuckBrain and RaceController now.
    /// </summary>
    public float GetEffectiveStopDuckTimeD(float globalD) => globalD;

    public bool IsSprinting() => false;

    public float GetMaxPosX() => transform.position.x;

    public bool IsSlowingForBound() => false;

    public void NotifyApproachingBound(float decelTime) { }

    public void StartSprintToWorldX(float targetWorldX, float totalTime)
    {
        // In the new system, sprint behavior is encoded inside DuckBrain via Progress P.
        // For compatibility we can simply snap towards the target X if requested.
        if (float.IsNaN(targetWorldX)) return;
        var pos = transform.position;
        pos.x = targetWorldX;
        transform.position = pos;
        ForceMarkReachedFinishNow();
    }

    public void StopSprint() { }
}
