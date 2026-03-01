using UnityEngine;

// L?u c?u h�nh cu?c ?ua - c�c h? th?ng kh�c c� th? ??c
public class RaceConfig : MonoBehaviour
{
    public static RaceConfig Instance { get; private set; }

    [Tooltip("Th?i gian cu?c ?ua t�nh b?ng gi�y (t?i thi?u 10s)")]
    public int durationSeconds = 15; // m?c ??nh 15s

    [Tooltip("S? l??ng v?t trong cu?c ?ua (t?i thi?u 3)")]
    public int duckCount = 5; // m?c ??nh 5

    [Tooltip("Gi� tr? quantity ngu?i ch?i set ? ch? ?? s? (kh�ng ph? thu?c danh s�ch t�n)")]
    public int quantityNumeric = 5; // l??ng v?t theo quantity mode

    [Tooltip("Danh s�ch skin v?t (g�n trong Inspector)")]
    public Sprite[] duckSkins;

    [Tooltip("Prefab v?t (g�n trong Inspector)")]
    public GameObject duckPrefab;

    [Tooltip("T�n c�c v?t (g?n t? UI) - optional")]
    public string[] duckNames;

    [Tooltip("Raw text nh?p t�n (gi?ng format multiline)")]
    public string duckNamesRaw = "";

    // Preference for whether to use names or numbers when spawning.
    public enum NameSourcePreference { Auto = 0, PreferNumbers = 1, PreferNames = 2 }

    [Tooltip("Preference khi c? t�n v� s? l??ng t?n t?i: Auto = d�ng t�n n?u c�, PreferNumbers = b?t bu?c s?, PreferNames = ?u ti�n t�n (n?u c�)")]
    public NameSourcePreference namePreference = NameSourcePreference.Auto;

    private const int MinDurationSeconds = 10;
    private const int MinDuckCount = 3;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        // Enforce minimums so user input cannot go below allowed values
        if (durationSeconds < MinDurationSeconds)
        {
            Debug.LogWarning($"RaceConfig.durationSeconds was below minimum ({durationSeconds} < {MinDurationSeconds}), clamping to {MinDurationSeconds}.");
            durationSeconds = MinDurationSeconds;
        }
        if (duckCount < MinDuckCount)
        {
            Debug.LogWarning($"RaceConfig.duckCount was below minimum ({duckCount} < {MinDuckCount}), clamping to {MinDuckCount}.");
            duckCount = MinDuckCount;
        }
        if (quantityNumeric < MinDuckCount)
        {
            // N?u quantityNumeric ch?a ??c thi?t l?p, d�ng duckCount hi?n t?i l�m m?c g?c
            quantityNumeric = duckCount;
        }
    }

    // Ensure values are clamped immediately when edited in the Inspector
    private void OnValidate()
    {
        if (durationSeconds < MinDurationSeconds)
        {
            Debug.LogWarning($"RaceConfig: durationSeconds was below minimum ({durationSeconds} < {MinDurationSeconds}), setting to {MinDurationSeconds}.");
            durationSeconds = MinDurationSeconds;
        }
        if (duckCount < MinDuckCount)
        {
            Debug.LogWarning($"RaceConfig: duckCount was below minimum ({duckCount} < {MinDuckCount}), setting to {MinDuckCount}.");
            duckCount = MinDuckCount;
        }
        if (quantityNumeric < MinDuckCount)
        {
            quantityNumeric = duckCount;
        }
    }
}
