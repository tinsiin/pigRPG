using System;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// IIntroOrchestrator + ContextProvider を束ねる実装。
/// </summary>
public sealed class IntroOrchestratorFacade : IIntroOrchestratorFacade
{
    private readonly IIntroOrchestrator _orchestrator;
    private readonly IIntroContextProvider _contextProvider;
    private readonly IEnemyPlacementContextProvider _placementProvider;
    private readonly Func<CancellationToken> _tokenProvider;

    public IntroOrchestratorFacade(
        IIntroOrchestrator orchestrator,
        IIntroContextProvider contextProvider,
        IEnemyPlacementContextProvider placementProvider,
        Func<CancellationToken> tokenProvider = null)
    {
        _orchestrator = orchestrator;
        _contextProvider = contextProvider;
        _placementProvider = placementProvider;
        _tokenProvider = tokenProvider;
    }

    public UniTask PrepareAsync(CancellationToken ct = default)
    {
        if (_orchestrator == null || _contextProvider == null) return UniTask.CompletedTask;
        var ctx = _contextProvider.BuildIntroContext();
        if (ctx == null) return UniTask.CompletedTask;
        return _orchestrator.PrepareAsync(ctx, ResolveToken(ct));
    }

    public UniTask PlayAsync(CancellationToken ct = default)
    {
        if (_orchestrator == null || _contextProvider == null) return UniTask.CompletedTask;
        var ctx = _contextProvider.BuildIntroContext();
        if (ctx == null) return UniTask.CompletedTask;
        return _orchestrator.PlayAsync(ctx, ResolveToken(ct));
    }

    public UniTask PlaceEnemiesAsync(BattleGroup enemyGroup, CancellationToken ct = default)
    {
        if (_orchestrator == null || _contextProvider == null || _placementProvider == null)
        {
            return UniTask.CompletedTask;
        }
        if (enemyGroup == null) return UniTask.CompletedTask;
        var ctx = _contextProvider.BuildIntroContext();
        if (ctx == null) return UniTask.CompletedTask;
        var placeCtx = _placementProvider.BuildPlacementContext(enemyGroup);
        if (placeCtx == null) return UniTask.CompletedTask;
        return _orchestrator.PlaceEnemiesAsync(ctx, placeCtx, ResolveToken(ct));
    }

    public UniTask RestoreAsync(bool animated = false, float duration = 0f, CancellationToken ct = default)
    {
        if (_orchestrator == null || _contextProvider == null) return UniTask.CompletedTask;
        var ctx = _contextProvider.BuildIntroContext();
        if (ctx == null) return UniTask.CompletedTask;
        return _orchestrator.RestoreAsync(ctx, animated, duration, ResolveToken(ct));
    }

    private CancellationToken ResolveToken(CancellationToken ct)
    {
        if (ct.CanBeCanceled) return ct;
        return _tokenProvider != null ? _tokenProvider() : ct;
    }
}
