using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// RaceController - Progress-based Directed Race System
/// 
/// Core concepts:
/// - Outcome-driven: Winner and rankings decided at Start using deterministic seed.
/// - Progress P: position calculated as x = startX + (P/100) * L
/// - P range: [-20, 105]
/// - No physics: pure Transform updates
/// - 3 Phases: Opening, Midgame, Sprint
/// </summary>
public enum RacePhase
{
    Opening,    // First 10-15s: scrambled, P in [-10, 30]
    Midgame,    // Until last 3s: elastic logic, losers drift back, contenders in [10, 60]
    Sprint      // Last 3s: deterministic move to FinalP
}

public class RaceController : MonoBehaviour
{
    #region Phase Config Data

    /// <summary>
    /// Configuration for duck behavior in a specific phase.
    /// All fields are public for Inspector tuning.
    /// </summary>
    [System.Serializable]
    public class PhaseConfig
    {
        [Tooltip("Min-Max thời gian (giây) giữa mỗi lần random hành động mới.")]
        public Vector2 actionFrequencyRange = new Vector2(4f, 6f);

        [Tooltip("Min-Max tốc độ tiến (Forward). Dùng để random tốc độ khi hành động là Tiến.")]
        public Vector2 forwardSpeedRange = new Vector2(0.1f, 0.5f);

        [Tooltip("Min-Max tốc độ lùi (Backward). Dùng để random tốc độ khi hành động là Lùi.")]
        public Vector2 backwardSpeedRange = new Vector2(0.1f, 0.3f);
    }

    #endregion

    #region Inspector Fields
    
    [Header("UI")]
    public TextMeshProUGUI countdownText;
    public Button startButton;
    public Button pauseButton;   // Được dùng như nút toggle Pause/Continue
    public Button continueButton; // Không còn dùng, giữ lại chỉ để không vỡ Inspector cũ
    public Button clearButton;
    public Button backButton;

    [Header("Pause Toggle Visuals")]
    [SerializeField] private Sprite pauseSprite;     // Sprite mặc định (Pause)
    [SerializeField] private Sprite continueSprite;  // Sprite khi đang Paused (Continue)
    
    [Header("Intro Flag Animation")]
    [SerializeField] private Transform flagImage;   // Flag_Image trong RaceScene (UI hoặc world)
    [SerializeField] private Transform targetA;     // Target_A mà cờ sẽ di chuyển tới
    [SerializeField] private float flagMoveDuration = 0.7f; // Thời gian animation easeInBack

    [Header("Anchors")]
    [Tooltip("RectTransform của vùng Spawn. Vịt được sinh làm con của RectTransform này. Width/Height dùng cho công thức spawn đường chéo.")]
    public RectTransform spawnArea;

    [Tooltip("RectTransform EndPoint (điểm đích mà Winner đạt 100% tiến độ, thường là tâm màn hình hoặc điểm kết thúc đường đua).")]
    public RectTransform endPoint;

    [Tooltip("RectTransform của Finish (vạch đích) – đối tượng di chuyển từ phải qua trái và dừng tại EndPoint.")]
    public RectTransform finishAnchor;

    [Header("Duck Spawning")]
    public GameObject duckPrefab;

    [Header("Race Config")]
    [Tooltip("Tổng thời lượng của cuộc đua (giây). Thời gian đếm ngược từ giá trị này về 0.")]
    public float raceDuration = 30f;
    
    [Tooltip("Số lượng vịt trong cuộc đua.")]
    public int duckCount = 6;
    
    [Tooltip("Thời gian nước rút T3 (giây cuối). Khi thời gian còn lại <= giá trị này thì toàn bộ vịt bước vào Phase Sprint.")]
    [SerializeField] private float sprintDuration = 3f;

    [Header("Phase Config - Phase 1 (Opening)")]
    [Tooltip("Cấu hình hành vi vịt trong Phase 1. Khoảng giá trị thấp hơn Phase 2.")]
    public PhaseConfig phase1Config = new PhaseConfig
    {
        actionFrequencyRange = new Vector2(4f, 6f),
        forwardSpeedRange = new Vector2(0.1f, 0.5f),
        backwardSpeedRange = new Vector2(0.1f, 0.3f)
    };

