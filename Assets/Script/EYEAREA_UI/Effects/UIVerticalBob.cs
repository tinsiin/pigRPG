using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

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
            if (!bobEnabled && rt != null)
            {
                // 無効化時は基準位置に戻す
                rt.anchoredPosition = baseAnchoredPos;
            }
        }
    }

    RectTransform rt;
    Vector2 baseAnchoredPos;
    float scaleFactorInv = 1f;

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
    }

    void OnDisable()
    {
        if (rt != null) rt.anchoredPosition = baseAnchoredPos;
    }

    void Update()
    {
        if (rt == null) return;
        
        // 無効時は常に基準位置に固定
        if (!bobEnabled)
        {
            if (rt.anchoredPosition != baseAnchoredPos)
                rt.anchoredPosition = baseAnchoredPos;
            return;
        }

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float t = time * speed;
        // ピクセル指定をUI単位に換算
        float ampUnits = amplitudePixels * scaleFactorInv;
        float y = Mathf.Sin(t * frequency + phase) * ampUnits;

        rt.anchoredPosition = baseAnchoredPos + new Vector2(0f, y);
    }

    // 外部から初期化したいとき用の簡易API
    public void SetParams(float ampPixels, float freq, float spd, float ph)
    {
        amplitudePixels = ampPixels;
        frequency = freq;
        speed = spd;
        phase = ph;
    }
}