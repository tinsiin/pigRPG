public sealed class PlayersContext
{
    public IPlayersParty Party { get; }
    public IPlayersUIControl UIControl { get; }
    public IPlayersSkillUI SkillUI { get; }
    public IPlayersTuning Tuning { get; }
    public IPlayersRoster Roster { get; }
    public IPartyComposition Composition { get; }

    public PlayersContext(
        IPlayersParty party,
        IPlayersUIControl uiControl,
        IPlayersSkillUI skillUi,
        IPlayersTuning tuning,
        IPlayersRoster roster,
        IPartyComposition composition = null)
    {
        Party = party;
        UIControl = uiControl;
        SkillUI = skillUi;
        Tuning = tuning;
        Roster = roster;
        Composition = composition;
    }
}

public interface IPlayersContextConsumer
{
    void InjectPlayersContext(PlayersContext context);
}
