using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Manages race lifecycle: LOADING (UI) -> READY -> RUNNING -> PAUSED
// Finish movement: start moving at stopDuckTimeD with fixed UI speed (anchored units/sec).
public class RaceController : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI countdownText;
    public Button startButton;
    public Button pauseButton;
    public Button continueButton;
    public Button clearButton;
    public Button backButton;

    [Header("Spawn / Ducks")]
    public RectTransform spawnArea;
    public Transform duckParent;
    public GameObject loadingPanel;
    public float minLoadingTime = 1.0f;

    [Header("Duck Movement Params (Inspector)")]
    public float speedMinA = 0.5f;
    public float speedMaxB = 2.0f;
    public float randomIntervalC = 1.0f;
    public float stopDuckTimeD = 1.0f;

    [Header("Duck Clamp (World Space)")]
    [Tooltip("Assign Transforms placed at desired min/max clamp X positions in world space. If empty, defaults are used.")]
    public Transform minPosTransform;
    public Transform maxPosTransform;

    // Defaults used when transforms are not assigned
    private const float DefaultMinPosX = -10f;
    private const float DefaultMaxPosX = 10f;

    [Header("Finish")]
    [Tooltip("Existing finish object in the scene (UI RectTransform) - assign here.")]
    public Transform finishTransform;

    [Tooltip("Optional target object (UI RectTransform) to move finish towards during sprint. If null, finish moves until ducks.")]
    public Transform finishTargetTransform;

    [Tooltip("Finish movement speed in UI anchored units per second (used when no target or when not auto-calc).")]
    public float finishSpeedF = 5f;

    [Tooltip("Remaining time in seconds (updated while running)")]
    public float remainingTime;

    [Tooltip("Total race time in seconds")]
    public float totalRaceTime;

    private enum State { Loading, Ready, Running, Paused }
    private State state = State.Loading;

    private bool hasSpawned = false;

    private readonly List<DuckMover> duckMovers = new List<DuckMover>();
    private readonly List<Vector3> initialPositions = new List<Vector3>();
    private readonly List<Quaternion> initialRotations = new List<Quaternion>();

    // finish timing (UI space)
    private RectTransform finishRect;
    private RectTransform finishTargetRect;

    // snapshot when ducks freeze / movement control
    private bool finishMoveStarted;
    private bool finishReachedTarget;
    private float finishStartX;
    private float frozenDuckMaxX_UI;

    // auto-speed for finish when target provided
    private bool useAutoFinishSpeed;
    private float finishAutoSpeed; // anchored units/sec
    private float finishFinalTargetX;

    // leader sprint
    private bool leaderSprintStarted;

    // ranking
    private List<DuckMover> finalRankingList;

    private float previousTimeScale = 1f;

    // Computed clamp X values (world-space)
    private float MinPosX => minPosTransform != null ? minPosTransform.position.x : DefaultMinPosX;
    private float MaxPosX => maxPosTransform != null ? maxPosTransform.position.x : DefaultMaxPosX;

    /// <summary>
    /// Random seed created per "race session" so duck RNG can be deterministic and replayable.
    /// DuckMover combines this with duckIndex.
    /// </summary>
    [NonSerialized]
    public int raceSessionSeed;

    // --- Phase 1.5: live ranking cache (world X) ---
    private readonly List<DuckMover> runningRanking = new List<DuckMover>();
    private readonly Dictionary<DuckMover, int> runningRankByDuck = new Dictionary<DuckMover, int>();

    [Header("Ranking (Runtime)")]
    [Tooltip("How often to recompute live rankings while running (seconds). Lower = more accurate, higher = cheaper.")]
    [Range(0.02f, 0.5f)]
    public float rankingUpdateInterval = 0.10f;

    [Header("Batch Duck Update / LOD")]
    public bool useBatchDuckUpdate = true;

    [Tooltip("Distance for FULL update (all systems).")]
    public float duckLodFullDistance = 30f;

    [Tooltip("Distance for SIMPLIFIED update (no momentum/comeback).")]
    public float duckLodMediumDistance = 100f;

    [Tooltip("If beyond medium distance, MINIMAL update is used.")]
    public float duckLodMinimalDistance = 250f;

    [Header("Spawning Performance")]
    public bool staggerSpawn = true;

    [Tooltip("Seconds between duck instantiations when staggerSpawn is enabled.")]
    [Range(0f, 0.2f)]
    public float staggerSpawnInterval = 0.02f;

    private Camera mainCam;

    private float nextRankingUpdateTime;

    private void Start()
    {
        // Create a session seed once per race controller lifetime.
        // This will be regenerated on Clear/Start (see OnStartPressed) so each race can differ.
        raceSessionSeed = Environment.TickCount;
        nextRankingUpdateTime = 0f;

        totalRaceTime = (RaceConfig.Instance != null) ? RaceConfig.Instance.durationSeconds : 15;
        remainingTime = totalRaceTime;
        UpdateCountdownText();

        startButton.onClick.AddListener(OnStartPressed);
        pauseButton.onClick.AddListener(OnPausePressed);
        continueButton.onClick.AddListener(OnContinuePressed);
        clearButton.onClick.AddListener(OnClearPressed);
        backButton.onClick.AddListener(OnBackPressed);

        CacheFinishRects();
        ResetFinishToSnapshotStart();

        Time.timeScale = 1f;

        mainCam = Camera.main;

        StartCoroutine(LoadAndSpawnRoutine());
    }

    private void CacheFinishRects()
    {
        if (finishRect == null && finishTransform != null)
            finishRect = finishTransform.GetComponent<RectTransform>();

        if (finishTargetRect == null && finishTargetTransform != null)
            finishTargetRect = finishTargetTransform.GetComponent<RectTransform>();
    }

    private IEnumerator LoadAndSpawnRoutine()
    {
        state = State.Loading;
        if (loadingPanel != null) loadingPanel.SetActive(true);

        PauseBackgrounds();

        float t0 = Time.realtimeSinceStartup;

        if (!hasSpawned)
        {
            hasSpawned = true;
            if (staggerSpawn)
                yield return StartCoroutine(SpawnDucksStaggered());
            else
                SpawnDucks();
        }

        float elapsed = Time.realtimeSinceStartup - t0;
        float wait = minLoadingTime - elapsed;
        if (wait > 0f) yield return new WaitForSecondsRealtime(wait);

        if (loadingPanel != null) loadingPanel.SetActive(false);
        state = State.Ready;
    }

    private IEnumerator SpawnDucksStaggered()
    {
        duckMovers.Clear();
        initialPositions.Clear();
        initialRotations.Clear();

        if (RaceConfig.Instance == null)
        {
            Debug.LogWarning("RaceConfig.Instance is null - cannot spawn ducks");
            yield break;
        }

        if (RaceConfig.Instance.duckPrefab == null)
        {
            Debug.LogWarning("duckPrefab not assigned in RaceConfig - cannot spawn ducks");
            yield break;
        }

        // If user provided non-empty names, spawn only those named ducks (ignore duckCount).
        string[] rcNames = RaceConfig.Instance.duckNames;
        List<string> filteredNames = new List<string>();
        if (rcNames != null)
        {
            foreach (var n in rcNames)
            {
                if (!string.IsNullOrWhiteSpace(n))
                    filteredNames.Add(n);
            }
        }

        var pref = RaceConfig.Instance.namePreference;
        bool hasNames = filteredNames.Count > 0;
        bool useNames;
        if (pref == RaceConfig.NameSourcePreference.PreferNumbers)
            useNames = false;
        else if (pref == RaceConfig.NameSourcePreference.PreferNames)
            useNames = hasNames;
        else
            useNames = hasNames;

        int count = useNames ? filteredNames.Count : Mathf.Max(1, RaceConfig.Instance.duckCount);

        if (duckParent == null)
        {
            var goParent = new GameObject("Ducks");
            duckParent = goParent.transform;
        }

        for (int i = duckParent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(duckParent.GetChild(i).gameObject);
        }

        if (spawnArea == null)
        {
            Debug.LogWarning("spawnArea not assigned");
            yield break;
        }

        List<Sprite> skinPool = new List<Sprite>();
        if (RaceConfig.Instance.duckSkins != null) skinPool.AddRange(RaceConfig.Instance.duckSkins);

        bool uniqueSkins = (count <= 10) && (skinPool.Count >= count);
        List<int> usedSkinIndices = new List<int>();

        float height = spawnArea.rect.height;

        float interval = Mathf.Max(0f, staggerSpawnInterval);

        for (int i = 0; i < count; i++)
        {
            float t = (i + 1f) / (count + 1f);
            float localY = Mathf.Lerp(-height / 2f, height / 2f, t);
            Vector3 localPos = new Vector3(0f, localY, 0f);
            Vector3 worldPos = spawnArea.TransformPoint(localPos);

            GameObject go = Instantiate(RaceConfig.Instance.duckPrefab, worldPos, Quaternion.identity, duckParent);

            var img = go.GetComponentInChildren<UnityEngine.UI.Image>();
            if (img != null && skinPool.Count > 0)
            {
                int idx;
                if (uniqueSkins)
                {
                    List<int> choices = new List<int>();
                    for (int k = 0; k < skinPool.Count; k++) if (!usedSkinIndices.Contains(k)) choices.Add(k);
                    idx = choices[UnityEngine.Random.Range(0, choices.Count)];
                    usedSkinIndices.Add(idx);
                }
                else
                {
                    idx = UnityEngine.Random.Range(0, skinPool.Count);
                }
                img.sprite = skinPool[idx];
            }

            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = useNames ? filteredNames[i] : (i + 1).ToString();
            }

            var canv = go.GetComponentInChildren<Canvas>();
            if (canv != null) canv.sortingOrder = count - i;
            else
            {
                Vector3 pz = go.transform.position;
                pz.z = -i * 0.01f;
                go.transform.position = pz;
            }

            var mover = go.GetComponent<DuckMover>();
            if (mover != null)
            {
                mover.Initialize(this);
                duckMovers.Add(mover);
            }
            else
            {
                duckMovers.Add(null);
            }

            initialPositions.Add(go.transform.position);
            initialRotations.Add(go.transform.rotation);

            if (interval > 0f)
                yield return new WaitForSecondsRealtime(interval);
            else
                yield return null; // at least spread over frames
        }
    }

    private void Update()
    {
        if (state != State.Running) return;

        CacheFinishRects();

        // Update live ranking on a fixed interval (cheaper than every frame).
        UpdateRunningRankingThrottled();

        // Batch duck update (optional)
        if (useBatchDuckUpdate)
        {
            BatchUpdateDucks(Time.deltaTime, Time.time);
        }

        // When time drops to or below stopDuckTimeD, snapshot ducks and start finish movement.
        if (!finishMoveStarted && remainingTime <= stopDuckTimeD)
        {
            frozenDuckMaxX_UI = GetMaxDuckX_UI();
            finishStartX = finishRect != null ? finishRect.anchoredPosition.x : 0f;
            finishMoveStarted = true;
            finishReachedTarget = false;
            leaderSprintStarted = false;

            // Determine final target X:
            float targetFromAssigned = float.NaN;
            if (finishTargetRect != null)
                targetFromAssigned = finishTargetRect.anchoredPosition.x;

            // If a target is provided, try to move to it within the available time (remainingTime).
            if (!float.IsNaN(targetFromAssigned))
            {
                // final target must not pass the frozen duck maximum (so we don't move beyond a duck)
                finishFinalTargetX = Mathf.Max(targetFromAssigned, frozenDuckMaxX_UI);

                float distance = Mathf.Abs(finishStartX - finishFinalTargetX);
                float availableTime = Mathf.Max(0.0001f, remainingTime); // avoid div by zero

                finishAutoSpeed = distance / availableTime;
                useAutoFinishSpeed = finishAutoSpeed > 0f;
            }
            else
            {
                // no assigned target -> move until frozenDuckMaxX_UI using fixed finishSpeedF
                finishFinalTargetX = frozenDuckMaxX_UI;
                useAutoFinishSpeed = false;
                finishAutoSpeed = 0f;
            }

            Debug.Log(
                $"time={remainingTime:F4} | finishX={finishRect.anchoredPosition.x:F2} | duckMaxX={frozenDuckMaxX_UI:F2} | finalTargetX={finishFinalTargetX:F2} | autoSpeed={finishAutoSpeed:F2}"
            );
        }

        // If movement started, move finish by calculated speed each frame until it reaches finishFinalTargetX
        if (finishMoveStarted && !finishReachedTarget && finishRect != null)
        {
            float speed = useAutoFinishSpeed ? finishAutoSpeed : Mathf.Max(0f, finishSpeedF);
            float delta = speed * Time.deltaTime;

            Vector2 p = finishRect.anchoredPosition;
            float newX = Mathf.MoveTowards(p.x, finishFinalTargetX, delta);
            p.x = newX;

            if (Mathf.Approximately(newX, finishFinalTargetX))
            {
                finishReachedTarget = true;
            }

            finishRect.anchoredPosition = p;
        }

        // Leader sprint timing: start once after finishMoveStarted, but when remainingTime <= leader's effective D.
        if (finishMoveStarted && !leaderSprintStarted)
        {
            DuckMover leaderByRank = GetLeader();
            if (leaderByRank != null)
            {
                float leaderEffectiveD = leaderByRank.GetEffectiveStopDuckTimeD(stopDuckTimeD);
                if (remainingTime <= leaderEffectiveD)
                {
                    TryStartLeaderSprint(leaderByRank);
                }
            }
        }

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;

            // ensure finish is exactly at the chosen final target when time is up
            if (finishRect != null && finishMoveStarted)
            {
                Vector2 p = finishRect.anchoredPosition;
                p.x = finishFinalTargetX;
                finishRect.anchoredPosition = p;
            }

            OnTimeUp();
        }

        UpdateCountdownText();
    }

    private void BatchUpdateDucks(float dt, float now)
    {
        if (duckMovers == null || duckMovers.Count == 0) return;

        Vector3 camPos = mainCam != null ? mainCam.transform.position : Vector3.zero;
        float fullSq = duckLodFullDistance * duckLodFullDistance;
        float medSq = duckLodMediumDistance * duckLodMediumDistance;

        for (int i = 0; i < duckMovers.Count; i++)
        {
            var d = duckMovers[i];
            if (d == null) continue;

            // disable individual Update to avoid double processing
            if (d.enabled) d.enabled = false;

            if (mainCam == null)
            {
                d.Tick(dt, now);
                continue;
            }

            float dsq = (d.transform.position - camPos).sqrMagnitude;
            if (dsq <= fullSq)
            {
                d.Tick(dt, now);
            }
            else if (dsq <= medSq)
            {
                d.TickSimplified(dt, now);
            }
            else
            {
                d.TickMinimal(dt);
            }
        }
    }

    private void UpdateRunningRankingThrottled()
    {
        if (rankingUpdateInterval <= 0f)
        {
            UpdateRunningRankingNow();
            return;
        }

        if (Time.time < nextRankingUpdateTime) return;

        nextRankingUpdateTime = Time.time + rankingUpdateInterval;
        UpdateRunningRankingNow();
    }

    private void UpdateRunningRankingNow()
    {
        runningRanking.Clear();
        foreach (var d in duckMovers)
        {
            if (d != null) runningRanking.Add(d);
        }

        // Rank by world X descending (leader = max X)
        runningRanking.Sort((a, b) => b.GetWorldX().CompareTo(a.GetWorldX()));

        runningRankByDuck.Clear();
        for (int i = 0; i < runningRanking.Count; i++)
        {
            // 1-based rank for readability
            runningRankByDuck[runningRanking[i]] = i + 1;
        }
    }

    /// <summary>
    /// Returns 1-based rank while running (1 = leader). Returns int.MaxValue if unknown.
    /// </summary>
    public int GetRankOf(DuckMover duck)
    {
        if (duck == null) return int.MaxValue;
        int r;
        if (runningRankByDuck.TryGetValue(duck, out r)) return r;
        return int.MaxValue;
    }

    public DuckMover GetLeader()
    {
        return runningRanking.Count > 0 ? runningRanking[0] : null;
    }

    public int GetRunnerCount()
    {
        return runningRanking.Count;
    }

    private float GetMaxDuckX_UI()
    {
        // Returns max duck X in the same anchored UI space as finishRect.
        // - If duck is UI (has RectTransform): use anchoredPosition.x.
        // - Else duck is world: convert to finishRect parent local X.
        if (duckMovers == null || duckMovers.Count == 0) return 0f;

        CacheFinishRects();
        RectTransform finishParent = finishRect != null ? finishRect.parent as RectTransform : null;

        float maxX = float.NegativeInfinity;
        bool any = false;

        foreach (var d in duckMovers)
        {
            if (d == null) continue;
            var duckRt = d.GetComponent<RectTransform>();
            float x;

            if (duckRt != null)
            {
                x = duckRt.anchoredPosition.x;
            }
            else
            {
                if (finishParent == null) continue;
                x = finishParent.InverseTransformPoint(d.transform.position).x;
            }

            any = true;
            if (x > maxX) maxX = x;
        }

        return any ? maxX : 0f;
    }

    // --- Background helpers ---
    private void PauseBackgrounds()
    {
        var scs = FindObjectsOfType<Scroller>();
        foreach (var s in scs) s.Pause();
    }

    private void ResumeBackgrounds()
    {
        var scs = FindObjectsOfType<Scroller>();
        foreach (var s in scs) s.Resume();
    }

    private void ResetFinishToSnapshotStart()
    {
        CacheFinishRects();
        if (finishRect == null) return;
        finishStartX = finishRect.anchoredPosition.x;
    }

    private void ResetFinishToStartX()
    {
        CacheFinishRects();
        if (finishRect == null) return;
        Vector2 p = finishRect.anchoredPosition;
        p.x = finishStartX;
        finishRect.anchoredPosition = p;
    }

    // --- Spawning ---
    private void SpawnDucksOnce()
    {
        // kept for compatibility; spawning now happens in LoadAndSpawnRoutine
        if (hasSpawned) return;
        hasSpawned = true;
        SpawnDucks();
    }

    private void SpawnDucks()
    {
        duckMovers.Clear();
        initialPositions.Clear();
        initialRotations.Clear();

        if (RaceConfig.Instance == null)
        {
            Debug.LogWarning("RaceConfig.Instance is null - cannot spawn ducks");
            return;
        }

        if (RaceConfig.Instance.duckPrefab == null)
        {
            Debug.LogWarning("duckPrefab not assigned in RaceConfig - cannot spawn ducks");
            return;
        }

        // If user provided non-empty names, spawn only those named ducks (ignore duckCount).
        string[] rcNames = RaceConfig.Instance.duckNames;
        List<string> filteredNames = new List<string>();
        if (rcNames != null)
        {
            foreach (var n in rcNames)
            {
                if (!string.IsNullOrWhiteSpace(n))
                    filteredNames.Add(n);
            }
        }

        // Decide whether to use names based on preference and availability
        var pref = RaceConfig.Instance.namePreference;
        bool hasNames = filteredNames.Count > 0;
        bool useNames;
        if (pref == RaceConfig.NameSourcePreference.PreferNumbers)
            useNames = false;
        else if (pref == RaceConfig.NameSourcePreference.PreferNames)
            useNames = hasNames;
        else // Auto
            useNames = hasNames;

        int count = useNames ? filteredNames.Count : Mathf.Max(1, RaceConfig.Instance.duckCount);

        if (duckParent == null)
        {
            var go = new GameObject("Ducks");
            duckParent = go.transform;
        }

        for (int i = duckParent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(duckParent.GetChild(i).gameObject);
        }

        if (spawnArea == null)
        {
            Debug.LogWarning("spawnArea not assigned");
            return;
        }

        List<Sprite> skinPool = new List<Sprite>();
        if (RaceConfig.Instance.duckSkins != null) skinPool.AddRange(RaceConfig.Instance.duckSkins);

        bool uniqueSkins = (count <= 10) && (skinPool.Count >= count);
        List<int> usedSkinIndices = new List<int>();

        float height = spawnArea.rect.height;

        for (int i = 0; i < count; i++)
        {
            float t = (i + 1f) / (count + 1f);
            float localY = Mathf.Lerp(-height / 2f, height / 2f, t);
            Vector3 localPos = new Vector3(0f, localY, 0f);
            Vector3 worldPos = spawnArea.TransformPoint(localPos);

            GameObject go = Instantiate(RaceConfig.Instance.duckPrefab, worldPos, Quaternion.identity, duckParent);

            var img = go.GetComponentInChildren<UnityEngine.UI.Image>();
            if (img != null && skinPool.Count > 0)
            {
                int idx;
                if (uniqueSkins)
                {
                    List<int> choices = new List<int>();
                    for (int k = 0; k < skinPool.Count; k++) if (!usedSkinIndices.Contains(k)) choices.Add(k);
                    idx = choices[UnityEngine.Random.Range(0, choices.Count)];
                    usedSkinIndices.Add(idx);
                }
                else
                {
                    idx = UnityEngine.Random.Range(0, skinPool.Count);
                }
                img.sprite = skinPool[idx];
            }

            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                if (useNames)
                {
                    // Use the filtered list of non-empty names
                    label.text = filteredNames[i];
                }
                else
                {
                    // Use numeric labels
                    label.text = (i + 1).ToString();
                }
            }

            var canv = go.GetComponentInChildren<Canvas>();
            if (canv != null) canv.sortingOrder = count - i;
            else
            {
                Vector3 pz = go.transform.position;
                pz.z = -i * 0.01f;
                go.transform.position = pz;
            }

            var mover = go.GetComponent<DuckMover>();
            if (mover != null)
            {
                mover.Initialize(this);
                duckMovers.Add(mover);
            }
            else
            {
                duckMovers.Add(null);
            }

            initialPositions.Add(go.transform.position);
            initialRotations.Add(go.transform.rotation);
        }
    }

    // --- Button callbacks ---
    private void OnStartPressed()
    {
        if (state != State.Ready && state != State.Paused) return;

        // New session seed each time user starts a race.
        raceSessionSeed = unchecked(Environment.TickCount ^ (int)DateTime.UtcNow.Ticks);

        runningRanking.Clear();
        runningRankByDuck.Clear();
        nextRankingUpdateTime = 0f;

        state = State.Running;
        ResumeBackgrounds();

        Time.timeScale = 1f;

        if (remainingTime <= 0f) remainingTime = totalRaceTime;

        CacheFinishRects();
        ResetFinishToSnapshotStart();

        finishMoveStarted = false;
        finishReachedTarget = false;
        frozenDuckMaxX_UI = 0f;
        useAutoFinishSpeed = false;
        finishAutoSpeed = 0f;
        finishFinalTargetX = 0f;
        leaderSprintStarted = false;

        foreach (var d in duckMovers)
        {
            if (d == null) continue;
            d.StopSprint();
            // pass computed clamp X values (world-space)
            d.ApplyRaceParams(speedMinA, speedMaxB, randomIntervalC, stopDuckTimeD, MinPosX, MaxPosX);
        }
    }

    private void OnPausePressed()
    {
        if (state != State.Running) return;
        state = State.Paused;
        PauseBackgrounds();

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    private void OnContinuePressed()
    {
        if (state != State.Paused) return;
        state = State.Running;
        ResumeBackgrounds();

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
    }

    private void OnClearPressed()
    {
        totalRaceTime = (RaceConfig.Instance != null) ? RaceConfig.Instance.durationSeconds : 15;
        remainingTime = totalRaceTime;
        UpdateCountdownText();

        state = State.Ready;
        PauseBackgrounds();

        Time.timeScale = 1f;

        finishMoveStarted = false;
        finishReachedTarget = false;
        frozenDuckMaxX_UI = 0f;

        runningRanking.Clear();
        runningRankByDuck.Clear();
        nextRankingUpdateTime = 0f;

        finalRankingList = null;
        leaderSprintStarted = false;

        for (int i = 0; i < duckMovers.Count; i++)
        {
            var d = duckMovers[i];
            if (d == null) continue;
            if (i < initialPositions.Count)
            {
                d.ResetToInitial(initialPositions[i], initialRotations[i]);
            }
            d.StopSprint();
        }

        ResetFinishToStartX();

        // Reset backgrounds and scrollers
        var spawners = FindObjectsOfType<BackgroundSpawner>();
        foreach (var sp in spawners) sp.ResetSpawner();

        var scrollers = FindObjectsOfType<Scroller>();
        foreach (var sc in scrollers) sc.ResetToStart();
    }

    private void OnBackPressed()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("SetupScene");
    }

    private void OnTimeUp()
    {
        state = State.Ready;
        PauseBackgrounds();

        Time.timeScale = 1f;

        finalRankingList = BuildFinalRankingList();

        var lb = FindObjectOfType<LeaderboardUI>();
        if (lb != null) lb.UpdateLeaderboard(finalRankingList);
    }

    private List<DuckMover> BuildFinalRankingList()
    {
        var movers = new List<DuckMover>();
        foreach (var d in duckMovers) if (d != null) movers.Add(d);

        movers.Sort((a, b) => b.GetWorldX().CompareTo(a.GetWorldX()));
        return movers;
    }

    private void UpdateCountdownText()
    {
        int sec = Mathf.CeilToInt(remainingTime);
        int h = sec / 3600;
        int m = (sec % 3600) / 60;
        int s = sec % 60;
        if (countdownText == null) return;
        if (h > 0) countdownText.text = string.Format("{0:D2}:{1:D2}:{2:D2}", h, m, s);
        else countdownText.text = string.Format("{0:D2}:{1:D2}", m, s);
    }

    public bool IsRunning()
    {
        return state == State.Running;
    }

    private void TryStartLeaderSprint(DuckMover leader)
    {
        if (leader == null) { leaderSprintStarted = true; return; }
        if (leader.IsSprinting()) { leaderSprintStarted = true; return; }

        CacheFinishRects();

        RectTransform finishParent = finishRect != null ? finishRect.parent as RectTransform : null;
        if (finishParent == null)
        {
            leaderSprintStarted = true;
            return;
        }

        Vector3 worldPoint = finishParent.TransformPoint(new Vector3(finishFinalTargetX, 0f, 0f));
        float finishTargetWorldX = worldPoint.x;

        leader.StartSprintToWorldX(finishTargetWorldX, remainingTime);
        leaderSprintStarted = true;
    }
}
