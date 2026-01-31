using UnityEngine;

/// <summary>
/// Phase 2: handles motion integration (speed smoothing, bounds clamp).
/// Now uses linear interpolation transitions (no accelDuration/MoveTowards).
/// </summary>
public sealed class DuckMovement
{
    public float minPosX;
    public float maxPosX;

    public int direction { get; private set; } = +1; // -1/+1

    // Signed speed convention:
    // - currentSpeedSigned stores direction * magnitude, so it can be negative.
    // - movement uses currentSpeedSigned directly to advance X.
    public float currentSpeedSigned { get; private set; }

    public float currentSpeed => Mathf.Abs(currentSpeedSigned);

    // target state (signed)
    private float targetSpeedSigned;

    // transition
    private bool inTransition;
    private float transitionStartTime;
    private float transitionTotalTime;

    // two-phase when sign flips
    private bool twoPhase;
    private float phaseStartSpeed;
    private float phaseMidSpeed; // always 0
    private float phaseEndSpeed;

    public int lastDirection { get; private set; } = +1;
    public float lastTargetSpeed { get; private set; }

    public void Reset()
    {
        direction = +1;
        currentSpeedSigned = 0f;
        targetSpeedSigned = 0f;
        inTransition = false;
        transitionStartTime = 0f;
        transitionTotalTime = 0f;
        twoPhase = false;
        phaseStartSpeed = 0f;
        phaseMidSpeed = 0f;
        phaseEndSpeed = 0f;

        lastDirection = +1;
        lastTargetSpeed = 0f;
    }

    public void SetBounds(float minX, float maxX)
    {
        minPosX = minX;
        maxPosX = maxX;
    }

    /// <summary>
    /// Start a new linear transition towards a signed target speed over transitionTime seconds.
    /// If sign flips (e.g. +1.0 -> -0.8), it transitions +1.0 -> 0 -> -0.8 with half time each.
    /// </summary>
    public void BeginSpeedTransition(int newDirection, float targetSpeedMagnitude, float transitionTime, float now)
    {
        direction = (newDirection < 0) ? -1 : +1;

        float mag = Mathf.Max(0f, targetSpeedMagnitude);
        float desiredSigned = direction * mag;

        transitionTotalTime = Mathf.Max(0f, transitionTime);
        transitionStartTime = now;

        float startSigned = currentSpeedSigned;
        targetSpeedSigned = desiredSigned;

        if (transitionTotalTime <= 0.0001f)
        {
            currentSpeedSigned = targetSpeedSigned;
            inTransition = false;
            twoPhase = false;
            return;
        }

        // two-phase when sign changes and non-zero start
        bool signFlip = (startSigned > 0f && targetSpeedSigned < 0f) || (startSigned < 0f && targetSpeedSigned > 0f);
        if (signFlip && Mathf.Abs(startSigned) > 0.0001f)
        {
            twoPhase = true;
            inTransition = true;
            phaseStartSpeed = startSigned;
            phaseMidSpeed = 0f;
            phaseEndSpeed = targetSpeedSigned;
        }
        else
        {
            twoPhase = false;
            inTransition = true;
            phaseStartSpeed = startSigned;
            phaseEndSpeed = targetSpeedSigned;
        }
    }

    public void ApplyAntiStrongReversal(float reversalSpeedThreshold)
    {
        // Keep the old rule in terms of direction + magnitude.
        if (lastDirection == +1 && lastTargetSpeed >= reversalSpeedThreshold && direction == -1 && Mathf.Abs(targetSpeedSigned) >= reversalSpeedThreshold)
        {
            targetSpeedSigned *= 0.5f;
            phaseEndSpeed = targetSpeedSigned;
        }

        lastDirection = direction;
        lastTargetSpeed = Mathf.Abs(targetSpeedSigned);
    }

    public void GuardDirectionAtBounds(float x)
    {
        if (x <= minPosX) direction = +1;
        else if (x >= maxPosX) direction = -1;
    }

    public void StepSpeed(float now)
    {
        if (!inTransition) return;

        float elapsed = now - transitionStartTime;
        if (elapsed <= 0f) return;

        float T = Mathf.Max(0.0001f, transitionTotalTime);

        if (!twoPhase)
        {
            float t = Mathf.Clamp01(elapsed / T);
            currentSpeedSigned = Mathf.Lerp(phaseStartSpeed, phaseEndSpeed, t);
            if (t >= 1f)
            {
                currentSpeedSigned = phaseEndSpeed;
                inTransition = false;
            }
            return;
        }

        // Two-phase: first half to 0, second half to end.
        float half = T * 0.5f;
        if (elapsed >= T)
        {
            currentSpeedSigned = phaseEndSpeed;
            inTransition = false;
            twoPhase = false;
            return;
        }

        if (elapsed <= half)
        {
            float t1 = Mathf.Clamp01(elapsed / half);
            currentSpeedSigned = Mathf.Lerp(phaseStartSpeed, 0f, t1);
        }
        else
        {
            float t2 = Mathf.Clamp01((elapsed - half) / half);
            currentSpeedSigned = Mathf.Lerp(0f, phaseEndSpeed, t2);
        }
    }

    public float StepPositionX(float x, float dt)
    {
        x += currentSpeedSigned * dt;

        float clampedX = Mathf.Clamp(x, minPosX, maxPosX);
        if (clampedX != x)
        {
            x = clampedX;

            if (x <= minPosX)
            {
                if (direction < 0) direction = +1;
            }
            else if (x >= maxPosX)
            {
                if (direction > 0) direction = -1;
            }

            // stop at bounds
            currentSpeedSigned = 0f;
            inTransition = false;
            twoPhase = false;
        }

        return x;
    }
}
