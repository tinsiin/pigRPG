using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RabitCat : BaseSkill
{
    public override HitResult SkillHitCalc(BaseStates target,float supremacyBonus = 0,HitResult hitResult = HitResult.Hit,bool PreliminaryMagicGrazeRoll = false)
    {
        var calculatedValue = CalculateRabitCatEffectiveness(Doer, target);
        if(calculatedValue > 0) return base.SkillHitCalc(target,supremacyBonus,hitResult,PreliminaryMagicGrazeRoll);
        return HitResult.CompleteEvade;//ラビットキャットの効果ターンが0以下、ないのなら、発動しなかったとして完全回避
    }

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
}