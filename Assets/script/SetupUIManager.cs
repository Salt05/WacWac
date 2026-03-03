using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SetupUIManager.cs
/// Manages the Setup Scene UI including keypad input, time/duck count displays,
/// name management panel with sliding animations, and name item instantiation.
/// Uses Cubic Ease Out coroutines for smooth animations.
/// </summary>
public class SetupUIManager : MonoBehaviour
{
    #region Enums

    /// <summary>
    /// Current setup state - either setting quantity values or managing names.
    /// </summary>
    public enum SetupState
    {
        Quantity,   // User is entering time/duck count values
        Names       // User is managing duck names
    }

    /// <summary>
    /// Which board is currently active for keypad input.
    /// </summary>
    public enum ActiveBoard
    {
        Time,       // Time value board is active
        Ducks       // Duck count board is active
    }

    #endregion

    #region Time Formatting Helpers

    private string FormatTimeFromDigits(string digits)
    {
        if (string.IsNullOrEmpty(digits)) return "00:00";

        // Pad left with zeros to get MMSS
        string padded = digits.PadLeft(4, '0');
        string mmStr = padded.Substring(0, 2);
        string ssStr = padded.Substring(2, 2);

        return mmStr + ":" + ssStr;
    }

    private int TimeDigitsToSeconds(string digits)
    {
        if (string.IsNullOrEmpty(digits)) return 0;

        string padded = digits.PadLeft(4, '0');
        string mmStr = padded.Substring(0, 2);
        string ssStr = padded.Substring(2, 2);

        if (int.TryParse(mmStr, out int mm) && int.TryParse(ssStr, out int ss))
        {
            return (mm * 60) + ss;
        }

        return 0;
    }

    #endregion

    #region Serialized Fields

    [Header("Display Text References")]
    [SerializeField] private TMP_Text text_TimeValue;        // Displays the race time value
    [SerializeField] private TMP_Text text_DuckCount;        // Displays the duck count value

    [Header("Keypad References")]
    [SerializeField] private Button[] keypadButtons;         // Buttons 0-9 for numeric input
    [SerializeField] private Button btn_ClearBoard;          // Clears the active board's value

    [Header("Board Selection Buttons")]
    [SerializeField] private Button btn_SelectTime;          // Button to select Time board
    [SerializeField] private Button btn_SelectDucks;         // Button to select Ducks board
    [SerializeField] private Button btn_SetNames;            // Toggle button: switches between Quantity <-> Names

    [Header("Board UI Roots")]
    [SerializeField] private RectTransform guiTimeRoot;      // GUI_Time
    [SerializeField] private RectTransform guiDucksRoot;     // GUI_Ducks

    [Header("Name Management Panel")]
    [SerializeField] private RectTransform panelNameManagement;  // The sliding panel for name management
    [SerializeField] private TMP_InputField inputFieldDuckName;  // Input field for entering new names
    [SerializeField] private Button btn_AddName;                  // Button to add a new name
    [SerializeField] private Button btn_ClearNames;              // Button to clear all names (btn_clear)
    [SerializeField] private Transform contentTransform;          // Parent transform for name items (ScrollView content)
    [SerializeField] private GameObject prefabNameItem;           // Prefab for individual name items

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;           // Normal button color
    [SerializeField] private Color activeColor = new Color(0.7f, 0.7f, 0.7f, 1f);  // Darkened active button color
    [SerializeField] private Color pressedColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Darkened pressed button color

    [Header("Toggle State Visuals")]
    [SerializeField] private Image imageToggleState;                 // Image component (SI) inside Btn_ToggleState
    [SerializeField] private Sprite spriteSetNames;                  // Sprite shown when in Quantity mode (button means "Set Names")
    [SerializeField] private Sprite spriteSetQuantity;               // Sprite shown when in Names mode (button means "Set Quantity")

