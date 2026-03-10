using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel nh?p t�n v?t
public class NameInputPanel : MonoBehaviour
{
    public GameObject root; // panel root
    public TMP_InputField inputField; // multiline input
    public Button clearButton;
    public Button doneButton;

    public int maxLines = 99;
    public int charsPerLine = 10;

    [Header("UI SFX")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField, Range(0f, 1f)] private float buttonClickVolume = 0.8f;
    [SerializeField] private AudioSource uiAudioSource;

    private void Start()
    {
        if (clearButton != null) clearButton.onClick.AddListener(OnClear);
        if (clearButton != null) clearButton.onClick.AddListener(PlayButtonClick);
        if (doneButton != null) doneButton.onClick.AddListener(OnDone);
        if (doneButton != null) doneButton.onClick.AddListener(PlayButtonClick);
        // ensure input allows multiline with Enter
        if (inputField != null)
        {
            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            inputField.onValueChanged.AddListener(OnInputChanged);
        }
        Hide();
    }

    private void PlayButtonClick()
    {
        if (buttonClickClip == null)
        {
            return;
        }

        AudioSource source = uiAudioSource;
        if (source == null)
        {
            source = GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }
            source.playOnAwake = false;
            uiAudioSource = source;
        }

        source.PlayOneShot(buttonClickClip, buttonClickVolume);
    }

    public void Show()
    {
        if (root != null) root.SetActive(true);
        if (inputField != null)
        {
            // populate from RaceConfig if available so content persists across opens
            if (RaceConfig.Instance != null && !string.IsNullOrEmpty(RaceConfig.Instance.duckNamesRaw))
            {
                inputField.text = RaceConfig.Instance.duckNamesRaw;
            }
            else if (RaceConfig.Instance != null && RaceConfig.Instance.duckNames != null && RaceConfig.Instance.duckNames.Length > 0)
            {
                inputField.text = string.Join("\n", RaceConfig.Instance.duckNames);
            }
            inputField.ActivateInputField();
            inputField.Select();
        }
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    private void OnClear()
    {
        if (inputField != null) inputField.text = string.Empty;
        // clear saved names as well
        if (RaceConfig.Instance != null)
        {
            RaceConfig.Instance.duckNames = new string[0];
            RaceConfig.Instance.duckNamesRaw = string.Empty;
        }
        if (DataManager.Instance != null)
            DataManager.Instance.ClearAllDuckNames();
    }

    private void OnInputChanged(string newText)
    {
        // live-save current lines up to limits
        if (inputField == null) return;
        var lines = inputField.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        var names = new List<string>();
        for (int i = 0; i < lines.Length && i < maxLines; i++)
        {
            var line = lines[i];
            if (line.Length > charsPerLine) line = line.Substring(0, charsPerLine);
            names.Add(line);
        }
        // Do NOT create RaceConfig here. RaceConfig should be created in SetupScene only.
        if (RaceConfig.Instance == null)
        {
            // No RaceConfig to save to; just return
            return;
        }
        RaceConfig.Instance.duckNames = names.ToArray();
        // persist raw multiline text so panel can restore exact content
        RaceConfig.Instance.duckNamesRaw = inputField.text;
    }

    private void OnDone()
    {
        if (inputField == null) return;
        var lines = inputField.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        var names = new List<string>();
        for (int i = 0; i < lines.Length && i < maxLines; i++)
        {
            var line = lines[i];
            if (line.Length > charsPerLine) line = line.Substring(0, charsPerLine);
            names.Add(line);
        }

        // Do NOT create RaceConfig here. RaceConfig instance must be created in SetupScene.
        if (RaceConfig.Instance == null)
        {
            // nothing to save to; just hide the panel
            Hide();
            return;
        }

        RaceConfig.Instance.duckNames = names.ToArray();
        RaceConfig.Instance.duckNamesRaw = inputField.text;
        if (DataManager.Instance != null)
            DataManager.Instance.SetDuckNames(names);
        Hide();
    }
}
