using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class StatesBannerStatBar : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private TextMeshProUGUI m_Label;
    [SerializeField] private TextMeshProUGUI m_ValueText;
    [SerializeField] private StatesBannerGauge m_Gauge;

    [Header("Colors")]
    [SerializeField] private Color m_NormalColor = Color.white;
    [SerializeField] private Color m_HighlightColor = Color.red;
    [SerializeField] private Color m_DimColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    public void SetLabel(string text)
    {
        if (m_Label != null) m_Label.text = text;
    }

    public void SetValue(float value, float maxForScale, bool highlight = false, bool dim = false)
    {
        if (m_Gauge != null)
        {
            float percent = (maxForScale > 0f) ? (value / maxForScale * 100f) : 0f;
            m_Gauge.SetPercent(percent);
            if (dim)
            {
                m_Gauge.FillColor = m_DimColor;
            }
            else
            {
                m_Gauge.FillColor = highlight ? m_HighlightColor : m_NormalColor;
            }
        }
        if (m_ValueText != null)
        {
            m_ValueText.text = value.ToString("0.##");
        }
    }

    public void SetColors(Color normal, Color highlight, Color dim)
    {
        m_NormalColor = normal;
        m_HighlightColor = highlight;
        m_DimColor = dim;
        if (m_Gauge != null)
        {
            m_Gauge.FillColor = m_NormalColor;
        }
    }
}
