using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForcedDowner : BasePassive
{
    public override float AGIFixedValueEffect()
    {
        var a =  (_owner.TenDayValuesBase().GetValueOrZero(TenDayAbility.HeatHaze) + 
                _owner.TenDayValuesBase().GetValueOrZero(TenDayAbility.ElementFaithPower) + 
                _owner.TenDayValuesBase().GetValueOrZero(TenDayAbility.SpringNap))/2;

        var result = Mathf.Max(a - _owner.TenDayValuesBase().GetValueOrZero(TenDayAbility.HeavenAndEndWar) * 0.8f, 0);
        return result;
    }
    public override void OnApply(BaseStates user, BaseStates grantor)
    {
        base.OnApply(user, grantor);

        //ベールの精神属性ならば、
        if(_owner.MyImpression == SpiritualProperty.BaleDrival)
        {
            //ランダムな十日能力が下降する。
            var randomTenDayAbility = _owner.GetRandomTenDayAbility();
            //7%下降
            _owner.TenDayDecreaseByPercent(randomTenDayAbility, 0.07f);
            
        }
        
    }
}
