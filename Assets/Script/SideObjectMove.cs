using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LitMotion;
using LitMotion.Extensions;
using RandomExtensions;
using UnityEngine.Serialization;

public class SideObjectMove : MonoBehaviour//エディタ拡張からアクセスされる変数は基本的にはpublicにしなければならない。
{
    //フェードアウトの処理に使う
    public Vector2 bornPos; //生成時の位置
    public Vector2 bornScaleXY; //生成時のサイズ
    public float bornRotationZ; // 生成時の回転（Z軸）

    public Vector2 pos; //標準の位置
    public Vector2 scaleXY; //標準のサイズ
    public float rotationZ; // 標準の回転（Z軸）


    //フェードアウト以降の処理に使う
    public Vector2 _midPos; //中間位置
    public Vector2 _midScaleXY; //中間サイズ

    public Vector2 _endPos; //終了位置
    public Vector2 _endScaleXY; //終了サイズ



    [SerializeField] private float fadeInTime = 1f; // フェードインの時間
    [SerializeField] private float fadeInRange = 0.2f; //フェードインのバラつき範囲
    [SerializeField] private float fadeInDelayRange = 0.1f; //フェードインの遅延範囲

    [SerializeField] private float speed; //移動速度
    [SerializeField] private AnimationCurve _curve; //アニメーションカーブ
    public Vector2 baseSize;//記録するためのsizeDelta

    private RectTransform _thisRect;
    Color _initLineColor;
    Color _initTwoColor;
    UILineRenderer lr;
    int fadeInEndPoint = 0;//フェードインのアニメーションが終わった数を記録する変数
    Transform parentRect;//親オブジェクトのTransform
    MotionHandle mh;
    public float boostSpeed;//加速度
    public Vector2 ScaleRange;//サイズの範囲


