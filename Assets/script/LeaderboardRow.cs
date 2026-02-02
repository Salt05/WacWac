using TMPro;
using UnityEngine;

// Row view model for LeaderboardUI dynamic list.
public class LeaderboardRow : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI unitText; // optional - will be hidden by default

    public void Bind(int rank, string name, int logicalUnit)
    {
        if (rankText != null)
        {
            rankText.text = rank.ToString();
        }
        if (nameText != null)
        {
            nameText.text = name;
        }

        // Hide the unit/score field so the UI only shows rank and name.
        if (unitText != null)
        {
            unitText.text = string.Empty;
            unitText.gameObject.SetActive(false);
        }
    }
}
