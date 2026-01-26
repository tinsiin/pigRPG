using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Event Definition")]
public sealed class EventDefinitionSO : ScriptableObject
{
    [Header("Steps")]
    [Tooltip("イベントステップ（MessageStep, NovelDialogueStep, BattleStep等）")]
    [SerializeReference]
    private IEventStep[] steps;

    [Header("Terminal Effects")]
    [Tooltip("全ステップ完了後に適用されるEffect")]
    [SerializeField] private EffectSO[] terminalEffects;

    // ※以下のズーム設定は非推奨。ズーム責務はNovelDialogueStepに移行。
    // AreaController.RunCentralEvent()からズーム処理を削除したため、これらは参照されなくなった。
    [Header("Zoom (Obsolete)")]
    [Tooltip("非推奨: NovelDialogueStepのズーム設定を使用してください")]
    [SerializeField] private bool zoomOnApproach = true;
    [Tooltip("非推奨: NovelDialogueStepのズーム設定を使用してください")]
    [SerializeField] private FocusArea focusArea;

    /// <summary>
    /// イベントステップ配列。
    /// </summary>
    public IEventStep[] Steps => steps;

    /// <summary>
    /// 全ステップ完了後に適用されるEffect。
    /// </summary>
    public EffectSO[] TerminalEffects => terminalEffects;

    /// <summary>
    /// 非推奨: ズーム責務はNovelDialogueStepに移行。
    /// </summary>
    [Obsolete("Use NovelDialogueStep zoom settings instead. This property is no longer referenced.")]
    public bool ZoomOnApproach => zoomOnApproach;

    /// <summary>
    /// 非推奨: ズーム責務はNovelDialogueStepに移行。
    /// </summary>
    [Obsolete("Use NovelDialogueStep zoom settings instead. This property is no longer referenced.")]
    public FocusArea FocusArea => focusArea;
}

/// <summary>
/// イベント選択肢。MessageStepで使用。
/// </summary>
[Serializable]
public sealed class EventChoice
{
    [SerializeField] private string label;
    [SerializeField] private EffectSO[] effects;

    public string Label => label;
    public EffectSO[] Effects => effects;

    public EventChoice() { }

    public EventChoice(string label, EffectSO[] effects)
    {
        this.label = label;
        this.effects = effects;
    }
}