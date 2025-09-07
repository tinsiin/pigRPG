using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using LitMotion;
using LitMotion.Extensions;

/// <summary>
/// 親RectTransformの下辺から子の矢印画像を上方向へ平行移動し、
/// 画像の中心点が親の上辺に触れたタイミングで、Xスケールを 1→0 に収束させて消す一回限りのUIエフェクト。
/// ・フェードは使用しない
/// ・移動速度はピクセル/秒で指定
/// ・非同期（UniTask）で1回だけ再生
/// 推奨設定：子矢印RectTransformの anchor=(0.5,0), pivot=(0.5,0), anchoredPosition=(0,0)
/// </summary>
public class ArrowGrowAndVanish : MonoBehaviour
{
    [Header("References _container:これは未指定時は自身のRectTrasformを使う。")]
    [SerializeField] private RectTransform _container;   // 親（通過矩形）。未指定時は自身のRectTransformを使用
    [SerializeField] private RectTransform _arrow;       // 子の矢印画像（Image推奨）

    [Header("Grow Settings (Y scale)")]
    [Tooltip("開始時のY拡大率（1で原寸）。フェードなしで現れるため通常は1推奨")]
    [SerializeField, Min(0f)] private float _startYScale = 1f;
    [Tooltip("Y拡大の速度（倍率/秒）。イージングなし・線形で増加")]
    [SerializeField, Min(0f)] private float _growSpeedPerSec = 1.0f;

    [Header("Move Settings (Y position)")]
    [Tooltip("上方向への移動速度（ピクセル/秒）。Yスケールは使用しません。")]
    [SerializeField, Min(0f)] private float _moveSpeedYPerSec = 250f;

    [Header("Vanish Settings (X scale)")]
    [Tooltip("しぼみ消滅にかける時間（秒）。>0 で時間固定。<=0 の場合は速度指定を使用")]
    [SerializeField] private float _vanishDuration = 0.2f;
    [Tooltip("時間指定を使わない場合のX縮小速度（倍率/秒）")]
    [SerializeField, Min(0f)] private float _vanishSpeedXPerSec = 5f;

    [Header("Options")]
    [Tooltip("完了時に矢印オブジェクトを非アクティブ化する")]
    [SerializeField] private bool _deactivateOnComplete = true;
    [Tooltip("Awakeで子矢印の anchor/pivot を (0.5,0) に強制設定")]
    [SerializeField] private bool _forceAnchorPivotOnAwake = true;

    [Header("Initialize From Icon")]
    [Tooltip("サイズ参照元となるアイコンのRectTransform。ここで指定が無い場合、InitializeArrowByIconの引数で渡してください。")]
    [SerializeField] private RectTransform _iconRect;
    [Tooltip("アイコンに対する矢印サイズの比率（X=幅比、Y=高さ比）。矢印RectTransformのsizeDeltaに反映します。")]
    [SerializeField] private Vector2 _arrowSizeRatioToIcon = new Vector2(1f, 1f);

    private CancellationTokenSource _cts;
    private MotionHandle _moveHandle;
    private MotionHandle _vanishHandle;
    private MotionHandle _growHandle;
    private bool _completed;
    private bool _vanishStarted;

    void Awake()
    {
        if (_container == null)
        {
            _container = transform as RectTransform;
        }

        if (_forceAnchorPivotOnAwake && _arrow != null)
        {
            _arrow.anchorMin = new Vector2(0.5f, 0f);
            _arrow.anchorMax = new Vector2(0.5f, 0f);
            _arrow.pivot     = new Vector2(0.5f, 0f);
            _arrow.anchoredPosition = Vector2.zero; // 親の下辺に底面を合わせる
        }

        // 初期状態では非表示（再生時のみ表示したい場合）
        if (_arrow != null)
        {
            _arrow.gameObject.SetActive(false);
        }
    }

    void OnDisable() => Cancel();

