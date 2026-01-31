using UnityEngine;

public class DuckMover : MonoBehaviour
{
    [HideInInspector] public RaceController raceController;

    // runtime params (provided by RaceController when race starts)
    private float speedMinA;
    private float speedMaxB;
    private float randomIntervalC;
    private float stopDuckTimeD;
    private float minPosX;
    private float maxPosX;

    // behavior cycle
    private float nextRandomTime; // (kept for backward compat; controlled by DuckBrain now)

    // initial transform for Clear
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    // --- Sprint mode (leader only) ---
    private bool isSprint = false;
    private float sprintTargetWorldX = 0f;
    private float sprintTotalTime = 0f;
    private float sprintTimeRemaining = 0f;
    private float sprintAccel = 0f;
    private float sprintFinalSpeed = 0f;

    // --- RNG + stats ---
    private System.Random rng;
    private DuckStats stats;
    private int duckIndex = -1;

    // --- Phase 2: components (C# classes) ---
    private readonly DuckMovement movement = new DuckMovement();
    private readonly DuckBrain brain = new DuckBrain();
    private DuckVisualizer visualizer;

    // --- LOD minimal tick throttling ---
    private float minimalTickAccumulator;
    private const float MinimalTickIntervalDefault = 0.2f;

    public void Initialize(RaceController rc)
    {
        raceController = rc;

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        duckIndex = transform.GetSiblingIndex();

        if (stats == null)
        {
            stats = new DuckStats();
            stats.duckIndex = duckIndex;
        }

        visualizer = GetComponent<DuckVisualizer>();
        if (visualizer == null) visualizer = gameObject.AddComponent<DuckVisualizer>();

        // Apply default stamina bar visibility from BalanceTuner (if any).
        if (BalanceTuner.Instance != null && visualizer != null)
        {
            visualizer.showStaminaBar = BalanceTuner.Instance.defaultShowStaminaBars;
            visualizer.SetStaminaBarVisible(visualizer.showStaminaBar);
        }

        ReseedRng();

        movement.Reset();
        nextRandomTime = Time.time;
        enabled = true;
    }

    private void ReseedRng()
    {
        int sessionSeed = raceController != null ? raceController.raceSessionSeed : 0;
        int seed = HashSeed(sessionSeed, duckIndex);

        rng = new System.Random(seed);

        RollInitialStats();

        brain.Reset(rng, Time.time);

        // apply tier color for debugging
        if (visualizer != null) visualizer.ApplyTierColor(stats.tier);
    }

