﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.AddressableAssets;
using RandomExtensions;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class EnemyCollectManager : MonoBehaviour
{
    /// <summary>
    ///     敵が一人の際の属性をそのままパーティー属性に変換する辞書データ
    /// </summary>
    public Dictionary<SpiritualProperty, PartyProperty> EnemyLonelyPartyImpression;

    /// <summary>
    ///     キャラ属性同士の敵集まりAIの相性の辞書データ　方向がある為順序の情報も含む。
    /// </summary>
    private Dictionary<(SpiritualProperty I, SpiritualProperty You), int> ImpressionMatchupTable;


    /// <summary>
    ///     この属性の敵キャラクターが最初の一人に選ばれたとき、そのまま一人で終わる確率。
    /// </summary>
    private Dictionary<SpiritualProperty, int> LonelyMatchImpression;

    /// <summary>
    ///     キャラクター属性の"固定"組み合わせによるパーティー属性の辞書データ
    /// </summary>
    private Dictionary<string, PartyProperty> PartyPropertyMatchupTable;

    /// <summary>
    ///     種別同士の敵集まりAIの相性の辞書データ
    ///     同じペアは逆順でも自動的に対応されるので、
    ///     わざわざ追加しなくてOK
    /// </summary>
    private Dictionary<(CharacterType, CharacterType), int> TypeMatchupTable;

    // staticなインスタンス
    public static EnemyCollectManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); //シーン遷移しても破棄されないようにする。
            ImpressionMatchUpCsvLoad(); //impressionMatchupTableのデータをcsvから読み込む。

            EnemyLonelyPartyImpression = new Dictionary<SpiritualProperty, PartyProperty> //一人の場合の属性をパーティー属性に変換する辞書データ
            {
                { SpiritualProperty.doremis, PartyProperty.Flowerees },
                { SpiritualProperty.pillar, PartyProperty.Odradeks },
                { SpiritualProperty.kindergarden, PartyProperty.TrashGroup },
                { SpiritualProperty.liminalwhitetile, PartyProperty.MelaneGroup },
                { SpiritualProperty.sacrifaith, PartyProperty.HolyGroup },
                { SpiritualProperty.cquiest, PartyProperty.MelaneGroup },
                { SpiritualProperty.pysco, GetRandomPartyProperty() }, //サイコのみランダムな値が出るようにする。 
                { SpiritualProperty.godtier, PartyProperty.Flowerees },
                { SpiritualProperty.baledrival, PartyProperty.TrashGroup },
                { SpiritualProperty.devil, PartyProperty.HolyGroup }
            };

            LonelyMatchImpression = new Dictionary<SpiritualProperty, int> //一人で終わる確率の辞書データ
            {
                { SpiritualProperty.doremis, 30 },
                { SpiritualProperty.pillar, 30 },
                { SpiritualProperty.kindergarden, 20 },
                { SpiritualProperty.liminalwhitetile, 30 },
                { SpiritualProperty.sacrifaith, 70 },
                { SpiritualProperty.cquiest, 30 },
                { SpiritualProperty.pysco, 30 },
                { SpiritualProperty.godtier, 30 },
                { SpiritualProperty.baledrival, 30 },
                { SpiritualProperty.devil, 30 }
            };

            TypeMatchupTable = new Dictionary<(CharacterType, CharacterType), int> //種別同士の相性値データ
            {
                { (CharacterType.TLOA, CharacterType.Life), 80 },
                { (CharacterType.TLOA, CharacterType.Machine), 30 },
                { (CharacterType.Machine, CharacterType.Life), 60 }
            };

            PartyPropertyMatchupTable = new Dictionary<string, PartyProperty> //固定の組み合わせによるパーティー属性の辞書データ
            {
                //listの{}内に順不同の組み合わせでキーを指定できる。
                {
                    NormalizeSpiritualKey(new List<SpiritualProperty>
                        { SpiritualProperty.baledrival, SpiritualProperty.pysco, SpiritualProperty.sacrifaith }),
                    PartyProperty.HolyGroup
                }
                //ベールドライヴァル、サイコパス、自己犠牲の組み合わせは聖戦
            };
        }
        else
        {
            Destroy(gameObject); //既に存在している場合は破棄
        }
    }

    /// <summary>
    ///     ランダムなパーティー属性を返す
    /// </summary>
    private PartyProperty GetRandomPartyProperty()
    {
        var values = Enum.GetValues(typeof(PartyProperty)); //getvaluesで指定したenumの値を全て配列に格納する。
        return (PartyProperty)values.GetValue(RandomEx.Shared.NextInt(0, values.Length)); //getvalueでランダムな値を取得
        //getValueはarrayのメソッドで、引数にインデックスを取り、そのインデックスの値をobject型で返す。
    }

    private async void ImpressionMatchUpCsvLoad() //
    {
        ImpressionMatchupTable = new Dictionary<(SpiritualProperty, SpiritualProperty), int>(); //辞書データの初期化
        
        var csvFile = "Assets/csvData/characterMatchData.csv"; //csvファイルのパス

        var textHandle = await Addressables.LoadAssetAsync<TextAsset>(csvFile).WithCancellation(destroyCancellationToken);
        //リーミナル、キンダー、自己、シークイ、デビル、ドレミス、支柱、ゴッドティア、ベールドライヴァル、サイコパスの順番に行も列も並んでいます。
        var SpiritualCsvArray = new[]
        {
            //スプレッドシートでの行と列での属性に対応するような順番で配列に格納。
            SpiritualProperty.liminalwhitetile,
            SpiritualProperty.kindergarden,
            SpiritualProperty.sacrifaith,
            SpiritualProperty.cquiest,
            SpiritualProperty.devil,
            SpiritualProperty.doremis,
            SpiritualProperty.pillar,
            SpiritualProperty.godtier,
            SpiritualProperty.baledrival,
            SpiritualProperty.pysco
        };
        var rows = textHandle.text //そのままテキストを渡す
            .Split("\n")//改行ごとに分割
            .Select(line => line.Trim())//行の先頭と末尾の空白や改行を削除する
            .Select(line => line.Split(',').Select(int.Parse).ToArray()) //それをさらにカンマで分割してint型に変換して配列に格納する。
            .ToArray(); //配列になった行をさらに配列に格納する。
        /*
         * new List<List<int>> {  実際はarrayだけどこういうイメージ
            new List<int> { 50, 20, 44, 53, 42, 37, 90, 100, 90, 50 },
            new List<int> { 60, 77, 160, 50, 80, 23, 32, 50, 51, 56 }}
         */

        for (var i = 0; i < rows.Length; i++) //行ごとに回していく oneは行たちを格納した配列
        for (var j = 0; j < rows[i].Length; j++) //数字ごとに回す　one[j]は行の中の数字を格納した配列
            ImpressionMatchupTable.Add((SpiritualCsvArray[j], SpiritualCsvArray[i]),
                //二つ目のループ[j]で列見出しの属性が直近で横に進んで感じる側に入り、一つ目のループの[i]は行見出しの感じられる側の属性。
                rows[i][j]); //値部分に相性値として、intのarray内のさらにarray内の値を入れる。
        Debug.Log("読み込まれたキャラクター属性同士の相性\n" +
                  string.Join(", ",
                      ImpressionMatchupTable.Select(kvp => $"[{kvp.Key}: {kvp.Value}]" + "\n"))); //デバックで全内容羅列。
    }

    /// <summary>
    ///     一人で終わる確率を返す、失敗したら-1を返す。
    /// </summary>
    /// <param name="I"></param>
    /// <returns></returns>
    public bool LonelyMatchUp(SpiritualProperty I)
    {
        var matchPer = LonelyMatchImpression[I];
        if (RandomEx.Shared.NextInt(100) < matchPer) return true;

        return false; //一人で終わらなかった場合失敗を返す。
    }


    /// <summary>
    ///     種別同士の敵集まりの相性
    /// </summary>
    /// <param name="I">既にパーティにいて、ジャッジする方</param>
    /// <param name="You">ジャッジされる側、相性が悪ければ</param>
    public bool TypeMatchUp(CharacterType I, CharacterType You)
    {
        // 順序に関係なく常に同じ結果を得るため、IとYouを並び替える
        var sortedPair = I < You ? (I, You) : (You, I);
        //1<2 ならtrueなので(1,2)が返り、　2<1ならfalseなので逆にして(1,2)が返る。常に同じキーを生成するロジック。
        //このロジックのお陰で順序を気にしなくていい。　characteTypeのenumは暗黙的に数値で扱われること前提。

        var matchPer = TypeMatchupTable[sortedPair]; //ジャッジ確率

        if (RandomEx.Shared.NextInt(100) < matchPer) return true;


        return false; //相性が合わなかった場合失敗を返す。
    }

    /// <summary>
    ///     デフォルトの値も含めて、キャラクター属性同士の相性値を取得する関数。
    /// </summary>
    public int GetImpressionMatchPercent(SpiritualProperty I, SpiritualProperty You)
    {
        // 順序が重要なので、順序をそのままにしてマッチを確認
        var key = (I, You);

        // ジャッジ確率のデフォルト
        var matchPer = 50;

        if (ImpressionMatchupTable.ContainsKey(key)) //デフォルトの確率ではないものだけが辞書に存在するので、ここで判定。
            matchPer = ImpressionMatchupTable[key]; //ジャッジ確率

        return matchPer;
    }

    /// <summary>
    ///     属性同士の敵集まりの相性判定
    ///     もし同情効果がtrueなら、相性判定の確率が二倍になる
    /// </summary>
    /// <param name="I">既にパーティにいて、ジャッジする方</param>
    /// <param name="You">ジャッジされる側、相性が悪ければ</param>
    /// <param name="sympathy">同情してるかどうか</param>
    public bool ImpressionMatchUp(SpiritualProperty I, SpiritualProperty You, bool sympathy = false)
    {
        var matchPer = GetImpressionMatchPercent(I, You);
        if (sympathy)
        {
            matchPer *= 2;//同情効果で二倍に
        }

        if (RandomEx.Shared.NextInt(100) < matchPer) return true;

        return false; //相性が合わなかった場合失敗を返す。
    }

    /// <summary>
    ///     キャラクター属性のリストを正規化された文字列のキーに変換。これを辞書データに渡してパーティー属性を取得する。
    /// </summary>
    /// <param name="keys"></param>
    /// <returns></returns>
    private string NormalizeSpiritualKey(List<SpiritualProperty> keys)
    {
        keys.Sort(); //ソートしておくことで、順序が違っても同じ文字列が生成される。 enumの数値が暗黙的に扱われること前提。
        return string.Join(",", keys); //カンマ区切りで文字列に変換
    }

    /// <summary>
    ///キャラクター属性の組み合わせによって、パーティー属性を決定する
    /// </summary>
    /// <param name="calcList">キャラクター属性を抽出するための敵のリスト</param>
    /// <returns></returns>
    public PartyProperty calculatePartyProperty(List<NormalEnemy> calcList)
    {
        //calclistの属性を抽出して属性リスト化
        var keys = calcList.Select(enemy => enemy.MyImpression).ToList();

        //keysをNormalizeSpiritualKeyに渡して正規化し、辞書データからパーティー属性を取得する。
        //固定の組み合わせによるパーティー属性の辞書データがある場合、それを返す。
        if (PartyPropertyMatchupTable.ContainsKey(NormalizeSpiritualKey(keys))) //辞書データにキーが存在するかどうか
            return PartyPropertyMatchupTable[NormalizeSpiritualKey(keys)]; //辞書データから返す。

        //固定の組み合わせによるパーティー属性の辞書データがなかった場合、相性値の判断で行う。
        //三人分のお互いの相性値を取得する。(3×2=6通り)
        var matchPercentages = new List<int>();
        foreach (var one in keys) //一人ずつ取り出して
        foreach (var other in keys) //他の一人ずつ取り出して
            matchPercentages.Add(GetImpressionMatchPercent(one, other));
        //リスト内の全ての値が70以上なら聖戦
        if (matchPercentages.All(percent => percent >= 70)) return PartyProperty.HolyGroup;
        //リスト内の全ての値が30以下ならオドラデクス
        if (matchPercentages.All(percent => percent <= 30)) return PartyProperty.Odradeks;

        //相性値の平均を取る
        var average = matchPercentages.Average();

        if (average >= 70) return PartyProperty.MelaneGroup; //メレーンズ　全平70以上

        //相性値の標準偏差を取る
        var variance = matchPercentages.Select(x => Math.Pow(x - average, 2)).Average(); //分散 powでべき乗を取り、averageで平均を取る
        var standardDeviation = Math.Sqrt(variance); //標準偏差 sqrtは平方根を取るメソッド

        if (standardDeviation >= 20) return PartyProperty.Odradeks; //オドラデクス　標準偏差20以上

        if (average > 57) return PartyProperty.Flowerees; //花樹 値のバラつきが激しい場合

        if (RandomEx.Shared.NextInt(100) < 67) //3分の2で馬鹿共かメレーンズか2分の1のランダム
        {
            if (RandomEx.Shared.NextInt(100) < 50) return PartyProperty.TrashGroup; //馬鹿共
            return PartyProperty.MelaneGroup; //メレーンズ
        }

        //どの条件にも当てはまらなかった場合、完全ランダムで決まる
        return GetRandomPartyProperty();
    }
}