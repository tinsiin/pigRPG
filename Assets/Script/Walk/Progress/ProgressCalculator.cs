using System.Collections.Generic;

/// <summary>
/// ゲートと出口の進捗状態を計算するクラス
/// アロケーションを最小化するためにバッファを再利用
/// </summary>
public sealed class ProgressCalculator
{
    // 再利用バッファ
    private readonly List<ProgressEntry> entryBuffer = new List<ProgressEntry>(8);
    private readonly List<(GateMarker marker, GateRuntimeState state)> gateStateBuffer = new List<(GateMarker, GateRuntimeState)>(8);

    // キャッシュ用配列（サイズが変わった時だけ再生成）
    private ProgressEntry[] cachedEntries = System.Array.Empty<ProgressEntry>();

    public ProgressSnapshot Calculate(
        NodeSO node,
        GateResolver gateResolver,
        WalkCounters counters)
    {
        if (node == null || gateResolver == null || counters == null)
        {
            return ProgressSnapshot.Empty;
        }

        entryBuffer.Clear();

        var trackProgress = counters.TrackProgress;
        var allGatesCleared = gateResolver.AllGatesCleared(node);

        // バッファを使ってソート済みゲート状態を取得（アロケーションなし）
        gateResolver.GetAllStates(node, gateStateBuffer);

        int orderIndex = 1;
        int remainingGateCount = 0;

        for (int i = 0; i < gateStateBuffer.Count; i++)
        {
            var (marker, state) = gateStateBuffer[i];

            var isActive = !state.IsCleared &&
                           state.CooldownRemaining <= 0 &&
                           state.ResolvedPosition <= trackProgress;

            var entry = ProgressEntry.CreateGate(
                orderIndex,
                marker.GateId,
                state.ResolvedPosition,
                state.IsCleared,
                isActive,
                state.CooldownRemaining > 0);

            entryBuffer.Add(entry);
            orderIndex++;

            // Repeatable gates don't count as "remaining" for exit blocking
            if (!state.IsCleared && !marker.Repeatable)
            {
                remainingGateCount++;
            }
        }

        // 出口を最後に追加
        var exitSpawn = node.ExitSpawn;
        var exitMode = exitSpawn?.Mode ?? ExitSpawnMode.None;
        int exitPosition = CalculateExitPosition(exitSpawn, counters);

        if (exitMode != ExitSpawnMode.None)
        {
            var exitEntry = ProgressEntry.CreateExit(orderIndex, exitPosition);
            entryBuffer.Add(exitEntry);
        }

        // 次のエントリを計算
        int? nextEntryIndex = null;
        int? stepsToNext = null;
        float? probability = null;

        for (int i = 0; i < entryBuffer.Count; i++)
        {
            var entry = entryBuffer[i];

            if (entry.Type == ProgressEntry.EntryType.Gate)
            {
                if (!entry.IsCleared && !entry.IsCoolingDown)
                {
                    nextEntryIndex = i;
                    stepsToNext = entry.Position > trackProgress
                        ? entry.Position - trackProgress
                        : 0;
                    break;
                }
            }
            else if (entry.Type == ProgressEntry.EntryType.Exit)
            {
                if (!allGatesCleared && exitSpawn?.RequireAllGatesCleared == true)
                {
                    // ゲートがクリアされるまで出口はブロック
                    nextEntryIndex = i;
                    stepsToNext = null;
                    probability = null;
                }
                else if (exitMode == ExitSpawnMode.Steps)
                {
                    nextEntryIndex = i;
                    stepsToNext = exitPosition > counters.NodeSteps
                        ? exitPosition - counters.NodeSteps
                        : 0;
                }
                else if (exitMode == ExitSpawnMode.Probability)
                {
                    nextEntryIndex = i;
                    stepsToNext = null;
                    probability = exitSpawn?.Rate ?? 0f;
                }
            }
        }

        // 配列のサイズが変わった時だけ再生成
        if (cachedEntries.Length != entryBuffer.Count)
        {
            cachedEntries = new ProgressEntry[entryBuffer.Count];
        }

        // バッファから配列にコピー
        for (int i = 0; i < entryBuffer.Count; i++)
        {
            cachedEntries[i] = entryBuffer[i];
        }

        var exitRequiresAllGatesCleared = exitSpawn?.RequireAllGatesCleared ?? true;

        return new ProgressSnapshot(
            cachedEntries,
            trackProgress,
            nextEntryIndex,
            stepsToNext,
            probability,
            allGatesCleared,
            exitMode,
            remainingGateCount,
            exitRequiresAllGatesCleared);
    }

    private static int CalculateExitPosition(ExitSpawnRule exitSpawn, WalkCounters counters)
    {
        if (exitSpawn == null) return 0;

        switch (exitSpawn.Mode)
        {
            case ExitSpawnMode.Steps:
                return exitSpawn.Steps;
            case ExitSpawnMode.Probability:
                return counters.NodeSteps; // 確率モードでは現在位置
            default:
                return 0;
        }
    }
}