    [Header("Phase Config - Phase 2 (Midgame)")]
    [Tooltip("Cấu hình hành vi vịt trong Phase 2. Khoảng giá trị cao hơn Phase 1.")]
    public PhaseConfig phase2Config = new PhaseConfig
    {
        actionFrequencyRange = new Vector2(4f, 6f),
        forwardSpeedRange = new Vector2(0.3f, 0.8f),
        backwardSpeedRange = new Vector2(0.1f, 0.5f)
    };

    [Header("Phase Timing (Auto-calculated)")]
    [Tooltip("T_12 = T - T_3. Tổng thời gian cho Phase 1 và Phase 2.")]
    [SerializeField, HideInInspector] private float phase12Duration;
    
    [Tooltip("T_1: Opening phase = 40% of T_12.")]
    [SerializeField, HideInInspector] private float phase1Duration;
    
    [Tooltip("T_2: Midgame phase = 60% of T_12.")]
    [SerializeField, HideInInspector] private float phase2Duration;

    /// <summary>T3: Sprint duration (set by user)</summary>
    public float T3 => sprintDuration;
    
    /// <summary>T1: Opening phase duration</summary>
    public float T1 => phase1Duration;
    
    /// <summary>T2: Midgame phase duration</summary>
    public float T2 => phase2Duration;

    [Header("Finish Movement (auto)")]
    [Tooltip("Bật/tắt cơ chế tự động cho Finish di chuyển từ phải qua trái để dừng đúng tại EndPoint vào cuối cuộc đua.")]
    public bool enableFinishAutoMove = true;

    [Tooltip("Tốc độ di chuyển của Finish (đơn vị anchored UI mỗi giây). Dương: tự động xác định hướng dựa trên vị trí Start/End.")]
    public float finishSpeed = 500f;

    #endregion

    #region Runtime State

    public enum GameState { Loading, Ready, Running, Paused, Finished }
    private GameState state = GameState.Loading;

    /// <summary>Distance L = finishAnchor.x - spawnArea.x (calculated once at start)</summary>
    public float L { get; private set; }

    /// <summary>Current countdown time</summary>
    public float CurrentTime { get; private set; }

    /// <summary>Total race duration</summary>
    public float TotalTime => raceDuration;

    /// <summary>Legacy compatibility: remaining time alias for CurrentTime.</summary>
    public float remainingTime => CurrentTime;

    /// <summary>Current race phase</summary>
    public RacePhase CurrentPhase { get; private set; }

    /// <summary>Deterministic random seed for this race</summary>
    public int RaceSeed { get; private set; }

    /// <summary>All duck brains in this race</summary>
    private readonly List<DuckBrain> ducks = new List<DuckBrain>();

    /// <summary>Winner duck ID (decided at start)</summary>
    public int WinnerDuckId { get; private set; }

    /// <summary>Final rankings (DuckID sorted by FinalP descending)</summary>
    private List<int> finalRankings = new List<int>();

    /// <summary>Shared RNG for deterministic behavior</summary>
    private System.Random rng;

    // --- Finish auto-move state ---
    private float finishTravelTime;   // Thời gian cần để Finish đi từ vị trí ban đầu tới EndPoint với finishSpeed
    private float finishStartTime;    // Thời gian còn lại (CurrentTime) khi Finish bắt đầu di chuyển
    private bool finishMoving;        // Cờ đang di chuyển Finish

