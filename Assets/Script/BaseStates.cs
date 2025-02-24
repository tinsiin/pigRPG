using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RandomExtensions;
using System;
using UnityEditor.Experimental.GraphView;
using static BattleManager;
using Unity.Burst.CompilerServices;
using static UnityEngine.Rendering.DebugUI;
using UnityEditor.UIElements;
using static CommonCalc;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEditor.Rendering;
using UnityEditor.ShaderGraph.Internal;
/// <summary>
///     キャラクター達の種別
/// </summary>
[Flags]
public enum CharacterType
{
    TLOA = 1 << 0,
    Machine = 1 << 1,
    Life = 1 << 2 //TLOAそのもの、機械、生命
}
/// <summary>
/// スキルの行動記録　リストで記録する
/// </summary>
public class ACTSkillData
{
    public bool IsDone;
    public BaseSkill Skill;
    public BaseStates Target;   
    public ACTSkillData(bool isdone,BaseSkill skill,BaseStates target)
    {
        IsDone = isdone;
        Skill = skill;
        Target = target;
    }
}
/// <summary>
/// 被害記録
/// </summary>
public class DamageData
{
    public BaseStates Attacker;
    /// <summary>
    /// 攻撃自体がヒットしたかどうかで、atktypeなら攻撃で全部ひっくるめてあたるから
    /// atktypeじゃないなら、falseで
    /// </summary>
    public bool IsAtkHit;
    public bool IsBadPassiveHit;
    public bool IsBadPassiveRemove;
    public bool IsGoodPassiveHit;
    public bool IsGoodPassiveRemove;


    public bool IsGoodVitalLayerHit;
    public bool IsGoodVitalLayerRemove;
    public bool IsBadVitalLayerHit;
    public bool IsBadVitalLayerRemove;


    /// <summary>
    /// 死回復も含める
    /// </summary>
    public bool IsHeal;
    //public bool IsConsecutive;　これは必要なし、なぜなら相性値の判断は毎ターン行われるから、連続ならちゃんと連続毎ターンで結果的に多く相性値関連の処理は加算される。
    public float Damage;
    public float Heal;
    //public BasePassive whatPassive;  多いしまだ必要ないから一旦コメントアウト
    //public int DamagePercent　最大HPはBaseStates側にあるからそっちから取得する
    public BaseSkill Skill;
    public DamageData(bool isAtkHit,bool isBadPassiveHit,bool isBadPassiveRemove,bool isGoodPassiveHit,bool isGoodPassiveRemove,bool isGoodVitalLayerHit,bool isGoodVitalLayerRemove,bool isBadVitalLayerHit,bool isBadVitalLayerRemove,bool isHeal,BaseSkill skill,float damage,float heal,BaseStates attacker)
    {
        IsAtkHit = isAtkHit;
        IsBadPassiveHit = isBadPassiveHit;
        IsBadPassiveRemove = isBadPassiveRemove;
        IsGoodPassiveHit = isGoodPassiveHit;
        IsGoodPassiveRemove = isGoodPassiveRemove;
        IsGoodVitalLayerHit = isGoodVitalLayerHit;
        IsGoodVitalLayerRemove = isGoodVitalLayerRemove;
        IsBadVitalLayerHit = isBadVitalLayerHit;
        IsBadVitalLayerRemove = isBadVitalLayerRemove;
        IsHeal = isHeal;

        Skill = skill;
        Damage = damage;
        Heal = heal;
        Attacker = attacker;
    }
}

/// <summary>
///     物理属性、スキルに依存し、キャラクター達の種別や個人との相性で攻撃の通りが変わる
/// </summary>
public enum PhysicalProperty
{
    heavy,
    volten,
    dishSmack //床ずれ、ヴぉ流転、暴断
    ,none
}
/// <summary>
/// 人間状況　全員持つけど例えばLife以外なんかは固定されてたりしたりする。
/// </summary>
public enum HumanConditionCircumstances
{
    /// <summary>
    /// 辛い状態を表します。
    /// </summary>
    Painful,
    /// <summary>
    /// 楽観的な状態を表します。
    /// </summary>
    Optimistic,
    /// <summary>
    /// 高揚した状態を表します。
    /// </summary>
    Elated,
    /// <summary>
    /// 覚悟を決めた状態を表します。
    /// </summary>
    Resolved,
    /// <summary>
    /// 怒りの状態を表します。
    /// </summary>
    Angry,
    /// <summary>
    /// 状況への疑念を抱いている状態を表します。
    /// </summary>
    Doubtful,
    /// <summary>
    /// 混乱した状態を表します。
    /// </summary>
    Confused,
    /// <summary>
    /// 普段の状態を表します。
    /// </summary>
    Normal
    }
/// <summary>
/// パワー、元気、気力値　歩行やその他イベントなどで短期的に上げ下げし、
/// 狙い流れ等の防ぎ方切り替え処理などで、さらに上下する値として導入されたりする。
/// </summary>
public enum ThePower
{
        /// <summary>たるい</summary>
    lowlow,
        /// <summary>低い</summary>
    low,
    /// <summary>普通</summary>
    medium,
    /// <summary>高い</summary>
    high
}
/// <summary>
/// 武器依存の戦闘規格
/// </summary>
public enum BattleProtocol
{
    /// <summary>地味</summary>
    LowKey,
    /// <summary>トライキー</summary>
    Tricky,
    /// <summary>派手</summary>
    Showey,
    /// <summary>
    /// この戦闘規格には狙い流れ(AimStyle)がないため、には防ぎ方(AimStyleごとに対応される防御排他ステ)もなく、追加攻撃力(戦闘規格による排他ステ)もない
    /// </summary>
    none
}
/// <summary>
/// 防ぎ方 狙い流れとも言う　戦闘規格とスキルにセットアップされる順番や、b_defの対応に使用される。
/// </summary>
public enum AimStyle
{

     /// <summary>
    /// アクロバマイナ体術1 - Acrobat Minor Technique 1
    /// </summary>
    AcrobatMinor,       // アクロバマイナ体術1

    /// <summary>
    /// ダブレット - Doublet
    /// </summary>
    Doublet,            // ダブレット

    /// <summary>
    /// 四弾差し込み - Quad Strike Insertion
    /// </summary>
    QuadStrike,         // 四弾差し込み

    /// <summary>
    /// ダスター - Duster
    /// </summary>
    Duster,             // ダスター

    /// <summary>
    /// ポタヌヴォルフのほうき術系 - Potanu Volf's Broom Technique
    /// </summary>
    PotanuVolf,         // ポタヌヴォルフのほうき術系

    /// <summary>
    /// 中天一弾 - Central Heaven Strike
    /// </summary>
    CentralHeavenStrike, // 中天一弾

    /// <summary>
    /// 戦闘規格のnoneに対して変化する防ぎ方
    /// </summary>
    none
}
/// <summary>
/// 命中率、攻撃力、回避力、防御力への補正
/// </summary>
public class ModifierPart
{
    /// <summary>
    /// どういう補正かを保存する　攻撃時にunderに出てくる
    /// </summary>
    public string whatModifier;

    /// <summary>
    /// 補正率
    /// </summary>
    public float Modifier;

    public ModifierPart(string txt, float value)
    {
        whatModifier = txt;
        Modifier = value;
    }
}
/// <summary>
///     精神属性、スキル、キャラクターに依存し、キャラクターは直前に使った物が適用される
    ///     だから精神属性同士で攻撃の通りは設定される。
    /// </summary>
[Flags]
public enum SpiritualProperty
{
    doremis = 1 << 0,   // ビットパターン: 0000 0001  (1)
    pillar = 1 << 1,   // ビットパターン: 0000 0010  (2)
    kindergarden = 1 << 2,   // ビットパターン: 0000 0100  (4)
    liminalwhitetile = 1 << 3,   // ビットパターン: 0000 1000  (8)
    sacrifaith = 1 << 4,   // ビットパターン: 0001 0000  (16)
    cquiest = 1 << 5,   // ビットパターン: 0010 0000  (32)
    pysco = 1 << 6,   // ビットパターン: 0100 0000  (64)
    godtier = 1 << 7,   // ビットパターン: 1000 0000  (128)
    baledrival = 1 << 8,   // ビットパターン: 0001 0000 0000  (256)
    devil = 1 << 9,    // ビットパターン: 0010 0000 0000  (512)
    none = 1 << 10    // ビットパターン: 0100 0000 0000  (1024)
}

public enum MemoryDensity
{
    /// <summary>
    /// 薄い
    /// </summary>
    Low,
    /// <summary>
    /// 普通
    /// </summary>
    Medium,
    /// <summary>
    /// しっかりと
    /// </summary>
    High,
}

/// <summary>
///     基礎ステータスのクラス　　クラスそのものは使用しないので抽象クラス
    /// </summary>
[Serializable]
public abstract class BaseStates
{
    /// <summary>
    /// このキャラの種別と一致してるかどうか
    /// </summary>
    public bool HasCharacterType(CharacterType type)
    {
        return (MyType & type) == type;
    }/// <summary>
     /// このキャラの印象/キャラクタ属性と一致してるかどうか
     /// </summary>
    public bool HasCharacterImpression(SpiritualProperty imp)
    {
        return (MyImpression & imp) == imp;
    }

    protected BattleManager manager => Walking.bm;
    /// <summary>
    /// キャラクターの被害記録
    /// </summary>
    public List<DamageData> damageDatas;
    /// <summary>
    /// キャラクターの行動記録
    /// </summary>
    public List<ACTSkillData> skillDatas;
    /// <summary>
    /// 現在持ってる対象者のボーナスデータ
    /// </summary>
    public TargetBonusDatas TargetBonusDatas ;

    /// <summary>
    /// 直近の行動記録
    /// </summary>
    public ACTSkillData RecentSkillData => skillDatas[skillDatas.Count - 1];
    /// <summary>
    /// 直近の被害記録
    /// </summary>
    public DamageData RecentDamageData => damageDatas[damageDatas.Count - 1];



    [SerializeField] private List<BasePassive> _passiveList;

    [SerializeField] List<BaseSkill> _skillList;

    [SerializeField] List<BaseVitalLayer> _vitalLaerList;

    /// <summary>
    /// 状態異常のリスト
    /// </summary>
    public IReadOnlyList<BasePassive> PassiveList => _passiveList;
    /// <summary>
    /// unityのインスペクタ上で設定したPassiveのIDからキャラが持ってるか調べる。
    /// </summary>
    public bool HasPassive(int id)
    {
        return _passiveList.Any(pas => pas.ID == id);
    }

    /// <summary>
    /// 所持してるリストの中から指定したIDのパッシブを取得する。存在しない場合はnullを返す
    /// </summary>
    /// <param name="passiveId">取得したいパッシブのID</param>
    /// <returns>パッシブのインスタンス。存在しない場合はnull</returns>
    public BasePassive GetPassiveByID(int passiveId)
    {
        return _passiveList.FirstOrDefault(p => p.ID == passiveId);
    }

    /// <summary>
    ///     パッシブを適用
    /// </summary>
    public bool ApplyPassive(int id)
    {
        var status = PassiveManager.Instance.GetAtID(id);//idを元にpassiveManagerから取得

        // 条件(OkType,OkImpression) は既にチェック済みならスキップ
        if (!HasCharacterType(status.OkType)) return false;
        if (!HasCharacterImpression(status.OkImpression)) return false;

        // すでに持ってるかどうか
        var existing = _passiveList.FirstOrDefault(p => p.ID == status.ID);
        if (existing != null)
        {
            // 重ね掛け
            existing.AddPassivePower(1);
        }
        else
        {
            // 新規追加
            _passiveList.Add(status);
            // パッシブ側のOnApplyを呼ぶ
            status.OnApply(this);
        }

        return true;
    }

    /// <summary>
    /// パッシブをIDで除去
    /// </summary>
    void RemovePassiveByID(int id)
    {
        var passive = PassiveManager.Instance.GetAtID(id);
        // パッシブがあるか確認
        if (_passiveList.Remove(passive))
        {
            // パッシブ側のOnRemoveを呼ぶ
            passive.OnRemove(this);
        }
    }

    /// <summary>
    /// パッシブを指定して除去
    /// </summary>
    public void RemovePassive(BasePassive passive)
    {
        // パッシブがあるか確認
        if (_passiveList.Remove(passive))
        {
            // パッシブ側のOnRemoveを呼ぶ
            passive.OnRemove(this);
        }
    }
    /// <summary>
    /// パッシブをidで指定し、存在するかチェックしてから、除去する。
    /// </summary>
    /// <param name="passiveId"></param>
    public void TryRemovePassiveByID(int passiveId)
    {
    if (HasPassive(passiveId))
    {
        RemovePassiveByID(passiveId);
    }
    }

