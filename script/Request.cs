using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameRequest", menuName = "WacWac/GameRequest")]
public class GameRequest : ScriptableObject
{
    [Header("Basic Info")]
    public string gameTitle;
    public string version;
    [TextArea(3, 8)]
    public string description;

    [Header("Content")]
    public List<LevelInfo> levels = new List<LevelInfo>();
    public List<Sprite> duckSkins = new List<Sprite>();
    public List<GameObject> prefabs = new List<GameObject>();

    [System.Serializable]
    public class LevelInfo
    {
        public string levelName;
        public string scenePath;
        public int difficulty = 1;
    }
}
