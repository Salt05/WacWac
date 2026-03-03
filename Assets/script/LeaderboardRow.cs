using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Row view model for LeaderboardUI dynamic list.
public class LeaderboardRow : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI unitText; // optional - will be hidden by default
    public Image rankIcon; // optional - used for top 3 sprites

    public void Bind(int rank, string name, int logicalUnit, bool showRank, Sprite rankSprite)
    {
        if (rankText != null)
        {
            rankText.gameObject.SetActive(showRank);
            rankText.text = showRank ? rank.ToString() : string.Empty;
        }
        if (nameText != null)
        {
            nameText.text = name;
        }

        if (rankIcon != null)
        {
            bool showIcon = rankSprite != null;
            rankIcon.gameObject.SetActive(showIcon);
            if (showIcon)
            {
                rankIcon.sprite = rankSprite;
            }
        }

        // Hide the unit/score field so the UI only shows rank and name.
        if (unitText != null)
        {
            unitText.text = string.Empty;
            unitText.gameObject.SetActive(false);
        }
    }
}
