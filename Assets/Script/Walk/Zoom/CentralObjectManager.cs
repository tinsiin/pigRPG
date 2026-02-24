using UnityEngine;

/// <summary>
/// ICentralObjectUIの実装。
/// 中央オブジェクトのスプライト管理と雑音連動の一時表情変更を担当。
/// </summary>
public sealed class CentralObjectManager : ICentralObjectUI
{
    private CentralObjectPresenter centralObjectPresenter;

    public void SetCentralObjectPresenter(CentralObjectPresenter presenter, PortraitDatabase portraitDatabase)
    {
        centralObjectPresenter = presenter;
        if (portraitDatabase != null)
        {
            centralObjectPresenter?.SetPortraitDatabase(portraitDatabase);
        }
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
}
