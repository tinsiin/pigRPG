using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// IZoomControllerの実装。WatchUIUpdateのzoomContainersを直接操作する。
/// WuiZoomControllerAdapterのリフレクション依存を解消するための置き換え。
/// </summary>
public sealed class ViewportZoomController : IZoomController
{
    private readonly IViewportController _viewport;
    private RectTransform _front;
    private RectTransform _back;
    private Vector2 _origFrontPos, _origFrontScale;
    private Vector2 _origBackPos, _origBackScale;
    private bool _hasOriginal;

    public ViewportZoomController(IViewportController viewport)
    {
        _viewport = viewport;
    }

    public UniTask ZoomInAsync(float duration, CancellationToken ct)
    {
        return SetZoomAsync(1.2f, duration, ct);
    }

    public UniTask ZoomOutAsync(float duration, CancellationToken ct)
    {
        return SetZoomAsync(1.0f, duration, ct);
    }

    public UniTask SetZoomAsync(float target, float duration, CancellationToken ct)
    {
        var front = _viewport.ZoomFrontContainer;
        var back = _viewport.ZoomBackContainer;

        if (front != null)
        {
            front.localScale = new Vector3(target, target, 1f);
        }
        if (back != null)
        {
            back.localScale = new Vector3(target, target, 1f);
        }
        return UniTask.CompletedTask;
    }

    public void Reset()
    {
        var front = _viewport.ZoomFrontContainer;
        var back = _viewport.ZoomBackContainer;

        if (front != null) front.localScale = Vector3.one;
        if (back != null) back.localScale = Vector3.one;
    }

    public void CaptureOriginal(RectTransform front, RectTransform back)
    {
        _front = front ?? _viewport.ZoomFrontContainer;
        _back = back ?? _viewport.ZoomBackContainer;
        _hasOriginal = false;

        if (_back != null)
        {
            _origBackPos = _back.anchoredPosition;
            var s = _back.localScale;
            _origBackScale = new Vector2(s.x, s.y);
            _hasOriginal = true;
        }
        if (_front != null)
        {
            _origFrontPos = _front.anchoredPosition;
            var s = _front.localScale;
            _origFrontScale = new Vector2(s.x, s.y);
            _hasOriginal = true;
        }
        Debug.Log("[ViewportZoomController] CaptureOriginal: hasOriginal=" + _hasOriginal);
    }

    public void RestoreImmediate()
    {
        if (!_hasOriginal)
        {
            Debug.Log("[ViewportZoomController] RestoreImmediate skipped (no original)");
            return;
        }
        if (_back != null)
        {
            _back.anchoredPosition = _origBackPos;
            _back.localScale = new Vector3(_origBackScale.x, _origBackScale.y, 1f);
        }
        if (_front != null)
        {
            _front.anchoredPosition = _origFrontPos;
            _front.localScale = new Vector3(_origFrontScale.x, _origFrontScale.y, 1f);
        }
        Debug.Log("[ViewportZoomController] RestoreImmediate completed");
    }

    public async UniTask RestoreAsync(float duration, AnimationCurve curve, CancellationToken ct)
    {
        if (!_hasOriginal)
        {
            RestoreImmediate();
            return;
        }
        if (duration <= 0f)
        {
            RestoreImmediate();
            return;
        }

        var tasks = new System.Collections.Generic.List<UniTask>(2);
        if (_back != null)
        {
            var startPos = _back.anchoredPosition;
            var startScale = _back.localScale;
            tasks.Add(AnimateAsync(_back, new Vector2(startScale.x, startScale.y), _origBackScale, startPos, _origBackPos, duration, curve, ct));
        }
        if (_front != null)
        {
            var startPos = _front.anchoredPosition;
            var startScale = _front.localScale;
            tasks.Add(AnimateAsync(_front, new Vector2(startScale.x, startScale.y), _origFrontScale, startPos, _origFrontPos, duration, curve, ct));
        }

        if (tasks.Count > 0)
        {
            var all = UniTask.WhenAll(tasks);
            await all.AttachExternalCancellation(ct);
        }
        Debug.Log("[ViewportZoomController] RestoreAsync completed (duration=" + duration + ")");
    }

    private async UniTask AnimateAsync(
        RectTransform target,
        Vector2 fromScale,
        Vector2 toScale,
        Vector2 fromPos,
        Vector2 toPos,
        float duration,
        AnimationCurve curve,
        CancellationToken ct)
    {
        if (target == null) return;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            ct.ThrowIfCancellationRequested();
            float t = Mathf.Clamp01(elapsed / duration);
            float e = curve != null ? curve.Evaluate(t) : t;
            var s = Vector2.LerpUnclamped(fromScale, toScale, e);
            var p = Vector2.LerpUnclamped(fromPos, toPos, e);
            target.localScale = new Vector3(s.x, s.y, 1f);
            target.anchoredPosition = p;
            elapsed += Time.unscaledDeltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        target.localScale = new Vector3(toScale.x, toScale.y, 1f);
        target.anchoredPosition = toPos;
    }
}
