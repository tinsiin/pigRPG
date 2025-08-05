using R3;
using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>
/// スキル特殊判別性質
/// </summary>
[Flags]
public enum SkillSpecialFlag
{
    TLOA = 1 << 0,
    Magic = 1 << 1,
    Blade = 1 << 2,
}

/// <summary>
/// スキルの実行性質
/// </summary>
[Flags]
public enum SkillType
{
    Attack = 1 << 0,
    Heal = 1 << 1,
    addPassive = 1 << 2,
    RemovePassive = 1 << 3,
    DeathHeal = 1 << 4,
    AddVitalLayer =1 << 5,
    RemoveVitalLayer =1 << 6,
    MentalHeal = 1 << 7,
    Manual1_GoodHitCalc = 1 << 8,
    Manual1_BadHitCalc = 1 << 9,
    /// <summary>
    /// スキルパッシブを付与する
    /// </summary>
    addSkillPassive = 1 << 10,
    /// <summary>
    /// 良いスキルパッシブを除去する
    /// </summary>
    removeGoodSkillPassive = 1 << 11,
    /// <summary>
    /// 悪いスキルパッシブを除去する
    /// </summary>
    removeBadSkillPassive = 1 << 12,
}
/// <summary>
/// スキル範囲の性質
/// </summary>
[Flags]
public enum SkillZoneTrait
{
    /// <summary>
    /// 選択可能な単体対象
    /// </summary>
    CanPerfectSelectSingleTarget = 1 << 0,
    /// <summary>
    /// 前のめりか後衛(内ランダム)で選択可能な単体対象
    /// </summary>
    CanSelectSingleTarget = 1 << 1,
    /// <summary>
    /// ランダムの単体対象
    /// </summary>
    RandomSingleTarget = 1 << 2,

    /// <summary>
    /// 前のめりか後衛で選択可能な範囲対象
    /// </summary>
    CanSelectMultiTarget = 1 << 3,
    /// <summary>
    /// ランダムで選ばれる前のめりか後衛の範囲対象
    /// この際唐突性があるためテラーズヒットは発生しない
    /// </summary>
    RandomSelectMultiTarget = 1 << 4,
    /// <summary>
    /// ランダムな範囲、つまり一人か二人か三人がランダム
    /// </summary>
    RandomMultiTarget = 1 << 5,
    /// <summary>
    /// 全範囲攻撃
    /// </summary>
    AllTarget = 1 << 6,

    /// <summary>
    /// 範囲ランダム　全シチュエーション
    /// </summary>
    RandomTargetALLSituation = 1 << 7,
    /// <summary>
    /// 範囲ランダム   前のめり,後衛単位or単体ランダム
    /// </summary>
    RandomTargetMultiOrSingle = 1 << 8,
    /// <summary>
    /// 範囲ランダム　全体or単体ランダム
    /// </summary>
    RandomTargetALLorSingle = 1 << 9,
    /// <summary>
    /// 範囲ランダム　全体or前のめり,後衛単位
    /// </summary>
    RandomTargetALLorMulti = 1 << 10,

    /// <summary>
    /// RandomRangeを省いた要素のうちが選択可能かどうか
    /// </summary>
    CanSelectRange = 1 << 11,
    /// <summary>
    /// 死を選べるまたは対象選別の範囲に入るかどうか
    /// </summary>
    CanSelectDeath = 1 << 12,
    /// <summary>
    /// 対立関係のグループ相手だけではなく、自陣をも選べるかどうか
    /// </summary>
    CanSelectAlly = 1 << 13,
    /// <summary>
    /// 基本的には前のめりしか選べない。 前のめりがいない場合
    ///ランダムな事故が起きる感じで、
    ///その事故は意志により選択は不可能
    ///canSelectAlly等は無効
    /// </summary>
    ControlByThisSituation = 1 << 14,
    /// <summary>
    /// 範囲ランダム判定用
    /// </summary>
    RandomRange = 1 << 15,
    /// <summary>
    /// 自分自身を対象にすることができるかどうか
    /// </summary>
    CanSelectMyself = 1 << 16,
    /// <summary>
    /// 自陣のみ選べる
    /// </summary>
    SelectOnlyAlly = 1 << 17,
    /// <summary>
    /// 自分自身の為のスキル
    /// </summary>
    SelfSkill = 1 << 18,

}
/// <summary>
/// スキルの実行順序的性質
/// </summary>
[Flags]
public enum SkillConsecutiveType
{
    /// <summary>
    /// 毎コマンドZoneTraitに従って対象者の選択可能　ランダムだったらそれに従う感じ
    /// </summary>
    CanOprate = 1 << 0,

    /// <summary>CanOprateの対局的なもの、つまり最初しかZoneTraitでできることを選べない
    /// 要は普通の連続攻撃の挙動です。
    CantOprate = 1 << 1, 

    /// <summary>
    /// ターンをまたいだ連続的攻撃　連続攻撃回数分だけ単体攻撃が無理やり進む感じ
    /// </summary>
    FreezeConsecutive = 1 << 2,

    /// <summary>
    /// 同一ターンで連続攻撃が行われるかどうか
    /// </summary>
    SameTurnConsecutive = 1 << 3,

    /// <summary>
    /// ランダムな百分率でスキル実行が連続されるかどうか
    /// </summary>
    RandomPercentConsecutive = 1 << 4, 
    /// <summary>
    /// _atkCountの値に応じて連続攻撃が行われるかどうか
    /// </summary>
    FixedConsecutive = 1 << 5,

    /// <summary>
    /// スキル保存性質　
    /// **意図的に実行とは別に攻撃保存を選べて**、
    ///その攻撃保存を**選んだ分だけ連続攻撃回数として発動**
    ///Randomな場合はパーセント補正が変わる？
    /// </summary>
    Stockpile = 1 << 6,

}
/// <summary>
/// 対象を選別する意思状態の列挙体 各手順で選ばれた末の結果なので、単一の結果のみだからビット演算とかでない
/// この列挙体は**状況内で選別するシチュエーション的な変数**という側面が強い。
/// 要はbattleManagerでそれらの状況に応じてキャラを選別するって感じ
/// </summary>
public enum DirectedWill
{
    /// <summary>
    /// 前のめり
    /// </summary>
    InstantVanguard,
    /// <summary>
    /// 後衛または前のめりいない集団
    /// </summary>
    BacklineOrAny,
    /// <summary>
    /// 単一ユニット
    /// </summary>
    One,
}
/// <summary>
/// "範囲"攻撃の分散性質を表す列挙体 予め設定された3～６つの割合をどう扱うかの指定
/// powerSpreadの配列のサイズで分散するかどうかを判定する。(つまりNoneみたいな値はない。)
/// </summary>
public enum AttackDistributionType
{
    /// <summary>
    /// 完全ランダムでいる分だけ割り当てる
    /// </summary>
    Random,
    /// <summary>
    /// 前のめり状態(敵味方問わず)のキャラが最初に回されるランダム分散　
    /// 放射系統のビーム的な
    /// </summary>
    Beam,
    /// <summary>
    /// 2までの値だけを利用して、前衛と後衛への割合。　
    /// 前衛が以内なら後衛単位　おそらく2が使われる
    /// </summary>
    Explosion,
    /// <summary>
    /// 投げる。　つまり敵味方問わず前のめり状態のが一番後ろに回される
    /// </summary>
    Throw,
}
/// <summary>
/// キャラクターを表現するのとスキルのタグを表すスキルの列挙体
/// </summary>
public enum SkillImpression
{
    /// <summary>
    /// TLOAPHANTOM
    /// </summary>
    TLOA_PHANTOM,
    /// <summary>
    /// 半壊TLOA
    /// </summary>
    HalfBreak_TLOA,
    /// <summary>
    /// アサルト機械
    /// </summary>
    Assault_Machine,
    /// <summary>
    /// サブアサルト機械　
    /// </summary>
    SubAssult_Machine,
}

