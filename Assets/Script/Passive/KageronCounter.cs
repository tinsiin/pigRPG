using RandomExtensions;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
public class KageronCounter : BasePassive//カゲロンカウンター
{

    public override bool OnBeforeDamageActivate(BaseStates attacker)
    {
        //一個でも行動不可能な悪いパッシブを持っているなら
        if(HasBadCantACTPassive(_owner.Passives))
        {
            //基礎威力のpassivePowerによる分岐
            float BasePower = 0.3f;
            if(PassivePower == 1)
            {
                BasePower = 0.6f;//重ね掛けされてたら 60%
            }

            //ownerの持ってる全パッシブの　isCantACT と　IsBadのそれぞれの数を記録
            int PowerCount = 0;
            foreach(var passive in _owner.Passives)
            {
                if (passive.IsCantACT) PowerCount++;
                if (passive.IsBad) PowerCount++;
            }

            //必要条件では二つ含まれるはずなので、PowerCountから2を引く
            PowerCount -= 2;

            //基礎威力に強くする威力をパッシブ要素からカウントを増やす　数を掛けてね
            var LastPower = BasePower + PowerCount * 0.14f;


            //攻撃してきた陣営全員をレイザーアクト対象者リストとして記録
            var Targets = RemoveDeathCharacters(manager.MyGroup(attacker).Ours);//生きてる人限定でね

            //BattleManager上で「単純な毒というよりは見た目とたとえるならRPGだとしたらわざわざエフェクトが出るような派手なパッシブダメージ」を予約
            EnqueueFlashyPassiveDamage(Targets, "カゲロンカウンター全体攻撃(レイザー)", LastPower);
            
            //攻撃(ダメージ)を無効化。
            return false;
        }
        //何もなければ通常通り実行される。
        return true;
    }

    /// <summary>
    /// 一つでも行動不可能な悪いパッシブを持っているかどうか
    /// </summary>
    bool HasBadCantACTPassive(List<BasePassive> passives)
    {
        foreach(var passive in passives)
        {
            if(passive.IsBad && passive.IsCantACT)
            {
                return true;
            }
        }
        return false;
    }
}