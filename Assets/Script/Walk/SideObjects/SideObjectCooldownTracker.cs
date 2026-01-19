using System;
using System.Collections.Generic;

[Serializable]
public sealed class SideObjectCooldownEntry
{
    public string SideObjectId;
    public int RemainingSteps;

    public SideObjectCooldownEntry() { }

    public SideObjectCooldownEntry(string id, int steps)
    {
        SideObjectId = id;
        RemainingSteps = steps;
    }
}

public sealed class SideObjectCooldownTracker
{
    private readonly Dictionary<string, int> cooldowns = new();

    public void StartCooldown(string sideObjectId, int steps)
    {
        if (string.IsNullOrEmpty(sideObjectId) || steps <= 0) return;
        cooldowns[sideObjectId] = steps;
    }

    public bool IsOnCooldown(string sideObjectId)
    {
        if (string.IsNullOrEmpty(sideObjectId)) return false;
        return cooldowns.TryGetValue(sideObjectId, out var remaining) && remaining > 0;
    }

    public void AdvanceStep()
    {
        var toRemove = new List<string>();
        foreach (var kvp in cooldowns)
        {
            var newValue = kvp.Value - 1;
            if (newValue <= 0)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var key in toRemove)
        {
            cooldowns.Remove(key);
        }

        // Decrement remaining values
        var keys = new List<string>(cooldowns.Keys);
        foreach (var key in keys)
        {
            cooldowns[key]--;
        }
    }

    public void Clear()
    {
        cooldowns.Clear();
    }

    public List<SideObjectCooldownEntry> Export()
    {
        var list = new List<SideObjectCooldownEntry>();
        foreach (var kvp in cooldowns)
        {
            list.Add(new SideObjectCooldownEntry(kvp.Key, kvp.Value));
        }
        return list;
    }

    public void Import(List<SideObjectCooldownEntry> entries)
    {
        cooldowns.Clear();
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.SideObjectId)) continue;
            if (entry.RemainingSteps > 0)
            {
                cooldowns[entry.SideObjectId] = entry.RemainingSteps;
            }
        }
    }
}
