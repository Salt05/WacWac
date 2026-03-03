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

    [Header("Top 3 Prefab")]
    [SerializeField] private LeaderboardRow top3Prefab;
    [SerializeField] private Sprite top1Sprite;
    [SerializeField] private Sprite top2Sprite;
    [SerializeField] private Sprite top3Sprite;

    [Header("Legacy Top3 (optional)")]
    [SerializeField] private TextMeshProUGUI firstText;
    [SerializeField] private TextMeshProUGUI secondText;
    [SerializeField] private TextMeshProUGUI thirdText;

    [Header("Settings")]
    [Tooltip("Update leaderboard every N seconds during race (0 = only at finish)")]
    [SerializeField] private float updateInterval = 0.5f;

    [Tooltip("Hide the leaderboard GameObject until the race finishes")]
    [SerializeField] private bool hideUntilFinish = true;

    [Header("Spawn Effect")]
    [Tooltip("Apply scale pop effect to first N ranks when rows are spawned")]
    [SerializeField] private int spawnEffectCount = 5;
    [SerializeField] private float spawnScaleStart = 0.7f;
    [SerializeField] private float spawnScaleDuration = 0.35f;

    private readonly List<LeaderboardRow> spawnedRows = new List<LeaderboardRow>();
    private float nextUpdateTime;

    private void Start()
    {
        if (hideUntilFinish)
        {
            gameObject.SetActive(false);
        }
    }

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
        EnsureRowsForCount(ducks.Count);

        for (int i = 0; i < ducks.Count; i++)
        {
            var duck = ducks[i];
            string name = ResolveDuckName(duck, i);

            int progressScore = Mathf.RoundToInt(duck.CurrentP);
            bool showRank = i >= 3;
            Sprite rankSprite = GetTopSpriteForIndex(i);
            spawnedRows[i].Bind(i + 1, name, progressScore, showRank, rankSprite);
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
        EnsureRowsForCount(ducks.Count);

        for (int i = 0; i < ducks.Count; i++)
        {
            var dm = ducks[i];
            string name = ResolveDuckNameFromMover(dm, i);
            int score = dm != null ? Mathf.RoundToInt(dm.GetWorldX()) : 0;
            bool showRank = i >= 3;
            Sprite rankSprite = GetTopSpriteForIndex(i);
            spawnedRows[i].Bind(i + 1, name, score, showRank, rankSprite);
        }
    }

    private void EnsureRowsForCount(int count)
    {
        if (contentRoot == null) return;

        while (spawnedRows.Count < count)
        {
            int index = spawnedRows.Count;
            var prefab = GetPrefabForRankIndex(index);
            if (prefab == null) break;

            var row = Instantiate(prefab, contentRoot);
            spawnedRows.Add(row);

            if (index < spawnEffectCount)
            {
                StartCoroutine(PlaySpawnEffect(row.transform));
            }
        }

        for (int i = 0; i < spawnedRows.Count; i++)
        {
            spawnedRows[i].gameObject.SetActive(i < count);
        }
    }

    private LeaderboardRow GetPrefabForRankIndex(int index)
    {
        if (index < 3 && top3Prefab != null) return top3Prefab;
        return rowPrefab;
    }

    private Sprite GetTopSpriteForIndex(int index)
    {
        switch (index)
        {
            case 0:
                return top1Sprite;
            case 1:
                return top2Sprite;
            case 2:
                return top3Sprite;
            default:
                return null;
        }
    }

    private System.Collections.IEnumerator PlaySpawnEffect(Transform target)
    {
        if (target == null) yield break;

        float elapsed = 0f;
        Vector3 start = Vector3.one * spawnScaleStart;
        Vector3 end = Vector3.one;
        target.localScale = start;

        while (elapsed < spawnScaleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / spawnScaleDuration);
            float eased = EaseOutCubic(t);
            target.localScale = Vector3.LerpUnclamped(start, end, eased);
            yield return null;
        }

        target.localScale = end;
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void ResetUI()
    {
        if (spawnedRows.Count > 0)
        {
            for (int i = 0; i < spawnedRows.Count; i++)
            {
                if (spawnedRows[i] != null)
                {
                    Destroy(spawnedRows[i].gameObject);
                }
            }
            spawnedRows.Clear();
        }

        if (firstText != null) firstText.text = string.Empty;
        if (secondText != null) secondText.text = string.Empty;
        if (thirdText != null) thirdText.text = string.Empty;

        if (hideUntilFinish)
        {
            gameObject.SetActive(false);
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
