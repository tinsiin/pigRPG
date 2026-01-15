public sealed class EncounterContext
{
    public EncounterSO Encounter { get; }
    public GameContext GameContext { get; }
    public int NowProgress { get; }

    public EncounterContext(EncounterSO encounter, GameContext gameContext)
    {
        Encounter = encounter;
        GameContext = gameContext;
        NowProgress = gameContext != null ? gameContext.Counters.GlobalSteps : 0;
    }
}
