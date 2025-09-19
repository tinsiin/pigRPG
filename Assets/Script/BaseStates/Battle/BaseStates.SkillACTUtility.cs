using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;


//戦闘などでスキル実行に伴う関数群
//ある程度はっきりしてるのをここにおいて　細かい物はACTUtilityの方にある。
//スキルを特定の計算に使ったかどーかとか、特定の関数内でキャンセルされたかどーかとか
public abstract partial class BaseStates    
{
    //  ==============================================================================================================================
    //                                              選別シチュエーション
    //  ==============================================================================================================================
    /// <summary>
    /// このキャラがどの辺りを狙っているか
    /// </summary>
    [Header("選別シチュエーション")]
    [Tooltip("このキャラがどの辺りを狙っているか（ターゲット意志）")]
    public DirectedWill Target = 0;

    //  ==============================================================================================================================
    //                                              範囲意志
    //  ==============================================================================================================================
    /// <summary>
    /// このキャラの現在の範囲の意思　　複数持てる
    /// スキルの範囲性質にcanSelectRangeがある場合のみ、ない場合はskillのzoneTraitをそのまま代入される。
    /// </summary>
    [Space]
    [Header("範囲意志")]
    [Tooltip("現在のスキル範囲の意思（複数可）。範囲選択性質がある場合のみ反映。無い場合はスキルのzoneTraitを代入")]
    public SkillZoneTrait RangeWill = 0;

    /// <summary>
    /// スキル範囲性質を持ってるかどうか
    /// 複数指定した場合は全て当てはまってるかどうかで判断
    /// </summary>
    public bool HasRangeWill(params SkillZoneTrait[] skills)
    {
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }
        return (RangeWill & combinedSkills) == combinedSkills;
    }
    /// <summary>
    /// 指定されたスキルフラグのうち、一つでもRangeWillに含まれている場合はtrueを返し、
    /// 全く含まれていない場合はfalseを返します。
    /// </summary>
    public bool HasRangeWillsAny(params SkillZoneTrait[] skills)
    {
        // 受け取ったスキルフラグをビット単位で結合
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }

        // RangeWillに含まれるフラグとcombinedSkillsのビットAND演算
        // 結果が0でなければ、一つ以上のフラグが含まれている
        return (RangeWill & combinedSkills) != 0;
    }

    /// <summary>
    /// 指定されたスキルフラグのうち、一つでもRangeWillに含まれている場合はfalseを返し、
    /// 全く含まれていない場合はtrueを返します。
    /// </summary>
    public bool DontHasRangeWill(params SkillZoneTrait[] skills)
    {
        // 受け取ったスキルフラグをビット単位で結合
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }

        // RangeWillに含まれるフラグとcombinedSkillsのビットAND演算
        // 結果が0でなければ、一つ以上のフラグが含まれている
        bool containsAny = (RangeWill & combinedSkills) != 0;

        // 一つでも含まれていればfalse、含まれていなければtrueを返す
        return !containsAny;
    }
    /// <summary>
    /// 単体系スキル範囲性質のいずれかを持っているかを判定
    /// </summary>
    public bool HasAnySingleRangeWillTrait()
    {
        return (RangeWill & SingleZoneTrait) != 0;
    }
    
    /// <summary>
    /// 単体系スキル範囲性質のすべてを持っているかを判定
    /// </summary>
    public bool HasAllSingleRangeWillTraits()
    {
        return (RangeWill & SingleZoneTrait) == SingleZoneTrait;
    }
    /// <summary>
    /// 完全な単体攻撃かどうか
    /// (例えばControlByThisSituationの場合はrangeWillにそのままskillのzoneTraitが入るので、
    /// そこに範囲系の性質(事故で範囲攻撃に変化)がある場合はfalseが返る
    /// </summary>
    /// <returns></returns>
    private bool IsPerfectSingleATK()
    {
        return DontHasRangeWill(SkillZoneTrait.CanSelectMultiTarget,
            SkillZoneTrait.RandomSelectMultiTarget, SkillZoneTrait.RandomMultiTarget,
            SkillZoneTrait.AllTarget);
    }

    /* ------------------------------------------------------------------------------------------------------------------------------------------
     * スキル強制続行　Freeze 関連
     * ------------------------------------------------------------------------------------------------------------------------------------------
     */

    /// <summary>
    /// 強制続行中のスキルの範囲性質
    /// </summary>
    [NonSerialized]
    public SkillZoneTrait FreezeRangeWill;
    /// <summary>
    /// 強制続行中のスキルの範囲性質を設定する
    /// </summary>
    public void SetFreezeRangeWill(SkillZoneTrait NowRangeWill)
    {
        FreezeRangeWill = NowRangeWill;
    }

    //  ==============================================================================================================================
    //                                              スキル強制続行　Freeze
    //  ==============================================================================================================================
    /// <summary>
    /// 使用中のスキルを強制続行中のスキルとする。　
    /// 例えばスキルの連続実行中の処理や発動カウント中のキャンセル不可能なスキルなどで使う
    /// </summary>
    public void FreezeSkill()
    {
        FreezeUseSkill = NowUseSkill;
    }
    /// <summary>
    /// 強制続行中のスキルをなくす
    /// </summary>
    public void Defrost()
    {
        FreezeUseSkill = null;
        FreezeRangeWill = 0;
    }

    /// <summary>
    /// スキルが強制続行中かどうか
    /// </summary>
    public bool IsFreeze => FreezeUseSkill != null;
    //  ==============================================================================================================================
    //                                              スキル連続攻撃強制続行　FreezeConsecuticve
    //  ==============================================================================================================================
    /// <summary>
    /// 現在の自分自身の実行中のFreezeConsecutiveを削除するかどうかのフラグ
    /// </summary>
    [NonSerialized]
    public bool IsDeleteMyFreezeConsecutive = false;

    /// <summary>
    /// FreezeConsecutive、ターンをまたぐ連続実行スキルが実行中かどうか。
    /// </summary>
    /// <returns></returns>
    public bool IsNeedDeleteMyFreezeConsecutive()
    {
        if(NowUseSkill?.NowConsecutiveATKFromTheSecondTimeOnward() == true)//連続攻撃中で、
        {
            if(NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
            {
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// ターンをまたぐ連続実行スキル(FreezeConsecutiveの性質持ち)が実行中なのを次回のターンで消す予約をする
    /// </summary>
    public void TurnOnDeleteMyFreezeConsecutiveFlag()
    {
        Debug.Log("TurnOnDeleteMyFreezeConsecutiveFlag を呼び出しました。");
        IsDeleteMyFreezeConsecutive = IsNeedDeleteMyFreezeConsecutive();
    }

    /// <summary>
    /// consecutiveな連続攻撃の消去
    /// </summary>
    public void DeleteConsecutiveATK()
    {
        if(FreezeUseSkill != null)
        {
            FreezeUseSkill.ResetAtkCountUp();//強制実行中のスキルの攻撃カウントアップをリセット
        }
        Defrost();//解除
        IsDeleteMyFreezeConsecutive = false;

    }



}
