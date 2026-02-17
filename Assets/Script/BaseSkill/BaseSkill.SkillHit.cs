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
    /// スキルの命中補正 int 百分率
    /// </summary>
    public int SkillHitPer => FixedSkillLevelData[_levelIndex].SkillHitPer;

    /// <summary>
    /// スキルにより補正された最終命中率
    /// 引数の命中凌駕はIsReactHitで使われるものなので、キャラ同士の命中回避計算が
    /// 必要ないものであれば、引数を指定しなくていい(デフォ値)
    /// またHitResultの引数は事前の命中回避計算等でどういうヒット結果になったかを渡して最終結果として返すため。
    /// スキル命中onlyならデフォルトで普通のHitが指定されるので渡さなくてOK
    /// </summary>
    /// <param name="supremacyBonus">命中ボーナス　主に命中凌駕用途</param>>
    public virtual HitResult SkillHitCalc(BaseStates target,float supremacyBonus = 0,HitResult hitResult = HitResult.Hit,bool PreliminaryMagicGrazeRoll = false, BaseStates actor = null)
    {
        //割り込みカウンターなら確実
        if(actor != null && actor.HasPassive(1)) return hitResult;

        //通常計算
        var rndMin = RandomSource.NextInt(3);//ボーナスがある場合ランダムで三パーセント~0パーセント引かれる
        if(supremacyBonus>rndMin)supremacyBonus -= rndMin;

        var result = RandomSource.NextInt(100) < supremacyBonus + SkillHitPer ? hitResult : HitResult.CompleteEvade;

        if(result == HitResult.CompleteEvade && IsMagic)//もし発生しなかった場合、魔法スキルなら
        {
            //三分の一の確率でかする
            if(RandomSource.NextInt(3) == 0) result = HitResult.Graze;
        }

        if(PreliminaryMagicGrazeRoll)//事前魔法かすり判定がIsReactHitで行われていたら
        {
            result = hitResult;//かすりを入れる
        }

        return result;
    }
    /// <summary>
    /// Manual系のスキルでの用いるスキルごとの独自効果
    /// </summary>
    /// <param name="target"></param>
    public virtual void ManualSkillEffect(BaseStates actor, BaseStates target, HitResult hitResult)
    {

    }

}
