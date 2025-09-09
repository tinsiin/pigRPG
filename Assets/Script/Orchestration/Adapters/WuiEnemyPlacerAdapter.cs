using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 既存の WatchUIUpdate.PlaceEnemiesFromBattleGroup に委譲するアダプタ実装。
/// Span（PlaceEnemies / SubSpans）は呼び出し側でラップする前提です。
/// </summary>
public sealed class WuiEnemyPlacerAdapter : IEnemyPlacer
{
    public async UniTask PlaceAsync(IIntroContext ctx, IEnemyPlacementContext placeCtx, CancellationToken ct)
    {
        if (placeCtx == null || placeCtx.EnemyGroup == null)
        {
            Debug.Log("[WuiEnemyPlacerAdapter] placeCtx or EnemyGroup is null. Skip.");
            return;
        }
        var wui = global::WatchUIUpdate.Instance;
        if (wui == null)
        {
            throw new InvalidOperationException("WatchUIUpdate.Instance is null");
        }
        // 既存の実装へ委譲（PlaceEnemiesFromBattleGroup が内部で await を使うため、そのまま待機）
        await wui.PlaceEnemiesFromBattleGroup(placeCtx.EnemyGroup);
        ct.ThrowIfCancellationRequested();
    }
}
