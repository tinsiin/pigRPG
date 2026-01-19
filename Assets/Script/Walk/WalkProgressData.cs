using System;
using System.Collections.Generic;

[Serializable]
public class FlagEntry
{
    public string Key;
    public bool Value;

    public FlagEntry() { }

    public FlagEntry(string key, bool value)
    {
        Key = key;
        Value = value;
    }
}

[Serializable]
public class CounterEntry
{
    public string Key;
    public int Value;

    public CounterEntry() { }

    public CounterEntry(string key, int value)
    {
        Key = key;
        Value = value;
    }
}

[Serializable]
public class WalkProgressData
{
    public int GlobalSteps;
    public int NodeSteps;
    public int TrackProgress;
    public string CurrentNodeId;
    public List<FlagEntry> Flags = new();
    public List<CounterEntry> Counters = new();

    // Phase 2: Gate/Anchor/Seed persistence
    public List<GateRuntimeStateData> GateStates = new();
    public List<WalkAnchorData> Anchors = new();
    public uint NodeSeed;
    public int VarietyHistoryIndex;

    // Phase 1.3: EncounterState persistence
    public List<EncounterStateData> EncounterStates = new();

    // Phase 1.1: SideObject state persistence
    public SideObjectState SideObjectState;

    // Phase 2.3: Persistent encounter overlays
    public List<EncounterOverlayData> EncounterOverlays = new();

    // Phase 3.1: Tags
    public List<string> Tags = new();

    public static WalkProgressData FromContext(GameContext context)
    {
        if (context == null) return null;

        var data = new WalkProgressData
        {
            GlobalSteps = context.Counters.GlobalSteps,
            NodeSteps = context.Counters.NodeSteps,
            TrackProgress = context.Counters.TrackProgress,
            CurrentNodeId = context.WalkState.CurrentNodeId,
            Flags = new List<FlagEntry>(),
            Counters = new List<CounterEntry>(),
            GateStates = new List<GateRuntimeStateData>(),
            Anchors = new List<WalkAnchorData>(),
            NodeSeed = context.WalkState.NodeSeed,
            VarietyHistoryIndex = context.WalkState.VarietyHistoryIndex,
            EncounterStates = context.ExportEncounterStates()
        };

        foreach (var kvp in context.GetAllFlags())
        {
            data.Flags.Add(new FlagEntry(kvp.Key, kvp.Value));
        }

        foreach (var kvp in context.GetAllCounters())
        {
            data.Counters.Add(new CounterEntry(kvp.Key, kvp.Value));
        }

        // Export gate states
        if (context.GateResolver != null)
        {
            var gateSnapshot = context.GateResolver.TakeSnapshot();
            foreach (var kvp in gateSnapshot)
            {
                data.GateStates.Add(new GateRuntimeStateData(kvp.Value));
            }
        }

        // Export anchors
        if (context.AnchorManager != null)
        {
            data.Anchors = context.AnchorManager.ExportAnchors();
        }

        // Export persistent encounter overlays
        data.EncounterOverlays = context.EncounterOverlays.ExportPersistent();

        // Export tags
        data.Tags = new List<string>(context.GetAllTags());

        // Export side object state
        if (context.SideObjectSelector != null)
        {
            data.SideObjectState = context.SideObjectSelector.ExportState();
        }

        return data;
    }

    public void ApplyToContext(GameContext context)
    {
        if (context == null) return;

        context.Counters.SetGlobalSteps(GlobalSteps);
        context.Counters.SetNodeSteps(NodeSteps);
        context.Counters.SetTrackProgress(TrackProgress);
        context.WalkState.CurrentNodeId = CurrentNodeId;
        context.WalkState.NodeSeed = NodeSeed;
        context.WalkState.VarietyHistoryIndex = VarietyHistoryIndex;

        context.RestoreFlags(Flags);
        context.RestoreCounters(Counters);

        // Restore gate states with node ID for proper snapshot validation
        // (SyncCurrentNodeFromWalkState uses this to decide whether to reapply after reinitialization)
        if (context.GateResolver != null && GateStates != null)
        {
            var gateSnapshot = new Dictionary<string, GateRuntimeState>();
            foreach (var stateData in GateStates)
            {
                gateSnapshot[stateData.GateId] = stateData.ToRuntimeState();
            }
            context.GateResolver.RestoreFromSnapshot(gateSnapshot, CurrentNodeId);
        }

        // Restore anchors
        if (context.AnchorManager != null && Anchors != null)
        {
            context.AnchorManager.ImportAnchors(Anchors);
        }

        // Restore encounter states
        context.ImportEncounterStates(EncounterStates);

        // Restore persistent encounter overlays
        context.EncounterOverlays.ImportPersistent(EncounterOverlays);

        // Restore tags
        context.RestoreTags(Tags);

        // Restore side object state
        if (context.SideObjectSelector != null && SideObjectState != null)
        {
            context.SideObjectSelector.ImportState(SideObjectState);
        }
    }
}
