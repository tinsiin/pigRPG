using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayersStates //セーブでセーブされるような事柄とかメインループで操作するためのステータス太刀
{

    /*public BassJackStates geino;
    public SateliteProcessStates sites;
    public StairStates noramlia;*/
    int _nowProgress;
    int _nowStageID;
    int _nowAreaID;

    /// <summary>
    /// 現在進行度
    /// </summary>
    public int NowProgress => _nowProgress;
    /// <summary>
    /// 現在のステージ
    /// </summary>
    public int NowStageID => _nowStageID;
    /// <summary>
    /// 現在のステージ内のエリア
    /// </summary>
    public int NowAreaID => _nowAreaID;

    public PlayersStates()//コンストラクター 
    { 
        _nowProgress = 0;
        _nowStageID = 0;
        _nowAreaID = 0;
        //geino = new BassJackStates(3,4,5,6,7,8);
        //sites = new SateliteProcessStates(3,4,5,6,7,8);
        //noramlia = new StairStates(3, 4, 5, 6, 7, 8);

        //セーブデータあるならこの後に処理
    }

    /// <summary>
    /// 進行度を増やす  
    /// </summary>
    /// <param name="addPoint"></param>
    public void AddProgress(int addPoint)
    {
        _nowProgress += addPoint;
    }
    /// <summary>
    /// 現在進行度をゼロにする
    /// </summary>
    public void ProgressReset()
    {
        _nowProgress = 0;
    }

    /// <summary>
    /// エリアをセットする。
    /// </summary>
    public void SetArea(int id)
    {
        _nowAreaID = id;
        Debug.Log(id + "をPlayerStatesに記録");
    }


}
/*
public class BassJackStates : BaseStates//共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public BassJackStates(int c_hp, int c_maxhp, int c_p, int c_maxP, int c_DEF, int c_ATK, int c_HIT, int c_AGI)
         : base(c_hp, c_maxhp, c_p, c_maxP, c_DEF, c_ATK, c_HIT, c_AGI)
    {//主人公のコンストラクタ
       
    }
}
public class SateliteProcessStates : BaseStates//共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public SateliteProcessStates(int c_hp, int c_maxhp, int c_p, int c_maxP, int c_DEF, int c_ATK, int c_HIT, int c_AGI)
         : base(c_hp, c_maxhp, c_p, c_maxP, c_DEF, c_ATK, c_HIT, c_AGI)
    {//同僚のコンストラクタ

    }
}
public class StairStates : BaseStates//共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{//先輩のコンストラクタ
    public StairStates(int c_hp, int c_maxhp, int c_p, int c_maxP, int c_DEF, int c_ATK, int c_HIT, int c_AGI)
        :base(c_hp, c_maxhp, c_p, c_maxP, c_DEF, c_ATK, c_HIT,c_AGI)
    {//同僚のコンストラクタ
        
    }
}
*/