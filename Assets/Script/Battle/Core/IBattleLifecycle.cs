using Cysharp.Threading.Tasks;

public interface IBattleLifecycle
{
    BattlePhase Phase { get; }
    TabState CurrentUiState { get; }

    TabState StartBattle();
    UniTask<TabState> RequestAdvanceAsync();
    TabState ApplyInput(ActionInput input);
    UniTask EndAsync();
    void Cancel();
}
