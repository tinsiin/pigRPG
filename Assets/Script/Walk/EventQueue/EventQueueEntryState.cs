using System;

/// <summary>
/// イベントキューエントリの発火状態。
/// セーブ/ロード対応。
/// </summary>
[Serializable]
public sealed class EventQueueEntryState
{
    /// <summary>
    /// 状態キー（hostKey:entryId形式）
    /// </summary>
    public string EntryId;

    /// <summary>
    /// 消費済みかどうか
    /// </summary>
    public bool Consumed;

    /// <summary>
    /// 発火回数
    /// </summary>
    public int TriggerCount;

    /// <summary>
    /// 最後の発火からの歩数
    /// </summary>
    public int StepsSinceLastTrigger;

    public EventQueueEntryState() { }

    public EventQueueEntryState(string entryId)
    {
        EntryId = entryId;
        Consumed = false;
        TriggerCount = 0;
        StepsSinceLastTrigger = 0;
    }
}
