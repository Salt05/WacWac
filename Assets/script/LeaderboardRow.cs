using TMPro;
using UnityEngine;

// Row view model for LeaderboardUI dynamic list.
public class LeaderboardRow : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI unitText; // optional

    public void Bind(int rank, string name, int logicalUnit)
    {
        if (rankText != null) rankText.text = rank.ToString();
        if (nameText != null) nameText.text = name;
        if (unitText != null) unitText.text = logicalUnit.ToString();
    }
}
