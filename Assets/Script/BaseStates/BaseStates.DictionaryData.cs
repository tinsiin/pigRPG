
using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using NRandom.Linq;
using Cysharp.Threading.Tasks;
using System.Linq;
using UnityEngine.AddressableAssets;


//辞書データ　staticな物
public abstract partial class BaseStates    
{

    /// <summary>
    /// 精神属性と十日能力の互換表
    /// デフォルト精神属性
    /// </summary>
    public static readonly Dictionary<SpiritualProperty, List<TenDayAbility>> SpritualTenDayAbilitysMap = 
    new()
    {
        {SpiritualProperty.Doremis, new List<TenDayAbility>(){TenDayAbility.Enokunagi, TenDayAbility.PersonaDivergence, TenDayAbility.KereKere, TenDayAbility.Rain, TenDayAbility.BlazingFire}},
        {SpiritualProperty.Pillar, new List<TenDayAbility>(){TenDayAbility.JoeTeeth, TenDayAbility.Sort, TenDayAbility.SilentTraining, TenDayAbility.Leisure, TenDayAbility.Vond}},
        {SpiritualProperty.Kindergarten, new List<TenDayAbility>(){TenDayAbility.Dokumamusi, TenDayAbility.Baka, TenDayAbility.TentVoid, TenDayAbility.SpringNap, TenDayAbility.WaterThunderNerve}},
        {SpiritualProperty.LiminalWhiteTile, new List<TenDayAbility>(){TenDayAbility.FlameBreathingWife, TenDayAbility.NightDarkness, TenDayAbility.StarTersi, TenDayAbility.FaceToHand, TenDayAbility.Pilmagreatifull}},
        {SpiritualProperty.Sacrifaith, new List<TenDayAbility>(){TenDayAbility.UnextinguishedPath, TenDayAbility.Miza, TenDayAbility.JoeTeeth, TenDayAbility.SpringNap}},
        {SpiritualProperty.Cquiest, new List<TenDayAbility>(){TenDayAbility.ColdHeartedCalm, TenDayAbility.NightDarkness, TenDayAbility.NightInkKnight, TenDayAbility.Glory, TenDayAbility.SpringNap}},
        {SpiritualProperty.Psycho, new List<TenDayAbility>(){TenDayAbility.Raincoat, TenDayAbility.TentVoid, TenDayAbility.Blades, TenDayAbility.Smiler, TenDayAbility.StarTersi}},
        {SpiritualProperty.GodTier, new List<TenDayAbility>(){TenDayAbility.HeavenAndEndWar, TenDayAbility.Vail, TenDayAbility.BlazingFire, TenDayAbility.FlameBreathingWife, TenDayAbility.SpringWater}},
        {SpiritualProperty.BaleDrival, new List<TenDayAbility>(){TenDayAbility.Smiler, TenDayAbility.Miza, TenDayAbility.HeatHaze, TenDayAbility.Vail}},
        {SpiritualProperty.Devil, new List<TenDayAbility>(){TenDayAbility.CryoniteQuality, TenDayAbility.HumanKiller, TenDayAbility.HeatHaze, TenDayAbility.FaceToHand}},
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
        return allStyles.RandomElement();
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
    /// 精神属性でのスキルの補正値　攻撃者の精神属性→キャラクター属性 csvDataからロード
    /// </summary>
    protected static Dictionary<(SpiritualProperty atk, SpiritualProperty def), FixedOrRandomValue> SpiritualModifier;
    /// <summary>
    /// スキル以外のキャラクター同士の攻撃的なニュアンスの精神補正
    /// </summary>
    public static FixedOrRandomValue GetOffensiveSpiritualModifier(BaseStates attacker,BaseStates Defer)
    {

        if(attacker.MyImpression == SpiritualProperty.None) return new FixedOrRandomValue(100);//noneなら補正なし(100%なので無変動)
        
        var resultModifier = SpiritualModifier[(attacker.MyImpression, Defer.MyImpression)];//攻撃的な人の放つ精神属性と被害者の精神属性による補正
        
        if(attacker.MyImpression == attacker.MyImpression)
        {
            resultModifier.RandomMaxPlus(s_fallbackRandom.NextInt(2,8));//スキルの計算でないのでほんのちょっとだけ。
        }

        return resultModifier;
    }
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
        SpiritualModifier = new Dictionary<(SpiritualProperty, SpiritualProperty), FixedOrRandomValue>();//初期化
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
            SpiritualProperty.LiminalWhiteTile,
            SpiritualProperty.Kindergarten,
            SpiritualProperty.Sacrifaith,
            SpiritualProperty.Cquiest,
            SpiritualProperty.Devil,
            SpiritualProperty.Devil,//乱数のmax
            SpiritualProperty.Doremis,
            SpiritualProperty.Pillar,
            SpiritualProperty.GodTier,
            SpiritualProperty.BaleDrival,
            SpiritualProperty.Psycho
        };
        var SpiritualCsvArrayColumn = new[]
        {
            //精神攻撃の相性の　列の属性並び順
            SpiritualProperty.LiminalWhiteTile,
            SpiritualProperty.Kindergarten,
            SpiritualProperty.Sacrifaith,
            SpiritualProperty.Cquiest,
            SpiritualProperty.Devil,
            SpiritualProperty.Doremis,
            SpiritualProperty.Pillar,
            SpiritualProperty.GodTier,
            SpiritualProperty.BaleDrival,
            SpiritualProperty.BaleDrival,//乱数のmax
            SpiritualProperty.Psycho
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
                    if (SpiritualModifier.ContainsKey(key))//キーが既にあれば
                    {
                        SpiritualModifier[key].SetMax(value);//乱数最大値を設定
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
                    if (!SpiritualModifier.ContainsKey(key))//キーが存在していなければ
                    {
                        SpiritualModifier.Add(key, new FixedOrRandomValue(value));//キーを追加
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
/// 精神補正用の割合を出すためのクラスなので　割合はintで0~100の百分率で指定します
/// </summary>
public class FixedOrRandomValue
{

    private int rndMax;//乱数の最大値 乱数かどうかはrndMaxに-1を入れればいい
    private int rndMinOrFixed;//単一の値または乱数としての最小値

    /// <summary>
    /// 乱数の上振れを増やす
    /// </summary>
    public void RandomMaxPlus(int plusAmount)
    {
        if(rndMax == -1)//乱数最大範囲がないなら
        {
            rndMax = rndMinOrFixed + plusAmount;//上振れを付けた状態で増やす
            return;
        }

        rndMax += plusAmount;//既にあるならそのまま最大値を増やす
    }

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

    /// <summary>
    /// -1を指定すると乱数ではないってことになる。
    /// </summary>
    public void SetMax(int value)
    {
        rndMax = value;//-1を指定するとないってこと
    }
    public float GetValue(float percentage = 1.0f)
    {
        float value;
        if (rndMax == -1) value = rndMinOrFixed / 100.0f;//乱数じゃないなら単一の値が返る
        else value = BaseStates.FallbackRandom.NextInt(rndMinOrFixed, rndMax + 1)  / 100.0f;//ランダムなら

        return value * percentage;//割合を掛ける

    }
    public FixedOrRandomValue DeepCopy()
    {
        var newData = new FixedOrRandomValue(rndMinOrFixed);
        newData.SetMax(rndMax);
        return newData;
    }
}