    /// <summary>
    /// 全パッシブのUpdateTurnSurvivalを呼ぶ ターン経過時パッシブが生存するかどうか
    /// </summary>
    void UpdateTurnAllPassiveSurvival()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.UpdateTurnSurvival(this);
        }
    }/// <summary>
    /// 全パッシブのUpdateWalkSurvivalを呼ぶ 歩行時パッシブが生存するかどうか
    /// </summary>
    void UpdateWalkAllPassiveSurvival()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.UpdateWalkSurvival(this);
        }
    }
    /// <summary>
    /// 全パッシブの歩行時効果を呼ぶ
    /// </summary>
    void AllPassiveWalkEffect()
    {
        foreach (var pas in _passiveList)
        {
            if(pas.DurationWalk > 0)
            {
                pas.WalkEffect();//歩行残存ターンが1以上でないと動作しない。
            }
        }
    }


    public IReadOnlyList<BaseVitalLayer> VitalLayers => _vitalLaerList;
    /// <summary>
    /// インスペクタ上で設定されたIDを通じて特定の追加HPを持ってるか調べる
    /// </summary>
    public bool HasVitalLayer(int id)
    {
        return _vitalLaerList.Any(vit => vit.id == id);
    }

    public ThePower NowPower;

    /// <summary>
    /// NowPowerが一段階上がる。
    /// </summary>
    void Power1Up()
    {
        NowPower = NowPower switch
            {
                ThePower.lowlow => ThePower.low,
                ThePower.low => ThePower.medium,
                ThePower.medium => ThePower.high,
                ThePower.high => ThePower.high, // 既に最高値の場合は変更なし
                _ => NowPower//ここはdefault句らしい
            };

    }

    /// <summary>
    /// NowPowerが一段階下がる。
    /// </summary>
    void Power1Down()
    {
        NowPower = NowPower switch
            {
                ThePower.high => ThePower.medium,
                ThePower.medium => ThePower.low,
                ThePower.low => ThePower.lowlow,
                ThePower.lowlow => ThePower.lowlow, // 既に最低値の場合は変更なし
                _ => NowPower//ここはdefault句らしい
            };
    }
    
    [Header("4大ステの基礎基礎値")]
    public float  b_b_atk = 4f;
    public float b_b_def = 4f;
    public float b_b_eye = 4f;
    public float b_b_agi = 4f;

    public SerializableDictionary<TenDayAbility,float> TenDayValues = new SerializableDictionary<TenDayAbility,float>();
    /// <summary>
    /// 十日能力の総量
    /// </summary>
    public float TenDayValuesSum => TenDayValues.Values.Sum();

    public float b_AGI
    {
        get
        {
            var calcAGI = 0f;

            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.FlameBreathingWife) * 0.3f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.Taraiton) * 0.3f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire) * 0.9f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.HeavenAndEndWar) * 1.0f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.FaceToHand) * 0.2f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.Vail) * 0.1f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.Vond) * 0.4f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.HeatHaze) * 0.6f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.WaterThunderNerve) * 0.6f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.PersonaDivergence) * 0.2f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.SilentTraining) * 0.02f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.Pilmagreatifull) * 0.2f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.SpringNap) * 0.03f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness) * 0.1f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.ElementFaithPower) * 0.04f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.ColdHeartedCalm) * 0.1f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.UnextinguishedPath) * 0.14f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.Raincoat) * 0.1f;
            calcAGI += TenDayValues.GetValueOrZero(TenDayAbility.Baka) * 2f;

            return b_b_agi + calcAGI;
        }
    }
    /// <summary>
    /// 攻撃力を十日能力とb_b_atkから計算した値
    /// </summary>
    public float b_ATK
    {
        get 
        {
            var calcATK = 0f;

            //共通の十日能力をまず加算する。
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.FlameBreathingWife) * 0.5f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire) * 0.8f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.HeavenAndEndWar) * 0.3f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Rain) * 0.058f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.FaceToHand) * 0.01f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.StarTersi) * 0.02f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.dokumamusi) * 0.4f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.HeatHaze) * 0.0666f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Leisure) * 0.01f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.SilentTraining) * 0.2f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Pilmagreatifull) * 0.56f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness) * 0.09f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.NightInkKnight) * 0.45f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.ElementFaithPower) * 0.04f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.JoeTeeth) * 0.5f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Blades) * 1.0f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Glory) * 0.1f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Smiler) * 0.02f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.ColdHeartedCalm) * 0.23f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Enokunagi) * 3f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Raincoat) * 22f;
            calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Baka) * -11f;

            //戦闘規格により分岐する
            switch (NowBattleProtocol)
            {
                case BattleProtocol.LowKey:
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Taraiton) * 0.9f;
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.SpringWater) * 1.7f;
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.HumanKiller) * 1.0f;
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.UnextinguishedPath) * 0.3f;
                    break;
                case BattleProtocol.Tricky:
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Miza) * 1.2f;
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.PersonaDivergence) * 0.8f;
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Vond) * 0.7f;
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Enokunagi) * 0.5f;
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Rain) * 0.6f;
                    break;
                case BattleProtocol.Showey:
                 calcATK += TenDayValues.GetValueOrZero(TenDayAbility.Vail) * 1.11f;
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.WaterThunderNerve) * 0.2f;
                    calcATK += TenDayValues.GetValueOrZero(TenDayAbility.HumanKiller) * 1.0f;
                    break;
                //noneの場合、そもそもこの追加攻撃力がない。
            }

            
            
            return b_b_atk + calcATK;
        }
    }
    /// <summary>
    /// 指定したAimStyleでの基礎防御力を計算する
    /// </summary>
    private float CalcBaseDefenseForAimStyle(AimStyle style)
    {
        float calcDEF = 0;

                    // 共通の十日能力をまず加算
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.FlameBreathingWife) * 1.0f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.NightInkKnight) * 1.3f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Raincoat) * 1.0f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.JoeTeeth) * 0.8f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.HeavenAndEndWar) * 0.3f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Vond) * 0.34f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.HeatHaze) * 0.23f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Pilmagreatifull) * 0.38f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Leisure) * 0.47f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Blades) * 0.3f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire) * 0.01f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Rain) * 0.2f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.FaceToHand) * 0.013f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Vail) * 0.02f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.StarTersi) * 0.04f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.SpringWater) * 0.035f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.SilentTraining) * 0.09f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness) * 0.01f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.HumanKiller) * 0.07f;
            calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Baka) * -0.1f;


        
        switch (style)
        {
                case AimStyle.CentralHeavenStrike: // 中天一弾
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Smiler) * 0.78f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.CryoniteQuality) * 1.0f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.SilentTraining) * 0.4f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Vail) * 0.5f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.JoeTeeth) * 0.9f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.ElementFaithPower) * 0.3f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness) * 0.1f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire) * 0.6f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.SpringNap) * -0.3f;
                break;

                case AimStyle.AcrobatMinor: // アクロバマイナ体術1
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.ColdHeartedCalm) * 1.0f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Taraiton) * 0.1f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Blades) * 1.1f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.StarTersi) * 0.1f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness) * 0.3f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.WaterThunderNerve) * 0.6f;
                    break;

                case AimStyle.Doublet: // ダブレット
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.HeatHaze) * 0.7f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Sort) * 0.3f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.SpringNap) * 0.4f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.NightInkKnight) * 0.3f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire) * 1.0f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Vond) * 0.2f;
                    break;

                case AimStyle.QuadStrike: // 四弾差し込み
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.SpringNap) * 1.0f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Rain) * 0.2f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.SpringWater) * 0.3f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Vond) * 0.6f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Enokunagi) * 0.5f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Vond) * 0.17f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.TentVoid) * 0.4f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness) * -0.2f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.ColdHeartedCalm) * -1.0f;
                    break;

                case AimStyle.Duster: // ダスター
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Miza) * 0.6f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Glory) * 0.8f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.TentVoid) * -0.2f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.WaterThunderNerve) * -0.2f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Raincoat) * 0.4f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Sort) * 0.1f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.SilentTraining) * 0.4f;
                    break;

                case AimStyle.PotanuVolf: // ポタヌヴォルフのほうき術
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Taraiton) * 0.4f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness) * 0.2f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Pilmagreatifull) * 1.4f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.WaterThunderNerve) * 0.2f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire) * -0.2f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.StarTersi) * 0.3f;
                    calcDEF += TenDayValues.GetValueOrZero(TenDayAbility.Vond) * -0.2f;
                    break;
                //none 掴んで投げるスキルの場合はこの排他ステはない。
        }
        
        return calcDEF;
    }


    /// <summary>
    /// 基礎攻撃防御　(大事なのは、基本的にこの辺りは超スキル依存なの)
    /// オプションのAimStyleに値を入れるとそのAimStyleでシミュレート
    /// </summary>
    /// <param name="SimulateAimStyle"></param>
    /// <returns></returns>
    public float b_DEF(AimStyle? SimulateAimStyle = null)
    {
        var calcDEF = 0f;

        if(SimulateAimStyle == null)
        {
            calcDEF += CalcBaseDefenseForAimStyle(NowDeffenceStyle);
        }
        else
        {
            calcDEF += CalcBaseDefenseForAimStyle(SimulateAimStyle.Value);

        }

        return b_b_def + calcDEF;
    }
    public float b_EYE
    {
        get
        {
            var calcEYE = 0f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.FlameBreathingWife) * 0.2f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.Taraiton) * 0.2f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.Rain) * 0.1f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.FaceToHand) * 0.8f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.Vail) * 0.25f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.StarTersi) * 0.6f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.SpringWater) * 0.04f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.dokumamusi) * 0.1f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.WaterThunderNerve) * 1.0f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.Leisure) * 0.1f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.PersonaDivergence) * 0.02f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.TentVoid) * 0.3f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.Sort) * 0.6f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.Pilmagreatifull) * 0.01f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.SpringNap) * 0.04f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.ElementFaithPower) * 0.001f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.Miza) * 0.5f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.JoeTeeth) * 0.03f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.ColdHeartedCalm) * 0.2f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.NightInkKnight) * 1.0f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.HumanKiller) * 0.2f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.CryoniteQuality) * 0.3f;
            calcEYE += TenDayValues.GetValueOrZero(TenDayAbility.Enokunagi) * -0.5f;

            return b_b_eye + calcEYE;
        }
    }
    /// <summary>
    ///     このキャラクターの名前
    /// </summary>
    public string CharacterName;

    /// <summary>
    /// 裏に出す種別も考慮した彼のことの名前
    /// </summary>
    public string ImpressionStringName;
    /// <summary>
    /// 装備中の武器
    /// </summary>
    public BaseWeapon NowUseWeapon;
    /// <summary>
    /// 今のキャラの戦闘規格
    /// </summary>
    public BattleProtocol NowBattleProtocol;
    /// <summary>
    /// 狙い流れに対する防ぎ方プロパティ
    /// </summary>
    public AimStyle NowDeffenceStyle;

    /// <summary>
    /// 狙い流れ(AimStyle)に対する短期記憶
    /// </summary>
    private AimStyleMemory _aimStyleMemory;

    /// <summary>
    ///現在のの攻撃ターンで使われる
    /// </summary>
    public BaseSkill NowUseSkill;

    /// <summary>
    /// 強制続行中のスキル　nullならその状態でないということ
    /// </summary>
    public BaseSkill FreezeUseSkill;
    /// <summary>
    /// 前回使ったスキルの保持
    /// </summary>
    private BaseSkill _tempUseSkill;
    /// <summary>
    /// スキルを連続実行した回数などをスキルのクラスに記録する関数
    /// </summary>
    /// <param name="useSkill"></param>
    public void SkillUseConsecutiveCountUp(BaseSkill useSkill)
    {
        useSkill.SkillHitCount();//スキルのヒット回数の計算

        if (useSkill == _tempUseSkill)//前回使ったスキルと同じなら
        {
            useSkill.DoConsecutiveCount++;//連続実行回数を増やす
            useSkill.HitConsecutiveCount++;//連続ヒット回数を増やす
        }
        else//違ったら
        {
            if (_tempUseSkill != null)//nullじゃなかったら
            {
                _tempUseSkill.DoConsecutiveCount = 0;//リセット
                _tempUseSkill.HitConsecutiveCount++;//連続ヒット回数をリセット　
            }
            useSkill.DoConsecutiveCount++;//最初の一回目として
            useSkill.HitConsecutiveCount++;//連続ヒット回数を増やす
        }
    }

        /// <summary>
    /// 現在の自分自身の実行中のFreezeConsecutiveを削除するかどうかのフラグ
    /// </summary>
    public bool IsDeleteMyFreezeConsecutive;
    
    public int MAXP;

    //ポイント
    public int P;
    /// <summary>
    /// 前回ターンが前のめりかの記録
    /// </summary>
    public bool _tempVanguard;
    /// <summary>
    /// 前回ターンに生きてたかどうかの比較のため
    /// </summary>
    public bool _tempLive;

    /// <summary>
    ///     リカバリターン/再行動クールタイムの設定値。
    /// </summary>
    public int maxRecoveryTurn;

    /// <summary>
    ///     recovelyTurnの基礎バッキングフィールド
    /// </summary>
    private int recoveryTurn;

    /// <summary>
    /// skillDidWaitCountなどで一時的に通常recovelyTurnに追加される一時的に再行動クールタイム/追加硬直値
    /// </summary>
    private int _tmpTurnsToAdd;
    /// <summary>
    /// 一時的に必要ターン数から引く短縮ターン
    /// </summary>
    private int _tmpTurnsToMinus;
    /// <summary>
    /// 一時保存用のリカバリターン判別用の前ターン変数
    /// </summary>
    private int _tmp_EncountTurn;
    /// <summary>
    /// recovelyTurnTmpMinusという行動クールタイムが一時的に短縮
    /// </summary>
    public void RecovelyTurnTmpMinus(int MinusTurn)
    {
        _tmpTurnsToMinus += MinusTurn;
    }
    /// <summary>
    /// recovelyCountという行動クールタイムに一時的に値を加える
    /// </summary>
    public void RecovelyCountTmpAdd(int addTurn)
    {
        if(!IsActiveCancelInSkillACT)//行動がキャンセルされていないなら
        {
            _tmpTurnsToAdd += addTurn;
        }
    }
    /// <summary>
    /// このキャラが戦場にて再行動を取れるかどうかと時間を唱える関数
    /// </summary>
    public bool RecovelyBattleField(int nowTurn)
    {
        var difference = Math.Abs(nowTurn - _tmp_EncountTurn);//前ターンと今回のターンの差異から経過ターン
        //もし前のめりならば、二倍で進む
        if(manager.IsVanguard(this))
        {
            difference *= 2;
        }

        _tmp_EncountTurn = nowTurn;//今回のターンを次回の差異計算のために一時保存
        if ((recoveryTurn += difference) >= maxRecoveryTurn + _tmpTurnsToAdd -_tmpTurnsToMinus)//累計ターン経過が最大値を超えたら
        {
            //ここでrecovelyTurnを初期化すると　リストで一括処理した時にカウントアップだけじゃなくて、
            //選ばれたことになっちゃうから、0に初期化する部分はBattleManagerで選ばれた時に処理する。
            return true;
        }
        return false;
    }
    /// <summary>
    /// 戦場へ参戦回復出来るまでのカウントスタート
    /// </summary>
    public void RecovelyWaitStart()
    {
        recoveryTurn = 0;
        RemoveRecovelyTmpAddTurn();//一時追加ターンをリセット
        RemoveRecovelyTmpMinusTurn();//一時短縮ターンをリセット
    }
    /// <summary>
    /// キャラに設定された追加硬直値をリセットする
    /// </summary>
    public void RemoveRecovelyTmpAddTurn()
    {
        _tmpTurnsToAdd = 0;
    }
    /// <summary>
    /// キャラに設定された再行動短縮ターンをリセットする
    /// </summary>
    public void RemoveRecovelyTmpMinusTurn()
    {
        _tmpTurnsToMinus = 0;
    }
    /// <summary>
    /// 戦場へ参戦回復が出来るようにする
    /// </summary>
    public void RecovelyOK()
    {
        recoveryTurn = maxRecoveryTurn;
    }

    //HP
    [SerializeField]
    private float _hp;
    public float HP
    {
        get { return _hp; }
        set
        {
            if (value > MAXHP)//最大値を超えないようにする
            {
                _hp = MAXHP;
            }
            else _hp = value;
        }
    }
    [SerializeField]
    private float _maxHp;
    public float MAXHP => _maxHp;

    //精神HP
    [SerializeField]
    float _mentalHP;
    /// <summary>
    /// 精神HP
    /// </summary>
    public float MentalHP 
    {
        get 
        {
            if(_mentalHP > MentalMaxHP)//最大値超えてたらカットする。
            {
                _mentalHP = MentalMaxHP;
            }
            return _mentalHP;
        }
        set
        {
            if(value > MentalMaxHP)//最大値を超えないようにする。
            {
                _mentalHP = MentalMaxHP;
            }
            else _mentalHP = value;
        }
    }
    /// <summary>
    /// 精神HP最大値
    /// </summary>
    public float MentalMaxHP => CalcMentalMaxHP();

    /// <summary>
    /// 精神HPの最大値を設定する　パワーでの分岐やHP最大値に影響される
    /// </summary>
    float CalcMentalMaxHP()
    {
        if(NowPower == ThePower.high)
        {
            return _hp * 1.3f + _maxHp *0.08f;
        }else
        {
            return _hp;
        }
    }
    /// <summary>
    /// 精神HPは攻撃時にb_atk分だけ回復する
    /// </summary>
    void MentalHealOnAttack()
    {
        MentalHP += b_ATK;
    }
    void MentalHPHealOnTurn()
    {
        MentalHP += TenDayValues.GetValueOrZero(TenDayAbility.Rain);
    }
    /// <summary>
    /// 実HPに比べて何倍離れているのだろうか。
    /// </summary>
    /// <returns></returns>
    float GetMentalDivergenceThreshold()
    {
        var ExtraValue = (TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness) - TenDayValues.GetValueOrZero(TenDayAbility.KereKere)) * 0.01f;//0クランプいらない
        var EnokunagiValue = TenDayValues.GetValueOrZero(TenDayAbility.Enokunagi) * 0.005f;
        switch (NowCondition)
        {
            case HumanConditionCircumstances.Angry:
                return 0.47f + ExtraValue;
            case HumanConditionCircumstances.Elated:
                return 2.6f+ ExtraValue;
            case HumanConditionCircumstances.Painful:
                return 0.6f+ ExtraValue;
            case HumanConditionCircumstances.Confused:
                return 0.3f+ ExtraValue;
            case HumanConditionCircumstances.Resolved:
                return 1.2f+ ExtraValue;
            case HumanConditionCircumstances.Optimistic:
                return 1.4f+ ExtraValue;
            case HumanConditionCircumstances.Normal:
                return 0.9f+ ExtraValue;
            case HumanConditionCircumstances.Doubtful:
                return 0.7f+ ExtraValue - EnokunagiValue;//疑念だとエノクナギの影響で乖離しやすくなっちゃうよ
            default:
                return 0f;
        }
    }
    /// <summary>
    /// 精神HPの乖離が起こるまでの発動持続ターン最大値を取得
    /// </summary>
    int GetMentalDivergenceMaxCount()
    {
        if(TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness)> 0)//ゼロ除算対策
        {
            var maxCount = (int)((TenDayValues.GetValueOrZero(TenDayAbility.SpringNap) - TenDayValues.GetValueOrZero(TenDayAbility.TentVoid ) / 2) / TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness));
            if(maxCount > 0)return maxCount;//0より大きければ返す
        }
        return 0 ;

    }
    /// <summary>
    /// 精神HPと実HPの乖離発生処理全般
    /// </summary>
    void MentalDiverGence()
    {
        // 乖離率は 実HPに対する精神HPの割合で決まる。
        float divergenceRatio = Mathf.Abs(MentalHP - HP) / HP;

        if(divergenceRatio > GetMentalDivergenceThreshold())//乖離してるなら
        {
            if(_mentalDivergenceCount >= GetMentalDivergenceMaxCount())//カウントが最大値を超えたら
            {
                _mentalDivergenceRefilCount = GetMentalDivergenceRefulMaxCount();//再度行われないようにカウント開始
                //精神HPが現在HPより上に乖離してるなら アッパー系の乖離メゾット
                if(MentalHP > HP)
                {
                    MentalUpperDiverGenceEffect();
                }else
                {//精神HPが現在HPより下に乖離してるなら ダウナ系の乖離メゾット
                    MentalDownerDiverGenceEffect();
                }
            }
            _mentalDivergenceCount++;//持続カウントをプラス
        }else
        {
            _mentalDivergenceCount = 0;//乖離から外れたらカウントをリセット
        }

    }
    /// <summary>
    /// 精神HPの乖離の再充填までのターン数を取得
    /// </summary>
    int GetMentalDivergenceRefulMaxCount()
    {
        var refil = TenDayValues.GetValueOrZero(TenDayAbility.TentVoid) * 3 - TenDayValues.GetValueOrZero(TenDayAbility.Miza) / 4 * TenDayValues.GetValueOrZero(TenDayAbility.Smiler);
        if(refil < 0)return 0;
        return (int)refil;
    }
    /// <summary>
    /// 再充填カウントがゼロより多いならばカウントダウンし、そうでなければtrue、つまり再充填されている。
    /// </summary>
    /// <returns></returns>
    bool IsMentalDiverGenceRefilCountDown()
    {
        if(_mentalDivergenceRefilCount > 0)
        {
            _mentalDivergenceRefilCount--;
            return true;
        }
        return false;//カウントは終わっている。
    }
    int _mentalDivergenceRefilCount = 0;
    int _mentalDivergenceCount = 0;

    /// <summary>
    /// 精神HPのアッパー乖離で起こる変化
    /// </summary>
    protected virtual void MentalUpperDiverGenceEffect()
    {//ここに書かれるのは基本効果
        ApplyPassive(4);//アッパーのパッシブを付与
    }
    /// <summary>
    /// 精神HPのダウナー乖離で起こる変化
    /// </summary>
    protected virtual void MentalDownerDiverGenceEffect()
    {//ここに書かれるのは基本効果
        
        if(MyType == CharacterType.TLOA)
        {
            HP = _hp * 0.76f;
        }else
        {//TLOA以外の種別なら
            ApplyPassive(3);//強制ダウナーのパッシブを付与
            if(rollper(50))
            {
                Power1Down();//二分の一でパワーが下がる。
            }
        }
    }
    

    /// <summary>
    /// vitalLayerでHPに到達する前に攻撃値を請け負う処理
    /// </summary>
    public float BarrierLayers(ref float dmg, ref float mentalDmg,BaseStates atker)
    {

        // 1) VitalLayer の順番どおりにダメージを適用していく
        //    ここでは「Priority が低い方(手前)が先に処理される想定」を前提に
        //    _vitalLaerList がすでに正しい順序でソートされていることを期待。

        for (int i = 0; i < _vitalLaerList.Count;)
        {
            var layer = _vitalLaerList[i];
            var skillPhy = atker.NowUseSkill.SkillPhysical;
            // 2) このレイヤーに貫通させて、返り値を「残りダメージ」とする
            layer.PenetrateLayer(ref dmg, ref mentalDmg, skillPhy);

            if (layer.LayerHP <= 0f)
            {
                // このレイヤーは破壊された
                _vitalLaerList.RemoveAt(i);
                // リストを削除したので、 i はインクリメントしない（要注意）
                //破壊慣れまたは破壊負け
                var kerekere = TenDayValues.GetValueOrZero(TenDayAbility.KereKere);
                if (skillPhy == PhysicalProperty.heavy)//暴断なら破壊慣れ
                {
                    dmg += dmg * 0.015f * kerekere;
                }
                if (skillPhy == PhysicalProperty.volten)//vol天なら破壊負け
                {
                    dmg -= dmg * 0.022f * (atker.b_ATK - kerekere);
                    //b_atk < kerekereになった際、減らずに逆に威力が増えるので、そういう場合の演出差分が必要
                }
            }
            else
            {
                // レイヤーが残ったら i を進める
                i++;
            }

            // 3) dmg が 0 以下になったら、もうこれ以上削る必要ない
            if (dmg <= 0f)
            {
                dmg = 0f;
                break;
            }
        }

        // 4) 層で削りきれなかった分を戻す
        if (dmg < 0) dmg = 0;//0未満チェック
        return dmg;
    }

    /// <summary>
    /// このキャラがどの辺りを狙っているか
    /// </summary>
    public DirectedWill Target;

    /// <summary>
    /// このキャラの現在の範囲の意思　　複数持てる
    /// スキルの範囲性質にcanSelectRangeがある場合のみ、ない場合はskillのzoneTraitをそのまま代入される。
    /// </summary>
    public SkillZoneTrait RangeWill;

    /// <summary>
    /// スキル範囲性質を持ってるかどうか
    /// 複数指定した場合は全て当てはまってるかどうかで判断
    /// </summary>
    public bool HasRangeWill(params SkillZoneTrait[] skills)
    {
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }
        return (RangeWill & combinedSkills) == combinedSkills;
    }

    /// <summary>
    /// 指定されたスキルフラグのうち、一つでもRangeWillに含まれている場合はfalseを返し、
    /// 全く含まれていない場合はtrueを返します。
    /// </summary>
    public bool DontHasRangeWill(params SkillZoneTrait[] skills)
    {
        // 受け取ったスキルフラグをビット単位で結合
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }

        // RangeWillに含まれるフラグとcombinedSkillsのビットAND演算
        // 結果が0でなければ、一つ以上のフラグが含まれている
        bool containsAny = (RangeWill & combinedSkills) != 0;

        // 一つでも含まれていればfalse、含まれていなければtrueを返す
        return !containsAny;
    }



    /// <summary>
    /// 使用中のスキルを強制続行中のスキルとする。　
    /// 例えばスキルの連続実行中の処理や発動カウント中のキャンセル不可能なスキルなどで使う
    /// </summary>
    public void FreezeSkill()
    {
        FreezeUseSkill = NowUseSkill;
    }
    /// <summary>
    /// 強制続行中のスキルをなくす
    /// </summary>
    public void Defrost()
    {
        FreezeUseSkill = null;
    }

    /// <summary>
    /// SkillACT内(damage関数やReactionSkill)などで行動をキャンセルされたかどうか。
    /// </summary>
    /// <returns></returns>
    public bool IsActiveCancelInSkillACT;

    /// <summary>
    ///     このキャラクターの種別
    /// </summary>
    public CharacterType MyType { get; }


    /// <summary>
    ///     このキャラクターの属性 精神属性が入る
    /// </summary>
    public SpiritualProperty MyImpression { get; private set; }

    /// <summary>
    ///     このキャラクターの"デフォルト"属性 精神属性が入る
    ///     一定数歩行するとMyImpressionがこれに戻る
    ///     当然この属性自体もゲーム中で変化する可能性はある。
    /// </summary>
    public SpiritualProperty DefaultImpression;

    /// <summary>
    /// 現在のこのキャラの人間状況
    /// </summary>
    HumanConditionCircumstances NowCondition;
    /// <summary>
    /// 前回の人間状況　同じのが続いてるかの判断要
    /// </summary>
    HumanConditionCircumstances PreviousCondition;
    /// <summary>
    /// 人間状況の続いてるターン　想定連続ターン
    /// </summary>
    int ConditionConsecutiveTurn;
    /// <summary>
    /// 人間状況の累積連続ターン　強制変化用
    /// </summary>
    int TotalTurnsInSameCondition;
    /// <summary>
    /// 人間状況の短期継続ターンをリセットする
    /// </summary>
    void ResetConditionConsecutiveTurn()
    {
        ConditionConsecutiveTurn = 0;
    }
    /// <summary>
    /// 人間状況のターン変数をすべてリセット
    /// </summary>
    void ResetConditionTurns()
    {
        ConditionConsecutiveTurn = 0;
        TotalTurnsInSameCondition = 0;
    }
    /// <summary>
    /// 人間状況が変わった際に必要な処理
    /// </summary>
    void ConditionTransition()
    {
            PreviousCondition = NowCondition;
            ResetConditionTurns();
    }
    /// <summary>
    /// 人間状況の次のターンへの変化
    /// </summary>
    void ConditionInNextTurn() 
    {
        // 状態が変わってたら
        if (PreviousCondition != NowCondition)
        {
            ConditionTransition();
        }else
        {//変わってなければターン経過
            ConditionConsecutiveTurn++;
            TotalTurnsInSameCondition++;
        }

        //ターン数が増えた後に時間変化の関数を実行  
        ApplyConditionChangeOnTimePass();
    }
    /// <summary>
    /// 戦闘開始時に決まる人間状況の初期値
    /// </summary>
    public void ApplyConditionOnBattleStart(float eneTenDays)
    {
        var myTenDays = TenDayValuesSum;
        // 安全策として、0除算を避ける
        float ratio = (eneTenDays == 0) 
            ? 999999f // 敵が0なら自分が勝ってる扱い(∞倍勝ち)
            : myTenDays / eneTenDays;

        // パワー(NowPower)は ThePower 型 (lowlow, low, medium, high など)
        // MyImpression は精神属性

        // 初期値はとりあえず普調にしておいて、後で条件を満たせば上書きする
        NowCondition = HumanConditionCircumstances.Normal;

        switch (MyImpression)
        {
            //--------------------------------
            // 1) ベール (baledrival)
            //--------------------------------
            case SpiritualProperty.baledrival:
                // 「高揚」：パワーが高 && 2倍負け( ratio <= 0.5 )
                if (NowPower == ThePower.high && ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                }
                else
                {
                    // それ以外は「楽観的」
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                break;

            //--------------------------------
            // 2) デビル (devil)
            //--------------------------------
            case SpiritualProperty.devil:
                // 「高揚」：1.8倍勝ち ( ratio >= 1.8 )
                if (ratio >= 1.8f)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                }
                else
                {
                    // それ以外 => 「普調」 (疑念にはならない)
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 3) 自己犠牲 (sacrifaith)
            //--------------------------------
            case SpiritualProperty.sacrifaith:
                // 覚悟：パワーが low より上(=low以上) かつ 2倍負け( ratio <= 0.5 )
                //   ※「パワーがlow“以上”」= (low, medium, highのいずれか)
                if (NowPower >= ThePower.low && ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Resolved;
                }
                // 疑念：パワーがlowlow && 1.6倍負け( ratio <= 1/1.6≒0.625 )
                else if (NowPower == ThePower.lowlow && ratio <= 0.625f)
                {
                    NowCondition = HumanConditionCircumstances.Doubtful;
                }
                else
                {
                    // それ以外 => 普調
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 4) ゴッドティア (godtier)
            //--------------------------------
            case SpiritualProperty.godtier:
                // 「楽観的」: 総量2.5倍勝ち( ratio >= 2.5 )
                if (ratio >= 2.5f)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                // 「覚悟」 : パワーがmedium以上 && 2倍負け( ratio <= 0.5 )
                else if (NowPower >= ThePower.medium && ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Resolved;
                }
                else
                {
                    // それ以外 => 普調
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 5) リーミナルホワイトタイル (liminalwhitetile)
            //--------------------------------
            case SpiritualProperty.liminalwhitetile:
                // 「楽観的」: 総量2倍勝ち( ratio >= 2.0 )
                if (ratio >= 2.0f)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                // 「疑念」 : 2倍負け( ratio <= 0.5 )
                else if (ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Doubtful;
                }
                else
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 6) キンダーガーデン (kindergarden)
            //--------------------------------
            case SpiritualProperty.kindergarden:
                // 「楽観的」: 1.7倍勝ち
                if (ratio >= 1.7f)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                // 「疑念」 : 1.5倍負け ( ratio <= 2/3 = 0.6667 )
                else if (ratio <= 0.6667f)
                {
                    NowCondition = HumanConditionCircumstances.Doubtful;
                }
                else
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 7) 支柱 (pillar) 
            //    戦闘開始時は「普調」だけ
            //--------------------------------
            case SpiritualProperty.pillar:
                NowCondition = HumanConditionCircumstances.Normal;
                break;

            //--------------------------------
            // 8) サイコパス (pysco)
            //    戦闘開始時は常に落ち着く => 普調
            //--------------------------------
            case SpiritualProperty.pysco:
                NowCondition = HumanConditionCircumstances.Normal;
                break;

            //--------------------------------
            // 9) ドレミス, シークイエスト, etc. 
            //    仕様外 or 未指定なら一旦「普調」にする
            //--------------------------------
            default:
                NowCondition = HumanConditionCircumstances.Normal;
                break;
        }
    }
    /// <summary>
    /// 人間状況の時間変化
    /// </summary>
    void ApplyConditionChangeOnTimePass()
    {
        bool changed = false; // 状態が変化したかどうか

        switch (NowCondition)
        {
            case HumanConditionCircumstances.Resolved:
                // 覚悟 → 高揚 (想定17)
                if (ConditionConsecutiveTurn >= 17)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Angry:
                // 怒り → 普調 (想定10)
                if (ConditionConsecutiveTurn >= 10)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                // 怒り → 高揚 (累積23)
                else if (TotalTurnsInSameCondition >= 23)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Doubtful:
                // 疑念 → 楽観的 (想定11)
                if (ConditionConsecutiveTurn >= 11)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                    changed = true;
                }
                // 疑念 → 混乱 (累積19)
                else if (TotalTurnsInSameCondition >= 19)
                {
                    NowCondition = HumanConditionCircumstances.Confused;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Confused:
                // 混乱 → 普調 (想定11)
                if (ConditionConsecutiveTurn >= 11)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                // 混乱 → 高揚 (累積22)
                else if (TotalTurnsInSameCondition >= 22)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Elated:
                // 高揚 → 普調 (想定13)
                if (ConditionConsecutiveTurn >= 13)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Painful:
                // 辛い → 普調 (想定14)
                if (ConditionConsecutiveTurn >= 14)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                break;

            // 楽観的, 普調 などは今回の仕様では変化しないので何もしない
            default:
                break;
        }

        if (changed)
        {
            ConditionTransition();
        }
    }
        
    /// <summary>
    /// 相性値の高い仲間が死んだ際の人間状況の変化
    /// </summary>
    public void ApplyConditionChangeOnCloseAllyDeath(int deathCount)
    {
        if(MyType == CharacterType.Life)//基本的に生命のみ
        {
            switch (NowCondition)//死によって、どの状況からどの状況へ変化するか
            {
                case HumanConditionCircumstances.Painful://辛い
                    NowCondition = HumanConditionCircumstances.Confused;//辛いと誰でも混乱する
                    break;
                case HumanConditionCircumstances.Optimistic://楽観的
                    switch(MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                            if(rollper(36))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.pysco:
                            if(deathCount > 1)
                            {//二人なら危機感を感じて普調になる
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount > 1)
                            {//二人なら怒り
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                //そうでないなら変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.devil:
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Angry;
                        break;
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount>1 && rollper(10))
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                    }
                    break;
                case HumanConditionCircumstances.Elated:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount>1)
                            {//二人なら混乱
                                NowCondition = HumanConditionCircumstances.Confused;
                            }else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            NowCondition = HumanConditionCircumstances.Confused;
                        break;
                        case SpiritualProperty.doremis:
                            if(deathCount == 1)
                            {//一人なら混乱する
                                NowCondition = HumanConditionCircumstances.Confused;
                            }else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.devil:
                            NowCondition = HumanConditionCircumstances.Normal;
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし　
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.pillar:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし　
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        //シークイエストとサイコパスは変化なし
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pysco:
                            ResetConditionConsecutiveTurn();//変化なし
                            break;
                    }
                    break;
                case HumanConditionCircumstances.Resolved:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.devil:
                            if(deathCount>1)
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(44))
                            {
                                NowCondition =HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        //後は全て変化なし
                        default:
                            ResetConditionConsecutiveTurn();
                            break;
                    }
                    break;
                case HumanConditionCircumstances.Angry:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.devil:
                            if(rollper(66.66f))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(28))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.sacrifaith:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;
                        //後は全て変化なし
                        default:
                            ResetConditionConsecutiveTurn();
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Doubtful:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.sacrifaith:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                        break;
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount > 1 && rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break; 
                        case SpiritualProperty.devil:  
                            if(deathCount > 1 && rollper(21.666f))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;
                        case SpiritualProperty.doremis:
                            ResetConditionConsecutiveTurn();//変化なし
                        break;
                        case SpiritualProperty.pysco:
                            switch(RandomEx.Shared.NextInt(5))
                            {
                                case 0:
                                NowCondition = HumanConditionCircumstances.Optimistic;
                                break;
                                case 1:
                                NowCondition = HumanConditionCircumstances.Resolved;
                                break;
                                case 2:
                                //変化なし
                                ResetConditionConsecutiveTurn();
                                break;
                                case 3:
                                NowCondition = HumanConditionCircumstances.Doubtful;
                                break;
                                case 4:
                                NowCondition = HumanConditionCircumstances.Angry;
                                break;
                            }
                        break;
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.cquiest:
                            NowCondition = HumanConditionCircumstances.Normal;
                        break;
                    }
                    break;
                case HumanConditionCircumstances.Confused:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pillar:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;   
                        case SpiritualProperty.devil:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.godtier:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        //後は全て変化なし
                        default:
                            ResetConditionConsecutiveTurn();
                            break;
                    }
                    break;
                case HumanConditionCircumstances.Normal:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.sacrifaith:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                        break;
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount > 1 && rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break; 
                        case SpiritualProperty.devil:  
                            if(deathCount > 1 && rollper(21.666f))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;
                        case SpiritualProperty.doremis:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.pysco:
                            switch(RandomEx.Shared.NextInt(5))
                            {
                                case 0:
                                NowCondition = HumanConditionCircumstances.Optimistic;
                                break;
                                case 1:
                                NowCondition = HumanConditionCircumstances.Resolved;
                                break;
                                case 2:
                                //変化なし
                                ResetConditionConsecutiveTurn();
                                break;
                                case 3:
                                NowCondition = HumanConditionCircumstances.Doubtful;
                                break;
                                case 4:
                                NowCondition = HumanConditionCircumstances.Angry;
                                break;
                            }
                        break;
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pillar:
                            ResetConditionConsecutiveTurn();//変化なし
                            break;
                    }
                break;
            }

        }
    }
    /// <summary>
    /// 敵を倒した際の人間状況の変化
    /// </summary>
    public void ApplyConditionChangeOnKillEnemy(BaseStates ene)
    {
        if (MyType == CharacterType.Life) // 基本的に生命のみ
        {
            switch (NowCondition)
            {
                case HumanConditionCircumstances.Painful:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(66))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(33))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(57))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(33))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            var OptimisticPer = 0;//楽観的に行く確率
                            var eneKereKere = ene.TenDayValues.GetValueOrZero(TenDayAbility.KereKere);
                            var eneWif = ene.TenDayValues.GetValueOrZero(TenDayAbility.FlameBreathingWife);
                            var KereKere = TenDayValues.GetValueOrZero(TenDayAbility.KereKere);
                            var Wif = TenDayValues.GetValueOrZero(TenDayAbility.FlameBreathingWife);
                            if(KereKere >= eneKereKere && Wif > eneWif)
                            {
                                OptimisticPer = (int)(Wif - eneWif);
                            }
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(OptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            var NormalPer = 0;
                            var EneLeisure = ene.TenDayValues.GetValueOrZero(TenDayAbility.Leisure);
                            var Leisure = TenDayValues.GetValueOrZero(TenDayAbility.Leisure);
                            if(Leisure > EneLeisure)
                            {
                                NormalPer = (int)(Leisure - EneLeisure);
                            }
                            if(rollper(90 + NormalPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(15))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(9))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(35))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(7))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            var C_NormalEndWarPer = 0;
                            var C_NormalNightPer = 0;
                            var C_EneEndWar = ene.TenDayValues.GetValueOrZero(TenDayAbility.HeavenAndEndWar);
                            var C_EneNight = ene.TenDayValues.GetValueOrZero(TenDayAbility.NightInkKnight);
                            var C_EndWar = TenDayValues.GetValueOrZero(TenDayAbility.HeavenAndEndWar);
                            var C_Night = TenDayValues.GetValueOrZero(TenDayAbility.NightInkKnight);
                            if(C_EndWar > C_EneEndWar)
                            {
                                C_NormalEndWarPer = (int)(C_EndWar - C_EneEndWar);
                            }
                            if(C_Night > C_EneNight)
                            {
                                C_NormalNightPer = (int)(C_Night - C_EneNight);
                            }

                            if(rollper(80 + C_NormalEndWarPer + C_NormalNightPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(22))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(78))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(75))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(5))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            var VondPer = 0;
                            var Vond = TenDayValues.GetValueOrZero(TenDayAbility.Vond);
                            var EneVond = ene.TenDayValues.GetValueOrZero(TenDayAbility.Vond);
                            if(Vond > EneVond)
                            {
                                VondPer = (int)(Vond - EneVond);
                            }
                            if(rollper(97 + VondPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(4))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(25))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Optimistic:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(11))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var KinderOptimToElated_PersonaPer = TenDayValues.GetValueOrZero(TenDayAbility.PersonaDivergence);
                            if(KinderOptimToElated_PersonaPer> 776)KinderOptimToElated_PersonaPer = 776;//最低でも1%残るようにする
                            if(rollper(777 - KinderOptimToElated_PersonaPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            var SacrifaithOptimToElated_HumanKillerPer = TenDayValues.GetValueOrZero(TenDayAbility.HumanKiller);
                            if(rollper(-50 + SacrifaithOptimToElated_HumanKillerPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(5))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            var baledrivalOptimToElated_HumanKillerPer = TenDayValues.GetValueOrZero(TenDayAbility.HumanKiller);
                            if(rollper(3 + baledrivalOptimToElated_HumanKillerPer*2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            var DevilOptimToElatedPer = TenDayValues.GetValueOrZero(TenDayAbility.TentVoid) - TenDayValues.GetValueOrZero(TenDayAbility.Enokunagi);
                            if(rollper(60 - DevilOptimToElatedPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(1))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(6))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(4))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(38)){
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        default:
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Elated:
                    //変わらない
                    ResetConditionConsecutiveTurn();
                    break;

                case HumanConditionCircumstances.Resolved:
                    var ResolvedToOptimisticPer = TenDayValues.GetValueOrZero(TenDayAbility.FlameBreathingWife) - ene.TenDayValues.GetValueOrZero(TenDayAbility.FlameBreathingWife);
                    if(ResolvedToOptimisticPer < 0)
                    {
                        ResolvedToOptimisticPer = 0;
                    }
                    ResolvedToOptimisticPer = Mathf.Sqrt(ResolvedToOptimisticPer) * 2;
                    switch (MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(11 + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var ResolvedToOptimisticKinder_luck = TenDayValues.GetValueOrZero(TenDayAbility.Lucky);
                            if(rollper(77 + ResolvedToOptimisticKinder_luck + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            var ResolvedToOptimisticSacrifaith_UnextinguishedPath = TenDayValues.GetValueOrZero(TenDayAbility.UnextinguishedPath);
                            if(rollper(15 -ResolvedToOptimisticSacrifaith_UnextinguishedPath + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(10 + TenDayValues.GetValueOrZero(TenDayAbility.StarTersi) * 0.9f + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(40 + ResolvedToOptimisticPer + TenDayValues.GetValueOrZero(TenDayAbility.SpringWater)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            var ResolvedToOptimisticDevil_BalePer= TenDayValues.GetValueOrZero(TenDayAbility.Vail) - ene.TenDayValues.GetValueOrZero(TenDayAbility.Vail);
                            var ResolvedToOptimisticDevil_FaceToHandPer= TenDayValues.GetValueOrZero(TenDayAbility.FaceToHand) - ene.TenDayValues.GetValueOrZero(TenDayAbility.FaceToHand);
                            if(rollper(40 + ResolvedToOptimisticPer + (ResolvedToOptimisticDevil_BalePer - ResolvedToOptimisticDevil_FaceToHandPer)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(12 + (TenDayValues.GetValueOrZero(TenDayAbility.SpringWater) - TenDayValues.GetValueOrZero(TenDayAbility.Taraiton)) + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(4 + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            // 変化なし
                            ResetConditionConsecutiveTurn();
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(7 + ResolvedToOptimisticPer))
                            {
                                NowCondition =HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Angry:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var AngryEneVail = ene.TenDayValues.GetValueOrZero(TenDayAbility.Vail);
                            var AngryVail = TenDayValues.GetValueOrZero(TenDayAbility.Vail);
                            var AngryEneWaterThunder = ene.TenDayValues.GetValueOrZero(TenDayAbility.WaterThunderNerve);
                            var AngryWaterThunder = TenDayValues.GetValueOrZero(TenDayAbility.WaterThunderNerve);
                            var AngryToElated_KinderPer = AngryVail - AngryEneVail + (AngryWaterThunder - AngryEneWaterThunder);
                            if(rollper(50 + AngryToElated_KinderPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(30 - TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire)))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(TenDayValues.GetValueOrZero(TenDayAbility.HumanKiller)))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            const float Threshold = 37.5f;
                            var AngryToElated_BaledrivalPer = Threshold;
                            var AngryToElated_Baledrival_VailValue = TenDayValues.GetValueOrZero(TenDayAbility.Vail)/2;
                            if(AngryToElated_Baledrival_VailValue >Threshold)AngryToElated_BaledrivalPer = AngryToElated_Baledrival_VailValue;
                            if(rollper(AngryToElated_BaledrivalPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(40 + (20 - TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire))))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(19))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(14))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(27))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Doubtful:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(30 + TenDayValues.GetValueOrZero(TenDayAbility.SpringNap)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(46))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(77))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(1 + TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire) + TenDayValues.GetValueOrZero(TenDayAbility.Smiler)))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            var eneRainCoat = ene.TenDayValues.GetValueOrZero(TenDayAbility.Raincoat);
                            var EndWar = TenDayValues.GetValueOrZero(TenDayAbility.HeavenAndEndWar);
                            if(rollper(40 - (EndWar - eneRainCoat)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(44))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(80 + TenDayValues.GetValueOrZero(TenDayAbility.Rain)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(90 + TenDayValues.GetValueOrZero(TenDayAbility.ColdHeartedCalm) / 4))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire) * 1.2f))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(32 + TenDayValues.GetValueOrZero(TenDayAbility.Leisure)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper((TenDayValues.GetValueOrZero(TenDayAbility.UnextinguishedPath)-2) / 5))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            var DoubtfulToOptimistic_CPer = 0f;
                            if(ene.TenDayValues.GetValueOrZero(TenDayAbility.Leisure) < TenDayValues.GetValueOrZero(TenDayAbility.NightInkKnight) * 0.3f)
                            {
                                DoubtfulToOptimistic_CPer = TenDayValues.GetValueOrZero(TenDayAbility.ElementFaithPower);
                            }

                            if(rollper(38 + DoubtfulToOptimistic_CPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(33))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper((TenDayValues.GetValueOrZero(TenDayAbility.HeavenAndEndWar) - 6) / 2))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(27 - TenDayValues.GetValueOrZero(TenDayAbility.NightInkKnight)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(85))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(70))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            const float Threshold = 49f;
                            var DoubtfulToNorml_Doremis_nightDarknessAndVoidValue = TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness) + TenDayValues.GetValueOrZero(TenDayAbility.TentVoid);
                            var DoubtfulToNorml_DoremisPer = Threshold;
                            if(DoubtfulToNorml_Doremis_nightDarknessAndVoidValue < Threshold) DoubtfulToNorml_DoremisPer = DoubtfulToNorml_Doremis_nightDarknessAndVoidValue;
                            if(rollper(TenDayValues.GetValueOrZero(TenDayAbility.NightDarkness) + 30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(DoubtfulToNorml_DoremisPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(TenDayValues.GetValueOrZero(TenDayAbility.StarTersi) / 1.7f))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Confused:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(70))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(44))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var ConfusedToPainful_Kindergarden_DokumamusiAndRainCoatAverage = 
                            (TenDayValues.GetValueOrZero(TenDayAbility.dokumamusi) + TenDayValues.GetValueOrZero(TenDayAbility.Raincoat)) / 2;
                            if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(80 - (ConfusedToPainful_Kindergarden_DokumamusiAndRainCoatAverage - TenDayValues.GetValueOrZero(TenDayAbility.ColdHeartedCalm))))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20 + TenDayValues.GetValueOrZero(TenDayAbility.Raincoat) * 0.4f + TenDayValues.GetValueOrZero(TenDayAbility.dokumamusi) * 0.6f))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(70 + (TenDayValues.GetValueOrZero(TenDayAbility.Sort)-4)))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(60))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(80))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(11))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(34))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(75))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(6))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(27))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(64))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(7))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(60))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(60))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(3 - TenDayValues.GetValueOrZero(TenDayAbility.SpringWater)))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(90))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(67))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Normal:
                    var y = TenDayValues.GetValueOrZero(TenDayAbility.Leisure) - ene.TenDayValues.GetValueOrZero(TenDayAbility.Leisure);//余裕の差
                    switch (MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(30 + y*0.8f))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(20 - y))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(40 + y*2))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(70))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(14))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(30 +y))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(80))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(35 + y*1.1f))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(30 + y*0.1f))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(20 + y/4))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(15 + y * 0.95f))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(12))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            var NormalToElated_DoremisPer = 0f;
                            if(y > 0) NormalToElated_DoremisPer = TenDayValues.GetValueOrZero(TenDayAbility.BlazingFire) + TenDayValues.GetValueOrZero(TenDayAbility.Miza);
                            if(rollper(38 + y/2))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(20 + NormalToElated_DoremisPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// 相性値の高い味方が復活した際の人間状況の変化
    /// </summary>    
    public void ApplyConditionChangeOnCloseAllyAngel()
    {
        if (MyType == CharacterType.Life) // 基本的に生命のみ
        {
            switch (NowCondition)
            {
                case HumanConditionCircumstances.Painful:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.baledrival:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        case SpiritualProperty.pysco:
                        case SpiritualProperty.liminalwhitetile:
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.devil:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Optimistic:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.baledrival:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;    
                    }
                    break;

                case HumanConditionCircumstances.Elated:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.devil:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.kindergarden:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        case SpiritualProperty.doremis:
                        case SpiritualProperty.cquiest:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Resolved:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.liminalwhitetile:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        case SpiritualProperty.doremis:
                        case SpiritualProperty.baledrival:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Angry:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.devil:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.liminalwhitetile:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pysco:
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Doubtful:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.cquiest:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        default:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Confused:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.devil:
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.pillar:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Normal:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.pysco:
                        case SpiritualProperty.liminalwhitetile:
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.devil:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;
            }
        }
    } 
    /// <summary>
    /// 死亡と復活の間は何もないも同然なので復活時の変化はなく、死亡時のみ。
    /// つまり==復活した直後にその人間状況のまま開始すること前提==で考える。
    /// </summary>
    public void ApplyConditionChangeOnDeath()
    {
        switch (NowCondition)
        {
            //------------------------------
            // 辛い (Painful)
            //------------------------------
            case HumanConditionCircumstances.Painful:
                // 普調 (一律50%)
                if (rollper(50))
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                else
                {
                    // 変化なし
                    ResetConditionConsecutiveTurn();
                }
                break;

            //------------------------------
            // 楽観的 (Optimistic)
            //------------------------------
            case HumanConditionCircumstances.Optimistic:
                switch (MyImpression)
                {
                    // 楽観的 → 辛い
                    case SpiritualProperty.devil:
                    case SpiritualProperty.sacrifaith:
                        NowCondition = HumanConditionCircumstances.Painful;
                        break;

                    // 楽観的 → 普調
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.godtier:
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.kindergarden:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    case SpiritualProperty.pysco:
                        // サイコパスは 50% 普調 / 50% 変化なし
                        if (rollper(50))
                        {
                            NowCondition = HumanConditionCircumstances.Normal;
                        }
                        else
                        {
                            // 変化なし
                            ResetConditionConsecutiveTurn();
                        }
                        break;

                    // 楽観的 → 変化なし
                    case SpiritualProperty.baledrival:
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.doremis:
                        ResetConditionConsecutiveTurn();
                        break;

                    default:
                        // 変化なし
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 高揚 (Elated)
            //------------------------------
            case HumanConditionCircumstances.Elated:
                switch (MyImpression)
                {
                    // 変化なし
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.godtier:
                    case SpiritualProperty.devil:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 普調
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.kindergarden:
                    case SpiritualProperty.doremis:
                    case SpiritualProperty.pysco:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    // 楽観的
                    case SpiritualProperty.baledrival:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 辛いにはいかなそう => default で変化なし
                    default:
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 覚悟 (Resolved)
            //------------------------------
            case HumanConditionCircumstances.Resolved:
                switch (MyImpression)
                {
                    // 変化なし => ベール
                    case SpiritualProperty.baledrival:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 普調 => シークイエスト, ドレミス, デビル, ゴッドティア, キンダー
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.doremis:
                    case SpiritualProperty.devil:
                    case SpiritualProperty.godtier:
                    case SpiritualProperty.kindergarden:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    // 辛い => 支柱, リーミナル
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.liminalwhitetile:
                        NowCondition = HumanConditionCircumstances.Painful;
                        break;

                    // 疑念 => 自己犠牲, サイコ
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.pysco:
                        NowCondition = HumanConditionCircumstances.Doubtful;
                        break;

                    default:
                        // 変化なし
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 怒り (Angry)
            //------------------------------
            case HumanConditionCircumstances.Angry:
                switch (MyImpression)
                {
                    // 変化なし => リーミナル, 自己犠牲
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.sacrifaith:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 楽観的 => サイコ
                    case SpiritualProperty.pysco:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 普調 => else
                    default:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;
                }
                break;

            //------------------------------
            // 疑念 (Doubtful)
            //------------------------------
            case HumanConditionCircumstances.Doubtful:
                switch (MyImpression)
                {
                    // 怒り => 自己犠牲, ベール, デビル
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.baledrival:
                    case SpiritualProperty.devil:
                        NowCondition = HumanConditionCircumstances.Angry;
                        break;

                    // 普調 => サイコ, リーミナル, 支柱
                    case SpiritualProperty.pysco:
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.pillar:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    // 楽観的 => ドレミス, シークイエスト, キンダー, ゴッドティア
                    case SpiritualProperty.doremis:
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.kindergarden:
                    case SpiritualProperty.godtier:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 辛いにはいかない => default => 変化なし
                    default:
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 混乱 (Confused)
            //------------------------------
            case HumanConditionCircumstances.Confused:
                switch (MyImpression)
                {
                    // 変化なし => キンダー
                    case SpiritualProperty.kindergarden:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 高揚 => ベール, リーミナル
                    case SpiritualProperty.baledrival:
                    case SpiritualProperty.liminalwhitetile:
                        NowCondition = HumanConditionCircumstances.Elated;
                        break;

                    // 普調 => else
                    default:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;
                }
                break;

            //------------------------------
            // 普調 (Normal)
            //------------------------------
            case HumanConditionCircumstances.Normal:
                switch (MyImpression)
                {
                    // 変化なし => 支柱, サイコ
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.pysco:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 楽観的 => ゴッドティア
                    case SpiritualProperty.godtier:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 疑念 => リーミナル, キンダー, デビル
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.kindergarden:
                    case SpiritualProperty.devil:
                        NowCondition = HumanConditionCircumstances.Doubtful;
                        break;

                    // 怒り => 自己犠牲, シークイエスト, ベール
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.baledrival:
                        NowCondition = HumanConditionCircumstances.Angry;
                        break;

                    default:
                        // 変化なし
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            default:
                // それ以外(例えば none) => 変化なし
                ResetConditionConsecutiveTurn();
                break;
        }
    }
        
    /// <summary>
    /// 次に使用する命中率へのパーセント補正用保持リスト
    /// </summary>
    private List<ModifierPart> _useHITPercentageModifiers;
    /// <summary>
    /// 次に使用する攻撃力へのパーセント補正用保持リスト
    /// </summary>
    private List<ModifierPart> _useATKPercentageModifiers;
    /// <summary>
    /// 次に使用する回避率へのパーセント補正用保持リスト
    /// </summary>
    private List<ModifierPart> _useAGIPercentageModifiers;
    /// <summary>
    /// 次に使用する防御力へのパーセント補正用保持リスト
    /// </summary>
    private List<ModifierPart> _useDEFPercentageModifiers;

    /// <summary>
    /// 命中率補正をセットする。
    /// </summary>
    public void SetHITPercentageModifier(float value, string memo)
    {
        if (_useHITPercentageModifiers == null) _useHITPercentageModifiers = new List<ModifierPart>();//nullチェック、処理
        _useHITPercentageModifiers.Add(new ModifierPart(memo, value));
    }
    /// <summary>
    /// 攻撃力補正をセットする。
    /// </summary>
    public void SetATKPercentageModifier(float value, string memo)
    {
        if (_useATKPercentageModifiers == null) _useATKPercentageModifiers = new List<ModifierPart>();//nullチェック、処理
        _useATKPercentageModifiers.Add(new ModifierPart(memo, value));
    }
    /// <summary>
    /// 回避率補正をセットする。
    /// </summary>
    public void SetAGIPercentageModifier(float value, string memo)
    {
        if (_useAGIPercentageModifiers == null) _useAGIPercentageModifiers = new List<ModifierPart>();//nullチェック、処理
        _useAGIPercentageModifiers.Add(new ModifierPart(memo, value));
    }
    /// <summary>
    /// 防御力補正をセットする。
    /// </summary>
    public void SetDEFPercentageModifier(float value, string memo)
    {
        if (_useDEFPercentageModifiers == null) _useDEFPercentageModifiers = new List<ModifierPart>();//nullチェック、処理
        _useDEFPercentageModifiers.Add(new ModifierPart(memo, value));
    }

    /// <summary>
    /// 特別な命中率補正
    /// </summary>
    /// <param name="per"></param>
    public float UseHITPercentageModifier
    {
        get => _useHITPercentageModifiers.Aggregate(1.0f, (total, m) => total * m.Modifier);//リスト内全ての値を乗算
    }

    /// <summary>
    /// 特別な攻撃力補正
    /// </summary>
    public float UseATKPercentageModifier
    {
        get => _useATKPercentageModifiers.Aggregate(1.0f, (total, m) => total * m.Modifier);//リスト内全ての値を乗算
    }

    /// <summary>
    /// 特別な回避率補正
    /// </summary>
    public float UseAGIPercentageModifier
    {
        get => _useAGIPercentageModifiers.Aggregate(1.0f, (total, m) => total * m.Modifier);//リスト内全ての値を乗算
    }

    /// <summary>
    /// 特別な防御力補正
    /// </summary>
    public float UseDEFPercentageModifier
    {
        get => _useDEFPercentageModifiers.Aggregate(1.0f, (total, m) => total * m.Modifier);//リスト内全ての値を乗算
    }
    /// <summary>
    /// 特別な命中率補正の保持リストを返す。　主にフレーバー要素用。
    /// </summary>
    public List<ModifierPart> UseHitPercentageModifiers
    {
        get => _useHITPercentageModifiers;
    }

    /// <summary>
    /// 特別な攻撃力補正の保持リストを返す。　主にフレーバー要素用。
    /// </summary>
    public List<ModifierPart> UseATKPercentageModifiers
    {
        get => _useATKPercentageModifiers;
    }

    /// <summary>
    /// 特別な回避率補正の保持リストを返す。　主にフレーバー要素用。
    /// </summary>
    public List<ModifierPart> UseAGIPercentageModifiers
    {
        get => _useAGIPercentageModifiers;
    }

    /// <summary>
    /// 特別な防御力補正の保持リストを返す。　主にフレーバー要素用。
    /// </summary>
    public List<ModifierPart> UseDEFPercentageModifiers
    {
        get => _useDEFPercentageModifiers;
    }

    /// <summary>
    /// 一時的な補正などをすべて消す
    /// </summary>
    public void RemoveUseThings()
    {
        _useHITPercentageModifiers = new List<ModifierPart>();
        _useATKPercentageModifiers = new List<ModifierPart>();
        _useAGIPercentageModifiers = new List<ModifierPart>();
        _useDEFPercentageModifiers = new List<ModifierPart>();
    }




    //スキルのリスト
    public IReadOnlyList<BaseSkill> SkillList => _skillList;
    /// <summary>
    /// 完全な単体攻撃かどうか
    /// (例えばControlByThisSituationの場合はrangeWillにそのままskillのzoneTraitが入るので、
    /// そこに範囲系の性質(事故で範囲攻撃に変化)がある場合はfalseが返る
    /// </summary>
    /// <returns></returns>
    private bool IsSingleATK()
    {
        return DontHasRangeWill(SkillZoneTrait.CanSelectMultiTarget,
            SkillZoneTrait.RandomSelectMultiTarget, SkillZoneTrait.RandomMultiTarget,
            SkillZoneTrait.AllTarget);
    }

    /// <summary>
    /// 命中率計算
    /// </summary>
    /// <returns></returns>
    public virtual float EYE()
    {
        float eye = b_EYE;//基礎命中率

        eye *= UseHITPercentageModifier;//命中率補正。リスト内がゼロならちゃんと1.0fが返る。
        eye *= _passiveList.Aggregate(1.0f, (total, m) => total * m.EYEPercentageModifier());//パッシブの乗算補正

        //範囲意志によるボーナス
        foreach (KeyValuePair<SkillZoneTrait, float> entry
            in NowUseSkill.HitRangePercentageDictionary)//辞書に存在する物全てをループ
        {
            if (HasRangeWill(entry.Key))//キーの内容が範囲意志と合致した場合
            {
                eye += entry.Value;//範囲意志による補正が掛かる

                //基本的に範囲は一つだけのはずなので無用なループは避けてここで終了
                break;
            }
        }

        //単体攻撃による命中補正
        //複数性質を持っていない、完全なる単体の攻撃なら
        if (IsSingleATK())
        //ControlBySituationでの事故性質でも複数性質で複数事故が起こるかもしれないので、それも加味してる。
        {
            var agiPer = 6;//攻撃者のAgiの命中補正用 割る数
            if (NowUseSkill.SkillPhysical == PhysicalProperty.heavy)//暴断攻撃なら
            {
                agiPer *= 2;//割る数が二倍に
            }
            eye += AGI() / agiPer;
        }

        //パッシブ「食らわせ」による命中低下
        var HurtDownPower = GetPassiveByID(2);
        if(HurtDownPower != null)
        {
            eye -= 10;
            eye *= 0.8f;
        }



        //割り込みカウンターパッシブなら+100
        var CounterPower = GetPassiveByID(1) as InterruptCounterPassive;
        if (CounterPower != null)
        {
            
            eye += CounterPower.EyeBonus;
        }

        //パッシブの補正　固定値
        eye += _passiveList.Sum(p => p.EYEFixedValueEffect());

        if(eye < 0) eye = 0;
        return eye;
    }

    /// <summary>
    /// 回避率計算
    /// </summary>
    public virtual float AGI()
    {
        float agi = b_AGI;//基礎回避率

        agi *= UseAGIPercentageModifier;//回避率補正。リスト内がゼロならちゃんと1.0fが返る。
        agi *= _passiveList.Aggregate(1.0f, (total, m) => total * m.AGIPercentageModifier());//パッシブの乗算補正

        if (manager.IsVanguard(this))//自分が前のめりなら
        {
            agi /= 2;//回避率半減
        }
        //パッシブの補正　固定値
        agi += _passiveList.Sum(p => p.AGIFixedValueEffect());

        if(agi < 0) agi = 0;
        return agi;
    }

    public virtual float ATK()
    {
        float atk = b_ATK;//基礎攻撃力

        atk *= UseATKPercentageModifier;//攻撃力補正
        atk *= _passiveList.Aggregate(1.0f, (total, m) => total * m.ATKPercentageModifier());//パッシブの乗算補正

        //範囲意志によるボーナス
        foreach (KeyValuePair<SkillZoneTrait, float> entry
            in NowUseSkill.PowerRangePercentageDictionary)//辞書に存在する物全てをループ
        {
            if (HasRangeWill(entry.Key))//キーの内容が範囲意志と合致した場合
            {
                atk += entry.Value;//範囲意志による補正が掛かる

                //基本的に範囲は一つだけのはずなので無用なループは避けてここで終了
                break;
            }
        }

        //単体攻撃で暴断物理攻撃の場合のAgi攻撃補正
        if (IsSingleATK())
        {
            if (NowUseSkill.SkillPhysical == PhysicalProperty.heavy)
            {
                atk += AGI() / 6;
            }
        }

        //割り込みカウンターパッシブがあるなら二倍の攻撃力
                //割り込みカウンターパッシブなら+100
        var CounterPower = GetPassiveByID(1) as InterruptCounterPassive;
        if (CounterPower != null)
        {
            atk *= CounterPower.AttackMultiplier;
        }

        //パッシブ「食らわせ」による威力低下
        var HurtDownPower = GetPassiveByID(2);
        if(HurtDownPower != null)
        {
            atk *= 0.81f;
        }

        //パッシブの補正　固定値を加算する
        atk += _passiveList.Sum(p => p.ATKFixedValueEffect());

        if(atk < 0) atk = 0;
        return atk;
    }

    /// <summary>
    ///     防御力計算 シミュレートも含む(AimStyle不一致によるクランプのため)
    ///     オプションのAimStyleに値を入れるとそのAimStyleでシミュレート
    /// </summary>
    /// <returns></returns>
    public virtual float DEF(float minusPer=0f, AimStyle? SimulateAimStyle = null)
    {
        var def = b_DEF(); //基礎防御力が基本。

        if(SimulateAimStyle != null)//シミュレートするなら
        {
            def = b_DEF(SimulateAimStyle);//b_defをシミュレート
        }

        def *= UseDEFPercentageModifier;//防御力補正
        def *= _passiveList.Aggregate(1.0f, (total, m) => total * m.DEFPercentageModifier());//パッシブの乗算補正

        var minusAmount = def * minusPer;//防御低減率

        //パッシブの補正　固定値
        def += _passiveList.Sum(p => p.DEFFixedValueEffect());

        def -= minusAmount;//低減

        if(def < 0) def = 0;
        return def;
    }
    /// <summary>
    /// 精神HP用の防御力
    /// </summary>
    public virtual float MentalDEF()
    {
        return b_DEF() * 0.7f * NowPower switch
        {
            ThePower.high => 1.4f,
            ThePower.medium => 1f,
            ThePower.low => 0.7f,
            ThePower.lowlow => 0.4f,
            _ => -4444444,//エラーだ
        };
    }



    


    /// <summary>
    ///初期精神属性決定関数(基本は印象を持ってるスキルリストから適当に選び出す
    /// </summary>
    public virtual void InitializeMyImpression()
    {
        SpiritualProperty that;

        if (SkillList != null)
        {
            var rnd = RandomEx.Shared.NextInt(0, SkillList.Count);
            that = SkillList[rnd].SkillSpiritual; //スキルの精神属性を抽出
            MyImpression = that; //印象にセット
        }
        else
        {
            Debug.Log(CharacterName + " のスキルが空です。");
        }
    }

    //互角一撃の生存処理--------------------------------------------------------------------------互角一撃の生存処理------------------------------ーーーーーーーーーーー

    /// <summary>
    /// 互角一撃の状況で「即死しかけたが奇跡的に生き残る」確率(%)を返す。
    ///
    /// ◆大まかな流れ：
    ///  1) 精神属性 × パワー条件 を満たしているかどうか
    ///      - 満たしていなければ 0%
    ///  2) 人間状況ごとの基本値をベースにする
    ///      - 怒り/高揚/辛い/混乱 → 0%
    ///      - 覚悟 → 7%
    ///      - 楽観的 → 2%
    ///      - 普調 → 4%
    ///      - 疑念 → 1%
    ///  3) 特定の「精神属性 × 人間状況」組み合わせでさらに上書き
    ///      - 例: ゴッドティア × 楽観的 = 12% など
    /// </summary>
    public int GetMutualKillSurvivalChance()
    {
        var property = MyImpression;
        var power = NowPower;
        var condition = NowCondition;
        // (A) まず "パワー条件" をチェックして、
        //     クリアしていなければ0%を返す
        //     （属性ごとに分岐。ゴッドティアなど「パワー条件なし」はスルー）
        if (!CheckPowerCondition(property, power))
        {
            return 0; 
        }

        // (B) 次に "人間状況" ごとの基本値を設定
        int baseChance = GetBaseChanceByCondition(condition);

        // (C) 最後に「特定の属性×状況」で上書き（例: デビル×楽観的=0% など）
        baseChance = OverrideByPropertyAndCondition(property, condition, baseChance);

        // 返却値を 0～100 にクランプ（負になったり100超えたりしないように）
        if (baseChance < 0) baseChance = 0;
        if (baseChance > 100) baseChance = 100;

        return baseChance;
    }


    /// <summary>
    /// 属性ごとの「パワー条件」をチェックし、満たしていればtrue、ダメならfalseを返す。
    /// </summary>
    private bool CheckPowerCondition(SpiritualProperty property, ThePower power)
    {
        switch (property)
        {
            case SpiritualProperty.liminalwhitetile:
                // パワーが普通以上 (>= medium)
                return (power >= ThePower.medium);

            case SpiritualProperty.kindergarden:
                // パワーが高い (== high)
                return (power == ThePower.high);

            case SpiritualProperty.sacrifaith:
                // パワーが普通以上 (>= medium)
                return (power >= ThePower.medium);

            case SpiritualProperty.cquiest:
                // 「低い以上」と書かれていたため (>= low)
                // 低い(low), 普通(medium), 高い(high) はOK。 たるい(lowlow)はNG
                return (power >= ThePower.low);

            case SpiritualProperty.devil:
                // 本文に「パワーが高いと」としか書かれていない→ここでは「高いでないとダメ」と仮定
                return (power == ThePower.high);

            case SpiritualProperty.doremis:
                // パワーが普通以上
                return (power >= ThePower.medium);

            case SpiritualProperty.pillar:
                // パワーが普通以上
                return (power >= ThePower.medium);

            case SpiritualProperty.godtier:
                // 「パワー条件なし」
                return true;

            case SpiritualProperty.baledrival:
                // 「パワーが低い以上」→ ここでは (power >= ThePower.low) と解釈
                return (power >= ThePower.low);

            case SpiritualProperty.pysco:
                // パワーが普通以上
                return (power >= ThePower.medium);

            default:
                // それ以外( none など) は特に定義されていない場合、0%扱い
                return false;
        }
    }


    /// <summary>
    /// 人間状況ごとの「基本値」を返す。
    /// </summary>
    private int GetBaseChanceByCondition(HumanConditionCircumstances condition)
    {
        switch (condition)
        {
            case HumanConditionCircumstances.Angry:
            case HumanConditionCircumstances.Elated:
            case HumanConditionCircumstances.Painful:
            case HumanConditionCircumstances.Confused:
                return 0;

            case HumanConditionCircumstances.Resolved:
                return 7;
            case HumanConditionCircumstances.Optimistic:
                return 2;
            case HumanConditionCircumstances.Normal:
                return 4;
            case HumanConditionCircumstances.Doubtful:
                return 1;

            default:
                // ここに来ることはあまり想定外だが、念のため0%
                return 0;
        }
    }

    /// <summary>
    /// 属性 × 状況 の特別な組み合わせで「上書き」する。
    /// 例：ゴッドティア × 楽観的 => 12% など
    /// </summary>
    private int OverrideByPropertyAndCondition(
        SpiritualProperty property,
        HumanConditionCircumstances condition,
        int baseChance
    )
    {
        switch (property)
        {
            //=======================================
            // ■ゴッドティア (godtier)
            //=======================================
            case SpiritualProperty.godtier:
                // 楽観的なら 12% (通常2%を上書き)
                if (condition == HumanConditionCircumstances.Optimistic)
                {
                    return 12;
                }
                break;

            //=======================================
            // ■デビル (devil)
            //=======================================
            case SpiritualProperty.devil:
                // 楽観的なら 0% (通常2% => 0% 上書き)
                if (condition == HumanConditionCircumstances.Optimistic)
                {
                    return 0;
                }
                break;

            //=======================================
            // ■自己犠牲 (sacrifaith)
            //=======================================
            case SpiritualProperty.sacrifaith:
                // 怒り => 6% (通常 怒りは0% => 6%で上書き)
                if (condition == HumanConditionCircumstances.Angry)
                {
                    return 6;
                }
                break;

            //=======================================
            // ■ドレミス (doremis)
            //=======================================
            case SpiritualProperty.doremis:
                // 疑念 => 14% (通常1% => 14%)
                if (condition == HumanConditionCircumstances.Doubtful)
                {
                    return 14;
                }
                break;

            //=======================================
            // ■支柱 (pillar)
            //=======================================
            case SpiritualProperty.pillar:
                // 辛い => 6% (通常0% => 6%)
                if (condition == HumanConditionCircumstances.Painful)
                {
                    return 6;
                }
                break;

            //=======================================
            // ■ベールドライヴァル (baledrival)
            //=======================================
            case SpiritualProperty.baledrival:
                // 高揚 => 11% (通常0% => 11%)
                if (condition == HumanConditionCircumstances.Elated)
                {
                    return 11;
                }
                break;

            //=======================================
            // ■その他のケース
            //   (サイコパスやキンダーガーデン、リーミナルホワイトタイルなど)
            //   特に指定がなければ、 baseChance のまま
            //=======================================
            default:
                break;
        }

        // 上記で特に上書きされなければ baseChance のまま
        return baseChance;
    }
    //互角一撃の生存処理--------------------------------------------------------------------------互角一撃の生存処理------------------------------ーーーーーーーーーーー

    /// <summary>
    /// 基礎山型分布によるダメージ補正
    /// </summary>
    float GetBaseCalcDamageWithPlusMinus22Percent(float baseDamage)
    {
        // 1) 8d5501 を振る（8回ランダム）
        int diceSum = 0;
        for (int i = 0; i < 8; i++)
        {
            // Range(1, 5502) => [1..5501] の整数
            diceSum += RandomEx.Shared.NextInt(1, 5502);
        }

        // 2) 平均(22008)を引いて、0.00001f を掛ける
        //    → -0.22 ～ +0.22 (±22%)
        float offset = (diceSum - 22008) * 0.00001f;

        // 3) baseDamage に対して (1 + offset) 倍する
        //    → (1 - 0.22)～(1 + 0.22) = 0.78～1.22 倍
        float finalDamage = baseDamage * (1f + offset);

        // 4) 必要であれば下限補正（例：0未満になれば0にする など）
        if (finalDamage < 0f)
        {
            finalDamage = 0f;
        }

        // 5) float で返す（丸めたくないのでそのまま）
        return finalDamage;
    }
    /// <summary>
    /// 防ぎ方(AimStyle)の不一致がある場合、クランプする
    /// </summary>
    private float ClampDefenseByAimStyle(BaseSkill skill,float def)
    {
        if(skill.NowAimStyle() != NowDeffenceStyle)
        {
            var MatchedMaxClampDef = DEF(skill.DEFATK, skill.NowAimStyle())*0.7f;//適切な防御力の0.7倍がクランプ最大値

            if(NowPower>ThePower.medium)//パワーが高い場合は 「適切な防御力をこしてた場合のみ」適切防御力の0.7倍にクランプ
            {
                //まず比較する、超していた場合にクランプ
                if(DEF()>DEF(0,skill.NowAimStyle()))//今回の防御力が適切な防御力を超してた場合、
                {
                    return MatchedMaxClampDef;//クランプされる。
                }
            }else//そうでない場合は、「適切な防御力を超してる越してない関係なく」適切防御力の0.7倍にクランプ(その最大値を絶対に超えない。)
            {
                
                if(def > MatchedMaxClampDef)
                {
                    return MatchedMaxClampDef;//最大値を超えたら最大値にクランプ
                }
            }
        }
        return def;//そのまま返す。
    }
    /// <summary>
    /// ダメージを渡し、がむしゃらの補正をかけて返す
    /// </summary>
    float GetFrenzyBoost(BaseStates atker,float dmg)
    {
        var boost =1.0f;
        var skill = atker.NowUseSkill;
        if(skill.NowConsecutiveATKFromTheSecondTimeOnward())//2回目以降の連続攻撃なら
        {
            var StrongFootEye = (EYE() + AGI()) /2f;
            var WeekEye = atker.EYE();
            var boostCoef = 0f;//ブースト係数

            if(StrongFootEye > WeekEye)//ちゃんと被害者側の命中回避平均値が攻撃者の命中より高い場合に限定する
            {
                boostCoef = Mathf.Floor((StrongFootEye - WeekEye) / 5);
                boost += boostCoef * 0.01f;
                for(int i =0;i<skill.ATKCountUP-1;i++)//初回=単回攻撃の恐れがある場合は、がむしゃらは発動しないので、二回目から一回ずつ乗算されるようにしたいから-1
                {
                    dmg *= boost;
                }
            }
        }
        return dmg;//連続攻撃でないなら、そのまま返す
    }
    /// <summary>
    /// 互角一撃の生存判定
    /// </summary>
    void CalculateMutualKillSurvivalChance(float LiveHP,float dmg,BaseStates atker)
    {
        //deathの判定が入る前に、互角一撃の生存判定を行い、HP再代入
        //ダメージの大きさからして絶対に死んでるからDeath判定は要らず、だからDeath辺りでの判定がいらない。(DeathCallBackが起こらない)
        if(LiveHP >= _maxHp*0.2f)//HPが二割以上の時に、
        {
            if(atker.TenDayValuesSum <= TenDayValuesSum * 1.6f)//自分の十日能力の総量の1.6倍以下なら
            {
                if (dmg >= _maxHp * 0.34f && dmg <= _maxHp * 0.66f )//大体半分くらいの攻撃なら  
                {
                    //生存判定が入る
                    if(rollper(GetMutualKillSurvivalChance()))
                    {
                        HP = _maxHp * 0.07f;
                    }
                }
            }
        }
    }


    /// <summary>
    ///     オーバライド可能なダメージ関数
    /// </summary>
    /// <param name="atkPoint"></param>
    public virtual float Damage(BaseStates Atker, float SkillPower,float SkillPowerForMental)
    {
        var skill = Atker.NowUseSkill;
        var def = DEF(skill.DEFATK);

        def = ClampDefenseByAimStyle(skill,def);//防ぎ方(AimStyle)の不一致がある場合、クランプする

        var dmg = ((Atker.ATK() - def) * SkillPower) + SkillPower;//(攻撃-対象者の防御) にスキルパワー加算と乗算
        var mentalATKBoost = Mathf.Max(Atker.TenDayValues.GetValueOrZero(TenDayAbility.Leisure) - TenDayValues.GetValueOrZero(TenDayAbility.Leisure),0)
        * Atker.MentalHP * 0.2f;//相手との余裕の差と精神HPの0.2倍を掛ける 
        var mentalDmg = ((Atker.ATK() - MentalDEF()) * SkillPowerForMental) + SkillPowerForMental * mentalATKBoost;//精神攻撃

        if(NowPower > ThePower.lowlow)//たるくなければ基礎山形補正がある。
        dmg = GetBaseCalcDamageWithPlusMinus22Percent(dmg);//基礎山型補正

        //がむしゃらな補正
        dmg = GetFrenzyBoost(Atker,dmg);

        //慣れ補正
        dmg *= AdaptToSkill(Atker, skill, dmg);

        //vitalLayerを通る処理
        BarrierLayers(ref dmg,ref mentalDmg, Atker);

        if(dmg < 0)dmg = 0;//0未満は0にする　逆に回復してしまうのを防止
        var tempHP = HP;//計算用にダメージ受ける前のHPを記録
        HP -= dmg;
        Debug.Log("攻撃が実行された");

        if(mentalDmg < 0)mentalDmg = 0;//0未満は0にする
        MentalHP -= mentalDmg;//実ダメージで精神HPの最大値がクランプされた後に、精神攻撃が行われる。

        CalculateMutualKillSurvivalChance(tempHP,dmg,Atker);//互角一撃の生存によるHP再代入の可能性
        Atker.MentalHealOnAttack();//精神HPの攻撃時回復

        //死んだら攻撃者のOnKillを発生
        if(Death())
        {
            Atker.OnKill(this);
        }

        //もし"攻撃者が"割り込みカウンターパッシブだったら
        var CounterPower = Atker.GetPassiveByID(1) as InterruptCounterPassive;
        if (CounterPower != null)
        {
            //攻撃者の割り込みカウンターパッシブの威力が下がる
            CounterPower.DecayEffects();//割り込みカウンターパッシブ効果半減　sameturnの連続攻撃で発揮する。(パッシブ自体は1ターンで終わる)

            //割り込みカウンターをされた = さっき「自分は連続攻撃」をしていた
            //その連続攻撃の追加硬直値分だけ、「食らわせ」というパッシブを食らう。
            var DurationTurn = skillDatas[skillDatas.Count - 1].Skill.SKillDidWaitCount;//食らうターン
            if(DurationTurn > 0)//持続ターンが存在すれば、
            {
                ApplyPassive(2);//パッシブ、食らわせを入手する。
                var hurt = GetPassiveByID(2);//適合したなら(適合条件がある)
                if(hurt != null)
                {
                    hurt.DurationTurn = DurationTurn;//持続ターンを入れる
                }
            }

        }


        return dmg;
    }

    /// <summary>
    /// ヒール
    /// </summary>
    /// <param name="HealPoint"></param>
    public virtual string Heal(float HealPoint)
    {
        if(!Death())
        {
            HP += HealPoint;
            Debug.Log("ヒールが実行された");
            return "癒された";
        }

        return "死んでいる";
    }
    /// <summary>
    /// 精神HPのヒール処理
    /// </summary>
    /// <param name="HealPoint"></param>
    public virtual void MentalHeal(float HealPoint)
    {
        if(!Death())
        {
            MentalHP += HealPoint;
            Debug.Log("精神ヒールが実行された");
        }
    }
    /// <summary>
    /// 命中凌駕の判定関数　引数倍命中が回避を凌駕してるのなら、スキル命中率に影響を与える
    /// </summary>
    private float AccuracySupremacy(float atkerEye, float undAtkerAgi, float multiplierThreshold = 2.5f)
    {
        var supremacyMargin = 0f;
        var modifyAgi = undAtkerAgi * multiplierThreshold;//補正されたagi
        if (atkerEye >= modifyAgi)//攻撃者のEYEが特定の倍被害者のAGIを上回っているならば、
        {
            supremacyMargin = (atkerEye - modifyAgi) / 2;//命中が引数倍された回避を超した分　÷　2
        }
        return supremacyMargin;
    }
    /// <summary>
    /// 攻撃者と防御者とスキルを利用してヒットするかの計算
    /// </summary>
    private bool IsReactHIT(BaseStates Attacker)
    {
        var skill = Attacker.NowUseSkill;
        var minusMyChance = 0f;

        //vanguardじゃなければ攻撃者の命中減少
        if (!manager.IsVanguard(Attacker))
        {
            minusMyChance += AGI() * 0.2f;    
        }

        if (minusMyChance > Attacker.EYE())//マイナス対策
        {
            minusMyChance = Attacker.EYE();
        }

        if (RandomEx.Shared.NextFloat(Attacker.EYE() + AGI()) < Attacker.EYE() - minusMyChance)//術者の命中+僕の回避率　をMAXに　ランダム値が術者の命中に収まったら　命中。
        {
            //スキルそのものの命中率 スキル命中率は基本独立させて、スキル自体の熟練度系ステータスで補正する？
            return skill.SkillHitCalc(AccuracySupremacy(Attacker.EYE(), AGI()));
        }
        skill.HitConsecutiveCount = 0;//外したら連続ヒット回数がゼロ　
        return false;
    }

    /// <summary>
/// nightinknightの値に応じて現在の「引き締める」補正段階を返す関数 </summary>
/// <returns>補正段階 は増えていく。/returns>
int GetTightenMindCorrectionStage()
{
    float nightinknightValue = TenDayValues.GetValueOrZero(TenDayAbility.NightInkKnight);

    nightinknightValue /= 10;
    nightinknightValue = Mathf.Floor(nightinknightValue);
    if(NowPower == ThePower.high && RandomEx.Shared.NextFloat(1) < 0.5f)  nightinknightValue += 1;//パワーが高く、二分の一の確率を当てると、補正段階が1増える

    return (int)nightinknightValue;
}

/// <summary>
/// 今回攻撃された際のAimStyle で短期記憶(TransformCount など)を更新する
/// </summary>
private bool UpdateAimStyleMemory(AimStyle newAimStyle, int tightenStage)
{
    // 現在の短期記憶
    var mem = _aimStyleMemory;

    // 1) まだ何も対応していない or 前回の TargetAimStyle と違う ならリセット
    if (mem.TargetAimStyle == null || mem.TargetAimStyle.Value != newAimStyle)
    {
        // 新しく対応を始める
        mem.TargetAimStyle      = newAimStyle;
        mem.TransformCount      = 0;

        // TightenStage を加味して「対応に必要なカウントMax」を求める
        mem.TransformCountMax   = CalcTransformCountMax(tightenStage, newAimStyle);

    }
    
        // 変革カウントを進める
        int increment = CalcTransformCountIncrement(tightenStage);

        mem.TransformCount += increment;

        // 更新を反映
        _aimStyleMemory = mem;

        if(mem.TransformCount >= mem.TransformCountMax)//カウント上限を超えたらリセットし変更成功の項を返す
        {
            mem.TransformCount = 0;
            mem.TargetAimStyle = null;
            mem.TransformCountMax = 0;
            // 更新を反映
            _aimStyleMemory = mem;
           return true;
        }
    return false;
    
}
/// <summary>
/// AimStyleを食らった時、何カウント増やすかを決める
/// ※ tightenStageが高いほど変革スピードが速い、など
/// </summary>
private int CalcTransformCountIncrement(int tightenStage)
{
    var rndmin = 0;
    var rndmax = tightenStage;
    if(NowPower< ThePower.medium)rndmax -= 1;
    if(tightenStage <2)return 1;//1以下なら基本値のみ
    if(tightenStage>5) rndmin = tightenStage/6;//6以上なら、補正段階の1/6が最小値
    return 1 + RandomEx.Shared.NextInt(rndmin, rndmax);//2以降なら補正段階分乱数の最大値が増える
}
/// <summary>
/// 引き締め段階(tightenStage)と、新AimStyle に応じて必要な最大カウントを算出
/// </summary>
    private int CalcTransformCountMax(int tightenStage, AimStyle AttackerStyle)
    {
        //AIMSTYLEの組み合わせ辞書により、必要な最大カウントを計算する
        var count = DefenseTransformationThresholds[(AttackerStyle, NowDeffenceStyle)];
        if(tightenStage>=2)
        {
            if(RandomEx.Shared.NextFloat(1)<0.31f + TenDayValues.GetValueOrZero(TenDayAbility.NightInkKnight)*0.01f)
        {
                count -= 1;

        }
        }
        
        if(tightenStage >= 5){
            if(RandomEx.Shared.NextFloat(1)<0.8f)
                {
                    count-=1;
                }
        }

    return  count;
    }

    /// <summary>防ぎ方の切り替え </summary>
    private void SwitchDefenceStyle(BaseStates atker)
    {
        if(atker.NowBattleProtocol == BattleProtocol.none)
        {
            NowDeffenceStyle = AimStyle.none;//戦闘規格がない(フリーハンドスキル)なら、防ぎ方もnone(防御排他ステがない)
            return;
        } 
        var skill = atker.NowUseSkill;
        var pattern = DefaultDefensePatternPerProtocol[atker.NowBattleProtocol];

        if(!skill.NowConsecutiveATKFromTheSecondTimeOnward()){//単回攻撃または初回攻撃なら  (戦闘規格noneが入ることを想定)

            var per = 1f;
            if(GetTightenMindCorrectionStage()>=2)per=0.75f;//補正段階が2以上になるまで75%の確率で切り替えます、それ以降は100%で完全対応

           if(RandomEx.Shared.NextFloat(1) < pattern.a)//パターンAなら 
           {
            skill.DecideNowMoveSet_A0_B1(0);

            if(RandomEx.Shared.NextFloat(1)<per){
                NowDeffenceStyle =  pattern.aStyle;
            }else{
                NowDeffenceStyle = GetRandomAimStyleExcept(pattern.aStyle);//aStyle以外のAimStyleをランダムに選びます
            }
           }
           else                                         //パターンBなら
           {
            skill.DecideNowMoveSet_A0_B1(1);

            if(RandomEx.Shared.NextFloat(1)<per){
                NowDeffenceStyle =  pattern.bStyle;
            }else{
                NowDeffenceStyle = GetRandomAimStyleExcept(pattern.bStyle);//bStyle以外のAimStyleをランダムに選びます
            }
           }

           skill.SetSingleAimStyle(NowDeffenceStyle);//スキルのAimStyleとしても記録する。
        }else{                                              //連続攻撃中なら　　(戦闘規格noneを連続攻撃のmovesetに入れないこと前提)
            var AtkAimStyle = skill.NowAimStyle();//攻撃者の現在のAimStyleを取得
            
            if (AtkAimStyle == NowDeffenceStyle) return;// 既に同じAimStyleなら何もしない

            var TightenMind = GetTightenMindCorrectionStage();//現在の自分の引き締め値を入手

            if(UpdateAimStyleMemory(AtkAimStyle, TightenMind))//まず短期記憶を更新または新生する処理
            {
                if(atker.NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
                {
                    if(RandomEx.Shared.NextFloat(1)<0.3f)return;
                }
                NowDeffenceStyle = AtkAimStyle;
            }//カウントアップ完了したなら、nowDeffenceStyleに記録されたAimStyleを適用するだけ
            
        }



    }
    /// <summary>
    /// 連続攻撃時、狙い流れの物理属性適正とスキルの物理属性の一致による1.1倍ブーストがあるかどうかを判定し行使する関数です
    /// </summary>
    void CheckPhysicsConsecutiveAimBoost(BaseStates attacker)
    {
        var skill = attacker.NowUseSkill;
        if(!skill.NowConsecutiveATKFromTheSecondTimeOnward())return;//連続攻撃でないなら何もしない

        if((skill.NowAimStyle() ==AimStyle.Doublet && skill.SkillPhysical == PhysicalProperty.volten) ||
            ( skill.NowAimStyle() ==AimStyle.PotanuVolf && skill.SkillPhysical == PhysicalProperty.volten) ||
            (skill.NowAimStyle() ==AimStyle.Duster) && skill.SkillPhysical == PhysicalProperty.dishSmack)
            {
                attacker.SetATKPercentageModifier(1.1f, "連続攻撃時、狙い流れの物理属性適正とスキルの物理属性の一致による1.1倍ブースト");
            }
    }

    /// <summary>
    /// 連続攻撃中の割り込みカウンターが可能かどうかを判定する
    /// </summary>
    private bool TryInterruptCounter(BaseStates attacker)
    {
        var skill = attacker.NowUseSkill;
        if(NowPower >= ThePower.medium)//普通のパワー以上で
        {
            var eneVond = attacker.TenDayValues.GetValueOrZero(TenDayAbility.Vond);
            var myVond =  TenDayValues.GetValueOrZero(TenDayAbility.Vond);
            var plusAtkChance = myVond> eneVond ? myVond - eneVond : 0f;//ヴォンドの差による微加算値
            if(RandomEx.Shared.NextFloat(1) < skill.DEFATK/3 + plusAtkChance*0.01f)
            {
                var mypersonDiver = TenDayValues.GetValueOrZero(TenDayAbility.PersonaDivergence);
                var myTentvoid = TenDayValues.GetValueOrZero(TenDayAbility.TentVoid);
                var eneSort = attacker.TenDayValues.GetValueOrZero(TenDayAbility.Sort);
                var eneRain = attacker.TenDayValues.GetValueOrZero(TenDayAbility.Rain);
                var eneCold = attacker.TenDayValues.GetValueOrZero(TenDayAbility.ColdHeartedCalm);
                var ExVoid = PlayersStates.Instance.ExplosionVoid;
                var counterValue = (myVond + mypersonDiver/(myVond-ExVoid)) * 0.9f;//カウンターする側の特定能力値
                var attackerValue = Mathf.Max(eneSort - eneRain/3,0)+eneCold;//攻撃者の特定能力値


                if(RandomEx.Shared.NextFloat(counterValue+attackerValue) < counterValue && RandomEx.Shared.NextFloat(1)<0.5f)
                {
                    //まず連続攻撃の無効化
                    attacker.DeleteConsecutiveATK();
                    attacker.IsActiveCancelInSkillACT = true;//スキルの行動を無効化された。
                    
                    //無効化のみ、次のターンで攻撃可能、それに加えて割り込みカウンターのパッシブが加わる。
                    //その三パターンで分かれる。　　最後のパッシブ条件のみ直接割り込みカウンターPassiveの方で設定している。

                    //割り込みカウンターのパッシブ付与しますが、適合するかどうかはそのpassiveの条件次第です。
                    ApplyPassive(1);

                    var CounterPower = GetPassiveByID(1);//適合したなら
                    if (CounterPower != null)
                    {
                        var attackerCounterPower = attacker.GetPassiveByID(1);
                        if(attackerCounterPower != null) //もし攻撃者が割り込みカウンターパッシブなら、
                        {
                            //攻撃者の割り込みカウンターパッシブのパワー+1で生成
                            CounterPower.SetPassivePower(attackerCounterPower.PassivePower +1);
                        }
                    }

                    //次のターンで攻撃、つまり先約リストの予約を判定する。　
                    if(HasCharacterType(CharacterType.Life))
                    {//生命なら、必ず反撃可能

                        //攻撃を食らった際、中断不可能なカウンターまたはfreezeConecutiveの場合、武器スキルでしか返せない。
                        var isfreeze = false;
                        if(NowUseSkill.NowConsecutiveATKFromTheSecondTimeOnward() && NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive) ||
                        NowUseSkill.IsTriggering) 
                        {
                        NowUseSkill = NowUseWeapon.WeaponSkill;
                        isfreeze = true;
                        }
                        manager.Acts.Add(this,manager.GetCharacterFaction(this),"割り込みカウンター",null,isfreeze);//通常の行動予約 
                    }

                    //無効化は誰でも可能です　以下のtrueを返して、呼び出し側で今回の攻撃の無効化は行います。
                    return true;
                }
            }
        }
        return false;
    }
    /// <summary>
    /// 悪いパッシブ付与処理
    /// </summary>
    bool BadPassiveHit(BaseSkill skill)
    {
        var hit = false;
        foreach (var id in skill.subEffects.Where(id => PassiveManager.Instance.GetAtID(id).IsBad))
        {
            hit |= ApplyPassive(id);//or演算だとtrueを一回でも発生すればfalseが来てもずっとtrueのまま
        }
        return hit;
    }
    /// <summary>
    /// 悪いパッシブ解除処理
    /// </summary>
    void BadPassiveRemove(BaseSkill skill)
    {
        foreach (var id in skill.subEffects.Where(id => PassiveManager.Instance.GetAtID(id).IsBad))
        {
            RemovePassiveByID(id);
        }
    }
    /// <summary>
    /// 悪い追加HP付与処理
    /// </summary>
    void BadVitalLayerHit(BaseSkill skill)
    {
        foreach (var id in skill.subVitalLayers.Where(id => VitalLayerManager.Instance.GetAtID(id).IsBad))
        {
            ApplyVitalLayer(id);
        }
    }
    /// <summary>
    /// 悪い追加HP解除処理
    /// </summary>
    void BadVitalLayerRemove(BaseSkill skill)
    {
        foreach (var id in skill.subVitalLayers.Where(id => VitalLayerManager.Instance.GetAtID(id).IsBad))
        {
            RemoveVitalLayerByID(id);
        }
    }
    /// <summary>
    /// 良いパッシブ付与処理
    /// </summary>
    bool GoodPassiveHit(BaseSkill skill)
    {
        var hit = false;
        foreach (var id in skill.subEffects.Where(id => !PassiveManager.Instance.GetAtID(id).IsBad))
        {
            hit |= ApplyPassive(id);
        }
        return hit;
    }
    /// <summary>
    /// 良いパッシブ解除処理
    /// </summary>
    void GoodPassiveRemove(BaseSkill skill)
    {
        foreach (var id in skill.subEffects.Where(id => !PassiveManager.Instance.GetAtID(id).IsBad))
        {
            RemovePassiveByID(id);
        }
    }
    /// <summary>
    /// 良い追加HP付与処理
    /// </summary>
    /// <param name="skill"></param>
    void GoodVitalLayerHit(BaseSkill skill)
    {
        foreach (var id in skill.subVitalLayers.Where(id => !VitalLayerManager.Instance.GetAtID(id).IsBad))
        {
            ApplyVitalLayer(id);
        }
    }
    /// <summary>
    /// 良い追加HP解除処理
    /// </summary>
    /// <param name="skill"></param>
    void GoodVitalLayerRemove(BaseSkill skill)
    {
        foreach (var id in skill.subVitalLayers.Where(id => !VitalLayerManager.Instance.GetAtID(id).IsBad))
        {
            RemoveVitalLayerByID(id);
        }
    }
    /// <summary>
    /// 直接攻撃じゃない敵対行動系
    /// </summary>
    void ApplyNonDamageHostileEffects(BaseSkill skill,out bool isBadPassiveHit, out bool isBadVitalLayerHit, out bool isGoodPassiveRemove, out bool isGoodVitalLayerRemove)
    {
        isBadPassiveHit = false;
        isBadVitalLayerHit = false;
        isGoodPassiveRemove = false;
        isGoodVitalLayerRemove = false;

        if (skill.HasType(SkillType.addPassive))//atktypeがあるからここで発生
        {
            //悪いパッシブを付与しようとしてるのなら、命中回避計算
            isBadPassiveHit = BadPassiveHit(skill);
        }

        if (skill.HasType(SkillType.AddVitalLayer))
        {
            //悪い追加HPを付与しようとしてるのなら、命中回避計算
            BadVitalLayerHit(skill);
            isBadVitalLayerHit = true;
        }
        if(skill.HasType(SkillType.RemovePassive))
        {
            //良いパッシブを取り除こうとしてるのなら、命中回避計算
            GoodPassiveRemove(skill);
            isGoodPassiveRemove = true;
        }
        if (skill.HasType(SkillType.RemoveVitalLayer))
        {
            //良い追加HPを取り除こうとしてるのなら、命中回避計算
            GoodVitalLayerRemove(skill);
            isGoodVitalLayerRemove = true;
        }
        
    }

    /// <summary>
    /// スキルに対するリアクション ここでスキルの解釈をする。
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="UnderIndex">攻撃される人の順番　スキルのPowerSpreadの順番に同期している</param>
    public virtual string ReactionSkill(BaseStates attacker, float spread)
    {
        var skill = attacker.NowUseSkill;

        //スキルパワーの精神属性による計算
        var modifier = SkillSpiritualModifier[(skill.SkillSpiritual, MyImpression)];//スキルの精神属性と自分の精神属性による補正
        var skillPower = skill.SkillPowerCalc(spread) * modifier.GetValue() / 100.0f;
        var skillPowerForMental = skill.SkillPowerForMentalCalc(spread) * modifier.GetValue() / 100.0f;//精神HPへのパワー
        var txt = "";//メッセージテキスト用
        var thisAtkTurn = true;

        //被害記録用の一時保存boolなど
        var isBadPassiveHit = false;
        var isBadPassiveRemove = false;
        var isGoodPassiveRemove = false;
        var isGoodPassiveHit = false;
        var isBadVitalLayerHit = false;
        var isBadVitalLayerRemove = false;
        var isGoodVitalLayerHit = false;
        var isGoodVitalLayerRemove = false;
        var isHeal = false;
        var isAtkHit = false;
        var healAmount = 0f;
        var damageAmount = 0f;

        //スキルの持ってる性質を全て処理として実行

        if (skill.HasType(SkillType.Attack))
        {
            if (IsReactHIT(attacker))
            {
                //割り込みカウンターの判定
                if(skill.NowConsecutiveATKFromTheSecondTimeOnward())//連続攻撃されてる途中なら
                {
                    if(!skill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))//ターンをまたいだ物じゃないなら
                    {
                        thisAtkTurn = !TryInterruptCounter(attacker);//割り込みカウンターの判定
                    }
                }

                if(thisAtkTurn)
                {
                    //防ぎ方の切り替え
                    SwitchDefenceStyle(attacker);
                    //連続攻撃の物理属性ブースト判定
                    CheckPhysicsConsecutiveAimBoost(attacker);
                    
                    //成功されるとダメージを受ける
                    damageAmount = Damage(attacker, skillPower,skillPowerForMental);
                    isAtkHit = true;//攻撃を受けたからtrue

                    ApplyNonDamageHostileEffects(skill,out isBadPassiveHit, out isBadVitalLayerHit, out isGoodPassiveRemove, out isGoodVitalLayerRemove);
                }
            }
        }
        else//atktypeがないと各自で判定
        {
             if (IsReactHIT(attacker))
            {
                ApplyNonDamageHostileEffects(skill,out isBadPassiveHit, out isBadVitalLayerHit, out isGoodPassiveRemove, out isGoodVitalLayerRemove);        
            }
        }

        //回復系は常に独立
        if(skill.HasType(SkillType.DeathHeal))
        {
            if (skill.SkillHitCalc(0))//スキル命中率の計算だけ行う
            {
                Angel();//降臨　アイコンがノイズで満たされるようなエフェクト
                isHeal = true;
                manager.MyGroup(this).PartyApplyConditionChangeOnCloseAllyAngel(this);//所属してるグループが自分の復活により相性値の高い味方の人間状況の変化
            }
        }

        if (skill.HasType(SkillType.Heal))
        {
            if (skill.SkillHitCalc(0))//スキル命中率の計算だけ行う
            {
                txt += Heal(skillPower);
                isHeal = true;
            }
        }

        if (skill.HasType(SkillType.MentalHeal))
        {
            if (skill.SkillHitCalc(0))//スキル命中率の計算だけ行う
            {
                MentalHeal(skillPower);
                isHeal = true;
            }
        }

        if (skill.HasType(SkillType.addPassive))
        {
            if (skill.SkillHitCalc(0))//スキル命中率の計算だけ行う
            {
                //良いパッシブを付与しようとしてるのなら、スキル命中計算のみ
                isGoodPassiveHit = GoodPassiveHit(skill);
            }
        }
        if (skill.HasType(SkillType.AddVitalLayer))
        {
            if (skill.SkillHitCalc(0))//スキル命中率の計算だけ行う
            {
                //良い追加HPを付与しようとしてるのなら、スキル命中のみ
               GoodVitalLayerHit(skill);
                isGoodVitalLayerHit = true;
            }
        }



        if(skill.HasType(SkillType.RemovePassive))
        {
            if (skill.SkillHitCalc(0))//スキル命中率の計算だけ行う
            {
                //悪いパッシブを取り除くのなら、スキル命中のみ
                BadPassiveRemove(skill);
                isBadPassiveRemove = true;
            }
        }
        if (skill.HasType(SkillType.RemoveVitalLayer))
        {
            if (skill.SkillHitCalc(0))//スキル命中率の計算だけ行う
            {
                //悪い追加HPを取り除こうとしてるのなら、スキル命中のみ
                BadVitalLayerRemove(skill);
                isBadVitalLayerRemove = true;
            }
        }

        
        



        Debug.Log("ReactionSkill");
        //ここで攻撃者の攻撃記録を記録する
        attacker.skillDatas.Add(new ACTSkillData(thisAtkTurn,skill,this));//発動したのか、何のスキルなのかを記録
        //被害の記録
        damageDatas.Add(new DamageData//クソ長い
        (isAtkHit,isBadPassiveHit,isBadPassiveRemove,isGoodPassiveHit,isGoodPassiveRemove,isGoodVitalLayerHit,isGoodVitalLayerRemove,isBadVitalLayerHit,isBadVitalLayerRemove,isHeal,skill,damageAmount,healAmount,attacker));

        return txt;
    }
    /// <summary>
    /// クラスを通じて相手を攻撃する
    /// </summary>
    public virtual string AttackChara(UnderActersEntryList Unders)
    {
        SkillUseConsecutiveCountUp(NowUseSkill);//連続カウントアップ
        string txt = "";

        // スキルの精神属性を自分の精神属性に変更
        NowUseSkill.SkillSpiritual = MyImpression;

        //対象者ボーナスの適用
        if(Unders.Count == 1)//結果として一人だけを選び、
        {
            if(NowUseSkill.HasZoneTraitAny(SkillZoneTrait.CanPerfectSelectSingleTarget,SkillZoneTrait.CanSelectSingleTarget,
            SkillZoneTrait.RandomSingleTarget,SkillZoneTrait.ControlByThisSituation))//単体スキルなら
            {
                var ene =Unders.GetAtCharacter(0);
                if(TargetBonusDatas.DoIHaveTargetBonus(ene))//対象者ボーナスを持っていれば
                {
                    //適用
                    var index = TargetBonusDatas.GetTargetIndex(ene);
                    SetATKPercentageModifier(TargetBonusDatas.GetAtPowerBonusPercentage(index),"対象者ボーナス");

                    //適用した対象者ボーナスの削除　該当インデックスのclear関数の制作
                    TargetBonusDatas.BonusClear(index);
                }
            }
        }

        for (var i = 0; i < Unders.Count; i++)
        {
            txt += Unders.GetAtCharacter(i).ReactionSkill(this, Unders.GetAtSpreadPer(i));//敵がスキルにリアクション
        }

        NowUseSkill.ConsecutiveFixedATKCountUP();//使用したスキルの攻撃回数をカウントアップ
        NowUseSkill.DoSkillCountUp();//使用したスキルの使用回数をカウントアップ
        RemoveUseThings();//特別な補正を消去
        Debug.Log("AttackChara");


        _tempUseSkill = NowUseSkill;//使ったスキルを一時保存
        return txt;
    }

    //FreezeConsecutiveの処理------------------------------------------------------------------------------------FreezeConsecutiveの消去、フラグの処理など-----------------------------------
    /// <summary>
    /// FreezeConsecutive、ターンをまたぐ連続実行スキルが実行中かどうか。
    /// </summary>
    /// <returns></returns>
    public bool IsNeedDeleteMyFreezeConsecutive()
    {
        if(NowUseSkill.NowConsecutiveATKFromTheSecondTimeOnward())//連続攻撃中で、
        {
            if(NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
            {
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// consecutiveな連続攻撃の消去
    /// </summary>
    public void DeleteConsecutiveATK()
    {
        FreezeUseSkill.ResetAtkCountUp();//強制実行中のスキルの攻撃カウントアップをリセット
        Defrost();//解除
        IsDeleteMyFreezeConsecutive = false;

    }
    //---------------------------------------------------------------------------------------------FreezeConsecutiveのフラグ、後処理など終わり------------------------------------------------------------
    /// <summary>
    /// 死んだ瞬間を判断するためのフラグ
    /// </summary>
    bool hasDied =false;

    /// <summary>
    ///     死を判定するオーバライド可能な関数
    /// </summary>
    /// <returns></returns>
    public virtual bool Death()
    {
        if (HP <= 0) 
        {
            if(!hasDied)
            {
            hasDied =true;
            DeathCallBack();
            }
            return true;
        }
        return false;
    }
    /// <summary>
    /// 復活する際の関数
    /// </summary>
    public virtual void Angel()
    {
        hasDied =false;
        HP = float.Epsilon;//生きてるか生きてないか=Angel
        if(NowPower == ThePower.high)
        {
            HP = 30;//気力が高いと多少回復
        }
    }
    /// <summary>
    /// 死亡時のコールバック　SkillsTmpResetでスキルの方からリセットできるような簡単じゃない奴をここで処理する。
    /// </summary>
    public virtual void DeathCallBack()
    {
        DeleteConsecutiveATK();
        ApplyConditionChangeOnDeath();

        //あるかわからないが続行中のスキルを消し、
        //以外のそれ以外のスキルの連続攻撃回数消去(基本的に一個しか増えないはずだが)は以下のforeachループで行う
        foreach (var skill in SkillList)
        {
            skill.OnDeath();
        }

        //対象者ボーナス全削除
        TargetBonusDatas.AllClear();

    }
    void HighNessChance(BaseStates deathEne)
    {
        var matchSkillCount = 0;
        foreach(var skill in deathEne.SkillList)//倒した敵のスキルで回す
        {
            if(skill.IsTLOA && skill.SkillSpiritual == MyImpression)//スキルがTLOAで自分の精神属性と一致するなら
            {
                matchSkillCount++;
            }
        }

        if(matchSkillCount > 0 && rollper(GetPowerUpChanceOnKillEnemy(matchSkillCount)))//合致数が一個以上あり、ハイネスチャンスの確率を通過すれば。
        {
            Power1Up();
        }
    }
    /// <summary>
    /// 敵を倒した時のパワー増加確率(%)を返す関数。 「ハイネスチャンスの確率」
    /// 精神属性ごとに分岐し、一致スキルの数 × 5% を加算する。
    /// </summary>
    float GetPowerUpChanceOnKillEnemy(int matchingSkillCount)
    {
        // 基礎確率を設定
        float baseChance = MyImpression switch
        {
            SpiritualProperty.kindergarden => 40f,
            SpiritualProperty.liminalwhitetile => 30f,
            _ => 20f
        };

        // 一致スキル数 × 5% を加算
        float totalChance = baseChance + matchingSkillCount * 5f;

        // 必要に応じて上限100%に丸めるなら下記をアンコメント
        // if (totalChance > 100f) totalChance = 100f;

        return totalChance;
    }

    /// <summary>
    /// 攻撃した相手が死んだ場合のコールバック
    /// </summary>
    void OnKill(BaseStates target)
    {
        HighNessChance(target);//ハイネスチャンス(ThePowerの増加判定)
        ApplyConditionChangeOnKillEnemy(target);//人間状況の変化
        
    }


    /// <summary>
    /// 持ってるスキルリストを初期化する
    /// </summary>
    public void OnInitializeSkillsAndChara()
    {
        foreach (var skill in SkillList)
        {
            skill.OnInitialize(this);
        }
    }
    /// <summary>
    /// BM終了時に全スキルの一時保存系プロパティをリセットする
    /// </summary>
    public void OnBattleEndSkills()
    {
        foreach (var skill in SkillList)
        {
            skill.OnBattleEnd();//プロパティをリセットする
        }
    }

    /// <summary>
    ///追加HPを適用  passiveと違い適合条件がないからvoid
    /// </summary>
    public void ApplyVitalLayer(int id)
    {
        //リスト内に同一の物があるか判定する。
        var sameHP = _vitalLaerList.FirstOrDefault(lay => lay.id == id);
        if (sameHP != null)
        {
            sameHP.ReplenishHP();//同一の為リストにある側を再補充する。
        }
        else//初物の場合
        {
            var newLayer = VitalLayerManager.Instance.GetAtID(id);//マネージャーから取得
            //優先順位にリスト内で並び替えつつ追加
            // _vitalLaerList 内で、新しいレイヤーの Priority より大きい最初の要素のインデックスを探す
            int insertIndex = _vitalLaerList.FindIndex(v => v.Priority > newLayer.Priority);

            if (insertIndex < 0)
            {
                // 該当する要素が見つからなかった場合（全ての要素が新しいレイヤー以下の Priority ）
                // リストの末尾に追加
                _vitalLaerList.Add(newLayer);
            }
            else
            {
                // 新しいレイヤーを適切な位置に挿入
                _vitalLaerList.Insert(insertIndex, newLayer);
            }
        }
    }
    /// <summary>
    /// 追加HPを消す
    /// </summary>
    public void RemoveVitalLayerByID(int id)
    {
        var layer = _vitalLaerList.FirstOrDefault(lay => lay.id == id);
        if (layer != null)//あったら消す
        {
            _vitalLaerList.Remove(layer);
        }
        else
        {
            Debug.Log("RemoveVitalLayer nothing. id:" + id);
        }
    }

    /// <summary>歩行時のコールバック引数なしの</summary>
    public void OnWalkNoArgument()
    {
        AllPassiveWalkEffect();//全パッシブの歩行効果を呼ぶ
        UpdateWalkAllPassiveSurvival();
    }

    /// <summary>
    /// 戦闘中に次のターンに進む際のコールバック
    /// </summary>
    public void OnNextTurnNoArgument()
    {
        UpdateTurnAllPassiveSurvival();

        //生きている場合にのみする処理
        if(!Death())
        {
            ConditionInNextTurn();

            if(IsMentalDiverGenceRefilCountDown() == false)//再充填とそのカウントダウンが終わってるのなら
            {
                MentalDiverGence();
                if(_mentalDivergenceRefilCount > 0)//乖離が発生した直後に回復が起こらないようにするif カウントダウンがセットされたら始まってるから
                {
                    MentalHPHealOnTurn();//精神HP自動回復
                }
               
            }
        }
       
        

        //記録系
        _tempLive = !Death();//死んでない = 生きてるからtrue
    }


    /// <summary>
    ///bm生成時に初期化される関数
    /// </summary>
    public void OnBattleStartNoArgument()
    {
        TempDamageTurn = 0;
        _tempVanguard = false;
        _tempLive = true;
        DecisionKinderAdaptToSkillGrouping();//慣れ補正の優先順位のグルーピング形式を決定するような関数とか
        DecisionSacriFaithAdaptToSkillGrouping();
        skillDatas = new List<ACTSkillData>();//スキルの行動記録はbm単位で記録する。
        damageDatas = new();
        TargetBonusDatas = new();
        ConditionTransition();
        _mentalDivergenceRefilCount = 0;//精神HP乖離の再充填カウントをゼロに戻す
        _mentalDivergenceCount = 0;//精神HP乖離のカウントをゼロに戻す
        
    }
    public void OnBattleEndNoArgument()
    {
        TempDamageTurn = 0;
        DeleteConsecutiveATK();
        foreach(var layer in _vitalLaerList.Where(lay => lay.IsBattleEndRemove))
        {
            RemoveVitalLayerByID(layer.id);//戦闘の終了で消える追加HPを持ってる追加HPリストから全部消す
        }
        foreach(var passive in _passiveList.Where(pas => pas.DurationWalk < 0))
        {
            RemovePassive(passive);//歩行残存ターンが-1の場合戦闘終了時に消える。
        }
    }

    //慣れ補正ーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーー慣れ補正ーーーーーーーーーーーーーーーーーーーーーーーーーーーーーーー
    /// <summary>
    /// 慣れ補正用　スキルの注目リスト
    /// </summary>
    public List<FocusedSkillAndUser> FocusSkillList = new List<FocusedSkillAndUser>();

    /// <summary>
    /// ベールドライヴァル用の慣れ補正の優先順位のグルーピング
    /// グルーピングっていうか　3 を境目に二つに分かれるだけ。
    /// </summary>
    int GetBaleAdaptToSkillGrouping(int number)
    {//序列が 0～2　つまり三位までなら
        if (number < 3) return 0;//グループ序列は0
        else return 1;//それ以降の順位なら、グループ序列は１
    }
    /// <summary>
    /// ゴッドティアー用の慣れ補正の優先順位のグルーピング
    /// 6ごとに区分けする
    /// </summary>
    int GetGodtierAdaptToSkillGrouping(int number)
    {
        return number / 6;
    }

    /// <summary>
    /// 支柱用の慣れ補正の優先順位のグルーピング
    /// 5ごとに区分けする
    /// </summary>
    int GetPillarAdaptToSkillGrouping(int number)
    {
        return number / 5;
    }
    /// <summary>
    /// ドレミスは用の慣れ補正の優先順位のグルーピング
    /// 最初の六つ(0～5)  そしてその後は七つ区切り
    /// </summary>
    int GetDoremisAdaptToSkillGrouping(int number)
    {
        if (number < 6)//最初の六つならそのまま
        {
            return number;
        }
        // 6以降は7つごと
        // 6～12 → グループ6
        // 13～19 → グループ7
        // 20～26 → グループ8 ...
        return 6 + (number - 6) / 7;  //6足してんのは最初の六つの固定グループ以降の順列であるから。　6引いてんのは最初の六つによるずれ修正
    }


    /// <summary>
    /// リーミナルホワイト用の素数による慣れ補正の"-スキル優先順位-"のグルーピング方式
    /// 引数の整数が何番目のグループに属するかを返す
    /// グループは0からはじまり、各素数を境界として区切る。
    /// グループnは [p_(n-1), p_n - 1] の範囲（p_0=0と仮定, p_1=2）
    /// 例: p_1=2の場合、グループ1は 0～1
    ///     p_2=3の場合、グループ2は 2～2
    ///     p_3=5の場合、グループ3は 3～4
    /// </summary>
    int GetLiminalAdaptToSkillGrouping(int number)
    {
        // number以上の素数を取得
        int primeAbove = GetPrimeAbove(number);

        // primeAboveがp_nだとして、p_(n-1)からp_n-1までがn番目のグループとなる
        // p_1=2, p_0=0とみなす
        int index = GetPrimeIndex(primeAbove); // primeAboveが何番目の素数か(2が1番目)
        int prevPrime = (index == 1) ? 0 : GetPrimeByIndex(index - 1); // 前の素数(なければ0)

        // prevPrime～(primeAbove-1)がindex番目のグループ
        if (number >= prevPrime && number <= primeAbove - 1)
        {
            return index;
        }

        return -1; // 理論上起こらないが安全策
    }
    /// <summary>
    /// シークイエスト用の十ごとに優先順位のグループ分けし、
    /// 渡されたindexが何番目に属するかを返す関数
    /// </summary>
    int GetCquiestAdaptToSkillGrouping(int number)
    {
        if (number < 0) return -1; // 負数はエラー扱い

        //0~9をグループ0、10~19をグループ1、20~29をグループ2…という風に10刻みで
        // グループ分けを行い、渡された整数がどのグループかを返します。
        // 
        // 例:
        //  - 0～9   → グループ0
        //  - 10～19 → グループ1
        //  - 20～29 → グループ2
        return number / 10;
    }
    /// <summary>
    /// 慣れ補正でintの精神属性ごとのグループ分け保持リストから優先順位のグループ序列を入手する関数
    /// 整数を受け取り、しきい値リストに基づいてその値が所属するグループ番号を返します。
    /// しきい値リストは昇順にソートされていることを想定。
    /// </summary>
    int GetAdaptToSkillGroupingFromList(int number, List<int> sequence)
    {
        //  例: thresholds = [10,20,30]
        // number <= 10 -> グループ0
        // 11 <= number <= 20 -> グループ1
        // 21 <= number <= 30 -> グループ2
        // 31 <= number     -> グループ3

        // 値がしきい値リストの最初の値以下の場合、0番目のグループに属するとする
        // ここは要件に合わせて調整可能。
        for (int i = 0; i < sequence.Count; i++)
        {
            if (number <= sequence[i])
            {
                return i;
            }
        }

        // 全てのしきい値を超えた場合は最後のグループ+1を返す
        return sequence.Count;
    }
    /// <summary>
    /// 自己犠牲用の慣れ補正のグルーピング方式の素数を交えた乱数を決定する。
    /// "bm生成時"に全キャラにこれを通じて決定される。
    /// 指定した数だけ素数を生成し、それらをリストに入れ、それらの素数の間に任意の数の乱数を挿入した数列を作成する
    /// </summary>
    void DecisionSacriFaithAdaptToSkillGrouping()
    {
        var primes = GetFirstNPrimes(countPrimes);
        if (primes == null || primes.Count == 0)
        {
            throw new ArgumentException("primesリストが空です");
        }
        if (insertProbability < 0.0 || insertProbability > 1.0)
        {
            throw new ArgumentException("insertProbabilityは0.0～1.0の間で指定してください");
        }

        List<int> result = new List<int>();

        for (int i = 0; i < primes.Count - 1; i++)
        {
            int currentPrime = primes[i];
            int nextPrime = primes[i + 1];

            // 現在の素数を追加
            result.Add(currentPrime);

            int gapStart = currentPrime + 1;
            int gapEnd = nextPrime - 1;

            // gap範囲を計算
            int gapRange = gapEnd - gapStart + 1;
            if (gapRange > 0)
            {
                // gapRange / 2.5回分の挿入判定を行う
                int tries = (int)Math.Floor(gapRange / 2.5);
                if (tries > 0)
                {
                    List<int> insertedNumbers = new List<int>();

                    for (int t = 0; t < tries; t++)
                    {
                        // 挿入確率判定
                        if (RandomEx.Shared.NextDouble() < insertProbability)
                        {
                            // gap内からランダムに1つ取得
                            int randomValue = RandomEx.Shared.NextInt(gapStart, gapEnd + 1);

                            if (!insertedNumbers.Contains(randomValue)) //重複してたら追加しない。
                                insertedNumbers.Add(randomValue);
                        }
                    }

                    // 取得した乱数をソートして追加（昇順順列にするため）
                    insertedNumbers.Sort();

                    foreach (var val in insertedNumbers)
                    {
                        result.Add(val);
                    }
                }
            }
        }

        // 最後の素数を追加
        result.Add(primes[primes.Count - 1]);

        SacrifaithAdaptToSkillGroupingIntegerList = result;//保持リストに入れる
    }
    //[Header("自己犠牲の慣れ補正用　HPの想定範囲 基本的に初期値からいじらない")]
    int countPrimes = 77;//生成する素数の数
    double insertProbability = 0.2;
    /// <summary>
    /// 自己犠牲用の慣れ補正グルーピングの数列を保持するリスト
    /// </summary>
    List<int> SacrifaithAdaptToSkillGroupingIntegerList;
    /// <summary>
    /// 指定した数の素数を小さい順に返す
    /// 簡易的な実装。多くの素数が欲しい場合は、高速なアルゴリズム(エラトステネスの篩など)に切り替え推奨
    /// </summary>
    private List<int> GetFirstNPrimes(int n)
    {
        List<int> primes = new List<int>();
        int num = 2;

        while (primes.Count < n)
        {
            if (IsPrime(num))
            {
                primes.Add(num);
            }
            num++;
        }

        return primes;
    }
    /// <summary>
    /// キンダーガーデン用の慣れ補正のグルーピング方式の乱数を決定する。
    /// "bm生成時"に全キャラにこれを通じて決定される。
    /// </summary>
    void DecisionKinderAdaptToSkillGrouping()
    {
        // decayRateを計算
        // completionFraction = exp(-decayRate*(maxHP-minHP))
        // decayRate = -ln(completionFraction)/(maxHP-minHP)
        if (completionFraction <= 0f || completionFraction >= 1f)
        {
            // completionFractionは0～1の間で設定してください
            completionFraction = 0.01f;
        }

        decayRate = -Mathf.Log(completionFraction) / (kinderGroupingMaxSimHP - kinderGroupingMinSimHP);

        var sum = 0;
        KinderAdaptToSkillGroupingIntegerList = new List<int>();//慣れ補正用のinteger保持リストを初期化
        //大体70個ほど決定する。hpの大きさに応じて最大間隔が狭まる
        for (var i = 0; i < 70; i++)
        {
            sum += RandomEx.Shared.NextInt(1, Mathf.RoundToInt(GetKinderGroupingIntervalRndMax()) + 1);
            KinderAdaptToSkillGroupingIntegerList.Add(sum);
        }

    }
    //[Header("キンダーガーデンの慣れ補正用　HPの想定範囲 基本的に初期値からいじらない")]
    float kinderGroupingMinSimHP = 1;    // ゲーム中でのHPの想定してる最小値
    float kinderGroupingMaxSimHP = 80;   // ゲーム中での想定してるHPの最大値(ここまでにキンダーガーデンの優先順位間隔が下がりきる。)

    //[Header("キンダーガーデンの慣れ補正用　出力値調整　基本的に初期値からいじらない")]
    float InitKinderGroupingInterval = 17;   // 最小HP時の出力値
    float limitKinderGroupingInterval = 2;    // 最大HP時に近づいていく限界値

    //[Tooltip("最大HP時点で、開始の値から限界の値までの差をどの割合まで縮めるか。\n0に近いほど限界値により近づく(下がりきる)。\n例えば0.01なら1%まで縮まる。")]
    float completionFraction = 0.01f;

    private float decayRate;
    /// <summary>
    /// キンダーガーデン用の慣れ補正グルーピングの数列を保持するリスト
    /// </summary>
    List<int> KinderAdaptToSkillGroupingIntegerList;
    /// <summary>
    /// キンダーガーデン用のグループ区切りでの乱数の最大値をゲットする。
    /// </summary>
    /// <returns></returns>
    float GetKinderGroupingIntervalRndMax()
    {
        // f(hp) = limitValue + (startValue - limitValue) * exp(-decayRate * (キャラの最大HP - minHP))
        float result = limitKinderGroupingInterval + (InitKinderGroupingInterval - limitKinderGroupingInterval) * Mathf.Exp(-decayRate * (_maxHp - kinderGroupingMinSimHP));
        return result;
    }
    /// <summary>
    /// n以上の素数のうち、最初に出てくる素数を返す
    /// nが素数ならnを返す
    /// </summary>
    int GetPrimeAbove(int n)
    {
        if (n <= 2) return 2;
        int candidate = n;
        while (!IsPrime(candidate))
        {
            candidate++;
        }
        return candidate;
    }

    /// <summary>
    /// 素数pが全素数列(2,3,5,7,...)の中で何番目かを返す(2が1番目)
    /// </summary>
    int GetPrimeIndex(int p)
    {
        int count = 0;
        int num = 2;
        while (num <= p)
        {
            if (IsPrime(num))
            {
                count++;
                if (num == p) return count;
            }
            num++;
        }
        return -1;
    }

    /// <summary>
    /// index番目(1-based)の素数を返す
    /// 1 -> 2, 2 -> 3, 3 -> 5, ...
    /// </summary>
    int GetPrimeByIndex(int index)
    {
        if (index < 1) throw new ArgumentException("indexは1以上である必要があります");
        int count = 0;
        int num = 2;
        while (true)
        {
            if (IsPrime(num))
            {
                count++;
                if (count == index)
                {
                    return num;
                }
            }
            num++;
        }
    }

    /// <summary>
    /// 素数判定(簡易)
    /// </summary>
    bool IsPrime(int x)
    {
        if (x < 2) return false;
        if (x == 2) return true;
        if (x % 2 == 0) return false;
        int limit = (int)Math.Sqrt(x);
        for (int i = 3; i <= limit; i += 2)
        {
            if (x % i == 0) return false;
        }
        return true;
    }

    /// <summary>
    /// 注目リスト内でのスキルのy優先順位の序列を返す 0から数えるインデックス　0から数える
    /// </summary>
    int AdaptPriorityDamageToSkill(BaseSkill skill)
    {
        //ダメージの大きさで並び替えて
        FocusSkillList = FocusSkillList.OrderByDescending(skill => skill.TopDmg).ToList();

        return FocusSkillList.FindIndex(fo => fo.skill == skill);
    }
    /// <summary>
    /// 注目リスト内でのスキルのy優先順位の序列を返す 0から数えるインデックス　0から数える
    /// </summary>
    int AdaptPriorityMemoryToSkill(BaseSkill skill)
    {
        //記憶回数のカウントで並び替えて
        FocusSkillList = FocusSkillList.OrderByDescending(skill => skill.MemoryCount).ToList();

        return FocusSkillList.FindIndex(fo => fo.skill == skill);
    }

    /// <summary>
    /// 現在のスキルの優先序列がどのグループ序列に属してるか
    /// 各関数のツールチップにグループ分け方式の説明アリ
    /// </summary>
    int AdaptToSkillsGrouping(int index)
    {
        int groupIndex = -1;
        if (index < 0) return -1;//負の値が優先序列として渡されたらエラー
        switch (MyImpression)//自分の印象によってスキルのグループ分けが変わる。
        {
            case SpiritualProperty.liminalwhitetile:
                groupIndex = GetLiminalAdaptToSkillGrouping(index);
                break;
            case SpiritualProperty.kindergarden:
                // キンダーガーデン用の素数による慣れ補正の"-スキル優先順位-"のグルーピング方式
                // 引数の整数が何番目のグループに属するかを返す
                // 最大HPが多ければ多いほど、乱数の間隔が狭まりやすい　= ダメージ格差による技への慣れの忘れやすさと慣れやすさが低段階化しやすい
                groupIndex = GetAdaptToSkillGroupingFromList(index, KinderAdaptToSkillGroupingIntegerList);
                break;
            case SpiritualProperty.sacrifaith:
                //自己犠牲は素数の間に　素数間隔 / 2.5　回　その間の数の乱数を入れる。
                //つまり素数と乱数の混じった優先順位のグループ分けがされる
                groupIndex = GetAdaptToSkillGroupingFromList(index, SacrifaithAdaptToSkillGroupingIntegerList);
                break;
            case SpiritualProperty.cquiest:
                //シークイエストは十ごとに区分けする。
                groupIndex = GetCquiestAdaptToSkillGrouping(index);
                break;
            case SpiritualProperty.baledrival:
                //ベールドライヴァルは三位以降以前に区分けする。
                groupIndex = GetBaleAdaptToSkillGrouping(index);
                break;
            case SpiritualProperty.godtier:
                //ゴッドティアは六つごとに区分けする
                groupIndex = GetGodtierAdaptToSkillGrouping(index);
                break;
            case SpiritualProperty.pillar:
                //支柱は六つごとに区分けする
                groupIndex = GetPillarAdaptToSkillGrouping(index);
                break;
            case SpiritualProperty.doremis:
                //ドレミスは六つ固定　以降七つ区切り
                groupIndex = GetDoremisAdaptToSkillGrouping(index);
                break;


            default:
                groupIndex = index;//デビルとサイコパスは省く
                break;
        }

        return groupIndex;
    }
    /// <summary>
    /// DEFによる基礎上昇値(慣れ補正)　記憶回数に加算されるものです。
    /// </summary>
    float GetBaseMemoryIncreaseValue()
    {
        var def = DEF();
        if (def <= increaseThreshold)
        {
            // 第1段階: startIncreaseValueからmidLimitIncreaseValueへ収束
            return midLimitIncreaseValue + (startIncreaseValue - midLimitIncreaseValue) * Mathf.Exp(-increaseDecayRate1 * def);
        }
        else
        {
            // 第2段階: threshold超過後はmidLimitIncreaseValueからfinalLimitIncreaseValueへ超緩やかに減少
            float excess = def - increaseThreshold;
            return finalLimitIncreaseValue + (midLimitIncreaseValue - finalLimitIncreaseValue) * Mathf.Exp(-increaseDecayRate2 * excess);
        }
    }
    //[Header("慣れ補正のDEFによる基礎上昇値パラメータ（第1段階）")]
    float startIncreaseValue = 1.89f; // DEF=0での基礎上昇値 
    float midLimitIncreaseValue = 4.444f; // 中間で収束する上昇値 
    float increaseDecayRate1 = 0.0444f; // 第1段階でstart→midLimitへ近づく速度 
    float increaseThreshold = 100f; // 第2段階移行DEF値 

    //[Header("慣れ補正のDEFによる基礎上昇値パラメータ（第2段階）")]
    float finalLimitIncreaseValue = 8.9f; // 第2段階で最終的に近づく値 
    float increaseDecayRate2 = 0.0027f; // 第2段階でmid→finalLimitへ近づく速度 
    /// <summary>
    /// DEFによる基礎減少値を返す。　これは慣れ補正の記憶回数に加算される物。
    /// </summary>
    float GetBaseMemoryReducationValue()
    {
        var def = DEF();//攻撃によって減少されないまっさらな防御力
        if (def <= thresholdDEF)
        {
            // 第1段階: StartValueからmidLimitValueへ収束
            // f(DEF) = midLimitValue + (StartValue - midLimitValue)*exp(-decayRate1*DEF)
            return midLimitValue + (startValue - midLimitValue) * Mathf.Exp(-decayRate1 * def);
        }
        else
        {
            // 第2段階: thresholdを超えたらmidLimitValueから0へ超ゆるやかな減衰
            // f(DEF) = finalLimitValue + midlimtValue * exp(-decayRate2*(DEF - threshold))
            float excess = def - thresholdDEF;
            return finalLimitValue + (midLimitValue - finalLimitValue) * Mathf.Exp(-decayRate2 * excess);
        }
    }
    //[Header("慣れ補正のDEFによる基礎減少値パラメータ（第1段階）")]
    float startValue = 0.7f;   // DEF=0での基礎減少値
    float midLimitValue = 0.2f; // 中間の下限値(比較的到達しやすい値)
    float decayRate1 = 0.04f;  // 第1段階で開始値から中間の下限値へ近づく速度
    float thresholdDEF = 88f;    // 第1段階から第2段階へ移行するDEF値

    //[Header("パラメータ（第2段階）")]
    // 第2段階：0.2から0への超低速な減衰
    float finalLimitValue = 0.0f;//基礎減少値がDEFによって下がりきる最終下限値　　基本的に0
    float decayRate2 = 0.007f; // 非常に小さい値にしてfinalLimitValueに収束するには莫大なDEFが必要になる

    /// <summary>
    /// 前にダメージを受けたターン
    /// </summary>
    int TempDamageTurn;
    /// <summary>
    /// 記憶回数の序列割合をゲット
    /// 指定されたインデックスがリスト内でどの程度の割合に位置しているかを計算します。
    /// 先頭が1.0、末尾が0.0の割合となります。 
    /// </summary>
    float GetMemoryCountRankRatio(int index)
    {
        if (FocusSkillList.Count == 1)
            return 1.0f; // リストに1つだけの場合、割合は1.0

        // 先頭が1.0、末尾が0.0となるように割合を計算
        return 1.0f - ((float)index / (FocusSkillList.Count - 1));
    }
    /// <summary>
    /// 自身の精神属性による記憶段階構造と範囲の取得
    /// </summary>
    /// <returns></returns>
    List<MemoryDensity> MemoryStageStructure()
    {
        List<MemoryDensity> rl;
        switch (MyImpression)//左から降順に入ってくる　一番左が最初の、一番上の値ってこと
        {
            case SpiritualProperty.doremis:
                rl = new List<MemoryDensity> { MemoryDensity.High, MemoryDensity.Medium, MemoryDensity.Medium };
                break;//しっかりと　普通　普通

            case SpiritualProperty.pillar:
                rl = new List<MemoryDensity> { MemoryDensity.Medium, MemoryDensity.Medium, MemoryDensity.Medium,
                MemoryDensity.Medium,MemoryDensity.Medium,MemoryDensity.Medium,};
                break;//普通　×6

            case SpiritualProperty.kindergarden:
                rl = new List<MemoryDensity> { MemoryDensity.Low };
                break;//薄い

            case SpiritualProperty.liminalwhitetile:
                rl = new List<MemoryDensity> { MemoryDensity.Medium,MemoryDensity.Medium,
                    MemoryDensity.Low,MemoryDensity.Low, MemoryDensity.Low};
                break;//普通×2 薄い×3

            case SpiritualProperty.sacrifaith:
                rl = new List<MemoryDensity> { MemoryDensity.High, MemoryDensity.Low };
                break;//ハイアンドロー

            case SpiritualProperty.cquiest:
                rl = new List<MemoryDensity> { MemoryDensity.High, MemoryDensity.High,MemoryDensity.High,MemoryDensity.High, MemoryDensity.High,
                MemoryDensity.Low};//しっかりと×5 //薄い1
                break;

            case SpiritualProperty.pysco:
                rl = new List<MemoryDensity> { MemoryDensity.High, MemoryDensity.Low };
                break;//ハイアンドロー

            case SpiritualProperty.godtier:
                rl = new List<MemoryDensity> { MemoryDensity.High,MemoryDensity.High,MemoryDensity.Medium,
                MemoryDensity.Medium,MemoryDensity.Medium,MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low};
                break;//しっかりと×2 普通×3 薄く×3

            case SpiritualProperty.baledrival:
                rl = new List<MemoryDensity> { MemoryDensity.High,MemoryDensity.High,MemoryDensity.High,MemoryDensity.High,
                MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low,
                MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low};
                break;//しっかりと×4 薄く　×8

            case SpiritualProperty.devil:
                rl = new List<MemoryDensity> { MemoryDensity.Medium,MemoryDensity.Medium,
                MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low};
                break;//普通×2 薄く×3
            default:
                rl = new List<MemoryDensity> { MemoryDensity.Low };
                break;//適当

        }
        return rl;
    }
    /// <summary>
    /// 攻撃力を減衰する最終的な"慣れ"の基礎量
    /// </summary>
    float GetBaseAdaptValue()
    {
        const float bValue = 0.0004f; //ここの単位調節は1バトルの長さと密接に関係すると思う。

        return bValue * b_EYE;//基礎命中率で補正。　「慣れは"元々"の視力と、記憶の精神由来の構成が物を言います。」
    }
    /// <summary>
    /// 慣れ補正割合値のデフォルトの下限しきい値
    /// </summary>
    const float initialAdaptThreshold = 0.85f;
    /// <summary>
    /// 慣れ補正割合値の下限しきい値がEYEが高いと下がる限界値
    /// </summary>
    const float maxEyeAdaptThreshold = 0.5566778f;

    /// <summary>
    /// EYE()を用いてAdaptModifyが下回らないようにする特定の下限しきい値を計算する関数
    /// - EYE()が0～30の間ではthresholdは0.85に固定
    /// - EYE()が30を超え、134まで増加するにつれてthresholdが非線形に0.85からmaxEyeThresholdへ減少
    /// - EYE()が134以上ではthresholdはmaxEyeThresholdに固定
    /// </summary>
    float CalculateEYEBasedAdaptThreshold()
    {
        // 定数の設定
        const float minEYE = 30f;//この値まではデフォルトのまま
        const float maxEYE = 134f;//EYEが補正される限界値

        // 現在のEYE()値を取得
        float eyeValue = EYE();

        // EYE()の値に応じてthresholdを計算
        if (eyeValue <= minEYE)
        {
            // EYE()が30以下の場合
            return initialAdaptThreshold;
        }
        else if (eyeValue >= maxEYE)
        {
            // EYE()が134以上の場合
            return maxEyeAdaptThreshold;
        }
        else
        {
            // EYE()が30を超え134未満の場合

            // EYE()を0から1に正規化（30から134を0から1にマッピング）
            float normalizedEye = (eyeValue - minEYE) / (maxEYE - minEYE);

            // シグモイド関数のパラメータ設定
            // k: 勾配（大きいほど急激な変化）
            // x0: シグモイドの中心点（ここでは0.5に設定）
            float k = 10f;        // 調整可能なパラメータ
            float x0 = 0.5f;      // 中心点

            // シグモイド関数の計算
            float sigmoid = 1 / (1 + Mathf.Exp(-k * (normalizedEye - x0)));

            // thresholdを計算
            // thresholdは初期値からmaxEyeThresholdへの変化
            float threshold = initialAdaptThreshold - (initialAdaptThreshold - maxEyeAdaptThreshold) * sigmoid;

            // クランプ（安全策としてしきい値が範囲内に収まるように）
            threshold = Mathf.Clamp(threshold, maxEyeAdaptThreshold, initialAdaptThreshold);

            return threshold;
        }
    }

    /// <summary>
    /// 目の瞬きをするように、
    /// 慣れ補正がデフォルトの下限しきい値を下回っている場合、そこまで押し戻される関数
    /// </summary>
    /// <param name="adapt">現在の慣れ補正値 (例: 0.7 など)</param>
    /// <param name="largestMin">最大下限しきい値(例: 0.556677)</param>
    /// <param name="defaultMin">デフォルトの下限しきい値(例: 0.85)</param>
    /// <param name="kMax">バネ係数の最大値(適宜調整)</param>
    /// <returns>押し戻し後の値(一度きりで計算)</returns>    
    float EyeBlink(
    float adapt,
    float largestMin,
    float defaultMin,
    float kMax
)
    {
        // もし adapt が defaultMin 以上なら押し戻し不要なので、そのまま返す
        if (adapt >= defaultMin)
            return adapt;

        // 1) ratio = (adapt - largestMin) / (defaultMin - largestMin)
        //    → adapt が largestMin に近いほど ratio が小さくなり、結果として k が大きくなる
        //    → adapt が 0.85 に近いほど ratio が 1 に近くなり、k は 0 に近づく(押しが弱い)
        float ratio = (adapt - largestMin) / (defaultMin - largestMin);
        ratio = Mathf.Clamp01(ratio);

        // 2) k を計算：近いほど k 大きく
        //    ここでは単純に k = kMax * (1 - ratio)
        //    ratio=0(=adapt==largestMin付近) → k=kMax(最大)
        //    ratio=1(=adapt==defaultMin付近) → k=0(押しなし)
        float k = kMax * (1f - ratio);

        // 3) バネ式で一度だけ押し上げ
        float diff = defaultMin - adapt;  // 正の数(例: 0.85-0.7=0.15)
                                          // e^(-k) を掛ける
        float newDiff = diff * Mathf.Exp(-k);
        // 実際に adapt を上書き
        float newAdapt = defaultMin - newDiff; // 例: 0.85 - (0.15*exp(-k))

        // 4) もし何らかの理由で newAdapt が既に defaultMin 超えるならクランプ
        if (newAdapt > defaultMin) newAdapt = defaultMin;

        return newAdapt;
    }

    //[Header("慣れ補正のランダム性設定")]
    //[SerializeField, Range(0.0f, 0.2f)]//インスペクタ上で調節するスライダーの範囲
    private float randomVariationRange = 0.04f; // ±%の変動
    /// <summary>
    /// スキルに慣れる処理 慣れ補正を返す
    /// </summary>
    float AdaptToSkill(BaseStates enemy, BaseSkill skill, float dmg)
    {
        var donthaveskill = true;//持ってないフラグ
        var IsFirstAttacker = false;//知っているスキルに食らったとき、その攻撃者が初見かどうか
        var IsConfused = false;//戸惑いフラグ
        float AdaptModify = -1;//デフォルト値
        var nowTurn = manager.BattleTurnCount;//現在のターン数
        FocusedSkillAndUser NowFocusSkill = null;//今回食らった注目慣れスキル

        //今回食らうスキルが既に食らってるかどうかの判定ーーーーーーーーーーーーーーーーー
        foreach (var fo in FocusSkillList)
        {
            if (fo.skill == skill)//スキル既にあるなら
            {
                fo.DamageMemory(dmg);// ダメージ記録
                donthaveskill = false;//既にあるフラグ！
                if (IsFirstAttacker = !fo.User.Any(chara => chara == enemy))//攻撃者が人員リストにいない場合　true
                {
                    fo.User.Add(enemy);//敵をそのスキルのユーザーリストに登録
                }
                NowFocusSkill = fo;//既にあるスキルを今回の慣れ注目スキルに
            }
        }
        //もし初めて食らうのならーーーーーーーーーーーーーーーーー
        if (donthaveskill)
        {
            NowFocusSkill = new FocusedSkillAndUser(enemy, skill, dmg);//新しく慣れ注目スキルに
            FocusSkillList.Add(NowFocusSkill);//最初のキャラクターとスキルを記録
        }

        //前回"スキル問わず"攻撃を受けてから今回受けるまでの　"経過ターン"
        //(スキル性質がAttackのとき、必ず実行されるから　攻撃を受けた間隔　が経過ターンに入ります　スキルによる差はありません。)
        var DeltaDamageTurn = Math.Abs(nowTurn - TempDamageTurn);

        //今回食らった以外の全てのスキルの記憶回数をターン数経過によって減らすーーーーーーーーーーーーーーーーーーーーーーーーーーーーー
        var templist = FocusSkillList;

        templist.Remove(NowFocusSkill);//今回の慣れ注目スキルを省く
        foreach (var fo in templist)
        {
            //まず優先順位を取得し、グループ序列(スキルの最終優先ランク)を取得
            var finalSkillRank = AdaptToSkillsGrouping(AdaptPriorityDamageToSkill(fo.skill));

            //DEFによる基礎減少値を取得
            var b_ReductionValue = GetBaseMemoryReducationValue();

            //DEFによる固定値と優先順位を計算して、どのくらい減るか　
            //優先順位が低ければ低いほど、つまりfinalSkillRankが多ければ多いほど、記憶回数が減りやすい(だからそのまま計算できる)
            var DeathMemoryFloat = 0f;//記憶忘却回数

            var rankNotTopModify = 0f;//二位以降での補正
            if (finalSkillRank > 0) rankNotTopModify = 0.08f;//優先順位が一軍でないのなら、序列補正に加算される固定値
            var PriorityModify = 1 + finalSkillRank / 8 + rankNotTopModify;//序列補正

            //計算　記憶忘却回数 = 序列補正×基礎減少値×経過ターン　
            //そのスキルの記憶回数の序列の割合/(3～2)により、 乱数判定成功したら、　　記憶忘却回数 /= 3　

            DeathMemoryFloat = PriorityModify * b_ReductionValue * DeltaDamageTurn;


            //記憶回数の序列割合を入手
            var MemoryRankRatio = GetMemoryCountRankRatio(AdaptPriorityMemoryToSkill(skill));

            var mod1 = RandomEx.Shared.NextFloat(2, 4);//2～3
            var rat1 = MemoryRankRatio / mod1;
            if (RandomEx.Shared.NextFloat(1f) < rat1)//乱数判定　成功したら。
            {
                DeathMemoryFloat /= 3;//3分の一に減衰される
            }


            fo.Forget(DeathMemoryFloat);//減る数だけ減る

        }


        //記憶回数による記憶範囲の判定と慣れ補正の計算☆ーーーーーーーーーーーーーーーーーーーーーーーーー

        //スキルの記憶回数での並べ替え
        //記憶回数が多い方から数えて、　　"今回のスキル"がそれに入ってるなら慣れ補正を返す
        //数える範囲は　記憶範囲
        FocusSkillList = FocusSkillList.OrderByDescending(skill => skill.MemoryCount).ToList();

        //記憶段階と範囲の取得　　
        var rl = MemoryStageStructure();

        //二回目以降で記憶範囲にあるのなら、補正計算して返す
        if (!donthaveskill)
        {
            for (var i = 0; i < rl.Count; i++)//記憶段階と範囲のサイズ分ループ
            {
                var fo = FocusSkillList[i];
                if (fo.skill == skill)//もし記憶範囲に今回のスキルがあるならば
                {
                    //もしスキルを使う行使者が初見なら(二人目以降の使用者)
                    //精神属性によっては戸惑って補正はない　　戸惑いフラグが立つ
                    if (IsFirstAttacker)
                    {
                        switch (MyImpression)
                        {//ドレミス　ゴッドティア　キンダー　シークイエストは戸惑わない
                            case SpiritualProperty.doremis:
                                IsConfused = false; break;
                            case SpiritualProperty.godtier:
                                IsConfused = false; break;
                            case SpiritualProperty.kindergarden:
                                IsConfused = false; break;
                            case SpiritualProperty.cquiest:
                                IsConfused = false; break;
                            default:
                                IsConfused = true; break;//それ以外で初見人なら戸惑う
                        }
                    }

                    if (!IsConfused)//戸惑ってなければ、補正がかかる。(デフォルト値の-1でなくなる。)
                    {
                        var BaseValue = GetBaseAdaptValue();//基礎量
                        var MemoryValue = Mathf.Floor(fo.MemoryCount);//記憶回数(小数点以下切り捨て)

                        float MemoryPriority = -1;//記憶段階による補正
                        switch (rl[i])
                        {
                            case MemoryDensity.Low:
                                MemoryPriority = 1.42f;
                                break;
                            case MemoryDensity.Medium:
                                MemoryPriority = 3.75f;
                                break;
                            case MemoryDensity.High:
                                MemoryPriority = 10f;
                                break;
                        }

                        //一回計算
                        AdaptModify = 1 - (BaseValue * MemoryValue * MemoryPriority);

                        // ランダムファクターの生成
                        float randomFactor = RandomEx.Shared.NextFloat(1.0f - randomVariationRange, 1.0f + randomVariationRange);
                        AdaptModify *= randomFactor;

                        //下限しきい値の設定
                        var Threshold = CalculateEYEBasedAdaptThreshold();

                        //もしデフォルトの下限しきい値を慣れ補正が下回っていたら
                        if (initialAdaptThreshold > AdaptModify)
                        {
                            var chance = (int)(777 - b_EYE * 5);//b_eyeの0~150 0.1~3.7%推移　 以降は5.2%
                            chance = Mathf.Max(19, chance);

                            if (RandomEx.Shared.NextInt(chance) == 0)//瞬きが起きる機会
                            {
                                AdaptModify = EyeBlink(AdaptModify, maxEyeAdaptThreshold, initialAdaptThreshold, 2.111f);
                            }
                        }


                        //もし最終的な慣れの補正量がしきい値を下回っていた場合、しきい値に固定される
                        if (Threshold > AdaptModify)
                        {
                            AdaptModify = Threshold;
                        }
                    }

                    //"慣れ減衰"の計算に使用☆

                    //fo.MemoryCount  //記憶回数の数(切り下げ、小数点以下切り捨て)
                    //rl[i]  //精神属性による段階
                    //EYEによる基礎量

                    break;//スキルを見つけ処理を終えたので、記憶範囲ループから外れる
                }
            }

            TempDamageTurn = nowTurn;//今回の被害ターンを記録する。
        }


        //戸惑いが立ってると記憶回数は増加しない
        if (!IsConfused)
        {//FocuseSkillはコンストラクタでMemory()されないため、donthaveSkillに関わらず、実行されます。

            //今回食らったスキルの記憶回数を増やすーーーーーーーーーーーーーーーーーーーーーーーーーーー☆
            var finalSkillRank1 = AdaptToSkillsGrouping(AdaptPriorityDamageToSkill(NowFocusSkill.skill));//優先順位取得
                                                                                                         //基礎上昇値取得
                                                                                                         //DEFによる基礎上昇値を取得
            var b_IncreaseValue = GetBaseMemoryIncreaseValue();

            // 優先順位による補正　値は変更されます。
            // (例)：一軍は2.0倍、下位になるほど0.9倍ずつ減らす
            // rank=0で2.0, rank=1で1.8, rank=2で1.62 ...など
            float priorityBaseGain = 2.2f * Mathf.Pow(0.77f, finalSkillRank1);

            //一軍なら微々たる追加補正
            float rankTopIncreaseModify = finalSkillRank1 == 0 ? 0.05f : 0f;//一軍ならば、左の値が優先順位補正に加算
            float PriorityIncreaseModify = priorityBaseGain + rankTopIncreaseModify;

            // 攻撃を受けてからの経過ターンが少ないほどターンボーナス(掛け算)が増す（
            float TurnBonus = 1.0f;//デフォルト値
            if (DeltaDamageTurn < 5) TurnBonus += 0.1f;//4ターン以内
            if (DeltaDamageTurn < 4) TurnBonus += 0.45f;//3ターン以内
            if (DeltaDamageTurn < 3) TurnBonus += 0.7f;//2ターン以内

            //記憶回数による微加算　(これは掛けるのではなく最終計算結果に加算する¥)
            float MemoryAdjust = 0.08f * NowFocusSkill.MemoryCount;

            // 最終的な増加量計算
            // メモリ増加例: (基礎上昇値 * 優先順位補正 * 記憶割合補正 + ターン補正)
            float MemoryIncrease = b_IncreaseValue * PriorityIncreaseModify * TurnBonus + MemoryAdjust;

            //注目スキルとして記憶回数が増える。
            NowFocusSkill.Memory(MemoryIncrease);
        }

        //慣れ補正がデフォルト値の-1のままだった場合、1.0として返す。
        if (AdaptModify < 0) AdaptModify = 1.0f;

        return AdaptModify;
    }

    //static 静的なメゾット(戦いに関する辞書データなど)

    /// <summary>
    /// 精神属性と十日能力の互換表
    /// </summary>
    public static readonly Dictionary<SpiritualProperty, List<TenDayAbility>> SpritualTenDayAbilitysMap = 
    new()
    {
        {SpiritualProperty.doremis, new List<TenDayAbility>(){TenDayAbility.Enokunagi, TenDayAbility.PersonaDivergence, TenDayAbility.KereKere, TenDayAbility.Rain, TenDayAbility.BlazingFire}},
        {SpiritualProperty.pillar, new List<TenDayAbility>(){TenDayAbility.JoeTeeth, TenDayAbility.Sort, TenDayAbility.SilentTraining, TenDayAbility.Leisure, TenDayAbility.Vond}},
        {SpiritualProperty.kindergarden, new List<TenDayAbility>(){TenDayAbility.dokumamusi, TenDayAbility.Baka, TenDayAbility.TentVoid, TenDayAbility.SpringNap, TenDayAbility.WaterThunderNerve}},
        {SpiritualProperty.liminalwhitetile, new List<TenDayAbility>(){TenDayAbility.FlameBreathingWife, TenDayAbility.NightDarkness, TenDayAbility.StarTersi, TenDayAbility.FaceToHand, TenDayAbility.Pilmagreatifull}},
        {SpiritualProperty.sacrifaith, new List<TenDayAbility>(){TenDayAbility.UnextinguishedPath, TenDayAbility.Miza, TenDayAbility.JoeTeeth, TenDayAbility.SpringNap}},
        {SpiritualProperty.cquiest, new List<TenDayAbility>(){TenDayAbility.ColdHeartedCalm, TenDayAbility.NightDarkness, TenDayAbility.NightInkKnight, TenDayAbility.Glory, TenDayAbility.SpringNap}},
        {SpiritualProperty.pysco, new List<TenDayAbility>(){TenDayAbility.Raincoat, TenDayAbility.TentVoid, TenDayAbility.Blades, TenDayAbility.Smiler, TenDayAbility.StarTersi}},
        {SpiritualProperty.godtier, new List<TenDayAbility>(){TenDayAbility.HeavenAndEndWar, TenDayAbility.Vail, TenDayAbility.BlazingFire, TenDayAbility.FlameBreathingWife, TenDayAbility.SpringWater}},
        {SpiritualProperty.baledrival, new List<TenDayAbility>(){TenDayAbility.Smiler, TenDayAbility.Miza, TenDayAbility.HeatHaze, TenDayAbility.Vail}},
        {SpiritualProperty.devil, new List<TenDayAbility>(){TenDayAbility.CryoniteQuality, TenDayAbility.HumanKiller, TenDayAbility.HeatHaze, TenDayAbility.FaceToHand}},
    };

    /// <summary>
    /// 戦闘規格ごとのデフォルトa,bの狙い流れ
    /// </summary>
    public static readonly Dictionary<BattleProtocol, (AimStyle aStyle,float a,AimStyle bStyle)> DefaultDefensePatternPerProtocol =
        new ()
        {
            {BattleProtocol.LowKey,(AimStyle.AcrobatMinor,0.6f,AimStyle.Doublet)},
            {BattleProtocol.Tricky,(AimStyle.Duster,0.9f,AimStyle.QuadStrike)},
            {BattleProtocol.Showey,(AimStyle.CentralHeavenStrike,0.8f,AimStyle.Doublet)}
        };


    /// <summary>
    /// 指定したAimStyleを除いた中からランダムに1つ選択する
    /// </summary>
    private AimStyle GetRandomAimStyleExcept(AimStyle excludeStyle)
    {
        // 全てのAimStyleの値を配列として取得
        var allStyles = Enum.GetValues(typeof(AimStyle))
                        .Cast<AimStyle>()
                        .Where(style => style != excludeStyle)
                        .ToArray();
        
        // ランダムに1つ選択して返す
        return RandomEx.Shared.GetItem(allStyles);
    }
    /// <summary>
    /// 攻撃者の狙い流れ(Aimstyle)、受け手の"現在の変更前の"防ぎ方(Aimstyle)の組み合わせによって受け手の防ぎ方変更までの最大ターン数を算出する辞書。
    /// 【その狙い流れを受けて、現在の防ぎ方がそのAimStyleに対応するまでのカウント】であって、決して前の防ぎ方から今回の防ぎ方への変化ではない。(複雑なニュアンスの違い)
    /// </summary>
    public static readonly Dictionary<(AimStyle attackerAIM, AimStyle nowDefenderAIM), int> DefenseTransformationThresholds =  
    new Dictionary<(AimStyle attackerAIM, AimStyle defenderAIM), int>()
    {
    { (AimStyle.AcrobatMinor, AimStyle.Doublet), 2 },         // アクロバマイナ体術1 ← ダブレット
    { (AimStyle.AcrobatMinor, AimStyle.QuadStrike), 6 },      // アクロバマイナ体術1 ← 四弾差し込み
    { (AimStyle.AcrobatMinor, AimStyle.Duster), 4 },          // アクロバマイナ体術1 ← ダスター
    { (AimStyle.AcrobatMinor, AimStyle.PotanuVolf), 7 },      // アクロバマイナ体術1 ← ポタヌヴォルフのほうき術系
    { (AimStyle.AcrobatMinor, AimStyle.CentralHeavenStrike), 4 }, // アクロバマイナ体術1 ← 中天一弾
    { (AimStyle.Doublet, AimStyle.AcrobatMinor), 3 },         // ダブレット ← アクロバマイナ体術1
    { (AimStyle.Doublet, AimStyle.QuadStrike), 6 },           // ダブレット ← 四弾差し込み
    { (AimStyle.Doublet, AimStyle.Duster), 8 },               // ダブレット ← ダスター
    { (AimStyle.Doublet, AimStyle.PotanuVolf), 4 },           // ダブレット ← ポタヌヴォルフのほうき術系
    { (AimStyle.Doublet, AimStyle.CentralHeavenStrike), 7 },  // ダブレット ← 中天一弾
    { (AimStyle.QuadStrike, AimStyle.AcrobatMinor), 4 },      // 四弾差し込み ← アクロバマイナ体術1
    { (AimStyle.QuadStrike, AimStyle.Doublet), 2 },           // 四弾差し込み ← ダブレット
    { (AimStyle.QuadStrike, AimStyle.Duster), 5 },            // 四弾差し込み ← ダスター
    { (AimStyle.QuadStrike, AimStyle.PotanuVolf), 6 },        // 四弾差し込み ← ポタヌヴォルフのほうき術系
    { (AimStyle.QuadStrike, AimStyle.CentralHeavenStrike), 4 }, // 四弾差し込み ← 中天一弾
    { (AimStyle.Duster, AimStyle.AcrobatMinor), 3 },          // ダスター ← アクロバマイナ体術1
    { (AimStyle.Duster, AimStyle.Doublet), 8 },               // ダスター ← ダブレット
    { (AimStyle.Duster, AimStyle.QuadStrike), 4 },            // ダスター ← 四弾差し込み
    { (AimStyle.Duster, AimStyle.PotanuVolf), 7 },            // ダスター ← ポタヌヴォルフのほうき術系
    { (AimStyle.Duster, AimStyle.CentralHeavenStrike), 5 },   // ダスター ← 中天一弾
    { (AimStyle.PotanuVolf, AimStyle.AcrobatMinor), 2 },      // ポタヌヴォルフのほうき術系 ← アクロバマイナ体術1
    { (AimStyle.PotanuVolf, AimStyle.Doublet), 3 },           // ポタヌヴォルフのほうき術系 ← ダブレット
    { (AimStyle.PotanuVolf, AimStyle.QuadStrike), 5 },        // ポタヌヴォルフのほうき術系 ← 四弾差し込み
    { (AimStyle.PotanuVolf, AimStyle.Duster), 4 },            // ポタヌヴォルフのほうき術系 ← ダスター
    { (AimStyle.PotanuVolf, AimStyle.CentralHeavenStrike), 5 }, // ポタヌヴォルフのほうき術系 ← 中天一弾
    { (AimStyle.CentralHeavenStrike, AimStyle.AcrobatMinor), 4 }, // 中天一弾 ← アクロバマイナ体術1
    { (AimStyle.CentralHeavenStrike, AimStyle.Doublet), 3 },      // 中天一弾 ← ダブレット
    { (AimStyle.CentralHeavenStrike, AimStyle.QuadStrike), 6 },   // 中天一弾 ← 四弾差し込み
    { (AimStyle.CentralHeavenStrike, AimStyle.Duster), 8 },       // 中天一弾 ← ダスター
    { (AimStyle.CentralHeavenStrike, AimStyle.PotanuVolf), 2 }    // 中天一弾 ← ポタヌヴォルフのほうき術系
    };

    /// <summary>
    /// 精神属性でのスキルの補正値　スキルの精神属性→キャラクター属性 csvDataからロード
    /// </summary>
    protected static Dictionary<(SpiritualProperty, SpiritualProperty), FixedOrRandomValue> SkillSpiritualModifier;

    /// <summary>
    /// セルの文字列を整数にパースする。空または無効な場合はデフォルト値を返す。
    /// </summary>
    /// <param name="cell">セルの文字列</param>
    /// <returns>パースされた整数値またはデフォルト値</returns>
    private static int ParseCell(string cell)
    {
        if (int.TryParse(cell, out int result))
        {
            return result;
        }//空セルの場合は整数変換に失敗してelseが入る　splitで,,みたいに区切り文字が二連続すると""空文字列が入る
        else return -1;  //空セルには-1が入る　空セルが入るのはrndMaxが入る所のみになってるはずなので、最大値が無効になる-1が入る
    }
    /// <summary>
    /// BaseStatus内で使われるデータ用のcsvファイルをロード
    /// </summary>
    public async static void CsvLoad()
    {
        SkillSpiritualModifier = new Dictionary<(SpiritualProperty, SpiritualProperty), FixedOrRandomValue>();//初期化
        var csvFile = "Assets/csvData/SpiritualMatchData.csv";

        var textHandle = await Addressables.LoadAssetAsync<TextAsset>(csvFile);


        var rows = textHandle.text //そのままテキストを渡す
            .Split("\n")//改行ごとに分割
                        //.Select(line => line.Trim())//行の先頭と末尾の空白や改行を削除する
            .Select(line => line.Split(',').Select(ParseCell).ToArray()) //それをさらにカンマで分割してint型に変換して配列に格納する。
            .ToArray(); //配列になった行をさらに配列に格納する。
        /*
         * new List<List<int>> {  実際はarrayだけどこういうイメージ
            new List<int> { 50, 20, 44, 53, 42, 37, 90, 100, 90, 50 },
            new List<int> { 60, 77, 160, 50, 80, 23, 32, 50, 51, 56 }}
         */

        var SpiritualCsvArrayRows = new[]
        {
            //精神攻撃の相性の　行の属性並び順
            SpiritualProperty.liminalwhitetile,
            SpiritualProperty.kindergarden,
            SpiritualProperty.sacrifaith,
            SpiritualProperty.cquiest,
            SpiritualProperty.devil,
            SpiritualProperty.devil,//乱数のmax
            SpiritualProperty.doremis,
            SpiritualProperty.pillar,
            SpiritualProperty.godtier,
            SpiritualProperty.baledrival,
            SpiritualProperty.pysco
        };
        var SpiritualCsvArrayColumn = new[]
        {
            //精神攻撃の相性の　列の属性並び順
            SpiritualProperty.liminalwhitetile,
            SpiritualProperty.kindergarden,
            SpiritualProperty.sacrifaith,
            SpiritualProperty.cquiest,
            SpiritualProperty.devil,
            SpiritualProperty.doremis,
            SpiritualProperty.pillar,
            SpiritualProperty.godtier,
            SpiritualProperty.baledrival,
            SpiritualProperty.baledrival,//乱数のmax
            SpiritualProperty.pysco
        };


        for (var i = 0; i < rows.Length; i++) //行ごとに回していく oneは行たちを格納した配列
        {
            //4行目と5行目はdevilへの乱数min,max

            //min 部分でクラスを生成　max部分で既にあるクラスにmaxをセット　空なら-1
            //つまり乱数のmaxにあたる行でのみSetmaxが既にある辞書に実行されるという仕組みにすればいいのだ楽だラクダ
            for (var j = 0; j < rows[i].Length; j++) //数字ごとに回す　one[j]は行の中の数字を格納した配列
            {
                //8,9列目はbaleが相手に対する乱数min,max

                var key = (SpiritualCsvArrayColumn[j], SpiritualCsvArrayRows[i]);
                var value = rows[i][j];
                if (i == 5 || j == 9)//もし五行目、または九列目の場合
                {
                    if (SkillSpiritualModifier.ContainsKey(key))//キーが既にあれば
                    {
                        SkillSpiritualModifier[key].SetMax(value);//乱数最大値を設定
                        //Debug.Log($"乱数セット{value}");
                    }
                    else
                    {
                        Debug.LogError($"キー {key} が存在しません。SetMax を実行できません。");
                    }
                    //既にある辞書データの乱数単一の値のクラスに最大値をセット
                }
                else
                {
                    //固定値としてクラスを生成 (生成時にrndMaxに初期値-1が入るよ)
                    if (!SkillSpiritualModifier.ContainsKey(key))//キーが存在していなければ
                    {
                        SkillSpiritualModifier.Add(key, new FixedOrRandomValue(value));//キーを追加
                    }
                    else
                    {
                        Debug.LogWarning($"キー {key} は既に存在しています。追加をスキップします。");
                    }

                }


            }
        }


        /*Debug.Log("読み込まれたキャラクター精神スキル補正値\n" +
              string.Join(", ",
                  SkillSpiritualModifier.Select(kvp => $"[{kvp.Key}: {kvp.Value.GetValue()} rndMax({kvp.Value.rndMax})]" + "\n")); //デバックで全内容羅列。*/
    }
}

/// <summary>
/// 固定値か最大値、最小値に応じた乱数のどっちかを返すクラス
/// </summary>
public class FixedOrRandomValue
{

    private int rndMax;//乱数の最大値 乱数かどうかはrndMaxに-1を入れればいい
    private int rndMinOrFixed;//単一の値または乱数としての最小値

    /// <summary>
    /// クラス生成
    /// </summary>
    /// <param name="isRnd">乱数として保持するかどうか</param>
    /// <param name="minOrFixed">最小値または単一の値として</param>
    /// <param name="max">省略可能、乱数なら最大値</param>
    public FixedOrRandomValue(int minOrFixed)
    {
        rndMinOrFixed = minOrFixed;//まず最小値またはデフォルトありきでクラスを作成
        rndMax = -1;//予め無を表す-1で初期化
    }

    public void SetMax(int value)
    {
        rndMax = value;//-1を指定するとないってこと
    }
    public int GetValue()
    {
        if (rndMax == -1) return rndMinOrFixed;//乱数じゃないなら単一の値が返る

        return RandomEx.Shared.NextInt(rndMinOrFixed, rndMax + 1);//ランダムなら

    }
}
/// <summary>
/// 慣れ補正で使用するスキルとその使用者
/// </summary>
public class FocusedSkillAndUser
{
    public FocusedSkillAndUser(BaseStates InitUser, BaseSkill askil, float InitDmg)
    {
        User = new List<BaseStates>();
        User.Add(InitUser);
        skill = askil;

        //Memory();//この記憶回数の処理の後に補正するので、作った瞬間はゼロから始めた方がいい
        DamageMemory(InitDmg);
    }

    /// <summary>
    /// そのスキルのユーザー
    /// </summary>
    public List<BaseStates> User;

    /// <summary>
    /// 保存スキル
    /// </summary>
    public BaseSkill skill;

    float _memoryCount;
    /// <summary>
    /// 慣れの記憶回数
    /// </summary>
    public float MemoryCount => _memoryCount;
    public void Memory(float value)
    {
        _memoryCount += value;
    }
    public void Forget(float value)
    {
        _memoryCount -= value;
    }


    float _topDmg;
    /// <summary>
    /// このスキルが自らに施した最大限のダメージ
    /// </summary>
    public float TopDmg => _topDmg;
    public void DamageMemory(float dmg)
    {
        if (dmg > _topDmg) _topDmg = dmg;//越してたら記録
    }
}

/// <summary>
/// 狙い流れ(AimStyle)に対する短期記憶・対応進行度をまとめた構造体
/// </summary>
public struct AimStyleMemory
{
    /// <summary>いま対応しようとしている相手の AimStyle==そのまま自分のNowDeffenceStyleに代入されます。</summary>
    public AimStyle? TargetAimStyle;

    /// <summary>現在の変革カウント(対応がどこまで進んでいるか)</summary>
    public int TransformCount;

    /// <summary>変革カウントの最大値。ここに達したら対応完了</summary>
    public int TransformCountMax;

    


}
/// <summary>
/// 対象者ボーナスのデータ
/// </summary>
public class TargetBonusDatas
{
    /// <summary>
    /// 持続ターン
    /// </summary>
    List<int> DurationTurns { get; set; }
    /// <summary>
    /// スキルのパワーボーナス倍率
    /// </summary>
    List<float> PowerBonusPercentages { get; set; }
    /// <summary>
    /// 対象者
    /// </summary>
    List<BaseStates> Targets { get; set; }
    /// <summary>
    /// 対象者がボーナスに含まれているか
    /// </summary>
    public bool DoIHaveTargetBonus(BaseStates target)
    {
        return Targets.Contains(target);
    }
    /// <summary>
    /// 対象者のインデックスを取得
    /// </summary>
    public int GetTargetIndex(BaseStates target)
    {
        return Targets.FindIndex(x => x == target);
    }
    /// <summary>
    /// 対象者ボーナスが発動しているか
    /// </summary>
    //public List<bool> IsTriggered { get; set; }     ーーーーーーーーーーーー一回自動で発動するようにするから消す、明確に対象者ボーナスの適用を手動にするなら解除
    /// <summary>
    /// 発動してるかどうかを取得
    /// </summary>
    /*public bool GetAtIsTriggered(int index)
    {
        return IsTriggered[index];
    }*/
    /// <summary>
    /// 対象者ボーナスの持続ターンを取得
    /// </summary>
    public int GetAtDurationTurns(int index)
    {
        return DurationTurns[index];
    }
    /// <summary>
    /// 全てのボーナスをデクリメントと自動削除の処理
    /// </summary>
    public void AllDecrementDurationTurn()
    {
        for (int i = 0; i < DurationTurns.Count; i++)
        {
            DecrementDurationTurn(i);
        }
    }
    /// <summary>
    /// 持続ターンをデクリメントし、0以下になったら削除する。全ての対象者ボーナスを削除する。
    /// </summary>
    void DecrementDurationTurn(int index)
    {
        DurationTurns[index]--;
        if (DurationTurns[index] <= 0)
        {
            DurationTurns.RemoveAt(index);
            PowerBonusPercentages.RemoveAt(index);
            Targets.RemoveAt(index);
        }
    }
    /// <summary>
    /// 対象者ボーナスのパワーボーナス倍率を取得
    /// </summary>
    public float GetAtPowerBonusPercentage(int index)
    {
        return PowerBonusPercentages[index];
    }
    /// <summary>
    /// 対象者ボーナスの対象者を取得
    /// </summary>
    public BaseStates GetAtTargets(int index)
    {
        return Targets[index];
    }

    public TargetBonusDatas()
    {
        DurationTurns =  new();
        PowerBonusPercentages = new();
        Targets = new();
        //IsTriggered = new();
    }

    public void Add(int duration, float powerBonusPercentage, BaseStates target)
    {
        //targetの重複確認
        if (Targets.Contains(target))
        {
            int index = Targets.IndexOf(target);//同じインデックスの物をすべて消す
            DurationTurns.RemoveAt(index);
            PowerBonusPercentages.RemoveAt(index);
            Targets.RemoveAt(index);
            //IsTriggered.RemoveAt(index);
            return;
        }

        //追加
        DurationTurns.Add(duration);
        PowerBonusPercentages.Add(powerBonusPercentage);
        Targets.Add(target);
        //IsTriggered.Add(false);
    }
    /// <summary>
    /// 全削除
    /// </summary>
    public void AllClear()
    {
        DurationTurns.Clear();
        PowerBonusPercentages.Clear();
        Targets.Clear();
        //IsTriggered.Clear();
    }
    /// <summary>
    /// 該当のインデックスのボーナスを削除
    /// </summary>
    public void BonusClear(int index)
    {
        DurationTurns.RemoveAt(index);
        PowerBonusPercentages.RemoveAt(index);
        Targets.RemoveAt(index);
        //IsTriggered.RemoveAt(index);
    }
}

