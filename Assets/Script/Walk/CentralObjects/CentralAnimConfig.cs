using System;
using LitMotion;
using UnityEngine;

[Serializable]
public struct CentralAnimConfig
{
    [Header("フェードイン")]
    [Tooltip("フェードイン時間")]
    public float FadeInDuration;

    [Tooltip("開始時のスケール（1.0未満で小さく、1.0超で大きく）")]
    public float FadeInStartScale;

    [Tooltip("フェードインのイージング")]
    public Ease FadeInEase;

    [Header("フェードアウト（サイドオブジェクトと同じ2段階移動）")]
    [Tooltip("フェードアウト時間（1段階あたり）")]
    public float FadeOutDuration;

    [Tooltip("中間点へのX方向移動距離（ピクセル）")]
    public float FadeOutMidX;

    [Tooltip("中間点へのY方向移動距離（ピクセル）")]
    public float FadeOutMidY;

    [Tooltip("終点へのX方向移動距離（中間点からの追加、ピクセル）")]
    public float FadeOutEndX;

    [Tooltip("終点へのY方向移動距離（中間点からの追加、ピクセル）")]
    public float FadeOutEndY;

    [Tooltip("フェードアウトのイージング")]
    public Ease FadeOutEase;

    public static CentralAnimConfig Default => new CentralAnimConfig
    {
        FadeInDuration = 0.3f,
        FadeInStartScale = 0.8f,
        FadeInEase = Ease.OutBack,
        FadeOutDuration = 0.25f,
        FadeOutMidX = 320f,     // 横に移動
        FadeOutMidY = -160f,    // 下に移動（負の値）
        FadeOutEndX = 160f,     // さらに横に
        FadeOutEndY = -80f,     // さらに下に
        FadeOutEase = Ease.InQuad
    };

    public bool IsValid => FadeInDuration > 0f || FadeOutDuration > 0f;
}
