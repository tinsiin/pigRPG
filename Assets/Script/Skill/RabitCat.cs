using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static CommonCalc;

public class RabitCat : BaseSkill
{
    public override HitResult SkillHitCalc(BaseStates target,float supremacyBonus = 0,HitResult hitResult = HitResult.Hit,bool PreliminaryMagicGrazeRoll = false)
    {
        var calculatedValue = CalculateRabitCatEffectiveness(Doer, target);
        if(calculatedValue > 0) return base.SkillHitCalc(target,supremacyBonus,hitResult,PreliminaryMagicGrazeRoll);
        return HitResult.CompleteEvade;//ラビットキャットの効果ターンが0以下、ないのなら、発動しなかったとして完全回避
    }
    /// <summary>
    /// ラビットキャットの効果ターン数
    /// </summary>
    float RabitCatTurn;
    /// <summary>
    /// ラビットキャット専用の発動計算を行う
    /// </summary>
    int CalculateRabitCatEffectiveness(BaseStates attacker, BaseStates defender)
    {
        var atk = attacker.ATK().Total;
        var def = defender.DEF().Total;
        var surplusValue = atk - def;
        if(surplusValue <= 0) return 0;//余剰値が無ければ発動しない0ターンを返す

        RabitCatTurn = surplusValue * 4 / def;
        // 最低1ターン保証
        RabitCatTurn = Mathf.Max(1f, RabitCatTurn);
        return (int)RabitCatTurn;
    }
    
    public override void ManualSkillEffect(BaseStates target,HitResult hitResult)//命中成功して、実行時の効果の本体
    {
        //相手が機械の種別なら発生しない
        if(target.MyType == CharacterType.Machine) return;
        //完全回避ならなし
        if(hitResult == HitResult.CompleteEvade) return;
        //かすりならば、1.3割の確率で発生 = 8.7割で発生しない
        if(hitResult == HitResult.Graze && rollper(87)) return;
        //クリティカルとHItに何の違いもない。
        
        const int CatID =21;
        const int RabitID = 20;
        //自分にキャットを付与
        Doer.ApplyPassiveBufferInBattleByID(CatID);
        //相手にラビットを付与
        target.ApplyPassiveBufferInBattleByID(RabitID);

        //パッシブリンクさせる。
        var catPas = Doer.GetBufferPassiveByID(CatID);
        var rabitPas = target.GetBufferPassiveByID(RabitID);
        catPas.SetPassiveLink(new LinkedPassive(rabitPas, true));//実行者側は兎側にダメージを通さないが、
        rabitPas.SetPassiveLink(new LinkedPassive(catPas, true,0.7f));//相手側は実行者にダメージがリンクする。
    }
}