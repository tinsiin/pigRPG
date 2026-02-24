using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// INovelZoomUIの実装。
/// ズーム制御と中央オブジェクトのスプライト操作を担当。
/// </summary>
public sealed class NovelZoomManager : INovelZoomUI
{
    private readonly NovelZoomController zoomController;
    private CentralObjectPresenter centralObjectPresenter;
    private PortraitDatabase portraitDatabase;

    public NovelZoomManager(NovelZoomConfig config)
    {
        zoomController = config != null ? new NovelZoomController(config) : null;
    }

    public void SetCentralObjectPresenter(CentralObjectPresenter presenter)
    {
        centralObjectPresenter = presenter;
        // PortraitDatabaseが先にセットされていた場合、ここで注入
        if (portraitDatabase != null)
        {
            centralObjectPresenter?.SetPortraitDatabase(portraitDatabase);
        }
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

    public void UpdateCentralObjectSprite(Sprite sprite, string characterId = null, string expression = null)
    {
        centralObjectPresenter?.UpdateSprite(sprite, characterId, expression);
    }

    public Sprite GetCurrentCentralObjectSprite()
    {
        return centralObjectPresenter?.GetCurrentSprite();
    }

    public string GetCurrentCentralObjectCharacterId()
    {
        return centralObjectPresenter?.CurrentCharacterId;
    }

    public string GetCurrentCentralObjectExpression()
    {
        return centralObjectPresenter?.CurrentExpression;
    }

    public void SetTemporaryCentralExpression(string expression)
    {
        centralObjectPresenter?.SetTemporaryExpression(expression);
    }

    public void ClearTemporaryCentralExpression()
    {
        centralObjectPresenter?.ClearTemporaryExpression();
    }

    public void SetPortraitDatabase(PortraitDatabase db)
    {
        portraitDatabase = db;
        centralObjectPresenter?.SetPortraitDatabase(db);
    }
}
