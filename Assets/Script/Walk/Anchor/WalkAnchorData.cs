using System;
using System.Collections.Generic;

[Serializable]
public struct StringBoolPair
{
    public string Key;
    public bool Value;

    public StringBoolPair(string key, bool value)
    {
        Key = key;
        Value = value;
    }
}

[Serializable]
public struct StringIntPair
{
    public string Key;
    public int Value;

    public StringIntPair(string key, int value)
    {
        Key = key;
        Value = value;
    }
}

[Serializable]
public sealed class GateRuntimeStateData
{
    public string GateId;
    public int ResolvedPosition;
    public bool IsCleared;
    public int FailCount;

    public GateRuntimeStateData() { }

    public GateRuntimeStateData(GateRuntimeState state)
    {
        GateId = state.GateId;
        ResolvedPosition = state.ResolvedPosition;
        IsCleared = state.IsCleared;
        FailCount = state.FailCount;
    }

    public GateRuntimeState ToRuntimeState()
    {
        return new GateRuntimeState
        {
            GateId = this.GateId,
            ResolvedPosition = this.ResolvedPosition,
            IsCleared = this.IsCleared,
            FailCount = this.FailCount
        };
    }
}

[Serializable]
public sealed class WalkAnchorData
{
    public string AnchorId;
    public string NodeId;
    public uint NodeSeed;
    public int GlobalSteps;
    public int NodeSteps;
    public int TrackProgress;
    public List<StringBoolPair> Flags = new();
    public List<StringIntPair> CounterValues = new();
    public List<GateRuntimeStateData> GateStates = new();
    public SideObjectState SideObjectState;
    public AnchorScope Scope;

    public static WalkAnchorData FromAnchor(WalkAnchor anchor)
    {
        var data = new WalkAnchorData
        {
            AnchorId = anchor.AnchorId,
            NodeId = anchor.NodeId,
            NodeSeed = anchor.NodeSeed,
            GlobalSteps = anchor.CountersSnapshot.GlobalSteps,
            NodeSteps = anchor.CountersSnapshot.NodeSteps,
            TrackProgress = anchor.CountersSnapshot.TrackProgress,
            Flags = new List<StringBoolPair>(),
            CounterValues = new List<StringIntPair>(),
            GateStates = new List<GateRuntimeStateData>(),
            SideObjectState = anchor.SideObjectStateSnapshot,
            Scope = anchor.Scope
        };

        foreach (var kvp in anchor.FlagsSnapshot)
            data.Flags.Add(new StringBoolPair(kvp.Key, kvp.Value));

        foreach (var kvp in anchor.CountersSnapshotMap)
            data.CounterValues.Add(new StringIntPair(kvp.Key, kvp.Value));

        foreach (var kvp in anchor.GateStatesSnapshot)
            data.GateStates.Add(new GateRuntimeStateData(kvp.Value));

        return data;
    }

    public WalkAnchor ToAnchor()
    {
        var flags = new Dictionary<string, bool>();
        foreach (var pair in Flags)
            flags[pair.Key] = pair.Value;

        var counters = new Dictionary<string, int>();
        foreach (var pair in CounterValues)
            counters[pair.Key] = pair.Value;

        var gateStates = new Dictionary<string, GateRuntimeState>();
        foreach (var stateData in GateStates)
            gateStates[stateData.GateId] = stateData.ToRuntimeState();

        return new WalkAnchor(
            AnchorId,
            NodeId,
            NodeSeed,
            new WalkCountersSnapshot(GlobalSteps, NodeSteps, TrackProgress),
            flags,
            counters,
            gateStates,
            SideObjectState,
            Scope);
    }
}
