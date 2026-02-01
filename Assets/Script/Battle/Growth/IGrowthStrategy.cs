public interface IGrowthStrategy
{
    GrowthStrategyType Type { get; }
    void Apply(EnemyGrowthContext context);
}
