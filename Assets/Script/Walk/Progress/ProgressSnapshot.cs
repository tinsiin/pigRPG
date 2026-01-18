using System.Collections.Generic;

/// <summary>
/// 進捗スナップショット（ゲートと出口の現在状態）
/// </summary>
public readonly struct ProgressSnapshot
{
    public IReadOnlyList<ProgressEntry> Entries { get; }
    public int CurrentTrackProgress { get; }
    public int? NextEntryIndex { get; }
    public int? StepsToNextEntry { get; }
    public float? ProbabilityOfNextEntry { get; }
    public bool AllGatesCleared { get; }
    public ExitSpawnMode ExitMode { get; }
    public int RemainingGateCount { get; }
    public bool ExitRequiresAllGatesCleared { get; }

    public ProgressSnapshot(
        IReadOnlyList<ProgressEntry> entries,
        int currentTrackProgress,
        int? nextEntryIndex,
        int? stepsToNextEntry,
        float? probabilityOfNextEntry,
        bool allGatesCleared,
        ExitSpawnMode exitMode,
        int remainingGateCount,
        bool exitRequiresAllGatesCleared = true)
    {
        Entries = entries;
        CurrentTrackProgress = currentTrackProgress;
        NextEntryIndex = nextEntryIndex;
        StepsToNextEntry = stepsToNextEntry;
        ProbabilityOfNextEntry = probabilityOfNextEntry;
        AllGatesCleared = allGatesCleared;
        ExitMode = exitMode;
        RemainingGateCount = remainingGateCount;
        ExitRequiresAllGatesCleared = exitRequiresAllGatesCleared;
    }

    public static ProgressSnapshot Empty => new ProgressSnapshot(
        System.Array.Empty<ProgressEntry>(),
        0,
        null,
        null,
        null,
        true,
        ExitSpawnMode.None,
        0,
        true);

    public bool HasNextEntry => NextEntryIndex.HasValue;

    public ProgressEntry? GetNextEntry()
    {
        if (!NextEntryIndex.HasValue || Entries == null) return null;
        var idx = NextEntryIndex.Value;
        if (idx < 0 || idx >= Entries.Count) return null;
        return Entries[idx];
    }
}
