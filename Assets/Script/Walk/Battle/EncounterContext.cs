public sealed class EncounterContext
{
    public EncounterSO Encounter { get; }
    public GameContext GameContext { get; }
    public int GlobalSteps { get; }

    public EncounterContext(EncounterSO encounter, GameContext gameContext)
    {
        Encounter = encounter;
        GameContext = gameContext;
        GlobalSteps = gameContext != null ? gameContext.Counters.GlobalSteps : 0;
    }
}
