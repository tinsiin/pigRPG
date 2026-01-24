/// <summary>
/// DialogueRunner実行時のコンテキスト。
/// EncounterContextと同じパターン。
/// </summary>
public sealed class DialogueContext
{
    public GameContext GameContext { get; set; }
    public FieldDialogueSO Dialogue { get; set; }
    public EventDialogueSO EventDialogue { get; set; }
    public DisplayMode InitialMode { get; set; }
    public bool AllowSkip { get; set; } = true;
    public bool AllowBacktrack { get; set; } = false;
    public bool ShowBacklog { get; set; } = false;

    /// <summary>
    /// 使用するステップ配列を取得。
    /// EventDialogueSOがあればそちらを優先。
    /// </summary>
    public DialogueStep[] GetSteps()
    {
        if (EventDialogue != null) return EventDialogue.Steps;
        if (Dialogue != null) return Dialogue.Steps;
        return null;
    }

    public bool HasSteps => GetSteps()?.Length > 0;

    public DialogueContext() { }

    public DialogueContext(FieldDialogueSO dialogue, GameContext gameContext)
    {
        Dialogue = dialogue;
        GameContext = gameContext;
        InitialMode = DisplayMode.Dinoid;
    }

    public DialogueContext(FieldDialogueSO dialogue, GameContext gameContext, DisplayMode initialMode, bool allowSkip = true)
    {
        Dialogue = dialogue;
        GameContext = gameContext;
        InitialMode = initialMode;
        AllowSkip = allowSkip;
    }

    /// <summary>
    /// EventDialogueSO用のコンストラクタ。
    /// 戻る機能とバックログをサポート。
    /// </summary>
    public DialogueContext(EventDialogueSO eventDialogue, GameContext gameContext)
    {
        EventDialogue = eventDialogue;
        GameContext = gameContext;
        InitialMode = eventDialogue?.InitialDisplayMode ?? DisplayMode.Portrait;
        AllowBacktrack = eventDialogue?.AllowBacktrack ?? false;
        ShowBacklog = eventDialogue?.ShowBacklog ?? false;
    }
}
