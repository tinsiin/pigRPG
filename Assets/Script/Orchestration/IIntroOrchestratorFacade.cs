using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// IIntroOrchestrator を使いやすくするファサード。
/// 文脈生成（IIntroContext/IEnemyPlacementContext）を内部で行う。
/// </summary>
public interface IIntroOrchestratorFacade
{
    UniTask PrepareAsync(CancellationToken ct = default);
    UniTask PlayAsync(CancellationToken ct = default);
    UniTask PlaceEnemiesAsync(BattleGroup enemyGroup, CancellationToken ct = default);
    UniTask RestoreAsync(bool animated = false, float duration = 0f, CancellationToken ct = default);
}
