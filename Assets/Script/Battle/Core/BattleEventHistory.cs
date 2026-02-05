using System.Collections.Generic;

public sealed class BattleEventHistory
{
    private readonly List<BattleEventEntry> entries = new();

    public IReadOnlyList<BattleEventEntry> Entries => entries;

    public void Add(string message, bool important)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }
        entries.Add(new BattleEventEntry(BattleEventType.Log, message, important));
    }

    public void Add(BattleEvent battleEvent)
    {
        if (string.IsNullOrEmpty(battleEvent.Message))
        {
            return;
        }
        entries.Add(new BattleEventEntry(battleEvent));
    }

    public void Clear()
    {
        entries.Clear();
    }
}

public readonly struct BattleEventEntry
{
    public BattleEventType Type { get; }
    public string Message { get; }
    public bool Important { get; }
    public int TurnCount { get; }
    public string ActorName { get; }

    public BattleEventEntry(BattleEventType type, string message, bool important, int turnCount = 0, string actorName = null)
    {
        Type = type;
        Message = message;
        Important = important;
        TurnCount = turnCount;
        ActorName = actorName ?? "";
    }

    public BattleEventEntry(BattleEvent battleEvent)
    {
        Type = battleEvent.Type;
        Message = battleEvent.Message;
        Important = battleEvent.Important;
        TurnCount = battleEvent.TurnCount;
        ActorName = battleEvent.ActorName ?? "";
    }
}
