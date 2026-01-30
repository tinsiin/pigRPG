using System;
using System.Collections.Generic;

[Serializable]
public sealed class CentralObjectCooldownEntry
{
    public string CentralObjectId;
    public int RemainingSteps;

    public CentralObjectCooldownEntry() { }

    public CentralObjectCooldownEntry(string id, int steps)
    {
        CentralObjectId = id;
        RemainingSteps = steps;
    }
}

public sealed class CentralObjectCooldownTracker
{
    private readonly Dictionary<string, int> cooldowns = new();

    public void StartCooldown(string centralObjectId, int steps)
    {
        if (string.IsNullOrEmpty(centralObjectId) || steps <= 0) return;
        cooldowns[centralObjectId] = steps;
    }

    public bool IsOnCooldown(string centralObjectId)
    {
        if (string.IsNullOrEmpty(centralObjectId)) return false;
        return cooldowns.TryGetValue(centralObjectId, out var remaining) && remaining > 0;
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

    public List<CentralObjectCooldownEntry> Export()
    {
        var list = new List<CentralObjectCooldownEntry>();
        foreach (var kvp in cooldowns)
        {
            list.Add(new CentralObjectCooldownEntry(kvp.Key, kvp.Value));
        }
        return list;
    }

    public void Import(List<CentralObjectCooldownEntry> entries)
    {
        cooldowns.Clear();
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.CentralObjectId)) continue;
            if (entry.RemainingSteps > 0)
            {
                cooldowns[entry.CentralObjectId] = entry.RemainingSteps;
            }
        }
    }
}
