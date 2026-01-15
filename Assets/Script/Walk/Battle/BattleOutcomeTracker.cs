public sealed class BattleOutcomeTracker : IBattleMetaProvider
{
    private readonly IBattleMetaProvider inner;

    public BattleOutcome Outcome { get; private set; } = BattleOutcome.Unknown;

    public BattleOutcomeTracker(IBattleMetaProvider inner)
    {
        this.inner = inner;
    }

    public int NowProgress => inner != null ? inner.NowProgress : 0;

    public void OnPlayersWin()
    {
        Outcome = BattleOutcome.Victory;
        inner?.OnPlayersWin();
    }

    public void OnPlayersLost()
    {
        Outcome = BattleOutcome.Defeat;
        inner?.OnPlayersLost();
    }

    public void OnPlayersRunOut()
    {
        Outcome = BattleOutcome.Escape;
        inner?.OnPlayersRunOut();
    }

    public void SetAlliesUIActive(bool isActive)
    {
        inner?.SetAlliesUIActive(isActive);
    }
}
