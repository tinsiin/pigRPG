using UnityEngine;

public sealed class WalkBattleMetaProvider : IBattleMetaProvider
{
    private readonly IPlayersParty party;
    private readonly IPlayersUIControl uiControl;

    public WalkBattleMetaProvider(IPlayersParty party, IPlayersUIControl uiControl)
    {
        this.party = party;
        this.uiControl = uiControl;
    }

    public int GlobalSteps => GameContextHub.Current?.Counters?.GlobalSteps ?? 0;

    public void OnPlayersWin()
    {
        if (party == null)
        {
            Debug.LogError("WalkBattleMetaProvider.OnPlayersWin: Party is null");
            return;
        }
        party.PlayersOnWin();
    }

    public void OnPlayersLost()
    {
        if (party == null)
        {
            Debug.LogError("WalkBattleMetaProvider.OnPlayersLost: Party is null");
            return;
        }
        party.PlayersOnLost();
    }

    public void OnPlayersRunOut()
    {
        if (party == null)
        {
            Debug.LogError("WalkBattleMetaProvider.OnPlayersRunOut: Party is null");
            return;
        }
        party.PlayersOnRunOut();
    }

    public void SetAlliesUIActive(bool isActive)
    {
        if (uiControl == null)
        {
            Debug.LogError("WalkBattleMetaProvider.SetAlliesUIActive: UIControl is null");
            return;
        }
        uiControl.AllyAlliesUISetActive(isActive);
    }
}
