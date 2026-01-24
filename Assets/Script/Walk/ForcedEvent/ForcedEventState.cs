using System;

/// <summary>
/// 強制イベントの発火状態。
/// セーブ/ロード対応。
/// </summary>
[Serializable]
public sealed class ForcedEventState
{
    public string TriggerId;
    public bool Consumed;
    public int TriggerCount;
    public int StepsSinceLastTrigger;

    public ForcedEventState() { }

    public ForcedEventState(string triggerId)
    {
        TriggerId = triggerId;
        Consumed = false;
        TriggerCount = 0;
        StepsSinceLastTrigger = 0;
    }
}
