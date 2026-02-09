using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Dynamic leaderboard UI.
/// 
/// NEW DESIGN:
/// - Rankings are sorted by duck CurrentP (Progress) in descending order
/// - Updates in real-time during race or at finish
/// - Finish is triggered when P >= 99.5 (logic-based, not collision-based)
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("Dynamic List")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private LeaderboardRow rowPrefab;

    [Header("Legacy Top3 (optional)")]
    [SerializeField] private TextMeshProUGUI firstText;
    [SerializeField] private TextMeshProUGUI secondText;
    [SerializeField] private TextMeshProUGUI thirdText;

    [Header("Settings")]
    [Tooltip("Update leaderboard every N seconds during race (0 = only at finish)")]
    [SerializeField] private float updateInterval = 0.5f;

    private readonly List<LeaderboardRow> spawnedRows = new List<LeaderboardRow>();
    private float nextUpdateTime;

    private void Update()
    {
        // Real-time update during race
        if (updateInterval > 0f && Time.time >= nextUpdateTime)
        {
            var rc = FindObjectOfType<RaceController>();
            if (rc != null && rc.IsRunning())
            {
                UpdateFromRaceController(rc);
            }
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    /// <summary>
    /// Update leaderboard from RaceController (uses DuckBrain Progress values)
    /// </summary>
    public void UpdateFromRaceController(RaceController rc)
    {
        if (rc == null) return;

        // This method will be called by RaceController
        // For now, we just trigger an update if we have content
        if (contentRoot != null && rowPrefab != null)
        {
            // Get sorted duck list from RaceController (will be implemented)
            // For now, just log
        }
    }

    /// <summary>
    /// Legacy method for DuckMover compatibility
    /// </summary>
    public void UpdateLeaderboard(List<DuckMover> ducks)
    {
        if (contentRoot != null && rowPrefab != null)
        {
            UpdateDynamicFromMover(ducks);
            UpdateLegacyTop3FromMover(ducks);
            return;
        }

        // fallback: legacy only
        UpdateLegacyTop3FromMover(ducks);
    }

    /// <summary>
    /// Update leaderboard from DuckBrain list (new Progress-based system)
    /// Sorted by CurrentP descending - higher P = higher rank
    /// </summary>
    public void UpdateLeaderboard(List<DuckBrain> ducks)
    {
        if (ducks == null || ducks.Count == 0) return;

        // Sort by CurrentP descending (highest progress = rank 1)
        var sorted = new List<DuckBrain>(ducks);
        sorted.Sort((a, b) => b.CurrentP.CompareTo(a.CurrentP));

        if (contentRoot != null && rowPrefab != null)
        {
            UpdateDynamic(sorted);
        }

        UpdateLegacyTop3(sorted);
    }

    private void UpdateDynamic(List<DuckBrain> ducks)
    {
        // Ensure enough rows
        while (spawnedRows.Count < ducks.Count)
        {
            var row = Instantiate(rowPrefab, contentRoot);
            spawnedRows.Add(row);
        }

        // Disable extra rows
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            spawnedRows[i].gameObject.SetActive(i < ducks.Count);
        }

        // Update each row
        for (int i = 0; i < ducks.Count; i++)
        {
            var duck = ducks[i];
            string name = ResolveDuckName(duck, i);
            
            // Display Progress value (0-100) as score
            int progressScore = Mathf.RoundToInt(duck.CurrentP);
            spawnedRows[i].Bind(i + 1, name, progressScore);
        }
    }

    private void UpdateLegacyTop3(List<DuckBrain> ducks)
    {
        if (firstText == null && secondText == null && thirdText == null) return;

        for (int i = 0; i < 3; i++)
        {
            string t = "";
            if (i < ducks.Count)
            {
                t = ResolveDuckName(ducks[i], i);
            }
            switch (i)
            {
                case 0:
                    if (firstText != null) firstText.text = t;
                    break;
                case 1:
                    if (secondText != null) secondText.text = t;
                    break;
                case 2:
                    if (thirdText != null) thirdText.text = t;
                    break;
            }
        }
    }

    private void UpdateDynamicFromMover(List<DuckMover> ducks)
    {
        // Ensure enough rows
        while (spawnedRows.Count < ducks.Count)
        {
            var row = Instantiate(rowPrefab, contentRoot);
            spawnedRows.Add(row);
        }

        // Disable extra
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            spawnedRows[i].gameObject.SetActive(i < ducks.Count);
        }

        for (int i = 0; i < ducks.Count; i++)
        {
            var dm = ducks[i];
            string name = ResolveDuckNameFromMover(dm, i);
            int score = dm != null ? Mathf.RoundToInt(dm.GetWorldX()) : 0;
            spawnedRows[i].Bind(i + 1, name, score);
        }
    }

    private void UpdateLegacyTop3FromMover(List<DuckMover> ducks)
    {
        if (firstText == null && secondText == null && thirdText == null) return;

        for (int i = 0; i < 3; i++)
        {
            string t = "";
            if (i < ducks.Count)
            {
                t = ResolveDuckNameFromMover(ducks[i], i);
            }
            switch (i)
            {
                case 0:
                    if (firstText != null) firstText.text = t;
                    break;
                case 1:
                    if (secondText != null) secondText.text = t;
                    break;
                case 2:
                    if (thirdText != null) thirdText.text = t;
                    break;
            }
        }
    }

    private string ResolveDuckName(DuckBrain duck, int index)
    {
        if (duck == null) return "Duck " + (index + 1);
        var label = duck.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null && !string.IsNullOrEmpty(label.text)) return label.text;
        return "Duck " + (duck.DuckId + 1);
    }

    private string ResolveDuckNameFromMover(DuckMover dm, int index)
    {
        if (dm == null) return "Duck " + (index + 1);
        var label = dm.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null && !string.IsNullOrEmpty(label.text)) return label.text;
        return "Duck " + (index + 1);
    }
}
