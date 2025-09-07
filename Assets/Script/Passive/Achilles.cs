using NRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Achilles : BasePassive//アキレスと亀-混沌時間　のアキレス
{

    /// <summary>
    /// 亀を攻撃して、ヒットした後の分岐。
    /// </summary>
    public override void OnAfterAttackToTargetWitheHitresult(BaseStates UnderAttacker, HitResult hitResult)
    {
        base.OnAfterAttackToTargetWitheHitresult(UnderAttacker, hitResult);

        //もし単体選択攻撃対象者が、亀のパッシブ者なら(=唯一のパッシブリンクと同じなら)
        if(UnderAttacker == _passiveLinkList[0].Passive._owner)
        {
            //ヒット以上なら、お互いのパッシブが崩壊する
            if(hitResult == HitResult.Hit || hitResult == HitResult.Critical)
            {
                RemovePassiveByOwnerCall();//このパッシブを消す(亀はパッシブリンクで消える。)
            }
            else//それ以外、っていうかかすりしかないけど、その場合、自分リカバリーターンが増えていく。
            {
                _maxRecoveryTurnModifier += NRandom.Shared.NextInt(1, 3);//1~2個ずつ
            }
        }

    }
}