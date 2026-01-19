using System;
using System.Collections.Generic;

public enum RollType
{
    SideObject,
    Encounter,
    Exit
}

[Serializable]
public sealed class RollLogEntry
{
    public RollType Type;
    public int Step;
    public string NodeId;
    public List<RollCandidate> Candidates = new();
    public string SelectedId;
    public float Roll;
    public string ExcludeReason;
    public DateTime Timestamp;

    public RollLogEntry() { }

    public RollLogEntry(RollType type, int step, string nodeId)
    {
        Type = type;
        Step = step;
        NodeId = nodeId;
        Timestamp = DateTime.Now;
    }
}

[Serializable]
public sealed class RollCandidate
{
    public string Id;
    public float Weight;
    public float EffectiveWeight;
    public bool Excluded;
    public string ExcludeReason;

    public RollCandidate() { }

    public RollCandidate(string id, float weight, float effectiveWeight)
    {
        Id = id;
        Weight = weight;
        EffectiveWeight = effectiveWeight;
    }
}
