public readonly struct BattleResult
{
    public bool Encountered { get; }
    public BattleOutcome Outcome { get; }

    public BattleResult(bool encountered, BattleOutcome outcome)
    {
        Encountered = encountered;
        Outcome = outcome;
    }

    public static BattleResult None => new BattleResult(false, BattleOutcome.Unknown);
}
