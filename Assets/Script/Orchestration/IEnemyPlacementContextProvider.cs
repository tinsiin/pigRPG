/// <summary>
/// 敵配置コンテキストを提供するためのプロバイダーI/F。
/// </summary>
public interface IEnemyPlacementContextProvider
{
    IEnemyPlacementContext BuildPlacementContext(BattleGroup enemyGroup);
}
