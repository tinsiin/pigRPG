using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using System.Collections.Generic;
using TMPro;
using UnityEngine;


public class WatchUIUpdate : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI StagesString; //ステージとエリア名のテキスト
    [SerializeField] private TenmetuNowImage MapImg; //直接で現在位置表示する簡易マップ
    [SerializeField] private RectTransform bgRect; //背景のRectTransform
    [SerializeField] private DarkWaveManager _waveManager;

    //ズーム用変数
    [SerializeField] private AnimationCurve _firstZoomAnimationCurve;
    [SerializeField] private float _firstZoomSpeedTime;
    [SerializeField] private Vector2 _gotoPos;
    [SerializeField] private Vector2 _gotoScaleXY;
    [SerializeField] private Vector2 _NormalPos;
    [SerializeField] private Vector2 _NormalScaleXY;

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

    RectTransform _rect;
    RectTransform Rect
    {
        get {
            if (_rect == null)
            {
                _rect = GetComponent<RectTransform>();
            }
            return _rect;
        }
    }

    /// <summary>
    ///     歩行時のEYEAREAのUI更新
    /// </summary>
    public void WalkUIUpdate(StageData sd, StageCut sc, PlayersStates pla)
    {
        StagesString.text = sd.StageName + "・\n" + sc.AreaName;
        NowImageCalc(sc, pla);
        SideObjectManage(sc, SideObject_Type.Normal);//サイドオブジェクト
    }

    /// <summary>
    /// エンカウントしたら最初にズームする処理
    /// </summary>
    public void FirstImpressionZoom()
    {
        var nowScaleXY = new Vector2(Rect.localScale.x, Rect.localScale.y);
        var nowPos = new Vector2(Rect.anchoredPosition.x, Rect.anchoredPosition.y);

        //_waveManager.InWave();

        //スケール移動
        LMotion.Create(nowScaleXY, _gotoScaleXY, _firstZoomSpeedTime)
            .WithEase(_firstZoomAnimationCurve)
            .BindToLocalScaleXY(Rect)
            .AddTo(this);

        //ポジション移動
        LMotion.Create(nowPos, _gotoPos, _firstZoomSpeedTime)
           .WithEase(_firstZoomAnimationCurve)
           .BindToAnchoredPosition(Rect)
           .AddTo(this);
    }

    /// <summary>
    /// 歩行の度に更新されるSideObjectの管理
    /// </summary>
    private void SideObjectManage(StageCut nowStageCut, SideObject_Type type)
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
            var LineObject = TwoObjects[i].GetComponent<UILineRenderer>();
            LineObject.sideObject_Type = type;//引数のタイプを渡す。
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