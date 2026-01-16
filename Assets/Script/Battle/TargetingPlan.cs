using System;

/// <summary>
/// ターゲット対象のスコープ
/// </summary>
public enum TargetScope
{
    /// <summary>敵のみ</summary>
    Enemy,
    /// <summary>味方のみ</summary>
    Ally,
    /// <summary>敵+味方両方</summary>
    Both,
    /// <summary>自分のみ</summary>
    Self
}

/// <summary>
/// ターゲット選択モード
/// </summary>
public enum SelectionMode
{
    /// <summary>完全単体選択（個々指定、前のめり/後衛の区別なし）</summary>
    PerfectSingle,
    /// <summary>前のめり/後衛選択（単体）</summary>
    VanguardBacklineSingle,
    /// <summary>前のめり/後衛選択（複数）</summary>
    VanguardBacklineMulti,
    /// <summary>ランダム単体</summary>
    RandomSingle,
    /// <summary>ランダム複数</summary>
    RandomMulti,
    /// <summary>全体</summary>
    All,
    /// <summary>状況依存（ControlByThisSituation）</summary>
    ControlBySituation
}

/// <summary>
/// 範囲性質の解釈結果（不変オブジェクト）
/// UI遷移、AI判断、TargetingServiceが共通利用する
/// </summary>
public readonly struct TargetingPlan
{
    /// <summary>対象のスコープ（敵/味方/両方/自分）</summary>
    public readonly TargetScope Scope;

    /// <summary>選択モード（完全単体/前のめり後衛/ランダム/全体）</summary>
    public readonly SelectionMode Mode;

    /// <summary>死者選択可能か</summary>
    public readonly bool CanSelectDeath;

    /// <summary>自分自身を選択可能か</summary>
    public readonly bool CanSelectMyself;

    /// <summary>範囲選択UIを表示するか</summary>
    public readonly bool ShowRangeSelection;

    /// <summary>対象選択UIを表示するか</summary>
    public readonly bool ShowTargetSelection;

    /// <summary>元のSkillZoneTrait（デバッグ用）</summary>
    public readonly SkillZoneTrait OriginalTrait;

    private TargetingPlan(
        TargetScope scope,
        SelectionMode mode,
        bool canSelectDeath,
        bool canSelectMyself,
        bool showRangeSelection,
        bool showTargetSelection,
        SkillZoneTrait originalTrait)
    {
        Scope = scope;
        Mode = mode;
        CanSelectDeath = canSelectDeath;
        CanSelectMyself = canSelectMyself;
        ShowRangeSelection = showRangeSelection;
        ShowTargetSelection = showTargetSelection;
        OriginalTrait = originalTrait;
    }

    /// <summary>
    /// SkillZoneTraitからTargetingPlanを生成する
    /// </summary>
    /// <param name="rangeWill">正規化済みのRangeWill</param>
    /// <returns>解釈結果</returns>
    public static TargetingPlan FromRangeWill(SkillZoneTrait rangeWill)
    {
        // スコープの判定
        var scope = DetermineScope(rangeWill);

        // 選択モードの判定
        var mode = DetermineMode(rangeWill);

        // オプションフラグ
        var canSelectDeath = rangeWill.HasAny(SkillZoneTrait.CanSelectDeath);
        var canSelectMyself = rangeWill.HasAny(SkillZoneTrait.CanSelectMyself);

        // UI表示判定
        var showRangeSelection = rangeWill.HasAny(SkillZoneTrait.CanSelectRange);
        var showTargetSelection = DetermineShowTargetSelection(scope, mode, rangeWill);

        return new TargetingPlan(
            scope,
            mode,
            canSelectDeath,
            canSelectMyself,
            showRangeSelection,
            showTargetSelection,
            rangeWill);
    }

    /// <summary>
    /// スキルから直接TargetingPlanを生成する（正規化込み）
    /// </summary>
    public static TargetingPlan FromSkill(BaseSkill skill)
    {
        if (skill == null)
        {
            return new TargetingPlan(
                TargetScope.Enemy,
                SelectionMode.RandomSingle,
                false,
                false,
                false,
                false,
                0);
        }

        var normalized = SkillZoneTraitNormalizer.NormalizeForInitial(skill.ZoneTrait);
        return FromRangeWill(normalized);
    }

    private static TargetScope DetermineScope(SkillZoneTrait traits)
    {
        // SelfSkillは最優先
        if (traits.HasAny(SkillZoneTrait.SelfSkill))
        {
            return TargetScope.Self;
        }

        // 味方専用
        if (traits.HasAny(SkillZoneTrait.SelectOnlyAlly))
        {
            return TargetScope.Ally;
        }

        // 味方巻き込み可
        if (traits.HasAny(SkillZoneTrait.CanSelectAlly))
        {
            return TargetScope.Both;
        }

        // デフォルトは敵のみ
        return TargetScope.Enemy;
    }

    private static SelectionMode DetermineMode(SkillZoneTrait traits)
    {
        // SelfSkill → 自分のみなので選択不要
        if (traits.HasAny(SkillZoneTrait.SelfSkill))
        {
            return SelectionMode.PerfectSingle;
        }

        // 全体
        if (traits.HasAny(SkillZoneTrait.AllTarget))
        {
            return SelectionMode.All;
        }

        // ランダム複数
        if (traits.HasAny(SkillZoneTrait.RandomMultiTarget | SkillZoneTrait.RandomSelectMultiTarget))
        {
            return SelectionMode.RandomMulti;
        }

        // ランダム単体
        if (traits.HasAny(SkillZoneTrait.RandomSingleTarget))
        {
            return SelectionMode.RandomSingle;
        }

        // 状況依存
        if (traits.HasAny(SkillZoneTrait.ControlByThisSituation))
        {
            return SelectionMode.ControlBySituation;
        }

        // 完全単体選択（前のめり/後衛の区別なし）
        if (traits.HasAny(SkillZoneTrait.CanPerfectSelectSingleTarget))
        {
            return SelectionMode.PerfectSingle;
        }

        // 前のめり/後衛選択（複数）
        if (traits.HasAny(SkillZoneTrait.CanSelectMultiTarget))
        {
            return SelectionMode.VanguardBacklineMulti;
        }

        // 前のめり/後衛選択（単体）
        if (traits.HasAny(SkillZoneTrait.CanSelectSingleTarget))
        {
            return SelectionMode.VanguardBacklineSingle;
        }

        // デフォルト
        return SelectionMode.RandomSingle;
    }

    private static bool DetermineShowTargetSelection(TargetScope scope, SelectionMode mode, SkillZoneTrait traits)
    {
        // SelfSkillは選択不要
        if (scope == TargetScope.Self)
        {
            return false;
        }

        // 全体攻撃は選択不要
        if (mode == SelectionMode.All)
        {
            return false;
        }

        // ランダム系は選択不要
        if (mode == SelectionMode.RandomSingle || mode == SelectionMode.RandomMulti)
        {
            return false;
        }

        // ControlBySituationは自動決定（前のめりがいれば前のめり、いなければ事故）なので選択不要
        // AllyClass.DetermineNextUIState:512-516 と同じ動作
        if (mode == SelectionMode.ControlBySituation)
        {
            return false;
        }

        // 選択が必要なモード
        return mode == SelectionMode.PerfectSingle ||
               mode == SelectionMode.VanguardBacklineSingle ||
               mode == SelectionMode.VanguardBacklineMulti;
    }

    /// <summary>
    /// TabStateへの変換（AllyClass.DetermineNextUIStateの代替）
    /// </summary>
    public TabState ToTabState()
    {
        if (ShowRangeSelection)
        {
            return TabState.SelectRange;
        }

        if (ShowTargetSelection)
        {
            return TabState.SelectTarget;
        }

        return TabState.NextWait;
    }

    /// <summary>
    /// 対象選択が必要か（TargetingServiceで自動決定可能か）
    /// </summary>
    public bool RequiresManualTargetSelection => ShowTargetSelection;

    /// <summary>
    /// 敵を対象にできるか
    /// </summary>
    public bool CanTargetEnemy => Scope == TargetScope.Enemy || Scope == TargetScope.Both;

    /// <summary>
    /// 味方を対象にできるか
    /// </summary>
    public bool CanTargetAlly => Scope == TargetScope.Ally || Scope == TargetScope.Both;

    /// <summary>
    /// 単体選択か
    /// </summary>
    public bool IsSingleTarget =>
        Mode == SelectionMode.PerfectSingle ||
        Mode == SelectionMode.VanguardBacklineSingle ||
        Mode == SelectionMode.RandomSingle ||
        Mode == SelectionMode.ControlBySituation;

    /// <summary>
    /// 前のめり/後衛の区別があるか
    /// </summary>
    public bool HasVanguardBacklineDistinction =>
        Mode == SelectionMode.VanguardBacklineSingle ||
        Mode == SelectionMode.VanguardBacklineMulti;

    public override string ToString()
    {
        return $"TargetingPlan(Scope={Scope}, Mode={Mode}, ShowRange={ShowRangeSelection}, ShowTarget={ShowTargetSelection})";
    }
}
