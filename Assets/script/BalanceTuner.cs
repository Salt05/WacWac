using UnityEngine;

/// <summary>
/// Centralized balancing settings editable in the Inspector.
/// Phase: Polish/Optimization.
///
/// Usage: place one BalanceTuner in the race scene.
/// Code can access BalanceTuner.Instance.Settings.
/// </summary>
public sealed class BalanceTuner : MonoBehaviour
{
    public static BalanceTuner Instance { get; private set; }

    [Header("Visualization Settings")]
    public bool defaultShowStaminaBars = true;

    [System.Serializable]
    public class BalanceSettings
    {
        [Header("Tier Distribution (percent, should sum to 100)")]
        [Range(0, 100)] public int slowPercent = 20;
        [Range(0, 100)] public int averagePercent = 50;
        [Range(0, 100)] public int fastPercent = 25;
        [Range(0, 100)] public int veryFastPercent = 5;

        [Header("Tier Multipliers")]
        public float slowMultiplier = 0.7f;
        public float averageMultiplier = 1.0f;
        public float fastMultiplier = 1.3f;
        public float veryFastMultiplier = 1.6f;

        [Header("Personality Distribution (percent, should sum to 100)")]
        [Range(0, 100)] public int steadyPercent = 20;
        [Range(0, 100)] public int erraticPercent = 20;
        [Range(0, 100)] public int sprinterPercent = 20;
        [Range(0, 100)] public int starterPercent = 20;
        [Range(0, 100)] public int chokerPercent = 20;

        [Header("Rubber-banding")]
        [Range(0.7f, 1.0f)] public float leaderNerfMin = 0.90f;
        [Range(0.7f, 1.0f)] public float leaderNerfMax = 0.95f;
        [Range(1.0f, 1.3f)] public float backBoostMin = 1.05f;
        [Range(1.0f, 1.3f)] public float backBoostMax = 1.10f;

        [Header("Stamina")]
        [Tooltip("Multiplier applied to drain=dt*speed*tierStaminaMultiplier")]
        public float staminaDrainRate = 1.0f;
        public float staminaRegenRate = 0.015f;
        public float lowStaminaThreshold = 0.3f;

        [Header("Comeback Mechanic")]
        public float comebackDuration = 3f;
        public float comebackMultiplier = 1.2f;
        [Tooltip("Seconds window to detect rank dropping")]
        public float rankDropWindowSeconds = 5f;

        [Header("Momentum")]
        [Range(0f, 0.5f)] public float momentumMaxBonus = 0.15f;
        public float momentumGainPerSecond = 0.35f;

        [Header("Speed Transition")]
        [Tooltip("transitionTime = effectiveRandomIntervalC * transitionTimeRatio")]
        [Range(0.05f, 1.0f)]
        public float transitionTimeRatio = 0.333f;
    }

    public BalanceSettings Settings = new BalanceSettings();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
}