/// <summary>
/// スキルレベルに含まれるデータ
/// </summary>
[Serializable]
public class SkillLevelData
{
    public TenDayAbilityDictionary TenDayValues;
    public float SkillPower;

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

[Serializable]
public class BaseSkill
{
    protected BattleManager manager => Walking.bm;
    /// <summary>
    ///     スキルの精神属性
    /// </summary>
    public SpiritualProperty SkillSpiritual;
    /// <summary>
    /// スキル印象　タグや慣れ補正で使う
    /// </summary>
    public SkillImpression Impression;
    /// <summary>
    /// 動作的雰囲気　スキル印象の裏バージョン
    /// </summary>
    public MotionFlavorTag MotionFlavor;// 空なら「唯一無二」のユニークスキルであるということ。




    /// <summary>
    /// スキル性質を持ってるかどうか
    /// 複数指定した場合は全て当てはまってるかどうかで判断
    /// </summary>
    public bool HasType(params SkillType[] skills)
    {
        SkillType combinedSkills = 0;
        foreach (SkillType skill in skills)
        {
            combinedSkills |= skill;
        }
        return (SkillType & combinedSkills) == combinedSkills;
    }
    /// <summary>
    /// スキル性質のいずれかを持ってるかどうか
    /// 複数指定の場合は一つでも持ってればtrueを返します。
    /// </summary>
    public bool HasTypeAny(params SkillType[] skills)
    {
        return skills.Any(skill => (SkillType & skill) != 0);    
    }
    /// <summary>
    /// そのスキル連続性質を持ってるかどうか
    /// </summary>
    public bool HasConsecutiveType(SkillConsecutiveType skill)
    {
        return (ConsecutiveType & skill) == skill;
    }
    /// <summary>
    /// スキル範囲性質を持ってるかどうか
    /// 複数指定した場合は全て当てはまってるかどうかで判断
    /// </summary>
    public bool HasZoneTrait(params SkillZoneTrait[] skills)
    {
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }
        return (ZoneTrait & combinedSkills) == combinedSkills;
    }
     /// <summary>
    /// スキル範囲性質のいずれかを持ってるかどうか
    /// 複数指定した場合はどれか一つでも当てはまればtrueを返す
    /// </summary>
    public bool HasZoneTraitAny(params SkillZoneTrait[] skills)
    {
        return skills.Any(skill => (ZoneTrait & skill) != 0);
    }
    /// <summary>
    /// 単体系スキル範囲性質のいずれかを持っているかを判定
    /// </summary>
    public bool HasAnySingleTargetTrait()
    {
        return (ZoneTrait & CommonCalc.SingleZoneTrait) != 0;
    }
    
    /// <summary>
    /// 単体系スキル範囲性質のすべてを持っているかを判定
    /// </summary>
    public bool HasAllSingleTargetTraits()
    {
        return (ZoneTrait & CommonCalc.SingleZoneTrait) == CommonCalc.SingleZoneTrait;
    }


    /// <summary>
    ///     スキルの物理属性
    /// </summary>
    public PhysicalProperty SkillPhysical;

    public BaseStates Doer;//行使者
    /// <summary>
    /// スキルの特殊判別性質
    /// </summary>
    public SkillSpecialFlag SpecialFlags;
    /// <summary>
    /// スキルの特殊判別性質を持っているかどうか
    /// </summary>
    public bool HasSpecialFlag(SkillSpecialFlag skill)
    {
        return (SpecialFlags & skill) == skill;
    }
    /// <summary>
    /// TLOAかどうか
    /// </summary>
    public bool IsTLOA
    {
        get { return HasSpecialFlag(SkillSpecialFlag.TLOA); }
    }
    /// <summary>
    /// 魔法スキルかどうか
    /// </summary>
    public bool IsMagic
    {
        get { return HasSpecialFlag(SkillSpecialFlag.Magic); }
    }
    /// <summary>
    /// 切物スキルかどうか
    /// </summary>
    public bool IsBlade
    {
        get { return HasSpecialFlag(SkillSpecialFlag.Blade); }
    }



    /// <summary>
    /// 殺せないスキルかどうか
    /// 1残る
    /// </summary>
    public bool Cantkill = false;
    /// <summary>
    /// このスキルを実行することにより影響させる使い手の回避率でAGIに掛けられる回避補正率
    /// 一番最後に掛けられる。
    /// </summary>
    public float EvasionModifier = 1f;
    /// <summary>
    /// このスキルを実行することにより影響させる使い手の攻撃力としてATKに掛けられる攻撃補正率
    /// 一番最初の素の攻撃力に掛けられる。
    /// </summary>
    public float AttackModifier = 1f;
    /// <summary>
    /// TLOAスキルのレベルアップに必要な使用回数
    /// </summary>
    protected const int TLOA_LEVEL_DIVIDER = 120;
    /// <summary>
    /// 非TLOAスキルのレベルアップに必要な使用回数
    /// </summary>
    protected const int NOT_TLOA_LEVEL_DIVIDER = 50;
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
        var MoodRange = ModifyMoodByAttributeCompatibility(BaseMoodRange,Doer.MyImpression,Walking.bm.MyGroup(Doer).OurImpression);

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

    /// <summary>
    /// 固定されたスキルレベルデータ部分
    /// このリスト以降なら無限のデータ
    /// </summary>
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

    /// <summary>
    /// スキルのパワー　
    /// ゆりかご効果によるスキルレベルを参照するか、そうでないか。
    /// </summary>
    protected virtual float _skillPower(bool IsCradle)
    {
        var Level = _nowSkillLevel;
        var powerMultiplier = SkillPassiveSkillPowerRate();//スキルパワーに乗算する値
        if(IsCradle)
        {
            Level = _cradleSkillLevel;//ゆりかごならゆりかご用計算されたスキルレベルが
        }
        // スキルレベルが有限範囲ならそれを返す
        if (FixedSkillLevelData.Count > Level)
        {
            return FixedSkillLevelData[Level].SkillPower * powerMultiplier;
        }
        else
        {// そうでないなら有限最終以降と無限単位の加算
            // 有限リストの最終値と無限単位に以降のスキルレベルを乗算した物を加算
            // 有限リストの最終値を基礎値にする
            var baseSkillPower = FixedSkillLevelData[FixedSkillLevelData.Count - 1].SkillPower;

            // 有限リストの超過分、無限単位にどの程度かけるかの数
            var infiniteLevelMultiplier = Level - (FixedSkillLevelData.Count - 1);

            // 基礎値に無限単位に超過分を掛けたものを加算して返す
            return (baseSkillPower + _infiniteSkillPowerUnit * infiniteLevelMultiplier) * powerMultiplier;

            // 有限リストがないってことはない。必ず一つは設定されてるはずだしね。
        }
    }
    /// <summary>
    /// スキルのパワー
    /// </summary>
    public float GetSkillPower(bool IsCradle = false) => _skillPower(IsCradle) * (1.0f - MentalDamageRatio);
    /// <summary>
    /// 精神HPへのスキルのパワー
    /// </summary>
    public float GetSkillPowerForMental(bool IsCradle = false) => _skillPower(IsCradle) * MentalDamageRatio;
    /// <summary>
    /// スキルパッシブ由来のスキルパワー百分率
    /// </summary>
    public float SkillPassiveSkillPowerRate()
    {
        //初期値を1にして、すべてのかかってるスキルパッシブのSkillPowerRateを掛ける
        var rate = ReactiveSkillPassiveList.Aggregate(1.0f, (acc, pas) => acc * (1.0f + pas.SkillPowerRate));
        return rate;
    }

