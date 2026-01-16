/// <summary>
/// SkillZoneTraitのグループ定義
/// 同一グループが複数箇所でハードコードされることを防ぐ
/// </summary>
public static class SkillZoneTraitGroups
{
    /// <summary>
    /// ランダム分岐用性質（実行時にはRangeWillから除去される）
    /// RandomTargetALLSituation, RandomTargetMultiOrSingle, etc.
    /// </summary>
    public static readonly SkillZoneTrait RandomBranchTraits =
        SkillZoneTrait.RandomTargetALLSituation |
        SkillZoneTrait.RandomTargetALLorMulti |
        SkillZoneTrait.RandomTargetALLorSingle |
        SkillZoneTrait.RandomTargetMultiOrSingle;

    /// <summary>
    /// 実際の範囲性質（TargetingServiceで処理される）
    /// AllTarget, RandomMultiTarget, etc.
    /// </summary>
    public static readonly SkillZoneTrait ActualRangeTraits =
        SkillZoneTrait.AllTarget |
        SkillZoneTrait.RandomMultiTarget |
        SkillZoneTrait.RandomSelectMultiTarget |
        SkillZoneTrait.RandomSingleTarget;

    /// <summary>
    /// メイン選択性質（DetermineRangeRandomlyの分岐で使用）
    /// 範囲ランダム決定後に除去される性質群
    /// </summary>
    public static readonly SkillZoneTrait MainSelectTraits =
        SkillZoneTrait.CanSelectSingleTarget |
        SkillZoneTrait.RandomSingleTarget |
        SkillZoneTrait.ControlByThisSituation |
        SkillZoneTrait.CanSelectMultiTarget |
        SkillZoneTrait.RandomSelectMultiTarget |
        SkillZoneTrait.RandomMultiTarget |
        SkillZoneTrait.AllTarget;

    /// <summary>
    /// サブ性質（オプション的な性質）
    /// DetermineRangeRandomly後も維持される
    /// </summary>
    public static readonly SkillZoneTrait SubTraits =
        SkillZoneTrait.CanSelectAlly |
        SkillZoneTrait.CanSelectDeath |
        SkillZoneTrait.CanSelectMyself |
        SkillZoneTrait.SelectOnlyAlly |
        SkillZoneTrait.CanSelectRange;

    /// <summary>
    /// 単体系性質
    /// 先約リストや単体ターゲット判定で使用
    /// </summary>
    /// <remarks>
    /// 旧定数: CommonCalc.SingleZoneTrait, SkillFilterPresets.SingleTargetZoneTraitMask
    /// </remarks>
    public static readonly SkillZoneTrait SingleTargetTraits =
        SkillZoneTrait.CanPerfectSelectSingleTarget |
        SkillZoneTrait.CanSelectSingleTarget |
        SkillZoneTrait.RandomSingleTarget |
        SkillZoneTrait.ControlByThisSituation;

    /// <summary>
    /// 複数対象系性質
    /// </summary>
    public static readonly SkillZoneTrait MultiTargetTraits =
        SkillZoneTrait.CanSelectMultiTarget |
        SkillZoneTrait.RandomSelectMultiTarget |
        SkillZoneTrait.RandomMultiTarget;

    /// <summary>
    /// 優先的性質（他の性質より優先して処理される）
    /// </summary>
    public static readonly SkillZoneTrait PriorityTraits =
        SkillZoneTrait.SelfSkill |
        SkillZoneTrait.SelectOnlyAlly;

    /// <summary>
    /// 前のめり/後衛を区別する性質
    /// SelectOnlyAllyと競合する
    /// </summary>
    public static readonly SkillZoneTrait VanguardBacklineTraits =
        SkillZoneTrait.CanSelectSingleTarget |
        SkillZoneTrait.CanSelectMultiTarget |
        SkillZoneTrait.RandomSelectMultiTarget;

    /// <summary>
    /// ランダム性を持つ性質
    /// </summary>
    public static readonly SkillZoneTrait RandomTraits =
        SkillZoneTrait.RandomSingleTarget |
        SkillZoneTrait.RandomSelectMultiTarget |
        SkillZoneTrait.RandomMultiTarget |
        SkillZoneTrait.RandomRange |
        RandomBranchTraits;
}
