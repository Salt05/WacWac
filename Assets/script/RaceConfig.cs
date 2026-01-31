using UnityEngine;

// L?u c?u hình cu?c ?ua ?? các h? th?ng khác có th? ??c
public class RaceConfig : MonoBehaviour
{
    public static RaceConfig Instance { get; private set; }

    [Tooltip("Th?i gian cu?c ?ua tính b?ng giây")]
    public int durationSeconds = 15; // m?c ??nh 15s

    [Tooltip("S? l??ng v?t trong cu?c ?ua")]
    public int duckCount = 5; // m?c ??nh 5

    [Tooltip("Danh sách skin v?t (gán trong Inspector)")]
    public Sprite[] duckSkins;

    [Tooltip("Prefab v?t (gán trong Inspector)")]
    public GameObject duckPrefab;

    [Tooltip("Tên các v?t (gán t? UI) - optional")]
    public string[] duckNames;

    [Tooltip("Raw text nh?p tên (gi? gi?ng format multiline)")]
    public string duckNamesRaw = "";

    // Preference for whether to use names or numbers when spawning.
    public enum NameSourcePreference { Auto = 0, PreferNumbers = 1, PreferNames = 2 }

    [Tooltip("Preference when both names and counts exist: Auto = use names if provided, PreferNumbers = force numeric labels, PreferNames = force names (if any)")]
    public NameSourcePreference namePreference = NameSourcePreference.Auto;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }
}
