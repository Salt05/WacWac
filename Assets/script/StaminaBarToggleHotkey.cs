using UnityEngine;

/// <summary>
/// Optional: press a hotkey to toggle all duck stamina bars.
/// Attach to any GameObject in the scene.
/// </summary>
public sealed class StaminaBarToggleHotkey : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F1;
    public bool defaultVisible = true;

    private bool currentVisible;

    private void Start()
    {
        // Use BalanceTuner default when available.
        if (BalanceTuner.Instance != null) defaultVisible = BalanceTuner.Instance.defaultShowStaminaBars;
        currentVisible = defaultVisible;
        DuckVisualizer.SetAllStaminaBarsVisible(currentVisible);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            currentVisible = !currentVisible;
            DuckVisualizer.SetAllStaminaBarsVisible(currentVisible);
        }
    }
}
