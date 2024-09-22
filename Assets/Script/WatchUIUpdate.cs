using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class WatchUIUpdate : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI StagesString;//ステージとエリア名のテキスト
    [SerializeField] TenmetuNowImage MapImg;//直接で現在位置表示する簡易マップ
    void Start()
    {
        
    }

    void Update()
    {
        
    }
    /// <summary>
    /// 全体的なEYEAREAのUI更新
    /// </summary>
    public void UIUpdate(StageData sd,StageCut sc,PlayersStates pla)
    {
        StagesString.text = sd.StageName + "・\n" + sc.AreaName;
        NowImageCalc(sc, pla);
    }
    /// <summary>
    /// 簡易マップ現在地のUI更新とその処理
    /// </summary>
    void NowImageCalc(StageCut sc,PlayersStates player)
    {
        //進行度自体の割合を計算
        float Ratio = (float)player.NowProgress / (sc.AreaDates.Count - 1);
        //進行度÷エリア数(countだから-1) 片方キャストしないと整数同士として小数点以下切り捨てられる。
        //Debug.Log("現在進行度のエリア数に対する割合"+Ratio);

        //lerpがベクトルを設定してくれる、調整された位置を渡す
        MapImg.LocationSet(Vector2.Lerp(sc.MapLineS,sc.MapLineE, Ratio));

    }
}
