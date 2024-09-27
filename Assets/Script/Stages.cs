using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class Stages:ScriptableObject
{//ステージにまつわる物を処理したり呼び出したり(ステージデータベース??
    public List<StageData> StageDates;//ステージのデータベースのリスト     
    [SerializeField,TextArea(1,30)] string memo;

}

/// <summary>
/// ステータスボーナスのクラス ステージごとに登録する。
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

[System.Serializable]
public class StageData//ステージデータのクラス
{
    [SerializeField]string _stageName;
    [SerializeField] List<StageCut> _cutArea;


    /// <summary>
    /// ステージの名前
    /// </summary>
    public string StageName => _stageName;//ラムダ式で読み取り専用

    /// <summary>
    /// ステージを小分けにしたリスト
    /// </summary>
    public IReadOnlyList<StageCut> CutArea => _cutArea;

    /// <summary>
    /// ステージごとに設定される主人公陣営たちのボーナス。
    /// </summary>
    public StatesBonus Satelite_StageBonus;
    /// <summary>
    /// ステージごとに設定される主人公陣営たちのボーナス。
    /// </summary>
    public StatesBonus Bass_StageBonus;
    /// <summary>
    /// ステージごとに設定される主人公陣営たちのボーナス。
    /// </summary>
    public StatesBonus Stair_StageBonus;
    
}
/// <summary>
/// 分岐に対応するため、ステージを小分けにしたもの。
/// </summary>
/// 
[System.Serializable]
public class StageCut
{
    [SerializeField]string _areaName;
    [SerializeField]int _id;
    [SerializeField]List<AreaDate> _areaDates;//並ぶエリア
    [SerializeField] Vector2 _mapLineS;
    [SerializeField] Vector2 _mapLineE;
    [SerializeField] string _mapsrc;
    [SerializeField] List<NormalEnemy> _enemyList;//敵のリスト


    /// <summary>
    /// 小分けしたエリアの名前
    /// </summary>
    public string AreaName => _areaName;


    /// <summary>
    /// マップ画像に定義する直線の始点　nowimgのanchoredPositionを直接入力
    /// </summary>
    public Vector2 MapLineS => _mapLineS;
    /// <summary>
    /// マップ画像に定義する直線の終点　nowimgのanchoredPositionを直接入力
    /// </summary>
    public Vector2 MapLineE => _mapLineE;

    /// <summary>
    /// 小分けにしたエリアのID
    /// </summary>
    public int Id => _id;
    /// <summary>
    /// エリアごとの簡易マップの画像。
    /// </summary>
    public string MapSrc => _mapsrc;

    /// <summary>
    /// 並べるエリアのデータ
    /// </summary>
    public IReadOnlyList<AreaDate> AreaDates => _areaDates;
    /// <summary>
    /// 敵のリスト
    /// </summary>
    public IReadOnlyList<NormalEnemy> EnemyList => _enemyList;

}
/// <summary>
/// ステージに並ぶエリアデータ
/// </summary>
[System.Serializable]
public class AreaDate
{
    
    [SerializeField] bool _rest;
    [SerializeField] string _backsrc;
    [SerializeField] string _nextID;
    [SerializeField] string _nextIDString;
    [SerializeField] string _nextStageID;

    /// <summary>
    /// 次のステージのid、入力されてないならスルー
    /// string.splitでstring[]に格納して分岐できる。
    /// 「,」で区切って入力。
    /// </summary>
    public string NextStageID => _nextStageID;
    /// <summary>
    /// 休憩地点かどうか
    /// </summary>
    public bool Rest => _rest;
    /// <summary>
    /// 背景画像のファイル名
    /// </summary>
    public string BackSrc => _backsrc;
    /// <summary>
    /// 次のエリアID、入力されてないならスルー
    /// string.splitでstring[]に格納して分岐できる。
    /// 「,」で区切って入力。
    /// </summary>
    public string NextID => _nextID;/// <summary>
    /// 次のエリア選択肢のボタン文章、入力されてないならスルー
    /// string.splitでstring[]に格納して分岐できる。
    /// 「,」で区切って入力。
    /// </summary>
    public string NextIDString => _nextIDString;

}

