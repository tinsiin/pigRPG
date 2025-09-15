using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;

public abstract partial class BaseStates    
{

    //  ==============================================================================================================================
    //                                              スキル影響攻撃補正率
    //  ==============================================================================================================================


    /// <summary>
    /// スキルにより影響された攻撃補正率
    /// </summary>
    float _skillAttackModifier = 1f;
    
    /// <summary>
    /// 平準化する攻撃補正率
    /// </summary>
    float _BaseAttackModifier = 1f;
    /// <summary>
    /// 攻撃力でATKに掛けるスキルにより影響された攻撃補正率
    /// 落ち着きターン経過により減衰する。
    /// 実際の敵HPに対する減算処理のみに参照し、尚且つなるべく素に近いATK()の補正積み重ねの初期に掛けられる。
    /// </summary>
    float SkillAttackModifier
    {
        get
        {
            var calmDownModifier = CalmDownCount;//落ち着きカウントによる補正
            var calmDownModifierMax = CalmDownCountMax;
            if (calmDownModifier < 0)calmDownModifier = 0;//落ち着きカウントがマイナスにならないようにする

            // カウントダウンの進行度に応じて線形補間
            // カウントが減るほど _BaseAttackModifier に近づく
            float progress = 1.0f - (calmDownModifier / calmDownModifierMax);
            
            // 線形補間: _skillAttackModifier から _BaseAttackModifier へ徐々に変化
            return _skillAttackModifier + (_BaseAttackModifier - _skillAttackModifier) * progress;
        }
    }
    
}