    /// <summary>
    /// 通常の精神攻撃率
    /// </summary>
    [SerializeField]
    float _mentalDamageRatio;
    /// <summary>
    /// 精神攻撃率　100だとSkillPower全てが精神HPの方に行くよ。
    /// 有限リストのオプション値で指定されてるのならそれを返す
    /// </summary>
    public float MentalDamageRatio
    {
        get
        {
            //スキルレベルが有限範囲なら
            if(FixedSkillLevelData.Count > _nowSkillLevel)
            {
                //-1でないならあるので返す
                // 現在のレベルから逆向きに検索して最初に有効な値を見つける
                for(int i = _nowSkillLevel; i >= 0; i--) 
                {
                    if(FixedSkillLevelData[i].OptionMentalDamageRatio != -1)
                    {
                        return FixedSkillLevelData[i].OptionMentalDamageRatio;
                    }
                }
            }
            //当然有限リストは絶対に存在するので、
            //有限範囲以降なら、その最終値でオプションで指定されてるならそれを返す
            //有限リスト外の場合、最後の要素から逆向きに検索
            for(int i = FixedSkillLevelData.Count - 1; i >= 0; i--) 
            {
                if(FixedSkillLevelData[i].OptionMentalDamageRatio != -1)
                {
                    return FixedSkillLevelData[i].OptionMentalDamageRatio;
                }
            }

            //そうでないなら設定値を返す
            return _mentalDamageRatio;
        }
    }
    /// <summary>
    /// 通常の分散割合
    /// </summary>
    [SerializeField]
    float[] _powerSpread;
    /// <summary>
    /// スキルの範囲効果における各割合　最大で6の長さまで使うと思う
    /// 有限リストのオプション値で指定されてるのならそれを返す
    /// </summary>
    public float[] PowerSpread
    {
        get
        {
            //スキルレベルが有限範囲なら
            if(FixedSkillLevelData.Count > _nowSkillLevel)
            {
                //nullでないならあるので返す
                // 現在のレベルから逆向きに検索して最初に有効な値を見つける
                for(int i = _nowSkillLevel; i >= 0; i--) 
                {
                    if(FixedSkillLevelData[i].OptionPowerSpread != null && 
                    FixedSkillLevelData[i].OptionPowerSpread.Length > 0)
                    {
                        return FixedSkillLevelData[i].OptionPowerSpread;
                    }
                }
            }
            //当然有限リストは絶対に存在するので、
            //有限範囲以降なら、その最終値でオプションで指定されてるならそれを返す
            //有限リスト外の場合、最後の要素から逆向きに検索
            for(int i = FixedSkillLevelData.Count - 1; i >= 0; i--) 
            {
                if(FixedSkillLevelData[i].OptionPowerSpread != null && 
                FixedSkillLevelData[i].OptionPowerSpread.Length > 0)
                {
                    return FixedSkillLevelData[i].OptionPowerSpread;
                }
            }

            //そうでないなら設定値を返す
            return _powerSpread;
        }
    }
    /// <summary>
    /// 通常の命中補正
    /// </summary>
    [SerializeField]
    int _skillHitPer;
    /// <summary>
    /// スキルの命中補正 int 百分率
    /// </summary>
    public int SkillHitPer
    {
        get
        {
            //スキルレベルが有限範囲なら
            if(FixedSkillLevelData.Count > _nowSkillLevel)
            {
                //-1でないならあるので返す
                if(FixedSkillLevelData[_nowSkillLevel].OptionSkillHitPer != -1)
                {
                    // 現在のレベルから逆向きに検索して最初に有効な値を見つける
                    for(int i = _nowSkillLevel; i >= 0; i--) {
                        if(FixedSkillLevelData[i].OptionSkillHitPer != -1) {
                            return FixedSkillLevelData[i].OptionSkillHitPer;
                        }
                    }
                }
            }
            //当然有限リストは絶対に存在するので、
            //有限範囲以降なら、その最終値でオプションで指定されてるならそれを返す
            //有限リスト外の場合、最後の要素から逆向きに検索
            for(int i = FixedSkillLevelData.Count - 1; i >= 0; i--) {
                if(FixedSkillLevelData[i].OptionSkillHitPer != -1) {
                    return FixedSkillLevelData[i].OptionSkillHitPer;
                }
            }
            //そうでないなら設定値を返す
            return _skillHitPer;
        }
    }

    /// <summary>
    /// 通常設定されたムーブセットA
    /// </summary>
    [SerializeField]
    List<MoveSet> _a_moveset = new();
    /// <summary>
    /// スキルごとのムーブセット 戦闘規格ごとのaに対応するもの。
    /// </summary>
    List<MoveSet> A_MoveSet_Cash = new();
    /// <summary>
    /// 通常設定されたムーブセットB
    /// </summary>
    [SerializeField]
    List<MoveSet> _b_moveset = new();
    /// <summary>
    /// スキルごとのムーブセット 戦闘規格ごとのbに対応するもの。
    /// </summary>
    List<MoveSet> B_MoveSet_Cash=new();
    /// <summary>
    /// 連続攻撃中にスキルレベル成長によるムーブセット変更を防ぐために、
    /// 連続攻撃開始時にムーブセットをキャッシュし使い続ける
    /// </summary>
    public void CashMoveSet()
    {
        var A_cash = _a_moveset;//
        var B_cash = _b_moveset;

        //スキルレベルが有限範囲なら
            if(FixedSkillLevelData.Count > _nowSkillLevel)
            {
                for(int i = _nowSkillLevel ; i>=0; i --)
                {
                    if(FixedSkillLevelData[i].OptionA_MoveSet != null)//nullでないならあるので返す
                    {
                        A_cash = FixedSkillLevelData[i].OptionA_MoveSet;
                        break;//レベルを下げてって一致した物だけを返し、ループを抜ける。
                    }
                }
                for(int i = _nowSkillLevel ; i>=0; i --)
                {
                    if(FixedSkillLevelData[i].OptionB_MoveSet != null)
                    {
                        B_cash = FixedSkillLevelData[i].OptionB_MoveSet;
                        break;
                    }
                }
            }else
            {
                //当然有限リストは絶対に存在するので、
                //有限範囲以降なら、その最終値から後ろまで回して、でオプションで指定されてるならそれを返す
                for(int i = FixedSkillLevelData.Count - 1 ; i>=0; i --)
                {
                    if(FixedSkillLevelData[i].OptionA_MoveSet != null)
                    {
                        A_cash = FixedSkillLevelData[i].OptionA_MoveSet;
                        break;
                    }
                }
                for(int i = FixedSkillLevelData.Count - 1 ; i>=0; i --)
                {
                    if(FixedSkillLevelData[i].OptionB_MoveSet != null)
                    {
                        B_cash = FixedSkillLevelData[i].OptionB_MoveSet;
                        break;
                    }
                }
            }

            //キャッシュする。
            A_MoveSet_Cash = A_cash;
            B_MoveSet_Cash = B_cash;
           
        }

    /// <summary>
    /// スキルの印象構造　十日能力値
    /// ゆりかごするかどうかは引数で
    /// </summary>
    public TenDayAbilityDictionary TenDayValues(bool IsCradle = false)
    {
        Debug.Log($"スキル印象構造の取得 : スキル有限レベルリストの数:{FixedSkillLevelData.Count},キャラ:{Doer.CharacterName}");
        var Level = _nowSkillLevel;
        if(IsCradle)
        {
            Level = _cradleSkillLevel;//ゆりかごならゆりかご用計算されたスキルレベルが
        }

        //skillLecelが有限範囲ならそれを返す
        if(FixedSkillLevelData.Count > Level)
        {
            return FixedSkillLevelData[Level].TenDayValues;
        }else
        {//そうでないなら有限最終以降と無限単位の加算
            //有限リストの最終値と無限単位に以降のスキルレベルを乗算した物を加算
            //有限リストの最終値を基礎値にする
            var BaseTenDayValues = FixedSkillLevelData[FixedSkillLevelData.Count - 1].TenDayValues;

            //有限リストの超過分、無限単位にどの程度かけるかの数
            var InfiniteLevelMultiplier =  Level - (FixedSkillLevelData.Count - 1);

            //基礎値に無限単位に超過分を掛けたものを加算して返す。
            return BaseTenDayValues + _infiniteSkillTenDaysUnit * InfiniteLevelMultiplier;
        
            //有限リストがないってことはない。必ず一つは設定されてるはずだしね。
        }
    }

