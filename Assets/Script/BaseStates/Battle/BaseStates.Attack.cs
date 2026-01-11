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
            AddBattleLog($"{ene.CharacterName}のReactionSkillが始まった-Undersのカウント数:{Unders.Count}", true);
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
            MentalHealOnAttack(NowUseSkill.AttackMentalHealPercent);
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
    //                                              行動記録
    //  ==============================================================================================================================

    /// <summary>
    /// キャラクターの対ひとりごとの行動記録
    /// </summary>
    [NonSerialized]
    public List<ACTSkillDataForOneTarget> ActDoOneSkillDatas;
    /// <summary>
    /// 直近の行動記録
    /// </summary>
    public ACTSkillDataForOneTarget RecentACTSkillData => ActDoOneSkillDatas[ActDoOneSkillDatas.Count - 1];
    /// <summary>
    /// アクション事のスキルデータ AttackChara単位で記録　= スキル一回に対して
    /// </summary>
    [NonSerialized]
    public List<ActionSkillData> DidActionSkillDatas = new();
    /// <summary>
    /// bm内にスキル実行した回数。
    /// </summary>
    protected int AllSkillDoCountInBattle => DidActionSkillDatas.Count;

    /// <summary>
    /// 直近の行動記録
    /// </summary>
    public ACTSkillDataForOneTarget RecentSkillData => ActDoOneSkillDatas[ActDoOneSkillDatas.Count - 1];
    /// <summary>
    /// 指定したスキルが最近のスキルデータでヒットしたかどうかを調べる
    /// </summary>
    private bool IsAnyHitInRecentSkillData(BaseSkill skill, int targetCount)
    {
        // 最新のtargetCount分のスキルデータを取得
        var recentSkillDatas = ActDoOneSkillDatas.Count >= targetCount 
            ? ActDoOneSkillDatas.GetRange(ActDoOneSkillDatas.Count - targetCount, targetCount) 
            : ActDoOneSkillDatas;

        // 最新のスキルデータでIsHitがtrueのものがあるか確認
        foreach (var data in recentSkillDatas)
        {
            if (data.IsHit && data.Skill == skill)
            {
                return true;
            }
        }
        
        return false;
    }
    /// <summary>
    /// そのキャラクターを殺すまでに与えたダメージ
    /// </summary>
    Dictionary<BaseStates, float> DamageDealtToEnemyUntilKill = new Dictionary<BaseStates, float>();
    /// <summary>
    /// キャラクターを殺すまでに与えるダメージを記録する辞書に記録する
    /// </summary>
    /// <param name="dmg"></param>
    /// <param name="target"></param>
    void RecordDamageDealtToEnemyUntilKill(float dmg,BaseStates target)//戦闘開始時にそのキャラクターを殺すまでに与えたダメージを記録する辞書に記録する
    {
        if (DamageDealtToEnemyUntilKill.ContainsKey(target))
        {
            DamageDealtToEnemyUntilKill[target] += dmg;
        }
        else
        {
            DamageDealtToEnemyUntilKill[target] = dmg;
        }
    }
    //  ==============================================================================================================================
    //                                             対象者ボーナス
    //  ==============================================================================================================================

    /// <summary>
    /// 現在持ってる対象者のボーナスデータ
    /// </summary>
    [NonSerialized]
    public TargetBonusDatas TargetBonusDatas = new();



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
/// <summary>
/// 対象者ボーナスのデータ
/// 相性値用の仕組みで汎用性は低い　　威力の1.〇倍ボーナス　を対象者限定で、尚且つ持続ターンありきのもの。
/// 割り込みカウンターのシングル単体攻撃とも違うぞ！
/// </summary>
public class TargetBonusDatas
{
    /// <summary>
    /// 持続ターン
    /// </summary>
    List<int> DurationTurns { get; set; }
    /// <summary>
    /// スキルのパワーボーナス倍率
    /// </summary>
    List<float> PowerBonusPercentages { get; set; }
    /// <summary>
    /// 対象者
    /// </summary>
    List<BaseStates> Targets { get; set; }
    /// <summary>
    /// 対象者がボーナスに含まれているか
    /// </summary>
    public bool DoIHaveTargetBonus(BaseStates target)
    {
        return Targets.Contains(target);
    }
    /// <summary>
    /// 渡されたリストの中に対象者が含まれているかどうか。
    /// 含まれていたらその対象者のリストのインデックスを返す。
    /// </summary>
    public int DoIHaveTargetBonusAny_ReturnListIndex(List<BaseStates> targets)
    {
        return Targets.FindIndex(x => targets.Contains(x));
    }
    
