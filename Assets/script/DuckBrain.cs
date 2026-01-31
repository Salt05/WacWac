using System;
using UnityEngine;

public sealed class DuckBrain
{
    private System.Random rng;

    // cycle
    private float nextDecisionTime;

    // momentum
    public float momentum01 { get; private set; }
    private const float MomentumMaxBonus = 0.15f;
    private const float MomentumGainPerSecond = 0.35f;

    // comeback mechanic
    private const float RankWindowSeconds = 5f;
    private const float ComebackBoostSeconds = 3f;
    private const float ComebackBoostMult = 1.20f;

    private int lastRank;
    private float lastRankSampleTime;
    private float rankDropWindowStartTime;
    private int rankAtWindowStart;
    private float comebackUntilTime;

    public void Reset(System.Random rng, float now)
    {
        this.rng = rng;
        nextDecisionTime = now;
        momentum01 = 0f;

        lastRank = int.MaxValue;
        lastRankSampleTime = now;

        rankDropWindowStartTime = now;
        rankAtWindowStart = int.MaxValue;

        comebackUntilTime = 0f;
    }

    private BalanceTuner.BalanceSettings SettingsOrNull => BalanceTuner.Instance != null ? BalanceTuner.Instance.Settings : null;

    public float GetEffectiveRandomInterval(float baseC, DuckStats.Personality personality)
    {
        switch (personality)
        {
            case DuckStats.Personality.Erratic: return baseC * 0.7f;
            case DuckStats.Personality.Steady: return baseC * 1.5f;
            default: return baseC;
        }
    }

    /// <summary>
    /// transitionTime = effectiveC * transitionTimeRatio.
    /// For effectiveC <= 0: return 0 (instant) to avoid per-frame partial transitions.
    /// </summary>
    public float GetSpeedTransitionTime(float effectiveC)
    {
        if (effectiveC <= 0f) return 0f;

        var s = SettingsOrNull;
        float ratio = s != null ? Mathf.Clamp(s.transitionTimeRatio, 0.05f, 1.0f) : 0.333f;
        return Mathf.Max(0f, effectiveC * ratio);
    }

    public float GetEffectiveStopDuckTimeD(float globalD, DuckStats.Personality personality)
    {
        switch (personality)
        {
            case DuckStats.Personality.Sprinter: return globalD * 1.5f;
            case DuckStats.Personality.Starter: return globalD * 0.7f;
            case DuckStats.Personality.Steady: return globalD * 1.2f;
            case DuckStats.Personality.Erratic: return globalD * 0.8f;
            case DuckStats.Personality.Choker: return globalD;
            default: return globalD;
        }
    }

    public void UpdateMomentum(float dt, float currentSpeed)
    {
        float gain = 0.35f;
        var s = SettingsOrNull;
        if (s != null) gain = Mathf.Max(0f, s.momentumGainPerSecond);

        if (Mathf.Abs(currentSpeed) > 0.05f)
            momentum01 = Mathf.Clamp01(momentum01 + gain * dt);
        else
            momentum01 = Mathf.Clamp01(momentum01 - (gain * 0.5f) * dt);
    }

    public void ResetMomentum() => momentum01 = 0f;

    /// <summary>
    /// Track if the duck is "dropping rank" over a 5-second window.
    /// If rank increased (worse) compared to window start, trigger a 3-second speed boost.
    /// </summary>
    public void UpdateComeback(float now, int currentRank)
    {
        if (currentRank == int.MaxValue) return;

        float window = 5f;
        var s = SettingsOrNull;
        if (s != null) window = Mathf.Max(0.1f, s.rankDropWindowSeconds);

        // reset window every 5 seconds
        if (now - rankDropWindowStartTime > window)
        {
            rankDropWindowStartTime = now;
            rankAtWindowStart = currentRank;
        }

        // If rank got worse vs window start, arm comeback.
        if (rankAtWindowStart != int.MaxValue && currentRank > rankAtWindowStart)
        {
            float dur = 3f;
            if (s != null) dur = Mathf.Max(0f, s.comebackDuration);
            comebackUntilTime = Mathf.Max(comebackUntilTime, now + dur);
        }

        lastRank = currentRank;
        lastRankSampleTime = now;
    }

    public float GetComebackMultiplier(float now)
    {
        var s = SettingsOrNull;
        float mult = s != null ? Mathf.Max(1f, s.comebackMultiplier) : 1.20f;
        return now < comebackUntilTime ? mult : 1f;
    }

