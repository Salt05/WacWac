using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DataManager.cs
/// Save/load system for duck names using PlayerPrefs (backed by localStorage on WebGL).
/// Data persists across browser reloads without requiring a database.
/// </summary>
public class DataManager : MonoBehaviour
{
    // Singleton instance for easy access
    public static DataManager Instance { get; private set; }

    // PlayerPrefs key for duck names
    private const string PrefsKey = "WacWac_DuckNames";

    // In-memory list of duck names
    private List<string> duckNames = new List<string>();

    /// <summary>
    /// Public read-only access to the duck names list.
    /// </summary>
    public List<string> DuckNames => duckNames;

    private void Awake()
    {
        // Singleton pattern setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Load existing names on startup
        LoadDuckNames();
    }

    /// <summary>
    /// Saves the current list of duck names to PlayerPrefs (localStorage on WebGL).
    /// </summary>
    public void SaveDuckNames()
    {
        PlayerPrefs.SetString(PrefsKey, string.Join("\n", duckNames));
        PlayerPrefs.Save();
        Debug.Log($"[DataManager] Saved {duckNames.Count} duck names to PlayerPrefs.");
    }

    /// <summary>
    /// Loads duck names from PlayerPrefs into the in-memory list.
    /// </summary>
    public void LoadDuckNames()
    {
        duckNames.Clear();

        string raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(raw))
        {
            string[] lines = raw.Split('\n');
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    duckNames.Add(line.Trim());
            }
        }
        Debug.Log($"[DataManager] Loaded {duckNames.Count} duck names from PlayerPrefs.");
    }

    /// <summary>
    /// Adds a new duck name to the list.
    /// </summary>
    /// <param name="name">The name to add.</param>
    public void AddDuckName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            // Limit name to 10 characters
            string trimmedName = name.Length > 10 ? name.Substring(0, 10) : name;
            duckNames.Add(trimmedName);
            SaveDuckNames();
            Debug.Log($"[DataManager] Added duck name: {trimmedName}");
        }
    }

    /// <summary>
    /// Removes a duck name at the specified index.
    /// </summary>
    /// <param name="index">The index of the name to remove.</param>
    public void RemoveDuckNameAt(int index)
    {
        if (index >= 0 && index < duckNames.Count)
        {
            string removedName = duckNames[index];
            duckNames.RemoveAt(index);
            SaveDuckNames();
            Debug.Log($"[DataManager] Removed duck name: {removedName}");
        }
    }

    /// <summary>
    /// Updates a duck name at the specified index.
    /// </summary>
    /// <param name="index">The index of the name to update.</param>
    /// <param name="newName">The new name value.</param>
    public void UpdateDuckNameAt(int index, string newName)
    {
        if (index >= 0 && index < duckNames.Count && !string.IsNullOrWhiteSpace(newName))
        {
            // Limit name to 10 characters
            string trimmedName = newName.Length > 10 ? newName.Substring(0, 10) : newName;
            duckNames[index] = trimmedName;
            SaveDuckNames();
            Debug.Log($"[DataManager] Updated duck name at index {index} to: {trimmedName}");
        }
    }

    /// <summary>
    /// Clears all duck names from the list and saves.
    /// </summary>
    public void ClearAllDuckNames()
    {
        duckNames.Clear();
        SaveDuckNames();
        Debug.Log("[DataManager] Cleared all duck names.");
    }

    /// <summary>
    /// Gets the count of duck names.
    /// </summary>
    public int GetDuckCount()
    {
        return duckNames.Count;
    }
}
