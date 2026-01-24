using LitMotion;
using UnityEngine;

/// <summary>
/// KZoom（アイコンタップ詳細表示）の設定を保持する構造体。
/// WatchUIUpdateのSerializeFieldから値を受け取る。
/// </summary>
[System.Serializable]
public class KZoomConfig
{
    /// <summary>ズーム対象ルート</summary>
    public RectTransform ZoomRoot;

    /// <summary>フィット目標枠</summary>
    public RectTransform TargetRect;

    /// <summary>フィットブレンド（0=高さ優先, 1=横幅優先）</summary>
    [Range(0f, 1f)]
    public float FitBlend = 0.5f;

    /// <summary>ズーム時間</summary>
    public float ZoomDuration = 0.6f;

    /// <summary>ズームイージング</summary>
    public Ease ZoomEase = Ease.OutQuart;

    /// <summary>名前テキスト</summary>
    public TMPTextBackgroundImage NameText;

    /// <summary>パッシブテキスト</summary>
    public TMPTextBackgroundImage PassivesText;

    /// <summary>テキストスライド時間</summary>
    public float TextSlideDuration = 0.35f;

    /// <summary>テキストスライドイージング</summary>
    public Ease TextSlideEase = Ease.OutCubic;

    /// <summary>テキストスライドオフセットX</summary>
    public float TextSlideOffsetX = 220f;

    /// <summary>パッシブフェード時間</summary>
    public float PassivesFadeDuration = 0.35f;

    /// <summary>パッシブ末尾ドット数</summary>
    public int PassivesEllipsisDotCount = 4;

    /// <summary>パッシブ高さセーフティ余白</summary>
    public float PassivesFitSafety = 1.0f;

    /// <summary>収まる場合でもドットを付けるか</summary>
    public bool PassivesAlwaysAppendEllipsis = true;

    /// <summary>RectMask2Dを使用するか</summary>
    public bool PassivesUseRectMask = true;

    /// <summary>デバッグモード</summary>
    public bool PassivesDebugMode = false;

    /// <summary>デバッグ用ダミー数</summary>
    public int PassivesDebugCount = 100;

    /// <summary>デバッグ用接頭辞</summary>
    public string PassivesDebugPrefix = "pas";

    /// <summary>既存ズーム中はアイコンクリック無効</summary>
    public bool DisableIconClickWhileBattleZoom = true;
}