    /// <summary>
    /// 対象者のインデックスを取得
    /// </summary>
    public int GetTargetIndex(BaseStates target)
    {
        return Targets.FindIndex(x => x == target);
    }
    /// <summary>
    /// 対象者ボーナスが発動しているか
    /// </summary>
    //public List<bool> IsTriggered { get; set; }     ーーーーーーーーーーーー一回自動で発動するようにするから消す、明確に対象者ボーナスの適用を手動にするなら解除
    /// <summary>
    /// 発動してるかどうかを取得
    /// </summary>
    /*public bool GetAtIsTriggered(int index)
    {
        return IsTriggered[index];
    }*/
    /// <summary>
    /// 対象者ボーナスの持続ターンを取得
    /// </summary>
    public int GetAtDurationTurns(int index)
    {
        return DurationTurns[index];
    }
    /// <summary>
    /// 全てのボーナスをデクリメントと自動削除の処理
    /// </summary>
    public void AllDecrementDurationTurn()
    {
        for (int i = 0; i < DurationTurns.Count; i++)
        {
            DecrementDurationTurn(i);
        }
    }
    /// <summary>
    /// 持続ターンをデクリメントし、0以下になったら削除する。全ての対象者ボーナスを削除する。
    /// </summary>
    void DecrementDurationTurn(int index)
    {
        DurationTurns[index]--;
        if (DurationTurns[index] <= 0)
        {
            DurationTurns.RemoveAt(index);
            PowerBonusPercentages.RemoveAt(index);
            Targets.RemoveAt(index);
        }
    }
    /// <summary>
    /// 対象者ボーナスのパワーボーナス倍率を取得
    /// </summary>
    public float GetAtPowerBonusPercentage(int index)
    {
        return PowerBonusPercentages[index];
    }
    /// <summary>
    /// 対象者ボーナスの対象者を取得
    /// </summary>
    public BaseStates GetAtTargets(int index)
    {
        return Targets[index];
    }

    public TargetBonusDatas()
    {
        DurationTurns =  new();
        PowerBonusPercentages = new();
        Targets = new();
        //IsTriggered = new();
    }

    public void Add(int duration, float powerBonusPercentage, BaseStates target)
    {
        //targetの重複確認
        if (Targets.Contains(target))
        {
            int index = Targets.IndexOf(target);//同じインデックスの物をすべて消す
            DurationTurns.RemoveAt(index);
            PowerBonusPercentages.RemoveAt(index);
            Targets.RemoveAt(index);
            //IsTriggered.RemoveAt(index);
            return;
        }

        //追加
        DurationTurns.Add(duration);
        PowerBonusPercentages.Add(powerBonusPercentage);
        Targets.Add(target);
        //IsTriggered.Add(false);
    }
    /// <summary>
    /// 全削除
    /// </summary>
    public void AllClear()
    {
        DurationTurns.Clear();
        PowerBonusPercentages.Clear();
        Targets.Clear();
        //IsTriggered.Clear();
    }
    /// <summary>
    /// 該当のインデックスのボーナスを削除
    /// </summary>
    public void BonusClear(int index)
    {
        DurationTurns.RemoveAt(index);
        PowerBonusPercentages.RemoveAt(index);
        Targets.RemoveAt(index);
        //IsTriggered.RemoveAt(index);
    }
}
/// <summary>
/// スキルの行動記録　リストで記録する
/// 一人一人に対するものってニュアンス
/// </summary>
public class ACTSkillDataForOneTarget
{
    public bool IsDone;
    /// <summary>
    /// 攻撃が乱れたかどうか
    /// </summary>
    public bool IsDisturbed;
    public bool IsHit;
    public BaseSkill Skill;
    public BaseStates Target;   
    public ACTSkillDataForOneTarget(bool isdone, bool isdisturbed, BaseSkill skill, BaseStates target, bool ishit)
    {
        IsDone = isdone;
        Skill = skill;
        Target = target;
        IsHit = ishit;
        IsDisturbed = isdisturbed;
    }
}
public class ActionSkillData
{
    /// <summary>
    /// 実行したスキルが乖離しているかどうか
    /// </summary>
    public bool IsDivergence;
    public BaseSkill Skill;
    public ActionSkillData(bool isdivergence, BaseSkill skill)
    {
        IsDivergence = isdivergence;
        Skill = skill;
    }
}
