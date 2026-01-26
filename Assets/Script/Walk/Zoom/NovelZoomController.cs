using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

/// <summary>
/// ノベルパート用ズームコントローラー。
/// 中央オブジェクトをターゲット領域にフィットするようZoomBackContainerを操作する。
/// KZoomControllerのフィット計算ロジックを流用。
/// </summary>
public sealed class NovelZoomController
{
    private readonly NovelZoomConfig _config;

    // 状態
    private Vector2 _originalPos;
    private Vector3 _originalScale;
    private bool _hasSnapshot;
    private bool _isZooming;
    private CancellationTokenSource _cts;

    // コーナー計算用キャッシュ
    private static Vector3[] s_corners;

    public bool IsZooming => _isZooming;

    public NovelZoomController(NovelZoomConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 中央オブジェクトをターゲット領域にフィットさせてズームイン。
    /// </summary>
    /// <param name="centralObjectRT">中央オブジェクトのRectTransform</param>
    /// <param name="focusArea">フォーカス領域（どの部分をフィットさせるか）</param>
    public async UniTask EnterZoom(RectTransform centralObjectRT, FocusArea focusArea = default)
    {
        if (_config == null || _config.ZoomContainer == null || _config.TargetRect == null)
        {
            Debug.LogWarning("[NovelZoomController] Config or references are null");
            return;
        }

        if (centralObjectRT == null)
        {
            Debug.LogWarning("[NovelZoomController] centralObjectRT is null");
            return;
        }

        if (_isZooming)
        {
            Debug.Log("[NovelZoomController] Already zooming, ignoring EnterZoom");
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _isZooming = true;

        // 原状保存
        _originalPos = _config.ZoomContainer.anchoredPosition;
        _originalScale = _config.ZoomContainer.localScale;
        _hasSnapshot = true;

        // フォーカス領域がdefaultの場合はFullを使用
        if (focusArea.Preset == FocusPreset.Full && focusArea.CustomRect == default)
        {
            focusArea = FocusArea.Default;
        }

        // フィット計算
        ComputeFit(centralObjectRT, focusArea, out float targetScale, out Vector2 targetPos);

        Debug.Log($"[NovelZoomController] EnterZoom: scale={targetScale:F2}, pos={targetPos}, focus={focusArea.Preset}");

        // ズームアニメーション
        try
        {
            var scaleTask = LMotion.Create(_originalScale, new Vector3(targetScale, targetScale, 1f), _config.ZoomDuration)
                .WithEase(_config.ZoomEase)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScale(_config.ZoomContainer)
                .ToUniTask(ct);

            var posTask = LMotion.Create(_originalPos, targetPos, _config.ZoomDuration)
                .WithEase(_config.ZoomEase)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAnchoredPosition(_config.ZoomContainer)
                .ToUniTask(ct);

            await UniTask.WhenAll(scaleTask, posTask);
        }
        catch (System.OperationCanceledException)
        {
            // キャンセル時は即時復帰
            RestoreImmediate();
        }
    }

    /// <summary>
    /// ズームアウトして原状復帰。
    /// </summary>
    public async UniTask ExitZoom()
    {
        if (!_hasSnapshot || _config?.ZoomContainer == null)
        {
            _isZooming = false;
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        Debug.Log("[NovelZoomController] ExitZoom");

        try
        {
            var scaleTask = LMotion.Create(_config.ZoomContainer.localScale, _originalScale, _config.ZoomDuration)
                .WithEase(_config.ZoomEase)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToLocalScale(_config.ZoomContainer)
                .ToUniTask(ct);

            var posTask = LMotion.Create(_config.ZoomContainer.anchoredPosition, _originalPos, _config.ZoomDuration)
                .WithEase(_config.ZoomEase)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAnchoredPosition(_config.ZoomContainer)
                .ToUniTask(ct);

            await UniTask.WhenAll(scaleTask, posTask);
        }
        catch (System.OperationCanceledException)
        {
            // キャンセル時も復帰
        }

        _isZooming = false;
        _hasSnapshot = false;
    }

    /// <summary>
    /// 即座に原状復帰（フェイルセーフ用）。
    /// </summary>
    public void RestoreImmediate()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_hasSnapshot && _config?.ZoomContainer != null)
        {
            _config.ZoomContainer.anchoredPosition = _originalPos;
            _config.ZoomContainer.localScale = _originalScale;
        }

        _isZooming = false;
        _hasSnapshot = false;
    }

    /// <summary>
    /// 中央オブジェクトがターゲット領域にフィットするスケールと位置を計算。
    /// KZoomController.ComputeKFitを参考にした実装。
    /// </summary>
    /// <param name="centralObjectRT">中央オブジェクトのRectTransform</param>
    /// <param name="focusArea">フォーカス領域</param>
    /// <param name="outScale">出力: 計算されたスケール</param>
    /// <param name="outPos">出力: 計算された位置</param>
    private void ComputeFit(RectTransform centralObjectRT, FocusArea focusArea, out float outScale, out Vector2 outPos)
    {
        // オブジェクト全体のワールド座標を取得
        GetWorldRect(centralObjectRT, out Vector2 objectCenter, out Vector2 objectSize);

        // フォーカス領域を適用
        var focusRect = focusArea.GetRect();
        var focusCenter = ApplyFocusRect(objectCenter, objectSize, focusRect, out Vector2 focusSize);

        // ターゲット領域を取得
        GetWorldRect(_config.TargetRect, out Vector2 targetCenter, out Vector2 targetSize);

        // サイズ比からスケール計算（フォーカス領域基準）
        float scaleH = SafeDiv(targetSize.y, focusSize.y);
        float scaleW = SafeDiv(targetSize.x, focusSize.x);
        float scale = Mathf.Lerp(scaleH, scaleW, _config.FitBlend);

        // マージン適用
        scale *= _config.Margin;

        // 位置計算: フォーカス領域の中心がターゲット中心に来るよう移動
        var containerPivotWorld = _config.ZoomContainer.TransformPoint(Vector3.zero);
        Vector2 moveWorld = targetCenter - ((focusCenter - (Vector2)containerPivotWorld) * scale + (Vector2)containerPivotWorld);

        var parentRT = _config.ZoomContainer.parent as RectTransform;
        Vector2 moveLocal = parentRT != null ? (Vector2)parentRT.InverseTransformVector(moveWorld) : moveWorld;

        outScale = scale;
        outPos = _originalPos + moveLocal;
    }

    /// <summary>
    /// フォーカス領域をオブジェクトのワールド座標に適用し、フォーカス領域の中心とサイズを返す。
    /// </summary>
    private static Vector2 ApplyFocusRect(Vector2 objectCenter, Vector2 objectSize, Rect focusRect, out Vector2 focusSize)
    {
        // フォーカス領域のサイズ
        focusSize = new Vector2(
            objectSize.x * focusRect.width,
            objectSize.y * focusRect.height
        );

        // フォーカス領域の中心（オブジェクトの左下を原点として計算）
        // focusRect.x, focusRect.y は左下からの相対位置（0-1）
        var objectBottomLeft = objectCenter - objectSize * 0.5f;
        var focusCenterLocal = new Vector2(
            focusRect.x + focusRect.width * 0.5f,
            focusRect.y + focusRect.height * 0.5f
        );
        var focusCenter = objectBottomLeft + new Vector2(
            objectSize.x * focusCenterLocal.x,
            objectSize.y * focusCenterLocal.y
        );

        return focusCenter;
    }

    private static float SafeDiv(float a, float b)
    {
        return Mathf.Abs(b) < 1e-5f ? 1f : a / b;
    }

    private static void GetWorldRect(RectTransform rt, out Vector2 center, out Vector2 size)
    {
        var corners = s_corners ??= new Vector3[4];
        rt.GetWorldCorners(corners);
        var min = new Vector2(corners[0].x, corners[0].y);
        var max = new Vector2(corners[2].x, corners[2].y);
        center = (min + max) * 0.5f;
        size = max - min;
    }
}
