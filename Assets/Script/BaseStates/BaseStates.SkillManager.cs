using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;


//スキル管理
public abstract partial class BaseStates    
{
    //  ==============================================================================================================================
    //                                              基本フィールド
    //  ==============================================================================================================================
    
    /// <summary>
    /// キャラクターが現在使用可能なスキルリスト
    /// </summary>
    public abstract IReadOnlyList<BaseSkill> SkillList { get; }
    /// <summary>
    /// 現在有効なTLOAスキルリスト
    /// </summary>
    public List<BaseSkill> TLOA_SkillList => SkillList.Where(x => x.IsTLOA).ToList();

    /// <summary>
    ///現在のの攻撃ターンで使われる
    /// </summary>
    [NonSerialized]
    public BaseSkill NowUseSkill;
    /// <summary>
    /// 強制続行中のスキル　nullならその状態でないということ
    /// </summary>
    [NonSerialized]
    public BaseSkill FreezeUseSkill;
    /// <summary>
    /// 前回使ったスキルの保持
    /// </summary>
    private BaseSkill _tempUseSkill;
    //  ==============================================================================================================================
    //                                             関数系
    //  ==============================================================================================================================

    /// <summary>
    /// スキル使用時の処理をまとめたコールバック
    /// </summary>
    public void SKillUseCall(BaseSkill useSkill)
    {
        //スキルのポインントの消費
        if(!TryConsumeForSkillAtomic(useSkill))
        {
            Debug.LogError(CharacterName + "のスキルのポインントの消費に失敗しました。" + CharacterName +"の" + (useSkill != null ? useSkill.SkillName : "<null>") + "を実行できません。"+
            "事前にポインント可否判定されてるはずなのにポインントが足りない。-SkillResourceFlowクラスとPs.OnlySelectActsやBattleAIBrainを確認して。" );
            return;
        }

        NowUseSkill = useSkill;//使用スキルに代入する
        Debug.Log(useSkill.SkillName + "を" + CharacterName +" のNowUseSkillにボタンを押して登録しました。");
        
        //ムーブセットをキャッシュする。連続攻撃でもそうでなくてもキャッシュ
        NowUseSkill.CashMoveSet();

        //今回選んだスキル以外のストック可能なスキル全てのストックを減らす。
        var list = SkillList.Where(skill =>  !ReferenceEquals(skill, useSkill) && skill.HasConsecutiveType(SkillConsecutiveType.Stockpile)).ToList();
        foreach(var stockSkill in list)
        {
            stockSkill.ForgetStock();
        }

    }
    /// <summary>
    /// スキルを連続実行した回数などをスキルのクラスに記録する関数
    /// </summary>
    /// <param name="useSkill"></param>
    public void SkillUseConsecutiveCountUp(BaseSkill useSkill)
    {
        useSkill.SkillHitCount();//スキルのヒット回数の計算

        if (useSkill == _tempUseSkill)//前回使ったスキルと同じなら
        {
            useSkill.DoConsecutiveCount++;//連続実行回数を増やす
            useSkill.HitConsecutiveCount++;//連続ヒット回数を増やす
        }
        else//違ったら
        {
            if (_tempUseSkill != null)//nullじゃなかったら
            {
                _tempUseSkill.DoConsecutiveCount = 0;//リセット
                _tempUseSkill.HitConsecutiveCount++;//連続ヒット回数をリセット　
            }
            useSkill.DoConsecutiveCount++;//最初の一回目として
            useSkill.HitConsecutiveCount++;//連続ヒット回数を増やす
        }
    }

}
