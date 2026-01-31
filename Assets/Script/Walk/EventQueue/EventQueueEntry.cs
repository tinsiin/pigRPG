using System;
using UnityEngine;

/// <summary>
/// イベントキューの1エントリ。
/// 発火条件、消費設定、クールダウン等を持つ。
/// </summary>
[Serializable]
public sealed class EventQueueEntry
{
    [Header("識別")]
    [SerializeField] private string entryId;

    [Header("イベント内容")]
    [SerializeField] private EventDefinitionSO eventDefinition;

    [Header("発火条件")]
    [SerializeField] private ConditionSO[] conditions;

    [Header("発火制御")]
    [Tooltip("true = 1回限り（消費される）")]
    [SerializeField] private bool consumeOnTrigger = true;

    [Tooltip("再発火までのクールダウン歩数（0 = 即再発火可）")]
    [SerializeField] private int cooldownSteps;

    [Tooltip("最大発火回数（0 = 無制限）")]
    [SerializeField] private int maxTriggerCount;

    public string EntryId => entryId;
    public EventDefinitionSO EventDefinition => eventDefinition;
    public ConditionSO[] Conditions => conditions;
    public bool ConsumeOnTrigger => consumeOnTrigger;
    public int CooldownSteps => cooldownSteps;
    public int MaxTriggerCount => maxTriggerCount;

    public bool HasConditions => conditions != null && conditions.Length > 0;
    public bool HasEventDefinition => eventDefinition != null;
}
