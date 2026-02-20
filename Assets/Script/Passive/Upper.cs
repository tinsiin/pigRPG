using RandomExtensions;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Upper : BasePassive//アッパー  精神HPの上の乖離の基本効果など
{
    public override float ATKFixedValueEffect()
    {
        return _owner.TenDayValuesBase().GetValueOrZero(TenDayAbility.BlazingFire);
    }

    public override float AGIFixedValueEffect()
    {
        var clamp = Mathf.Max(_owner.TenDayValuesBase().GetValueOrZero(TenDayAbility.WaterThunderNerve) / 2.5f - _owner.TenDayValuesBase().GetValueOrZero(TenDayAbility.Dokumamusi), 0);
        return Mathf.Min(-(_owner.TenDayValuesBase().GetValueOrZero(TenDayAbility.BlazingFire) - clamp), 0);
    }

    public override float DEFFixedValueEffect()
    {
        var clamp = _owner.TenDayValuesBase().GetValueOrZero(TenDayAbility.NightInkKnight) / 5f;
        return Mathf.Min(-(_owner.TenDayValuesBase().GetValueOrZero(TenDayAbility.BlazingFire) - clamp), 0);
    }
    public override void OnApply(BaseStates user, BaseStates grantor)
    {
        base.OnApply(user, grantor);

        //キンダーの精神属性ならば、
        if(_owner.MyImpression == SpiritualProperty.Kindergarten)
        {
            //ランダムな十日能力が上昇する。
            var randomTenDayAbility = _owner.GetRandomTenDayAbility();
            //3~11%上昇
            var randomPercent = RandomEx.Shared.NextFloat(0.03f, 0.11f);
            _owner.TenDayGrowByPercentOfCurrent(randomTenDayAbility, randomPercent);
            
        }
        
    }


}
