using Cysharp.Threading.Tasks;

/// <summary>
/// ノベルパート/会話実行インターフェース。
/// IBattleRunnerと同じパターン。
/// </summary>
public interface IDialogueRunner
{
    UniTask<DialogueResult> RunDialogueAsync(DialogueContext context);
}
