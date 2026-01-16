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
    public string CurrentNodeId;
    public List<FlagEntry> Flags = new();
    public List<CounterEntry> Counters = new();

    public static WalkProgressData FromContext(GameContext context)
    {
        if (context == null) return null;

        var data = new WalkProgressData
        {
            GlobalSteps = context.Counters.GlobalSteps,
            CurrentNodeId = context.WalkState.CurrentNodeId,
            Flags = new List<FlagEntry>(),
            Counters = new List<CounterEntry>()
        };

        foreach (var kvp in context.GetAllFlags())
        {
            data.Flags.Add(new FlagEntry(kvp.Key, kvp.Value));
        }

        foreach (var kvp in context.GetAllCounters())
        {
            data.Counters.Add(new CounterEntry(kvp.Key, kvp.Value));
        }

        return data;
    }

    public void ApplyToContext(GameContext context)
    {
        if (context == null) return;

        context.Counters.SetGlobalSteps(GlobalSteps);
        context.WalkState.CurrentNodeId = CurrentNodeId;

        context.RestoreFlags(Flags);
        context.RestoreCounters(Counters);
    }
}
