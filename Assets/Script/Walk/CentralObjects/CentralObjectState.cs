using System;
using System.Collections.Generic;

[Serializable]
public sealed class CentralObjectState
{
    public List<string> VarietyHistory = new();
    public List<CentralObjectCooldownEntry> Cooldowns = new();

    public CentralObjectState() { }

    public CentralObjectState(
        VarietyHistory history,
        CentralObjectCooldownTracker tracker)
    {
        VarietyHistory = history?.ToList() ?? new List<string>();
        Cooldowns = tracker?.Export() ?? new List<CentralObjectCooldownEntry>();
    }
}