    /// <summary>
    /// スキルの印象構造の十日能力値の合計
    /// </summary>
    public float TenDayValuesSum => TenDayValues().Sum(kvp => kvp.Value);

    private int _doConsecutiveCount;//スキルを連続実行した回数
    private int _doCount;//スキルを実行した回数
    protected int _recordDoCount;
    private int _hitCount;    // スキルがヒットした回数
    private int _hitConsecutiveCount;//スキルが連続ヒットした回数
    private int _triggerCount;//発動への−カウント　このカウント分連続でやらないと発動しなかったりする　重要なのは連続でやらなくても　一気にまたゼロからになるかはスキル次第
    [SerializeField]
    private int _triggerCountMax;//発動への−カウント　の指標

    /// <summary>
    /// triggerCountが0以上の複数ターン実行が必要なスキルの場合、複数ターンに跨る発動カウント実行中に中断出来るかどうか。
    /// </summary>
    public bool CanCancelTrigger = true;
    /// <summary>
    /// 発動カウント中に他のスキルを選んだ際に巻き戻るカウントの量
    /// </summary>
    [SerializeField]
    private int _triggerRollBackCount;
    private int _atkCountUP;//連続攻撃中のインデックス的回数
    [SerializeField]
    private float _RandomConsecutivePer;//連続実行の確率判定のパーセント

    //stockpile用　最大値はランダム確率の連続攻撃と同じように、ATKCountを参照する。
    private int _nowStockCount;//現在のストック数
    /// <summary>
    /// デフォルトのストック数
    /// </summary>
    [SerializeField]
    private int _defaultStockCount = 1;
    ///<summary> ストックデフォルト値。DefaultAtkCount を超えないように調整された値を返す</summary> ///
    int DefaultStockCount => _defaultStockCount > DefaultAtkCount ? DefaultAtkCount : _defaultStockCount;

    [SerializeField]
    private int _stockPower = 1;//ストック単位
    /// <summary>
    /// ストック単位を手に入れる
    /// </summary>
    protected virtual int GetStcokPower()
    {
        return _stockPower;
    }
    [SerializeField]
    private int _stockForgetPower = 1;//ストック忘れ単位
    /// <summary>
    /// ストック忘れ単位を手に入れる
    /// </summary>
    protected virtual int GetStcokForgetPower()
    {
        return _stockForgetPower;
    }


    /// <summary>
    /// 前回のスキル行使の戦闘ターン ない状態は-1
    /// </summary>
    private int _tmpSkillUseTurn = -1;
    /// <summary>
    /// 前回スキル実行した時からの経過ターン 
    /// </summary>
    public int DeltaTurn { get; private set; } = -1;

    /// <summary>
    /// 前回からのターン差を記録するDeltaTurnを更新する関数  battleManagerで利用する。
    /// </summary>
    public void SetDeltaTurn(int nowTurn)
    {
        if (_tmpSkillUseTurn >= 0)//前回のターンが記録されていたら
        {
            DeltaTurn = Math.Abs(_tmpSkillUseTurn - nowTurn);//前回との差をdeltaTurn保持変数に記録する
        }
        //記録されていない場合、リセットされて-1になっているので何もしない。


        _tmpSkillUseTurn = nowTurn;//今回のターン数を一時記録

    }

    

    /// <summary>
    /// このスキルを利用すると前のめり状態になるかどうか
    /// </summary>
    public bool IsAggressiveCommit = true;
    /// <summary>
/// 発動カウント実行時に前のめりになるかどうか
/// </summary>
    public bool IsReadyTriggerAgressiveCommit = false;
    /// <summary>
    /// スキルのストック時に前のめりになるかどうか
    /// </summary>
    public bool IsStockAgressiveCommit = false;
    /// <summary>
    /// スキルが前のめりになるからならないかを選べるかどうか
    /// </summary>
    public bool CanSelectAggressiveCommit = false;


    /// <summary>
    /// 実行したキャラに付与される追加硬直値
    /// </summary>
    public int SKillDidWaitCount;//スキルを行使した後の硬直時間。 Doer、行使者のRecovelyTurnに一時的に加算される？


    public string SkillName = "ここに名前を入れてください";

    /// <summary>
    /// BattleManager単位で行使した回数
    /// 実行対象のreactionSkill内でインクリメント
    /// </summary>    
    public int DoCount
    {
        get { return _doCount; }
        set { _doCount = value; }
    }
    /// <summary>
    /// 行使した回数 永続的にカウントされる
    /// </summary>    
    public int RecordDoCount
    {
        get { return _recordDoCount; }
        set { _recordDoCount = value; }
    }
    /// <summary>
    /// 永続的なものと一時的な物両方のスキル使用回数をカウントアップ
    /// </summary>
    public virtual void DoSkillCountUp()
    {
        _doCount++;
        _recordDoCount++;
    }

    /// <summary>
    /// BattleManager単位で"連続"で使われた回数。　
    /// 実行する際にdoerのSkillUseConsecutiveCountUpからAttackChara内で使用
    /// </summary>
    public virtual int DoConsecutiveCount
    {
        get { return _doConsecutiveCount; }
        set { _doConsecutiveCount = value; }

    }

    /// <summary>
    /// スキルがヒットしたときの回数カウント
    /// </summary>
    public int HitCount
    {
        get => _hitCount;
        set { _hitCount = value; }
    }
    /// <summary>
    /// スキルが連続ヒットしたときの回数カウント
    /// </summary>
    public int HitConsecutiveCount
    {
        get => _hitConsecutiveCount;
        set { _hitConsecutiveCount = value; }
    }

    /// <summary>
    /// スキルのヒットした回数、またその連続回数をカウントアップする
    /// </summary>
    public void SkillHitCount()
    {
        HitCount++;//単純にヒット回数を増やす
    }

    /// <summary>
    /// スキル実行に必要なカウント　-1で実行される。
    /// </summary>
    public virtual int TrigerCount()
    {
        if (_triggerCountMax > 0)//1回以上設定されてたら
        {
            _triggerCount--;
            return _triggerCount;
        }

        //発動カウントが0に設定されている場合、そのまま実行される。
        return -1;
    }
    /// <summary>
    /// トリガーカウントをスキルの巻き戻しカウント数に応じて巻き戻す処理
    /// </summary>
    public void RollBackTrigger()
    {
        _triggerCount += _triggerRollBackCount;
        if (_triggerCount > _triggerCountMax)_triggerCount = _triggerCountMax;//最大値を超えないようにする
    }

    /// <summary>
    /// 発動カウントが実行中かどうかを判定する
    /// </summary>
    /// <returns>発動カウントが開始済みで、まだカウント中ならtrue、それ以外はfalse</returns>
    public bool IsTriggering
    {
        get{
            // 発動カウントが0以下の場合は即時実行なのでfalse
            if (_triggerCountMax <= 0) return false;
            
            // カウントが開始されていない場合はfalse
            // カウントが開始されると_triggerCountは_triggerCountMaxより小さくなる
            if (_triggerCount >= _triggerCountMax) return false;
            
            // カウントが開始済みで、まだカウントが残っている場合はtrue
            return _triggerCount  > -1;
        }
    }

    /// <summary>
    /// 実行に成功した際の発動カウントのリセット 0ばら
    /// </summary>
    public virtual void ReturnTrigger()
    {
        _triggerCount = _triggerCountMax;//基本的にもう一回最初から
    }
    /// <summary>
    /// ストック数をデフォルトにリセット
    /// </summary>
    public void ResetStock()
    {
        _nowStockCount = DefaultStockCount;
    }
    /// <summary>
    /// 攻撃回数をストック
    /// </summary>
    public void ATKCountStock()
    {

        _nowStockCount += GetStcokPower();
        if(_nowStockCount > DefaultAtkCount)_nowStockCount = DefaultAtkCount;//想定される最大値を超えないようにする
        
    }
    /// <summary>
    /// ストックを忘れる
    /// </summary>
    public void ForgetStock()
    {
        _nowStockCount -= GetStcokForgetPower();
        if(_nowStockCount < DefaultStockCount)_nowStockCount = DefaultStockCount;//ストック数はデフォルト値を下回らないようにする
    }
    /// <summary>
    /// ストックが満杯かどうか
    /// </summary>
    public bool IsFullStock()
    {
        return _nowStockCount >= DefaultAtkCount;//最大値以上ならばストックが満杯とする
    }

