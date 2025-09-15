using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;

public abstract partial class BaseStates    
{

    /// <summary>
    /// 命中回避計算で使用する回避率
    /// </summary>
    float EvasionRate(float baseAgi,BaseStates Attacker)
    {
        float evasionRate;

        evasionRate = baseAgi * SkillEvasionModifier;

        if(Attacker.BattleFirstSurpriseAttacker)//bm最初のターンで先手攻撃を受ける場合
        evasionRate  = baseAgi * 0.7f;//0.7倍で固定

        //パッシブ由来のキャラクタ限定回避補正
        evasionRate *= PassivesEvasionPercentageModifierByAttacker();

        return evasionRate;
    }

    
    //  ==============================================================================================================================
    //                                              スキル影響回避率
    //  ==============================================================================================================================

    /// <summary>
    /// スキルにより影響された回避補正率
    /// </summary>
    float _skillEvasionModifier = 1f;

    /// <summary>
    /// 平準化する回避補正率
    /// </summary>
    float _BaseEvasionModifier = 1f;

    /// <summary>
    /// 回避率でAGIに掛けるスキルにより影響された回避補正率　
    /// 落ち着きターン経過により減衰する。
    /// 回避率の計算の際に最終回避率としてAGIと掛ける
    /// </summary>
    float SkillEvasionModifier
    {
        get
        {
            var calmDownModifier = CalmDownCount;//落ち着きカウントによる補正
            var calmDownModifierMax = CalmDownCountMax;
            if (calmDownModifier < 0)calmDownModifier = 0;//落ち着きカウントがマイナスにならないようにする

            // カウントダウンの進行度に応じて線形補間
            // カウントが減るほど _BaseEvasionModifier に近づく
            float progress = 1.0f - (calmDownModifier / calmDownModifierMax);
            
            // 線形補間: _skillEvasionModifier から _BaseEvasionModifier へ徐々に変化
            return _skillEvasionModifier + (_BaseEvasionModifier - _skillEvasionModifier) * progress;
        }
    }


    //  ==============================================================================================================================
    //                                              味方別口回避
    //  ==============================================================================================================================


    /// <summary>
    /// パーティー属性という雰囲気における味方同士の攻撃の別口回避
    /// 既存の計算とは独立している
    /// </summary>
    HitResult AllyEvadeCalculation(BaseStates attacker)
    {
        if(manager.IsFriend(attacker, this))//まず味方同士かどうかを判断
        {
            float evasionRate;//回避弱体補正
            var mygroup = manager.MyGroup(this);
            //パーティー属性により発生判定と、回避倍率を決める
            switch(mygroup.OurImpression)
            {
                case PartyProperty.Odradeks:
                evasionRate = 0.25f;
                break;
                case PartyProperty.TrashGroup:
                evasionRate = 0.5f;
                break;
                default:
                return HitResult.none;//発生しなかった。
            }

            //相性値による回避率のすげ替え
            var agi = AGI().Total;
            if(mygroup.CharaCompatibility[(attacker,this)] >= 88)//攻撃者から自分への味方同士の相性値が特定以上なら
            {
                //また、攻撃者のAGIが自分より大きければ、計算に使うAGIをすげ替える
                agi = Mathf.Max(agi,attacker.AGI().Total);
            }

            if (RandomEx.Shared.NextFloat(attacker.EYE().Total + agi * evasionRate) < attacker.EYE().Total)
            {
                //三分の一でかすり
                if(rollper(33))return HitResult.Graze;

                return HitResult.CompleteEvade;
            }
        
        }
        return HitResult.none;//発生せず
    }


}
