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

    //  ==============================================================================================================================
    //                                              レベルアップ定数
    //  ==============================================================================================================================

    /// <summary>
    /// TLOAスキルのレベルアップに必要な使用回数
    /// </summary>
    protected const int TLOA_LEVEL_DIVIDER = 120;
    /// <summary>
    /// 非TLOAスキルのレベルアップに必要な使用回数
    /// </summary>
    protected const int NOT_TLOA_LEVEL_DIVIDER = 50;

    //  ==============================================================================================================================
    //                                              スキルレベル
    //  ==============================================================================================================================

    /// <summary>
    /// スキルレベル
    /// 永続実行回数をTLOAスキルかそうでないかで割る数が変わる。
    /// </summary>
    protected virtual int _nowSkillLevel
    {
        get
        {
            if(IsTLOA)
            {
                return _recordDoCount / TLOA_LEVEL_DIVIDER;
            }
            else
            {
                return _recordDoCount / NOT_TLOA_LEVEL_DIVIDER;
            }
        }
    }
    /// <summary>
    /// 固定されたスキルレベルデータ部分
    /// このリスト以降なら無限のデータ
    /// </summary>
    [Header("固定されたスキルレベルデータ部分は必ず一つのデータを設定する必要があります。\n(スキルはレベルが上がるのが普通なのでそういう設計にしてるから、デバックでも必ず一つは必要)")]
    public List<SkillLevelData> FixedSkillLevelData = new();
    /// <summary>
    /// 無限に伸びる部分のスキルパワーの単位。
    /// </summary>
    [SerializeField]
    float _infiniteSkillPowerUnit;
    /// <summary>
    /// 無限に伸びる部分の印象構造(全て)の単位。
    /// </summary>
    [SerializeField]
    float _infiniteSkillTenDaysUnit;


    //  ==============================================================================================================================
    //                                              TLOAスキルのレベルはゆりかご計算される。
    //  ==============================================================================================================================


    /// <summary>
    /// ゆりかご計算されたスキルレベル
    /// </summary>
    int _cradleSkillLevel = -1;
    /// <summary>
    /// TLOAスキルをゆりかご計算する。
    /// Doer = 攻撃者だよ　スキルは攻撃だし、攻撃者が呼び出す関数だし
    /// </summary>
    public void CalcCradleSkillLevel(BaseStates underAtker)
    {
        if(!IsTLOA) return;//TLOAスキルでなければ計算しない

        //まずゆりかごのルート位置から決める
        //強さ　　　　　　　　　//TLOAも気持ちの問題なのでスキルの十日能力補正は当然かかる(TLOAスキルを参照)
        var TendayAdvantage = Doer.TenDayValuesSum(true)/underAtker.TenDayValuesSum(false);
        //実効ライバハル　= ライバハル　÷　敵に対する強さ　
        //相手より弱いほどライバハルが増えたら変なので、1クランプする。
        var EffectiveRivahal = Doer.Rivahal / Mathf.Max(1, TendayAdvantage);
        //ルート位置補正計算式についてはobsidianのスキルレベルを参照して
        var skillLevelRandomDivisorMax = EffectiveRivahal /2;
        if(skillLevelRandomDivisorMax < 1) skillLevelRandomDivisorMax = 1;
        //ルート位置算出
        var root = EffectiveRivahal + _nowSkillLevel / RandomEx.Shared.NextFloat(1, skillLevelRandomDivisorMax);

        //次に調子の範囲を決める
        //まず精神補正値による固定範囲
        var ratio = 0.7f;
        var BaseMoodRange = GetEstablishSpiritualMoodRange(BaseStates.GetOffensiveSpiritualModifier(Doer,underAtker).GetValue(ratio),ratio);
        //次にパーティ属性と自分の属性相性による調子の範囲の補正
        var MoodRange = ModifyMoodByAttributeCompatibility(BaseMoodRange,Doer.MyImpression,manager.MyGroup(Doer).OurImpression);

        //今度は調子の範囲で使う上下レートを決める
        //スキルの印象構造と同じ攻撃者の(Doer)十日能力値の総量を取得
        var AtkerTenDaySumMatchingSkill  = 0f;
        foreach(var tenDay in TenDayValues())//スキルの印象構造で回す
        {
            //蓄積変数にスキルの印象構造と同じ攻撃者Doerの十日能力値を足していくｐ！
            AtkerTenDaySumMatchingSkill += Doer.TenDayValues(true).GetValueOrZero(tenDay.Key);
        }
        //上下レート算出　印象構造対応Doerの十日能力値　÷　スキルの十日能力値の総量　「どのくらいスキルを使いこなしているか」が指標
        var MoodRangeRate = AtkerTenDaySumMatchingSkill / TenDayValuesSum;
        MoodRangeRate = Mathf.Max(MoodRangeRate - 2,RandomEx.Shared.NextFloat(2));//ランダム　0~2が上下レートの最低値


        //ルート位置から、調子の範囲によって上下レートを元にずらす。
        _cradleSkillLevel = (int)(root + MoodRange * MoodRangeRate);
    }
    /// <summary>
    /// 調子の範囲を決める
    /// 後で上下レートと掛けるので、-1,0,1の範囲の、下がるか上がるかそのままかで返す。
    /// これは精神補正に掛ける補正率によって精神補正による分布範囲の前提が崩れるので、
    /// 精神補正に掛ける率と同じ値を範囲指定に掛ける。
    /// </summary>
    int GetEstablishSpiritualMoodRange(float value,float ratio)
    {
        // 精神補正値に基づいて調子の範囲を決定
        if (value <= 95 *ratio)
        {
            return -1;
        }
        else if (value <= 100 *ratio)
        {
            return 0;
        }
        else
        {
            return 1; //78以上
        }    
    }
    /// <summary>
    /// 調子の範囲を追加補正
    /// </summary>
    int ModifyMoodByAttributeCompatibility(int BaseValue, SpiritualProperty MyImpression,PartyProperty partyProperty)
    {
        // キャラクターの精神属性がnoneの場合は変化なし
        if (MyImpression == SpiritualProperty.none)
            return BaseValue;

        // 相性表に基づいて調整
        bool increase = false;
        bool decrease = false;
        
        switch (Doer.MyImpression)
        {
            case SpiritualProperty.liminalwhitetile:
                if (partyProperty == PartyProperty.Flowerees)
                    decrease = true;
                else if (partyProperty == PartyProperty.Odradeks)
                    increase = true;
                break;
                
            case SpiritualProperty.kindergarden:
                if (partyProperty == PartyProperty.Flowerees)
                    decrease = true;
                else if (partyProperty == PartyProperty.TrashGroup)
                    increase = true;
                break;
                
            case SpiritualProperty.sacrifaith:
                if (partyProperty == PartyProperty.Flowerees || partyProperty == PartyProperty.HolyGroup)
                    increase = true;
                break;
                
            case SpiritualProperty.cquiest:
                if (partyProperty == PartyProperty.TrashGroup)
                    decrease = true;
                else if (partyProperty == PartyProperty.HolyGroup || partyProperty == PartyProperty.Odradeks)
                    increase = true;
                break;
                
            case SpiritualProperty.devil:
                if (partyProperty == PartyProperty.Odradeks)
                    decrease = true;
                else if (partyProperty == PartyProperty.Flowerees || partyProperty == PartyProperty.TrashGroup)
                    increase = true;
                break;
                
            case SpiritualProperty.doremis:
                if (partyProperty == PartyProperty.Flowerees || partyProperty == PartyProperty.HolyGroup)
                    increase = true;
                break;
                
            case SpiritualProperty.godtier:
                if (partyProperty == PartyProperty.TrashGroup)
                    increase = true;
                break;
                
            case SpiritualProperty.baledrival:
                if (partyProperty == PartyProperty.Odradeks)
                    decrease = true;
                else if (partyProperty == PartyProperty.TrashGroup || partyProperty == PartyProperty.HolyGroup)
                    increase = true;
                break;
                
            case SpiritualProperty.pysco:
                if (partyProperty == PartyProperty.Flowerees || partyProperty == PartyProperty.MelaneGroup)
                    increase = true;
                break;
        }
        
        // 上昇または下降の適用（-1から1の間に制限）
        if (increase)
            return Math.Min(1, BaseValue + 1);
        else if (decrease)
            return Math.Max(-1, BaseValue - 1);
        else
            return BaseValue;
    }



}





