using UnityEngine;

/// <summary>
/// 歩行画面のビューポート（視覚領域）を抽象化するインターフェース。
/// IZoomController（Back/Frontズーム）へのアクセスと、レイヤー参照を提供する。
/// </summary>
public interface IViewportController
{
    /// <summary>ズーム制御（既存のIZoomController - Back/Frontを個別にズーム）</summary>
    IZoomController Zoom { get; }

    /// <summary>ズームする背景レイヤー（ZoomBackContainer）</summary>
    RectTransform ZoomBackContainer { get; }

    /// <summary>ズームする前景レイヤー（ZoomFrontContainer）</summary>
    RectTransform ZoomFrontContainer { get; }

    /// <summary>共通背景への参照（BackGround）</summary>
    Transform Background { get; }
}
