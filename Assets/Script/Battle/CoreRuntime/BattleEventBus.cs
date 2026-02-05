using System.Collections.Generic;

public sealed class BattleEventBus
{
    private readonly List<IBattleEventSink> sinks = new();

    public void Register(IBattleEventSink sink)
    {
        if (sink == null) return;
        if (sinks.Contains(sink)) return;
        sinks.Add(sink);
    }

    public void Unregister(IBattleEventSink sink)
    {
        if (sink == null) return;
        sinks.Remove(sink);
    }

    public void Publish(BattleEvent battleEvent)
    {
        for (var i = 0; i < sinks.Count; i++)
        {
            try
            {
                sinks[i].OnBattleEvent(battleEvent);
            }
            catch
            {
            }
        }
    }
}
