using UnityEngine;

/// <summary>
/// 会話データSO。
/// フィールド会話/イベント会話の両方に使用可能。
/// 「戻れるか」はNovelDialogueStep.allowBacktrackで制御。
/// </summary>
[CreateAssetMenu(menuName = "Walk/Dialogue")]
public sealed class DialogueSO : ScriptableObject
{
    [SerializeField] private string dialogueId;
    [SerializeField] private DisplayMode defaultMode = DisplayMode.Dinoid;
    [SerializeField] private DialogueStep[] steps;

    [Header("Zoom")]
    [Tooltip("中央オブジェクトアプローチ時にズームするか（デフォルト: ON）")]
    [SerializeField] private bool zoomOnApproach = true;
    [Tooltip("ズーム時のフォーカス領域")]
    [SerializeField] private FocusArea focusArea;

    public string DialogueId => dialogueId;
    public DisplayMode DefaultMode => defaultMode;
    public DialogueStep[] Steps => steps;

    /// <summary>
    /// 中央オブジェクトアプローチ時にズームするか。
    /// </summary>
    public bool ZoomOnApproach => zoomOnApproach;

    /// <summary>
    /// ズーム時のフォーカス領域。
    /// </summary>
    public FocusArea FocusArea => focusArea;

    public bool HasSteps => steps != null && steps.Length > 0;
    public int StepCount => steps?.Length ?? 0;
}
