public sealed class PlayersBattleCallbacks
{
    private readonly PlayersRoster roster;

    public PlayersBattleCallbacks(PlayersRoster roster)
    {
        this.roster = roster;
    }

    public void PlayersOnWin()
    {
        foreach (var ally in roster.AllAllies)
        {
            ally.OnAllyWinCallBack();
        }
    }

    public void PlayersOnLost()
    {
        foreach (var ally in roster.AllAllies)
        {
            ally.OnAllyLostCallBack();
        }
    }

    public void PlayersOnRunOut()
    {
        foreach (var ally in roster.AllAllies)
        {
            ally.OnAllyRunOutCallBack();
        }
    }
}
