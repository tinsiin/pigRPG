using UnityEngine;

[CreateAssetMenu(menuName = "Benchmark/MetricsSettings", fileName = "MetricsSettings")]
public sealed class MetricsSettings : ScriptableObject
{
    [Header("Metrics 計測の有効/無効")]
    public bool enableMetrics = true;
    public bool enableSpan = true;
    public bool enableJitter = true;

    [TextArea]
    public string description;
}
