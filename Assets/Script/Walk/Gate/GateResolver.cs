using System.Collections.Generic;

public sealed class GateResolver
{
    private readonly Dictionary<string, GateRuntimeState> gateStates = new();

    // キャッシュ: ノード初期化時にソート済みリストを保持
    private readonly List<GateMarker> sortedGatesCache = new();
    private NodeSO cachedNode;

    // キャッシュ: AllGatesCleared の結果
    private bool cachedAllGatesCleared;
    private bool allGatesClearedDirty = true;

    // Track which node the restored snapshot belongs to (for cross-node anchor rewinds)
    private string restoredFromNodeId;

    public void InitializeForNode(NodeSO node, uint nodeSeed)
    {
        gateStates.Clear();
        sortedGatesCache.Clear();
        cachedNode = node;
        allGatesClearedDirty = true;
        restoredFromNodeId = null;

        if (node?.Gates == null) return;

        var trackLength = node.TrackConfig?.Length ?? 100;

        // ゲートを追加しながらソート用リストも構築
        foreach (var gate in node.Gates)
        {
            if (gate == null) continue;
            var position = gate.PositionSpec.ResolvePosition(trackLength, nodeSeed, gate.GateId);
            gateStates[gate.GateId] = new GateRuntimeState(gate.GateId, position);
            sortedGatesCache.Add(gate);
        }

        // Order でソート（1回だけ）
        sortedGatesCache.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public void RestoreFromSnapshot(Dictionary<string, GateRuntimeState> snapshot, string sourceNodeId = null)
    {
        gateStates.Clear();
        allGatesClearedDirty = true;
        restoredFromNodeId = sourceNodeId;

        if (snapshot == null) return;
        foreach (var kvp in snapshot)
        {
            gateStates[kvp.Key] = kvp.Value.Clone();
        }
    }

    public Dictionary<string, GateRuntimeState> TakeSnapshot()
    {
        var snapshot = new Dictionary<string, GateRuntimeState>();
        foreach (var kvp in gateStates)
        {
            snapshot[kvp.Key] = kvp.Value.Clone();
        }
        return snapshot;
    }

    public GateMarker GetNextGate(NodeSO node, int trackProgress)
    {
        if (node?.Gates == null) return null;

        // キャッシュされたソート済みリストを使用（アロケーションなし）
        for (int i = 0; i < sortedGatesCache.Count; i++)
        {
            var gate = sortedGatesCache[i];
            if (gate == null) continue;

            if (!gateStates.TryGetValue(gate.GateId, out var state)) continue;
            if (state.IsCleared) continue;
            if (state.ResolvedPosition > trackProgress) continue;
            if (state.CooldownRemaining > 0) continue;

            return gate;
        }

        return null;
    }

    public bool AllGatesCleared(NodeSO node)
    {
        // キャッシュが有効ならそれを返す
        if (!allGatesClearedDirty)
        {
            return cachedAllGatesCleared;
        }

        if (node?.Gates == null || node.Gates.Length == 0)
        {
            cachedAllGatesCleared = true;
            allGatesClearedDirty = false;
            return true;
        }

        // foreach で判定（LINQ.All を置き換え）
        foreach (var gate in node.Gates)
        {
            if (gate == null) continue;

            // Repeatable gates don't block exit (they respawn after cooldown)
            if (gate.Repeatable) continue;

            if (!gateStates.TryGetValue(gate.GateId, out var state))
            {
                cachedAllGatesCleared = false;
                allGatesClearedDirty = false;
                return false;
            }
            if (!state.IsCleared)
            {
                cachedAllGatesCleared = false;
                allGatesClearedDirty = false;
                return false;
            }
        }

        cachedAllGatesCleared = true;
        allGatesClearedDirty = false;
        return true;
    }

    public void MarkCleared(GateMarker gate)
    {
        if (gate == null) return;
        if (!gateStates.TryGetValue(gate.GateId, out var state)) return;

        allGatesClearedDirty = true; // キャッシュ無効化

        if (gate.Repeatable)
        {
            state.CooldownRemaining = gate.CooldownSteps;
        }
        else
        {
            state.IsCleared = true;
        }
    }

    public void MarkFailed(GateMarker gate)
    {
        if (gate == null) return;
        if (!gateStates.TryGetValue(gate.GateId, out var state)) return;

        state.FailCount++;
        state.CooldownRemaining = gate.CooldownSteps;
    }

    public void TickCooldowns()
    {
        foreach (var state in gateStates.Values)
        {
            if (state.CooldownRemaining > 0)
                state.CooldownRemaining--;
        }
    }

    public GateRuntimeState GetState(string gateId)
    {
        return gateStates.TryGetValue(gateId, out var state) ? state : null;
    }

    /// <summary>
    /// 全ゲートの状態を取得（Progress UI用）
    /// ソート済みリストを使用してアロケーションを削減
    /// </summary>
    public int GetAllStates(NodeSO node, List<(GateMarker marker, GateRuntimeState state)> resultBuffer)
    {
        resultBuffer.Clear();

        if (node?.Gates == null) return 0;

        // ソート済みキャッシュがあればそれを使用
        if (sortedGatesCache.Count > 0)
        {
            for (int i = 0; i < sortedGatesCache.Count; i++)
            {
                var gate = sortedGatesCache[i];
                if (gate == null) continue;
                if (!gateStates.TryGetValue(gate.GateId, out var state)) continue;
                resultBuffer.Add((gate, state));
            }
        }
        else
        {
            // フォールバック: ノードから直接取得（テスト用など）
            foreach (var gate in node.Gates)
            {
                if (gate == null) continue;
                if (!gateStates.TryGetValue(gate.GateId, out var state)) continue;
                resultBuffer.Add((gate, state));
            }
            // Order でソート
            resultBuffer.Sort((a, b) => a.marker.Order.CompareTo(b.marker.Order));
        }

        return resultBuffer.Count;
    }

    /// <summary>
    /// 残りゲート数をカウント（アロケーションなし）
    /// </summary>
    public int CountRemainingGates()
    {
        int count = 0;
        foreach (var state in gateStates.Values)
        {
            if (!state.IsCleared)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Get the node ID that the current gate states belong to.
    /// Used to verify snapshot validity before restoration.
    /// </summary>
    public string GetCachedNodeId()
    {
        return cachedNode?.NodeId;
    }

    /// <summary>
    /// Get the node ID that a restored snapshot belongs to.
    /// Used for cross-node anchor rewinds where the snapshot is for a different node.
    /// </summary>
    public string GetRestoredFromNodeId()
    {
        return restoredFromNodeId;
    }
}
