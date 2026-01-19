using System.Collections.Generic;

public enum AnchorScope
{
    Node,
    Region,
    Graph
}

public enum RewindMode
{
    PositionOnly,
    PositionAndState
}

public sealed class WalkAnchor
{
    public string AnchorId { get; }
    public string NodeId { get; }
    public uint NodeSeed { get; }
    public WalkCountersSnapshot CountersSnapshot { get; }
    public Dictionary<string, bool> FlagsSnapshot { get; }
    public Dictionary<string, int> CountersSnapshotMap { get; }
    public Dictionary<string, GateRuntimeState> GateStatesSnapshot { get; }
    public SideObjectState SideObjectStateSnapshot { get; }
    public AnchorScope Scope { get; }

    public WalkAnchor(
        string anchorId,
        string nodeId,
        uint nodeSeed,
        WalkCountersSnapshot counters,
        Dictionary<string, bool> flags,
        Dictionary<string, int> counterMap,
        Dictionary<string, GateRuntimeState> gateStates,
        SideObjectState sideObjectState,
        AnchorScope scope)
    {
        AnchorId = anchorId;
        NodeId = nodeId;
        NodeSeed = nodeSeed;
        CountersSnapshot = counters;
        FlagsSnapshot = new Dictionary<string, bool>(flags ?? new Dictionary<string, bool>());
        CountersSnapshotMap = new Dictionary<string, int>(counterMap ?? new Dictionary<string, int>());
        GateStatesSnapshot = new Dictionary<string, GateRuntimeState>();
        if (gateStates != null)
        {
            foreach (var kvp in gateStates)
            {
                GateStatesSnapshot[kvp.Key] = kvp.Value.Clone();
            }
        }
        SideObjectStateSnapshot = sideObjectState;
        Scope = scope;
    }
}
