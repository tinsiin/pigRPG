using Cysharp.Threading.Tasks;

/// <summary>
/// ノベルパートの入力を提供するインターフェース。
/// USERUI側のコンポーネントがこれを実装し、NovelPartDialogueRunnerが入力を待つ。
/// </summary>
public interface INovelInputProvider
{
    /// <summary>
    /// 次へ進む入力を待つ。
    /// </summary>
    UniTask WaitForNextAsync();

    /// <summary>
    /// 次へまたは戻る入力を待つ。
    /// </summary>
    /// <returns>true: 次へ, false: 戻る</returns>
    UniTask<bool> WaitForNextOrBackAsync();

    /// <summary>
    /// 選択肢の入力を待つ。
    /// </summary>
    /// <param name="choiceCount">選択肢の数</param>
    /// <returns>選択されたインデックス（0始まり）</returns>
    UniTask<int> WaitForChoiceAsync(int choiceCount);

    /// <summary>
    /// 入力待ちをキャンセルする（リアクション発火時など）。
    /// </summary>
    void CancelWait();
}
