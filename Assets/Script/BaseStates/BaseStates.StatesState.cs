using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;


//状態管理系ステータス
public abstract partial class BaseStates    
{

    //  ==============================================================================================================================
    //                                              回避率/攻撃率の落ち着き管理
    //  ==============================================================================================================================

    /// <summary>
    /// 落ち着きカウント
    /// 回避率や次攻撃者率　の平準化に用いられる
    /// </summary>
    int CalmDownCount = 0;
    /// <summary>
    /// 落ち着きカウントの最大値
    /// 影響された補正率がどの程度平準化されているかの計算に用いるために保存する。
    /// </summary>
    int CalmDownCountMax;
    /// <summary>
    /// 落ち着きカウントの最大値算出
    /// </summary>
    int CalmDownCountMaxRnd => RandomEx.Shared.NextInt(4, 8);
    /// <summary>
    /// 落ち着きカウントのカウント開始準備
    /// スキル回避率もセット
    /// </summary>
    public void CalmDownSet(float EvasionModifier = 1f, float AttackModifier = 1f)
    {
        CalmDownCountMax = CalmDownCountMaxRnd;//乱数から設定して、カウントダウンの最大値を設定
        CalmDownCount = CalmDownCountMax;//カウントダウンを最大値に設定
        CalmDownCount++;//NextTurnで即引かれるので調整　　落ち着き#カウント対処を参照して
        _skillEvasionModifier = EvasionModifier;//スキルにより影響された回避補正率をセット
        _skillAttackModifier = AttackModifier;//スキルにより影響された攻撃補正率をセット
    }
    /// <summary>
    /// 落ち着きカウントダウン
    /// </summary>
    void CalmDownCountDec()
    {
        CalmDownCount--;
    }
    /// <summary>
    /// 意図的に落ち着きカウントをゼロにすることにより、落ち着いた判定にする。
    /// </summary>
    void CalmDown()
    {
        CalmDownCount = 0;
    }

}
