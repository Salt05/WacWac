using System;
using UnityEngine;

/// <summary>
/// Phase 1: duck "personality" + tier + stamina + consistency container.
/// Pure data + helper functions so DuckMover can stay mostly unchanged.
/// </summary>
[Serializable]
public sealed class DuckStats
{
    public enum Tier
    {
        Slow = 0,
        Average = 1,
        Fast = 2,
        VeryFast = 3
    }

    public enum Personality
    {
        Steady = 0,
        Erratic = 1,
        Sprinter = 2,
        Starter = 3,
        Choker = 4
    }

    [Header("Identity")]
    public int duckIndex;

    [Header("Core")]
    public Tier tier = Tier.Average;
    public Personality personality = Personality.Steady;

    [Range(0f, 1f)]
    public float stamina01 = 1f;

    /// <summary>
    /// 0..1 where 1 = very stable, 0 = very volatile.
    /// </summary>
    [Range(0f, 1f)]
    public float consistency01 = 0.6f;

    public float TierBaseSpeedMultiplier
    {
        get
        {
            var s = BalanceTuner.Instance != null ? BalanceTuner.Instance.Settings : null;
            if (s != null)
            {
                switch (tier)
                {
                    case Tier.Slow: return s.slowMultiplier;
                    case Tier.Average: return s.averageMultiplier;
                    case Tier.Fast: return s.fastMultiplier;
                    case Tier.VeryFast: return s.veryFastMultiplier;
                }
            }

            switch (tier)
            {
                case Tier.Slow: return 0.7f;
                case Tier.Average: return 1.0f;
                case Tier.Fast: return 1.3f;
                case Tier.VeryFast: return 1.6f;
                default: return 1.0f;
            }
        }
    }

    public float TierStaminaMultiplier
    {
        get
        {
            // Light weighting for now (can be tuned later).
            switch (tier)
            {
                case Tier.Slow: return 0.85f;
                case Tier.Average: return 1.0f;
                case Tier.Fast: return 1.15f;
                case Tier.VeryFast: return 1.30f;
                default: return 1.0f;
            }
        }
    }

    /// <summary>
    /// When stamina is low: max speed should be reduced.
    /// Returns a multiplier applied to current max speed.
    /// </summary>
    public float GetStaminaLimitedMaxSpeedMultiplier()
    {
        float threshold = 0.30f;
        var s = BalanceTuner.Instance != null ? BalanceTuner.Instance.Settings : null;
        if (s != null) threshold = Mathf.Clamp01(s.lowStaminaThreshold);

        if (stamina01 >= threshold) return 1f;

        // Map threshold -> 1.0, 0.00 -> 0.70
        float t = Mathf.InverseLerp(0f, Mathf.Max(0.0001f, threshold), stamina01);
        return Mathf.Lerp(0.70f, 1.0f, t);
    }

    /// <summary>
    /// When stamina is low: variability should increase.
    /// Returns a multiplier applied to noise amplitude.
    /// </summary>
    public float GetLowStaminaVariabilityMultiplier()
    {
        float threshold = 0.30f;
        var s = BalanceTuner.Instance != null ? BalanceTuner.Instance.Settings : null;
        if (s != null) threshold = Mathf.Clamp01(s.lowStaminaThreshold);

        if (stamina01 >= threshold) return 1f;

        // Map threshold -> 1.0, 0.00 -> 1.6
        float t = Mathf.InverseLerp(0f, Mathf.Max(0.0001f, threshold), stamina01);
        return Mathf.Lerp(1.6f, 1.0f, t);
    }
}