using System;
using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using UnityEngine;

[CreateAssetMenu]
public class Stages : ScriptableObject
{
    //ステージにまつわる物を処理したり呼び出したり(ステージデータベース??
    public List<StageData> StageDates; //ステージのデータベースのリスト     
    [SerializeField] [TextArea(1, 30)] private string memo;

}

/// <summary>
///     ステータスボーナスのクラス ステージごとに登録する。
/// </summary>
[Serializable]
public class StatesBonus
{
    public int ATKBpunus;
    public int DEFBonus;
    public int AGIBonus;
    public int HITBonus;
    public int HPBonus;
    public int PBonus;
    public int RecovelyTurnMinusBonus;
}

[Serializable]
public class StageData //ステージデータのクラス
{
    [SerializeField] private string _stageName;
    [SerializeField] private List<StageCut> _cutArea;
    /// <summary>
    ///     ステージごとに設定される主人公陣営たちのボーナス。
    /// </summary>
    public StatesBonus Satelite_StageBonus;

    /// <summary>
    ///     ステージごとに設定される主人公陣営たちのボーナス。
    /// </summary>
    public StatesBonus Bass_StageBonus;

    /// <summary>
    ///     ステージごとに設定される主人公陣営たちのボーナス。
    /// </summary>
    public StatesBonus Stair_StageBonus;


    /// <summary>
    ///     ステージの名前
    /// </summary>
    public string StageName => _stageName; //ラムダ式で読み取り専用

    /// <summary>
    ///     ステージを小分けにしたリスト
    /// </summary>
    public IReadOnlyList<StageCut> CutArea => _cutArea;

}

/// <summary>
///     分岐に対応するため、ステージを小分けにしたもの。
/// </summary>
[Serializable]
public class StageCut
{
    [SerializeField] private string _areaName;
    [SerializeField] private int _id;
    [SerializeField] private List<AreaDate> _areaDates; //並ぶエリア
    [SerializeField] private Vector2 _mapLineS;
    [SerializeField] private Vector2 _mapLineE;
    [SerializeField] private string _mapsrc;
    [SerializeField] private List<NormalEnemy> _enemyList; //敵のリスト
    [SerializeField] private GameObject[] _sideObject_Lefts;//左側に出現するオブジェクト
    [SerializeField] private GameObject[] _sideObject_Rights;//右側に出現するオブジェクト


    /// <summary>
    ///     エンカウント率
    /// </summary>
    [SerializeField] private int EncounterRate;

    /// <summary>
    ///     小分けしたエリアの名前
    /// </summary>
    public string AreaName => _areaName;


    /// <summary>
    ///     マップ画像に定義する直線の始点　nowimgのanchoredPositionを直接入力
    /// </summary>
    public Vector2 MapLineS => _mapLineS;

    /// <summary>
    ///     マップ画像に定義する直線の終点　nowimgのanchoredPositionを直接入力
    /// </summary>
    public Vector2 MapLineE => _mapLineE;

    /// <summary>
    ///     小分けにしたエリアのID
    /// </summary>
    public int Id => _id;

    /// <summary>
    ///     エリアごとの簡易マップの画像。
    /// </summary>
    public string MapSrc => _mapsrc;

    /// <summary>
    ///     並べるエリアのデータ
    /// </summary>
    public IReadOnlyList<AreaDate> AreaDates => _areaDates;

    /// <summary>
    ///     敵のリスト
    /// </summary>
    public IReadOnlyList<NormalEnemy> EnemyList => _enemyList;

    private bool EncountCheck()
    {
        if (RandomEx.Shared.NextInt(100) < EncounterRate) //エンカウント率の確率でエンカウント
            return true;

        return false;
    }

