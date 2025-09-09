using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 既存実装への委譲で最小限にまとめたデフォルト実装。
/// </summary>
public sealed class DefaultIntroOrchestrator : IIntroOrchestrator
{
    private readonly IEnemyPlacer _placer;
    private readonly IZoomController _zoom;

    public DefaultIntroOrchestrator(IEnemyPlacer placer, IZoomController zoom)
    {
        _placer = placer;
        _zoom = zoom;
    }

    public UniTask PrepareAsync(IIntroContext ctx, CancellationToken ct)
    {
        using (global::MetricsHub.Instance.BeginSpan("Intro.Zoom.Prepare", global::MetricsContext.None))
        {
            // 初期ズーム状態のキャプチャをオーケストレータ側で実施
            _zoom?.CaptureOriginal(ctx?.ZoomFrontContainer, ctx?.ZoomBackContainer);
            Debug.Log($"[Orchestrator.Zoom] Prepare: captured original (front={(ctx?.ZoomFrontContainer!=null)}, back={(ctx?.ZoomBackContainer!=null)})");
        }
        return UniTask.CompletedTask;
    }

    public async UniTask PlayAsync(IIntroContext ctx, CancellationToken ct)
    {
        if (ctx == null) return;
        var gotoScaleXY = ctx.GotoScaleXY;
        var gotoPos     = ctx.GotoPos;
        var duration    = Mathf.Max(0f, ctx.ZoomDuration);
        var curve       = ctx.ZoomCurve;
        var zoomFront   = ctx.ZoomFrontContainer;
        var zoomBack    = ctx.ZoomBackContainer;

        if (zoomFront == null && zoomBack == null) return;
        ct.ThrowIfCancellationRequested();

        using (global::MetricsHub.Instance.BeginSpan("Intro.Zoom.Play", global::MetricsContext.None))
        {
            Debug.Log($"[Orchestrator.Zoom] Play: duration={duration}, gotoScale={gotoScaleXY}, gotoPos={gotoPos}");
            var tasks = new System.Collections.Generic.List<UniTask>(2);
            if (zoomBack != null)
            {
                var startScale = new Vector2(zoomBack.localScale.x, zoomBack.localScale.y);
                var startPos   = new Vector2(zoomBack.anchoredPosition.x, zoomBack.anchoredPosition.y);
                tasks.Add(AnimateZoomAsync(zoomBack, startScale, gotoScaleXY, startPos, gotoPos, duration, curve, ct));
            }
            if (zoomFront != null)
            {
                var startScale = new Vector2(zoomFront.localScale.x, zoomFront.localScale.y);
                var startPos   = new Vector2(zoomFront.anchoredPosition.x, zoomFront.anchoredPosition.y);
                tasks.Add(AnimateZoomAsync(zoomFront, startScale, gotoScaleXY, startPos, gotoPos, duration, curve, ct));
            }

            if (tasks.Count > 0)
            {
                // 外部キャンセルを尊重しつつ並行完了を待機
                ct.ThrowIfCancellationRequested();
                try
                {
                    var all = UniTask.WhenAll(tasks);
                    await all.AttachExternalCancellation(ct);
                }
                catch (System.OperationCanceledException)
                {
                    Debug.Log("[Orchestrator.Zoom] Cancel detected. Restoring immediately.");
                    try { _zoom?.RestoreImmediate(); } catch { /* no-op */ }
                    using (global::MetricsHub.Instance.BeginSpan("Intro.Zoom.Cancel", global::MetricsContext.None)) { }
                    throw;
                }
            }
        }
    }

    private async UniTask AnimateZoomAsync(
        RectTransform target,
        Vector2 startScale,
        Vector2 endScale,
        Vector2 startPos,
        Vector2 endPos,
        float duration,
        AnimationCurve curve,
        CancellationToken ct)
    {
        if (target == null)
        {
            return;
        }
        if (duration <= 0f)
        {
            // 即時適用
            target.localScale = new Vector3(endScale.x, endScale.y, 1f);
            target.anchoredPosition = endPos;
            return;
        }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            ct.ThrowIfCancellationRequested();
            float t = Mathf.Clamp01(elapsed / duration);
            float e = curve != null ? curve.Evaluate(t) : t;
            var s = Vector2.LerpUnclamped(startScale, endScale, e);
            var p = Vector2.LerpUnclamped(startPos, endPos, e);
            target.localScale = new Vector3(s.x, s.y, 1f);
            target.anchoredPosition = p;
            elapsed += Time.unscaledDeltaTime; // timeScale非依存
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        // 最終スナップ
        target.localScale = new Vector3(endScale.x, endScale.y, 1f);
        target.anchoredPosition = endPos;
    }

    // no helpers needed（ctx経由に移行）

    public async UniTask RestoreAsync(IIntroContext ctx, bool animated, float duration, CancellationToken ct)
    {
        using (global::MetricsHub.Instance.BeginSpan("Intro.Zoom.Restore", global::MetricsContext.None))
        {
            Debug.Log($"[Orchestrator.Zoom] Restore: animated={animated}, duration={duration}");
            if (!animated)
            {
                _zoom?.RestoreImmediate();
                return;
            }
            try
            {
                if (_zoom != null)
                {
                    await _zoom.RestoreAsync(Mathf.Max(0f, duration), ctx?.ZoomCurve, ct);
                }
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[Orchestrator.Zoom] Restore canceled. Forcing immediate restore.");
                _zoom?.RestoreImmediate();
                throw;
            }
        }
    }

    public UniTask PlaceEnemiesAsync(IIntroContext ctx, IEnemyPlacementContext placeCtx, CancellationToken ct)
    {
        if (_placer == null) return UniTask.CompletedTask;
        return _placer.PlaceAsync(ctx, placeCtx, ct);
    }
}
