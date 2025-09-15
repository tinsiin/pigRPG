using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;

public abstract partial class BaseStates    
{
    //  ==============================================================================================================================
    //                                              関連の関数
    //  ==============================================================================================================================

    /// <summary>
    /// 命中凌駕の判定関数　引数倍命中が回避を凌駕してるのなら、スキル命中率に影響を与える
    /// </summary>
    private float AccuracySupremacy(float atkerEye, float undAtkerAgi, float multiplierThreshold = 2.5f)
    {
        var supremacyMargin = 0f;
        var modifyAgi = undAtkerAgi * multiplierThreshold;//補正されたagi
        if (atkerEye >= modifyAgi)//攻撃者のEYEが特定の倍被害者のAGIを上回っているならば、
        {
            supremacyMargin = (atkerEye - modifyAgi) / 2;//命中が引数倍された回避を超した分　÷　2
        }
        return supremacyMargin;
    }




}