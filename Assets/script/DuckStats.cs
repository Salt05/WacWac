using System;
using UnityEngine;

/// <summary>
/// DuckStats - Simplified duck data.
/// No stamina, no personality system.
/// Each duck only has: FinalProgress (ranking), action frequency, speed.
/// All parameters are public for Inspector tuning.
/// </summary>
[Serializable]
public sealed class DuckStats
{
    #region Enums

    /// <summary>
    /// Duck movement action (Forward / Idle / Backward)
    /// </summary>
    public enum DuckAction
    {
        Forward,    // Moving forward
        Idle,       // Standing still
        Backward    // Moving backward
    }

    /// <summary>
    /// Legacy tier kept for DuckVisualizer/DuckSystemTester compatibility.
    /// No gameplay effect.
    /// </summary>
    public enum Tier
    {
        Slow,
        Average,
        Fast,
        VeryFast
    }

    /// <summary>
    /// Legacy personality enum kept for compatibility.
    /// No gameplay effect.
    /// </summary>
    public enum Personality
    {
        Steady = 0,
        Sprinter = 1,
        Underdog = 2,
        Gambler = 3
    }

    #endregion

    #region Fields

    [Header("Identity")]
    public int duckIndex;

    [Header("Current Action State")]
    [SerializeField] private DuckAction currentAction = DuckAction.Idle;
    [SerializeField] private float targetSpeed;           // Current target speed for this action cycle
    [SerializeField] private float actionDuration;        // Total duration of current action cycle
    [SerializeField] private float actionTimer;           // Time remaining in current action cycle

    [Header("Legacy Compatibility")]
    public Tier tier = Tier.Average;
    public Personality personality = Personality.Steady;
    [Range(0f, 1f)] public float stamina01 = 1f;

    #endregion

    #region Properties

    public DuckAction CurrentAction => currentAction;
    public float TargetSpeed => targetSpeed;
    public float ActionDuration => actionDuration;
    public float ActionTimer => actionTimer;

    /// <summary>Has current action cycle completed?</summary>
    public bool IsActionComplete => actionTimer <= 0f;

    #endregion

    #region Initialization

    public void Initialize(int index)
    {
        duckIndex = index;
        currentAction = DuckAction.Idle;
        targetSpeed = 0f;
        actionDuration = 0f;
        actionTimer = 0f;
        stamina01 = 1f;
    }

    #endregion

    #region Action Management

    /// <summary>
    /// Start a new random action cycle with given phase config ranges.
    /// </summary>
    public void StartNewAction(
        float frequencyMin, float frequencyMax,
        float forwardSpeedMin, float forwardSpeedMax,
        float backwardSpeedMin, float backwardSpeedMax,
        System.Random rng)
    {
        // 1. Random action duration (frequency)
        float baseDuration = LerpFloat(frequencyMin, frequencyMax, (float)rng.NextDouble());

        // 2. Random action type: Forward, Idle, or Backward (equal 1/3 probability)
        int actionRoll = rng.Next(0, 3);
        currentAction = (DuckAction)actionRoll;

        // 3. Random speed based on action
        float cycleDuration = baseDuration;
        switch (currentAction)
        {
            case DuckAction.Forward:
                targetSpeed = LerpFloat(forwardSpeedMin, forwardSpeedMax, (float)rng.NextDouble());
                break;
            case DuckAction.Backward:
                targetSpeed = LerpFloat(backwardSpeedMin, backwardSpeedMax, (float)rng.NextDouble());
                break;
            case DuckAction.Idle:
            default:
                targetSpeed = 0f;
                cycleDuration = baseDuration * 0.5f;
                break;
        }

        actionDuration = cycleDuration;
        actionTimer = cycleDuration;

    }

    /// <summary>
    /// Tick the action timer down by deltaTime.
    /// </summary>
    public void TickActionTimer(float deltaTime)
    {
        if (actionTimer > 0f)
        {
            actionTimer -= deltaTime;
        }
    }

    /// <summary>
    /// Force-stop current action immediately.
    /// Used for Phase 2 -> Phase 3 transition (instant cancel).
    /// </summary>
    public void ForceStopAction()
    {
        actionTimer = 0f;
        currentAction = DuckAction.Idle;
        targetSpeed = 0f;
    }

    #endregion

    #region Utility

    private static float LerpFloat(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    #endregion
}