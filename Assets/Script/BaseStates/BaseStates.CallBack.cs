using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;

public abstract partial class BaseStates    
{
    //  ==============================================================================================================================
    //                                              ReactionSkill用
    //  ==============================================================================================================================

    /// <summary>
    /// 一人に対するスキル実行が終わった時のコールバック
    /// </summary>
    void OnAttackerOneSkillActEnd()
    {
        //バッファをクリア
        NowUseSkill.EraseBufferSkillType();//攻撃性質のバッファ
        NowUseSkill.EraseBufferSubEffects();//スキルの追加パッシブ付与リスト
        
    }
    /// <summary>
    /// 一人に対するスキル実行が始まった時のコールバック
    /// </summary>
    void OnAttackerOneSkillActStart(BaseStates UnderAtker)
    {
        ApplyExtraPassivesToSkill(UnderAtker);//攻撃者にスキルの追加パッシブ性質を適用
        NowUseSkill.CalcCradleSkillLevel(UnderAtker);//「攻撃者の」スキルのゆりかご計算
        NowUseSkill.RefilCanEraceCount();//除去スキル用の消せるカウント回数の補充
    }



}
