public sealed class PlayersContext
{
    public IPlayersProgress Progress { get; }
    public IPlayersParty Party { get; }
    public IPlayersUIControl UIControl { get; }
    public IPlayersSkillUI SkillUI { get; }
    public IPlayersTuning Tuning { get; }
    public IPlayersRoster Roster { get; }

    public PlayersContext(
        IPlayersProgress progress,
        IPlayersParty party,
        IPlayersUIControl uiControl,
        IPlayersSkillUI skillUi,
        IPlayersTuning tuning,
        IPlayersRoster roster)
    {
        Progress = progress;
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