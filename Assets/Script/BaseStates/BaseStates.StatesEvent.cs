using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;

//キャラクターステータスの複合イベント
//余りにも複合前提の物はここに置かれる。
public abstract partial class BaseStates
{
    //  ==============================================================================================================================
    //                                              精神HPによるポインントの自然回復処理
    //  ==============================================================================================================================

    /// <summary>
    /// 精神HPに応じてポイントを自然回復する関数。
    /// 回復量は精神Hp現在値を割った数とそれの実HP最大値との割合によるカット
    /// </summary>
    protected void MentalNaturalRecovelyPont()
    {
         // 精神HPを定数で割り回復量に変換する
        var baseRecovelyP = (int)MentalHP / PlayersStates.Instance.MentalHP_TO_P_Recovely_CONVERSION_FACTOR;
        
        // 精神HPと実HP最大値との割合
        var mentalToMaxHPRatio = MentalHP / MaxHP;
        
        var RecovelyValue = baseRecovelyP * mentalToMaxHPRatio;//回復量
        
        if(RecovelyValue < 0)RecovelyValue = 0;
        // ポイント回復
        P += (int)RecovelyValue;
    }
    /// <summary>
    /// 精神HPによるポイント自然回復のカウントアップ用変数
    /// </summary>
    int _mentalPointRecoveryCountUp;
    /// <summary>
    /// 精神HPによるポイント自然回復の最大カウント = 回復頻度
    /// </summary>
    int MentalPointRecovelyMaxCount 
    {
        get
        {
            // テント空洞と夜暗黒の基本値計算
            var tentVoidValue = TenDayValues(false).GetValueOrZero(TenDayAbility.TentVoid) * 2;
            var nightDarknessValue = TenDayValues(false).GetValueOrZero(TenDayAbility.NightDarkness) * 1.6f;
            
            // ミザとスマイラー、元素信仰力の減算値計算
            var mizaValue = TenDayValues(false).GetValueOrZero(TenDayAbility.Miza) / 4f * TenDayValues(false).GetValueOrZero(TenDayAbility.Smiler);
            var elementFaithValue = TenDayValues(false).GetValueOrZero(TenDayAbility.ElementFaithPower) * 0.7f;
        
            // 最終計算
            var finalValue = (int)(tentVoidValue + nightDarknessValue - (mizaValue + elementFaithValue));
            if(finalValue < 2) finalValue = 2;//最低回復頻度ターンは2
            return finalValue;
        }
    }
    /// <summary>
    /// 精神HPによるポイント自然回復の判定と処理
    /// </summary>
    void TryMentalPointRecovery()
    {
        _mentalPointRecoveryCountUp++;
        if(_mentalPointRecoveryCountUp >= MentalPointRecovelyMaxCount)
        {
            _mentalPointRecoveryCountUp = 0;
            MentalNaturalRecovelyPont();
        }
    }


}
