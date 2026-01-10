public static class PlayersStatesHub
{
    public static IPlayersProgress Progress { get; private set; }
    public static IPlayersParty Party { get; private set; }
    public static IPlayersUIControl UIControl { get; private set; }
    public static IPlayersSkillUI SkillUI { get; private set; }
    public static IPlayersTuning Tuning { get; private set; }
    public static IPlayersRoster Roster { get; private set; }

    public static void Bind(
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

    public static void ClearAll()
    {
        Progress = null;
        Party = null;
        UIControl = null;
        SkillUI = null;
        Tuning = null;
        Roster = null;
    }
}
