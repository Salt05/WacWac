using UnityEngine;

// L?u c?u hình cu?c ?ua - các h? th?ng khác có th? ??c
public class RaceConfig : MonoBehaviour
{
    public static RaceConfig Instance { get; private set; }

    [Tooltip("Th?i gian cu?c ?ua tính b?ng giây (t?i thi?u 10s)")]
    public int durationSeconds = 15; // m?c ??nh 15s

    [Tooltip("S? l??ng v?t trong cu?c ?ua (t?i thi?u 3)")]
    public int duckCount = 5; // m?c ??nh 5

    [Tooltip("Danh sách skin v?t (gán trong Inspector)")]
    public Sprite[] duckSkins;

    [Tooltip("Prefab v?t (gán trong Inspector)")]
    public GameObject duckPrefab;

    [Tooltip("Tên các v?t (g?n t? UI) - optional")]
    public string[] duckNames;

    [Tooltip("Raw text nh?p tên (gi?ng format multiline)")]
    public string duckNamesRaw = "";

    // Preference for whether to use names or numbers when spawning.
    public enum NameSourcePreference { Auto = 0, PreferNumbers = 1, PreferNames = 2 }

    [Tooltip("Preference khi c? tên và s? l??ng t?n t?i: Auto = dùng tên n?u có, PreferNumbers = b?t bu?c s?, PreferNames = ?u tiên tên (n?u có)")]
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
    }
}
