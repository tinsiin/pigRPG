using System.Collections.Generic;

public static class PlayersContextRegistry
{
    private static readonly HashSet<IPlayersContextConsumer> consumers = new();
    private static PlayersContext context;

    public static PlayersContext Context => context;

    public static void SetContext(PlayersContext newContext)
    {
        context = newContext;
        if (context == null) return;
        foreach (var consumer in consumers)
        {
            consumer?.InjectPlayersContext(context);
        }
    }

    public static void ClearContext(PlayersContext expected)
    {
        if (!ReferenceEquals(context, expected)) return;
        context = null;
    }

    public static void Register(IPlayersContextConsumer consumer)
    {
        if (consumer == null) return;
        consumers.Add(consumer);
        if (context != null)
        {
            consumer.InjectPlayersContext(context);
        }
    }

    public static void Unregister(IPlayersContextConsumer consumer)
    {
        if (consumer == null) return;
        consumers.Remove(consumer);
    }
}