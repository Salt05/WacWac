using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TransitionManager.cs
/// Handles the transition flag animation when moving from SetupScene to RaceScene.
/// The flag is a regular 2D GameObject (not UI) that slides to a target position
/// using Cubic Ease Out animation before loading the race scene.
/// </summary>
public class TransitionManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("Transition Objects")]
    [SerializeField] private Transform flagTransform;    // The Flag_Image GameObject's transform (child of Canvas_MainUI)
    [SerializeField] private Transform targetA;          // Target position A for the flag to move to
    [SerializeField] private Transform posOffScreen;     // Optional: starting off-screen position for the flag (Pos_OffScreen)
    [SerializeField] private Transform targetB;          // Optional: unused point B that will be destroyed when transition runs

    [Header("Setup References")]
    [SerializeField] private SetupUIManager setupUIManager;   // Optional reference to read time/duck count from Setup scene

    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 1.0f;  // Duration of the slide animation in seconds

    [Header("Scene Settings")]
    [SerializeField] private string raceSceneName = "RaceScene";  // Name of the race scene to load

    #endregion

    #region Private Fields

    private bool isTransitioning = false;  // Prevents multiple transition triggers
    private Coroutine transitionCoroutine;  // Reference to the active transition coroutine

    #endregion

    #region Public Methods

    /// <summary>
    /// Triggers the transition animation and starts the race.
    /// Call this method when the player confirms their setup and wants to start racing.
    /// </summary>
    public void TriggerTransitionAndStartRace()
    {
        // Trước khi chuyển scene, đồng bộ dữ liệu thiết lập (thời gian, số vịt, tên)
        // từ SetupUIManager + DataManager sang RaceConfig để RaceScene không dùng
        // giá trị mặc định nữa.
        SyncSetupToRaceConfig();

        // Prevent multiple triggers
        if (isTransitioning)
        {
            Debug.LogWarning("[TransitionManager] Transition already in progress. Ignoring duplicate call.");
            return;
        }

        // Validate required references
        if (flagTransform == null)
        {
            Debug.LogError("[TransitionManager] Flag Transform is not assigned! Cannot perform transition.");
            // Still load the scene as fallback
            LoadRaceScene();
            return;
        }

        if (targetA == null)
        {
            Debug.LogError("[TransitionManager] Target A Transform is not assigned! Cannot perform transition.");
            // Still load the scene as fallback
            LoadRaceScene();
            return;
        }

        // Start the transition
        isTransitioning = true;
        transitionCoroutine = StartCoroutine(TransitionCoroutine());

        Debug.Log("[TransitionManager] Transition started.");
    }

    /// <summary>
    /// Allows external scripts to check if a transition is currently in progress.
    /// </summary>
    public bool IsTransitioning()
    {
        return isTransitioning;
    }

    /// <summary>
    /// Cancels the current transition if one is in progress.
    /// Does NOT prevent the scene from eventually loading if already triggered.
    /// </summary>
    public void CancelTransition()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
            isTransitioning = false;
            Debug.Log("[TransitionManager] Transition cancelled.");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Đồng bộ dữ liệu từ màn Setup sang RaceConfig:
    /// - Thời gian đua (giây)
    /// - Số lượng vịt
    /// - Danh sách tên vịt
    ///
    /// RaceController ở RaceScene luôn đọc từ RaceConfig.Instance, nên nếu ta không
    /// ghi các giá trị này ở đây thì nó sẽ dùng các giá trị mặc định trong RaceConfig.
    /// </summary>
    private void SyncSetupToRaceConfig()
    {
        // Lấy SetupUIManager nếu chưa được gán trong Inspector
        if (setupUIManager == null)
        {
            setupUIManager = FindObjectOfType<SetupUIManager>();
        }

        int timeValue = 0;
        int numericQuantity = 0;

        if (setupUIManager != null)
        {
            timeValue = setupUIManager.GetTimeValue();
            // quantity dạng số do người chơi nhập trên keypad (giữ riêng)
            numericQuantity = setupUIManager.GetNumericQuantity();
        }

        // Giá trị mặc định & tối thiểu (theo RaceConfig / logic cũ)
        const int DefaultTimeSeconds = 15;
        const int DefaultDuckCount = 5;
        const int MinTimeSeconds = 10;
        const int MinDuckCount = 3;

        if (timeValue <= 0)
            timeValue = DefaultTimeSeconds;
        if (numericQuantity <= 0)
            numericQuantity = DefaultDuckCount;

        if (timeValue < MinTimeSeconds)
            timeValue = MinTimeSeconds;
        if (numericQuantity < MinDuckCount)
            numericQuantity = MinDuckCount;

        // Lấy (hoặc tạo) RaceConfig để lưu cấu hình qua scene
        RaceConfig config = RaceConfig.Instance;
        if (config == null)
        {
            // Nếu chưa có RaceConfig trong scene Setup, tạo mới một cái.
            GameObject go = new GameObject("RaceConfig");
            config = go.AddComponent<RaceConfig>();
        }

        config.durationSeconds = timeValue;
        // Lưu quantity dạng số để khôi phục lại ở SetupScene
        config.quantityNumeric = numericQuantity;

        // Ghi lại chế độ đặt tên hiện tại (tên hay số) để RaceScene & lần mở Setup sau dùng lại
        if (setupUIManager != null)
        {
            config.namePreference =
                (setupUIManager.GetCurrentState() == SetupUIManager.SetupState.Names)
                    ? RaceConfig.NameSourcePreference.PreferNames
                    : RaceConfig.NameSourcePreference.PreferNumbers;
        }

        // Đồng bộ tên vịt từ DataManager (nếu có)
        if (DataManager.Instance != null)
        {
            var list = DataManager.Instance.DuckNames;
            if (list != null && list.Count > 0)
            {
                config.duckNames = list.ToArray();
                config.duckNamesRaw = string.Join("\n", list);
            }
            else
            {
                // Không có tên -> cho phép RaceController fallback sang số thứ tự
                config.duckNames = null;
                config.duckNamesRaw = string.Empty;
            }
        }

        // Sau khi có namePreference và danh sách tên, quyết định số vịt thực sự cho RaceScene
        int actualDuckCount = numericQuantity;
        int nameCount = (config.duckNames != null) ? config.duckNames.Length : 0;

        if (config.namePreference == RaceConfig.NameSourcePreference.PreferNames && nameCount > 0)
        {
            // Chế độ tên: số vịt = số tên
            actualDuckCount = nameCount;
        }

        // Đây là số vịt mà RaceScene sẽ dùng để spawn
        config.duckCount = actualDuckCount;
    }

    /// <summary>
    /// Coroutine that handles the flag movement animation and scene loading.
    /// Uses Cubic Ease Out for smooth deceleration: t = 1 - (1 - progress)^3
    /// </summary>
    private IEnumerator TransitionCoroutine()
    {
        // If a specific off-screen starting position is provided, snap the flag there first
        if (posOffScreen != null)
        {
            flagTransform.position = posOffScreen.position;
        }

        // Store the starting position (current flag position)
        Vector3 startPosition = flagTransform.position;
        Vector3 endPosition = targetA.position;

        float elapsed = 0f;

        Debug.Log($"[TransitionManager] Moving flag from {startPosition} to {endPosition} over {transitionDuration} seconds.");

        // Animation loop
        while (elapsed < transitionDuration)
        {
            // Increment elapsed time
            elapsed += Time.deltaTime;

            // Calculate linear progress (0 to 1)
            float progress = Mathf.Clamp01(elapsed / transitionDuration);

            // Apply Cubic Ease Out formula: t = 1 - (1 - progress)^3
            // This creates a smooth deceleration effect
            float easedProgress = CubicEaseOut(progress);

            // Interpolate position using the eased progress value
            flagTransform.position = Vector3.Lerp(startPosition, endPosition, easedProgress);

            // Wait for next frame
            yield return null;
        }

        // Ensure the flag is exactly at the target position
        flagTransform.position = endPosition;

        Debug.Log("[TransitionManager] Flag reached target position. Loading race scene...");

        // Clean up helper points (B and OffScreen) if they exist, as requested
        if (targetB != null)
        {
            Destroy(targetB.gameObject);
        }

        if (posOffScreen != null)
        {
            Destroy(posOffScreen.gameObject);
        }

        // Small delay before loading scene (optional, for polish)
        yield return new WaitForSeconds(0.1f);

        // Load the race scene
        LoadRaceScene();
    }

    /// <summary>
    /// Cubic Ease Out easing function.
    /// Creates smooth deceleration - starts fast and slows down towards the end.
    /// Formula: t = 1 - (1 - progress)^3
    /// </summary>
    /// <param name="t">Linear progress value from 0 to 1.</param>
    /// <returns>Eased progress value from 0 to 1.</returns>
    private float CubicEaseOut(float t)
    {
        // Clamp input to valid range
        t = Mathf.Clamp01(t);

        // Apply cubic ease out formula
        // As t goes from 0 to 1:
        // - (1 - t) goes from 1 to 0
        // - Pow((1 - t), 3) goes from 1 to 0 (but curved)
        // - 1 - Pow((1 - t), 3) goes from 0 to 1 (with ease out curve)
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    /// <summary>
    /// Loads the race scene.
    /// </summary>
    private void LoadRaceScene()
    {
        Debug.Log($"[TransitionManager] Loading scene: {raceSceneName}");
        SceneManager.LoadScene(raceSceneName);
    }

    #endregion

    #region Editor Helpers

    /// <summary>
    /// Draws gizmos in the editor to visualize the transition path.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (flagTransform != null && targetA != null)
        {
            // Draw line from flag to target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(flagTransform.position, targetA.position);

            // Draw sphere at target position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(targetA.position, 0.5f);

            // Draw sphere at flag position
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(flagTransform.position, 0.3f);
        }
    }

    #endregion
}
