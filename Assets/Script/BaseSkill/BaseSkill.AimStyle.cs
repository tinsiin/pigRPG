using R3;
using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
public partial class BaseSkill
{
    /// <summary>
    /// 単体攻撃時のAimStyle保存用
    /// </summary>
    AimStyle _nowSingleAimStyle;
    /// <summary>
    /// 単体攻撃時のAimStyle設定
    /// </summary>
    public void SetSingleAimStyle(AimStyle style)
    {
        _nowSingleAimStyle = style;
    }
    /// <summary>
    /// 現在のムーブセットでのAimStyleを、現在の攻撃回数から取得する
    /// </summary>
    /// <returns></returns>
    public AimStyle NowAimStyle()
    {
        if(!NowConsecutiveATKFromTheSecondTimeOnward()||IsSingleHitAtk)return _nowSingleAimStyle;//初回攻撃なら単体保存した変数を返す または単回攻撃でも

        return NowMoveSetState.GetAtState(_atkCountUP - 1); 
    }
}
