using System.Collections.Generic;

public sealed class MetricsContext
{
    public string ScenarioName;      // 例: "Walk(1)"
    public string PresetSummary;     // 例: "true 4 0.01 0.06"
    public int PresetIndex;          // 0-based index
    public int RunIndex;             // 1..N
    public Dictionary<string, string> Tags; // 任意の追加タグ

    public static readonly MetricsContext None = new MetricsContext();
}
