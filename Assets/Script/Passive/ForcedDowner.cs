using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForcedDowner : BasePassive
{
    public override float AGIFixedValueEffect()
    {
        var a =  (_owner.TenDayValues.GetValueOrZero(TenDayAbility.HeatHaze) + 
                _owner.TenDayValues.GetValueOrZero(TenDayAbility.ElementFaithPower) + 
                _owner.TenDayValues.GetValueOrZero(TenDayAbility.SpringNap))/2;

        var result = Mathf.Max(a - _owner.TenDayValues.GetValueOrZero(TenDayAbility.HeavenAndEndWar) * 0.8f, 0);
        return result;
    }
}
