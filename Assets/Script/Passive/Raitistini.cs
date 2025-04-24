using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ジーノのスレームに付帯されるサブ効果のパッシブ　ライティスティニ
/// </summary>
public class Raitistini : BasePassive
{
    //割り込みカウンター時に、「ライティスティニの効果パッシブ」と「前のめり交代阻止のパッシブ」を付与
    public override void OnInterruptCounter()
    {
        base.OnInterruptCounter();
        //「ライティスティニの効果パッシブ」を付与
        _owner.ApplyPassiveBufferInBattleByID(11);

        //「前のめり交代阻止のパッシブ」を付与
        _owner.ApplyPassiveBufferInBattleByID(8);
        var pas =_owner.GetBufferPassiveByID(8);
        if(pas != null)pas.DurationTurn = 3;//持続ターンを3ターンに変更する
    }
}