    public void Cancel()
    {
        // 再入安全: _cts をローカルに退避し、先にフィールドを null にしてから Cancel/Dispose
        var cts = _cts;
        _cts = null;
        if (cts != null)
        {
            try { cts.Cancel(); }
            finally { cts.Dispose(); }
        }

        if (_moveHandle.IsActive()) _moveHandle.Cancel();
        if (_vanishHandle.IsActive()) _vanishHandle.Cancel();
        if (_growHandle.IsActive()) _growHandle.Cancel();
        _vanishStarted = false;
        _completed = false;

        // 再生中断時も非表示にしておく
        if (_arrow != null)
        {
            _arrow.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 外部から一回限り再生。await可能。
    /// </summary>
    public UniTask PlayOnceAsync(CancellationToken externalToken = default)
    {
        Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        return PlayRoutineAsync(_cts.Token);
    }

    private async UniTask PlayRoutineAsync(CancellationToken token)
    {
        if (_container == null || _arrow == null)
        {
            Debug.LogWarning("[ArrowGrowAndVanish] Missing references.");
            return;
        }

        if (!_arrow.gameObject.activeSelf)
            _arrow.gameObject.SetActive(true);

        // 初期スケールを原寸に、位置を親下辺(0)へリセット（元実装に忠実）
        var s = _arrow.localScale;
        s.x = 1f;
        s.y = Mathf.Max(0f, _startYScale);
        _arrow.localScale = s;
        _arrow.anchoredPosition = Vector2.zero;

        // 状態フラグ初期化
        _completed = false;
        _vanishStarted = false;

        // 上方向への移動を LitMotion で駆動（unscaled）
        Vector2 startPos = _arrow.anchoredPosition;
        _moveHandle = LMotion.Create(0f, 1f, 1f)
            .WithEase(Ease.Linear)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithLoops(-1, LoopType.Incremental)
            .Bind(timeSec =>
            {
                if (_arrow == null) return;
                float newY = startPos.y + _moveSpeedYPerSec * timeSec;
                _arrow.anchoredPosition = new Vector2(startPos.x, newY);

                // 到達判定で移動を終了し、消滅フェーズへ
                if (!_vanishStarted && IsArrowCenterTouchingContainerTop())
                {
                    _vanishStarted = true;
                    if (_moveHandle.IsActive()) _moveHandle.Cancel();
                    StartVanishTween();
                }
            })
            .AddTo(gameObject);

        // Yスケール成長を LitMotion で駆動（unscaled、線形・速度指定）
        StartGrowTween();

        using (token.Register(Cancel))
        {
            await UniTask.WaitUntil(() => _completed, cancellationToken: token);
        }
    }

    /// <summary>
    /// 画像の中心点がコンテナ上辺に触れたか判定（回転なし前提）。
    /// 子矢印の pivot=(0.5,0) を前提に、ローカル中心(0, rect.h/2) を TransformPoint でワールドへ変換。
    /// World Y の比較で上辺到達を判定する。
    /// </summary>
    private bool IsArrowCenterTouchingContainerTop()
    {
        // 子画像の中心（ローカル：pivot(0.5,0)なら (0, rect.h/2)）
        Vector3 arrowCenterLocal = new Vector3(0f, _arrow.rect.height * 0.5f, 0f);
        Vector3 arrowCenterWorld = _arrow.TransformPoint(arrowCenterLocal);

        // 親の上辺ローカルY（pivotに依存）
        float topLocalY = (1f - _container.pivot.y) * _container.rect.height;
        Vector3 containerTopWorld = _container.TransformPoint(new Vector3(0f, topLocalY, 0f));

        // 基本UIは回転しない想定なのでWorld Yで比較
        return arrowCenterWorld.y >= containerTopWorld.y;
    }

    private void StartVanishTween()
    {
        if (_vanishHandle.IsActive()) _vanishHandle.Cancel();
        if (_growHandle.IsActive()) _growHandle.Cancel();

        float duration;
        if (_vanishDuration > 0f)
        {
            duration = _vanishDuration;
        }
        else
        {
            float speed = Mathf.Max(0.0001f, _vanishSpeedXPerSec);
            duration = 1f / speed; // X=1→0 までの所要時間
        }

        _vanishHandle = LMotion.Create(1f, 0f, duration)
            .WithEase(Ease.Linear)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(x =>
            {
                if (_arrow == null) return;
                var cur = _arrow.localScale;
                cur.x = x;
                _arrow.localScale = cur;

                if (x <= 0f)
                {
                    if (_deactivateOnComplete && _arrow != null)
                    {
                        _arrow.gameObject.SetActive(false);
                    }
                    _completed = true;
                }
            })
            .AddTo(gameObject);
    }

    private void StartGrowTween()
    {
        if (_growHandle.IsActive()) _growHandle.Cancel();
        float baseScale = Mathf.Max(0f, _startYScale);
        _growHandle = LMotion.Create(0f, 1f, 1f)
            .WithEase(Ease.Linear)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithLoops(-1, LoopType.Incremental)
            .Bind(timeSec =>
            {
                if (_arrow == null) return;
                var sc = _arrow.localScale;
                sc.y = baseScale + _growSpeedPerSec * timeSec;
                _arrow.localScale = sc;
            })
            .AddTo(gameObject);
    }

    /// <summary>
    /// アイコン画像のサイズに基づいて、矢印画像（_arrow）のサイズを初期化します。
    /// Inspectorの <see cref="_arrowSizeRatioToIcon"/> を用いて、
    /// newSize = icon.rect.size * ratio として <see cref="RectTransform.sizeDelta"/> に反映します。
    /// スケールは変更しません（成長アニメはスケールで行うため）。
    /// </summary>
    /// <param name="iconOverride">ここに渡した場合は _iconRect を上書きして使用します。</param>
    public void InitializeArrowByIcon(RectTransform iconOverride = null)
    {
        if (iconOverride != null)
        {
            _iconRect = iconOverride;
        }

        if (_arrow == null || _iconRect == null)
        {
            Debug.LogWarning("[ArrowGrowAndVanish] InitializeArrowByIcon: Missing _arrow or _iconRect.");
            return;
        }

        // アイコンのローカル矩形サイズを参照（親のスケールは反映しない）。
        // 必要であれば将来的に見た目サイズ（ワールド）対応へ拡張可能。
        Vector2 iconSize = _iconRect.rect.size;

        float w = Mathf.Max(0f, _arrowSizeRatioToIcon.x) * iconSize.x;
        float h = Mathf.Max(0f, _arrowSizeRatioToIcon.y) * iconSize.y;

        // sizeDeltaに反映（anchorは(0.5,0)前提だが、いずれにせよsizeDeltaでOK）
        _arrow.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        _arrow.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
    }

    /// <summary>
    /// 実行時にパラメータを上書きして再生。
    /// </summary>
    public UniTask Play(
        float? startYScale = null,
        float? growSpeedPerSec = null,
        float? vanishDuration = null,
        float? vanishSpeedXPerSec = null,
        CancellationToken external = default)
    {
        if (startYScale.HasValue) _startYScale = Mathf.Max(0f, startYScale.Value);
        if (growSpeedPerSec.HasValue) _growSpeedPerSec = Mathf.Max(0f, growSpeedPerSec.Value);
        if (vanishDuration.HasValue) _vanishDuration = vanishDuration.Value;
        if (vanishSpeedXPerSec.HasValue) _vanishSpeedXPerSec = Mathf.Max(0f, vanishSpeedXPerSec.Value);
        return PlayOnceAsync(external);
    }
}
