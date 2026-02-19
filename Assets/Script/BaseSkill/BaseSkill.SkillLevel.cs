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
    /// _levelIndex を経由せずに TLOA かどうかを判定するヘルパー。
    /// _levelIndex → _nowSkillLevel → IsTLOA → SpecialFlags → _levelIndex → ∞ の循環参照を回避する。
    /// </summary>
    protected bool IsTloaDirect
        => FixedSkillLevelData != null && FixedSkillLevelData.Count > 0
        && (FixedSkillLevelData[0].SpecialFlags & SkillSpecialFlag.TLOA) == SkillSpecialFlag.TLOA;

    /// <summary>
    /// スキルレベル
    /// 永続実行回数をTLOAスキルかそうでないかで割る数が変わる。
    /// </summary>
    protected virtual int _nowSkillLevel
    {
        get
        {
            if(IsTloaDirect)
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
    /// 現在のスキルレベルに対応するSkillLevelDataのインデックス。
    /// リスト上限でクランプ。空リストなら防御的に自動生成する（全プロパティがここ経由）。
    /// </summary>
    int _levelIndex
    {
        get
        {
            if (FixedSkillLevelData == null || FixedSkillLevelData.Count == 0)
                FixedSkillLevelData = new List<SkillLevelData> { new SkillLevelData() };
            return Math.Clamp(_nowSkillLevel, 0, FixedSkillLevelData.Count - 1);
        }
    }

    /// <summary>
    /// 固定されたスキルレベルデータ部分
    /// このリスト以降なら無限のデータ
    /// （BaseSkillDrawerが描画を管理するため[Header]不要）
    /// </summary>
    public List<SkillLevelData> FixedSkillLevelData = new();
    /// <summary>
    /// 無限に伸びる部分のスキルパワーの単位。
    /// 有限レベル設定の限界以降、ずっと用いられる。
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
/// スキルレベルに含まれるデータ。
/// Phase 2: 全プロパティの完全な値セットを各レベルが持つ。
/// 哨兵値なし、逆向き検索なし。値の読み取り = そのレベルのデータを直接読むだけ。
/// </summary>
[Serializable]
public class SkillLevelData
{
    // ─── ① 基本情報 ───
    public string SkillName = "";
    public SpiritualProperty SkillSpiritual;
    public PhysicalProperty SkillPhysical;
    public SkillImpression Impression;
    public MotionFlavorTag MotionFlavor;
    public SkillSpecialFlag SpecialFlags;

    // ─── ② スキル性質 ───
    public SkillType BaseSkillType;
    public SkillConsecutiveType ConsecutiveType;
    public SkillZoneTrait ZoneTrait;
    public AttackDistributionType DistributionType;
    public SerializableDictionary<SkillZoneTrait, float> PowerRangePercentageDictionary = new();
    public SerializableDictionary<SkillZoneTrait, float> HitRangePercentageDictionary = new();

    // ─── ③ 威力・命中・ダメージ ───
    public float SkillPower;
    public TenDayAbilityDictionary TenDayValues;
    public int SkillHitPer;
    public float MentalDamageRatio;
    public float DefAtk;
    public float[] PowerSpread;
    public bool Cantkill;

    // ─── ④ コスト・補正 ───
    public int RequiredNormalP;
    public SerializableDictionary<SpiritualProperty, int> RequiredAttrP = new();
    public float RequiredRemainingHPPercent;
    public float EvasionModifier = 1f;
    public float AttackModifier = 1f;
    public float AttackMentalHealPercent = 80f;
    public int SkillDidWaitCount;

    // ─── ⑤ 連撃・ストック・トリガー ───
    public float RandomConsecutivePer;
    public int DefaultStockCount = 1;
    public int StockPower = 1;
    public int StockForgetPower = 1;
    public int TriggerCountMax;
    public bool CanCancelTrigger = true;
    public int TriggerRollBackCount;

    // ─── ⑥ 前のめり ───
    public PhaseAggressiveSetting AggressiveOnExecute = new(true, false);
    public PhaseAggressiveSetting AggressiveOnTrigger = new(false, false);
    public PhaseAggressiveSetting AggressiveOnStock = new(false, false);

    // ─── ⑦ ムーブセット ───
    public List<MoveSet> A_MoveSet;
    public List<MoveSet> B_MoveSet;

    // ─── ⑧ ビジュアルエフェクト ───
    [EffectName("icon")] public string CasterEffectName;
    [EffectName("icon")] public string TargetEffectName;
    [EffectName("field")] public string FieldEffectName;

    // ─── ⑨ エフェクト・パッシブ付与 ───
    public List<int> SubEffects = new();
    public List<int> SubVitalLayers = new();
    public List<int> CanEraceEffectIDs = new();
    public int CanEraceEffectCount;
    public List<int> CanEraceVitalLayerIDs = new();
    public int CanEraceVitalLayerCount;
    public SkillPassiveTargetSelection TargetSelection;
    public List<SkillPassiveReactionCharaAndSkill> ReactionCharaAndSkillList = new();
    public int SkillPassiveEffectCount = 1;
    public SkillFilter SkillPassiveGibeSkillFilter = new();

    public SkillLevelData Clone()
    {
        var copy = new SkillLevelData
        {
            // ① 基本情報
            SkillName = this.SkillName,
            SkillSpiritual = this.SkillSpiritual,
            SkillPhysical = this.SkillPhysical,
            Impression = this.Impression,
            MotionFlavor = this.MotionFlavor,
            SpecialFlags = this.SpecialFlags,
            // ② スキル性質
            BaseSkillType = this.BaseSkillType,
            ConsecutiveType = this.ConsecutiveType,
            ZoneTrait = this.ZoneTrait,
            DistributionType = this.DistributionType,
            // ③ 威力・命中・ダメージ
            SkillPower = this.SkillPower,
            SkillHitPer = this.SkillHitPer,
            MentalDamageRatio = this.MentalDamageRatio,
            DefAtk = this.DefAtk,
            Cantkill = this.Cantkill,
            // ④ コスト・補正
            RequiredNormalP = this.RequiredNormalP,
            RequiredRemainingHPPercent = this.RequiredRemainingHPPercent,
            EvasionModifier = this.EvasionModifier,
            AttackModifier = this.AttackModifier,
            AttackMentalHealPercent = this.AttackMentalHealPercent,
            SkillDidWaitCount = this.SkillDidWaitCount,
            // ⑤ 連撃・ストック・トリガー
            RandomConsecutivePer = this.RandomConsecutivePer,
            DefaultStockCount = this.DefaultStockCount,
            StockPower = this.StockPower,
            StockForgetPower = this.StockForgetPower,
            TriggerCountMax = this.TriggerCountMax,
            CanCancelTrigger = this.CanCancelTrigger,
            TriggerRollBackCount = this.TriggerRollBackCount,
            // ⑧ ビジュアルエフェクト
            CasterEffectName = this.CasterEffectName,
            TargetEffectName = this.TargetEffectName,
            FieldEffectName = this.FieldEffectName,
            // ⑨ エフェクト・パッシブ付与
            CanEraceEffectCount = this.CanEraceEffectCount,
            CanEraceVitalLayerCount = this.CanEraceVitalLayerCount,
            TargetSelection = this.TargetSelection,
            SkillPassiveEffectCount = this.SkillPassiveEffectCount,
        };

        // TenDayValuesのディープコピー
        if (this.TenDayValues != null)
            copy.TenDayValues = new TenDayAbilityDictionary(this.TenDayValues);

        // PowerSpreadのディープコピー
        if (this.PowerSpread != null)
        {
            copy.PowerSpread = new float[this.PowerSpread.Length];
            Array.Copy(this.PowerSpread, copy.PowerSpread, this.PowerSpread.Length);
        }

        // MoveSetのディープコピー
        if (this.A_MoveSet != null)
        {
            copy.A_MoveSet = new List<MoveSet>();
            foreach (var moveSet in this.A_MoveSet)
                if (moveSet != null) copy.A_MoveSet.Add(moveSet.DeepCopy());
        }
        if (this.B_MoveSet != null)
        {
            copy.B_MoveSet = new List<MoveSet>();
            foreach (var moveSet in this.B_MoveSet)
                if (moveSet != null) copy.B_MoveSet.Add(moveSet.DeepCopy());
        }

        // RequiredAttrP
        if (this.RequiredAttrP != null)
        {
            copy.RequiredAttrP = new SerializableDictionary<SpiritualProperty, int>();
            foreach (var kv in this.RequiredAttrP)
                copy.RequiredAttrP.Add(kv.Key, kv.Value);
        }

        // SubEffects, SubVitalLayers, CanEraceEffectIDs, CanEraceVitalLayerIDs
        if (this.SubEffects != null)
            copy.SubEffects = new List<int>(this.SubEffects);
        if (this.SubVitalLayers != null)
            copy.SubVitalLayers = new List<int>(this.SubVitalLayers);
        if (this.CanEraceEffectIDs != null)
            copy.CanEraceEffectIDs = new List<int>(this.CanEraceEffectIDs);
        if (this.CanEraceVitalLayerIDs != null)
            copy.CanEraceVitalLayerIDs = new List<int>(this.CanEraceVitalLayerIDs);

        // Dictionaries
        if (this.PowerRangePercentageDictionary != null)
        {
            copy.PowerRangePercentageDictionary = new SerializableDictionary<SkillZoneTrait, float>();
            foreach (var kv in this.PowerRangePercentageDictionary)
                copy.PowerRangePercentageDictionary.Add(kv.Key, kv.Value);
        }
        if (this.HitRangePercentageDictionary != null)
        {
            copy.HitRangePercentageDictionary = new SerializableDictionary<SkillZoneTrait, float>();
            foreach (var kv in this.HitRangePercentageDictionary)
                copy.HitRangePercentageDictionary.Add(kv.Key, kv.Value);
        }

        // AggressiveSetting
        if (this.AggressiveOnExecute != null)
            copy.AggressiveOnExecute = this.AggressiveOnExecute.Clone();
        if (this.AggressiveOnTrigger != null)
            copy.AggressiveOnTrigger = this.AggressiveOnTrigger.Clone();
        if (this.AggressiveOnStock != null)
            copy.AggressiveOnStock = this.AggressiveOnStock.Clone();

        // ReactionCharaAndSkillList
        if (this.ReactionCharaAndSkillList != null)
        {
            copy.ReactionCharaAndSkillList = new List<SkillPassiveReactionCharaAndSkill>();
            foreach (var item in this.ReactionCharaAndSkillList)
                copy.ReactionCharaAndSkillList.Add(new SkillPassiveReactionCharaAndSkill
                    { CharaName = item.CharaName, SkillName = item.SkillName });
        }

        // SkillFilter（内部にmutableなList<>を持つためディープコピー）
        if (this.SkillPassiveGibeSkillFilter != null)
        {
            var src = this.SkillPassiveGibeSkillFilter;
            copy.SkillPassiveGibeSkillFilter = new SkillFilter
            {
                Impressions = new(src.Impressions),
                MotionFlavors = new(src.MotionFlavors),
                MentalAttrs = new(src.MentalAttrs),
                PhysicalAttrs = new(src.PhysicalAttrs),
                AttackTypes = new(src.AttackTypes),
                TenDayAbilities = new(src.TenDayAbilities),
                TenDayMode = src.TenDayMode,
                SkillTypes = new(src.SkillTypes),
                SkillTypeMode = src.SkillTypeMode,
                SpecialFlags = new(src.SpecialFlags),
                SpecialFlagMode = src.SpecialFlagMode,
            };
        }

        return copy;
    }
}
