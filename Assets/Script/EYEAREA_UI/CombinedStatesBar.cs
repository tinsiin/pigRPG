using UnityEngine;
using UnityEngine.UI;
using LitMotion;

/// <summary>
/// HPバー（上段）と精神HPバー（下段）を統合したクラス。
/// 同じ長さ・太さで縦に並べて表示し、縦余白を調整可能。
/// 両方のバーでアニメーション機能と即時スナップ→再アニメーション機能を提供。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class CombinedStatesBar : MaskableGraphic
{
    [Header("共通設定")]
    [SerializeField] float m_Width = 200f;                  // バーの幅
    [SerializeField] float m_Height = 20f;                  // 各バーの高さ（太さ）
    [SerializeField] float m_VerticalSpacing = 5f;          // 縦余白（HPバーと精神HPバーの間）
    
    [Header("HPバー設定（上段）")]
    [SerializeField, Range(0f, 1f)] float m_HPPercent = 1f; // HP割合 (0.0～1.0)
    [SerializeField] Color m_HPBarColor = Color.white;      // HPバーの色
    [SerializeField] bool m_ShowHPBackground = true;        // HP背景を表示するか
    [SerializeField] Color m_HPBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); // HP背景色
    
    [Header("精神HPバー設定（下段）")]
    [SerializeField, Range(0f, 3f)] float m_MentalPercent = 1f; // 精神HP割合 (0.0～3.0)
    [SerializeField] Color m_MentalBarColor = Color.blue;   // 精神HPバーの色
    [SerializeField] float m_MentalThreshold = 1f;          // 乖離しきい値
    [SerializeField] float m_ThresholdLineWidth = 2f;       // しきい値縦線の太さ
    [SerializeField] Color m_ThresholdLineColor = Color.white; // しきい値縦線の色
    [SerializeField] bool m_ShowMentalBackground = false;   // 精神HP背景を表示するか（デフォルト透明）
    [SerializeField] Color m_MentalBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); // 精神HP背景色
    
    [Header("Divergence Indicator Settings")]
    [SerializeField, Range(0f, 5f)] private float m_DivergenceMultiplier = 1f;
    [SerializeField] private Color m_DivergenceLineColor = Color.black;
    [SerializeField, Range(0.5f, 10f)] private float m_DivergenceLineThickness = 2f;
    [SerializeField, Range(0.1f, 5f)] private float m_DivergenceAnimDuration = 0.5f;
    [SerializeField, Range(0.1f, 10f)] private float m_DivergenceAnimSpeed = 2f;
    [SerializeField, Range(0.1f, 2f)] private float m_MentalColorLerpSpeed = 0.5f;
    
    [Header("アニメーション設定")]
    [SerializeField] bool m_EnableAnimation = true;         // アニメーションを有効にするか
    [SerializeField] float m_AnimationDuration = 0.5f;      // アニメーション時間（秒）
    [SerializeField] float m_AnimationSpeed = 2f;           // 1フレームあたりの変化速度倍率
    
    [Header("デバッグ情報（読み取り専用）")]
    [SerializeField] private float debug_DisplayHPPercent;     // 実際の表示用HP割合
    [SerializeField] private float debug_TargetHPPercent;      // 目標HP割合
    [SerializeField] private float debug_DisplayMentalRatio;   // 実際の表示用精神HP比率
    [SerializeField] private float debug_TargetMentalRatio;    // 目標精神HP比率
    [SerializeField] private float debug_DisplayDivergenceMultiplier; // 実際の表示用乖離倍率
    [SerializeField] private float debug_TargetDivergenceMultiplier;  // 目標乖離倍率
    [SerializeField] private bool debug_IsHPAnimating;         // HPアニメ中かどうか
    [SerializeField] private bool debug_IsMentalAnimating;     // 精神HPアニメ中かどうか
    [SerializeField] private bool debug_IsDivergenceAnimating; // 乖離アニメ中かどうか
    
    // アニメーション用の内部変数
    private float m_DisplayHPPercent;                       // HP表示用の割合
    private float m_TargetHPPercent;                        // HP目標割合
    private MotionHandle m_HPHandle;                        // HPアニメーションハンドル
    
    private float m_DisplayMentalRatio;                     // 精神HP表示用の比率
    private float m_TargetMentalRatio;                      // 精神HP目標比率
    private MotionHandle m_MentalHandle;                    // 精神HPアニメーションハンドル
    
    private float m_DisplayDivergenceMultiplier;
    private float m_TargetDivergenceMultiplier;
    private MotionHandle m_DivergenceHandle;                // 乖離アニメーションハンドル

    // 実行中のインスペクタ変更検知用
    private float _prevWidth;
    private float _prevHeight;
    private float _prevVerticalSpacing;
    private float _prevHPPercent;
    private float _prevMentalPercent;
    private float _prevDivergenceMultiplier;
    
    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        // 表示用変数を初期値で初期化
        m_DisplayHPPercent = m_HPPercent;
        m_TargetHPPercent = m_HPPercent;
        m_DisplayMentalRatio = m_MentalPercent;
        m_TargetMentalRatio = m_MentalPercent;
        m_DisplayDivergenceMultiplier = m_DivergenceMultiplier;
        m_TargetDivergenceMultiplier = m_DivergenceMultiplier;
        
        CacheCurrentValues();
        UpdateRectTransformSize();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;

        bool sizeChanged = !Mathf.Approximately(_prevWidth, m_Width) ||
                           !Mathf.Approximately(_prevHeight, m_Height) ||
                           !Mathf.Approximately(_prevVerticalSpacing, m_VerticalSpacing);

        bool barParamChanged = !Mathf.Approximately(_prevHPPercent, m_HPPercent) ||
                               !Mathf.Approximately(_prevMentalPercent, m_MentalPercent) ||
                               !Mathf.Approximately(_prevDivergenceMultiplier, m_DivergenceMultiplier);

        if (sizeChanged)
        {
            UpdateRectTransformSize();
        }

        // ランタイム中は、値変更時に再度セッターを呼ぶとスナップしてしまうため呼ばない。
        // サイズ変更時のみメッシュを更新し、バー値の変更は各セッター発のアニメーションに任せる。
        if (sizeChanged)
        {
            SafeSetVerticesDirty();
        }

        // 変更検知の基準を更新（毎フレーム実施でOK）
        CacheCurrentValues();
        
        // デバッグ情報を更新
        UpdateDebugInfo();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // オブジェクトが無効化/破棄されるときに全アニメーションを停止
        CancelHPAnimation();
        CancelMentalAnimation();
        CancelDivergenceAnimation();
    }

    private void CacheCurrentValues()
    {
        _prevWidth = m_Width;
        _prevHeight = m_Height;
        _prevVerticalSpacing = m_VerticalSpacing;
        _prevHPPercent = m_HPPercent;
        _prevMentalPercent = m_MentalPercent;
        _prevDivergenceMultiplier = m_DivergenceMultiplier;
    }
    
    private void UpdateDebugInfo()
    {
        debug_DisplayHPPercent = m_DisplayHPPercent;
        debug_TargetHPPercent = m_TargetHPPercent;
        debug_DisplayMentalRatio = m_DisplayMentalRatio;
        debug_TargetMentalRatio = m_TargetMentalRatio;
        debug_DisplayDivergenceMultiplier = m_DisplayDivergenceMultiplier;
        debug_TargetDivergenceMultiplier = m_TargetDivergenceMultiplier;
        // 一部のLitMotionバージョンでは IsPlaying() が存在しないため IsActive() のみを使用
        debug_IsHPAnimating = m_HPHandle.IsActive();
        debug_IsMentalAnimating = m_MentalHandle.IsActive();
        debug_IsDivergenceAnimating = m_DivergenceHandle.IsActive();
    }

    /// <summary>
    /// RectTransform の sizeDelta をバーサイズに合わせて更新
    /// </summary>
    private void UpdateRectTransformSize()
    {
        if (TryGetComponent<RectTransform>(out var rt))
        {
            float totalHeight = m_Height * 2f + m_VerticalSpacing;
            rt.sizeDelta = new Vector2(m_Width, totalHeight);
        }
    }

    #endregion

