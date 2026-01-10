public sealed class PlayersBattleCallbacks
{
    private readonly PlayersRoster roster;

    public PlayersBattleCallbacks(PlayersRoster roster)
    {
        this.roster = roster;
    }

    public void PlayersOnWin()
    {
        var allies = roster.Allies;
        for (int i = 0; i < allies.Length; i++)
        {
            allies[i].OnAllyWinCallBack();
        }
    }

    public void PlayersOnLost()
    {
        var allies = roster.Allies;
        for (int i = 0; i < allies.Length; i++)
        {
            allies[i].OnAllyLostCallBack();
        }
    }

    public void PlayersOnRunOut()
    {
        var allies = roster.Allies;
        for (int i = 0; i < allies.Length; i++)
        {
            allies[i].OnAllyRunOutCallBack();
        }
    }
}
