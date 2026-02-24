using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// INovelZoomUIの実装。
/// ズームイン/アウトのアニメーション制御のみを担当。
/// </summary>
public sealed class NovelZoomManager : INovelZoomUI
{
    private readonly NovelZoomController zoomController;

    public NovelZoomManager(NovelZoomConfig config)
    {
        zoomController = config != null ? new NovelZoomController(config) : null;
    }

    public async UniTask ZoomToCentralAsync(RectTransform centralObjectRT, FocusArea focusArea)
    {
        if (zoomController == null)
        {
            Debug.LogWarning("[NovelZoomManager] ZoomController is not initialized");
            return;
        }

        await zoomController.EnterZoom(centralObjectRT, focusArea);
    }

    public async UniTask ExitZoomAsync()
    {
        if (zoomController == null) return;
        await zoomController.ExitZoom();
    }

    public void RestoreZoomImmediate()
    {
        zoomController?.RestoreImmediate();
    }
}
