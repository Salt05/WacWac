using System;
using UnityEngine;

/// <summary>
/// DuckBrain - Simplified action-based AI for an individual duck.
/// 
/// No stamina, no personality. Each duck:
/// - Has 3 actions: Forward / Idle / Backward
/// - Random action frequency per phase
/// - Random speed per action (within phase config range)
/// - All movement uses Cubic Ease-In (f(t)=t^3)
/// 
/// Phase transitions:
/// - Phase 1 -> 2: wait for current action cycle to complete, then use Phase 2 ranges
/// - Phase 2 -> 3: immediately cancel action, move to FinalProgress via Cubic
/// </summary>
public class DuckBrain : MonoBehaviour
{
    #region State

    private RaceController raceController;
    private System.Random rng;

    public int DuckId { get; private set; }
    public float StartX { get; private set; }
    public float StartY { get; private set; }

    public float CurrentP { get; private set; }
    public float TargetP { get; private set; }
    public float FinalP { get; private set; }
    public bool IsFinished { get; private set; }

    [SerializeField] private DuckStats stats = new DuckStats();
    public DuckStats Stats => stats;

    #endregion

    #region Transition State

    private float transitionStartP;
    private float transitionEndP;
    private float transitionDuration;
    private float transitionElapsed;
    private bool isTransitioning;

    private bool sprintActivated;

    // Track which phase config is currently active for this duck
    private RacePhase activePhaseConfig = RacePhase.Opening;
    // Track whether we've been notified of a phase change while mid-action
    private bool pendingPhaseSwitch;

    #endregion

    #region Initialization

    public void Initialize(RaceController rc, int duckId, float startX, float startY, System.Random rng)
    {
        this.raceController = rc;
        this.DuckId = duckId;
        this.StartX = startX;
        this.StartY = startY;
        this.rng = rng;

        CurrentP = 0f;
        TargetP = 0f;
        FinalP = 0f;
        IsFinished = false;

        isTransitioning = false;
        transitionElapsed = 0f;
        transitionDuration = 0f;

        sprintActivated = false;
        activePhaseConfig = RacePhase.Opening;
        pendingPhaseSwitch = false;

        stats.Initialize(duckId);

        UpdatePosition();
    }

    public void SetFinalP(float p)
    {
        FinalP = p;
    }

    #endregion

    #region Update Loop

    public void Tick(RacePhase phase, float deltaTime, float remainingTime)
    {
        if (IsFinished) return;

        switch (phase)
        {
            case RacePhase.Opening:
            case RacePhase.Midgame:
                TickActionPhase(phase, deltaTime);
                break;
            case RacePhase.Sprint:
                TickSprint(deltaTime, remainingTime);
                break;
        }

        ApplyTransition(deltaTime);

        CurrentP = Mathf.Clamp(CurrentP, -50f, 105f);

        if (CurrentP >= 99.5f && !IsFinished)
        {
            CurrentP = 100f;
        }

        UpdatePosition();
    }

    #endregion

    #region Phase 1 & 2 - Action-based movement

    private void TickActionPhase(RacePhase currentPhase, float deltaTime)
    {
        // Update action timer
        stats.TickActionTimer(deltaTime);

        // Check for phase transition: Phase 1 -> Phase 2
        if (currentPhase == RacePhase.Midgame && activePhaseConfig == RacePhase.Opening)
        {
            // Mark that we need to switch to Phase 2 ranges after current action completes
            if (!stats.IsActionComplete)
            {
                pendingPhaseSwitch = true;
                // Don't start new action yet - let current one finish
                return; // keep ticking current transition
            }
            else
            {
                // Current action is done, switch to Phase 2
                activePhaseConfig = RacePhase.Midgame;
                pendingPhaseSwitch = false;
            }
        }

        // If pending phase switch and action completed, apply it now
        if (pendingPhaseSwitch && stats.IsActionComplete)
        {
            activePhaseConfig = currentPhase;
            pendingPhaseSwitch = false;
        }

        // If action is complete, start a new one
        if (stats.IsActionComplete)
        {
            StartNewRandomAction();
        }
    }

    private void StartNewRandomAction()
    {
        // Get phase config from RaceController
        RaceController.PhaseConfig config = raceController.GetPhaseConfig(activePhaseConfig);

        // Let stats handle the random action selection
        stats.StartNewAction(
            config.actionFrequencyRange.x, config.actionFrequencyRange.y,
            config.forwardSpeedRange.x, config.forwardSpeedRange.y,
            config.backwardSpeedRange.x, config.backwardSpeedRange.y,
            rng
        );

        // Calculate target P based on action
        float actionDuration = stats.ActionDuration;
        float speed = stats.TargetSpeed;
        float targetP;

        switch (stats.CurrentAction)
        {
            case DuckStats.DuckAction.Forward:
                // Move forward: P increases
                targetP = CurrentP + speed * actionDuration;
                break;
            case DuckStats.DuckAction.Backward:
                // Move backward: P decreases
                targetP = CurrentP - speed * actionDuration;
                break;
            case DuckStats.DuckAction.Idle:
            default:
                // Stay in place
                targetP = CurrentP;
                break;
        }

        // Clamp target
        targetP = Mathf.Clamp(targetP, -20f, 105f);

        // Begin cubic transition to target
        BeginTransition(targetP, actionDuration);
    }

    #endregion

    #region Phase 3 - Sprint

    private void TickSprint(float deltaTime, float remainingTime)
    {
        if (sprintActivated) return;
        sprintActivated = true;

        // Force-stop any ongoing action
        stats.ForceStopAction();

        // Move to FinalP using cubic over remaining time
        float targetP = FinalP;
        float timeLeft = Mathf.Max(0.1f, remainingTime);
        BeginTransition(targetP, timeLeft);
    }

    #endregion

    #region Transition (Cubic Ease-In)

    private void BeginTransition(float endP, float duration)
    {
        transitionStartP = CurrentP;
        transitionEndP = endP;
        transitionDuration = Mathf.Max(0.01f, duration);
        transitionElapsed = 0f;
        isTransitioning = true;
        TargetP = endP;
    }

    private void ApplyTransition(float deltaTime)
    {
        if (!isTransitioning) return;
        transitionElapsed += deltaTime;
        float t = Mathf.Clamp01(transitionElapsed / transitionDuration);
        float eased = EaseInCubic(t);
        CurrentP = Mathf.Lerp(transitionStartP, transitionEndP, eased);
        if (t >= 1f)
        {
            CurrentP = transitionEndP;
            isTransitioning = false;
        }
    }

    private static float EaseInCubic(float t) => t * t * t;

    #endregion

    #region Position & UI

    private void UpdatePosition()
    {
        if (raceController == null) return;
        float anchoredX = raceController.PToAnchoredX(StartX, CurrentP);
        float anchoredY = StartY;

        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(anchoredX, anchoredY);
        }
        else
        {
            RectTransform spawnRect = raceController.GetSpawnAreaRect();
            if (spawnRect != null)
            {
                Vector3 worldPos = spawnRect.TransformPoint(new Vector3(anchoredX, anchoredY, 0f));
                transform.position = worldPos;
            }
        }
    }

    #endregion

    #region Public Accessors

    public void SnapToFinal()
    {
        CurrentP = FinalP;
        isTransitioning = false;
        IsFinished = true;
        UpdatePosition();
    }

    public float GetWorldX() => transform.position.x;

    #endregion
}
