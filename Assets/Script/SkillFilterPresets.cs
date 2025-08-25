using System;

public static class SkillFilterPresets
{
    /// <summary>
    /// 単体先約用の範囲トレイトとタイプのマスク（共通定義）
    /// </summary>
    public static readonly SkillZoneTrait SingleTargetZoneTraitMask =
          SkillZoneTrait.CanPerfectSelectSingleTarget
        | SkillZoneTrait.CanSelectSingleTarget
        | SkillZoneTrait.RandomSingleTarget
        | SkillZoneTrait.ControlByThisSituation;

    /// <summary>
    /// 単体先約用のタイプのマスク（共通定義）
    /// </summary>
    public static readonly SkillType SingleTargetTypeMask = SkillType.Attack;

    /// <summary>
    /// スキルが単体先約用の条件を満たすか（AI/味方UI 共通で使用可能）
    /// </summary>
    public static bool MatchesSingleTargetReservation(BaseSkill skill)
    {
        if (skill == null) return false;
        return skill.HasZoneTraitAny(SingleTargetZoneTraitMask)
            && skill.HasType(SingleTargetTypeMask);
    }
}
