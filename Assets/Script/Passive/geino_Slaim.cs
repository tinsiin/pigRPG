using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ジーノのスレーム
/// </summary>
public class geino_Slaim : Slaim
{
    public override void OnBeforeDamage(BaseStates Atker)
    {
        //パワーが普通以上なら、ライティスティニを追加
        if(_owner.NowPower >= ThePower.medium)
        {
            _owner.ApplyPassiveBufferInBattleByID(10);
        }
        
        base.OnBeforeDamage(Atker);
    }
}