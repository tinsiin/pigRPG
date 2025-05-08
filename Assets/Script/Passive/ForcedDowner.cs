using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForcedDowner : BasePassive
{
    public override float AGIFixedValueEffect()
    {
        var a =  (_owner.TenDayValues(false).GetValueOrZero(TenDayAbility.HeatHaze) + 
                _owner.TenDayValues(false).GetValueOrZero(TenDayAbility.ElementFaithPower) + 
                _owner.TenDayValues(false).GetValueOrZero(TenDayAbility.SpringNap))/2;

        var result = Mathf.Max(a - _owner.TenDayValues(false).GetValueOrZero(TenDayAbility.HeavenAndEndWar) * 0.8f, 0);
        return result;
    }
}
