using UnityEngine;

public sealed class PlayersPartyService : IPlayersParty
{
    private readonly PlayersRoster roster;
    private readonly PartyBuilder partyBuilder;
    private readonly PlayersBattleCallbacks battleCallbacks;
    private readonly WalkLoopService walkLoopService;

    public PlayersPartyService(
        PlayersRoster roster,
        PartyBuilder partyBuilder,
        PlayersBattleCallbacks battleCallbacks,
        WalkLoopService walkLoopService)
    {
        this.roster = roster;
        this.partyBuilder = partyBuilder;
        this.battleCallbacks = battleCallbacks;
        this.walkLoopService = walkLoopService;
    }

    public BattleGroup GetParty()
    {
        return partyBuilder.BuildParty();
    }

    public void PlayersOnWin()
    {
        battleCallbacks.PlayersOnWin();
    }

    public void PlayersOnLost()
    {
        battleCallbacks.PlayersOnLost();
    }

    public void PlayersOnRunOut()
    {
        battleCallbacks.PlayersOnRunOut();
    }

    public void PlayersOnWalks(int walkCount)
    {
        walkLoopService.PlayersOnWalks(walkCount);
    }

    public void RequestStopFreezeConsecutive(CharacterId id)
    {
        var actor = roster.GetAlly(id);
        if (actor == null)
        {
            Debug.LogWarning($"RequestStopFreezeConsecutive: CharacterId {id} が不正です。");
            return;
        }
        actor.TurnOnDeleteMyFreezeConsecutiveFlag();
    }
}
