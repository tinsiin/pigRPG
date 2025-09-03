using System;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ColumnsChart))]
public class DefenseColumnsView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ColumnsChart m_Chart;

    [Header("Colors")]
    [SerializeField] private Color m_NormalColor = Color.white;

    [Header("Baseline")]
    [SerializeField] private Color m_BaselineColor = new Color(1f, 1f, 1f, 0.7f);
    [SerializeField, Min(0f)] private float m_BaselineThickness = 2f;
    [SerializeField] private bool m_ShowBaseline = true;
    [SerializeField, Tooltip("オンの場合のみ、ビュー側の設定でベースラインを上書きします（初回一度だけ）")]
    private bool m_OverrideBaseline = false;

    private bool m_BaselineApplied = false;

    private void Reset()
    {
        if (m_Chart == null) m_Chart = GetComponent<ColumnsChart>();
    }

    private void Awake()
    {
        if (m_Chart == null) m_Chart = GetComponent<ColumnsChart>();
    }

    private void OnEnable()
    {
        // ベースラインは最初の一回のみ適用（OverrideBaselineが有効な場合のみ）
        if (!m_BaselineApplied && m_Chart != null && m_OverrideBaseline)
        {
            m_Chart.SetBaseline(m_BaselineColor, m_BaselineThickness, m_ShowBaseline);
            m_BaselineApplied = true;
        }
    }

    public void Bind(BaseStates actor)
    {
        if (actor == null || m_Chart == null) return;

        // 防御は全列 NormalColor・ディム無効（ベースラインは初回OnEnableで設定済み/未設定はチャート直指定を使用）
        m_Chart.SetColors(m_NormalColor, m_NormalColor, m_NormalColor, false);

        var styles = Enum.GetValues(typeof(AimStyle))
            .Cast<AimStyle>()
            .Where(s => s != AimStyle.none)
            .ToArray();

        // AimStyleごとに「排他のみ」で比較
        var values = styles
            .Select(s => actor.DEFProtocolExclusiveTotal(s))
            .ToArray();

        // 防御側はハイライトなし
        m_Chart.SetValues(values, -1);
    }

    public void SetUserScale(float scale)
    {
        if (m_Chart == null) return;
        m_Chart.SetUserScale(scale);
    }
}
