using NRandom;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Upper : BasePassive//アッパー  精神HPの上の乖離の基本効果など
{
    public override float ATKFixedValueEffect()
    {
        return _owner.TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire);
    }

    public override float AGIFixedValueEffect()
    {
        var clamp = Mathf.Max(_owner.TenDayValues(false).GetValueOrZero(TenDayAbility.WaterThunderNerve) / 2.5f - _owner.TenDayValues(false).GetValueOrZero(TenDayAbility.dokumamusi), 0);
        return Mathf.Min(-(_owner.TenDayValues(false).GetValueOrZero(TenDayAbility.BlazingFire) - clamp), 0);
    }

    public override float DEFFixedValueEffect()
    {
        var clamp = _owner.TenDayValues(false).GetValueOrZero(TenDayAbility.NightInkKnight) / 5f;
        return Mathf.Min(-(_owner.TenDayValues(false).GetValueOrZero(TenDayAbility.BlazingFire) - clamp), 0);
    }
    public override void OnApply(BaseStates user, BaseStates grantor)
    {
        base.OnApply(user, grantor);

        //キンダーの精神属性ならば、
        if(_owner.MyImpression == SpiritualProperty.kindergarden)
        {
            //ランダムな十日能力が上昇する。
            var randomTenDayAbility = _owner.GetRandomTenDayAbility();
            //3~11%上昇
            var randomPercent = NRandom.Shared.NextSingle(0.03f, 0.11f);
            _owner.TenDayGrowByPercentOfCurrent(randomTenDayAbility, randomPercent);
            
        }
        
    }


}
