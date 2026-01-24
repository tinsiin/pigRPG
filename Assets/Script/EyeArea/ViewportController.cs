using UnityEngine;

/// <summary>
/// ビューポート（ズームレイヤー）を管理するコントローラー。
/// IViewportControllerの実装を担当し、WatchUIUpdateから責務を分離する。
/// </summary>
public sealed class ViewportController : IViewportController
{
    private readonly RectTransform _backContainer;
    private readonly RectTransform _frontContainer;
    private readonly IZoomController _zoomController;

    /// <summary>
    /// ViewportControllerを構築する。
    /// </summary>
    /// <param name="backContainer">背景用ズームコンテナ（zoomBackContainer）</param>
    /// <param name="frontContainer">前景用ズームコンテナ（zoomFrontContainer）</param>
    public ViewportController(RectTransform backContainer, RectTransform frontContainer)
    {
        _backContainer = backContainer;
        _frontContainer = frontContainer;
        _zoomController = new ViewportZoomController(this);
    }

    /// <summary>ズーム制御（IZoomController）</summary>
    public IZoomController Zoom => _zoomController;

    /// <summary>背景用ズームコンテナ</summary>
    public RectTransform ZoomBackContainer => _backContainer;

    /// <summary>前景用ズームコンテナ</summary>
    public RectTransform ZoomFrontContainer => _frontContainer;

    /// <summary>背景Transform（BackContainerの最初の子）</summary>
    public Transform Background => _backContainer?.childCount > 0 ? _backContainer.GetChild(0) : null;

}
