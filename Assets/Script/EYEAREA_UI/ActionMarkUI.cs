using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using LitMotion;
using LitMotion.Extensions;

/// <summary>
/// 2つの色の間を一定間隔で変化し続ける四角形のUIコンポーネント。
/// 縦横のサイズと変化スピードを指定できる。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class ActionMarkUI : MaskableGraphic
{
    [Header("サイズ設定")]
    [SerializeField] float m_Width = 100f;          // 四角形の幅
    [SerializeField] float m_Height = 100f;         // 四角形の高さ
    
    [Header("色変化設定")]
    [SerializeField] Color m_ColorA = Color.magenta;    // 変化色A ステージテーマ色とこの色を行き来する。
    Color m_StageThemeColor = Color.magenta;//ステージテーマ色　固定用
    [SerializeField] float m_ChangeSpeed = 1f;      // 変化スピード（1秒で1サイクル）
    
    [Header("アニメーション設定")]
    [SerializeField] bool m_AutoStart = true;       // 自動開始
    [SerializeField] AnimationCurve m_InterpolationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 補間カーブ
    
    [Header("移動/サイズ追従設定")]
    [SerializeField] bool m_EnableMoveAnimation = true; // マークの移動アニメーション有効
    [SerializeField] float m_MoveDuration = 0.35f;      // 移動/サイズ補間の所要時間
    [SerializeField] AnimationCurve m_MoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 移動/サイズの補間カーブ
    [SerializeField] float m_SizeMultiplier = 1.16f;    // 対象アイコンサイズに対する倍率
    
    private float m_Timer = 0f; // 色アニメの位相オフセット[0,1)
    private bool m_IsAnimating = false;
    private MotionHandle m_MoveHandle;
    private MotionHandle m_ColorHandle;
    private float m_ColorAccum = 0f; // LitMotion開始以降の累積値（秒換算1/s）
    
    #region Public API
    /// <summary>
    /// 四角形の幅
    /// </summary>
    public float Width
    {
        get => m_Width;
        set 
        { 
            m_Width = Mathf.Max(0f, value); 
            UpdateRectTransformSize();
            SetVerticesDirty(); 
        }
    }
    
    /// <summary>
    /// 四角形の高さ
    /// </summary>
    public float Height
    {
        get => m_Height;
        set 
        { 
            m_Height = Mathf.Max(0f, value); 
            UpdateRectTransformSize();
            SetVerticesDirty(); 
        }
    }
    
    /// <summary>
    /// 変化色A
    /// </summary>
    public Color ColorA
    {
        get => m_ColorA;
        set { m_ColorA = value; }
    }
    
    /// <summary>
    /// 変化色B
    /// </summary>
    public Color ColorB
    {
        get => m_StageThemeColor;
        set { m_StageThemeColor = value; }
    }
    
    /// <summary>
    /// 変化スピード（1秒で1サイクル）
    /// </summary>
    public float ChangeSpeed
    {
        get => m_ChangeSpeed;
        set { m_ChangeSpeed = Mathf.Max(0.01f, value); }
    }
    
    /// <summary>
    /// サイズを設定
    /// </summary>
    public void SetSize(float width, float height)
    {
        m_Width = Mathf.Max(0f, width);
        m_Height = Mathf.Max(0f, height);
        UpdateRectTransformSize();
        SetVerticesDirty();
    }
    
    /// <summary>
    /// ステージテーマ色を保持
    /// </summary>
    public void SetStageThemeColor(Color color)
    {
        m_StageThemeColor = color;
    }
    
    /// <summary>
    /// アニメーションを開始
    /// </summary>
    public void StartAnimation()
    {
        m_IsAnimating = true;
        StartColorTweenIfNeeded();
    }
    
    /// <summary>
    /// アニメーションを停止
    /// </summary>
    public void StopAnimation()
    {
        m_IsAnimating = false;
        StopColorTween();
    }
    
    /// <summary>
    /// アニメーションの再生/停止を切り替え
    /// </summary>
    public void ToggleAnimation()
    {
        m_IsAnimating = !m_IsAnimating;
        if (m_IsAnimating) StartColorTweenIfNeeded(); else StopColorTween();
    }
    
    /// <summary>
    /// タイマーをリセット
    /// </summary>
    public void ResetTimer()
    {
        m_Timer = 0f;
    }

    /// <summary>
    /// 指定したRectTransformの中心へ移動し、サイズも倍率に応じて追従する。
    /// immediateがtrueの場合は即座にスナップ。
    /// </summary>
    /// <param name="target">対象のRectTransform（アイコンなど）</param>
    /// <param name="immediate">即時反映するか</param>
    public void MoveToTarget(RectTransform target, bool immediate = false)
    {
        if (target == null)
        {
            if (m_MoveHandle.IsActive()) { m_MoveHandle.Cancel(); }
            return;
        }

        EnsureCenteredPivotAndAnchors();

        // 目標位置（マークの親のローカル座標に変換された中心点）
        var parentRT = rectTransform.parent as RectTransform;
        if (parentRT == null)
        {
            // 親がRectTransformでない場合はローカル変換でフォールバック
            Vector2 fallbackLocal = rectTransform.InverseTransformPoint(target.TransformPoint(target.rect.center));
            ApplyMoveAndSize(fallbackLocal, target.rect.size * m_SizeMultiplier, immediate);
            return;
        }

        Vector3 targetWorldCenter = target.TransformPoint(target.rect.center);
        Vector2 targetLocalCenter = parentRT.InverseTransformPoint(targetWorldCenter);
        Vector2 targetSize = target.rect.size * m_SizeMultiplier;

        ApplyMoveAndSize(targetLocalCenter, targetSize, immediate);
    }

    /// <summary>
    /// 指定したRectTransformの中心へ移動し、サイズも倍率に応じて追従する。
    /// extraScaleでターゲットの見かけ上のスケール差を補正する。
    /// immediateがtrueの場合は即座にスナップ。
    /// </summary>
    /// <param name="target">対象のRectTransform（アイコンなど）</param>
    /// <param name="extraScale">親階層ズーム差などを補う追加スケール（target相対）</param>
    /// <param name="immediate">即時反映するか</param>
    public void MoveToTargetWithScale(RectTransform target, Vector2 extraScale, bool immediate = false)
    {
        if (target == null)
        {
            if (m_MoveHandle.IsActive()) { m_MoveHandle.Cancel(); }
            return;
        }

        EnsureCenteredPivotAndAnchors();

        var parentRT = rectTransform.parent as RectTransform;
        if (parentRT == null)
        {
            Vector2 fallbackLocal = rectTransform.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Vector2 size = Vector2.Scale(target.rect.size * m_SizeMultiplier, extraScale);
            ApplyMoveAndSize(fallbackLocal, size, immediate);
            return;
        }

        Vector3 targetWorldCenter = target.TransformPoint(target.rect.center);
        Vector2 targetLocalCenter = parentRT.InverseTransformPoint(targetWorldCenter);
        Vector2 targetSize = Vector2.Scale(target.rect.size * m_SizeMultiplier, extraScale);

        ApplyMoveAndSize(targetLocalCenter, targetSize, immediate);
    }
    #endregion
    
    protected override void Start()
    {
        // base.Start() は必須ではないため呼び出しません（Graphic/MaskableGraphic 側での初期化に依存しない）
        EnsureCenteredPivotAndAnchors();
        UpdateRectTransformSize();
        if (m_AutoStart)
        {
            StartAnimation();
        }
        // エディタ起動直後（プレイ前）は依存先が未初期化の可能性があるためガード
        if (Application.isPlaying)
        {
            var walking = Walking.Instance;
            if (walking != null && walking.NowStageData != null && walking.NowStageData.StageThemeColorUI != null)
            {
                SetStageThemeColor(walking.NowStageData.StageThemeColorUI.ActionMarkColor); // ゲーム起動時のステージの色を保持
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (m_IsAnimating)
        {
            StartColorTweenIfNeeded();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (m_MoveHandle.IsActive())
        {
            m_MoveHandle.Cancel();
        }
        StopColorTween();
    }
    
    #if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        m_Width = Mathf.Max(0f, m_Width);
        m_Height = Mathf.Max(0f, m_Height);
        m_ChangeSpeed = Mathf.Max(0.01f, m_ChangeSpeed);
        m_MoveDuration = Mathf.Max(0.0f, m_MoveDuration);
        m_SizeMultiplier = Mathf.Max(0.0f, m_SizeMultiplier);
        EnsureCenteredPivotAndAnchors();
        UpdateRectTransformSize();
        SetVerticesDirty();
    }
#endif    
    private void Update()
    {
        if (m_IsAnimating)
        {
            if (!m_ColorHandle.IsActive())
            {
                StartColorTweenIfNeeded();
            }
        }
        else
        {
            if (m_ColorHandle.IsActive())
            {
                StopColorTween();
            }
        }
    }

    private void StartColorTweenIfNeeded()
    {
        if (m_ColorHandle.IsActive()) return;
        // 1秒で1ずつ増加する時間を無限ループで供給し、元のロジックをBind内で再現
        m_ColorHandle = LMotion.Create(0f, 1f, 1f)
            .WithEase(Ease.Linear)
            .WithScheduler(MotionScheduler.Update)
            .WithLoops(-1, LoopType.Incremental)
            .Bind(timeSec =>
            {
                // 元の実装: m_Timer += dt * m_ChangeSpeed; normalized = m_Timer % 1
                // ここでは timeSec が開始からの累積（1秒で+1）なので、m_Timer を位相として加算
                m_ColorAccum = timeSec;
                float normalizedTime = (m_Timer + timeSec * m_ChangeSpeed) % 1f;
                float pingPongTime = normalizedTime <= 0.5f ? normalizedTime * 2f : (1f - normalizedTime) * 2f;
                float curveValue = m_InterpolationCurve != null ? m_InterpolationCurve.Evaluate(pingPongTime) : pingPongTime;
                Color currentColor = Color.Lerp(m_ColorA, m_StageThemeColor, curveValue);
                if (color != currentColor)
                {
                    color = currentColor;
                    SetVerticesDirty();
                }
            })
            .AddTo(gameObject);
    }

    private void StopColorTween()
    {
        if (m_ColorHandle.IsActive())
        {
            // 現在の累積を位相オフセットに畳み込み、再開時に続きから始める
            m_Timer = (m_Timer + m_ColorAccum * m_ChangeSpeed) % 1f;
            m_ColorHandle.Cancel();
        }
    }
    
    /// <summary>
    /// RectTransformのサイズを更新
    /// </summary>
    private void UpdateRectTransformSize()
    {
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(m_Width, m_Height);
        }
    }

    /// <summary>
    /// アンカー/ピボットを中央に固定（座標計算の安定化）
    /// </summary>
    private void EnsureCenteredPivotAndAnchors()
    {
        if (rectTransform == null) return;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// 目標座標・サイズへ移動/補間する内部処理
    /// </summary>
    private void ApplyMoveAndSize(Vector2 targetLocalPos, Vector2 targetSize, bool immediate)
    {
        if (m_MoveHandle.IsActive())
        {
            m_MoveHandle.Cancel();
        }

        if (immediate || !m_EnableMoveAnimation || m_MoveDuration <= 0f)
        {
            rectTransform.anchoredPosition = targetLocalPos;
            SetSize(targetSize.x, targetSize.y);
            return;
        }

        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 startSize = new Vector2(m_Width, m_Height);

        m_MoveHandle = LMotion.Create(0f, 1f, m_MoveDuration)
            .WithEase(Ease.Linear)
            .WithScheduler(MotionScheduler.Update)
            .Bind(u =>
            {
                float e = (m_MoveCurve != null) ? m_MoveCurve.Evaluate(u) : u;
                var pos = Vector2.LerpUnclamped(startPos, targetLocalPos, e);
                var size = Vector2.LerpUnclamped(startSize, targetSize, e);
                rectTransform.anchoredPosition = pos;
                SetSize(size.x, size.y);
            })
            .AddTo(gameObject);
    }

    
    
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        
        // 四角形の頂点を計算
        float halfWidth = m_Width * 0.5f;
        float halfHeight = m_Height * 0.5f;
        
        // 四角形を描画
        AddQuad(vh,
            new Vector2(-halfWidth, -halfHeight),  // 左下
            new Vector2(halfWidth, -halfHeight),   // 右下
            new Vector2(halfWidth, halfHeight),    // 右上
            new Vector2(-halfWidth, halfHeight),   // 左上
            color);
    }
    
    /// <summary>
    /// 四角形を頂点ヘルパーに追加
    /// </summary>
    private void AddQuad(VertexHelper vh, Vector2 bottomLeft, Vector2 bottomRight, Vector2 topRight, Vector2 topLeft, Color quadColor)
    {
        int startIndex = vh.currentVertCount;
        
        // 頂点を追加
        vh.AddVert(new UIVertex
        {
            position = bottomLeft,
            color = quadColor,
            uv0 = new Vector2(0, 0)
        });
        vh.AddVert(new UIVertex
        {
            position = bottomRight,
            color = quadColor,
            uv0 = new Vector2(1, 0)
        });
        vh.AddVert(new UIVertex
        {
            position = topRight,
            color = quadColor,
            uv0 = new Vector2(1, 1)
        });
        vh.AddVert(new UIVertex
        {
            position = topLeft,
            color = quadColor,
            uv0 = new Vector2(0, 1)
        });
        
        // 三角形を追加（時計回り）
        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }
}