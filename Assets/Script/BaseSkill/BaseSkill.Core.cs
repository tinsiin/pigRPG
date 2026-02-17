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
    // Phase 4: Hub依存削減 - 注入優先、フォールバックでHub
    private IBattleContext _battleContext;
    protected IBattleContext manager => _battleContext ?? BattleContextHub.Current;
    private static readonly IBattleRandom s_fallbackRandom = new SystemBattleRandom();
    protected IBattleRandom RandomSource => manager?.Random ?? s_fallbackRandom;

    public void BindBattleContext(IBattleContext context)
    {
        _battleContext = context;
    }
    /// <summary>
    /// OnInitialize済みかどうか（旧Doer nullチェックの代替）
    /// </summary>
    [NonSerialized]
    public bool IsInitialized;

    //  ==============================================================================================================================
    //                                              基本プロパティ（Phase 2: SkillLevelData直接読み取り）
    //  ==============================================================================================================================

    // ─── プロパティ（Phase 2: FixedSkillLevelData[_levelIndex] 直接読み取り） ───

    public string SkillName => FixedSkillLevelData[_levelIndex].SkillName;
    public SpiritualProperty SkillSpiritual => FixedSkillLevelData[_levelIndex].SkillSpiritual;
    public SkillImpression Impression => FixedSkillLevelData[_levelIndex].Impression;
    public MotionFlavorTag MotionFlavor => FixedSkillLevelData[_levelIndex].MotionFlavor;
    public PhysicalProperty SkillPhysical => FixedSkillLevelData[_levelIndex].SkillPhysical;
    public SkillSpecialFlag SpecialFlags => FixedSkillLevelData[_levelIndex].SpecialFlags;

    /// <summary>
    /// スキルの攻撃性質　バッファ用フィールド
    /// </summary>
    SkillType bufferSkillType;
    /// <summary>
    /// スキルの攻撃性質（ベース + バッファ合成）
    /// </summary>
    public SkillType SkillType
        => FixedSkillLevelData[_levelIndex].BaseSkillType | bufferSkillType;
    /// <summary>一時的に追加する SkillType を設定</summary>
    public void SetBufferSkillType(SkillType skill)
        => bufferSkillType = skill;
    /// <summary>バッファをクリア</summary>
    public void EraseBufferSkillType()
        => bufferSkillType = 0;

    public SkillConsecutiveType ConsecutiveType => FixedSkillLevelData[_levelIndex].ConsecutiveType;
    public SkillZoneTrait ZoneTrait => FixedSkillLevelData[_levelIndex].ZoneTrait;

    public int RequiredNormalP => FixedSkillLevelData[_levelIndex].RequiredNormalP;
    public SerializableDictionary<SpiritualProperty, int> RequiredAttrP => FixedSkillLevelData[_levelIndex].RequiredAttrP;
    public float RequiredRemainingHPPercent => FixedSkillLevelData[_levelIndex].RequiredRemainingHPPercent;
    public bool Cantkill => FixedSkillLevelData[_levelIndex].Cantkill;
    public int SKillDidWaitCount => FixedSkillLevelData[_levelIndex].SkillDidWaitCount;
    public float EvasionModifier => FixedSkillLevelData[_levelIndex].EvasionModifier;
    public float AttackModifier => FixedSkillLevelData[_levelIndex].AttackModifier;
    public float AttackMentalHealPercent => FixedSkillLevelData[_levelIndex].AttackMentalHealPercent;

    //  ==============================================================================================================================
    //                                              ディープコピー
    //  ==============================================================================================================================


    /// <summary>
    /// ランタイム用にスキルをディープコピーする関数
    /// Phase 2: SkillLevelDataが全値を持つため、レベルリストのディープコピーが中心
    /// </summary>
    public void InitDeepCopy(BaseSkill dst)
    {
        // --- スキルレベル（有限リストのディープコピー + 無限単位） ---
        dst.FixedSkillLevelData = new();
        foreach(var levelData in FixedSkillLevelData)
            dst.FixedSkillLevelData.Add(levelData.Clone());
        dst._infiniteSkillPowerUnit = _infiniteSkillPowerUnit;
        dst._infiniteSkillTenDaysUnit = _infiniteSkillTenDaysUnit;

        // bufferSkillType, bufferSubEffects等: 非シリアライズのランタイム一時値なのでデフォルト(0/空)のままでOK
        // A/B_MoveSet_Cash: CashMoveSet()で戦闘開始時にレベルデータから詰められるためコピー不要
        // ReactiveSkillPassiveList / AggressiveSkillPassiveList: 戦闘中に動的に追加されるためコピー不要
    }

    //  ==============================================================================================================================
    //                                              デフォルト値
    //  ==============================================================================================================================

    /// <summary>
    /// Unityがコンポーネント追加時・Inspector右クリック→Reset時に自動呼出。
    /// 設定しないと実行時に例外でクラッシュするフィールドのみを初期化する。
    /// </summary>
    void Reset()
    {
        // FixedSkillLevelData — 空だと_skillPower(), TenDayValues()等でArgumentOutOfRangeException
        if (FixedSkillLevelData == null || FixedSkillLevelData.Count == 0)
            FixedSkillLevelData = new List<SkillLevelData> { new SkillLevelData() };
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
