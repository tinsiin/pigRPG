using System;
using LitMotion;
using UnityEngine;

/// <summary>
/// ノベルパート用ズームの設定。
/// 中央オブジェクトをターゲット領域にフィットさせる。
/// </summary>
[Serializable]
public class NovelZoomConfig
{
    [Header("ターゲット領域")]
    [Tooltip("中央オブジェクトをフィットさせる領域のRectTransform")]
    public RectTransform TargetRect;

    [Header("ズームコンテナ")]
    [Tooltip("ズーム対象のコンテナ（ZoomBackContainer）")]
    public RectTransform ZoomContainer;

    [Header("アニメーション")]
    [Tooltip("ズームアニメーション時間")]
    public float ZoomDuration = 0.4f;

    [Tooltip("ズームイージング")]
    public Ease ZoomEase = Ease.OutQuad;

    [Header("フィット調整")]
    [Tooltip("縦横フィットのブレンド (0=縦基準, 1=横基準, 0.5=平均)")]
    [Range(0f, 1f)]
    public float FitBlend = 0.5f;

    [Tooltip("追加マージン（フィット後に少し余裕を持たせる）")]
    public float Margin = 0.9f;
}
