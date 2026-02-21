using System.Collections.Generic;

public sealed class BattleEventHistory
{
    private readonly List<BattleEventEntry> entries = new();
    private int _displayedUpTo = 0;

    public IReadOnlyList<BattleEventEntry> Entries => entries;

    /// <summary>
    /// 前回呼び出し以降に追加されたエントリの開始インデックスと件数を返す
    /// </summary>
    public (int start, int count) AdvanceDisplayCursor()
    {
        int start = _displayedUpTo;
        int count = entries.Count - start;
        _displayedUpTo = entries.Count;
        return (start, count);
    }

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
        _displayedUpTo = 0;
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
