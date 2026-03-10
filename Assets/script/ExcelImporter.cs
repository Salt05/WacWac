using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attach to the Import button GameObject.
/// Assign <see cref="nameInputPanel"/> in the Inspector.
///
/// Click flow:
///   WebGL     -> JS file-picker -> OnFileLoaded -> parse -> NameInputPanel.SetImportedText
///   Editor    -> EditorUtility.OpenFilePanel -> parse -> NameInputPanel.SetImportedText
///
/// Supported formats:
///   .csv  - first column, one name per row (UTF-8)
///   .xlsx - column A of Sheet1
/// </summary>
public class ExcelImporter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("NameInputPanel that will receive the imported names.")]
    public NameInputPanel nameInputPanel;

    [Tooltip("SetupUIManager để refresh ScrollView sau khi import.")]
    public SetupUIManager setupUIManager;

    [Tooltip("Button that triggers the import. If null, the Button on this GameObject is used.")]
    public Button importButton;

    [Header("Settings")]
    [Tooltip("Maximum number of names to import.")]
    public int maxNames = 99;

    [Tooltip("Maximum characters per name (longer names are trimmed).")]
    public int maxCharsPerName = 10;

    // ---------------------------------------------------------------

    private void Awake()
    {
        if (importButton == null)
            importButton = GetComponent<Button>();
    }

    private void Start()
    {
        if (importButton != null)
            importButton.onClick.AddListener(OnImportClicked);
    }

    // ---------------------------------------------------------------
    //  Button handler
    // ---------------------------------------------------------------

    private void OnImportClicked()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        OpenFilePickerWebGL();
#elif UNITY_EDITOR
        OpenFilePickerEditor();
#else
        Debug.LogWarning("ExcelImporter: File picker not supported on this platform.");
#endif
    }

    // ---------------------------------------------------------------
    //  WebGL path  (JavaScript plugin)
    // ---------------------------------------------------------------

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void JS_OpenFilePicker(string goName, string cbMethod, string errMethod);

    private void OpenFilePickerWebGL()
    {
        JS_OpenFilePicker(gameObject.name, nameof(OnFileLoaded), nameof(OnFileError));
    }
#endif

    // ---------------------------------------------------------------
    //  Editor path
    // ---------------------------------------------------------------

#if UNITY_EDITOR
    private void OpenFilePickerEditor()
    {
        string path = EditorUtility.OpenFilePanel("Chọn file Excel hoặc CSV", "", "xlsx,csv");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            List<string> names;
            if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                names = ParseCsv(text);
            }
            else
            {
                byte[] bytes = File.ReadAllBytes(path);
                names = ParseXlsx(bytes);
            }
            ApplyNames(names);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ExcelImporter: {ex.Message}");
        }
    }
