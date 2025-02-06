using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InterruptCounterPassive : BasePassive
{
    //スキル命中率は常に100%だからプロパティの必要なし。
    [SerializeField]
    float _attackMultipiler = 2f;
    /// <summary>
    /// 攻撃力に乗算する値（2倍）
    /// passivepowerに応じて、攻撃乗算値は増えていく
    /// </summary>
    public float AttackMultiplier 
    { 
        get 
        {
            var plusAtk = 0f;
            for(int i = 0 ; i<PassivePower;i++)
            {
                plusAtk += 0.26f;
            }
            return _attackMultipiler + plusAtk;
        } 
    }
    
    [SerializeField]
    float _eyeBonus = 100f;
    /// <summary>
    /// 命中率に加算する値（+100）
    /// </summary>
    public float EyeBonus 
    {
        get 
        {
            var plusEye = 0f;
            for(int i = 0 ; i<PassivePower;i++)
            {
                plusEye += 12;
            }
            return _eyeBonus + plusEye;
        } 
    }

    /// <summary>
    /// パッシブ効果を半減させる。AttackMultiplierは1.0が下限
    /// </summary>
    public void DecayEffects()
    {
        // 攻撃力乗算値を半減（1.0未満にはならない）
        _attackMultipiler = Mathf.Max(1.0f, _attackMultipiler / 2f);
        
        // 命中率ボーナスを半減
        _eyeBonus /= 2f;
    }
}
