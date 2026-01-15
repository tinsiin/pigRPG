using UnityEngine;

public sealed class PlayersStatesBattleMetaProvider : IBattleMetaProvider
{
    private readonly IPlayersProgress progress;
    private readonly IPlayersParty party;
    private readonly IPlayersUIControl uiControl;
    private readonly int? nowProgressOverride;

    public PlayersStatesBattleMetaProvider(
        IPlayersProgress progress,
        IPlayersParty party,
        IPlayersUIControl uiControl,
        int? nowProgressOverride = null)
    {
        this.progress = progress;
        this.party = party;
        this.uiControl = uiControl;
        this.nowProgressOverride = nowProgressOverride;
    }

    public int NowProgress => nowProgressOverride ?? (progress != null ? progress.NowProgress : 0);

    public void OnPlayersWin()
    {
        if (party == null)
        {
            Debug.LogError("PlayersStatesBattleMetaProvider.OnPlayersWin: Party が null です");
            return;
        }
        party.PlayersOnWin();
    }

    public void OnPlayersLost()
    {
        if (party == null)
        {
            Debug.LogError("PlayersStatesBattleMetaProvider.OnPlayersLost: Party が null です");
            return;
        }
        party.PlayersOnLost();
    }

    public void OnPlayersRunOut()
    {
        if (party == null)
        {
            Debug.LogError("PlayersStatesBattleMetaProvider.OnPlayersRunOut: Party が null です");
            return;
        }
        party.PlayersOnRunOut();
    }

    public void SetAlliesUIActive(bool isActive)
    {
        if (uiControl == null)
        {
            Debug.LogError("PlayersStatesBattleMetaProvider.SetAlliesUIActive: UIControl が null です");
            return;
        }
        uiControl.AllyAlliesUISetActive(isActive);
    }
}
