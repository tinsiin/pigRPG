using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using LitMotion;
using LitMotion.Extensions;

[DisallowMultipleComponent]
public class UIVerticalBob : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("上下振幅（ピクセル）。CanvasScaler使用時はscaleFactorで割って内部単位に変換")]
    [SerializeField] private float amplitudePixels = 2f;

    [Tooltip("サイン波の周波数（毎秒の往復テンポ）")]
    [SerializeField] private float frequency = 2.0f;

    [Tooltip("全体の速度スケール")]
    [SerializeField] private float speed = 1.0f;

    [Tooltip("開始位相（0~2π）。個体差を出すならランダムに設定")]
    [SerializeField] private float phase = 0f;

    [Header("Time")]
    [SerializeField] private bool useUnscaledTime = true;
    
    [Header("Enable")]
    [Tooltip("縦揺れの有効/無効。デフォルトは無効")]
    [FormerlySerializedAs("m_Enabled")]
    [SerializeField] private bool bobEnabled = false;
    
    /// <summary>
    /// 縦揺れの有効/無効（プロパティ）
    /// </summary>
    public bool Enabled
    {
        get => bobEnabled;
        set
        {
            bobEnabled = value;
            if (bobEnabled)
            {
                StartBobTween();
            }
            else
            {
                StopBobTween(resetPosition: true);
            }
        }
    }

    RectTransform rt;
    Vector2 baseAnchoredPos;
    float scaleFactorInv = 1f;
    MotionHandle bobHandle;
    bool lastUseUnscaledTime;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (rt == null) { enabled = false; return; }

        // CanvasのscaleFactorを考慮（ピクセル→UI単位の換算）
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.isRootCanvas)
        {
            var sf = canvas.scaleFactor;
            scaleFactorInv = (sf > 0f) ? (1f / sf) : 1f;
        }
        else
        {
            // 子Canvasや非ルートの場合のフォールバック
            var root = canvas != null ? canvas.rootCanvas : null;
            var sf = (root != null) ? root.scaleFactor : 1f;
            scaleFactorInv = (sf > 0f) ? (1f / sf) : 1f;
        }
    }

    void OnEnable()
    {
        if (rt != null) baseAnchoredPos = rt.anchoredPosition;
        if (bobEnabled)
        {
            StartBobTween();
        }
    }

    void OnDisable()
    {
        StopBobTween(resetPosition: true);
    }

    void Update()
    {
        if (rt == null) return;
        
        // インスペクタからの切替にも追従できるよう、フレーム毎に状態同期
        if (!bobEnabled)
        {
            if (bobHandle.IsActive())
            {
                StopBobTween(resetPosition: true);
            }
            return;
        }

        if (!bobHandle.IsActive())
        {
            StartBobTween();
        }
        else if (lastUseUnscaledTime != useUnscaledTime)
        {
            // 実行中にスケジューラ種別が変わった場合は作り直す
            StartBobTween();
        }
    }

    // 外部から初期化したいとき用の簡易API
    public void SetParams(float ampPixels, float freq, float spd, float ph)
    {
        amplitudePixels = ampPixels;
        frequency = freq;
        speed = spd;
        phase = ph;
    }

    void StartBobTween()
    {
        if (rt == null) return;
        // 既存が生きていたら停止
        if (bobHandle.IsActive())
        {
            bobHandle.Cancel();
        }

        // 基準位置を最新化（外部で位置が変わっている可能性に対応）
        baseAnchoredPos = rt.anchoredPosition;

        var scheduler = useUnscaledTime ? MotionScheduler.UpdateIgnoreTimeScale : MotionScheduler.Update;
        lastUseUnscaledTime = useUnscaledTime;

        // 1秒で1ずつ増える連続時間（累積）を生成し、Bind内で元のサイン波式を再現
        bobHandle = LMotion.Create(0f, 1f, 1f)
            .WithEase(Ease.Linear)
            .WithScheduler(scheduler)
            .WithLoops(-1, LoopType.Incremental)
            .Bind(timeSec =>
            {
                if (rt == null) return;
                float ampUnits = amplitudePixels * scaleFactorInv; // ピクセル→UI単位
                float omega = speed * frequency;                   // 角速度（rad/s）
                float y = Mathf.Sin(timeSec * omega + phase) * ampUnits;
                rt.anchoredPosition = baseAnchoredPos + new Vector2(0f, y);
            })
            .AddTo(gameObject);
    }

    void StopBobTween(bool resetPosition)
    {
        if (bobHandle.IsActive())
        {
            bobHandle.Cancel();
        }
        if (resetPosition && rt != null)
        {
            rt.anchoredPosition = baseAnchoredPos;
        }
    }
}