using NRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Turtle : BasePassive//アキレスと亀-混沌時間　の亀
{
    //アキレス限定の防御時ケレン行動パーセント　と　回避率(非AGI) を登録する処理は　スキルから行う。

    //アキレスに攻撃されない間 = アキレスにHIT以上を当てられて崩壊するまで
    //= 常に、登録された限定の回避率全てと、限定のDontDamageRationを成長させる
    public override void OnNextTurn()
    {
        base.OnNextTurn();

        //キャラ限定　回避率と非ダメージ率の自動成長
        GrowDontDamageHpMinRatioByAttacker(0.013f);//1.3%ずつ増える
        GrowEvasionPercentageModifierByAttacker(0.04f);//4%ずつ増える
    }
    
    //完全単体選択攻撃時もしアキレスを攻撃してたらアキレスと亀が崩壊する処理
    public override void OnAfterPerfectSingleAttack(BaseStates UnderAttacker)
    {
        base.OnAfterPerfectSingleAttack(UnderAttacker);

        //もし単体選択攻撃対象者が、アキレスのパッシブ者なら(=唯一のパッシブリンクと同じなら)
        if(UnderAttacker == _passiveLinkList[0].Passive._owner)
        {
            RemovePassiveByOwnerCall();//自分の亀パッシブを消す(パッシブリンクしてるのは自動で消える)
        }
    }

}