#region Public API
    /// <summary>HPと精神HPを一括で更新</summary>
    public void UpdateBothBars(float currentHP, float maxHP, float mentalHP, float threshold)
    {
        UpdateBothBars(currentHP, maxHP, mentalHP, threshold, m_DivergenceMultiplier);
    }
    
    /// <summary>HPと精神HPを一括で更新</summary>
    public void UpdateBothBars(float currentHP, float maxHP, float mentalHP, float threshold, float divergenceMultiplier)
    {
        if (maxHP <= 0) return;
        
        float hpPercent = Mathf.Clamp01(currentHP / maxHP);
        float mentalRatio = maxHP <= 0f ? 0f : mentalHP / maxHP;
        
        HPPercent = hpPercent;
        MentalRatio = mentalRatio;
        DivergenceMultiplier = divergenceMultiplier;
        
        SetVerticesDirty();
    }
    
    /// <summary>HP割合を設定（0.0～1.0）</summary>
    public float HPPercent
    {
        get => m_HPPercent;
        set { SetHPPercent(Mathf.Clamp01(value)); }
    }
    
    /// <summary>精神HP比率を設定</summary>
    public float MentalRatio
    {
        get => m_MentalPercent;
        set { SetMentalRatio(Mathf.Max(0f, value)); }
    }
    
    /// <summary>精神HP乖離しきい値を設定</summary>
    public float MentalThreshold
    {
        get => m_MentalThreshold;
        set { m_MentalThreshold = Mathf.Max(0f, value); SetVerticesDirty(); }
    }
    
    /// <summary>HPバーの色を設定</summary>
    public Color HPBarColor
    {
        get => m_HPBarColor;
        set { m_HPBarColor = value; SetVerticesDirty(); }
    }
    
    /// <summary>精神HPバーの色を設定</summary>
    public Color MentalBarColor
    {
        get => m_MentalBarColor;
        set { m_MentalBarColor = value; SetVerticesDirty(); }
    }
    
    /// <summary>バーのサイズを設定</summary>
    public void SetSize(float width, float height)
    {
        m_Width = width;
        m_Height = height;
        UpdateRectTransformSize();
        SetVerticesDirty();
    }
    
    /// <summary>縦余白を設定</summary>
    public float VerticalSpacing
    {
        get => m_VerticalSpacing;
        set { m_VerticalSpacing = value; UpdateRectTransformSize(); SetVerticesDirty(); }
    }
    
    /// <summary>アニメーション設定</summary>
    public bool EnableAnimation
    {
        get => m_EnableAnimation;
        set => m_EnableAnimation = value;
    }
    
    public float AnimationDuration
    {
        get => m_AnimationDuration;
        set => m_AnimationDuration = Mathf.Max(0.01f, value);
    }
    
    public float AnimationSpeed
    {
        get => m_AnimationSpeed;
        set => m_AnimationSpeed = Mathf.Max(0.1f, value);
    }
    
    /// <summary>精神HP乖離倍率を設定</summary>
    public float DivergenceMultiplier
    {
        get => m_DivergenceMultiplier;
        set => SetDivergenceMultiplier(value);
    }
    
    /// <summary>乖離指標線の色を設定</summary>
    public Color DivergenceLineColor
    {
        get => m_DivergenceLineColor;
        set
        {
            m_DivergenceLineColor = value;
            SetVerticesDirty();
        }
    }
    
    /// <summary>乖離指標線の太さを設定</summary>
    public float DivergenceLineThickness
    {
        get => m_DivergenceLineThickness;
        set
        {
            m_DivergenceLineThickness = Mathf.Clamp(value, 0.5f, 10f);
            SetVerticesDirty();
        }
    }
    
    /// <summary>精神HPバーの色変化速度を設定</summary>
    public float MentalColorLerpSpeed
    {
        get => m_MentalColorLerpSpeed;
        set => m_MentalColorLerpSpeed = Mathf.Clamp(value, 0.1f, 2f);
    }
    
    /// <summary>アニメーションなしで即座に設定</summary>
    public void SetBothBarsImmediate(float hpPercent, float mentalRatio, float divergenceMultiplier)
    {
        SetBothBarsImmediate(hpPercent, mentalRatio, divergenceMultiplier, 0f);
    }
    
    /// <summary>アニメーションなしで即座に設定</summary>
    public void SetBothBarsImmediate(float hpPercent, float mentalRatio, float divergenceMultiplier, float threshold)
    {
        // HP
        m_HPPercent = Mathf.Clamp01(hpPercent);
        m_DisplayHPPercent = m_HPPercent;
        m_TargetHPPercent = m_HPPercent;
        CancelHPAnimation();
        
        // 精神HP
        m_MentalPercent = Mathf.Max(0f, mentalRatio);
        m_DisplayMentalRatio = m_MentalPercent;
        m_TargetMentalRatio = m_MentalPercent;
        CancelMentalAnimation();
        
        // 乖離倍率
        m_DivergenceMultiplier = divergenceMultiplier;
        m_DisplayDivergenceMultiplier = divergenceMultiplier;
        m_TargetDivergenceMultiplier = divergenceMultiplier;
        CancelDivergenceAnimation();
        
        // しきい値
        m_MentalThreshold = Mathf.Max(0f, threshold);
        
        SetVerticesDirty();
    }
    #endregion
    
    #region Safety
    private void SafeSetVerticesDirty()
    {
        // UnityEngine.Object の破棄チェックとアクティブチェック
        if (!this) return;
        if (!isActiveAndEnabled) return;
        SetVerticesDirty();
    }
    #endregion
    #region Private Methods
    /// <summary>HP割合をアニメーション付きで設定</summary>
    private void SetHPPercent(float newPercent)
    {
        // アニメーションが無効の場合は即座に反映
        if (!m_EnableAnimation)
        {
            m_HPPercent = newPercent;
            m_DisplayHPPercent = newPercent;
            m_TargetHPPercent = newPercent;
            SetVerticesDirty();
            return;
        }
        // 進行中なら即時スナップ（存在時のみ完了）
        if (m_HPHandle.IsActive()) m_HPHandle.Complete();
        // 新しい目標値を設定
        m_TargetHPPercent = newPercent;
        m_HPPercent = newPercent;
        // 既存アニメを停止
        CancelHPAnimation();
        // LitMotionでアニメ開始（速度はdurationを1/Speedにスケーリング）
        float duration = Mathf.Max(0.0001f, m_AnimationDuration / Mathf.Max(0.0001f, m_AnimationSpeed));
        m_HPHandle = LMotion.Create(m_DisplayHPPercent, m_TargetHPPercent, duration)
            .WithEase(Ease.Linear)
            .Bind(x => { m_DisplayHPPercent = x; SafeSetVerticesDirty(); });
    }
    
    /// <summary>精神HP比率をアニメーション付きで設定</summary>
    private void SetMentalRatio(float newRatio)
    {
        
        // アニメーションが無効の場合は即座に反映
        if (!m_EnableAnimation)
        {
            m_MentalPercent = newRatio;
            m_DisplayMentalRatio = newRatio;
            m_TargetMentalRatio = newRatio;
            SetVerticesDirty();
            return;
        }
        
        // 進行中なら即時スナップ（存在時のみ完了）
        if (m_MentalHandle.IsActive()) m_MentalHandle.Complete();
        
        // 新しい目標値を設定してアニメーション開始
        m_TargetMentalRatio = newRatio;
        m_MentalPercent = newRatio;
        CancelMentalAnimation();
        // LitMotionでアニメ開始
        float duration = Mathf.Max(0.0001f, m_AnimationDuration / Mathf.Max(0.0001f, m_AnimationSpeed));
        m_MentalHandle = LMotion.Create(m_DisplayMentalRatio, m_TargetMentalRatio, duration)
            .WithEase(Ease.Linear)
            .Bind(x => { m_DisplayMentalRatio = x; SafeSetVerticesDirty(); });
    }
    
    /// <summary>乖離倍率をアニメーション付きで設定</summary>
    private void SetDivergenceMultiplier(float newMultiplier)
    {
        
        if (!m_EnableAnimation)
        {
            m_DivergenceMultiplier = newMultiplier;
            m_DisplayDivergenceMultiplier = newMultiplier;
            m_TargetDivergenceMultiplier = newMultiplier;
            SetVerticesDirty();
            return;
        }
        
        // 進行中なら即時スナップ（存在時のみ完了）
        if (m_DivergenceHandle.IsActive()) m_DivergenceHandle.Complete();
        
        m_TargetDivergenceMultiplier = newMultiplier;
        m_DivergenceMultiplier = newMultiplier;
        // 既存を停止
        CancelDivergenceAnimation();
        // LitMotionでアニメ開始（独自のduration/speed）
        float duration = Mathf.Max(0.0001f, m_DivergenceAnimDuration / Mathf.Max(0.0001f, m_DivergenceAnimSpeed));
        m_DivergenceHandle = LMotion.Create(m_DisplayDivergenceMultiplier, m_TargetDivergenceMultiplier, duration)
            .WithEase(Ease.Linear)
            .Bind(x => { m_DisplayDivergenceMultiplier = x; SafeSetVerticesDirty(); });
    }
    

    
    /// <summary>HPアニメーションをキャンセル</summary>
    private void CancelHPAnimation()
    {
        if (m_HPHandle.IsActive()) m_HPHandle.Cancel();
    }
    
    /// <summary>精神HPアニメーションをキャンセル</summary>
    private void CancelMentalAnimation()
    {
        if (m_MentalHandle.IsActive()) m_MentalHandle.Cancel();
    }
    
    /// <summary>乖離倍率アニメーションをキャンセル</summary>
    private void CancelDivergenceAnimation()
    {
        if (m_DivergenceHandle.IsActive()) m_DivergenceHandle.Cancel();
    }
    
    // 旧UniTaskアニメーションはLitMotionに置き換え済み
    
    /// <summary>精神HPバーの色を評価（青→黄→赤）</summary>
    private Color EvaluateMentalBarColor()
    {
        if (m_DisplayDivergenceMultiplier <= 0f) return m_MentalBarColor;
        
        // HPバー長に対する精神HPバー長の差を基準に色を決める
        // 中央 (精神HP=HP) で ratio=0, 乖離指標到達で1, 超過で1固定
        float diffRatio = Mathf.Abs(m_DisplayMentalRatio - 1f);
        float colorRatio = m_DisplayDivergenceMultiplier > 0f ? diffRatio / m_DisplayDivergenceMultiplier : 0f;
        colorRatio = Mathf.Clamp01(colorRatio);
        
        // Apply color lerp speed
        float lerpedRatio = Mathf.Pow(colorRatio, 1f / m_MentalColorLerpSpeed);
        
        Color targetColor;
        if (lerpedRatio < 0.5f)
        {
            // Blue to Yellow
            targetColor = Color.Lerp(Color.blue, Color.yellow, lerpedRatio * 2f);
        }
        else
        {
            // Yellow to Red
            targetColor = Color.Lerp(Color.yellow, Color.red, (lerpedRatio - 0.5f) * 2f);
        }
        
        return targetColor;
    }
    #endregion
    
    #region Unity Lifecycle
    #if UNITY_EDITOR

    protected override void OnValidate()
    {
        base.OnValidate();
        UpdateRectTransformSize();

        if (!Application.isPlaying)
        {
            // プレビュー用の表示値を設定
            m_HPPercent     = Mathf.Clamp01(m_HPPercent);
            m_MentalPercent = Mathf.Clamp(m_MentalPercent, 0f, 3f);
            m_DisplayHPPercent           = m_HPPercent == 0f ? 1f : m_HPPercent;
            m_DisplayMentalRatio         = m_MentalPercent;
            m_DisplayDivergenceMultiplier = m_DivergenceMultiplier;
        }
        SetVerticesDirty();
    }