    // --- Finish initial position (for reset on Clear) ---
    private Vector2 finishInitialAnchoredPosition;
    private bool finishInitialPositionSaved;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Wire up buttons
        if (startButton != null) startButton.onClick.AddListener(OnStartPressed);
        if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseTogglePressed);
        if (clearButton != null) clearButton.onClick.AddListener(OnClearPressed);
        if (backButton != null) backButton.onClick.AddListener(OnBackPressed);

        // Load config from RaceConfig singleton if available
        if (RaceConfig.Instance != null)
        {
            raceDuration = RaceConfig.Instance.durationSeconds;
            duckCount = Mathf.Max(3, RaceConfig.Instance.duckCount);
        }

        StartCoroutine(InitializeRace());

        // Khởi tạo sprite nút pause/continue theo trạng thái ban đầu
        UpdatePauseButtonVisual();
    }

    private void Update()
    {
        if (state != GameState.Running) return;

        // Update timer
        CurrentTime -= Time.deltaTime;
        if (CurrentTime < 0f) CurrentTime = 0f;

        // Determine phase
        CurrentPhase = DeterminePhase();

        // Move Finish (vạch đích) nếu bật auto-move
        UpdateFinishMovement(Time.deltaTime);

        // Update all ducks
        for (int i = 0; i < ducks.Count; i++)
        {
            ducks[i].Tick(CurrentPhase, Time.deltaTime, CurrentTime);
        }

        // Update UI
        UpdateCountdownUI();

        // Check finish
        if (CurrentTime <= 0f)
        {
            FinishRace();
        }
    }

    #endregion

    #region Initialization

    private IEnumerator InitializeRace()
    {
        state = GameState.Loading;

        // Save finish initial position on first init
        if (!finishInitialPositionSaved && finishAnchor != null)
        {
            finishInitialAnchoredPosition = finishAnchor.anchoredPosition;
            finishInitialPositionSaved = true;
        }

        // Calculate L
        CalculateL();

        // Create seed
        RaceSeed = Environment.TickCount;
        rng = new System.Random(RaceSeed);

        // Calculate phase timing (fixed 40/60 split)
        CalculatePhaseTiming();

        // Spawn ducks
        yield return StartCoroutine(SpawnDucks());

        // Assign outcome (winner + final P values)
        AssignOutcome();

        // Initialize timer
        CurrentTime = raceDuration;
        CurrentPhase = RacePhase.Opening;

        state = GameState.Ready;

        UpdateCountdownUI();

        // Sau khi spam ducks xong, chạy intro Flag_Image -> Target_A bằng easeInBack
        if (flagImage != null && targetA != null && flagMoveDuration > 0f)
        {
            StartCoroutine(PlayFlagIntroAnimation());
        }
    }

    private void CalculateL()
    {
        if (spawnArea == null || endPoint == null)
        {
            Debug.LogError("RaceController: spawnArea hoặc endPoint chưa được gán!");
            L = 100f; // fallback
            return;
        }

        // Độ dài đường đua dùng cho Progress P:
        // L = khoảng cách anchored X từ SpawnArea tới EndPoint.
        L = endPoint.anchoredPosition.x - spawnArea.anchoredPosition.x;
        Debug.Log($"RaceController: L = {L:F2} (anchored units từ SpawnArea tới EndPoint)");

        // Thiết lập lịch di chuyển cho Finish (nếu được bật)
        SetupFinishMovement();
    }

    private IEnumerator SpawnDucks()
    {
        // Clear existing ducks from spawnArea
        ducks.Clear();
        if (spawnArea != null)
        {
            for (int i = spawnArea.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(spawnArea.GetChild(i).gameObject);
            }
        }

        if (duckPrefab == null)
        {
            // Try from RaceConfig
            if (RaceConfig.Instance != null && RaceConfig.Instance.duckPrefab != null)
                duckPrefab = RaceConfig.Instance.duckPrefab;
        }

        if (duckPrefab == null)
        {
            Debug.LogError("RaceController: duckPrefab not assigned!");
            yield break;
        }

        if (spawnArea == null)
        {
            Debug.LogError("RaceController: spawnArea not assigned!");
            yield break;
        }

        // Get SpawnArea dimensions from RectTransform
        float frameWidth = spawnArea.rect.width;
        float frameHeight = spawnArea.rect.height;
        int n = duckCount;
        
        Debug.Log($"RaceController: SpawnArea size = {frameWidth} x {frameHeight}, ducks = {n}");

        // Get skins
        Sprite[] skins = null;
        if (RaceConfig.Instance != null && RaceConfig.Instance.duckSkins != null)
            skins = RaceConfig.Instance.duckSkins;

        // Get names
        string[] names = null;
        if (RaceConfig.Instance != null && RaceConfig.Instance.duckNames != null)
            names = RaceConfig.Instance.duckNames;

        // Decide whether to use names or numbers for labels based on RaceConfig preference
        bool useNames = false;
        RaceConfig cfg = RaceConfig.Instance;
        if (cfg != null)
        {
            switch (cfg.namePreference)
            {
                case RaceConfig.NameSourcePreference.PreferNumbers:
                    useNames = false; // luôn dùng số, bỏ qua danh sách tên nếu có
                    break;
                case RaceConfig.NameSourcePreference.PreferNames:
                    useNames = (names != null && names.Length > 0);
                    break;
                default: // Auto
                    useNames = (names != null && names.Length > 0);
                    break;
            }
        }
        else
        {
            // Không có RaceConfig -> fallback: nếu có danh sách tên thì dùng, không thì dùng số
            useNames = (names != null && names.Length > 0);
        }

        // SPAWN ASCENDING: k=1 to n ensures ducks[i].DuckId == i
        // Z-ORDER: Use SetAsFirstSibling() so earlier spawned ducks (higher Y, top lanes) 
        // are pushed behind later spawned ducks (lower Y, bottom lanes)
        for (int k = 1; k <= n; k++)
        {
            int duckId = k - 1; // 0-indexed duck ID (nội bộ): ducks[0]=DuckId 0, ducks[1]=DuckId 1, etc.

            // Calculate spawn position using diagonal formula:
            // x = (Width * k) / (n + 1)
            // y = (Height * k) / (n + 1)
            float localX = (frameWidth * k) / (n + 1f);
            float localY = (frameHeight * k) / (n + 1f);

            // Instantiate as child of spawnArea (RectTransform)
            GameObject go = Instantiate(duckPrefab, spawnArea);
            // Đặt tên hiển thị 1-based: Duck_1, Duck_2, ...
            go.name = $"Duck_{duckId + 1}";
            
            // Z-ORDER: Push earlier spawned (top lanes) behind later spawned (bottom lanes)
            go.transform.SetAsFirstSibling();
            
            // Set anchored position within spawnArea
            RectTransform duckRect = go.GetComponent<RectTransform>();
            if (duckRect != null)
            {
                // Position relative to spawnArea's lower-left corner
                duckRect.anchoredPosition = new Vector2(localX, localY);
            }
            else
            {
                // Fallback for non-UI prefab
                go.transform.localPosition = new Vector3(localX, localY, 0f);
            }

            // Assign skin
            if (skins != null && skins.Length > 0)
            {
                var img = go.GetComponentInChildren<Image>();
                if (img != null)
                {
                    int skinIdx = duckId % skins.Length;
                    img.sprite = skins[skinIdx];
                }
            }

            // Assign name label
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                if (useNames && names != null && duckId < names.Length && !string.IsNullOrWhiteSpace(names[duckId]))
                {
                    // Đang ở chế độ tên: chỉ hiển thị tên, không trộn số
                    label.text = names[duckId];
                }
                else
                {
                    // Đang ở chế độ số (hoặc không có tên hợp lệ): chỉ hiển thị số thứ tự
                    label.text = (duckId + 1).ToString();
                }
            }

            // Get or add DuckBrain
            DuckBrain brain = go.GetComponent<DuckBrain>();
            if (brain == null) brain = go.AddComponent<DuckBrain>();

            // Initialize brain with spawn data (no personality needed)
            float startX = localX; // relative to spawnArea
            float startY = localY;
            brain.Initialize(this, duckId, startX, startY, rng);

            ducks.Add(brain);

            // Stagger spawn for visual feedback
            yield return null;
        }

        Debug.Log($"RaceController: Spawned {ducks.Count} ducks");
    }
    
    /// <summary>
    /// Assign winner and final P values for all ducks (outcome-driven)
    /// </summary>
    private void AssignOutcome()
    {
        if (ducks.Count == 0) return;

        // Pick winner randomly
        WinnerDuckId = rng.Next(0, ducks.Count);

        // Assign FinalP values theo "độ hoàn thành" (0..100):
        // - Winner luôn đạt 100%: đi đúng tới EndPoint.
        // - Các vịt còn lại nhận giá trị trong khoảng [~20%, ~95%] để có con về gần đích, có con tụt hậu.

        List<int> otherIds = new List<int>();
        for (int i = 0; i < ducks.Count; i++)
        {
            if (i != WinnerDuckId) otherIds.Add(i);
        }

        // Shuffle others for random ranking
        ShuffleList(otherIds, rng);

        // Winner = 100% tiến độ (đi tới EndPoint)
        ducks[WinnerDuckId].SetFinalP(100f);

        int otherCount = otherIds.Count;
        if (otherCount > 0)
        {
            // Three-band distribution to avoid clustered finishes when duckCount is high
            float[] bandRatios = { 0.50f, 0.30f, 0.20f }; // low / mid / high
            float[] bandMins = { -50f, 0f, 50f };
            float[] bandMaxs = { 0f, 50f, 80f };

            int bandLength = bandRatios.Length;
            int[] bandCounts = new int[bandLength];
            float[] desiredCounts = new float[bandLength];
            int allocated = 0;

            for (int i = 0; i < bandLength; i++)
            {
                float desired = bandRatios[i] * otherCount;
                desiredCounts[i] = desired;
                bandCounts[i] = Mathf.FloorToInt(desired);
                allocated += bandCounts[i];
            }

            int remainder = otherCount - allocated;
            while (remainder > 0)
            {
                int bestIndex = 0;
                float bestFraction = float.MinValue;
                for (int i = 0; i < bandLength; i++)
                {
                    float fraction = desiredCounts[i] - bandCounts[i];
                    if (fraction > bestFraction)
                    {
                        bestFraction = fraction;
                        bestIndex = i;
                    }
                }

                bandCounts[bestIndex]++;
                remainder--;
            }

            List<float> pooledPValues = new List<float>(otherCount);
            for (int band = 0; band < bandLength; band++)
            {
                float min = bandMins[band];
                float max = bandMaxs[band];
                for (int count = 0; count < bandCounts[band]; count++)
                {
                    float roll = min + (float)rng.NextDouble() * (max - min);
                    pooledPValues.Add(roll);
                }
            }

            ShuffleList(pooledPValues, rng);

            for (int i = 0; i < otherIds.Count; i++)
            {
                int duckId = otherIds[i];
                float finalP = Mathf.Clamp(pooledPValues[i], -50f, 80f);
                ducks[duckId].SetFinalP(finalP);
            }
        }

        // Build final rankings
        finalRankings.Clear();
        List<(int id, float p)> sorted = new List<(int, float)>();
        for (int i = 0; i < ducks.Count; i++)
        {
            sorted.Add((i, ducks[i].FinalP));
        }
        sorted.Sort((a, b) => b.p.CompareTo(a.p)); // descending
        foreach (var item in sorted)
            finalRankings.Add(item.id);

        // Log dùng chỉ số 1-based cho dễ đối chiếu với số trên thân vịt
        string rankingsOneBased = string.Join(",", finalRankings.ConvertAll(id => id + 1));
        Debug.Log($"RaceController: Winner = Duck {WinnerDuckId + 1}, Rankings = [{rankingsOneBased}]");
    }

    #endregion

    #region Phase Logic

    /// <summary>
    /// Calculate phase durations using fixed 40/60 split:
    /// T_12 = T - T_3
    /// T_1 = 40% of T_12
    /// T_2 = 60% of T_12
    /// </summary>
    private void CalculatePhaseTiming()
    {
        // T_12 = Total time minus sprint duration
        phase12Duration = raceDuration - sprintDuration;
        
        // T_1 = fixed 40% of T_12
        phase1Duration = phase12Duration * 0.4f;
        
        // T_2 = fixed 60% of T_12
        phase2Duration = phase12Duration * 0.6f;
        
        Debug.Log($"RaceController: Phase Timing - T={raceDuration}s, T3={sprintDuration}s");
        Debug.Log($"RaceController: T_12={phase12Duration}s, T_1={phase1Duration}s, T_2={phase2Duration}s");
    }

    /// <summary>
    /// Get the PhaseConfig for a given phase.
    /// Used by DuckBrain to get action frequency and speed ranges.
    /// </summary>
    public PhaseConfig GetPhaseConfig(RacePhase phase)
    {
        switch (phase)
        {
            case RacePhase.Opening:
                return phase1Config;
            case RacePhase.Midgame:
                return phase2Config;
            default:
                return phase2Config; // Sprint doesn't use PhaseConfig but return something valid
        }
    }

    private RacePhase DeterminePhase()
    {
        // Calculate elapsed time from start
        float elapsed = raceDuration - CurrentTime;
        
        // Phase 1 (Opening): 0 to T_1
        if (elapsed < phase1Duration)
            return RacePhase.Opening;
        
        // Phase 2 (Midgame): T_1 to (T_1 + T_2)
        if (elapsed < phase1Duration + phase2Duration)
            return RacePhase.Midgame;
        
        // Phase 3 (Sprint): Last T_3 seconds
        return RacePhase.Sprint;
    }

    #endregion

    #region Public API for DuckBrain

    /// <summary>
    /// Chuyển Progress P thành anchored X (local trong SpawnArea) cho 1 con vịt.
    ///
    /// - startX: vị trí xuất phát (P = 0).
    /// - L: anchored X của EndPoint tính từ SpawnArea.
    /// - P: % hoàn thành [-20 .. 105].
    ///
    /// Công thức: x = startX + (P/100) * (L - startX)
    ///   -> P = 0   => x = startX
    ///   -> P = 100 => x = L (tức cùng X với EndPoint, bất kể startX khác nhau)
    /// </summary>
    public float PToAnchoredX(float startX, float p)
    {
        float t = p / 100f;
        return startX + t * (L - startX);
    }

    /// <summary>
    /// Get spawnArea RectTransform for position calculations
    /// </summary>
    public RectTransform GetSpawnAreaRect()
    {
        return spawnArea;
    }

    /// <summary>
    /// Get current rank of a duck (1 = leader)
    /// </summary>
    public int GetCurrentRank(int duckId)
    {
        // Sort by current P descending
        List<(int id, float p)> sorted = new List<(int, float)>();
        for (int i = 0; i < ducks.Count; i++)
        {
            sorted.Add((i, ducks[i].CurrentP));
        }
        sorted.Sort((a, b) => b.p.CompareTo(a.p));

        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].id == duckId)
                return i + 1; // 1-indexed rank
        }
        return ducks.Count;
    }

    /// <summary>
    /// Get leader's current P
    /// </summary>
    public float GetLeaderP()
    {
        float maxP = float.MinValue;
        for (int i = 0; i < ducks.Count; i++)
        {
            if (ducks[i].CurrentP > maxP)
                maxP = ducks[i].CurrentP;
        }
        return maxP;
    }

    /// <summary>
    /// Get total duck count
    /// </summary>
    public int GetDuckCount() => ducks.Count;

    #endregion

    #region Race Flow

    private void OnStartPressed()
    {
        if (state == GameState.Ready)
        {
            state = GameState.Running;
            Time.timeScale = 1f;
            ResumeAllScrollers();
            UpdatePauseButtonVisual();
        }
        else if (state == GameState.Paused)
        {
            OnContinuePressed();
        }
    }

    /// <summary>
    /// Nút pause duy nhất: khi đang Running thì Pause, khi đang Paused thì Continue.
    /// </summary>
    private void OnPauseTogglePressed()
    {
        if (state == GameState.Running)
        {
            OnPausePressed();
        }
        else if (state == GameState.Paused)
        {
            OnContinuePressed();
        }
    }

    private void OnPausePressed()
    {
        if (state != GameState.Running) return;
        state = GameState.Paused;
        Time.timeScale = 0f;
        PauseAllScrollers();
        UpdatePauseButtonVisual();
    }

    private void OnContinuePressed()
    {
        if (state != GameState.Paused) return;
        state = GameState.Running;
        Time.timeScale = 1f;
        ResumeAllScrollers();
        UpdatePauseButtonVisual();
    }

    private void OnClearPressed()
    {
        Time.timeScale = 1f;
        PauseAllScrollers();
        ResetAllScrollers();

        var leaderboardUI = FindObjectOfType<LeaderboardUI>(true);
        if (leaderboardUI != null)
        {
            leaderboardUI.ResetUI();
        }

        // Reset finish line to initial position
        ResetFinishLine();

        StartCoroutine(InitializeRace());

        // Sau khi clear, đưa nút pause về trạng thái Pause (vì race sẽ Ready)
        UpdatePauseButtonVisual();
    }

    /// <summary>
    /// Reset finish line (vạch đích) back to its initial position.
    /// </summary>
    private void ResetFinishLine()
    {
        if (finishAnchor != null && finishInitialPositionSaved)
        {
            finishAnchor.anchoredPosition = finishInitialAnchoredPosition;
            finishMoving = false;
        }
    }

    private void OnBackPressed()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SetupScene");
    }

    /// <summary>
    /// Cập nhật sprite cho nút Pause/Continue dựa trên trạng thái hiện tại.
    /// - Ready / Running  -> sprite Pause
    /// - Paused           -> sprite Continue
    /// </summary>
    private void UpdatePauseButtonVisual()
    {
        if (pauseButton == null) return;

        var image = pauseButton.GetComponent<Image>();
        if (image == null) return;

        if (state == GameState.Paused)
        {
            if (continueSprite != null)
                image.sprite = continueSprite;
        }
        else
        {
            if (pauseSprite != null)
                image.sprite = pauseSprite;
        }
    }

    /// <summary>
    /// Intro: di chuyển Flag_Image tới Target_A bằng easeInBack khi vào RaceScene.
    /// </summary>
    private IEnumerator PlayFlagIntroAnimation()
    {
        Vector3 startPos = flagImage.position;
        Vector3 endPos = targetA.position;

        float elapsed = 0f;

        while (elapsed < flagMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flagMoveDuration);

            // EaseInBack: bắt đầu lùi nhẹ rồi mới tiến tới đích
            float eased = EaseInBack(t);
            flagImage.position = Vector3.Lerp(startPos, endPos, eased);

            yield return null;
        }

        flagImage.position = endPos;
    }

    private void FinishRace()
    {
        state = GameState.Finished;

        // Pause background scrollers
        PauseAllScrollers();

        // Snap all ducks to final P
        for (int i = 0; i < ducks.Count; i++)
        {
            ducks[i].SnapToFinal();
        }

        // Display leaderboard
        DisplayLeaderboard();

        Debug.Log("Race Finished!");
    }

    /// <summary>
    /// Easing hàm EaseInBack (0..1) -> 0..1.
    /// Bắt đầu chậm, hơi lùi lại rồi tăng tốc về đích.
    /// </summary>
    private float EaseInBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        // Công thức chuẩn EaseInBack
        return c3 * t * t * t - c1 * t * t;
    }

    private void DisplayLeaderboard()
    {
        // Find LeaderboardUI if exists
        var leaderboardUI = FindObjectOfType<LeaderboardUI>(true);
        if (leaderboardUI != null)
        {
            leaderboardUI.Show();
            // Convert ducks list to List<DuckBrain> for the leaderboard
            leaderboardUI.UpdateLeaderboard(ducks);
        }

        // Log final rankings
        Debug.Log($"Final Rankings: Winner = Duck {WinnerDuckId + 1}");
        for (int i = 0; i < finalRankings.Count; i++)
        {
            int id = finalRankings[i];
            var duck = ducks[id];
            Debug.Log($"  Rank {i + 1}: Duck {id + 1} (P = {duck.FinalP:F1})");
        }
    }

    /// <summary>
    /// Get list of all ducks for leaderboard updates
    /// </summary>
    public List<DuckBrain> GetDucks() => ducks;

    #endregion

    #region UI

    private void UpdateCountdownUI()
    {
        if (countdownText == null) return;

        int totalSec = Mathf.CeilToInt(CurrentTime);
        int min = totalSec / 60;
        int sec = totalSec % 60;
        countdownText.text = $"{min:D2}:{sec:D2}";
    }

    #endregion

    #region Utility

    private static void ShuffleList<T>(List<T> list, System.Random rng)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T temp = list[k];
            list[k] = list[n];
            list[n] = temp;
        }
    }

    /// <summary>
    /// Tính toán lịch di chuyển cho Finish (vạch đích) sao cho với tốc độ finishSpeed
    /// nó sẽ bắt đầu di chuyển ở thời điểm CurrentTime ~= finishStartTime và dừng
    /// đúng tại EndPoint khi CurrentTime về 0.
    /// </summary>
    private void SetupFinishMovement()
    {
        finishMoving = false;
        finishTravelTime = 0f;
        finishStartTime = 0f;

        if (!enableFinishAutoMove || finishAnchor == null || endPoint == null)
            return;

        float startX = finishAnchor.anchoredPosition.x;
        float targetX = endPoint.anchoredPosition.x;
        float distance = targetX - startX; // âm nếu đi từ phải sang trái

        float speedAbs = Mathf.Abs(finishSpeed);
        if (speedAbs < 0.01f || Mathf.Abs(distance) < 0.01f)
            return;

        // Thời gian cần để di chuyển từ startX -> targetX với tốc độ finishSpeed
        finishTravelTime = Mathf.Abs(distance) / speedAbs;

        // Ta muốn Finish bắt đầu di chuyển khi thời gian còn lại == thời gian cần di chuyển
        finishStartTime = Mathf.Min(raceDuration, finishTravelTime);

        Debug.Log($"RaceController: Finish travelTime={finishTravelTime:F2}s, finishStartTime={finishStartTime:F2}s, distance={distance:F1}");
    }

    /// <summary>
    /// Cập nhật chuyển động Finish mỗi frame khi race đang chạy.
    /// </summary>
    private void UpdateFinishMovement(float deltaTime)
    {
        if (!enableFinishAutoMove || finishAnchor == null || endPoint == null)
            return;

        // Khi thời gian còn lại <= finishStartTime thì bắt đầu cho Finish di chuyển
        if (!finishMoving && CurrentTime <= finishStartTime && CurrentTime > 0f)
        {
            finishMoving = true;
        }

        if (!finishMoving)
            return;

        Vector2 pos = finishAnchor.anchoredPosition;
        float targetX = endPoint.anchoredPosition.x;
        float remaining = targetX - pos.x;

        if (Mathf.Abs(remaining) < 0.01f)
        {
            // Đã tới EndPoint
            pos.x = targetX;
            finishAnchor.anchoredPosition = pos;
            finishMoving = false;
            return;
        }

        float dir = Mathf.Sign(remaining); // tự xác định nên đi trái hay phải
        float step = Mathf.Abs(finishSpeed) * dir * deltaTime;

        // Nếu step sẽ vượt quá target thì clamp lại đúng target
        if (Mathf.Abs(step) >= Mathf.Abs(remaining))
        {
            pos.x = targetX;
            finishMoving = false;
        }
        else
        {
            pos.x += step;
        }

        finishAnchor.anchoredPosition = pos;
    }

    /// <summary>
    /// Resume all Scroller instances in scene (for background movement)
    /// </summary>
    private void ResumeAllScrollers()
    {
        var scrollers = FindObjectsOfType<Scroller>();
        foreach (var s in scrollers)
        {
            s.Resume();
        }
    }

    /// <summary>
    /// Pause all Scroller instances in scene
    /// </summary>
    private void PauseAllScrollers()
    {
        var scrollers = FindObjectsOfType<Scroller>();
        foreach (var s in scrollers)
        {
            s.Pause();
        }
    }

    /// <summary>
    /// Reset all Scroller instances to starting position
    /// </summary>
    private void ResetAllScrollers()
    {
        var scrollers = FindObjectsOfType<Scroller>();
        foreach (var s in scrollers)
        {
            s.ResetToStart();
        }
    }

    #endregion

    #region Public State Queries

    public bool IsRunning() => state == GameState.Running;
    public bool IsFinished() => state == GameState.Finished;
    public GameState GetState() => state;

    #endregion
}
