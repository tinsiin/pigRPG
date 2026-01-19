using System.Collections.Generic;
using UnityEngine;

public sealed class WalkDebugLog
{
    private static WalkDebugLog instance;
    public static WalkDebugLog Instance => instance ??= new WalkDebugLog();

    private readonly List<RollLogEntry> rollLogs = new();
    private readonly List<string> sideObjectHistory = new();
    private readonly List<string> varietyHistory = new();
    private int maxEntries = 1000;
    private bool enabled = false;

    public bool Enabled
    {
        get => enabled;
        set => enabled = value;
    }

    public int MaxEntries
    {
        get => maxEntries;
        set => maxEntries = Mathf.Max(1, value);
    }

    public void LogRoll(RollLogEntry entry)
    {
        if (!enabled || entry == null) return;

        rollLogs.Add(entry);
        TrimIfNeeded(rollLogs);
    }

    public void LogSideObjectSelection(string sideObjectId)
    {
        if (!enabled || string.IsNullOrEmpty(sideObjectId)) return;

        sideObjectHistory.Add(sideObjectId);
        TrimIfNeeded(sideObjectHistory);
    }

    public void LogVarietyUpdate(IEnumerable<string> history)
    {
        if (!enabled) return;

        varietyHistory.Clear();
        if (history != null)
        {
            foreach (var id in history)
            {
                varietyHistory.Add(id);
            }
        }
    }

    public IReadOnlyList<RollLogEntry> GetRollLogs() => rollLogs;
    public IReadOnlyList<string> GetSideObjectHistory() => sideObjectHistory;
    public IReadOnlyList<string> GetVarietyHistory() => varietyHistory;

    public void Clear()
    {
        rollLogs.Clear();
        sideObjectHistory.Clear();
        varietyHistory.Clear();
    }

    public string ExportAsText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Walk Debug Log ===");
        sb.AppendLine();

        sb.AppendLine("--- Roll Logs ---");
        foreach (var entry in rollLogs)
        {
            sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] {entry.Type} Step={entry.Step} Node={entry.NodeId}");
            sb.AppendLine($"  Selected: {entry.SelectedId ?? "(none)"} Roll={entry.Roll:0.###}");
            if (!string.IsNullOrEmpty(entry.ExcludeReason))
            {
                sb.AppendLine($"  ExcludeReason: {entry.ExcludeReason}");
            }
            if (entry.Candidates.Count > 0)
            {
                sb.AppendLine("  Candidates:");
                foreach (var c in entry.Candidates)
                {
                    var excludeInfo = c.Excluded ? $" (excluded: {c.ExcludeReason})" : "";
                    sb.AppendLine($"    - {c.Id}: weight={c.Weight:0.###} effective={c.EffectiveWeight:0.###}{excludeInfo}");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("--- Side Object History ---");
        for (int i = 0; i < sideObjectHistory.Count; i++)
        {
            sb.AppendLine($"  {i + 1}. {sideObjectHistory[i]}");
        }

        sb.AppendLine();
        sb.AppendLine("--- Variety History ---");
        sb.AppendLine($"  [{string.Join(", ", varietyHistory)}]");

        return sb.ToString();
    }

    private void TrimIfNeeded<T>(List<T> list)
    {
        while (list.Count > maxEntries)
        {
            list.RemoveAt(0);
        }
    }
}
