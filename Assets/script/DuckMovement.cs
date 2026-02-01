using UnityEngine;

/// <summary>
/// Phase 2: handles motion integration (speed smoothing, bounds clamp).
/// Reworked to use a single cubic ease-in-out interpolation for speed transitions
/// (no two-phase sign-flip special casing). BeginSpeedTransition records the
/// start speed to avoid jumps when targets change mid-transition.
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
    public bool isTransitioning => inTransition;
    private float transitionStartTime;
    private float transitionDuration;

    // interpolation endpoints
    private float startSpeedSigned;

    public int lastDirection { get; private set; } = +1;
    public float lastTargetSpeed { get; private set; }

    public void Reset()
    {
        direction = +1;
        currentSpeedSigned = 0f;
        targetSpeedSigned = 0f;
        inTransition = false;
        transitionStartTime = 0f;
        transitionDuration = 0f;
        startSpeedSigned = 0f;

        lastDirection = +1;
        lastTargetSpeed = 0f;
    }

    public void SetBounds(float minX, float maxX)
    {
        minPosX = minX;
        maxPosX = maxX;
    }

    /// <summary>
    /// Start a new transition towards a signed target speed over transitionDuration seconds.
    /// This uses a cubic ease-in-out easing curve for smooth start and stop.
    /// BeginSpeedTransition records the current speed as the start so changes mid-transition
    /// do not produce velocity jumps.
    /// </summary>
    public void BeginSpeedTransition(int newDirection, float targetSpeedMagnitude, float transitionTime, float now)
    {
        direction = (newDirection < 0) ? -1 : +1;

        float mag = Mathf.Max(0f, targetSpeedMagnitude);
        float desiredSigned = direction * mag;

        transitionDuration = Mathf.Max(0f, transitionTime);
        transitionStartTime = now;

        // Record the current signed speed as the start to avoid jumps when decisions change.
        startSpeedSigned = currentSpeedSigned;
        targetSpeedSigned = desiredSigned;

        if (transitionDuration <= 0.0001f)
        {
            currentSpeedSigned = targetSpeedSigned;
            inTransition = false;
            return;
        }

        inTransition = true;
    }

    /// <summary>
    /// Immediately stop any ongoing transition, sampling the current interpolated speed so
    /// external code (e.g. sprint) can take over cleanly.
    /// </summary>
    public void StopTransition()
    {
        if (!inTransition) return;

        float now = Time.time;
        float elapsed = now - transitionStartTime;
        if (elapsed <= 0f)
        {
            currentSpeedSigned = startSpeedSigned;
        }
        else
        {
            float T = Mathf.Max(0.0001f, transitionDuration);
            float t = Mathf.Clamp01(elapsed / T);
            float e = EaseCubicInOut(t);
            currentSpeedSigned = Mathf.Lerp(startSpeedSigned, targetSpeedSigned, e);
        }

        inTransition = false;
    }

    public void ApplyAntiStrongReversal(float reversalSpeedThreshold)
    {
        // Keep the old rule in terms of direction + magnitude.
        if (lastDirection == +1 && lastTargetSpeed >= reversalSpeedThreshold && direction == -1 && Mathf.Abs(targetSpeedSigned) >= reversalSpeedThreshold)
        {
            targetSpeedSigned *= 0.5f;
        }

        lastDirection = direction;
        lastTargetSpeed = Mathf.Abs(targetSpeedSigned);
    }

    public void GuardDirectionAtBounds(float x)
    {
        if (x <= minPosX) direction = +1;
        else if (x >= maxPosX) direction = -1;
    }

    private static float EaseCubicInOut(float t)
    {
        // f(t) = (t < 0.5 ? 4t^3 : 1 - ((-2t + 2)^3) / 2)
        if (t < 0.5f)
            return 4f * t * t * t;
        float k = -2f * t + 2f;
        return 1f - (k * k * k) / 2f;
    }

    public void StepSpeed(float now)
    {
        if (!inTransition) return;

        float elapsed = now - transitionStartTime;
        if (elapsed <= 0f) return;

        float T = Mathf.Max(0.0001f, transitionDuration);

        float t = Mathf.Clamp01(elapsed / T);
        float e = EaseCubicInOut(t);

        currentSpeedSigned = Mathf.Lerp(startSpeedSigned, targetSpeedSigned, e);

        if (t >= 1f)
        {
            currentSpeedSigned = targetSpeedSigned;
            inTransition = false;
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
        }

        return x;
    }
}
