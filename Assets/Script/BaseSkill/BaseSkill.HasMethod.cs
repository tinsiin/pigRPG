using R3;
using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
public partial class BaseSkill
{

    /// <summary>
    /// スキル性質を持ってるかどうか
    /// 複数指定した場合は全て当てはまってるかどうかで判断
    /// </summary>
    public bool HasType(params SkillType[] skills)
    {
        SkillType combinedSkills = 0;
        foreach (SkillType skill in skills)
        {
            combinedSkills |= skill;
        }
        return (SkillType & combinedSkills) == combinedSkills;
    }
    /// <summary>
    /// スキル性質のいずれかを持ってるかどうか
    /// 複数指定の場合は一つでも持ってればtrueを返します。
    /// </summary>
    public bool HasTypeAny(params SkillType[] skills)
    {
        return skills.Any(skill => (SkillType & skill) != 0);    
    }
    /// <summary>
    /// そのスキル連続性質を持ってるかどうか
    /// </summary>
    public bool HasConsecutiveType(SkillConsecutiveType skill)
    {
        return (ConsecutiveType & skill) == skill;
    }
    /// <summary>
    /// スキル範囲性質を持ってるかどうか
    /// 複数指定した場合は全て当てはまってるかどうかで判断
    /// </summary>
    public bool HasZoneTrait(params SkillZoneTrait[] skills)
    {
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }
        return (ZoneTrait & combinedSkills) == combinedSkills;
    }
    /// <summary>
    /// 先約リストでのsingleTarget指定用のスキルの性質にあってるものかどうか。
    /// </summary>
    /// <returns></returns>
    public bool IsEligibleForSingleTargetReservation()
    {
        return SkillFilterPresets.MatchesSingleTargetReservation(this);
    }
    /// <summary>
    /// スキル範囲性質のいずれかを持ってるかどうか
    /// 複数指定した場合はどれか一つでも当てはまればtrueを返す
    /// </summary>
    public bool HasZoneTraitAny(params SkillZoneTrait[] skills)
    {
        return skills.Any(skill => (ZoneTrait & skill) != 0);
    }
    /// <summary>
    /// 単体系スキル範囲性質のいずれかを持っているかを判定
    /// </summary>
    public bool HasAnySingleTargetTrait()
    {
        return (ZoneTrait & CommonCalc.SingleZoneTrait) != 0;
    }
    
    /// <summary>
    /// 単体系スキル範囲性質のすべてを持っているかを判定
    /// </summary>
    public bool HasAllSingleTargetTraits()
    {
        return (ZoneTrait & CommonCalc.SingleZoneTrait) == CommonCalc.SingleZoneTrait;
    }


    /// <summary>
    /// スキルの特殊判別性質を持っているかどうか
    /// </summary>
    public bool HasSpecialFlag(SkillSpecialFlag skill)
    {
        return (SpecialFlags & skill) == skill;
    }
    /// <summary>
    /// TLOAかどうか
    /// </summary>
    public bool IsTLOA
    {
        get { return HasSpecialFlag(SkillSpecialFlag.TLOA); }
    }
    /// <summary>
    /// 魔法スキルかどうか
    /// </summary>
    public bool IsMagic
    {
        get { return HasSpecialFlag(SkillSpecialFlag.Magic); }
    }
    /// <summary>
    /// 切物スキルかどうか
    /// </summary>
    public bool IsBlade
    {
        get { return HasSpecialFlag(SkillSpecialFlag.Blade); }
    }
}