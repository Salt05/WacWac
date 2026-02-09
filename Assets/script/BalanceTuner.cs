using UnityEngine;

/// <summary>
/// Centralized balancing settings editable in the Inspector.
/// Phase: Polish/Optimization.
///
/// Usage: place one BalanceTuner in the race scene.
/// Code can access BalanceTuner.Instance.Settings.
/// </summary>
public sealed class BalanceTuner : MonoBehaviour
{
    public static BalanceTuner Instance { get; private set; }

    [System.Serializable]
    public class BalanceSettings
    {
        [Header("Tier Distribution (percent, should sum to 100)")]
        [Tooltip("% số vịt thuộc tier Chậm (Slow). Nên để tổng 4 mục = 100%.")]
        [Range(0, 100)] public int slowPercent = 20;

        [Tooltip("% số vịt thuộc tier Trung bình (Average). Nên để tổng 4 mục = 100%.")]
        [Range(0, 100)] public int averagePercent = 50;

        [Tooltip("% số vịt thuộc tier Nhanh (Fast). Nên để tổng 4 mục = 100%.")]
        [Range(0, 100)] public int fastPercent = 25;

        [Tooltip("% số vịt thuộc tier Rất nhanh (VeryFast). Nên để tổng 4 mục = 100%.")]
        [Range(0, 100)] public int veryFastPercent = 5;

        [Header("Tier Multipliers")]
        [Tooltip("Hệ số tốc độ cơ bản cho tier Chậm (Slow). <1 = chậm hơn trung bình.")]
        public float slowMultiplier = 0.7f;

        [Tooltip("Hệ số tốc độ cơ bản cho tier Trung bình (Average). 1.0 = chuẩn.")]
        public float averageMultiplier = 1.0f;

        [Tooltip("Hệ số tốc độ cơ bản cho tier Nhanh (Fast). >1 = nhanh hơn.")]
        public float fastMultiplier = 1.3f;

        [Tooltip("Hệ số tốc độ cơ bản cho tier Rất nhanh (VeryFast). Giá trị cao tạo vịt cực nhanh.")]
        public float veryFastMultiplier = 1.6f;

        [Header("Personality Distribution (percent, should sum to 100)")]
        [Tooltip("% số vịt có tính cách Steady (ổn định, ít dao động tốc độ).")]
        [Range(0, 100)] public int steadyPercent = 20;

        [Tooltip("% số vịt Erratic (dao động mạnh, khó đoán).")]
        [Range(0, 100)] public int erraticPercent = 20;

        [Tooltip("% số vịt Sprinter (tăng tốc mạnh ở đoạn cuối, nếu được dùng).")]
        [Range(0, 100)] public int sprinterPercent = 20;

        [Tooltip("% số vịt Starter (mạnh ở đầu race, sau đó giảm dần).")]
        [Range(0, 100)] public int starterPercent = 20;

        [Tooltip("% số vịt Choker (dễ hụt hơi khi gần đích nếu được dùng).")]
        [Range(0, 100)] public int chokerPercent = 20;

        [Header("Rubber-banding")]
        [Tooltip("Giảm tốc tối thiểu áp cho leader (leaderNerf). 1.0 = không giảm.")]
        [Range(0.7f, 1.0f)] public float leaderNerfMin = 0.90f;

        [Tooltip("Giảm tốc tối đa áp cho leader. Giá trị ngẫu nhiên trong [Min, Max].")]
        [Range(0.7f, 1.0f)] public float leaderNerfMax = 0.95f;

        [Tooltip("Buff tốc tối thiểu cho vịt ở phía sau (backBoost). 1.0 = không buff.")]
        [Range(1.0f, 1.3f)] public float backBoostMin = 1.05f;

        [Tooltip("Buff tốc tối đa cho vịt phía sau. Giá trị càng cao, comeback càng mạnh.")]
        [Range(1.0f, 1.3f)] public float backBoostMax = 1.10f;

        [Header("Stamina")]
        [Tooltip("Hệ số nhân cho tốc độ tụt Stamina: drain = dt * speed * tierStaminaMultiplier * staminaDrainRate.")]
        public float staminaDrainRate = 1.0f;

        [Tooltip("Tốc độ hồi Stamina mỗi giây khi vịt chạy chậm/đứng yên.")]
        public float staminaRegenRate = 0.015f;

        [Tooltip("Ngưỡng Stamina (0..1) dưới đó vịt được coi là 'thấp năng lượng' – dùng để tăng độ dao động, giảm tốc tối đa.")]
        public float lowStaminaThreshold = 0.3f;

        [Header("Comeback Mechanic")]
        [Tooltip("Thời gian (giây) áp dụng buff comeback sau khi phát hiện vịt bị tụt rank.")]
        public float comebackDuration = 3f;

        [Tooltip("Hệ số buff comeback (1.2 = tăng 20% tốc độ trong thời gian comeback).")]
        public float comebackMultiplier = 1.2f;

        [Tooltip("Khoảng thời gian (giây) dùng để quan sát xem vịt có bị tụt hạng hay không.")]
        public float rankDropWindowSeconds = 5f;

        [Header("Momentum")]
        [Tooltip("Bonus tốc độ tối đa từ Momentum (0..0.5). Giá trị càng cao, chạy ổn định càng được thưởng nhiều.")]
        [Range(0f, 0.5f)] public float momentumMaxBonus = 0.15f;

        [Tooltip("Tốc độ tích lũy Momentum mỗi giây khi vịt chạy ổn định.")]
        public float momentumGainPerSecond = 0.35f;

        [Header("Speed Transition")]
        [Tooltip("Tỉ lệ dùng để tính thời gian chuyển tốc: transitionTime = effectiveRandomIntervalC * transitionTimeRatio.")]
        [Range(0.05f, 1.0f)]
        public float transitionTimeRatio = 0.333f;
    }

    public BalanceSettings Settings = new BalanceSettings();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
}
