using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Upper : BasePassive//アッパー  精神HPの上の乖離の基本効果など
{
    public override float ATKFixedValueEffect()
    {
        return _owner.TenDayValues().GetValueOrZero(TenDayAbility.BlazingFire);
    }

    public override float AGIFixedValueEffect()
    {
        var clamp = Mathf.Max(_owner.TenDayValues().GetValueOrZero(TenDayAbility.WaterThunderNerve) / 2.5f - _owner.TenDayValues().GetValueOrZero(TenDayAbility.dokumamusi), 0);
        return Mathf.Min(-(_owner.TenDayValues().GetValueOrZero(TenDayAbility.BlazingFire) - clamp), 0);
    }

    public override float DEFFixedValueEffect()
    {
        var clamp = _owner.TenDayValues().GetValueOrZero(TenDayAbility.NightInkKnight) / 5f;
        return Mathf.Min(-(_owner.TenDayValues().GetValueOrZero(TenDayAbility.BlazingFire) - clamp), 0);
    }


}
