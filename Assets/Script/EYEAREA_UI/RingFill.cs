using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// “時計の12時”から時計回り／反時計回りに fill していくリング UI。
/// Sprite を使わず自前で頂点を生成するので太さ・穴の大きさを自由に変えられる。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class RingFill : MaskableGraphic
{
    [Range(0f, 1f)]
    [SerializeField] float m_FillPercent = 1f;   // 1=100%
    [SerializeField] float m_InnerRadius = 40f;  // 穴の半径
    [SerializeField] float m_Thickness   = 10f;  // リングの太さ
    [SerializeField] bool  m_Clockwise   = true;

    const int SEGMENTS = 100; // メッシュ分割数（多いほど滑らか）

    #region public API
    public float FillPercent
    {
        get => m_FillPercent;
        set { m_FillPercent = Mathf.Clamp01(value); SetVerticesDirty(); }
    }
    public void SetRatio(float ratio) => FillPercent = ratio;
    #endregion

    #if UNITY_EDITOR
    // Inspector 変更時に RectTransform サイズを更新しておく
    protected override void OnValidate()
    {
        base.OnValidate();
        float size = (m_InnerRadius + m_Thickness) * 2f;
        rectTransform.sizeDelta = new Vector2(size, size);
        SetVerticesDirty();
    }
    #endif

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float outer = m_InnerRadius + m_Thickness;
        int   steps = Mathf.CeilToInt(SEGMENTS * m_FillPercent) + 1;
        float angStep = (2f * Mathf.PI * m_FillPercent) / (steps - 1);
        if (!m_Clockwise) angStep = -angStep;

        float startRad = Mathf.PI / 2f; // 12 時の方向

        for (int i = 0; i < steps; ++i)
        {
            float a = startRad - i * angStep;
            float cos = Mathf.Cos(a);
            float sin = Mathf.Sin(a);

            Vector2 inner = new Vector2(cos * m_InnerRadius, sin * m_InnerRadius);
            Vector2 outerV= new Vector2(cos * outer,         sin * outer);

            int idx = vh.currentVertCount;
            vh.AddVert(inner,  color, Vector2.zero); // 0: 内側
            vh.AddVert(outerV, color, Vector2.zero); // 1: 外側

            if (i > 0)
            {
                // 直前の 2+2 と今回の 2 で Quad → 三角 2 枚
                vh.AddTriangle(idx - 2, idx - 1, idx);
                vh.AddTriangle(idx,     idx - 1, idx + 1);
            }
        }
    }
}