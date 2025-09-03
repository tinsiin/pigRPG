using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ColumnsChart : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField, Min(0f)] private float m_PaddingLR = 8f;
    [SerializeField, Min(0f)] private float m_Spacing = 8f;
    [SerializeField, Min(0f)] private float m_MinColumnWidth = 6f;

    [Header("Colors")]
    [SerializeField] private Color m_NormalColor = Color.white;
    [SerializeField] private Color m_HighlightColor = Color.red;
    [SerializeField] private Color m_DimColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField, Tooltip("ハイライト以外をディムさせる（攻撃グラフ向け）")]
    private bool m_DimNonHighlighted = true;

    [Header("Baseline")]
    [SerializeField, Tooltip("ゼロ位置の下線を表示する")]
    private bool m_ShowBaseline = true;
    [SerializeField] private Color m_BaselineColor = new Color(1f, 1f, 1f, 0.7f);
    [SerializeField, Min(0f), Tooltip("下線の太さ（px）")]
    private float m_BaselineThickness = 2f;

    private RectTransform _rt;
    private readonly List<MaskableGraphic> _cols = new List<MaskableGraphic>();
    private float[] _values = Array.Empty<float>();
    private int _highlightIndex = -1;
    private float _max = 1f;
    private float _userScale = 1f;
    [Header("Baseline Ref")]
    [SerializeField, Tooltip("ここに下線用のGraphicを割り当てると、そのRectTransformを下端に配置・リサイズします")]
    private MaskableGraphic m_BaselineGraphic;

    public void SetColors(Color normal, Color highlight, Color dim, bool dimNonHighlighted)
    {
        m_NormalColor = normal;
        m_HighlightColor = highlight;
        m_DimColor = dim;
        m_DimNonHighlighted = dimNonHighlighted;
        ApplyColors();
    }

    public void SetLayout(float paddingLR, float spacing, float minColumnWidth)
    {
        m_PaddingLR = Mathf.Max(0f, paddingLR);
        m_Spacing = Mathf.Max(0f, spacing);
        m_MinColumnWidth = Mathf.Max(0f, minColumnWidth);
        ApplyLayout();
    }

    public void SetBaseline(Color color, float thickness, bool show = true)
    {
        m_BaselineColor = color;
        m_BaselineThickness = Mathf.Max(0f, thickness);
        m_ShowBaseline = show;
        ApplyLayout();
    }

    public void Clear()
    {
        _values = Array.Empty<float>();
        _highlightIndex = -1;
        _max = 1f;
        for (int i = _cols.Count - 1; i >= 0; i--)
        {
            var c = _cols[i];
            if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
        }
        _cols.Clear();
    }

    public void SetValues(float[] values, int highlightIndex = -1)
    {
        if (values == null) values = Array.Empty<float>();
        _values = values;
        _highlightIndex = (highlightIndex >= 0 && highlightIndex < values.Length) ? highlightIndex : -1;
        _max = Mathf.Max(1f, values.Length > 0 ? values.Max() : 1f);
        EnsureColumns(values.Length);
        ApplyAll();
    }

    public void SetUserScale(float scale)
    {
        _userScale = Mathf.Max(0.01f, scale);
        ApplyLayout();
    }

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        ApplyAll();
    }

    private void OnValidate()
    {
        _rt = GetComponent<RectTransform>();
        // エディタ上でのプレビュー生成は行わない（実行時に適用）
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyLayout();
    }

    private void ApplyAll()
    {
        ApplyColors();
        ApplyLayout();
    }

    private void ApplyColors()
    {
        for (int i = 0; i < _cols.Count; i++)
        {
            var g = _cols[i];
            if (g == null) continue;
            bool isHighlight = (i == _highlightIndex);
            if (isHighlight)
            {
                g.color = m_HighlightColor;
            }
            else
            {
                g.color = m_DimNonHighlighted ? m_DimColor : m_NormalColor;
            }
        }
        if (m_BaselineGraphic != null)
        {
            m_BaselineGraphic.color = m_BaselineColor;
        }
    }

    private void ApplyLayout()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        float width = Mathf.Max(0f, _rt.rect.width);
        float height = Mathf.Max(0f, _rt.rect.height);
        int n = _values.Length;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        float contentW = Mathf.Max(0f, width - m_PaddingLR * 2f);
        float totalSpacing = m_Spacing * Mathf.Max(0, n - 1);
        float colW = (n > 0) ? (contentW - totalSpacing) / n : contentW;
        colW = Mathf.Max(m_MinColumnWidth, colW);

        // 左端の開始X（中央寄せ配置）
        float usedW = (n > 0) ? (n * colW + totalSpacing) : contentW;
        float startX = (width - usedW) * 0.5f;

        // 有効な最大値（スケール反映）
        float effectiveMax = Mathf.Max(1f, _max / Mathf.Max(0.01f, _userScale));

        // カラム配置
        for (int i = 0; i < n; i++)
        {
            float v = Mathf.Max(0f, _values[i]);
            float h = height * (v / effectiveMax);

            var g = _cols[i];
            if (g == null) continue;
            var rt = g.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);

            float x = startX + i * (colW + m_Spacing) + colW * 0.5f;
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(colW, h);
        }

        // ベースライン配置
        // ベースライン配置（Inspector参照）
        if (m_BaselineGraphic != null)
        {
            var brt = m_BaselineGraphic.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(0f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(startX + usedW * 0.5f, 0f);
            brt.sizeDelta = new Vector2(usedW, m_BaselineThickness);
            m_BaselineGraphic.color = m_BaselineColor;
            m_BaselineGraphic.gameObject.SetActive(m_ShowBaseline && usedW > 0f && height > 0f);
            // チャートの子である場合のみ、最前面に移動
            if (m_BaselineGraphic.transform.parent == transform)
            {
                m_BaselineGraphic.transform.SetSiblingIndex(transform.childCount - 1);
            }
        }
    }

    private void EnsureColumns(int count)
    {
        if (count < 0) count = 0;
        // Remove extras
        for (int i = _cols.Count - 1; i >= count; i--)
        {
            var c = _cols[i];
            _cols.RemoveAt(i);
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
            }
        }
        // Add missing
        while (_cols.Count < count)
        {
            _cols.Add(CreateColumn(_cols.Count));
        }
    }

    private MaskableGraphic CreateColumn(int index)
    {
        var go = new GameObject($"Col_{index}", typeof(RectTransform), typeof(SolidGraphic));
        go.transform.SetParent(transform, false);
        var g = go.GetComponent<SolidGraphic>();
        g.raycastTarget = false;
        EnsureCanvasRenderer(g);
        g.color = m_NormalColor;
        return g;
    }

    private static void EnsureCanvasRenderer(Component c)
    {
        if (c == null) return;
        var cr = c.GetComponent<CanvasRenderer>();
        if (cr == null) c.gameObject.AddComponent<CanvasRenderer>();
    }
}
