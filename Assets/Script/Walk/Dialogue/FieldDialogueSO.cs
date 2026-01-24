using UnityEngine;

/// <summary>
/// フィールド会話用SO。
/// サイドオブジェクト、中央オブジェクト、強制イベントで使用。
/// </summary>
[CreateAssetMenu(menuName = "Walk/Field Dialogue")]
public sealed class FieldDialogueSO : ScriptableObject
{
    [SerializeField] private string dialogueId;
    [SerializeField] private DisplayMode defaultMode = DisplayMode.Dinoid;
    [SerializeField] private DialogueStep[] steps;

    public string DialogueId => dialogueId;
    public DisplayMode DefaultMode => defaultMode;
    public DialogueStep[] Steps => steps;

    public bool HasSteps => steps != null && steps.Length > 0;
    public int StepCount => steps?.Length ?? 0;
}
