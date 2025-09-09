using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 敵UIの生成・レイアウト・有効化（Spawn/Layout/Activate）を担う責務の抽象。
/// 計測（Span）は呼び出し側でラップしやすいよう、1メソッドに集約しています。
/// </summary>
public interface IEnemyPlacer
{
    /// <summary>
    /// コンテキストに基づき敵UIを配置します。
    /// </summary>
    UniTask PlaceAsync(IIntroContext ctx, IEnemyPlacementContext placeCtx, CancellationToken ct);
}
