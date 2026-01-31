using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime tester utilities for validating duck systems with large counts.
/// Attach to any GameObject in the race scene.
/// </summary>
public sealed class DuckSystemTester : MonoBehaviour
{
    [Header("Refs")]
    public RaceController raceController;

    [Header("Logging")]
    public bool logOnStart = true;

    [Tooltip("Threshold for considering ducks too synchronized (correlation check is expensive; we use a cheap heuristic).")]
    public float syncToleranceX = 0.001f;

    private void Start()
    {
        if (raceController == null) raceController = FindObjectOfType<RaceController>();
        if (logOnStart) LogDistributionStats();
    }

    public void LogDistributionStats()
    {
        if (raceController == null)
        {
            Debug.LogWarning("DuckSystemTester: RaceController not found");
            return;
        }

        var ducks = FindObjectsOfType<DuckMover>();
        if (ducks.Length == 0)
        {
            Debug.LogWarning("DuckSystemTester: no DuckMover found");
            return;
        }

        var tierCounts = new Dictionary<DuckStats.Tier, int>();
        var persCounts = new Dictionary<DuckStats.Personality, int>();

        foreach (var d in ducks)
        {
            var t = d.GetTier();
            var p = d.GetPersonality();

            tierCounts[t] = tierCounts.ContainsKey(t) ? tierCounts[t] + 1 : 1;
            persCounts[p] = persCounts.ContainsKey(p) ? persCounts[p] + 1 : 1;
        }

        int total = ducks.Length;
        Debug.Log($"[DuckSystemTester] Total ducks: {total}");

        foreach (DuckStats.Tier t in Enum.GetValues(typeof(DuckStats.Tier)))
        {
            int c = tierCounts.ContainsKey(t) ? tierCounts[t] : 0;
            Debug.Log($"Tier {t}: {c} ({(c * 100f / total):F1}%)");
        }

        foreach (DuckStats.Personality p in Enum.GetValues(typeof(DuckStats.Personality)))
        {
            int c = persCounts.ContainsKey(p) ? persCounts[p] : 0;
            Debug.Log($"Personality {p}: {c} ({(c * 100f / total):F1}%)");
        }
    }

    /// <summary>
    /// Cheap check: if too many ducks have the exact same X delta between samples, they are likely synchronized.
    /// </summary>
    public void ValidateNoSyncMovement(int sampleFrames = 60)
    {
        StartCoroutine(ValidateNoSyncMovementRoutine(sampleFrames));
    }

    private System.Collections.IEnumerator ValidateNoSyncMovementRoutine(int sampleFrames)
    {
        var ducks = FindObjectsOfType<DuckMover>();
        if (ducks.Length < 2)
        {
            Debug.LogWarning("DuckSystemTester: need at least 2 ducks");
            yield break;
        }

        float[] lastX = new float[ducks.Length];
        for (int i = 0; i < ducks.Length; i++) lastX[i] = ducks[i].GetWorldX();

        int suspiciousFrames = 0;

        for (int f = 0; f < sampleFrames; f++)
        {
            yield return null;

            // build histogram of dx rounded to tolerance
            var hist = new Dictionary<int, int>();
            for (int i = 0; i < ducks.Length; i++)
            {
                float x = ducks[i].GetWorldX();
                float dx = x - lastX[i];
                lastX[i] = x;

                int key = Mathf.RoundToInt(dx / Mathf.Max(1e-6f, syncToleranceX));
                hist[key] = hist.ContainsKey(key) ? hist[key] + 1 : 1;
            }

            int maxBucket = 0;
            foreach (var kv in hist) maxBucket = Mathf.Max(maxBucket, kv.Value);

            // If more than 60% share identical dx bucket, flag frame.
            if (maxBucket > ducks.Length * 0.60f) suspiciousFrames++;
        }

        Debug.Log($"[DuckSystemTester] Sync suspicious frames: {suspiciousFrames}/{sampleFrames} (tolerance={syncToleranceX})");
    }

    /// <summary>
    /// Simple helper: start race if possible, then fast-forward timeScale for quick logic coverage.
    /// (Does not guarantee deterministic unit test; use for manual smoke testing.)
    /// </summary>
    public void SimulateRace(float timeScale = 5f, float secondsRealTime = 3f)
    {
        StartCoroutine(SimulateRaceRoutine(timeScale, secondsRealTime));
    }

    private System.Collections.IEnumerator SimulateRaceRoutine(float ts, float seconds)
    {
        float old = Time.timeScale;
        Time.timeScale = ts;
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = old;
        Debug.Log("[DuckSystemTester] SimulateRace finished");
    }
}