    [Header("Animation Settings")]
    [SerializeField] private float panelSlideDuration = 0.5f;            // Duration of panel slide animation
    [SerializeField] private Vector2 panelHiddenPosition = new Vector2(800f, 0f);   // Off-screen position (right)
    [SerializeField] private Vector2 panelVisiblePosition = new Vector2(0f, 0f);    // On-screen position

    [Header("Board Select Animation")]
    [SerializeField] private float boardSelectScale = 1.12f;
    [SerializeField] private float boardSelectDuration = 0.28f;

    [Header("Value Limits")]
    [SerializeField] private int maxTimeValue = 999;         // Maximum race time value
    [SerializeField] private int maxDuckCount = 99;          // Maximum number of ducks
    [SerializeField] private int maxNameLength = 10;         // Maximum characters per name

    #endregion

    #region Private Fields

    private SetupState currentState = SetupState.Quantity;
    private ActiveBoard activeBoard = ActiveBoard.Time;

    private string timeValueString = "";
    private string duckCountString = "";
    // Buffer storing entered time digits (newest digit at front).
    // Example entry sequence: press 3 -> "3" (displays 00:03)
    // then press 5 -> "53" (displays 00:53), press 2 -> "253" (displays 02:53)
    private string timeDigits = "";

    private List<GameObject> nameItemInstances = new List<GameObject>();  // Track instantiated name items

    private Coroutine panelSlideCoroutine;   // Reference to current slide coroutine
    private int currentEditingIndex = -1;    // Index of name being edited (-1 = none)
    private Coroutine boardSelectUpCoroutine;
    private Coroutine boardSelectDownCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Initialize UI state
        InitializeUI();

        // Setup button listeners
        SetupButtonListeners();

        // Set initial panel position (hidden)
        if (panelNameManagement != null)
        {
            panelNameManagement.anchoredPosition = panelHiddenPosition;
        }

        // Load existing names from DataManager
        RefreshNameList();

        // Khởi tạo trạng thái từ RaceConfig (nhớ lần trước là dùng tên hay số)
        InitializeFromRaceConfig();

        // Cập nhật hiển thị và sprite nút toggle theo trạng thái hiện tại
        UpdateDisplays();
        UpdateToggleStateVisuals();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the UI to its default state.
    /// </summary>
    private void InitializeUI()
    {
        // Set Time board as active by default
        activeBoard = ActiveBoard.Time;
        currentState = SetupState.Quantity;

        // Update button visuals
        UpdateBoardButtonVisuals();
    }

    /// <summary>
    /// Đọc RaceConfig để biết lần trước người chơi đang dùng kiểu tên nào
    /// (tên chữ hay số), từ đó khởi tạo lại UI:
    /// - Nếu PreferNames: bật Names mode, panel trượt vào, toggle = Sprite Set Quantity.
    /// - Nếu PreferNumbers hoặc Auto: giữ Quantity mode, panel ẩn, toggle = Sprite Set Names.
    /// </summary>
    private void InitializeFromRaceConfig()
    {
        if (RaceConfig.Instance == null)
        {
            return;
        }

        RaceConfig config = RaceConfig.Instance;

        // Khôi phục chế độ đặt tên (tên / số) và trạng thái panel
        switch (config.namePreference)
        {
            case RaceConfig.NameSourcePreference.PreferNames:
                currentState = SetupState.Names;
                // đảm bảo panel bắt đầu ở vị trí ẩn rồi thực hiện hoạt ảnh trượt vào
                if (panelNameManagement != null)
                {
                    panelNameManagement.anchoredPosition = panelHiddenPosition;
                    SlideInNamePanel();
                }
                break;

            case RaceConfig.NameSourcePreference.PreferNumbers:
            case RaceConfig.NameSourcePreference.Auto:
            default:
                currentState = SetupState.Quantity;
                if (panelNameManagement != null)
                {
                    panelNameManagement.anchoredPosition = panelHiddenPosition;
                }
                break;
        }

        // Khôi phục lại giá trị thời gian đã set (nếu > 0)
        if (config.durationSeconds > 0)
        {
            // Convert seconds to MMSS digits buffer (newest-first storage)
            int total = config.durationSeconds;
            int mm = total / 60;
            int ss = total % 60;
            string s = mm.ToString("00") + ss.ToString("00"); // e.g. "0253"
            // store as newest-first buffer by trimming leading zeros
            timeDigits = s.TrimStart('0');
            if (timeDigits == "0000") timeDigits = "";
        }

        // Khôi phục lại quantity (số lượng vịt) từ quantityNumeric (giá trị người chơi set ở chế độ số)
        // Giá trị này được giữ riêng, không phụ thuộc tên, để khi flow
        // Quantity(16) -> Names(5) -> RaceScene -> SetupScene -> Quantity vẫn hiển thị 16.
        if (config.quantityNumeric > 0)
        {
            duckCountString = config.quantityNumeric.ToString();
        }
    }