    private void Start()
    {
        lr = GetComponent<UILineRenderer>();
        _thisRect = GetComponent<RectTransform>();
        parentRect = transform.parent;

        var _lineThickness = lr.thickness; //線の予定された太さを取得
        _initLineColor = lr.lineColor; //線の予定された色を取得
        _initTwoColor = lr.two; //線の予定された色を取得
        lr.lineColor = new Color(_initLineColor.r, _initLineColor.g, _initLineColor.b, 0); //透明にする このコード消すとフェードインの前にサイドオブジェクトがちらつく
        lr.two = new Color(_initTwoColor.r, _initTwoColor.g, _initTwoColor.b, 0); //透明にする このコード消すとフェードインの前にサイドオブジェクトがちらつく

        //スケールに大きさのノイズを含める(位置ノイズはUiLineRendererのAwake)
        // 一度だけ乱数を生成
        Vector2 randomOffset = new Vector2(
            RandomEx.Shared.NextFloat(ScaleRange.x),//x,yでスケールを分けるんじゃなくて、一意な乱数でx,y両方を同じにして、
            RandomEx.Shared.NextFloat(ScaleRange.y)//比率を同じにして拡大した方が見栄えは良いと思うけど、とりあえず今の方がノイズ感あるからそのまま、てゆーか大して変わらん
        );
        bornScaleXY += randomOffset;
        scaleXY += randomOffset;
        _midScaleXY += randomOffset;
        _endScaleXY += randomOffset;

        // フェードインのアニメーション
        var fadeRnd = RandomEx.Shared.NextFloat(0, fadeInRange);
        var fadeDelayRnd = RandomEx.Shared.NextFloat(0, fadeInDelayRange);

        mh= LMotion.Create(0, _initLineColor.a, fadeInTime + fadeRnd)//フェードインの時間にバラつきを加える/開始時間にもバラつきを
            .WithEase(_curve)
            .WithDelay(fadeDelayRnd)//遅延時間にバラつきを加える
            .WithOnComplete(() => fadeInEndPoint++)//終わったことを記録
            .Bind(alpha =>
            {
                var color = lr.lineColor;
                color.a = alpha;
                lr.lineColor = color;
                lr.SetVerticesDirty(); // uilinerendererの頂点データに変更を通知
            })
            .AddTo(this);
        mh.PlaybackSpeed = boostSpeed;//再生速度を速める

        mh= LMotion.Create(0, _initTwoColor.a, fadeInTime + fadeRnd)//フェードインの時間にバラつきを加える/開始時間にもバラつきを
            .WithEase(_curve)
            .WithDelay(fadeDelayRnd)//遅延時間にバラつきを加える
            .WithOnComplete(() => fadeInEndPoint++)//終わったことを記録
            .Bind(alpha =>
            {
                var color = lr.two;
                color.a = alpha;
                lr.two = color;
                lr.SetVerticesDirty(); // uilinerendererの頂点データに変更を通知
            })
            .AddTo(this);
        mh.PlaybackSpeed = boostSpeed;//再生速度を速める


        // 位置のアニメーション
        mh = LMotion.Create(bornPos, pos, speed)
            .WithEase(_curve)
            .WithDelay(fadeDelayRnd)//遅延時間にバラつきを加える
            .WithOnComplete(() => fadeInEndPoint++)//終わったことを記録
            .BindToAnchoredPosition(_thisRect)
            .AddTo(this);
        mh.PlaybackSpeed = boostSpeed;//再生速度を速める


        // サイズのアニメーションを追加
        mh = LMotion.Create(bornScaleXY, scaleXY, speed)
            .WithEase(_curve)
            .WithDelay(fadeDelayRnd)//遅延時間にバラつきを加える
            .WithOnComplete(() => fadeInEndPoint++)//終わったことを記録
            .BindToLocalScaleXY(_thisRect)
            .AddTo(this);
        mh.PlaybackSpeed = boostSpeed;//再生速度を速める


        // 太さのアニメーションを追加
        mh = LMotion.Create(0, _lineThickness, speed)
            .WithEase(_curve)
            .WithDelay(fadeDelayRnd)//遅延時間にバラつきを加える
            .WithOnComplete(() => fadeInEndPoint++)//終わったことを記録
            .Bind(thick => {
                lr.thickness = thick;
                lr.SetVerticesDirty(); // uilinerendererの頂点データに変更を通知
            })
            .AddTo(this);
        mh.PlaybackSpeed = boostSpeed;//再生速度を速める


        // 回転のアニメーションを追加
        var cs = _thisRect.localEulerAngles; //現在の角度を取得
        mh = LMotion.Create(bornRotationZ, rotationZ, speed)
            .WithEase(_curve)
            .WithDelay(fadeDelayRnd)//遅延時間にバラつきを加える
            .WithOnComplete(() => fadeInEndPoint++)//終わったことを記録
            .Bind(nowZ => _thisRect.localEulerAngles = new Vector3(cs.x, cs.y, nowZ)) // Z軸のみ変更
            .AddTo(this);
        mh.PlaybackSpeed = boostSpeed;//再生速度を速める

        //Debug.Log(fadeRnd.ToString() + "=fadeRnd");
        //Debug.Log(fadeDelayRnd.ToString() + "=fadeDelayRnd");
    }

    /// <summary>
    /// サイドオブジェクトの消えるエフェクト 終わったら自動でオブジェクトを削除
    /// </summary>
    public async UniTask FadeOut()
    {
        await UniTask.WaitUntil(() => fadeInEndPoint >= 5);//フェードインのアニメーションが終わるまで待つ

        var end = false;//withOnCompleteでモーションの終わりを判断する。

        //まず中点まで移動。
        mh = LMotion.Create(pos, _midPos, speed/2)
            .WithEase(_curve)
            .WithOnComplete(() => end = true)
            .BindToAnchoredPosition(_thisRect)
            .AddTo(this);
        mh.PlaybackSpeed = boostSpeed;//再生速度を速める

        await UniTask.WaitUntil(() => end);//中点まで移動が終わるまで待つ

        end = false;

        //中点から終点へ移動を記録
        mh=  LMotion.Create(_midPos, _endPos, speed/2)
            .WithEase(_curve)
            .WithOnComplete(() => end = true)
            .BindToAnchoredPosition(_thisRect)
            .AddTo(this);
        mh.PlaybackSpeed = boostSpeed;//再生速度を速める

        await UniTask.WaitUntil(() => end);//終点まで移動が終わるまで待つs


        Destroy(gameObject);//オブジェクトを削除
    }
}
