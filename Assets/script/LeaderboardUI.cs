using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Dynamic leaderboard UI.
// NEW DESIGN:
// - Final ranking is sorted by duck world position.x (desc) in RaceController.
// - This UI should not depend on logicalUnit.
public class LeaderboardUI : MonoBehaviour
{
    [Header("Dynamic List")]
    public Transform contentRoot;
    public LeaderboardRow rowPrefab;

    [Header("Legacy Top3 (optional)")]
    public TextMeshProUGUI firstText;
    public TextMeshProUGUI secondText;
    public TextMeshProUGUI thirdText;

    private readonly List<LeaderboardRow> spawnedRows = new List<LeaderboardRow>();

    public void UpdateLeaderboard(List<DuckMover> ducks)
    {
        if (contentRoot != null && rowPrefab != null)
        {
            UpdateDynamic(ducks);
            UpdateLegacyTop3(ducks);
            return;
        }

        // fallback: legacy only
        UpdateLegacyTop3(ducks);
    }

    private void UpdateDynamic(List<DuckMover> ducks)
    {
        // ensure enough rows
        while (spawnedRows.Count < ducks.Count)
        {
            var row = Instantiate(rowPrefab, contentRoot);
            spawnedRows.Add(row);
        }

        // disable extra
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            spawnedRows[i].gameObject.SetActive(i < ducks.Count);
        }

        for (int i = 0; i < ducks.Count; i++)
        {
            var dm = ducks[i];
            string name = ResolveDuckName(dm, i);

            // Keep LeaderboardRow API compatible: use a "score" field as worldX rounded.
            int score = dm != null ? Mathf.RoundToInt(dm.GetWorldX()) : 0;
            spawnedRows[i].Bind(i + 1, name, score);
        }
    }

    private void UpdateLegacyTop3(List<DuckMover> ducks)
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

    private string ResolveDuckName(DuckMover dm, int index)
    {
        if (dm == null) return "Duck " + (index + 1);
        var label = dm.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null && !string.IsNullOrEmpty(label.text)) return label.text;
        return "Duck " + (index + 1);
    }
}
