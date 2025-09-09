/// <summary>
/// IEnemyPlacementContext のシンプル実装。
/// </summary>
public sealed class EnemyPlacementContext : IEnemyPlacementContext
{
    public int EnemyCount { get; private set; }
    public bool BatchActivate { get; private set; }
    public float? FixedSizeOverride { get; private set; }
    public BattleGroup EnemyGroup { get; private set; }

    public EnemyPlacementContext(BattleGroup enemyGroup, int enemyCount, bool batchActivate, float? fixedSizeOverride)
    {
        EnemyGroup = enemyGroup;
        EnemyCount = enemyCount;
        BatchActivate = batchActivate;
        FixedSizeOverride = fixedSizeOverride;
    }
}
