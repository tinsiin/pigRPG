/// <summary>
/// イベントキューの発火判定・実行を行う。
/// </summary>
public sealed class EventQueueResolver
{
    /// <summary>
    /// 配列から発火可能なイベントを1つ取得する。
    /// 順番に評価し、最初に条件を満たすものを返す。
    /// </summary>
    /// <param name="entries">イベントエントリ配列</param>
    /// <param name="hostKey">ホストキー（例: "central:npc_merchant"）</param>
    /// <param name="context">ゲームコンテキスト</param>
    /// <param name="stateManager">状態マネージャー</param>
    /// <returns>発火するイベント定義。なければnull</returns>
    public EventDefinitionSO ResolveNext(
        EventQueueEntry[] entries,
        string hostKey,
        GameContext context,
        EventEntryStateManager stateManager)
    {
        if (entries == null || entries.Length == 0) return null;

        foreach (var entry in entries)
        {
            if (CanTrigger(entry, hostKey, context, stateManager))
            {
                // 発火を記録
                stateManager.RecordTrigger(hostKey, entry.EntryId, entry.ConsumeOnTrigger);
                return entry.EventDefinition;
            }
        }
        return null;
    }

    /// <summary>
    /// エントリが発火可能かどうか判定する。
    /// </summary>
    public bool CanTrigger(
        EventQueueEntry entry,
        string hostKey,
        GameContext context,
        EventEntryStateManager stateManager)
    {
        if (entry == null || !entry.HasEventDefinition) return false;

        var state = stateManager.GetState(hostKey, entry.EntryId);

        // 消費済みチェック
        if (entry.ConsumeOnTrigger && state.Consumed) return false;

        // 最大回数チェック
        if (entry.MaxTriggerCount > 0 && state.TriggerCount >= entry.MaxTriggerCount) return false;

        // クールダウンチェック（初回は常に許可）
        if (entry.CooldownSteps > 0 && state.TriggerCount > 0 && state.StepsSinceLastTrigger < entry.CooldownSteps) return false;

        // 条件チェック
        if (entry.HasConditions)
        {
            foreach (var cond in entry.Conditions)
            {
                if (cond != null && !cond.IsMet(context)) return false;
            }
        }

        return true;
    }
}
