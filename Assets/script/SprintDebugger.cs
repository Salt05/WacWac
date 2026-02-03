using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// Lightweight debugging helper you can attach anywhere.
// Purpose: log state around the sprint/start-of-finish phase without touching existing code.
// Delete this file to remove all instrumentation.
public class SprintDebugger : MonoBehaviour
{
    public RaceController raceController;
    [Tooltip("Seconds between polls (lower = more logs).")]
    public float pollInterval = 0.1f;

    // Only log the "enter sprint phase" snapshot once per race by default.
    public bool logOncePerRace = true;

    private float nextPoll = 0f;
    private bool loggedFinishPhase = false;

    // track sprinting transitions per duck to log when a sprint actually starts
    private readonly Dictionary<DuckMover, bool> prevSprinting = new Dictionary<DuckMover, bool>();

    void Reset()
    {
        pollInterval = 0.1f;
        logOncePerRace = true;
    }

    void OnEnable()
    {
        nextPoll = Time.time + 0.02f;
        loggedFinishPhase = false;
        prevSprinting.Clear();
    }

    void Update()
    {
        if (Time.time < nextPoll) return;
        nextPoll = Time.time + Mathf.Max(0.01f, pollInterval);

        if (raceController == null)
        {
            raceController = FindObjectOfType<RaceController>();
            if (raceController == null) return;
        }

        PollState();
    }

    [ContextMenu("Log Sprint Debug Snapshot Now")]
    public void LogNow()
    {
        if (raceController == null) raceController = FindObjectOfType<RaceController>();
        if (raceController == null)
        {
            Debug.LogWarning("SprintDebugger: RaceController not found");
            return;
        }
        LogSnapshotAggregated("Manual snapshot");
    }

    private void PollState()
    {
        // Detect when we enter the finish/sprint window: remainingTime <= stopDuckTimeD
        float remaining = raceController.remainingTime;
        float stopD = raceController.stopDuckTimeD;

        if (!loggedFinishPhase && remaining <= stopD)
        {
            LogSnapshotAggregated("Entered sprint/finish phase");
            if (logOncePerRace) loggedFinishPhase = true;
        }

        // Detect individual ducks starting sprint and aggregate into a single log
        var ducks = FindObjectsOfType<DuckMover>();
        var started = new List<DuckMover>();

        foreach (var d in ducks)
        {
            bool cur = d.IsSprinting();
            bool prev = false;
            if (!prevSprinting.TryGetValue(d, out prev)) prev = false;

            if (!prev && cur)
            {
                started.Add(d);
            }

            prevSprinting[d] = cur;
        }

        if (started.Count > 0)
        {
            LogSprintStartAggregated(started, remaining, stopD);
        }
    }

    // Single aggregated snapshot log containing high-level race and candidate info.
    private void LogSnapshotAggregated(string title)
    {
        var ducks = FindObjectsOfType<DuckMover>();
        int total = ducks.Length;
        int sprinting = ducks.Count(d => d != null && d.IsSprinting());
        int finished = ducks.Count(d => d != null && d.GetTimeReachedFinish() >= 0f);

        float finishWorldX = raceController.GetFinishWorldX();

        var sb = new StringBuilder();
        sb.AppendFormat("[SprintDebugger] {0} | remaining={1:F3} stopD={2:F3} totalDucks={3} sprinting={4} finished={5} finishWorldX={6}",
            title, raceController.remainingTime, raceController.stopDuckTimeD, total, sprinting, finished,
            float.IsNaN(finishWorldX) ? "NaN" : finishWorldX.ToString("F3"));

        // include top non-finished candidates (compact)
        var candidates = ducks.Where(d => d != null && d.GetTimeReachedFinish() < 0f).OrderByDescending(d => d.GetWorldX()).ToArray();
        sb.AppendFormat(" | nonFinished={0}", candidates.Length);
        int maxShow = Mathf.Min(6, candidates.Length);
        if (maxShow > 0)
        {
            sb.Append(" | top=");
            for (int i = 0; i < maxShow; i++)
            {
                var d = candidates[i];
                if (i > 0) sb.Append(";");
                sb.AppendFormat("#{0}:idx{1},x{2:F3},st{3:F2}", i + 1, d.GetDuckIndex(), d.GetWorldX(), d.GetStamina01());
            }
        }

        // include simple tier/personality counts
        var tierCounts = new Dictionary<DuckStats.Tier, int>();
        var persCounts = new Dictionary<DuckStats.Personality, int>();
        foreach (var d in ducks)
        {
            if (d == null) continue;
            var t = d.GetTier();
            tierCounts[t] = tierCounts.ContainsKey(t) ? tierCounts[t] + 1 : 1;
            var p = d.GetPersonality();
            persCounts[p] = persCounts.ContainsKey(p) ? persCounts[p] + 1 : 1;
        }

        sb.Append(" | tiers=");
        foreach (DuckStats.Tier tt in System.Enum.GetValues(typeof(DuckStats.Tier)))
        {
            int c = tierCounts.ContainsKey(tt) ? tierCounts[tt] : 0;
            sb.AppendFormat("{0}:{1},", tt.ToString(), c);
        }

        sb.Append(" | pers=");
        foreach (DuckStats.Personality pp in System.Enum.GetValues(typeof(DuckStats.Personality)))
        {
            int c = persCounts.ContainsKey(pp) ? persCounts[pp] : 0;
            sb.AppendFormat("{0}:{1},", pp.ToString(), c);
        }

        Debug.Log(sb.ToString());
    }

    // Single aggregated log when one or more ducks start sprinting.
    private void LogSprintStartAggregated(List<DuckMover> started, float remaining, float stopD)
    {
        var sb = new StringBuilder();
        sb.AppendFormat("[SprintDebugger] SprintStarted count={0} remaining={1:F3} stopD={2:F3} | ", started.Count, remaining, stopD);

        for (int i = 0; i < started.Count; i++)
        {
            var d = started[i];
            if (i > 0) sb.Append(";");
            sb.AppendFormat("idx{0},x{1:F3},maxX{2:F3},st{3:F2}", d.GetDuckIndex(), d.GetWorldX(), d.GetMaxPosX(), d.GetStamina01());
        }

        // add a compact top-3 snapshot to give context
        var ducks = FindObjectsOfType<DuckMover>();
        var candidates = ducks.Where(d => d != null && d.GetTimeReachedFinish() < 0f).OrderByDescending(d => d.GetWorldX()).Take(3).ToArray();
        if (candidates.Length > 0)
        {
            sb.Append(" | top3=");
            for (int i = 0; i < candidates.Length; i++)
            {
                var d = candidates[i];
                if (i > 0) sb.Append(".");
                sb.AppendFormat("idx{0},x{1:F3}", d.GetDuckIndex(), d.GetWorldX());
            }
        }

        Debug.Log(sb.ToString());

        // Also emit one full snapshot for context (optional) but keep it as a single log
        LogSnapshotAggregated("Context after sprint starts");
    }
}
