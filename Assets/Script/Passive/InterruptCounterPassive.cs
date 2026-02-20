using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InterruptCounterPassive : BasePassive
{
    //スキル命中率は常に100%だからプロパティの必要なし。


    /// <summary>
    /// 攻撃力に乗算する値（2倍）
    /// passivepowerに応じて、攻撃乗算値は増えていく
    /// </summary>
    public override float ATKPercentageModifier()
    { 
        var plusAtk = 0f;
        for(int i = 0 ; i<PassivePower;i++)
        {
            plusAtk += 0.26f;
        }
        return base.ATKPercentageModifier() + plusAtk;
        
    }
    
    /// <summary>
    /// 命中率に加算する値（+100）
    /// </summary>
    public override float EYEFixedValueEffect()
    {
        var plusEye = 0f;
        for(int i = 0 ; i<PassivePower;i++)
        {
        plusEye += 12;
        }
        return base.EYEFixedValueEffect() + plusEye;
    } 

    /// <summary>
    /// パッシブ効果を半減させる。AttackMultiplierは1.0が下限
    /// </summary>
    void DecayEffects()
    {
        // 攻撃力乗算値を半減（1.0未満にはならない）
        var curATK = ATKPercentageModifier();
        SetPercentageModifier(StatModifier.Atk, Mathf.Max(1.0f, curATK / 2f));
        
        // 命中率ボーナスを半減
        var curEye = EYEFixedValueEffect();
        SetFixedValue(StatModifier.Eye, Mathf.Max(0f, curEye / 2f));
    }

    public override void OnAfterAttack()
    {
        //攻撃者の割り込みカウンターパッシブの威力が下がる
        DecayEffects();//割り込みカウンターパッシブ効果半減　sameturnの連続攻撃で発揮する。(パッシブ自体は1ターンで終わる)
        base.OnAfterAttack();
    }

}
