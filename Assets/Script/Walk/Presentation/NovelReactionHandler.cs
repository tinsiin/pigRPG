using UnityEngine;

/// <summary>
/// INovelReactionUIの実装。
/// リアクション（セリフ内クリッカブル要素）のコールバック管理を担当。
/// </summary>
public sealed class NovelReactionHandler : INovelReactionUI
{
    private readonly TextBoxPresenter textBoxPresenter;
    private System.Action<ReactionSegment> currentCallback;

    public NovelReactionHandler(TextBoxPresenter textBoxPresenter)
    {
        this.textBoxPresenter = textBoxPresenter;
    }

    public void SetReactionText(string richText, ReactionSegment[] reactions, System.Action<ReactionSegment> onClicked)
    {
        currentCallback = onClicked;

        if (textBoxPresenter != null)
        {
            textBoxPresenter.SetRichText(richText);

            var handler = textBoxPresenter.GetCurrentReactionHandler();
            if (handler != null)
            {
                handler.Setup(reactions, OnReactionClicked);
            }
            else
            {
                Debug.LogWarning("[NovelReactionHandler] ReactionTextHandler is not assigned in TextBoxPresenter");
            }
        }
        else
        {
            Debug.LogWarning("[NovelReactionHandler] TextBoxPresenter is not assigned");
        }
    }

    public void ClearReactions()
    {
        currentCallback = null;
        textBoxPresenter?.ClearAllReactionHandlers();
    }

    private void OnReactionClicked(ReactionSegment segment)
    {
        currentCallback?.Invoke(segment);
    }
}
