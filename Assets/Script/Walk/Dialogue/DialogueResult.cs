/// <summary>
/// DialogueRunner実行結果。
/// BattleResultと同じパターン。
/// </summary>
public struct DialogueResult
{
    public bool Completed { get; set; }
    public int SelectedChoiceIndex { get; set; }
    public string ChangedSpiritProperty { get; set; }

    /// <summary>
    /// リアクション終了時に発火したリアクション。
    /// nullでなければリアクション終了。
    /// </summary>
    public ReactionSegment TriggeredReaction { get; set; }

    /// <summary>
    /// リアクションによる終了か。
    /// </summary>
    public bool IsReactionEnded => TriggeredReaction != null;

    /// <summary>
    /// 通常終了か（全ステップ完了）。
    /// </summary>
    public bool IsNormalEnded => Completed && TriggeredReaction == null;

    public static DialogueResult CompletedResult => new DialogueResult { Completed = true, SelectedChoiceIndex = -1 };
    public static DialogueResult FailedResult => new DialogueResult { Completed = false, SelectedChoiceIndex = -1 };

    /// <summary>
    /// リアクション終了結果を生成。
    /// </summary>
    public static DialogueResult ReactionEndedResult(ReactionSegment reaction) => new DialogueResult
    {
        Completed = false,
        SelectedChoiceIndex = -1,
        TriggeredReaction = reaction
    };
}
