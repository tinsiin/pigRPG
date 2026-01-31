using System.Collections.Generic;

/// <summary>
/// イベントエントリの状態を統合管理する。
/// ForcedEventとEventQueueの両方の状態を1つのマネージャーで管理。
///
/// キー形式:
/// - ForcedEvent: "forced:{nodeId}:{triggerId}"
/// - EventQueue: "{soType}:{soId}:{entryId}"
/// </summary>
public sealed class EventEntryStateManager
{
    private readonly Dictionary<string, EventQueueEntryState> states = new();

    /// <summary>
    /// 指定のキーの状態を取得する。
    /// 存在しなければ新規作成して返す。
    /// </summary>
    public EventQueueEntryState GetState(string fullKey)
    {
        if (string.IsNullOrEmpty(fullKey)) return new EventQueueEntryState();

        if (!states.TryGetValue(fullKey, out var state))
        {
            state = new EventQueueEntryState(fullKey);
            states[fullKey] = state;
        }
        return state;
    }

    /// <summary>
    /// EventQueue用: hostKeyとentryIdから状態を取得。
    /// </summary>
    public EventQueueEntryState GetState(string hostKey, string entryId)
    {
        var fullKey = $"{hostKey}:{entryId}";
        return GetState(fullKey);
    }

    /// <summary>
    /// エントリ発火を記録する。
    /// </summary>
    public void RecordTrigger(string fullKey, bool consume)
    {
        if (string.IsNullOrEmpty(fullKey)) return;

        var state = GetState(fullKey);
        state.TriggerCount++;
        state.StepsSinceLastTrigger = 0;
        if (consume) state.Consumed = true;
    }

    /// <summary>
    /// EventQueue用: hostKeyとentryIdで発火を記録。
    /// </summary>
    public void RecordTrigger(string hostKey, string entryId, bool consume)
    {
        var fullKey = $"{hostKey}:{entryId}";
        RecordTrigger(fullKey, consume);
    }

    /// <summary>
    /// ForcedEvent用: 発火可能かどうか判定する。
    /// </summary>
    public bool CanTrigger(ForcedEventTrigger trigger, string nodeId)
    {
        if (trigger == null || string.IsNullOrEmpty(trigger.TriggerId)) return false;

        var fullKey = MakeForcedEventKey(nodeId, trigger.TriggerId);
        var state = GetState(fullKey);

        // 消費済みチェック
        if (trigger.ConsumeOnTrigger && state.Consumed) return false;

        // 最大回数チェック
        if (trigger.MaxTriggerCount > 0 && state.TriggerCount >= trigger.MaxTriggerCount) return false;

        // クールダウンチェック（初回は常に許可）
        if (trigger.CooldownSteps > 0 && state.TriggerCount > 0 && state.StepsSinceLastTrigger < trigger.CooldownSteps) return false;

        return true;
    }

    /// <summary>
    /// ForcedEvent用: 発火を記録。
    /// </summary>
    public void RecordForcedTrigger(string nodeId, string triggerId, bool consume)
    {
        var fullKey = MakeForcedEventKey(nodeId, triggerId);
        RecordTrigger(fullKey, consume);
    }

    /// <summary>
    /// 歩数を進める（全エントリのクールダウン用）。
    /// </summary>
    public void IncrementSteps()
    {
        foreach (var state in states.Values)
        {
            state.StepsSinceLastTrigger++;
        }
    }

    /// <summary>
    /// 状態をクリアする。
    /// </summary>
    public void Clear()
    {
        states.Clear();
    }

    /// <summary>
    /// 状態をエクスポートする（セーブ用）。
    /// </summary>
    public List<EventQueueEntryState> Export()
    {
        var list = new List<EventQueueEntryState>();
        foreach (var kvp in states)
        {
            list.Add(kvp.Value);
        }
        return list;
    }

    /// <summary>
    /// 状態をインポートする（ロード用）。
    /// </summary>
    public void Import(List<EventQueueEntryState> dataList)
    {
        states.Clear();
        if (dataList == null) return;

        foreach (var state in dataList)
        {
            if (state == null || string.IsNullOrEmpty(state.EntryId)) continue;
            states[state.EntryId] = state;
        }
    }

    /// <summary>
    /// ForcedEvent用のキーを生成。
    /// </summary>
    private static string MakeForcedEventKey(string nodeId, string triggerId)
        => $"forced:{nodeId}:{triggerId}";
}
