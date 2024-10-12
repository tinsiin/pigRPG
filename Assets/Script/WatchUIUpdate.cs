using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WatchUIUpdate : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI StagesString; //ステージとエリア名のテキスト
    [SerializeField] private TenmetuNowImage MapImg; //直接で現在位置表示する簡易マップ
    [SerializeField] private RectTransform bgRect; //背景のRectTransform

    GameObject[] TwoObjects;//サイドオブジェクトの配列
    SideObjectMove[] SideObjectMoves = new SideObjectMove[2];//サイドオブジェクトのスクリプトの配列
    List<SideObjectMove>[] LiveSideObjects;//生きているサイドオブジェクトのリスト 間引き用


    private void Start()
    {
        TwoObjects = new GameObject[2];//サイドオブジェクト二つ分の生成
        LiveSideObjects = new List<SideObjectMove>[2];//生きているサイドオブジェクトのリスト
        LiveSideObjects[0] = new List<SideObjectMove>();//左右二つ分
        LiveSideObjects[1] = new List<SideObjectMove>();
    }

    private void Update()
    {
        /*for(int i = 0; i < 2; i++)
        {
            //Debug.Log("サイドオブジェクトのリスト数" + LiveSideObjects[i].Count);

            //オブジェクト自体が再生を完了して自動でDestroyしたのを消す処理
            for (int j = LiveSideObjects[i].Count - 1; j >= 0; j--) // 後ろ向きのループ
            {
                if ((LiveSideObjects[i][j] == null)) // もしnullなら
                {
                    Debug.Log("SideObjectが自動で消されたのでLiveListからも削除");
                    LiveSideObjects[i].RemoveAt(j); // リストから削除
                }
            }
            

        }*/
    }

    /// <summary>
    ///     全体的なEYEAREAのUI更新
    /// </summary>
    public void UIUpdate(StageData sd, StageCut sc, PlayersStates pla)
    {
        StagesString.text = sd.StageName + "・\n" + sc.AreaName;
        NowImageCalc(sc, pla);
        SideObjectManage(sc);//サイドオブジェクト
    }

    /// <summary>
    /// 歩行の度に更新されるSideObjectの管理
    /// </summary>
    public void SideObjectManage(StageCut nowStageCut)
    {

        var GetObjects = nowStageCut.GetRandomSideObject();//サイドオブジェクトLEFTとRIGHTを取得

        //サイドオブジェクト二つ分の生成
        for(int i =0; i < 2; i++)
        {
            if (TwoObjects[i] != null)
            {
                SideObjectMoves[i].FadeOut().Forget();//フェードアウトは待たずに処理をする。
            }

            TwoObjects[i] = Instantiate(GetObjects[i], bgRect);//サイドオブジェクトを生成、配列に代入
            SideObjectMoves[i] = TwoObjects[i].GetComponent<SideObjectMove>();//スクリプトを取得
            SideObjectMoves[i].boostSpeed=3.0f;//スピードを初期化
            LiveSideObjects[i].Add(SideObjectMoves[i]);//生きているリスト(左右どちらか)に追加
            //Debug.Log("サイドオブジェクト生成[" + i +"]");

            //数が多くなりだしたら
            /*if (LiveSideObjects[i].Count > 2) {
                SideObjectMoves[i].boostSpeed = 3.0f;//スピードをブースト

            }*/

        }
    }


    /// <summary>
    ///     簡易マップ現在地のUI更新とその処理
    /// </summary>
    private void NowImageCalc(StageCut sc, PlayersStates player)
    {
        //進行度自体の割合を計算
        var Ratio = (float)player.NowProgress / (sc.AreaDates.Count - 1);
        //進行度÷エリア数(countだから-1) 片方キャストしないと整数同士として小数点以下切り捨てられる。
        //Debug.Log("現在進行度のエリア数に対する割合"+Ratio);

        //lerpがベクトルを設定してくれる、調整された位置を渡す
        MapImg.LocationSet(Vector2.Lerp(sc.MapLineS, sc.MapLineE, Ratio));
    }
}