/// <summary>
/// スキルレベルに含まれるデータ
/// </summary>
[Serializable]
public class SkillLevelData
{
    [Header("必須の値")]
    public TenDayAbilityDictionary TenDayValues;
    public float SkillPower;

    [Header("オプションの値")]
    /// <summary>
    /// スキルレベルによる精神攻撃率 (オプション)
    /// -1なら参照されない
    /// </summary>
    public float OptionMentalDamageRatio = -1;

    /// <summary>
    /// スキルレベルによる分散割合 (オプション)
    /// nullなら参照されない
    /// </summary>
    public float[] OptionPowerSpread = null;
    [Header("スキルレベル命中補正は-1にすれば単一の設定値が用いられる。0のままだとこれが参照されて命中しない")]
    /// <summary>
    /// スキルレベルによる命中補正 (オプション)
    /// -1なら参照されない
    /// </summary>
    public int OptionSkillHitPer = -1;

    public List<MoveSet> OptionA_MoveSet = null;
    public List<MoveSet> OptionB_MoveSet = null;

    public SkillLevelData Clone()
    {
        // 基本データをコピー
        var copy = new SkillLevelData
        {
            SkillPower = this.SkillPower,
            OptionMentalDamageRatio = this.OptionMentalDamageRatio,
            OptionSkillHitPer = this.OptionSkillHitPer
        };
        
        // TenDayValuesのディープコピー
        if (this.TenDayValues != null)
        {
            copy.TenDayValues = new TenDayAbilityDictionary(this.TenDayValues);
        }
        
        // PowerSpreadのディープコピー
        if (this.OptionPowerSpread != null)
        {
            copy.OptionPowerSpread = new float[this.OptionPowerSpread.Length];
            Array.Copy(this.OptionPowerSpread, copy.OptionPowerSpread, this.OptionPowerSpread.Length);
        }

        //MoveSetのディープコピー
        if(this.OptionA_MoveSet != null)
        {
            foreach(var moveSet in this.OptionA_MoveSet)
            {
                copy.OptionA_MoveSet.Add(moveSet.DeepCopy());
            }
        }
        
        return copy;
    }
}
