public interface IBattleMetaProvider
{
    int GlobalSteps { get; }
    void OnPlayersWin();
    void OnPlayersLost();
    void OnPlayersRunOut();
    void SetAlliesUIActive(bool isActive);
}
