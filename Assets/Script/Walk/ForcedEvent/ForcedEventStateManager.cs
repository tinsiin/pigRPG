using System;
using System.Collections.Generic;

/// <summary>
/// 強制イベントの状態を管理する。
/// 発火済み、クールダウン、トリガー回数を追跡。
/// </summary>
public sealed class ForcedEventStateManager
{
    private readonly Dictionary<string, ForcedEventState> states = new();

    /// <summary>
    /// 指定のトリガーが発火可能かどうか判定する。
    /// </summary>
    public bool CanTrigger(ForcedEventTrigger trigger)
    {
        if (trigger == null || string.IsNullOrEmpty(trigger.TriggerId))
        {
            return false;
        }

        if (!states.TryGetValue(trigger.TriggerId, out var state))
        {
            return true; // 初回は発火可能
        }

        // 消費済みチェック
        if (trigger.ConsumeOnTrigger && state.Consumed)
        {
            return false;
        }

        // 最大回数チェック
        if (trigger.MaxTriggerCount > 0 && state.TriggerCount >= trigger.MaxTriggerCount)
        {
            return false;
        }

        // クールダウンチェック
        if (trigger.CooldownSteps > 0 && state.StepsSinceLastTrigger < trigger.CooldownSteps)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// トリガー発火を記録する。
    /// </summary>
    public void RecordTrigger(string triggerId, bool consume)
    {
        if (string.IsNullOrEmpty(triggerId)) return;

        if (!states.TryGetValue(triggerId, out var state))
        {
            state = new ForcedEventState(triggerId);
            states[triggerId] = state;
        }

        state.TriggerCount++;
        state.StepsSinceLastTrigger = 0;
        if (consume) state.Consumed = true;
    }

    /// <summary>
    /// 歩数を進める（全トリガーのクールダウン用）。
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
    public List<ForcedEventState> Export()
    {
        var list = new List<ForcedEventState>();
        foreach (var kvp in states)
        {
            list.Add(kvp.Value);
        }
        return list;
    }

    /// <summary>
    /// 状態をインポートする（ロード用）。
    /// </summary>
    public void Import(List<ForcedEventState> dataList)
    {
        states.Clear();
        if (dataList == null) return;

        foreach (var state in dataList)
        {
            if (state == null || string.IsNullOrEmpty(state.TriggerId)) continue;
            states[state.TriggerId] = state;
        }
    }
}