    /// <summary>
    /// Sets up all button click listeners.
    /// </summary>
    private void SetupButtonListeners()
    {
        // Keypad buttons (0-9)
        for (int i = 0; i < keypadButtons.Length; i++)
        {
            int digit = i; // Capture for closure
            if (keypadButtons[i] != null)
            {
                keypadButtons[i].onClick.AddListener(() => OnKeypadButtonPressed(digit));
            }
        }

        // Clear button
        if (btn_ClearBoard != null)
        {
            btn_ClearBoard.onClick.AddListener(OnClearButtonPressed);
        }

        // Board selection buttons
        if (btn_SelectTime != null)
        {
            btn_SelectTime.onClick.AddListener(() => SelectBoard(ActiveBoard.Time));
        }

        if (btn_SelectDucks != null)
        {
            btn_SelectDucks.onClick.AddListener(() => SelectBoard(ActiveBoard.Ducks));
        }

        // Set Names button
        if (btn_SetNames != null)
        {
            // This button now toggles between Quantity <-> Names modes,
            // showing/hiding the name management panel and swapping the SI image.
            btn_SetNames.onClick.AddListener(OnToggleStateButtonPressed);
        }

        // Add Name button
        if (btn_AddName != null)
        {
            btn_AddName.onClick.AddListener(OnAddNamePressed);
        }

        // Clear Names button
        if (btn_ClearNames != null)
        {
            btn_ClearNames.onClick.AddListener(OnClearNamesPressed);
        }

        // Input field submit on Enter key
        if (inputFieldDuckName != null)
        {
            inputFieldDuckName.onSubmit.AddListener(OnInputFieldSubmit);
            inputFieldDuckName.characterLimit = maxNameLength;
        }
    }

    #endregion

    #region Keypad Handling

    /// <summary>
    /// Handles keypad button presses.
    /// Appends the digit to the active board's value.
    /// </summary>
    /// <param name="digit">The digit pressed (0-9).</param>
    private void OnKeypadButtonPressed(int digit)
    {
        // In Name mode, the Ducks board ignores keypad input
        if (currentState == SetupState.Names && activeBoard == ActiveBoard.Ducks)
        {
            Debug.Log("[SetupUIManager] Duck count is auto-managed in Name mode. Keypad input ignored.");
            return;
        }

        // Only allow keypad in Quantity mode or for Time board
        if (currentState == SetupState.Names && activeBoard != ActiveBoard.Time)
        {
            return;
        }

        switch (activeBoard)
        {
            case ActiveBoard.Time:
                AppendToTimeValue(digit);
                break;
            case ActiveBoard.Ducks:
                AppendToDuckCount(digit);
                break;
        }

        UpdateDisplays();
    }

    /// <summary>
    /// Appends a digit to the time value string.
    /// </summary>
    private void AppendToTimeValue(int digit)
    {
        // Append newest digit to the end of the buffer
        timeDigits = timeDigits + digit.ToString();
        // Keep at most 4 digits (MMSS)
        if (timeDigits.Length > 4) timeDigits = timeDigits.Substring(timeDigits.Length - 4, 4);
    }

