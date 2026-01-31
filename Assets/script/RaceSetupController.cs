using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Controller cho màn hình thi?t l?p cu?c ?ua
public class RaceSetupController : MonoBehaviour
{
    [Header("Time display")]
    public TextMeshProUGUI timeText; // hi?n th? th?i gian l?n

    [Header("Numeric keypad")]
    public Button[] digitButtons; // 0-9
    public Button setButton;
    public Button clearButton;

    [Header("Ducks")]
    public Slider duckSlider;
    public TextMeshProUGUI duckCountText;
    public Transform previewParent;
    public GameObject duckPreviewPrefab; // prefab cho 1 con v?t preview (có Image + Text)
    public int previewSlots = 6;

    [Header("Navigation")]
    public Button backButton;
    public Button startButton;

    [Header("Limits")]
    public int minDucks = 2;
    public int maxDucks = 100;
    public int maxHours = 99; // max HH

    // internal
    private int[] digits = new int[6]; // right->left: ss mm hh -> [0] = sec ones, [5] = hour tens
    private bool hasSet = false;

    private void Start()
    {
        // setup defaults
        ResetDigits();
        UpdateTimeText();

        // slider
        duckSlider.minValue = minDucks;
        duckSlider.maxValue = maxDucks;
        duckSlider.wholeNumbers = true;
        duckSlider.value = RaceConfig.Instance != null ? RaceConfig.Instance.duckCount : 5;
        UpdateDuckCountText((int)duckSlider.value);
        duckSlider.onValueChanged.AddListener((v) => UpdateDuckCountText((int)v));

        // keypad
        for (int i = 0; i < digitButtons.Length; i++)
        {
            int n = i; // capture
            digitButtons[i].onClick.AddListener(() => OnDigitPressed(n));
        }
        clearButton.onClick.AddListener(OnClearPressed);
        setButton.onClick.AddListener(OnSetPressed);

        startButton.onClick.AddListener(OnStartPressed);
        backButton.onClick.AddListener(OnBackPressed);

        // preview
        PopulatePreview();
    }

    private void ResetDigits()
    {
        for (int i = 0; i < digits.Length; i++) digits[i] = 0;
    }

    private void OnDigitPressed(int n)
    {
        // shift left (towards higher significance) and insert digit at rightmost (digits[0])
        for (int i = digits.Length - 1; i > 0; i--)
            digits[i] = digits[i - 1];
        digits[0] = n;
        ClampDigitsToMax();
        UpdateTimeText();
    }

    private void OnClearPressed()
    {
        ResetDigits();
        UpdateTimeText();
        hasSet = false;
    }

    private void OnSetPressed()
    {
        int seconds = GetTotalSeconds();
        if (seconds <= 0)
        {
            Debug.Log("Th?i gian ph?i l?n h?n 0");
            return;
        }
        hasSet = true;
        // optionally provide feedback
        Debug.Log($"Set time: {seconds}s");
    }

    private void OnStartPressed()
    {
        if (!ValidateAndStore()) return;

        // hide setup UI (assume this GameObject is the setup UI root)
        this.gameObject.SetActive(false);

        // start race in same scene: notify RaceController ho?c các h? th?ng khác
        // We'll keep RaceConfig updated and assume race scene logic listens to it or is in same scene
    }

    private void OnBackPressed()
    {
        // simple: go back to previous scene in build settings if any
        SceneManager.LoadScene("MainMenu");
    }

    private void PopulatePreview()
    {
        // clear
        foreach (Transform t in previewParent) Destroy(t.gameObject);

        int slots = previewSlots;
        for (int i = 0; i < slots; i++)
        {
            var go = Instantiate(duckPreviewPrefab, previewParent);
            // set label number
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = (i + 1).ToString();
            // set skin randomly from RaceConfig if available
            var img = go.GetComponentInChildren<UnityEngine.UI.Image>();
            if (img != null && RaceConfig.Instance != null && RaceConfig.Instance.duckSkins != null && RaceConfig.Instance.duckSkins.Length > 0)
            {
                int idx = UnityEngine.Random.Range(0, RaceConfig.Instance.duckSkins.Length);
                img.sprite = RaceConfig.Instance.duckSkins[idx];
            }
        }
    }

    private void UpdateDuckCountText(int v)
    {
        duckCountText.text = v.ToString();
        // update RaceConfig live
        if (RaceConfig.Instance != null) RaceConfig.Instance.duckCount = v;
    }

    private void UpdateTimeText()
    {
        int total = GetTotalSecondsFromDigits();
        string s = FormatTime(total);
        if (timeText != null) timeText.text = s;
    }

    private int GetTotalSecondsFromDigits()
    {
        int sec = digits[0] + digits[1] * 10;
        int min = digits[2] + digits[3] * 10;
        int hr = digits[4] + digits[5] * 10;
        return sec + min * 60 + hr * 3600;
    }

    private int GetTotalSeconds()
    {
        if (hasSet) return GetTotalSecondsFromDigits();
        // if not set, return default 15s
        return RaceConfig.Instance != null ? RaceConfig.Instance.durationSeconds : 15;
    }

    private string FormatTime(int totalSeconds)
    {
        if (totalSeconds <= 0) return "00:00";
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;
        if (h > 0)
            return string.Format("{0:D2}:{1:D2}:{2:D2}", h, m, s);
        else
            return string.Format("{0:D2}:{1:D2}", m, s);
    }

    private void ClampDigitsToMax()
    {
        // enforce max hours
        int hr = digits[4] + digits[5] * 10;
        if (hr > maxHours)
        {
            digits[4] = maxHours % 10;
            digits[5] = maxHours / 10;
        }
        // enforce seconds/minutes < 60
        int sec = digits[0] + digits[1] * 10;
        if (sec > 59)
        {
            sec = 59;
            digits[0] = sec % 10;
            digits[1] = sec / 10;
        }
        int min = digits[2] + digits[3] * 10;
        if (min > 59)
        {
            min = 59;
            digits[2] = min % 10;
            digits[3] = min / 10;
        }
    }

    private bool ValidateAndStore()
    {
        int seconds = hasSet ? GetTotalSecondsFromDigits() : (RaceConfig.Instance != null ? RaceConfig.Instance.durationSeconds : 15);
        if (seconds <= 0)
        {
            Debug.Log("Th?i gian ph?i l?n h?n 0");
            return false;
        }
        int ducks = (int)duckSlider.value;
        if (ducks < minDucks)
        {
            Debug.Log($"S? v?t ph?i >= {minDucks}");
            return false;
        }

        if (RaceConfig.Instance != null)
        {
            RaceConfig.Instance.durationSeconds = seconds;
            RaceConfig.Instance.duckCount = ducks;
        }
        else
        {
            // optional: create a RaceConfig object
            GameObject go = new GameObject("RaceConfig");
            var rc = go.AddComponent<RaceConfig>();
            rc.durationSeconds = seconds;
            rc.duckCount = ducks;
        }

        return true;
    }
}
