using UnityEngine;

/// <summary>
/// イベント会話用ScriptableObject。
/// 戻る機能やバックログをサポート。
/// </summary>
[CreateAssetMenu(menuName = "Walk/Event Dialogue")]
public sealed class EventDialogueSO : ScriptableObject
{
    [SerializeField] private string dialogueId;
    [SerializeField] private DialogueStep[] steps;
    [SerializeField] private bool allowBacktrack = true;
    [SerializeField] private bool showBacklog = true;
    [SerializeField] private DisplayMode initialDisplayMode = DisplayMode.Portrait;

    public string DialogueId => dialogueId;
    public DialogueStep[] Steps => steps;
    public bool AllowBacktrack => allowBacktrack;
    public bool ShowBacklog => showBacklog;
    public DisplayMode InitialDisplayMode => initialDisplayMode;
    public int StepCount => steps?.Length ?? 0;
}
