/// <summary>
/// SkillZoneTrait向けビット演算ヘルパー拡張メソッド
/// ビット演算の意図を明確にし、バグを防ぐ
/// </summary>
public static class SkillZoneTraitExtensions
{
    /// <summary>
    /// 指定した性質を追加する
    /// </summary>
    /// <example>
    /// rangeWill = rangeWill.Add(SkillZoneTrait.CanSelectAlly);
    /// </example>
    public static SkillZoneTrait Add(this SkillZoneTrait current, SkillZoneTrait toAdd)
        => current | toAdd;

    /// <summary>
    /// 指定した性質を除去する
    /// </summary>
    /// <example>
    /// rangeWill = rangeWill.Remove(SkillZoneTraitGroups.MainSelectTraits);
    /// </example>
    public static SkillZoneTrait Remove(this SkillZoneTrait current, SkillZoneTrait toRemove)
        => current & ~toRemove;

    /// <summary>
    /// 指定した性質のみを残す（それ以外を除去）
    /// </summary>
    /// <example>
    /// subTraitsOnly = rangeWill.KeepOnly(SkillZoneTraitGroups.SubTraits);
    /// </example>
    public static SkillZoneTrait KeepOnly(this SkillZoneTrait current, SkillZoneTrait toKeep)
        => current & toKeep;

    /// <summary>
    /// 指定した性質を全て持っているか
    /// </summary>
    /// <example>
    /// if (rangeWill.HasAll(SkillZoneTrait.SelectOnlyAlly | SkillZoneTrait.CanSelectMyself)) { ... }
    /// </example>
    public static bool HasAll(this SkillZoneTrait current, SkillZoneTrait traits)
        => (current & traits) == traits;

    /// <summary>
    /// 指定した性質のいずれかを持っているか
    /// </summary>
    /// <example>
    /// if (rangeWill.HasAny(SkillZoneTraitGroups.SingleTargetTraits)) { ... }
    /// </example>
    public static bool HasAny(this SkillZoneTrait current, SkillZoneTrait traits)
        => (current & traits) != 0;

    /// <summary>
    /// 指定した性質を持っていないか
    /// </summary>
    public static bool HasNone(this SkillZoneTrait current, SkillZoneTrait traits)
        => (current & traits) == 0;

    /// <summary>
    /// 性質の置き換え（oldを除去してnewを追加）
    /// </summary>
    /// <example>
    /// rangeWill = rangeWill.Replace(
    ///     SkillZoneTrait.RandomSelectMultiTarget,
    ///     SkillZoneTrait.RandomMultiTarget);
    /// </example>
    public static SkillZoneTrait Replace(
        this SkillZoneTrait current,
        SkillZoneTrait oldTraits,
        SkillZoneTrait newTraits)
        => (current & ~oldTraits) | newTraits;
}