    /// <summary>
    /// 通常の攻撃回数
    /// </summary>
    int DefaultAtkCount =>  1 + NowMoveSetState.States.Count;
    /// <summary>
    /// オーバライド可能な攻撃回数
    /// </summary>
    public virtual int ATKCount
    {
        get { 
            if(HasConsecutiveType(SkillConsecutiveType.Stockpile))
            {
                return _nowStockCount;//stockpileの場合は現在ストック数を参照する
            }
            return DefaultAtkCount; 
            }

    }
    /// <summary>
    /// 現在の連続攻撃回数のindex
    /// </summary>
    public int ATKCountUP => _atkCountUP;

    /// <summary>
    /// 攻撃回数カウントをリセットする
    /// </summary>
    public void ResetAtkCountUp()
    {
        _atkCountUP = 0;
    }

    /// <summary>
    /// 現在の連続攻撃回数を参照して次回の連続攻撃があるかどうか
    /// </summary>
    public bool NextConsecutiveATK()
    {
        if(HasConsecutiveType(SkillConsecutiveType.FixedConsecutive))//回数による連続性質の場合
        {
            if (_atkCountUP >= ATKCount)//もし設定した値にカウントアップ値が達成してたら。
            {
                _atkCountUP = 0;//値初期化
                return false;//終わり
            }
            return true;//まだ達成してないから次の攻撃がある。

        }else if(HasConsecutiveType(SkillConsecutiveType.RandomPercentConsecutive))//確率によるなら
        {
            if (_atkCountUP >= ATKCount)//もし設定した値にカウントアップ値が達成してたら。
            {
                _atkCountUP = 0;//値初期化
                return false;//終わり
            }

            if(RandomEx.Shared.NextFloat(1)<_RandomConsecutivePer)//確率があったら、
            {
                
                return true;
            }
            return false;
        }

        return false;
    }
    /// <summary>
    /// 現在が連続攻撃中(二回目以降)かどうか
    /// </summary>
    public bool NowConsecutiveATKFromTheSecondTimeOnward()
    {
        if (_atkCountUP > 0)
        {
            return true;
        }
        return false;
    }
    /// <summary>
    /// 連続攻撃の値を増やす
    /// </summary>
    /// <returns></returns>
    public virtual int ConsecutiveFixedATKCountUP()
    {
        _atkCountUP++;
        return _atkCountUP;
    }

    

    /// <summary>
    /// 初期化コールバック関数 初期化なので起動時の最初の一回しか使わないような処理しか書かないようにして
    /// </summary>
    public void OnInitialize(BaseStates owner)
    {
        Doer = owner;//管理者を記録
        ResetStock();//_nowstockは最初は0になってるので、初期化でdefaultstockと同じ数にする。
    }
    /// <summary>
    /// 行使者 doerが死亡したときdoer側で呼ばれるコールバック
    /// </summary>
    public void OnDeath()
    {
        ResetStock();
        ResetAtkCountUp();
        ReturnTrigger();
    }
    public void OnBattleStart()
    {
        //カウントが専ら参照されるので、バグ出ないようにとりあえず仮のムーブセットを決めておく。
        DecideNowMoveSet_A0_B1(0);
    }
    /// <summary>
    /// スキルの"一時保存"系プロパティをリセットする　BattleManager終了時など
    /// </summary>
    public void OnBattleEnd()
    {
        _doCount = 0;
        _doConsecutiveCount = 0;
        _hitCount = 0;
        _hitConsecutiveCount = 0;
        _cradleSkillLevel = -1;//ゆりかご用スキルレベルをエラーにする。
        ResetAtkCountUp();
        ReturnTrigger();//発動カウントはカウントダウンするから最初っから
        _tmpSkillUseTurn = -1;//前回とのターン比較用の変数をnullに
        ResetStock();

        ///スキルパッシブの終了時の処理
        foreach(var pas in ReactiveSkillPassiveList.Where(pas => pas.DurationWalkTurn < 0))
        {
            RemoveSkillPassive(pas);
        }
    }

    /// <summary>
    /// スキルパワーの計算
    /// </summary>
    public virtual float SkillPowerCalc(bool IsCradle = false)
    {
        var pwr = GetSkillPower(IsCradle);//基礎パワー




        return pwr;
    }
    public virtual float SkillPowerForMentalCalc(bool IsCradle = false)
    {
        var pwr = GetSkillPowerForMental(IsCradle);//基礎パワー


        return pwr;
    }

    /// <summary>
    /// スキルにより補正された最終命中率
    /// 引数の命中凌駕はIsReactHitで使われるものなので、キャラ同士の命中回避計算が
    /// 必要ないものであれば、引数を指定しなくていい(デフォ値)
    /// またHitResultの引数は事前の命中回避計算等でどういうヒット結果になったかを渡して最終結果として返すため。
    /// スキル命中onlyならデフォルトで普通のHitが指定されるので渡さなくてOK
    /// </summary>
    /// <param name="supremacyBonus">命中ボーナス　主に命中凌駕用途</param>>
    public virtual HitResult SkillHitCalc(BaseStates target,float supremacyBonus = 0,HitResult hitResult = HitResult.Hit,bool PreliminaryMagicGrazeRoll = false)
    {
        //割り込みカウンターなら確実
        if(Doer.HasPassive(1)) return hitResult;

        //通常計算
        var rndMin = RandomEx.Shared.NextInt(3);//ボーナスがある場合ランダムで三パーセント~0パーセント引かれる
        if(supremacyBonus>rndMin)supremacyBonus -= rndMin;

        var result = RandomEx.Shared.NextInt(100) < supremacyBonus + SkillHitPer ? hitResult : HitResult.CompleteEvade;

        if(result == HitResult.CompleteEvade && IsMagic)//もし発生しなかった場合、魔法スキルなら
        {
            //三分の一の確率でかする
            if(RandomEx.Shared.NextInt(3) == 0) result = HitResult.Graze;
        }

        if(PreliminaryMagicGrazeRoll)//事前魔法かすり判定がIsReactHitで行われていたら
        {
            result = hitResult;//かすりを入れる
        }

        return result;
    }
    /// <summary>
    /// Manual系のスキルでの用いるスキルごとの独自効果
    /// </summary>
    /// <param name="target"></param>
    public virtual void ManualSkillEffect(BaseStates target,HitResult hitResult)
    {
        
    }

