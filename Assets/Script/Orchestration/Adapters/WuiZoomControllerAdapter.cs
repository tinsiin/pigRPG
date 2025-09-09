using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// WatchUIUpdate の zoomFrontContainer / zoomBackContainer を直接操作する簡易アダプタ。
/// 先行フェーズでは派手な演出は行わず、SetZoomAsync の即時適用で足場を作る。
/// </summary>
public sealed class WuiZoomControllerAdapter : IZoomController
{
    private RectTransform _front;
    private RectTransform _back;
    private Vector2 _origFrontPos, _origFrontScale;
    private Vector2 _origBackPos, _origBackScale;
    private bool _hasOriginal;

    public UniTask ZoomInAsync(float duration, CancellationToken ct)
    {
        // 先行フェーズ: 固定倍率へ即時適用（将来、曲線/LMotionへ移行）
        return SetZoomAsync(1.2f, duration, ct);
    }

    public UniTask ZoomOutAsync(float duration, CancellationToken ct)
    {
        return SetZoomAsync(1.0f, duration, ct);
    }

    public UniTask SetZoomAsync(float target, float duration, CancellationToken ct)
    {
        var wui = WatchUIUpdate.Instance;
        if (wui == null) return UniTask.CompletedTask;
        // front/back の RectTransform を見つけてスケールを合わせる
        var front = GetRect(wui, "zoomFrontContainer");
        var back  = GetRect(wui, "zoomBackContainer");
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
        var wui = WatchUIUpdate.Instance;
        if (wui == null) return;
        var front = GetRect(wui, "zoomFrontContainer");
        var back  = GetRect(wui, "zoomBackContainer");
        if (front != null) front.localScale = Vector3.one;
        if (back  != null) back.localScale  = Vector3.one;
    }

    public void CaptureOriginal(RectTransform front, RectTransform back)
    {
        _front = front;
        _back  = back;
        _hasOriginal = false;
        if (_back != null)
        {
            _origBackPos = _back.anchoredPosition;
            var s = _back.localScale; _origBackScale = new Vector2(s.x, s.y);
            _hasOriginal = true;
        }
        if (_front != null)
        {
            _origFrontPos = _front.anchoredPosition;
            var s = _front.localScale; _origFrontScale = new Vector2(s.x, s.y);
            _hasOriginal = true;
        }
        Debug.Log("[ZoomController] CaptureOriginal: hasOriginal=" + _hasOriginal);
    }

    public void RestoreImmediate()
    {
        if (!_hasOriginal)
        {
            Debug.Log("[ZoomController] RestoreImmediate skipped (no original)");
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
        Debug.Log("[ZoomController] RestoreImmediate completed");
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
        Debug.Log("[ZoomController] RestoreAsync completed (duration=" + duration + ")");
    }

    private async UniTask AnimateAsync(RectTransform target, Vector2 fromScale, Vector2 toScale, Vector2 fromPos, Vector2 toPos, float duration, AnimationCurve curve, CancellationToken ct)
    {
        if (target == null)
        {
            return;
        }
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

    private RectTransform GetRect(WatchUIUpdate wui, string fieldName)
    {
        // リフレクションで private フィールドを簡便に参照（当面の足場）。将来はI/F化で置換。
        var fi = typeof(WatchUIUpdate).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var tr = fi?.GetValue(wui) as Transform;
        return tr as RectTransform;
    }
}
