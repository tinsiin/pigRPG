using System;
using UnityEngine;

/// <summary>
/// 強制イベントのトリガー定義。
/// NodeSOに配列で持たせる。
/// </summary>
[Serializable]
public sealed class ForcedEventTrigger
{
    [Header("識別")]
    [SerializeField] private string triggerId;

    [Header("発火条件")]
    [SerializeField] private ForcedEventType type;
    [SerializeField] private int stepCount;
    [SerializeField] [Range(0f, 1f)] private float probability;
    [SerializeField] private ConditionSO[] conditions;

    [Header("発火制御")]
    [SerializeField] private bool consumeOnTrigger;
    [SerializeField] private int cooldownSteps;
    [SerializeField] private int maxTriggerCount;

    [Header("内容")]
    [SerializeField] private FieldDialogueSO dialogue;

    public string TriggerId => triggerId;
    public ForcedEventType Type => type;
    public int StepCount => stepCount;
    public float Probability => probability;
    public ConditionSO[] Conditions => conditions;
    public bool ConsumeOnTrigger => consumeOnTrigger;
    public int CooldownSteps => cooldownSteps;
    public int MaxTriggerCount => maxTriggerCount;
    public FieldDialogueSO Dialogue => dialogue;

    public bool HasConditions => conditions != null && conditions.Length > 0;
}
