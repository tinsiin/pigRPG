using System;
using System.Collections.Generic;

[Serializable]
public sealed class SideObjectState
{
    public List<string> VarietyHistory = new();
    public List<SideObjectCooldownEntry> Cooldowns = new();
    public string PendingLeftId;
    public string PendingRightId;

    public SideObjectState() { }

    public SideObjectState(
        VarietyHistory history,
        SideObjectCooldownTracker tracker,
        string pendingLeftId,
        string pendingRightId)
    {
        VarietyHistory = history?.ToList() ?? new List<string>();
        Cooldowns = tracker?.Export() ?? new List<SideObjectCooldownEntry>();
        PendingLeftId = pendingLeftId;
        PendingRightId = pendingRightId;
    }
}
