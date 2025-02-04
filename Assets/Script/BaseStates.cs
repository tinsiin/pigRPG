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
    devil = 1 << 9    // ビットパターン: 0010 0000 0000  (512)
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

    private BattleManager manager;

    public void Managed(BattleManager ma)
    {
        manager = ma;
    }
    public void LostManaged()
    {
        manager = null;
    }

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

    public IReadOnlyList<BaseVitalLayer> VitalLayers => _vitalLaerList;
    /// <summary>
    /// インスペクタ上で設定されたIDを通じて特定の追加HPを持ってるか調べる
    /// </summary>
    public bool HasVitalLayer(int id)
    {
        return _vitalLaerList.Any(vit => vit.id == id);
    }

    public ThePower NowPower;

    [Header("4大ステの基礎基礎値")]
    public float  b_b_atk = 4f;
    public float b_b_def = 4f;
    public float b_b_eye = 4f;
    public float b_b_agi = 4f;

    public SerializableDictionary<TenDayAbility,float> TenDayValues = new SerializableDictionary<TenDayAbility,float>();

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
    /// ケレケレ　外連味により増減するステータス
    /// </summary>
    public float KereKere;

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
    private int tmpTurnsToAdd;
    /// <summary>
    /// 一時保存用のリカバリターン判別用の前ターン変数
    /// </summary>
    private int tmp_EncountTurn;
    /// <summary>
    /// recovelyCountという行動クールタイムに一時的に値を加える
    /// </summary>
    public void RecovelyCountTmpAdd(int addTurn)
    {
        if(!IsActiveCancelInSkillACT)//行動がキャンセルされていないなら
        {
            tmpTurnsToAdd += addTurn;
        }
    }
    /// <summary>
    /// このキャラが戦場にて再行動を取れるかどうかと時間を唱える関数
    /// </summary>
    public bool RecovelyBattleField(int nowTurn)
    {
        var difference = Math.Abs(nowTurn - tmp_EncountTurn);//前ターンと今回のターンの差異から経過ターン
        tmp_EncountTurn = nowTurn;//一時保存
        if ((recoveryTurn += difference) >= maxRecoveryTurn + tmpTurnsToAdd)//累計ターン経過が最大値を超えたら
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
        RemoveRecovelyTmpAddTurn();
    }
    /// <summary>
    /// キャラに設定された追加硬直値をリセットする
    /// </summary>
    public void RemoveRecovelyTmpAddTurn()
    {
        tmpTurnsToAdd = 0;
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

    /// <summary>
    /// vitalLayerでHPに到達する前に攻撃値を請け負う処理
    /// </summary>
    public float BarrierLayers(float dmg, BaseStates atker)
    {

        // 1) VitalLayer の順番どおりにダメージを適用していく
        //    ここでは「Priority が低い方(手前)が先に処理される想定」を前提に
        //    _vitalLaerList がすでに正しい順序でソートされていることを期待。

        for (int i = 0; i < _vitalLaerList.Count;)
        {
            var layer = _vitalLaerList[i];
            var skillPhy = atker.NowUseSkill.SkillPhysical;
            // 2) このレイヤーに貫通させて、返り値を「残りダメージ」とする
            dmg = layer.PenetrateLayer(dmg, skillPhy);

            if (layer.LayerHP <= 0f)
            {
                // このレイヤーは破壊された
                _vitalLaerList.RemoveAt(i);
                // リストを削除したので、 i はインクリメントしない（要注意）

                //破壊慣れまたは破壊負け
                if (skillPhy == PhysicalProperty.heavy)//暴断なら破壊慣れ
                {
                    dmg += dmg * 0.015f * KereKere;
                }
                if (skillPhy == PhysicalProperty.volten)//vol天なら破壊負け
                {
                    dmg -= dmg * 0.022f * (atker.b_ATK - KereKere);
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
    public SpiritualProperty DefaultImpression { get; private set; }



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

        //割り込みカウンターパッシブなら+100
        if (HasPassive(1))
        {
            eye += 100;
        }

        return eye;
    }

    /// <summary>
    /// 回避率計算
    /// </summary>
    public virtual float AGI()
    {
        float agi = b_AGI;//基礎回避率

        agi *= UseAGIPercentageModifier;//回避率補正。リスト内がゼロならちゃんと1.0fが返る。

        if (manager.IsVanguard(this))//自分が前のめりなら
        {
            agi /= 2;//回避率半減
        }

        return agi;
    }

    public virtual float ATK()
    {
        float atk = b_ATK;//基礎攻撃力

        atk *= UseATKPercentageModifier;//攻撃力補正

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
        if(HasPassive(1))
        {
            atk *= 2;
        }


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

        var minusAmount = def * minusPer;//防御低減率


        return def - minusAmount;
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
    ///     オーバライド可能なダメージ関数
    /// </summary>
    /// <param name="atkPoint"></param>
    public virtual string Damage(BaseStates Atker, float SkillPower)
    {
        var skill = Atker.NowUseSkill;
        var def = DEF(skill.DEFATK);

        def = ClampDefenseByAimStyle(skill,def);//防ぎ方(AimStyle)の不一致がある場合、クランプする

        var dmg = (Atker.ATK() - def) * SkillPower;//(攻撃-対象者の防御) ×スキルパワー？

        if(NowPower > ThePower.lowlow)//たるくなければ基礎山形補正がある。
        dmg = GetBaseCalcDamageWithPlusMinus22Percent(dmg);//基礎山型補正

        //慣れ補正
        dmg *= AdaptToSkill(Atker, skill, dmg);

        //vitalLayerを通る処理
        dmg = BarrierLayers(dmg, Atker);

        HP -= dmg;
        Debug.Log("攻撃が実行された");
        return "-+~*⋮¦";
    }

    /// <summary>
    /// ヒールは防御できない、つまりヒールが逆効果のキャラクターならヒールは有効打ってこと
    /// </summary>
    /// <param name="HealPoint"></param>
    public virtual string Heal(float HealPoint)
    {
        HP += HealPoint;
        Debug.Log("ヒールが実行された");
        return "癒された";
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

        if (RandomEx.Shared.NextFloat(Attacker.EYE() + AGI()) < Attacker.EYE())//術者の命中+僕の回避率　をMAXに　ランダム値が術者の命中に収まったら　命中。
        {
            //スキルそのものの命中率 スキル命中率は基本独立させて、スキル自体の熟練度系ステータスで補正する？
            return skill.SkillHitCalc(AccuracySupremacy(Attacker.EYE(), AGI()));
        }

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
                    ApplyPassive(PassiveManager.Instance.GetAtID(1));

                    //次のターンで攻撃、つまり先約リストの予約を判定する。　
                    if(HasCharacterType(CharacterType.Life))
                    {//生命なら、必ず反撃可能

                        //攻撃を食らった際、中断不可能なカウンターまたはfreezeConecutiveの場合、武器スキルでしか返せない。
                        var isfreeze = false;
                        if(NowUseSkill.NowConsecutiveATKFromTheSecondTimeOnward() && NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive) ||
                        NowUseSkill.IsTriggering()) 
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
        var txt = "";//メッセージテキスト用

        //スキルの持ってる性質を全て処理として実行

        if (skill.HasType(SkillType.Attack))
        {
            if (IsReactHIT(attacker))
            {
                var thisAtkTurn = true;
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
                    txt += Damage(attacker, skillPower);
                }
            }
            else
            {//外したら
                skill.HitConsecutiveCount = 0;//連続ヒット回数がゼロ　
            }
        }

        if (skill.HasType(SkillType.Heal))
        {
            if (skill.SkillHitCalc(0))//スキル命中率の計算だけ行う
            {
                txt += Heal(skillPower);
            }
        }

        if (skill.HasType(SkillType.addPassive))
        {
            foreach (var id in skill.subEffects)
                ApplyPassive(PassiveManager.Instance.GetAtID(id));
        }

        if (skill.HasType(SkillType.AddVitalLayer))
        {
            foreach (var id in skill.subVitalLayers)
                ApplyVitalLayer(VitalLayerManager.Instance.GetAtID(id));
        }

        Debug.Log("ReactionSkill");
        return txt;
    }


    /// <summary>
    /// クラスを通じて相手を攻撃する
    /// </summary>
    /// <param name="UnderAttacker"></param>
    public virtual string AttackChara(UnderActersEntryList Unders)
    {



        SkillUseConsecutiveCountUp(NowUseSkill);//連続カウントアップ
        string txt = "";

        // スキルの精神属性を自分の精神属性に変更
        NowUseSkill.SkillSpiritual = MyImpression;

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
    ///     死を判定するオーバライド可能な関数
    /// </summary>
    /// <returns></returns>
    public virtual bool Death()
    {
        if (HP <= 0) 
        {
            DeathCallBack();
            return true;
        }
        return false;
    }
    /// <summary>
    /// 死亡時のコールバック　SkillsTmpResetでスキルの方からリセットできるような簡単じゃない奴をここで処理する。
    /// </summary>
    public virtual void DeathCallBack()
    {
        DeleteConsecutiveATK();
        //あるかわからないが続行中のスキルを消し、
        //以外のそれ以外のスキルの連続攻撃回数消去(基本的に一個しか増えないはずだが)は以下のforeachループで行う
        foreach (var skill in SkillList)
        {
            skill.OnDeath();
        }

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
    ///追加HPを適用 
    /// </summary>
    public virtual void ApplyVitalLayer(BaseVitalLayer newLayer)
    {
        //リスト内に同一の物があるか判定する。
        var sameHP = _vitalLaerList.FirstOrDefault(lay => lay.id == newLayer.id);
        if (sameHP != null)
        {
            sameHP.ReplenishHP();//同一の為リストにある側を再補充する。
        }
        else//初物の場合
        {
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
    ///     パッシブを適用
    /// </summary>
    public virtual void ApplyPassive(BasePassive status)
    {
        // 条件(OkType,OkImpression) は既にチェック済みならスキップ
        if (!HasCharacterType(status.OkType)) return;
        if (!HasCharacterImpression(status.OkImpression)) return;

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
    }

    /// <summary>
    /// パッシブを除去
    /// </summary>
    public virtual void RemovePassive(BasePassive status)
    {
        // パッシブがあるか確認
        if (_passiveList.Remove(status))
        {
            // パッシブ側のOnRemoveを呼ぶ
            status.OnRemove(this);
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
    /// <summary>歩行時のコールバック引数なしの</summary>
    public void OnWalkNoArgument()
    {
        UpdateWalkAllPassiveSurvival();
    }

    /// <summary>
    /// 戦闘中に次のターンに進む際のコールバック
    /// </summary>
    public void OnNextTurnNoArgument()
    {
        UpdateTurnAllPassiveSurvival();
    }

    /// <summary>
    ///bm生成時に初期化される関数
    /// </summary>
    public void OnBattleStartNoArgument()
    {
        TempDamageTurn = 0;
        DecisionKinderAdaptToSkillGrouping();//慣れ補正の優先順位のグルーピング形式を決定するような関数とか
        DecisionSacriFaithAdaptToSkillGrouping();
        
    }
    public void OnBattleEndNoArgument()
    {
        TempDamageTurn = 0;
        DeleteConsecutiveATK();
        LostManaged();
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
    /// 戦闘規格ごとのデフォルトa,bの狙い流れ
    /// </summary>
    public static readonly Dictionary<BattleProtocol, (AimStyle aStyle,float a,AimStyle bStyle)> DefaultDefensePatternPerProtocol =
        new Dictionary<BattleProtocol, (AimStyle aStyle,float a,AimStyle bStyle)>
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
