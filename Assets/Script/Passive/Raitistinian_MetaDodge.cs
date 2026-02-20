using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// ライティスティニ者の回避回遊　ライティスティニの効果パッシブ
/// </summary>
public class Raitistinian_MetaDodge : BasePassive
{
    public override float DEFFixedValueEffect()//防御値だけ十日能力補正するのでここで書く
    {
        var smiler = _owner.TenDayValues(false).GetValueOrZero(TenDayAbility.Smiler);
        var Dokumamusi = _owner.TenDayValues(false).GetValueOrZero(TenDayAbility.Dokumamusi);
        return (smiler + Dokumamusi) / 3;
    }
}