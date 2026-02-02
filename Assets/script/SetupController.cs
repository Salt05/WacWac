using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Quản lý màn hình thiết lập: nhập số (thời gian hoặc số lượng vịt), mở NameInputPanel, lưu vào RaceConfig
public class SetupController : MonoBehaviour
{
    public enum InputMode { Time, DuckCount }

    [Header("Mode")]
    public Button modeTimeButton;
    public Button modeDuckButton;
    public TextMeshProUGUI modeTimeLabel;
    public TextMeshProUGUI modeDuckLabel;

    [Header("Time / Ducks display")]
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI duckCountText;

    [Header("Keypad")]
    public Button[] digitButtons; // 0..9
    public Button clearButton;
    public Button setNamesButton;
    public Button okButton;

    [Header("Name Panel")]
    public NameInputPanel nameInputPanel; // assign in Inspector

    [Header("Limits")]
    public int maxTimeDigits = 6; // up to HHMMSS
    public int maxDuckDigits = 2; // up to 99

    [Header("Defaults")]
    public int defaultTimeSeconds = 15;
    public int defaultDuckCount = 10;

    private InputMode mode = InputMode.Time;

    // buffers
    private List<int> timeDigits = new List<int>(); // right->left
    private List<int> duckDigits = new List<int>();

    private void Start()
    {
        // buttons
        for (int i = 0; i < digitButtons.Length; i++)
        {
            int n = i;
            digitButtons[i].onClick.AddListener(() => OnDigit(n));
        }
        clearButton.onClick.AddListener(OnClear);
        modeTimeButton.onClick.AddListener(() => SetMode(InputMode.Time));
        modeDuckButton.onClick.AddListener(() => { SetMode(InputMode.DuckCount); OnModeDuckClicked(); });
        setNamesButton.onClick.AddListener(OnSetNamesPressed);
        okButton.onClick.AddListener(OnOK);

        // init from RaceConfig if exists
        if (RaceConfig.Instance != null)
        {
            timeText.text = FormatFromSeconds(RaceConfig.Instance.durationSeconds);
            duckCountText.text = RaceConfig.Instance.duckCount.ToString();
        }
        else
        {
            timeText.text = FormatFromSeconds(defaultTimeSeconds);
            duckCountText.text = defaultDuckCount.ToString();
        }

        SetMode(InputMode.Time);
    }

    private void SetMode(InputMode m)
    {
        mode = m;
        modeTimeLabel.color = (m == InputMode.Time) ? Color.yellow : Color.white;
        modeDuckLabel.color = (m == InputMode.DuckCount) ? Color.yellow : Color.white;
    }

    private void OnDigit(int n)
    {
        if (mode == InputMode.Time)
        {
            if (timeDigits.Count >= maxTimeDigits) return;
            // insert at head (rightmost)
            timeDigits.Insert(0, n);
            UpdateTimeDisplayFromDigits();
        }
        else
        {
            if (duckDigits.Count >= maxDuckDigits) return;
            duckDigits.Insert(0, n);
            UpdateDuckDisplayFromDigits();
        }
    }

    private void OnClear()
    {
        timeDigits.Clear();
        duckDigits.Clear();
        UpdateTimeDisplayFromDigits();
        UpdateDuckDisplayFromDigits();
    }

    // New handler for Set Names button - shows NameInputPanel without clearing previous content
    private void OnSetNamesPressed()
    {
        NameInputPanel panel = nameInputPanel;
        if (panel == null)
        {
            panel = FindObjectOfType<NameInputPanel>();
        }
        if (panel == null) return;

        // Use panel.Show() which restores saved names into inputField
        panel.Show();

        // Record preference: user clicked Set Names -> prefer names
        if (RaceConfig.Instance != null)
        {
            RaceConfig.Instance.namePreference = RaceConfig.NameSourcePreference.PreferNames;
        }
    }

    private void OnModeDuckClicked()
    {
        // User clicked the Duck mode button -> prefer numbers
        if (RaceConfig.Instance != null)
        {
            RaceConfig.Instance.namePreference = RaceConfig.NameSourcePreference.PreferNumbers;
        }
    }

    private void OnOK()
    {
        int seconds = timeDigits.Count == 0 ? defaultTimeSeconds : SecondsFromDigits(timeDigits);
        int ducks = duckDigits.Count == 0 ? defaultDuckCount : IntFromDigits(duckDigits);

        // Clamp to minimums: time >= 10s, ducks >= 3
        const int MinTimeSeconds = 10;
        const int MinDuckCount = 3;
        if (seconds < MinTimeSeconds)
        {
            seconds = MinTimeSeconds;
        }
        if (ducks < MinDuckCount)
        {
            ducks = MinDuckCount;
        }

        // Ensure RaceConfig exists in SetupScene; do NOT auto-create it here to avoid duplicates
        if (RaceConfig.Instance == null)
        {
            Debug.LogError("RaceConfig.Instance is null. Please create RaceConfig in SetupScene before starting the race.");
            return;
        }

        RaceConfig.Instance.durationSeconds = seconds;
        RaceConfig.Instance.duckCount = ducks;

        // Load RaceScene (RaceController will handle loading UI and spawn)
        SceneManager.LoadScene("RaceScene");
    }

    private void UpdateTimeDisplayFromDigits()
    {
        int seconds = timeDigits.Count == 0 ? defaultTimeSeconds : SecondsFromDigits(timeDigits);
        timeText.text = FormatFromSeconds(seconds);
    }

    private void UpdateDuckDisplayFromDigits()
    {
        int v = duckDigits.Count == 0 ? defaultDuckCount : IntFromDigits(duckDigits);
        duckCountText.text = v.ToString();
    }

    private int IntFromDigits(List<int> ds)
    {
        int val = 0;
        for (int i = ds.Count - 1; i >= 0; i--)
        {
            val = val * 10 + ds[i];
        }
        return val;
    }

    private int SecondsFromDigits(List<int> ds)
    {
        // ds: right->left digits, up to 6: s1 s10 m1 m10 h1 h10
        int s = 0, m = 0, h = 0;
        if (ds.Count >= 1) s += ds[0];
        if (ds.Count >= 2) s += ds[1] * 10;
        if (ds.Count >= 3) m += ds[2];
        if (ds.Count >= 4) m += ds[3] * 10;
        if (ds.Count >= 5) h += ds[4];
        if (ds.Count >= 6) h += ds[5] * 10;
        // clamp
        if (s > 59) s = 59;
        if (m > 59) m = 59;
        if (h > 99) h = 99;
        return s + m * 60 + h * 3600;
    }

    private string FormatFromSeconds(int totalSeconds)
    {
        if (totalSeconds <= 0) return "00:00";
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;
        if (h > 0) return string.Format("{0:D2}:{1:D2}:{2:D2}", h, m, s);
        return string.Format("{0:D2}:{1:D2}", m, s);
    }
}
