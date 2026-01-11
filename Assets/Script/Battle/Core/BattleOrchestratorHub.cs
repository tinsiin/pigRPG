public static class BattleOrchestratorHub
{
    public static BattleOrchestrator Current { get; private set; }

    public static void Set(BattleOrchestrator orchestrator)
    {
        Current = orchestrator;
    }

    public static void Clear(BattleOrchestrator orchestrator)
    {
        if (ReferenceEquals(Current, orchestrator))
        {
            Current = null;
        }
    }
}