    /// <summary>
    /// SubEffectsの基本的な奴
    /// </summary>
    [SerializeField] List<int> subEffects;
    /// <summary>
    /// SubEffectsのバッファ 主にパッシブによる追加適用用など
    /// </summary>
    List<int> bufferSubEffects;
    /// <summary>
    /// スキルのパッシブ付与効果に追加適用する。　
    /// バッファーのリストに追加する。
    /// </summary>
    /// <param name="subEffects"></param>
    public void SetBufferSubEffects(List<int> subEffects)
    {
        bufferSubEffects = subEffects;
    }
    /// <summary>
    /// スキルのパッシブ付与効果の追加適用を消す。
    /// </summary>
    public void EraseBufferSubEffects()
    {
        bufferSubEffects.Clear();
    }
    /// <summary>
    /// スキル実行時に付与する状態異常とか ID指定
    /// </summary>
    public List<int> SubEffects
    {
        get { return subEffects.Concat(bufferSubEffects).ToList(); }
    }
    /// <summary>
    /// スキル実行時に付与する追加HP(Passive由来でない)　ID指定
    /// </summary>
    public List<int> subVitalLayers;
    /// <summary>
    /// 除去スキルとして消せるパッシブのID範囲
    /// </summary>
    public List<int> canEraceEffectIDs;
    /// <summary>
    /// 除去スキルとして消せる追加HPのID範囲
    /// </summary>
    public List<int> canEraceVitalLayerIDs;
    /// <summary>
    /// 除去スキルとして使用する際に指定する消せるパッシブの数
    /// 除去スキルでないと参照されない
    /// </summary>
    public int CanEraceEffectCount;
    /// <summary>
    /// 現在の消せるパッシブの数
    /// ReactionSkill内で除去する度に減っていく値
    /// </summary>
    [NonSerialized]
    public int Now_CanEraceEffectCount;
    /// <summary>
    /// 除去スキルとして使用する際に指定する消せる追加HPの数
    /// 除去スキルでないと参照されない
    /// </summary>
    public int CanEraceVitalLayerCount;
    /// <summary>
    /// 現在の消せる追加HPの数
    /// ReactionSkill内で除去する度に減っていく値
    /// </summary>
    [NonSerialized]
    public int Now_CanEraceVitalLayerCount;
    /// <summary>
    /// 除去可能数をReactionSkill冒頭で補充する。
    /// </summary>
    public void RefilCanEraceCount()
    {
        Now_CanEraceEffectCount = CanEraceEffectCount;
        Now_CanEraceVitalLayerCount = CanEraceVitalLayerCount;
    }


    /// <summary>
    /// 現在のムーブセット
    /// </summary>
    [NonSerialized]
    MoveSet NowMoveSetState=new();

    /// <summary>
    /// A-MoveSetのList<MoveSet>から現在のMoveSetをランダムに取得する
    /// なにもない場合はreturnで終わる。つまり単体攻撃前提ならmovesetが決まらない。
    /// aOrB 0:A 1:B
    /// </summary>
    public void DecideNowMoveSet_A0_B1(int aOrB)
    {
        if(aOrB == 0)
        {
            if(A_MoveSet_Cash.Count == 0)
            {
                NowMoveSetState = new();
                return;
            }
            NowMoveSetState = A_MoveSet_Cash[RandomEx.Shared.NextInt(A_MoveSet_Cash.Count)];
        }
        else if(aOrB == 1)
        {
            if(B_MoveSet_Cash.Count == 0)
            {
                NowMoveSetState = new();
                return;
            }
            NowMoveSetState = B_MoveSet_Cash[RandomEx.Shared.NextInt(B_MoveSet_Cash.Count)];
        }
    }
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
        if(!NowConsecutiveATKFromTheSecondTimeOnward())return _nowSingleAimStyle;//初回攻撃なら単体保存した変数を返す