#endif

    // ---------------------------------------------------------------
    //  SendMessage callbacks from JavaScript (WebGL only)
    // ---------------------------------------------------------------

    /// <summary>Called via SendMessage from JS when a file is successfully read.</summary>
    public void OnFileLoaded(string data)
    {
        try
        {
            List<string> names;
            if (data.StartsWith("csv:"))
            {
                names = ParseCsv(data.Substring(4));
            }
            else if (data.StartsWith("xlsx:"))
            {
                byte[] bytes = Convert.FromBase64String(data.Substring(5));
                names = ParseXlsx(bytes);
            }
            else
            {
                Debug.LogError("ExcelImporter: Unknown file data format.");
                return;
            }
            ApplyNames(names);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ExcelImporter: Parse error - {ex.Message}");
        }
    }

    /// <summary>Called via SendMessage from JS when an error occurs.</summary>
    public void OnFileError(string message)
    {
        Debug.LogError($"ExcelImporter: JS error - {message}");
    }

    // ---------------------------------------------------------------
    //  Parsers
    // ---------------------------------------------------------------

    private List<string> ParseCsv(string text)
    {
        var names = new List<string>();
        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (names.Count >= maxNames) break;

            // take first column only
            string raw = line.Contains(',') ? line.Split(',')[0] : line;
            raw = raw.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (raw.Length > maxCharsPerName) raw = raw.Substring(0, maxCharsPerName);
            names.Add(raw);
        }
        return names;
    }

    private List<string> ParseXlsx(byte[] data)
    {
        var names = new List<string>();

        using (var ms = new MemoryStream(data))
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            // --- Shared Strings ---
            var sharedStrings = new List<string>();
            var ssEntry = zip.GetEntry("xl/sharedStrings.xml");
            if (ssEntry != null)
            {
                using (var s = ssEntry.Open())
                {
                    var doc = new XmlDocument();
                    doc.Load(s);
                    var xmlns = doc.DocumentElement?.NamespaceURI
                                ?? "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    var mgr = new XmlNamespaceManager(doc.NameTable);
                    mgr.AddNamespace("x", xmlns);

                    var siNodes = doc.SelectNodes("//x:si", mgr);
                    if (siNodes != null)
                    {
                        foreach (XmlNode si in siNodes)
                        {
                            var sb = new StringBuilder();
                            var tNodes = si.SelectNodes(".//x:t", mgr);
                            if (tNodes != null)
                                foreach (XmlNode t in tNodes)
                                    sb.Append(t.InnerText);
                            sharedStrings.Add(sb.ToString());
                        }
                    }
                }
            }

            // --- Sheet1 ---
            var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml")
                          ?? zip.GetEntry("xl/worksheets/Sheet1.xml");
            if (sheetEntry == null) return names;

            using (var s = sheetEntry.Open())
            {
                var doc = new XmlDocument();
                doc.Load(s);
                var xmlns = doc.DocumentElement?.NamespaceURI
                            ?? "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                var mgr = new XmlNamespaceManager(doc.NameTable);
                mgr.AddNamespace("x", xmlns);

                var rows = doc.SelectNodes("//x:row", mgr);
                if (rows == null) return names;

                foreach (XmlNode row in rows)
                {
                    if (names.Count >= maxNames) break;

                    var cells = row.SelectNodes("x:c", mgr);
                    if (cells == null) continue;

                    foreach (XmlNode cell in cells)
                    {
                        // Only column A: ref = "A1", "A2", ..., "A99", "A100"
                        // Reference always starts with 'A' and next char is a digit
                        string cellRef = cell.Attributes?["r"]?.Value ?? "";
                        if (cellRef.Length < 2 || cellRef[0] != 'A' || !char.IsDigit(cellRef[1]))
                            continue;

                        string cellType = cell.Attributes?["t"]?.Value ?? "";
                        string value    = "";

                        var vNode  = cell.SelectSingleNode("x:v",      mgr);
                        var isNode = cell.SelectSingleNode("x:is/x:t", mgr);

                        if (cellType == "s" && vNode != null)
                        {
                            // Shared string index
                            if (int.TryParse(vNode.InnerText, out int idx) && idx < sharedStrings.Count)
                                value = sharedStrings[idx];
                        }
                        else if (cellType == "inlineStr" && isNode != null)
                        {
                            value = isNode.InnerText;
                        }
                        else if (vNode != null)
                        {
                            value = vNode.InnerText;
                        }

                        value = value.Trim();
                        if (string.IsNullOrWhiteSpace(value)) break; // empty cell A → skip row
                        if (value.Length > maxCharsPerName) value = value.Substring(0, maxCharsPerName);
                        names.Add(value);
                        break; // only one cell (column A) per row
                    }
                }
            }
        }

        return names;
    }

    // ---------------------------------------------------------------
    //  Apply to NameInputPanel
    // ---------------------------------------------------------------

    private void ApplyNames(List<string> names)
    {
        if (names == null || names.Count == 0)
        {
            Debug.LogWarning("ExcelImporter: No names found in file.");
            return;
        }

        if (nameInputPanel == null)
        {
            Debug.LogWarning("ExcelImporter: nameInputPanel reference is not assigned.");
            return;
        }

        // ScrollView dùng reverseArrangement nên đảo ngược để hiển thị đúng thứ tự Excel (row 1 ở trên cùng)
        names.Reverse();
        nameInputPanel.SetImportedText(string.Join("\n", names));

        // Refresh ScrollView UI immediately
        if (setupUIManager == null)
            setupUIManager = FindObjectOfType<SetupUIManager>();
        if (setupUIManager != null)
            setupUIManager.RefreshNameList();

        Debug.Log($"ExcelImporter: Imported {names.Count} names.");
    }
}
