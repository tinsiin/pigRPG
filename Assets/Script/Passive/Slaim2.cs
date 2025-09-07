using R3.Collections;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NRandom;
using NRandom.Linq;

/// <summary>
/// スレームII- ロゼロ
/// </summary>
public class Slaim2 : BasePassive
{
    /// <summary>
    /// スレーム2は敵の全体攻撃時に発生する。
    /// </summary>
    public override void OnBeforeAllAlliesDamage(BaseStates Atker,ref UnderActersEntryList underActers)
    {
        if(!TenDayCalc(Atker,_owner))return;//十日能力計算で失敗したら終わり。

        if(underActers.charas.Count == 1)return;//そもそも一人しかいないのなら。

        //十日能力計算でかく乱量フルかどうか
        if(!IsFullShuffleByTenDay(Atker,_owner))//フルでないのなら
        {
            //実行者だけの順番がランダムに移動する。
            var currentIndex = underActers.charas.IndexOf(_owner);//実行者のインデックスを取得
            // ランダムなインデックスを生成（自分自身以外のインデックスを選ぶ）
            int targetIndex;
            do {
                targetIndex = NRandom.Shared.NextInt(0, underActers.charas.Count);//Countは+1されているので
            } while (targetIndex == currentIndex && underActers.charas.Count > 1); // 自分自身以外のインデックスを選ぶ

            // 要素を入れ替え
            (underActers.charas[currentIndex], underActers.charas[targetIndex]) = 
                (underActers.charas[targetIndex], underActers.charas[currentIndex]);
            
        }else//フルの場合は
        {
            //全員の順番がランダムに移動する。
            underActers.charas.Shuffle();
        }
        
    }


    /// <summary>
    /// 十日能力比較由来のかく乱量の決定
    /// </summary>
    bool IsFullShuffleByTenDay(BaseStates Attacker,BaseStates Slaimer)
    {
        var eneRain = Attacker.TenDayValues(false).GetValueOrZero(TenDayAbility.Rain);
        var SlaimerSort = Slaimer.TenDayValues(false).GetValueOrZero(TenDayAbility.Sort);
        
        return SlaimerSort > eneRain;//ソートが攻撃者に勝ってたら、全てかく乱できる。
    }

    /// <summary>
    /// 十日能力の発生計算
    /// </summary>
    bool TenDayCalc(BaseStates Attacker,BaseStates Slaimer)
    {
        //まず水雷神経で相手を凌駕しすぎているのなら
        if(Slaimer.TenDayValues(false).GetValueOrZero(TenDayAbility.WaterThunderNerve) > Attacker.TenDayValuesSum(true)/2)
        {
            return true;
        }

        var RainSmile = Slaimer.TenDayValues(false).GetValueOrZero(TenDayAbility.Raincoat) + Slaimer.TenDayValues(false).GetValueOrZero(TenDayAbility.Smiler);
        var KagerouTarai = Slaimer.TenDayValues(false).GetValueOrZero(TenDayAbility.HeatHaze) + Slaimer.TenDayValues(false).GetValueOrZero(TenDayAbility.Taraiton);
        var FaceHandDoku = Slaimer.TenDayValues(false).GetValueOrZero(TenDayAbility.FaceToHand) + Slaimer.TenDayValues(false).GetValueOrZero(TenDayAbility.dokumamusi);

        var eneRain = Attacker.TenDayValues(false).GetValueOrZero(TenDayAbility.Rain);
        //各通常比較、実行者側が多ければ発生ということで
        if(RainSmile > eneRain)return true;
        if(KagerouTarai > eneRain)return true;
        if(FaceHandDoku > eneRain)return true;


        return false;
    }
}
