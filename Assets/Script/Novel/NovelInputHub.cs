using System;
using Cysharp.Threading.Tasks;

/// <summary>
/// ノベルパートの入力を集約するハブ。
/// 各UIコンポーネントからの入力を受け取り、INovelInputProviderとして提供する。
/// </summary>
public sealed class NovelInputHub : INovelInputProvider
{
    private UniTaskCompletionSource<bool> nextOrBackTcs;
    private UniTaskCompletionSource<int> choiceTcs;
    private bool isCancelled;
    private bool backlogRequested;

    /// <summary>
    /// 次へ進む入力を通知する。
    /// </summary>
    public void NotifyNext()
    {
        nextOrBackTcs?.TrySetResult(true);
    }

    /// <summary>
    /// 戻る入力を通知する。
    /// </summary>
    public void NotifyBack()
    {
        nextOrBackTcs?.TrySetResult(false);
    }

    /// <summary>
    /// 選択肢が選ばれたことを通知する。
    /// </summary>
    public void NotifyChoice(int index)
    {
        choiceTcs?.TrySetResult(index);
    }

    public async UniTask WaitForNextAsync()
    {
        isCancelled = false;
        nextOrBackTcs = new UniTaskCompletionSource<bool>();

        try
        {
            await nextOrBackTcs.Task;
        }
        finally
        {
            nextOrBackTcs = null;
        }
    }

    public async UniTask<bool> WaitForNextOrBackAsync()
    {
        isCancelled = false;
        nextOrBackTcs = new UniTaskCompletionSource<bool>();

        try
        {
            return await nextOrBackTcs.Task;
        }
        finally
        {
            nextOrBackTcs = null;
        }
    }

    public async UniTask<int> WaitForChoiceAsync(int choiceCount)
    {
        isCancelled = false;
        choiceTcs = new UniTaskCompletionSource<int>();

        try
        {
            return await choiceTcs.Task;
        }
        finally
        {
            choiceTcs = null;
        }
    }

    public void CancelWait()
    {
        isCancelled = true;
        nextOrBackTcs?.TrySetCanceled();
        choiceTcs?.TrySetCanceled();
    }

    /// <summary>
    /// キャンセルされたかどうか。
    /// </summary>
    public bool IsCancelled => isCancelled;

    /// <summary>
    /// バックログ表示入力を通知する。
    /// </summary>
    public void NotifyBacklog()
    {
        backlogRequested = true;
        // 入力待ちをキャンセルして即座にバックログ処理へ進む
        nextOrBackTcs?.TrySetCanceled();
    }

    /// <summary>
    /// バックログ表示リクエストを消費する。
    /// </summary>
    public bool ConsumeBacklogRequest()
    {
        var result = backlogRequested;
        backlogRequested = false;
        return result;
    }
}
