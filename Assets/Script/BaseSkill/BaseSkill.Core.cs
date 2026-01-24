using R3;
using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

[Serializable]
public partial class BaseSkill
{
    //  ==============================================================================================================================
    //                                              参照
    //  ==============================================================================================================================

    // Phase 2b: 未使用のschizoLogプロパティを削除
    protected IBattleContext manager => BattleContextHub.Current;
    [NonSerialized]
    public BaseStates Doer;//行使者

    //  ==============================================================================================================================
    //                                              基本プロパティ
    //  ==============================================================================================================================

    [Header("基本プロパティ")]

    public string SkillName = "ここに名前を入れてください";

    [Header("スキルの精神属性 Flagsの列挙体なので複数指定できるが、必ず一つの値のみを指定して")]
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
    ///     スキルの物理属性
    /// </summary>
    public PhysicalProperty SkillPhysical;
    /// <summary>
    /// スキルの特殊判別性質
    /// </summary>
    public SkillSpecialFlag SpecialFlags;

    [Header("スキルの攻撃性質は必ず設定")]
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
    [Header("スキルの連続性質も必ず設定")]
    /// <summary>
    /// スキルの連撃性質
    /// </summary>
    public SkillConsecutiveType ConsecutiveType;
    [Header("スキルの範囲性質も必ず設定")]
    /// <summary>
    /// スキルの範囲性質
    /// 初期値は0
    /// </summary>
    public SkillZoneTrait ZoneTrait = 0;//初期値


    /// <summary>
    /// スキルの実行に必要なポイント設定
    /// </summary>
    [Header("スキル実行コスト")]
    /// <summary>
    /// スキル実行に必要なノーマルポイント。
    /// 0ならノーマルP消費なし。
    /// </summary>
    public int RequiredNormalP = 0;
    /// <summary>
    /// スキル実行に必要な属性ポイント内訳。
    /// キー: SpiritualProperty, 値: 必要ポイント。
    /// 空なら属性P消費なし。
    /// </summary>
    public SerializableDictionary<SpiritualProperty, int> RequiredAttrP = new SerializableDictionary<SpiritualProperty, int>();
    [Header("スキル実行に必要な残りHP割合（0〜100）。0で制限なし。")]
    [Tooltip("行使者の現在HPが最大HPに対してこの割合未満の場合は使用不可。")]
    [Range(0f, 100f)]
    public float RequiredRemainingHPPercent = 0f;
    /// <summary>
    /// 殺せないスキルかどうか
    /// 1残る
    /// </summary>
    public bool Cantkill = false;
    [Header("実行したキャラに付与される追加硬直値 バトルで")]
    /// <summary>
    /// 実行したキャラに付与される追加硬直値
    /// </summary>
    public int SKillDidWaitCount;//スキルを行使した後の硬直時間。 Doer、行使者のRecovelyTurnに一時的に加算される？


    [Header("戦闘時補正率")]
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
    [Tooltip("攻撃時に精神HPを回復させる百分率。80なら攻撃力の80%分回復、負値で減少。")]
    public float AttackMentalHealPercent = 80f;

    //  ==============================================================================================================================
    //                                              ディープコピー
    //  ==============================================================================================================================


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
        dst._skillHitPer = _skillHitPer;//基本スキル命中率
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
        dst.RequiredNormalP = RequiredNormalP;//スキルの必要ポインント
        dst.RequiredAttrP = new SerializableDictionary<SpiritualProperty, int>();
        if (RequiredAttrP != null)
        {
            foreach (var kv in RequiredAttrP)
            {
                dst.RequiredAttrP.Add(kv.Key, kv.Value);
            }
        }

        dst.AttackMentalHealPercent = AttackMentalHealPercent;

    }



}





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
