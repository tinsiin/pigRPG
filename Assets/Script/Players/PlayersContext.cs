public sealed class PlayersContext
{
    public IPlayersParty Party { get; }
    public IPlayersUIControl UIControl { get; }
    public IPlayersSkillUI SkillUI { get; }
    public IPlayersTuning Tuning { get; }
    public IPlayersRoster Roster { get; }

    public PlayersContext(
        IPlayersParty party,
        IPlayersUIControl uiControl,
        IPlayersSkillUI skillUi,
        IPlayersTuning tuning,
        IPlayersRoster roster)
    {
        Party = party;
        UIControl = uiControl;
        SkillUI = skillUi;
        Tuning = tuning;
        Roster = roster;
    }
}

public interface IPlayersContextConsumer
{
    void InjectPlayersContext(PlayersContext context);
}
