using UnityEngine;

/// <summary>
/// DialogueRunner実行時のコンテキスト。
/// EncounterContextと同じパターン。
/// </summary>
public sealed class DialogueContext
{
    public GameContext GameContext { get; set; }
    public DialogueSO Dialogue { get; set; }
    public EventDialogueSO EventDialogue { get; set; }
    public DisplayMode InitialMode { get; set; }
    public bool AllowSkip { get; set; } = true;
    public bool AllowBacktrack { get; set; } = false;
    public bool ShowBacklog { get; set; } = false;

    /// <summary>
    /// 中央オブジェクトのRectTransform（ズーム用）。
    /// 設定されている場合、会話開始時にズームイン、終了時にズームアウトする。
    /// </summary>
    public RectTransform CentralObjectRT { get; set; }

    /// <summary>
    /// ズーム時のフォーカス領域。
    /// 中央オブジェクトのどの部分をターゲット領域にフィットさせるか。
    /// </summary>
    public FocusArea ZoomFocusArea { get; set; } = FocusArea.Default;

    /// <summary>
    /// 主人公（精神属性連携用）- AllyId版（互換性用）。
    /// 精神属性の表示・変化対象となるパーティーメンバー。
    /// </summary>
    [System.Obsolete("ProtagonistCharacterIdを使用してください")]
    public AllyId? Protagonist { get; set; }

    /// <summary>
    /// 主人公（精神属性連携用）- CharacterId版（推奨）。
    /// 精神属性の表示・変化対象となるパーティーメンバー。
    /// </summary>
    public CharacterId? ProtagonistCharacterId { get; set; }

    /// <summary>
    /// パーティー情報へのアクセス（精神属性取得用）。
    /// </summary>
    public IPlayersRoster Roster { get; set; }

    /// <summary>
    /// 主人公の現在の精神属性を取得。
    /// 主人公未設定またはRoster未設定の場合はnull。
    /// CharacterId版を優先し、なければAllyId版を使用。
    /// </summary>
    public SpiritualProperty? GetProtagonistImpression()
    {
        if (Roster == null) return null;

        // CharacterId版を優先
        if (ProtagonistCharacterId.HasValue && ProtagonistCharacterId.Value.IsValid)
        {
            var ally = Roster.GetAlly(ProtagonistCharacterId.Value);
            return ally?.MyImpression;
        }

        // フォールバック: AllyId版
#pragma warning disable CS0618 // Obsolete warning suppressed for compatibility
        if (Protagonist.HasValue)
        {
            var ally = Roster.GetAllyById(Protagonist.Value);
            return ally?.MyImpression;
        }
#pragma warning restore CS0618

        return null;
    }

    /// <summary>
    /// 主人公のAllyClassを取得。
    /// CharacterId版を優先し、なければAllyId版を使用。
    /// </summary>
    public AllyClass GetProtagonistAlly()
    {
        if (Roster == null) return null;

        // CharacterId版を優先
        if (ProtagonistCharacterId.HasValue && ProtagonistCharacterId.Value.IsValid)
        {
            return Roster.GetAlly(ProtagonistCharacterId.Value);
        }

        // フォールバック: AllyId版
#pragma warning disable CS0618 // Obsolete warning suppressed for compatibility
        if (Protagonist.HasValue)
        {
            return Roster.GetAllyById(Protagonist.Value) as AllyClass;
        }
#pragma warning restore CS0618

        return null;
    }

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

    public DialogueContext(DialogueSO dialogue, GameContext gameContext)
    {
        Dialogue = dialogue;
        GameContext = gameContext;
        InitialMode = DisplayMode.Dinoid;
    }

    public DialogueContext(DialogueSO dialogue, GameContext gameContext, DisplayMode initialMode, bool allowSkip = true)
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
