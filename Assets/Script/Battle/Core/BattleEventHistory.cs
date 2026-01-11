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
        entries.Add(new BattleEventEntry(message, important));
    }

    public void Clear()
    {
        entries.Clear();
    }
}

public readonly struct BattleEventEntry
{
    public string Message { get; }
    public bool Important { get; }

    public BattleEventEntry(string message, bool important)
    {
        Message = message;
        Important = important;
    }
}