#endif
    
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float halfWidth = m_Width * 0.5f;
        float halfHeight = m_Height * 0.5f;
        float totalHeight = m_Height * 2 + m_VerticalSpacing;
        float halfTotalHeight = totalHeight * 0.5f;
        
        // HPバー（上段）の位置
        float hpBarCenterY = halfTotalHeight - halfHeight;
        
        // 精神HPバー（下段）の位置
        float mentalBarCenterY = -halfTotalHeight + halfHeight;

        // HPバー背景（上段）
        if (m_ShowHPBackground)
        {
            AddQuad(vh, 
                new Vector2(-halfWidth, hpBarCenterY - halfHeight),
                new Vector2(halfWidth, hpBarCenterY + halfHeight),
                m_HPBackgroundColor);
        }

        // HPバー（上段）- 左から右に伸びる
        float displayHPPercent = m_DisplayHPPercent > 0f ? m_DisplayHPPercent : m_HPPercent;
        if (displayHPPercent > 0f)
        {
            float hpBarWidth = m_Width * displayHPPercent;
            
            AddQuad(vh,
                new Vector2(-halfWidth, hpBarCenterY - halfHeight),
                new Vector2(-halfWidth + hpBarWidth, hpBarCenterY + halfHeight),
                m_HPBarColor);
        }

        // 精神HPバー背景（下段）
        if (m_ShowMentalBackground)
        {
            AddQuad(vh,
                new Vector2(-halfWidth, mentalBarCenterY - halfHeight),
                new Vector2(halfWidth, mentalBarCenterY + halfHeight),
                m_MentalBackgroundColor);
        }

        // 精神HPバー（下段）- 左から右に伸びる
        float displayMentalRatio = m_DisplayMentalRatio > 0f ? m_DisplayMentalRatio : m_MentalPercent;
        if (displayMentalRatio > 0f)
        {
            float mentalBarWidth = displayMentalRatio * m_Width;
            
            AddQuad(vh,
                new Vector2(-halfWidth, mentalBarCenterY - halfHeight),
                new Vector2(-halfWidth + mentalBarWidth, mentalBarCenterY + halfHeight),
                EvaluateMentalBarColor());
        }

        // 乖離指標線
        if (m_DisplayDivergenceMultiplier > 0f)
        {
            float hpBarWidth = (displayHPPercent > 0f ? displayHPPercent : 1f) * m_Width;
            float halfLine = m_DivergenceLineThickness * 0.5f;
            
            // 基準点: HPバー右端
            float baseX = -halfWidth + hpBarWidth;
            float offset = hpBarWidth * m_DisplayDivergenceMultiplier;
            
            // 右側の縦線（HPバー右端から右方向）
            float rightLineX = baseX + offset;
            AddQuad(vh,
                new Vector2(rightLineX - halfLine, mentalBarCenterY - halfHeight),
                new Vector2(rightLineX + halfLine, mentalBarCenterY + halfHeight),
                m_DivergenceLineColor);
            
            // 左側の縦線（HPバー右端から左方向）
            float leftLineX = baseX - offset;
            AddQuad(vh,
                new Vector2(leftLineX - halfLine, mentalBarCenterY - halfHeight),
                new Vector2(leftLineX + halfLine, mentalBarCenterY + halfHeight),
                m_DivergenceLineColor);
        }
    }
    #endregion
    


    #region Utility Methods
    private static readonly Vector2[] s_UVs = new Vector2[4]
    {
        new Vector2(0, 0),
        new Vector2(1, 0),
        new Vector2(1, 1),
        new Vector2(0, 1)
    };

    private void AddQuad(VertexHelper vh, Vector2 bottomLeft, Vector2 topRight, Color color)
    {
        int start = vh.currentVertCount;

        vh.AddVert(new Vector3(bottomLeft.x, bottomLeft.y), color, s_UVs[0]); // BL
        vh.AddVert(new Vector3(topRight.x, bottomLeft.y),   color, s_UVs[1]); // BR
        vh.AddVert(new Vector3(topRight.x, topRight.y),     color, s_UVs[2]); // TR
        vh.AddVert(new Vector3(bottomLeft.x, topRight.y),   color, s_UVs[3]); // TL

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
    #endregion
}