    /// <summary>
    /// Appends a digit to the duck count string.
    /// </summary>
    private void AppendToDuckCount(int digit)
    {
        string newValue = duckCountString + digit.ToString();
        int maxDigits = maxDuckCount.ToString().Length;

        if (newValue.Length > maxDigits)
        {
            return;
        }

        if (int.TryParse(newValue, out int parsed))
        {
            if (parsed > maxDuckCount)
            {
                duckCountString = maxDuckCount.ToString();
            }
            else
            {
                duckCountString = newValue;
            }
        }
    }

    /// <summary>
    /// Handles the clear button press.
    /// Clears ONLY the active board's value.
    /// </summary>
    private void OnClearButtonPressed()
    {
        switch (activeBoard)
        {
            case ActiveBoard.Time:
                timeDigits = "";
                Debug.Log("[SetupUIManager] Time value cleared.");
                break;
            case ActiveBoard.Ducks:
                // In Name mode, don't allow clearing duck count (it's auto-managed)
                if (currentState == SetupState.Names)
                {
                    Debug.Log("[SetupUIManager] Duck count is auto-managed in Name mode. Clear ignored.");
                    return;
                }
                duckCountString = "";
                Debug.Log("[SetupUIManager] Duck count cleared.");
                break;
        }

        UpdateDisplays();
    }

    #endregion

    #region Board Selection

    /// <summary>
    /// Selects the active board for keypad input.
    /// </summary>
    /// <param name="board">The board to select.</param>
    private void SelectBoard(ActiveBoard board)
    {
        activeBoard = board;
        UpdateBoardButtonVisuals();
        PlayBoardSelectAnimation(board);
        Debug.Log($"[SetupUIManager] Selected board: {board}");
    }

    /// <summary>
    /// Updates the visual appearance of board selection buttons.
    /// Active board button is tinted darker.
    /// </summary>
    private void UpdateBoardButtonVisuals()
    {
        // Update Time button
        if (btn_SelectTime != null)
        {
            Image timeImage = btn_SelectTime.GetComponent<Image>();
            if (timeImage != null)
            {
                timeImage.color = (activeBoard == ActiveBoard.Time) ? activeColor : normalColor;
            }
        }

        // Update Ducks button
        if (btn_SelectDucks != null)
        {
            Image ducksImage = btn_SelectDucks.GetComponent<Image>();
            if (ducksImage != null)
            {
                ducksImage.color = (activeBoard == ActiveBoard.Ducks) ? activeColor : normalColor;
            }
        }
    }

    private void PlayBoardSelectAnimation(ActiveBoard board)
    {
        RectTransform target = (board == ActiveBoard.Time) ? guiTimeRoot : guiDucksRoot;
        RectTransform other = (board == ActiveBoard.Time) ? guiDucksRoot : guiTimeRoot;

        if (target == null)
        {
            return;
        }

        if (boardSelectUpCoroutine != null)
        {
            StopCoroutine(boardSelectUpCoroutine);
        }

        if (boardSelectDownCoroutine != null)
        {
            StopCoroutine(boardSelectDownCoroutine);
        }

        boardSelectUpCoroutine = StartCoroutine(AnimateScale(target, Vector3.one, Vector3.one * boardSelectScale, false));
        if (other != null)
        {
            boardSelectDownCoroutine = StartCoroutine(AnimateScale(other, other.localScale, Vector3.one, true));
        }
    }

    private IEnumerator AnimateScale(RectTransform target, Vector3 from, Vector3 to, bool isDown)
    {
        float duration = Mathf.Max(0.01f, boardSelectDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = EaseInCubic(p);
            target.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        target.localScale = to;
        if (isDown)
        {
            boardSelectDownCoroutine = null;
        }
        else
        {
            boardSelectUpCoroutine = null;
        }
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private IEnumerator AnimateItemShrink(Transform target, float duration)
    {
        if (target == null) yield break;

        Vector3 startScale = target.localScale;
        Vector3 endScale = Vector3.zero;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);
            target.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }

        if (target != null)
        {
            target.localScale = endScale;
        }
    }

