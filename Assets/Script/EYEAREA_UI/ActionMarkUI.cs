using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] Color m_ColorA = Color.red;    // 変化色A
    [SerializeField] Color m_ColorB = Color.blue;   // 変化色B
    [SerializeField] float m_ChangeSpeed = 1f;      // 変化スピード（1秒で1サイクル）
    
    [Header("アニメーション設定")]
    [SerializeField] bool m_AutoStart = true;       // 自動開始
    [SerializeField] AnimationCurve m_InterpolationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 補間カーブ
    
    private float m_Timer = 0f;
    private bool m_IsAnimating = false;
    
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
        get => m_ColorB;
        set { m_ColorB = value; }
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
    /// 色を設定
    /// </summary>
    public void SetColors(Color colorA, Color colorB)
    {
        m_ColorA = colorA;
        m_ColorB = colorB;
    }
    
    /// <summary>
    /// アニメーションを開始
    /// </summary>
    public void StartAnimation()
    {
        m_IsAnimating = true;
    }
    
    /// <summary>
    /// アニメーションを停止
    /// </summary>
    public void StopAnimation()
    {
        m_IsAnimating = false;
    }
    
    /// <summary>
    /// アニメーションの再生/停止を切り替え
    /// </summary>
    public void ToggleAnimation()
    {
        m_IsAnimating = !m_IsAnimating;
    }
    
    /// <summary>
    /// タイマーをリセット
    /// </summary>
    public void ResetTimer()
    {
        m_Timer = 0f;
    }
    #endregion
    
    protected override void Start()
    {
        base.Start();
        if (m_AutoStart)
        {
            StartAnimation();
        }
    }
    
    protected override void OnValidate()
    {
        base.OnValidate();
        m_Width = Mathf.Max(0f, m_Width);
        m_Height = Mathf.Max(0f, m_Height);
        m_ChangeSpeed = Mathf.Max(0.01f, m_ChangeSpeed);
        UpdateRectTransformSize();
        SetVerticesDirty();
    }
    
    private void Update()
    {
        if (m_IsAnimating)
        {
            // タイマーを更新
            m_Timer += Time.deltaTime * m_ChangeSpeed;
            
            // 0-1の範囲でループ
            float normalizedTime = (m_Timer % 1f);
            
            // 0-1-0のパターンにするため、0.5で反転
            float pingPongTime = normalizedTime <= 0.5f ? normalizedTime * 2f : (1f - normalizedTime) * 2f;
            
            // アニメーションカーブを適用
            float curveValue = m_InterpolationCurve.Evaluate(pingPongTime);
            
            // 色を補間
            Color currentColor = Color.Lerp(m_ColorA, m_ColorB, curveValue);
            
            // 色が変わった場合のみ再描画
            if (color != currentColor)
            {
                color = currentColor;
                SetVerticesDirty();
            }
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