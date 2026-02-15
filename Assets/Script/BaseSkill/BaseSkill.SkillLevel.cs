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
    [Header("レベルに対応する必須プロパティは、レベルデータに明示的に登録する以外で、単純にレベルに応じて伸びる無限単位が設定できます。\nこれは有限レベル設定の限界以降、ずっと用いられます。")]
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
    /// TLOAスキルをゆりかご計算する（カトレア案）。
    /// ゆりかごレベル = max(0, スキルレベル + 揺れ)
    /// 揺れ = 感情強度 × 使いこなし × 戦闘増幅
    /// actor = 攻撃者、underAtker = 被害者
    /// </summary>
    public void CalcCradleSkillLevel(BaseStates underAtker, BaseStates actor)
    {
        if(!IsTLOA) return;
        if(actor == null) { Debug.LogError($"CalcCradleSkillLevel: actorがnullです (skill: {SkillName})"); return; }

        // === 1. 調子（連続値 -1.0 ~ +1.0） ===
        const float ratio = 0.7f;
        const float center = 100f * ratio; // = 70
        const float moodSpread = 3.5f;
        float spiritualValue = BaseStates.GetOffensiveSpiritualModifier(actor, underAtker).GetValue(ratio);
        float mood = Mathf.Clamp((spiritualValue - center) / moodSpread, -1f, 1f);

        // パーティ属性補正（±0.3）
        mood = ModifyMoodContinuous(mood, actor.MyImpression, manager.MyGroup(actor).OurImpression);

        // === 2. 使いこなし（素の十日能力でスキル印象構造との一致度） ===
        float attackerMatchSum = 0f;
        foreach(var tenDay in TenDayValues(actor: actor))
            attackerMatchSum += actor.BaseTenDayValues.GetValueOrZero(tenDay.Key);
        float mastery = (TenDayValuesSum > 0f) ? attackerMatchSum / TenDayValuesSum : 1f;

        // === 3. 戦闘増幅（ライバハルの漸近的増幅） ===
        const float K = 30f;
        const float R = 2f;
        float rivahalFactor = actor.Rivahal / (actor.Rivahal + K);
        float battleAmp = 1f + rivahalFactor * R;

        // === 4. フリーハンド感情転換 ===
        float emotionIntensity = mood;
        var freehand = WeaponManager.Instance?.GetFreehandWeapon();
        if(freehand != null && actor.NowUseWeapon == freehand)
        {
            const float F = 0.75f;
            emotionIntensity = Mathf.Lerp(mood, Mathf.Abs(mood), F);
        }

        // === 5. 最終合成 ===
        float swing = emotionIntensity * mastery * battleAmp;
        _cradleSkillLevel = Mathf.Max(0, (int)(_nowSkillLevel + swing));
    }

    /// <summary>
    /// パーティ属性相性による調子の連続補正（±0.3）
    /// </summary>
    float ModifyMoodContinuous(float mood, SpiritualProperty impression, PartyProperty partyProperty)
    {
        if (impression == SpiritualProperty.none) return mood;
        float bonus = GetPartyCompatibilityBonus(impression, partyProperty);
        return Mathf.Clamp(mood + bonus, -1f, 1f);
    }

    /// <summary>
    /// 精神属性×パーティ属性の相性ボーナス（-0.3 / 0 / +0.3）
    /// </summary>
    float GetPartyCompatibilityBonus(SpiritualProperty impression, PartyProperty partyProperty)
    {
        switch (impression)
        {
            case SpiritualProperty.liminalwhitetile:
                if (partyProperty == PartyProperty.Flowerees) return -0.3f;
                if (partyProperty == PartyProperty.Odradeks) return 0.3f;
                break;
            case SpiritualProperty.kindergarden:
                if (partyProperty == PartyProperty.Flowerees) return -0.3f;
                if (partyProperty == PartyProperty.TrashGroup) return 0.3f;
                break;
            case SpiritualProperty.sacrifaith:
                if (partyProperty == PartyProperty.Flowerees || partyProperty == PartyProperty.HolyGroup) return 0.3f;
                break;
            case SpiritualProperty.cquiest:
                if (partyProperty == PartyProperty.TrashGroup) return -0.3f;
                if (partyProperty == PartyProperty.HolyGroup || partyProperty == PartyProperty.Odradeks) return 0.3f;
                break;
            case SpiritualProperty.devil:
                if (partyProperty == PartyProperty.Odradeks) return -0.3f;
                if (partyProperty == PartyProperty.Flowerees || partyProperty == PartyProperty.TrashGroup) return 0.3f;
                break;
            case SpiritualProperty.doremis:
                if (partyProperty == PartyProperty.Flowerees || partyProperty == PartyProperty.HolyGroup) return 0.3f;
                break;
            case SpiritualProperty.godtier:
                if (partyProperty == PartyProperty.TrashGroup) return 0.3f;
                break;
            case SpiritualProperty.baledrival:
                if (partyProperty == PartyProperty.Odradeks) return -0.3f;
                if (partyProperty == PartyProperty.TrashGroup || partyProperty == PartyProperty.HolyGroup) return 0.3f;
                break;
            case SpiritualProperty.pysco:
                if (partyProperty == PartyProperty.Flowerees || partyProperty == PartyProperty.MelaneGroup) return 0.3f;
                break;
        }
        return 0f;
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

    [Header("オプションの値 それぞれnull,-1なら 通常の設定値が用いられる。\nこれらの値を「レベル」の構造に合わせて変動するようにしたい場合、設定すると通常の設定値を上書きする形で\nこれらのプロパティがレベルに対応し変動する値になる。\nレベルオプションは基本的に通常の設定値と同じ設定方法です。")]
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
    [Header("nullなら通常の設定値が用いられる。オプションムーブセット設定")]
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
            copy.OptionA_MoveSet = new List<MoveSet>();
            foreach(var moveSet in this.OptionA_MoveSet)
            {
                if(moveSet != null)
                    copy.OptionA_MoveSet.Add(moveSet.DeepCopy());
            }
        }
        if(this.OptionB_MoveSet != null)
        {
            copy.OptionB_MoveSet = new List<MoveSet>();
            foreach(var moveSet in this.OptionB_MoveSet)
            {
                if(moveSet != null)
                    copy.OptionB_MoveSet.Add(moveSet.DeepCopy());
            }
        }

        return copy;
    }
}