        return NowMoveSetState.GetAtState(_atkCountUP - 1); 
    }
    /// <summary>
    /// 現在のムーブセットでのDEFATKを、現在の攻撃回数から取得する
    /// 初回攻撃を指定したらエラー出るます
    /// </summary>
    float NowAimDefATK()
    {
        return NowMoveSetState.GetAtDEFATK(_atkCountUP - 1); 
        //-1してる理由　ムーブセットListは二回目以降から指定されるので。
        //リストのインデックスでしっかり初回から参照されるように二回目前提として必ず-1をする。
    }

    [SerializeField]
    float _defAtk;
    
    /// <summary>
    /// 連続攻撃時にはそれ用の、それ以外はスキル自体の防御無視率が返ります。
    /// </summary>
    public float DEFATK{
        get{
            
            if(NowConsecutiveATKFromTheSecondTimeOnward())//連続攻撃中ならば、
            {
                return NowAimDefATK();//連続攻撃に設定されているDEFATKを乗算する
            }

            return _defAtk;
        }
    }
    /// <summary>
    /// スキルの攻撃性質　基本的な奴
    /// </summary>
    [SerializeField]
    SkillType _baseSkillType;
    /// <summary>
    /// スキルの攻撃性質　バッファ用フィールド
    /// </summary>
    SkillType bufferSkillType;
    /// <summary>
    /// スキルの攻撃性質
    /// </summary>
    public SkillType SkillType
    {
        get => _baseSkillType | bufferSkillType;
    }
    // ⑤ バッファのセット／クリア用メソッド
    /// <summary>一時的に追加する SkillType を設定</summary>
    public void SetBufferSkillType(SkillType skill)
        => bufferSkillType = skill;

    /// <summary>バッファをクリア</summary>
    public void EraseBufferSkillType()
        => bufferSkillType = 0;
    /// <summary>
    /// スキルの連撃性質
    /// </summary>
    public SkillConsecutiveType ConsecutiveType;
    /// <summary>
    /// スキルの範囲性質
    /// 初期値は0
    /// </summary>
    public SkillZoneTrait ZoneTrait = 0;//初期値
    /// <summary>
    /// スキルの分散性質
    /// </summary>
    public AttackDistributionType DistributionType;

    /// <summary>
    /// 威力の範囲が複数に選択または分岐可能時の割合差分
    /// インスペクタで追加し、
    /// 基本的にskillPowerCalcと味方の範囲選択ボタンでの表記で用い、
    /// canselectRangeで範囲が複数選択できる、
    /// またはrandomRangeで範囲が複数分岐した際に使う感じ。
    /// </summary>
    public SerializableDictionary<SkillZoneTrait, float>
        PowerRangePercentageDictionary;
    /// <summary>
    /// 命中率のステータスに直接かかるスキルの範囲意志による威力補正
    /// </summary>
    public SerializableDictionary<SkillZoneTrait, float>
        HitRangePercentageDictionary;


    [Header("スキルパッシブ設定")]
    
    /// <summary>
    /// スキルに掛ってるパッシブリスト
    /// このスキルが感染してる病気ってこと
    /// </summary>
    public List<BaseSkillPassive> ReactiveSkillPassiveList = new();
    /// <summary>
    /// このスキルがスキルパッシブ付与スキルとして実行される際の、装弾されたスキルパッシブです
    /// 除去スキルはスキルパッシブごとに判別しないため、これは付与スキルパッシブのみのリストです
    /// </summary>
    public List<BaseSkillPassive> AggressiveSkillPassiveList = new();
    /// <summary>
    /// スキルパッシブをリストから除去する
    /// </summary>
    public void RemoveSkillPassive(BaseSkillPassive passive)
    {
        ReactiveSkillPassiveList.Remove(passive);
    }
    /// <summary>
    /// スキル効果により、一気にスキルパッシブを抹消する関数
    /// スキルパッシブの性質によるもの
    /// </summary>
    public void SkillRemoveSkillPassive()
    {
        ReactiveSkillPassiveList.Clear();
    }
    /// <summary>
    /// スキルパッシブを付与する。
    /// </summary>
    /// <param name="passive"></param>
    public void ApplySkillPassive(BaseSkillPassive passive)
    {
        ReactiveSkillPassiveList.Add(passive);
    }

    /// <summary>
    /// スキルパッシブバージョンの付与処理用バッファーリスト
    /// </summary>
    List<BaseSkillPassive> BufferApplyingSkillPassiveList = new();
    /// <summary>
    /// バッファのスキルパッシブを追加する。
    /// </summary>
    public void ApplyBufferApplyingSkillPassive()
    {
        foreach(var passive in BufferApplyingSkillPassiveList)
        {
            ApplySkillPassive(passive);
        }
        BufferApplyingSkillPassiveList.Clear();//追加したからバッファ消す
    }
    /// <summary>
    /// 戦闘中のスキルパッシブ追加は基本バッファに入れよう
    /// </summary>
    public void ApplySkillPassiveBufferInBattle(BaseSkillPassive passive)
    {
        BufferApplyingSkillPassiveList.Add(passive);
    }



    /// <summary>
    /// 付与するスキルを選択する方式。
    /// </summary>
    public SkillPassiveTargetSelection TargetSelection;

    /// <summary>
    /// スキルパッシブ付与スキルである際の、反応式の対象キャラとスキル
    /// </summary>
    public List<SkillPassiveReactionCharaAndSkill> ReactionCharaAndSkillList = new();

    /// <summary>
    /// スキルパッシブ付与スキルだとして、何個まで付与できるか。
    /// </summary>
    public int SkillPassiveEffectCount = 1;
    /// <summary>
    /// スキルパッシブ付与スキルのスキルの区別フィルター
    /// </summary>
    [SerializeField] SkillFilter _skillPassiveGibeSkill_SkillFilter = new();

    /// <summary>
    /// スキルパッシブの付与対象となるスキルを対象者のスキルリストから選ぶ。
    /// </summary>
    public async UniTask<List<BaseSkill>> SelectSkillPassiveAddTarget(BaseStates target)
    {
        var targetSkills = target.SkillList.ToList();//ターゲットの現在解放されてるスキル
        if(targetSkills.Count == 0)
        {
        Debug.LogError("スキルパッシブの対象スキルの選別を試みましたが、\n対象者のスキルリストが空です");
        return null;}

        //直接選択式(UI)
        if(TargetSelection == SkillPassiveTargetSelection.Select)
        {
            //敵ならAIで 未実装
            if(manager.GetCharacterFaction(Doer) == allyOrEnemy.Enemyiy)
            {
                
            }

            //味方はUI選択
            if(manager.GetCharacterFaction(Doer) == allyOrEnemy.alliy)
            {
                if(_skillPassiveGibeSkill_SkillFilter != null && _skillPassiveGibeSkill_SkillFilter.HasAnyCondition)//フィルタ条件があるなら絞り込む
                {
                    //フィルタで絞り込む
                    targetSkills = targetSkills.Where(s => s.MatchFilter(_skillPassiveGibeSkill_SkillFilter)).ToList();
                    // 絞り込み後に候補が0件の場合
                    if(targetSkills.Count == 0)
                    {
                        Debug.LogWarning("フィルタ条件に合致するスキルがありませんでした。UI選択をキャンセルします。");
                        return null;
                    }
                }

                //選択ボタンエリア生成と受け取り
                var result = await PlayersStates.Instance.
                GoToSelectSkillPassiveTargetSkillButtonsArea(targetSkills, SkillPassiveEffectCount);


                if(result.Count == 0)
                {
                    Debug.LogError("スキルパッシブの対象スキルを直接選択しましたが何も返ってきません");
                    return null;
                }
                return result;
            }

        }

        //反応式
        if(TargetSelection == SkillPassiveTargetSelection.Reaction)
        {
            var correctReactSkills = new List<BaseSkill>();

            //そのキャラのターゲットスキルに対して反応する「キャラとスキルの」リストが一致したら
            foreach(var targetSkill in targetSkills)
            {
                foreach(var hold in ReactionCharaAndSkillList)//反応するキャラとスキルのリスト
                {
                    //そもそもキャラ名が違っていたら、飛ばす
                    if(target.CharacterName != hold.CharaName) continue;
                    
                    if(targetSkill.SkillName == hold.SkillName)//スキル名まで一致したら
                    {
                        correctReactSkills.Add(targetSkill);//今回のターゲットスキルを入れる。
                        break;//スキルが一致して、他のスキルネームで検証する必要がなくなったので、次の対象スキルへ
                    }
                }
            }
            if(correctReactSkills.Count == 0)
            {
                Debug.Log("スキルパッシブ付与スキルはどのスキルにも反応しませんでした。");
                return null;
            }
            return correctReactSkills;
        }


        //ランダム方式(フィルタ条件による区切り実装済み)
        if(TargetSelection == SkillPassiveTargetSelection.Random)
        {
            var randomSkills = new List<BaseSkill>();

            if(_skillPassiveGibeSkill_SkillFilter != null && _skillPassiveGibeSkill_SkillFilter.HasAnyCondition)
            {
                // フィルタ条件がある場合：絞り込んでから抽選
                var candidates = targetSkills.Where(s => s.MatchFilter(_skillPassiveGibeSkill_SkillFilter)).ToList();
                if(candidates.Count == 0)
                {
                    Debug.LogWarning("フィルタ条件に合致するスキルがありませんでした。");
                    return null;
                }
                
                int selectCount = Math.Min(SkillPassiveEffectCount, candidates.Count);
                for(int i = 0; i < selectCount; i++)// 絞り込んだスキルリスト からランダム選択
                {
                    var  item = RandomEx.Shared.GetItem(candidates.ToArray());
                    randomSkills.Add(item);
                    candidates.Remove(item);
                }
            }
            else
            {//全体からrandomに一つ(指定個数分)選ぶ単純な方式
                //付与対象のスキル数が指定個数より少ない場合、ループが終わる
                int selectCount = Math.Min(SkillPassiveEffectCount, targetSkills.Count);
                for(int i = 0; i < selectCount; i++)
                {
                    var  item = RandomEx.Shared.GetItem(targetSkills.ToArray());//ランダムに選んで
                    randomSkills.Add(item);//追加
                    targetSkills.Remove(item);//重複を防ぐため削除
                }
            }

            return randomSkills;
        }
        return null;
    }

    public bool MatchFilter(SkillFilter filter)
{
    if (filter == null || !filter.HasAnyCondition) return true;

    // 基本方式の判定
    if (filter.Impressions.Count > 0 && !filter.Impressions.Contains(Impression))//スキル印象
        return false;
    if (filter.MotionFlavors.Count > 0 && !filter.MotionFlavors.Contains(MotionFlavor))//動作的雰囲気
        return false;
    if (filter.MentalAttrs.Count > 0 && !filter.MentalAttrs.Contains(SkillSpiritual))//精神属性
        return false;
    if (filter.PhysicalAttrs.Count > 0 && !filter.PhysicalAttrs.Contains(SkillPhysical))//物理属性
        return false;
    if (filter.AttackTypes.Count > 0 && !filter.AttackTypes.Contains(DistributionType))//スキル分散性質
        return false;

    // b方式の判定
    //十日能力
    if (filter.TenDayAbilities.Count > 0 && 
        !SkillFilterUtil.CheckContain(EnumerateTenDayAbilities(), filter.TenDayAbilities, filter.TenDayMode))
        return false;

    //スキルの攻撃性質
    if (filter.SkillTypes.Count > 0 && 
        !SkillFilterUtil.CheckContain(EnumerateSkillTypes(), filter.SkillTypes, filter.SkillTypeMode))
        return false;

    //スキル特殊判別性質
    if (filter.SpecialFlags.Count > 0 && 
        !SkillFilterUtil.CheckContain(EnumerateSpecialFlags(), filter.SpecialFlags, filter.SpecialFlagMode))
        return false;

    return true;
}
    
    /// <summary>
    /// SkillTypeを列挙可能にする
    /// </summary>
    public IEnumerable<SkillType> EnumerateSkillTypes()
    {
        return SkillFilterUtil.FlagsToEnumerable<SkillType>(SkillType);
    }

    /// <summary>
    /// SpecialFlagを列挙可能にする  
    /// </summary>
    public IEnumerable<SkillSpecialFlag> EnumerateSpecialFlags()
    {
        return SkillFilterUtil.FlagsToEnumerable<SkillSpecialFlag>(SpecialFlags);
    }

    /// <summary>
    /// TenDayAbilityを列挙可能にする
    /// </summary>
    public IEnumerable<TenDayAbility> EnumerateTenDayAbilities()
    {
        return TenDayValues().Keys;
    }


    /// <summary>
    /// ランタイム用にスキルをディープコピーする関数
    /// </summary>
    public void InitDeepCopy(BaseSkill dst)
    {
        dst.SkillSpiritual = SkillSpiritual;
        dst.SkillPhysical = SkillPhysical;
        dst.Impression = Impression;
        /*foreach(var tenDay in TenDayValues)
        {
            dst.TenDayValues.Add(tenDay.Key,tenDay.Value);
        }*///十日能力は有限スキルレベルリストから参照する
        dst._triggerCountMax = _triggerCountMax;
        dst._triggerRollBackCount = _triggerRollBackCount;
        dst._RandomConsecutivePer = _RandomConsecutivePer;
        dst._defaultStockCount = _defaultStockCount;
        dst._stockPower = _stockPower;
        dst._stockForgetPower = _stockForgetPower;
        dst.CanCancelTrigger = CanCancelTrigger;
        dst.IsAggressiveCommit = IsAggressiveCommit;
        dst.CanSelectAggressiveCommit = CanSelectAggressiveCommit;
        dst.SKillDidWaitCount = SKillDidWaitCount;
        dst.SkillName = SkillName;
        dst.SpecialFlags = SpecialFlags;//特殊判別性質
        dst._powerSpread = _powerSpread;//通常の分散割合
        dst._mentalDamageRatio = _mentalDamageRatio;//通常の精神攻撃率
        dst._infiniteSkillPowerUnit = _infiniteSkillPowerUnit;//無限スキルの威力単位
        dst._infiniteSkillTenDaysUnit = _infiniteSkillTenDaysUnit;//無限スキルの10日単位
        dst.FixedSkillLevelData = FixedSkillLevelData;//固定スキルレベルデータ
        //有限スキルレベルリストのディープコピー
        dst.FixedSkillLevelData = new();
        foreach(var levelData in FixedSkillLevelData)
        {
            dst.FixedSkillLevelData.Add(levelData.Clone());
        }
        
        dst.subEffects = new List<int>(subEffects);
        dst.subVitalLayers = new List<int>(subVitalLayers);
        foreach(var moveSet in A_MoveSet_Cash)
        {
            dst.A_MoveSet_Cash.Add(moveSet.DeepCopy());
        }
        foreach(var moveSet in B_MoveSet_Cash)
        {
            dst.B_MoveSet_Cash.Add(moveSet.DeepCopy());
        }
        dst._defAtk = _defAtk;
        dst._baseSkillType = _baseSkillType;
        dst.ConsecutiveType = ConsecutiveType;
        dst.ZoneTrait = ZoneTrait;
        dst.DistributionType = DistributionType;
        dst.PowerRangePercentageDictionary = PowerRangePercentageDictionary;
        foreach (var pair in PowerRangePercentageDictionary)
        {
            dst.PowerRangePercentageDictionary.Add(pair.Key, pair.Value);
        }
        dst.HitRangePercentageDictionary = HitRangePercentageDictionary;
        foreach (var pair in HitRangePercentageDictionary)
        {
            dst.HitRangePercentageDictionary.Add(pair.Key, pair.Value);
        }

        dst.canEraceEffectIDs = new(canEraceEffectIDs);
        dst.canEraceVitalLayerIDs = new(canEraceVitalLayerIDs);
        dst.CanEraceEffectCount = CanEraceEffectCount;
        dst.CanEraceVitalLayerCount = CanEraceVitalLayerCount;
        dst.ReactiveSkillPassiveList = new(ReactiveSkillPassiveList);

    }

}
/// <summary>
/// インスペクタで表示可能なAimStyleのリストの一つのステータスとDEFATKのペア
/// </summary>
[Serializable]
public class MoveSet: ISerializationCallbackReceiver
{
    public List<AimStyle> States = new List<AimStyle>();
    public List<float> DEFATKList = new List<float>();
    public AimStyle GetAtState(int index)
    {
        return States[index];
    }
    public float GetAtDEFATK(int index)
    {
        return DEFATKList[index];
    }
    public MoveSet()
    {
        States.Clear();
        DEFATKList.Clear();
    }
    //Unityインスペクタ上で新しく防御無視率を設定した際に、デフォルトで何も起こらない(=1.0f)値が入るようにするための処理。

