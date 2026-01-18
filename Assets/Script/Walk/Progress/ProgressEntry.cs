/// <summary>
/// 進捗エントリ（ゲートまたは出口）
/// </summary>
public readonly struct ProgressEntry
{
    public enum EntryType { Gate, Exit }

    public EntryType Type { get; }
    public int Order { get; }
    public string Id { get; }
    public int Position { get; }
    public bool IsCleared { get; }
    public bool IsActive { get; }
    public bool IsCoolingDown { get; }
    public string DisplayLabel { get; }

    public ProgressEntry(
        EntryType type,
        int order,
        string id,
        int position,
        bool isCleared,
        bool isActive,
        bool isCoolingDown,
        string displayLabel)
    {
        Type = type;
        Order = order;
        Id = id;
        Position = position;
        IsCleared = isCleared;
        IsActive = isActive;
        IsCoolingDown = isCoolingDown;
        DisplayLabel = displayLabel;
    }

    public static ProgressEntry CreateGate(
        int order,
        string gateId,
        int position,
        bool isCleared,
        bool isActive,
        bool isCoolingDown)
    {
        return new ProgressEntry(
            EntryType.Gate,
            order,
            gateId,
            position,
            isCleared,
            isActive,
            isCoolingDown,
            order.ToString());
    }

    public static ProgressEntry CreateExit(int order, int position)
    {
        return new ProgressEntry(
            EntryType.Exit,
            order,
            "exit",
            position,
            isCleared: false,
            isActive: false,
            isCoolingDown: false,
            "Exit");
    }
}