    /// <summary>
    ///     EnemyCollectManagerを使って敵を選ぶAI　キャラクター属性や種別などを考慮して選ぶ。
    ///     エンカウント失敗したら、nullを返す
    /// </summary>
    public BattleGroup EnemyCollectAI()
    {
        if (!EncountCheck()) return null; //エンカウント失敗したら、nullを返す

        var ResultList = new List<NormalEnemy>(); //返す用のリスト
        PartyProperty ourImpression; //初期値は馬鹿共
        var targetList = _enemyList; //引数のリストをコピー

        //最初の一人はランダムで選ぶ
        var rndIndex = RandomEx.Shared.NextInt(0, targetList.Count - 1); //ランダムインデックス指定
        var referenceOne = targetList[rndIndex]; //抽出
        targetList.RemoveAt(rndIndex); //削除
        ResultList.Add(referenceOne); //追加

        //数判定(一人判定)　
        if (EnemyCollectManager.Instance.LonelyMatchUp(referenceOne.MyImpression))
        {
            //パーティー属性を決める　一人なのでその一人の属性をそのままパーティー属性にする
            ourImpression =
                EnemyCollectManager.Instance.EnemyLonelyPartyImpression
                    [referenceOne.MyImpression]; //()ではなく[]でアクセスすることに注意

            return new BattleGroup(ResultList.Cast<BaseStates>().ToList(), ourImpression); //while文に入らずに返す  
        }

        while (true)
        {
            //まず吟味する加入対象をランダムに選ぶ
            var targetIndex = RandomEx.Shared.NextInt(0, targetList.Count - 1); //ランダムでインデックス指定
            var okCount = 0; //適合数 これがResultList.Countと同じになったら加入させる

            for (var i = 0; i < ResultList.Count; i++) //既に選ばれた敵全員との相性を見る
                //for文で判断しないと現在の配列のインデックスを相性値用の配列のインデックス指定に使えない
                //種別同士の判定 if文内で変数に代入できる
                if (EnemyCollectManager.Instance.TypeMatchUp(ResultList[i].MyType, targetList[targetIndex].MyType))
                    //属性同士の判定　上クリアしたら
                    if (EnemyCollectManager.Instance.ImpressionMatchUp(ResultList[i].MyImpression,
                            targetList[targetIndex].MyImpression))
                        okCount++; //適合数を増やす
            //foreachで全員との相性を見たら、加入させる。
            if (okCount == ResultList.Count) //全員との相性が合致したら
            {
                ResultList.Add(targetList[targetIndex]); //結果のリストに追加
                targetList.RemoveAt(targetIndex); //候補リストから削除
            }

            //数判定
            if (ResultList.Count == 1) //一人だったら(まだ一人も見つけれてない場合)
                if (RandomEx.Shared.NextInt(100) < 88) //88%の確率で一人で終わる計算に入る。
                    //数判定(一人判定)　
                    if (EnemyCollectManager.Instance.LonelyMatchUp(referenceOne.MyImpression))
                    {
                        //パーティー属性を決める　一人なのでその一人の属性をそのままパーティー属性にする
                        ourImpression =
                            EnemyCollectManager.Instance.EnemyLonelyPartyImpression
                                [referenceOne.MyImpression]; //()ではなく[]でアクセスすることに注意

                        break;
                    }

            if (ResultList.Count == 2) //二人だったら三人目の加入を決める
                if (RandomEx.Shared.NextInt(100) < 65) //この確率で終わる。
                {
                    //パーティー属性を決める
                    ourImpression = EnemyCollectManager.Instance.calculatePartyProperty(ResultList);
                    break;
                }

            if (ResultList.Count >= 3)
            {
                //パーティー属性を決める
                ourImpression = EnemyCollectManager.Instance.calculatePartyProperty(ResultList);
                break; //三人になったら強制終了
            }
        }


        return new BattleGroup(ResultList.Cast<BaseStates>().ToList(), ourImpression); //バトルグループを制作 
    }

    /// <summary>
    /// leftとRightのオブジェクトを返す。
    /// </summary>
    /// <returns>GameObjectの入った配列</returns>
    public GameObject[] GetRandomSideObject()
    {
        if (_sideObject_Lefts.Length < 0)
        {
            Debug.LogError("_sideObject_LeftsのPrefabのリストが空です");
            return null;
        }
        if (_sideObject_Rights.Length < 0)
        {
            Debug.LogError("_sideObject_RightsのPrefabのリストが空です");
            return null;
        }

        // ランダムなオブジェクトを選択
        var leftItem = RandomEx.Shared.GetItem<GameObject>(_sideObject_Lefts);
        var rightItem = RandomEx.Shared.GetItem<GameObject>(_sideObject_Rights);

        return new GameObject[] { leftItem, rightItem };
    }

}

/// <summary>
///     ステージに並ぶエリアデータ
/// </summary>
[Serializable]
public class AreaDate
{
    [SerializeField] private bool _rest;
    [SerializeField] private string _backsrc;
    [SerializeField] private string _nextID;
    [SerializeField] private string _nextIDString;
    [SerializeField] private string _nextStageID;

    /// <summary>
    ///     次のステージのid、入力されてないならスルー
    ///     string.splitでstring[]に格納して分岐できる。
    ///     「,」で区切って入力。
    /// </summary>
    public string NextStageID => _nextStageID;

    /// <summary>
    ///     休憩地点かどうか
    /// </summary>
    public bool Rest => _rest;

    /// <summary>
    ///     背景画像のファイル名
    /// </summary>
    public string BackSrc => _backsrc;

    /// <summary>
    ///     次のエリアID、入力されてないならスルー
    ///     string.splitでstring[]に格納して分岐できる。
    ///     「,」で区切って入力。
    /// </summary>
    public string NextID => _nextID;

    /// <summary>
    ///     次のエリア選択肢のボタン文章、入力されてないならスルー
    ///     string.splitでstring[]に格納して分岐できる。
    ///     「,」で区切って入力。
    /// </summary>
    public string NextIDString => _nextIDString;
}