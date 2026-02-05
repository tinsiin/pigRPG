using System;

/// <summary>
/// 戦闘中のUI/メッセージ表示を担当するクラス。
/// BattleManagerからUI副作用を分離し、テスト可能性を向上させる。
/// </summary>
public sealed class BattlePresentation
{
    private readonly BattleEventBus _eventBus;
    private string _uniqueTopMessage = "";

    public BattlePresentation(BattleEventBus eventBus)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <summary>
    /// 冠詞メッセージをリセット
    /// </summary>
    public void ResetTopMessage()
    {
        _uniqueTopMessage = "";
    }

    /// <summary>
    /// 冠詞メッセージを設定
    /// </summary>
    public void SetTopMessage(string message)
    {
        _uniqueTopMessage = message ?? "";
    }

    /// <summary>
    /// 冠詞メッセージに追記
    /// </summary>
    public void AppendTopMessage(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            _uniqueTopMessage += message;
        }
    }

    /// <summary>
    /// 戦闘メッセージを作成して表示
    /// </summary>
    public void CreateBattleMessage(string txt)
    {
        _eventBus.Publish(BattleEvent.MessageOnly(_uniqueTopMessage + txt));
    }

    /// <summary>
    /// イベントバスへの直接アクセス（移行期間用）
    /// </summary>
    public BattleEventBus EventBus => _eventBus;
}
