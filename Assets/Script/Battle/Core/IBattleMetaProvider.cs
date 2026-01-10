public interface IBattleMetaProvider
{
    int NowProgress { get; }
    void OnPlayersWin();
    void OnPlayersLost();
    void OnPlayersRunOut();
    void SetAlliesUIActive(bool isActive);
}