    public float GetRubberBandMultiplier(int rank, int total)
    {
        if (rank == int.MaxValue || total <= 0) return 1f;

        float percentile = (float)rank / (float)total;

        var s = SettingsOrNull;
        float leaderMin = s != null ? s.leaderNerfMin : 0.90f;
        float leaderMax = s != null ? s.leaderNerfMax : 0.95f;
        float backMin = s != null ? s.backBoostMin : 1.10f;
        float backMax = s != null ? s.backBoostMax : 1.05f;

        if (percentile <= 0.1f)
        {
            float t = Mathf.Clamp01(percentile / 0.1f);
            return Mathf.Lerp(leaderMin, leaderMax, t);
        }

        if (percentile >= 0.7f)
        {
            float t = Mathf.Clamp01((percentile - 0.7f) / 0.3f);
            return Mathf.Lerp(backMin, backMax, t);
        }

        return 1.0f;
    }

    public (int direction, float targetSpeed, bool directionChanged) DecideTarget(
        float now,
        DuckStats stats,
        RaceController rc,
        int currentDirection,
        float speedMinA,
        float speedMaxB,
        float randomIntervalC,
        int rank,
        int totalRunners)
    {
        float effectiveC = GetEffectiveRandomInterval(randomIntervalC, stats.personality);
        bool shouldDecide = effectiveC <= 0f || now >= nextDecisionTime;
        if (!shouldDecide)
            return (currentDirection, float.NaN, false);

        nextDecisionTime = effectiveC <= 0f ? now : (now + effectiveC);

        int newDirection = NextSign();

        float baseMin = speedMinA * stats.TierBaseSpeedMultiplier;
        float baseMax = speedMaxB * stats.TierBaseSpeedMultiplier;

        baseMax *= stats.GetStaminaLimitedMaxSpeedMultiplier();
        if (baseMax < baseMin) baseMax = baseMin;

        float desired = NextFloat(baseMin, baseMax);

        float progress01 = 0f;
        if (rc != null && rc.totalRaceTime > 0f)
            progress01 = Mathf.Clamp01(1f - (rc.remainingTime / rc.totalRaceTime));

        float noiseAmp = Mathf.Lerp(0.60f, 0.10f, stats.consistency01) * stats.GetLowStaminaVariabilityMultiplier();

        // personality shaping
        switch (stats.personality)
        {
            case DuckStats.Personality.Steady:
                noiseAmp *= 0.6f;
                if (rng != null && rng.NextDouble() < 0.75) newDirection = currentDirection;
                break;

            case DuckStats.Personality.Erratic:
                noiseAmp *= 1.5f;
                if (rng != null && rng.NextDouble() < 0.60) newDirection = -currentDirection;
                break;

            case DuckStats.Personality.Sprinter:
                desired *= Mathf.Lerp(0.95f, 1.15f, progress01);
                break;

            case DuckStats.Personality.Starter:
                desired *= Mathf.Lerp(1.15f, 0.95f, progress01);
                break;

            case DuckStats.Personality.Choker:
                // If leading, slow; if chasing, speed up.
                if (rank == 1) desired *= 0.92f;
                else if (rank != int.MaxValue) desired *= 1.08f;
                break;
        }

        float noise = NextFloat(-noiseAmp, noiseAmp);
        float speed = Mathf.Clamp(desired * (1f + noise), baseMin, baseMax);

        bool changed = newDirection != currentDirection;
        return (newDirection, speed, changed);
    }

    public float ComposeSpeedMultiplier(float now, int rank, int total)
    {
        var s = SettingsOrNull;
        float maxBonus = s != null ? Mathf.Clamp(s.momentumMaxBonus, 0f, 1f) : 0.15f;

        float momentumBonus = 1f + (momentum01 * maxBonus);
        float rubber = GetRubberBandMultiplier(rank, total);
        float comeback = GetComebackMultiplier(now);
        return momentumBonus * rubber * comeback;
    }

    private float NextFloat(float minInclusive, float maxInclusive)
    {
        if (rng == null) return UnityEngine.Random.Range(minInclusive, maxInclusive);
        float t = (float)rng.NextDouble();
        return Mathf.Lerp(minInclusive, maxInclusive, t);
    }

    private int NextSign()
    {
        if (rng == null) return (UnityEngine.Random.value < 0.5f) ? -1 : +1;
        return rng.NextDouble() < 0.5 ? -1 : +1;
    }
}