    private IEnumerator AnimateItemShrinkAndDestroy(Transform target, float duration)
    {
        if (target == null) yield break;

        Vector3 startScale = target.localScale;
        Vector3 endScale = Vector3.zero;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);
            target.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }

        if (target != null)
        {
            target.localScale = endScale;
            Destroy(target.gameObject);
        }
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    /// <summary>
    /// Updates the sprite of the toggle button image (SI) based on current state.
    ///
    /// - Quantity mode  : hiển thị spriteSetNames  (nút mang ý nghĩa "Set Names").
    /// - Names mode      : hiển thị spriteSetQuantity (nút mang ý nghĩa "Set Quantity").
    /// </summary>
    private void UpdateToggleStateVisuals()
    {
        if (imageToggleState == null)
        {
            return;
        }

        if (currentState == SetupState.Quantity)
        {
            if (spriteSetNames != null)
            {
                imageToggleState.sprite = spriteSetNames;
            }
        }
        else // SetupState.Names
        {
            if (spriteSetQuantity != null)
            {
                imageToggleState.sprite = spriteSetQuantity;
            }
        }
    }

    #endregion

    #region Display Updates

    /// <summary>
    /// Updates all display text elements with current values.
    /// </summary>
    private void UpdateDisplays()
    {
        // Update Time display
        if (text_TimeValue != null)
        {
            text_TimeValue.text = FormatTimeFromDigits(timeDigits);
        }

        // Update Duck Count display
        if (text_DuckCount != null)
        {
            // In Name mode, show the count of names in the list
            if (currentState == SetupState.Names)
            {
                int nameCount = DataManager.Instance != null ? DataManager.Instance.GetDuckCount() : 0;
                // Chỉ hiển thị số lượng tên hiện có, KHÔNG ghi đè giá trị quantity đã nhập
                // để khi quay lại chế độ quantity vẫn giữ được số lượng ban đầu.
                text_DuckCount.text = nameCount.ToString();
            }
            else
            {
                text_DuckCount.text = string.IsNullOrEmpty(duckCountString) ? "0" : duckCountString;
            }
        }
    }

    #endregion

    #region Name Management

    /// <summary>
    /// Handles the toggle button press (Btn_ToggleState / SI).
    ///
    /// - Khi đang ở Quantity: chuyển sang Names, panel trượt vào, SI đổi sang hình SetQuantity.
    /// - Khi đang ở Names: chuyển về Quantity, panel trượt ra, SI đổi sang hình SetNames.
    /// </summary>
    private void OnSetNamesPressed()
    {
        // Kept for backward compatibility if already used in Inspector
        OnToggleStateButtonPressed();
    }

    /// <summary>
    /// New explicit handler for the toggle button.
    /// </summary>
    private void OnToggleStateButtonPressed()
    {
        if (currentState == SetupState.Quantity)
        {
            // Quantity -> Names
            currentState = SetupState.Names;
            SlideInNamePanel();
            Debug.Log("[SetupUIManager] Switched to Names mode (panel shown).");

            // Ghi nhớ: đang ưu tiên tên chữ
            if (RaceConfig.Instance != null)
            {
                RaceConfig.Instance.namePreference = RaceConfig.NameSourcePreference.PreferNames;
            }
        }
        else
        {
            // Names -> Quantity
            currentState = SetupState.Quantity;
            SlideOutNamePanel();
            Debug.Log("[SetupUIManager] Switched to Quantity mode (panel hidden).");

            // Ghi nhớ: đang ưu tiên số
            if (RaceConfig.Instance != null)
            {
                RaceConfig.Instance.namePreference = RaceConfig.NameSourcePreference.PreferNumbers;
            }
        }

        // Update SI sprite and duck count display
        UpdateToggleStateVisuals();
        UpdateDisplays();
    }

    /// <summary>
    /// Handles the Add Name button press.
    /// Adds the input field text as a new duck name.
    /// </summary>
    private void OnAddNamePressed()
    {
        if (inputFieldDuckName == null) return;

        string newName = inputFieldDuckName.text.Trim();

        if (!string.IsNullOrEmpty(newName))
        {
            // Limit to max name length
            if (newName.Length > maxNameLength)
            {
                newName = newName.Substring(0, maxNameLength);
            }

            // Add to DataManager
            if (DataManager.Instance != null)
            {
                DataManager.Instance.AddDuckName(newName);
            }

            // Clear input field
            inputFieldDuckName.text = "";

            // Refresh the displayed list
            RefreshNameList();

            // Update displays (duck count will auto-update)
            UpdateDisplays();

            Debug.Log($"[SetupUIManager] Added new name: {newName}");

            // Keep focus on input field for continuous input
            inputFieldDuckName.Select();
            inputFieldDuckName.ActivateInputField();
        }
    }

    /// <summary>
    /// Handles Enter key submission in the input field.
    /// </summary>
    private void OnInputFieldSubmit(string text)
    {
        OnAddNamePressed();
    }

    private void OnClearNamesPressed()
    {
        StartCoroutine(AnimateClearAllNames());
    }

    private IEnumerator AnimateClearAllNames()
    {
        if (nameItemInstances.Count == 0)
        {
            if (DataManager.Instance != null)
            {
                DataManager.Instance.ClearAllDuckNames();
            }
            RefreshNameList();
            UpdateDisplays();
            yield break;
        }

        float delayBetweenItems = 0.08f;
        List<GameObject> itemsToDestroy = new List<GameObject>(nameItemInstances);

        for (int i = 0; i < itemsToDestroy.Count; i++)
        {
            GameObject item = itemsToDestroy[i];
            if (item != null)
            {
                StartCoroutine(AnimateItemShrinkAndDestroy(item.transform, 0.2f));
            }
            yield return new WaitForSeconds(delayBetweenItems);
        }

        yield return new WaitForSeconds(0.25f);

        if (DataManager.Instance != null)
        {
            DataManager.Instance.ClearAllDuckNames();
        }

        nameItemInstances.Clear();
        UpdateDisplays();

        Debug.Log("[SetupUIManager] Cleared all names.");
    }

    /// <summary>
    /// Refreshes the name list UI by destroying old items and instantiating new ones.
    /// </summary>
    private void RefreshNameList()
    {
        // Clear existing name item instances
        foreach (GameObject item in nameItemInstances)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        nameItemInstances.Clear();

        // Get names from DataManager
        if (DataManager.Instance == null || contentTransform == null || prefabNameItem == null)
        {
            return;
        }

        List<string> names = DataManager.Instance.DuckNames;

        // Instantiate a prefab for each name
        for (int i = 0; i < names.Count; i++)
        {
            int index = i; // Capture for closures
            GameObject newItem = Instantiate(prefabNameItem, contentTransform);
            nameItemInstances.Add(newItem);

            // Setup the name item components
            SetupNameItem(newItem, index, names[i]);
        }

        Debug.Log($"[SetupUIManager] Refreshed name list with {names.Count} items.");
    }

    /// <summary>
    /// Sets up a name item prefab with the appropriate text and button handlers.
    /// Expects the prefab to have:
    /// - A TMP_Text or Text component for displaying the name
    /// - A TMP_InputField for editing (initially disabled)
    /// - A Button for the "X" remove button
    /// </summary>
    /// <param name="item">The instantiated prefab GameObject.</param>
    /// <param name="index">The index of this name in the list.</param>
    /// <param name="nameText">The name text to display.</param>
    private void SetupNameItem(GameObject item, int index, string nameText)
    {
        // Find and set the name text display
        TMP_Text nameLabel = item.GetComponentInChildren<TMP_Text>();
        if (nameLabel != null)
        {
            nameLabel.text = nameText;
        }

        // Find the input field for editing (should be disabled by default)
        TMP_InputField editField = item.GetComponentInChildren<TMP_InputField>();
        if (editField != null)
        {
            editField.text = nameText;
            editField.characterLimit = maxNameLength;
            editField.gameObject.SetActive(false); // Hidden by default

            // Handle edit completion
            editField.onEndEdit.AddListener((newText) => OnNameEditComplete(index, newText, editField, nameLabel));
        }

        // Find buttons in the item
        Button[] buttons = item.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            // Check for remove button (named "Btn_Remove" or has "X" text)
            TMP_Text btnText = btn.GetComponentInChildren<TMP_Text>();
            if (btn.name.Contains("Remove") || btn.name.Contains("Delete") || 
                (btnText != null && btnText.text == "X"))
            {
                btn.onClick.AddListener(() => OnRemoveNameFromItem(item));
            }
            // Check for edit/click-to-edit functionality on the main item
            else if (btn.name.Contains("Edit") || btn.name.Contains("Name"))
            {
                btn.onClick.AddListener(() => OnEditNamePressed(index, editField, nameLabel));
            }
        }

        // If the main item itself is a button (click to edit)
        Button mainButton = item.GetComponent<Button>();
        if (mainButton != null && editField != null)
        {
            mainButton.onClick.AddListener(() => OnEditNamePressed(index, editField, nameLabel));
        }
    }

    /// <summary>
    /// Handles clicking on a name to edit it.
    /// Shows the input field and hides the label.
    /// </summary>
    private void OnEditNamePressed(int index, TMP_InputField editField, TMP_Text nameLabel)
    {
        if (editField == null) return;

        // Hide label, show input field
        if (nameLabel != null)
        {
            nameLabel.gameObject.SetActive(false);
        }

        editField.gameObject.SetActive(true);
        editField.Select();
        editField.ActivateInputField();

        currentEditingIndex = index;
        Debug.Log($"[SetupUIManager] Editing name at index {index}");
    }

    /// <summary>
    /// Handles completion of name editing (when input field loses focus or Enter is pressed).
    /// </summary>
    private void OnNameEditComplete(int index, string newText, TMP_InputField editField, TMP_Text nameLabel)
    {
        if (editField == null) return;

        // Hide input field, show label
        editField.gameObject.SetActive(false);
        if (nameLabel != null)
        {
            nameLabel.gameObject.SetActive(true);
        }

        // Update the name if it's not empty
        newText = newText.Trim();
        if (!string.IsNullOrEmpty(newText))
        {
            if (DataManager.Instance != null)
            {
                DataManager.Instance.UpdateDuckNameAt(index, newText);
            }

            // Update the label text
            if (nameLabel != null)
            {
                nameLabel.text = newText.Length > maxNameLength ? newText.Substring(0, maxNameLength) : newText;
            }

            Debug.Log($"[SetupUIManager] Updated name at index {index} to: {newText}");
        }

        currentEditingIndex = -1;
    }

    /// <summary>
    /// Handles the remove "X" button press for a name item.
    /// Finds the index dynamically to avoid closure index capture issues.
    /// </summary>
    private void OnRemoveNameFromItem(GameObject item)
    {
        if (item == null) return;

        int index = nameItemInstances.IndexOf(item);
        if (index >= 0)
        {
            StartCoroutine(AnimateRemoveAndRefresh(item, index));
        }
    }

    /// <summary>
    /// Handles the remove "X" button press for a name item (legacy, by index).
    /// </summary>
    /// <param name="index">The index of the name to remove.</param>
    private void OnRemoveNamePressed(int index)
    {
        if (index >= 0 && index < nameItemInstances.Count)
        {
            GameObject itemToRemove = nameItemInstances[index];
            if (itemToRemove != null)
            {
                StartCoroutine(AnimateRemoveAndRefresh(itemToRemove, index));
            }
        }
        else
        {
            OnRemoveNamePressedImmediate(index);
        }
    }

    private IEnumerator AnimateRemoveAndRefresh(GameObject item, int index)
    {
        yield return StartCoroutine(AnimateItemShrinkAndDestroy(item.transform, 0.25f));

        if (DataManager.Instance != null)
        {
            DataManager.Instance.RemoveDuckNameAt(index);
        }

        if (nameItemInstances.Contains(item))
        {
            nameItemInstances.Remove(item);
        }

        UpdateDisplays();

        Debug.Log($"[SetupUIManager] Removed name at index {index}");
    }

    private void OnRemoveNamePressedImmediate(int index)
    {
        if (DataManager.Instance != null)
        {
            DataManager.Instance.RemoveDuckNameAt(index);
        }

        RefreshNameList();
        UpdateDisplays();

        Debug.Log($"[SetupUIManager] Removed name at index {index}");
    }

    #endregion

    #region Panel Slide Animations

    /// <summary>
    /// Slides the name management panel in from the right using Cubic Ease Out.
    /// </summary>
    private void SlideInNamePanel()
    {
        if (panelNameManagement == null) return;

        // Stop any existing slide animation
        if (panelSlideCoroutine != null)
        {
            StopCoroutine(panelSlideCoroutine);
        }

        panelSlideCoroutine = StartCoroutine(SlidePanelCoroutine(panelHiddenPosition, panelVisiblePosition));
    }

    /// <summary>
    /// Slides the name management panel out to the right using Cubic Ease Out.
    /// </summary>
    private void SlideOutNamePanel()
    {
        if (panelNameManagement == null) return;

        // Stop any existing slide animation
        if (panelSlideCoroutine != null)
        {
            StopCoroutine(panelSlideCoroutine);
        }

        panelSlideCoroutine = StartCoroutine(SlidePanelCoroutine(panelVisiblePosition, panelHiddenPosition));
    }

    /// <summary>
    /// Coroutine that slides the panel from start to end position using Cubic Ease Out.
    /// Formula: t = 1f - Mathf.Pow(1f - progress, 3f)
    /// </summary>
    /// <param name="startPos">Starting anchored position.</param>
    /// <param name="endPos">Target anchored position.</param>
    private IEnumerator SlidePanelCoroutine(Vector2 startPos, Vector2 endPos)
    {
        float elapsed = 0f;

        // Set initial position
        panelNameManagement.anchoredPosition = startPos;

        while (elapsed < panelSlideDuration)
        {
            elapsed += Time.deltaTime;

            // Calculate linear progress (0 to 1)
            float progress = Mathf.Clamp01(elapsed / panelSlideDuration);

            // Apply Cubic Ease Out formula: t = 1 - (1 - progress)^3
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);

            // Lerp position using eased progress
            panelNameManagement.anchoredPosition = Vector2.Lerp(startPos, endPos, easedProgress);

            yield return null;
        }

        // Ensure final position is exact
        panelNameManagement.anchoredPosition = endPos;

        panelSlideCoroutine = null;
    }

    #endregion

    #region Public Accessors

    /// <summary>
    /// Gets the current time value as an integer.
    /// </summary>
    public int GetTimeValue()
    {
        return TimeDigitsToSeconds(timeDigits);
    }

    /// <summary>
    /// Gets the current duck count value as an integer.
    /// </summary>
    public int GetDuckCount()
    {
        if (currentState == SetupState.Names && DataManager.Instance != null)
        {
            return DataManager.Instance.GetDuckCount();
        }

        if (int.TryParse(duckCountString, out int value))
        {
            return value;
        }
        return 0;
    }

    /// <summary>
    /// L?y gi� tr? quantity (d?ng s?) m� ngu?i ch?i ? �, kh�ng ph? thu?c Names mode.
    /// D�ng ?? l?u sang RaceConfig.quantityNumeric ?? c� th? kh�i ph?c khi quay l?i SetupScene.
    /// </summary>
    public int GetNumericQuantity()
    {
        if (int.TryParse(duckCountString, out int value))
        {
            return value;
        }
        return 0;
    }

    /// <summary>
    /// Gets the current setup state.
    /// </summary>
    public SetupState GetCurrentState()
    {
        return currentState;
    }

    /// <summary>
    /// Gets the currently active board.
    /// </summary>
    public ActiveBoard GetActiveBoard()
    {
        return activeBoard;
    }

    #endregion
}
