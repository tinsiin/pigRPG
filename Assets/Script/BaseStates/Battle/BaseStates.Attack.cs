using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;

public abstract partial class BaseStates    
{
    //  ==============================================================================================================================
    //                                              メイン関数
    //  ==============================================================================================================================

    /// <summary>
    /// クラスを通じて相手を攻撃する
    /// </summary>
    public virtual async UniTask<string> AttackChara(UnderActersEntryList Unders)
    {
        TenDayGrowthListByHIT = new();//ヒット分成長リストを初期化する。

        //素振り分のスキルの印象構造の十日能力が上昇する。
        if(NowUseSkill.HasType(SkillType.Attack))
        {
            GrowTenDayAbilityBySkill(0.3f,NowUseSkill.TenDayValues());
        }else{
            GrowTenDayAbilityBySkill(0.1f,NowUseSkill.TenDayValues());
        }

        SkillUseConsecutiveCountUp(NowUseSkill);//連続カウントアップ
        string txt = "";

       

        //対象者ボーナスの適用
        //if(Unders.Count == 1)//結果として一人だけを選び、
        //{
            //範囲意志で判定。
            var randomRangeSpecialBool //RandomRangeなら範囲意志が書き変わるので、RandomRangeがあるならば、Skill.HasZoneTraitで単体スキルかどうかの判定をする
            = SkillCalculatedRandomRange && NowUseSkill.HasAnySingleTargetTrait();
            if(HasAnySingleRangeWillTrait()|| randomRangeSpecialBool)//単体スキルの範囲意志を持ってるのなら
            {
                int index = -1;//判定関数でのFindIndexは見つからなかった場合-1を返す
                if((index = TargetBonusDatas.DoIHaveTargetBonusAny_ReturnListIndex(Unders.GetCharacterList())) != -1)//対象者ボーナスを持っていれば = 選んだ敵が対象者ボーナスならば。
                {
                    //適用
                    SetSpecialModifier("対象者ボーナス", whatModify.atk,TargetBonusDatas.GetAtPowerBonusPercentage(index));

                    //適用した対象者ボーナスの削除　該当インデックスのclear関数の制作
                    TargetBonusDatas.BonusClear(index);
                }
            }
        //}

        //「全体攻撃時」、被害者側全員の、「自分陣営が全体攻撃食らった時の自分のパッシブコールバック」
        if(HasRangeWill(SkillZoneTrait.AllTarget))
        {
            //undersに含まれる陣営を全て抽出し、その陣営グループのコールバックを呼び出す
            var EneFaction = Unders.charas.Any(x=>manager.GetCharacterFaction(x) == allyOrEnemy.Enemyiy);
            var AllyFaction = Unders.charas.Any(x=>manager.GetCharacterFaction(x) == allyOrEnemy.alliy);

            //それぞれの陣営のコールバック
            if(EneFaction)
            manager.FactionToGroup(allyOrEnemy.Enemyiy).PartyPassivesOnBeforeAllAlliesDamage(this,ref Unders);
            if(AllyFaction)
            manager.FactionToGroup(allyOrEnemy.alliy).PartyPassivesOnBeforeAllAlliesDamage(this,ref Unders);
        }

        //キャラクターに対して実行
        BeginSkillHitAggregation();
        for (var i = 0; i < Unders.Count; i++)
        {
            var ene = Unders.GetAtCharacter(i);
            ApplyCharaConditionalToSpecial(ene);//キャラ限定補正を通常の特別補正リストに追加　キャラが合ってればね
            schizoLog.AddLog($"{ene.CharacterName}のReactionSkillが始まった-Undersのカウント数:{Unders.Count}",true);
            txt += await ene.ReactionSkillOnBattle(this, Unders.GetAtSpreadPer(i));//敵がスキルにリアクション
        }
        // 対象処理が完了したのでキャスト単位でポイント精算
        var overallHit = EndSkillHitAggregation();
        SettlePointsAfterSkillOutcome(NowUseSkill, overallHit);

        NowUseSkill.ConsecutiveFixedATKCountUP();//使用したスキルの攻撃回数をカウントアップ
        NowUseSkill.DoSkillCountUp();//使用したスキルの使用回数をカウントアップ
        RemoveUseThings();//特別な補正を消去
        PassivesOnAfterAttack();//攻撃後のパッシブ効果
        Debug.Log("AttackChara- 攻撃した人数:" + Unders.Count);

        //今回の攻撃で一回でもヒットしていれば  「攻撃者側の攻撃の単位 = 範囲攻撃でも一回だけ = 攻撃者の為の処理」で実行されてほしい
        if (IsAnyHitInRecentSkillData(NowUseSkill, Unders.Count))
        {
            //当たったので精神回復　行動が一応成功したからメンタルが安心する。
            MentalHealOnAttack();
            CalmDownSet(NowUseSkill.EvasionModifier,NowUseSkill.AttackModifier);//スキル回避率と落ち着きカウントをセット
        }
        //HIT分の十日能力の成長
        foreach(var growData in TenDayGrowthListByHIT)
        {
            GrowTenDayAbilityBySkill(growData.Factor,growData.growTenDay);
        }

        _tempUseSkill = NowUseSkill;//使ったスキルを一時保存

        //スキルの精神属性に染まる
        PullImpressionFromSkill();
        //思えの値を回復する
        ResonanceHealingOnBattle();
        //アクション単位での行動記録
        var isdivergence = GetIsSkillDivergence();
        DidActionSkillDatas.Add(new ActionSkillData(isdivergence, NowUseSkill));
        return txt;
    }


    //  ==============================================================================================================================
    //                                              行動による変化関数
    //  ==============================================================================================================================
    /// <summary>
    /// スキルを実行した結果として精神属性に染まる
    /// </summary>
    void PullImpressionFromSkill()
    {
        var NextImpression =  NowUseSkill.SkillSpiritual;
        

        switch(NextImpression)//スキルの精神属性で特殊な分岐かそうでないかで
        {
            case SpiritualProperty.mvoid:
                MyImpression = SpiritualProperty.none;
                break;
            case SpiritualProperty.Galvanize:
                //変化なし
                break;
            case SpiritualProperty.air:
                //変化なし
                break;
            case SpiritualProperty.memento:
                MyImpression = DefaultImpression;
                break;
            default:
                 MyImpression = NextImpression;//基本的に実行した精神属性にそまる
                break;
        }
       
    }


    
}