    private static int HashSeed(int raceSessionSeed, int duckIndex)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + raceSessionSeed;
            h = h * 31 + duckIndex;
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);
            return h;
        }
    }

    private void RollInitialStats()
    {
        if (stats == null) return;

        stats.duckIndex = duckIndex;
        stats.stamina01 = 1f;

        double t = rng != null ? rng.NextDouble() : 0.5;
        if (t < 0.20) stats.tier = DuckStats.Tier.Slow;
        else if (t < 0.70) stats.tier = DuckStats.Tier.Average;
        else if (t < 0.95) stats.tier = DuckStats.Tier.Fast;
        else stats.tier = DuckStats.Tier.VeryFast;

        int p = rng != null ? rng.Next(0, 5) : 0;
        stats.personality = (DuckStats.Personality)p;

        float baseC = NextFloat(0.35f, 0.85f);
        float tweak = 0f;
        switch (stats.tier)
        {
            case DuckStats.Tier.Slow: tweak = +0.08f; break;
            case DuckStats.Tier.Average: tweak = +0.03f; break;
            case DuckStats.Tier.Fast: tweak = -0.02f; break;
            case DuckStats.Tier.VeryFast: tweak = -0.07f; break;
        }
        stats.consistency01 = Mathf.Clamp01(baseC + tweak);

        if (stats.personality == DuckStats.Personality.Steady) stats.consistency01 = Mathf.Clamp01(stats.consistency01 + 0.15f);
        if (stats.personality == DuckStats.Personality.Erratic) stats.consistency01 = Mathf.Clamp01(stats.consistency01 - 0.25f);
    }

    private float NextFloat(float minInclusive, float maxInclusive)
    {
        if (rng == null) return Random.Range(minInclusive, maxInclusive);
        float t = (float)rng.NextDouble();
        return Mathf.Lerp(minInclusive, maxInclusive, t);
    }

    public void ApplyRaceParams(float speedMinA, float speedMaxB, float randomIntervalC, float stopDuckTimeD, float minPosX, float maxPosX)
    {
        this.speedMinA = speedMinA;
        this.speedMaxB = speedMaxB;
        this.randomIntervalC = randomIntervalC;
        this.stopDuckTimeD = stopDuckTimeD;
        this.minPosX = minPosX;
        this.maxPosX = maxPosX;

        movement.SetBounds(minPosX, maxPosX);

        // reseed per race to be deterministic per session
        ReseedRng();

        if (nextRandomTime < Time.time) nextRandomTime = Time.time;
    }

    private void Update()
    {
        // Default path (when not batch-updated): keep previous behavior.
        Tick(Time.deltaTime, Time.time);
    }

    public void Tick(float deltaTime, float now)
    {
        // Full update.
        TickInternal(deltaTime, now, TickMode.Full);
    }

    public void TickSimplified(float deltaTime, float now)
    {
        // Medium update: no momentum/comeback.
        TickInternal(deltaTime, now, TickMode.Simplified);
    }

    public void TickMinimal(float deltaTime)
    {
        float dt = deltaTime;
        if (raceController == null) return;
        if (!raceController.IsRunning()) return;

        // Accumulate time; only do the cheap "decision" every interval.
        minimalTickAccumulator += dt;

        float interval = MinimalTickIntervalDefault;
        var balance = BalanceTuner.Instance != null ? BalanceTuner.Instance.Settings : null;
        // allow designers to get a bit more/less updates by piggybacking on ranking interval if desired
        if (raceController != null && raceController.rankingUpdateInterval > 0f)
            interval = Mathf.Clamp(raceController.rankingUpdateInterval * 2f, 0.1f, 0.5f);

        float now = Time.time;

        if (minimalTickAccumulator >= interval)
        {
            minimalTickAccumulator = 0f;

            float tierMult = stats != null ? stats.TierBaseSpeedMultiplier : 1f;
            float baseSpeed = Mathf.Lerp(speedMinA, speedMaxB, 0.5f) * tierMult;

            float noise = 0f;
            if (rng != null)
                noise = Mathf.Lerp(-0.08f, 0.08f, (float)rng.NextDouble());
            else
                noise = Random.Range(-0.08f, 0.08f);

            float target = Mathf.Max(0f, baseSpeed * (1f + noise));

            bool flip = false;
            if (rng != null) flip = rng.NextDouble() < 0.05;
            else flip = Random.value < 0.05f;

            int dir = movement.direction;
            if (flip) dir = -dir;

            // For minimal: use a short transition for visual smoothness.
            float transitionTime = Mathf.Max(0f, interval / 3f);
            movement.BeginSpeedTransition(dir, target, transitionTime, now);
            movement.GuardDirectionAtBounds(transform.position.x);
        }

        movement.StepSpeed(now);
        float x = movement.StepPositionX(transform.position.x, dt);
        Vector3 pos = transform.position;
        pos.x = x;
        transform.position = pos;
    }

    private enum TickMode { Full, Simplified }

    private void TickInternal(float deltaTime, float now, TickMode mode)
    {
        if (raceController == null) return;
        if (!raceController.IsRunning()) return;

        float dt = deltaTime;

        float effectiveD = stats != null ? brain.GetEffectiveStopDuckTimeD(stopDuckTimeD, stats.personality) : stopDuckTimeD;
        bool inSprintPhase = raceController.remainingTime <= effectiveD;

        UpdateStamina(dt);

        if (visualizer != null) visualizer.UpdateStaminaBar(GetStamina01());

        if (inSprintPhase && isSprint)
        {
            StepSprint(dt);
            return;
        }

        if (inSprintPhase && !isSprint)
        {
            // freeze: keep current signed speed, just integrate
            movement.StepSpeed(now);
            float xLock = movement.StepPositionX(transform.position.x, dt);
            Vector3 pLock = transform.position;
            pLock.x = xLock;
            transform.position = pLock;
            return;
        }

        int rank = raceController.GetRankOf(this);
        int total = raceController.GetRunnerCount();

        if (mode == TickMode.Full)
        {
            brain.UpdateMomentum(dt, movement.currentSpeed);
            brain.UpdateComeback(now, rank);
        }

        float effectiveC = brain.GetEffectiveRandomInterval(randomIntervalC, stats != null ? stats.personality : DuckStats.Personality.Steady);
        float transitionTime = brain.GetSpeedTransitionTime(effectiveC);

        var decision = brain.DecideTarget(
            now,
            stats,
            raceController,
            movement.direction,
            speedMinA,
            speedMaxB,
            randomIntervalC,
            rank,
            total);

        if (!float.IsNaN(decision.targetSpeed))
        {
            float mult;
            if (mode == TickMode.Full)
                mult = brain.ComposeSpeedMultiplier(now, rank, total);
            else
                mult = brain.GetRubberBandMultiplier(rank, total);

            float desiredSpeed = decision.targetSpeed * mult;

            // Begin linear transition (two-phase if direction flips).
            movement.BeginSpeedTransition(decision.direction, desiredSpeed, transitionTime, now);
            movement.GuardDirectionAtBounds(transform.position.x);
            movement.ApplyAntiStrongReversal(1.5f);

            if (decision.directionChanged && mode == TickMode.Full)
                brain.ResetMomentum();
        }

        // Step speed based on absolute time and then integrate position.
        movement.StepSpeed(now);
        float x = movement.StepPositionX(transform.position.x, dt);

        Vector3 pos = transform.position;
        pos.x = x;
        transform.position = pos;
    }

    private void UpdateStamina(float dt)
    {
        if (stats == null) return;
        if (dt <= 0f) return;

        var s = BalanceTuner.Instance != null ? BalanceTuner.Instance.Settings : null;
        float drainRate = s != null ? Mathf.Max(0f, s.staminaDrainRate) : 1.0f;
        float regenRate = s != null ? Mathf.Max(0f, s.staminaRegenRate) : 0.015f;

        float drain = dt * Mathf.Max(0f, movement.currentSpeed) * stats.TierStaminaMultiplier * drainRate;

        if (Mathf.Abs(movement.currentSpeed) < 0.25f)
        {
            float recover = dt * regenRate;
            stats.stamina01 = Mathf.Clamp01(stats.stamina01 - drain + recover);
        }
        else
        {
            stats.stamina01 = Mathf.Clamp01(stats.stamina01 - drain);
        }
    }

    private void StepSprint(float dt)
    {
        if (sprintTimeRemaining <= 0f)
        {
            Vector3 p0 = transform.position;
            p0.x = sprintTargetWorldX;
            transform.position = p0;
            isSprint = false;
            movement.Reset();
            return;
        }

        // Integrate sprint speed using explicit acceleration (independent of brain transitions)
        float v = movement.currentSpeed + sprintAccel * dt;
        v = Mathf.Max(0f, v);

        int dir = (sprintTargetWorldX >= transform.position.x) ? +1 : -1;
        // Store sprint speed in movement as signed speed so minimal/regular movement stays consistent.
        movement.BeginSpeedTransition(dir, v, 0f, Time.time); // immediate set

        Vector3 p = transform.position;
        p.x += movement.currentSpeedSigned * dt;

        // clamp within lane bounds
        float clampedX = Mathf.Clamp(p.x, movement.minPosX, movement.maxPosX);
        p.x = clampedX;

        // check overshoot / reached
        if ((dir > 0 && p.x >= sprintTargetWorldX) || (dir < 0 && p.x <= sprintTargetWorldX))
        {
            p.x = sprintTargetWorldX;
            isSprint = false;
            movement.Reset();
        }

        transform.position = p;

        sprintTimeRemaining -= dt;
        if (sprintTimeRemaining <= 0f && isSprint)
        {
            Vector3 pp = transform.position;
            pp.x = sprintTargetWorldX;
            transform.position = pp;
            isSprint = false;
            movement.Reset();
        }
    }

    public void ResetToInitial(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;

        movement.Reset();
        isSprint = false;
        sprintTimeRemaining = 0f;

        if (stats != null) stats.stamina01 = 1f;
        minimalTickAccumulator = 0f;
    }

    public float GetWorldX() => transform.position.x;

    public float GetEffectiveStopDuckTimeD(float globalD)
    {
        return stats != null ? brain.GetEffectiveStopDuckTimeD(globalD, stats.personality) : globalD;
    }

    public bool IsSprinting() => isSprint;

    public float GetStamina01() => stats != null ? stats.stamina01 : 1f;

    public DuckStats.Tier GetTier() => stats != null ? stats.tier : DuckStats.Tier.Average;

    public DuckStats.Personality GetPersonality() => stats != null ? stats.personality : DuckStats.Personality.Steady;

    public void StartSprintToWorldX(float targetWorldX, float totalTime)
    {
        if (float.IsNaN(targetWorldX) || totalTime <= 0f) return;

        // Clamp target within bounds to avoid sprinting outside the lane.
        float clampedTarget = Mathf.Clamp(targetWorldX, movement.minPosX, movement.maxPosX);

        sprintTargetWorldX = clampedTarget;
        sprintTotalTime = Mathf.Max(0.0001f, totalTime);
        sprintTimeRemaining = sprintTotalTime;
        isSprint = true;

        float x = transform.position.x;
        float distance = Mathf.Abs(sprintTargetWorldX - x);

        if (distance <= 0.0001f)
        {
            isSprint = false;
            movement.Reset();
            return;
        }

        float v0 = movement.currentSpeed;
        float v1 = (2f * distance / sprintTotalTime) - v0;
        v1 = Mathf.Max(0f, v1);

        sprintFinalSpeed = v1;
        sprintAccel = (sprintFinalSpeed - v0) / sprintTotalTime;

        int dir = (sprintTargetWorldX > x) ? +1 : -1;
        movement.BeginSpeedTransition(dir, movement.currentSpeed, 0f, Time.time);
    }

    public void StopSprint()
    {
        isSprint = false;
        sprintTimeRemaining = 0f;
        sprintTotalTime = 0f;
        sprintAccel = 0f;
        sprintFinalSpeed = 0f;
    }
}
