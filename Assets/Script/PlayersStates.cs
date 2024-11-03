using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///セーブでセーブされるような事柄とかメインループで操作するためのステータス太刀　シングルトン
/// </summary>
public class PlayersStates:MonoBehaviour 
{
    //staticなインスタンス
    public static PlayersStates Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)//シングルトン
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public StairStates geino;
    public BassJackStates noramlia;
    public SateliteProcessStates sites;

    public PlayersStates() //コンストラクター 
    {
        NowProgress = 0;
        NowStageID = 0;
        NowAreaID = 0;

        //セーブデータあるならこの後に処理
    }

    /// <summary>
    ///     現在進行度
    /// </summary>
    public int NowProgress { get; private set; }

    /// <summary>
    ///     現在のステージ
    /// </summary>
    public int NowStageID { get; }

    /// <summary>
    ///     現在のステージ内のエリア
    /// </summary>
    public int NowAreaID { get; private set; }

    public BattleGroup GetParty()
    {
        var playerGroup = new List<BaseStates> { geino, sites, noramlia }; //キャラ
        var nowOurImpression = GetPartyImpression(); //パーティー属性を彼らのHPから決定

        return new BattleGroup(playerGroup, nowOurImpression,WhichGroup.alliy); //パーティー属性を返す
    }

    /// <summary>
    /// 味方のパーティー属性を取得する 現在のHPによって決まる。
    /// </summary>
    /// <returns></returns>
    private PartyProperty GetPartyImpression()
    {
        //それぞれの最大HP５％以内の差は許容し、それを前提に三人が同じHPだった場合
        float toleranceStair = geino.MAXHP * 0.05f;
        float toleranceSateliteProcess = sites.MAXHP * 0.05f;
        float toleranceBassJack = noramlia.MAXHP * 0.05f;
        if (Mathf.Abs(geino.HP - sites.HP) <= toleranceStair && 
            Mathf.Abs(sites.HP - noramlia.HP) <= toleranceSateliteProcess &&
            Mathf.Abs(noramlia.HP - geino.HP) <= toleranceBassJack)
        {
            return PartyProperty.MelaneGroup;//三人のHPはほぼ同じ
        }
        
        
        //HPの差による場合分け
        if (geino.HP >= sites.HP && sites.HP >= noramlia.HP)
        {
            // geino >= sites >= noramlia ステアが一番HPが多い　サテライトプロセスが二番目　バスジャックが三番目
            return PartyProperty.MelaneGroup;
        }
        else if (geino.HP >= noramlia.HP && noramlia.HP >= sites.HP)
        {
            // geino >= noramlia >= sites　ステアが一番HPが多い　ステアが二番目　バスジャックが三番目
            return PartyProperty.Odradeks;
        }
        else if (sites.HP >= geino.HP && geino.HP >= noramlia.HP)
        {
            // sites >= geino >= noramlia　サテライトプロセスが一番HPが多い　ステアが二番目　バスジャックが三番目
            return PartyProperty.MelaneGroup;
        }
        else if (sites.HP >= noramlia.HP && noramlia.HP >= geino.HP)
        {
            // sites >= noramlia >= geino　サテライトプロセスが一番HPが多い　バスジャックが二番目　ステアが三番目
            return PartyProperty.HolyGroup;
        }
        else if (noramlia.HP >= geino.HP && geino.HP >= sites.HP)
        {
            // noramlia >= geino >= sites　バスジャックが一番HPが多い　ステアが二番目　サテライトプロセスが三番目
            return PartyProperty.TrashGroup;
        }
        else if (noramlia.HP >= sites.HP && sites.HP >= geino.HP)
        {
            // noramlia >= sites >= geino　バスジャックが一番HPが多い　サテライトプロセスが二番目　ステアが三番目
            return PartyProperty.Flowerees;
        }

        return PartyProperty.MelaneGroup;//基本的にif文で全てのパターンを網羅しているので、ここには来ない
    }

    /// <summary>
    ///     進行度を増やす
    /// </summary>
    /// <param name="addPoint"></param>
    public void AddProgress(int addPoint)
    {
        NowProgress += addPoint;
    }

    /// <summary>
    ///     現在進行度をゼロにする
    /// </summary>
    public void ProgressReset()
    {
        NowProgress = 0;
    }

    /// <summary>
    ///     エリアをセットする。
    /// </summary>
    public void SetArea(int id)
    {
        NowAreaID = id;
        Debug.Log(id + "をPlayerStatesに記録");
    }
}

[Serializable]
public class BassJackStates : BaseStates //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    /// <summary>
    ///     コンストラクタ
    /// </summary>
}
[Serializable]

public class SateliteProcessStates : BaseStates //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
}
[Serializable]
public class StairStates : BaseStates //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    //先輩のコンストラクタ
}