    // 旧サイズを保持しておくための変数
    [NonSerialized]
    private int oldSizeDEFATK = 0;

// シリアライズ直前に呼ばれる
    public void OnBeforeSerialize()
    {
        // 新規追加があった場合、そのぶんだけ1.0fを代入
        if (DEFATKList.Count > oldSizeDEFATK)
        {
            for (int i = oldSizeDEFATK; i < DEFATKList.Count; i++)
            {
                // 新しく挿入された分
                DEFATKList[i] = 1.0f;
            }
        }

        // 今回のリストサイズを保存
        oldSizeDEFATK = DEFATKList.Count;
    }

    // デシリアライズ後に呼ばれる
    public void OnAfterDeserialize()
    {
        // 特に何もしない場合は空でOK
    }
    public MoveSet DeepCopy()
    {
        // 新しい MoveSet を生成
        var copy = new MoveSet();

        // List<AimStyle> の中身をまるごとコピー
        // (AimStyle は enum なので値コピーで OK)
        copy.States = new List<AimStyle>(this.States);

        // List<float> の中身をまるごとコピー
        copy.DEFATKList = new List<float>(this.DEFATKList);

        // oldSizeDEFATK は NonSerializedなので
        // 新しい copy では 0 に初期化されるが
        // OnBeforeSerialize が動くときにまた
        // 適切に更新されるので問題なし

        return copy;
    }



}

/// <summary>
/// スキルの区別判定方式
/// </summary>
public enum ContainMode { Any, All }   // b方式のどちらで判定するか
public static class SkillFilterUtil
{
    // Flags列挙体として使われるビットフラグを個別の列挙体に分解
    public static IEnumerable<TEnum> FlagsToEnumerable<TEnum>(Enum flags)
    {
        foreach (TEnum val in Enum.GetValues(typeof(TEnum)))
            if (flags.HasFlag((Enum)(object)val)) yield return val;
    }

    // 条件判定
    public static bool CheckContain<T>(IEnumerable<T> skillValues, 
                                       List<T> filterValues, 
                                       ContainMode mode)
    {
        var filterSet = new HashSet<T>(filterValues);
        return mode == ContainMode.Any 
            ? skillValues.Any(filterSet.Contains)
            : filterSet.All(skillValues.Contains);
    }
}

/// <summary>
/// スキル区別方式
/// 特定のプロパティに応じてスキルを区分けするためのフィルタークラス
/// </summary>
[Serializable]
public class SkillFilter
{
    // —— 基本方式 ——            
/// <summary>スキル印象の区切り</summary>
    public List<SkillImpression> Impressions = new();
/// <summary>動作的雰囲気の区切り</summary>
    public List<MotionFlavorTag> MotionFlavors = new();
/// <summary>精神属性の区切り</summary>
    public List<SpiritualProperty> MentalAttrs = new();
/// <summary>物理属性の区切り</summary>
    public List<PhysicalProperty> PhysicalAttrs = new();
/// <summary>スキルの分散割合タイプの区切り</summary>
    public List<AttackDistributionType> AttackTypes = new();

    // —— b方式（スキル側が複数値を持ち得るもの）——
/// <summary>十日能力の区切り</summary>
    public List<TenDayAbility> TenDayAbilities = new();
    public ContainMode TenDayMode = ContainMode.Any;
    /// <summary>スキルの攻撃性質の区切り</summary>
    public List<SkillType> SkillTypes = new();
    public ContainMode SkillTypeMode = ContainMode.Any;
    /// <summary>スキルの特殊判別性質の区切り</summary>
    public List<SkillSpecialFlag> SpecialFlags = new();
    public ContainMode SpecialFlagMode = ContainMode.Any;

    

    /// <summary>
    /// 条件が 1 つもセットされていない場合は「フィルタ無し」とみなす
    /// </summary>
    public bool HasAnyCondition =>
        Impressions.Count + MotionFlavors.Count + MentalAttrs.Count + PhysicalAttrs.Count +
        AttackTypes.Count + TenDayAbilities.Count + SkillTypes.Count + SpecialFlags.Count > 0;
}
