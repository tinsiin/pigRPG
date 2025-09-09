using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 導入処理（準備/演出/配置）のオーケストレーションI/F。
/// 計測（Span）は呼び出し側でラップする前提で、責務を明確化します。
/// </summary>
public interface IIntroOrchestrator
{
    /// <summary>
    /// 準備フェーズ。UI構築/配置準備/ロード/Canvas更新など、演出前のセットアップを行います。
    /// </summary>
    UniTask PrepareAsync(IIntroContext ctx, CancellationToken ct);

    /// <summary>
    /// 演出フェーズ。ズーム/スライド/段差/遅延などのアニメーションを実行します。
    /// </summary>
    UniTask PlayAsync(IIntroContext ctx, CancellationToken ct);

    /// <summary>
    /// 敵配置。必要に応じ、準備フェーズ内からも呼び出されます。
    /// </summary>
    UniTask PlaceEnemiesAsync(IIntroContext ctx, IEnemyPlacementContext placeCtx, CancellationToken ct);

    /// <summary>
    /// 復元フェーズ。ズーム後の原状復帰を実施します（キャンセル時/デバッグ時など）。
    /// </summary>
    UniTask RestoreAsync(IIntroContext ctx, bool animated, float duration, CancellationToken ct);
}
