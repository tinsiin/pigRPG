using System.Collections.Generic;
using System.Linq;

public sealed class AnchorManager
{
    private readonly Dictionary<string, WalkAnchor> anchors = new();
    private readonly Stack<string> anchorHistory = new();

    public bool HasAnchor(string anchorId) => anchors.ContainsKey(anchorId);

    public void CreateAnchor(string anchorId, GameContext context, GateResolver gateResolver, AnchorScope scope)
    {
        var snapshot = new WalkCountersSnapshot(
            context.Counters.GlobalSteps,
            context.Counters.NodeSteps,
            context.Counters.TrackProgress);

        var anchor = new WalkAnchor(
            anchorId,
            context.WalkState.CurrentNodeId,
            context.WalkState.NodeSeed,
            snapshot,
            new Dictionary<string, bool>(context.GetAllFlags()),
            new Dictionary<string, int>(context.GetAllCounters()),
            gateResolver?.TakeSnapshot(),
            scope);

        anchors[anchorId] = anchor;
        anchorHistory.Push(anchorId);
    }

    public void RewindToAnchor(string anchorId, GameContext context, GateResolver gateResolver, RewindMode mode)
    {
        if (!anchors.TryGetValue(anchorId, out var anchor)) return;

        // スナップショットから全カウンターを復元
        context.Counters.SetGlobalSteps(anchor.CountersSnapshot.GlobalSteps);
        context.Counters.SetNodeSteps(anchor.CountersSnapshot.NodeSteps);
        context.Counters.SetTrackProgress(anchor.CountersSnapshot.TrackProgress);
        context.WalkState.SetCurrentNodeId(anchor.NodeId);
        context.WalkState.NodeSeed = anchor.NodeSeed;

        if (mode == RewindMode.PositionAndState)
        {
            context.RestoreFlags(anchor.FlagsSnapshot.Select(kvp => new FlagEntry { Key = kvp.Key, Value = kvp.Value }));
            context.RestoreCounters(anchor.CountersSnapshotMap.Select(kvp => new CounterEntry { Key = kvp.Key, Value = kvp.Value }));

            // Pass the anchor's node ID so cross-node rewinds can validate the snapshot
            gateResolver?.RestoreFromSnapshot(anchor.GateStatesSnapshot, anchor.NodeId);
        }
    }

    public void JumpToAnchor(string anchorId, GameContext context, GateResolver gateResolver)
    {
        RewindToAnchor(anchorId, context, gateResolver, RewindMode.PositionOnly);
    }

    public void ClearAnchorsInScope(AnchorScope scope)
    {
        var toRemove = anchors
            .Where(kvp => kvp.Value.Scope == scope)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            anchors.Remove(key);
        }
    }

    public List<WalkAnchorData> ExportAnchors()
    {
        var list = new List<WalkAnchorData>();
        foreach (var anchor in anchors.Values)
        {
            list.Add(WalkAnchorData.FromAnchor(anchor));
        }
        return list;
    }

    public void ImportAnchors(List<WalkAnchorData> dataList)
    {
        anchors.Clear();
        anchorHistory.Clear();
        if (dataList == null) return;

        foreach (var data in dataList)
        {
            var anchor = data.ToAnchor();
            anchors[anchor.AnchorId] = anchor;
            anchorHistory.Push(anchor.AnchorId);
        }
    }
}
