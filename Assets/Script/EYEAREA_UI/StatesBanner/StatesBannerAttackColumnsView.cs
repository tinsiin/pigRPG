using System;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(StatesBannerColumnsChart))]
public class StatesBannerAttackColumnsView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private StatesBannerColumnsChart m_Chart;

    [Header("Colors")]
    [SerializeField] private Color m_NormalColor = Color.white;
    [SerializeField] private Color m_HighlightColor = Color.red;
    [SerializeField] private Color m_DimColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField, Tooltip("ハイライト以外をディムする（攻撃グラフ向け）")]
    private bool m_DimNonHighlighted = true;

    [Header("Baseline")]
    [SerializeField] private Color m_BaselineColor = new Color(1f, 1f, 1f, 0.7f);
    [SerializeField, Min(0f)] private float m_BaselineThickness = 2f;
    [SerializeField] private bool m_ShowBaseline = true;
    [SerializeField, Tooltip("オンの場合のみ、ビュー側の設定でベースラインを上書きします（初回一度だけ）")]
    private bool m_OverrideBaseline = false;

    private bool m_BaselineApplied = false;

    private void Reset()
    {
        if (m_Chart == null) m_Chart = GetComponent<StatesBannerColumnsChart>();
    }

    private void Awake()
    {
        if (m_Chart == null) m_Chart = GetComponent<StatesBannerColumnsChart>();
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

        // 色とディム挙動を攻撃仕様に設定（ベースラインは初回OnEnableで設定済み）
        m_Chart.SetColors(m_NormalColor, m_HighlightColor, m_DimColor, m_DimNonHighlighted);

        var protocols = Enum.GetValues(typeof(BattleProtocol))
            .Cast<BattleProtocol>()
            .Where(p => p != BattleProtocol.none)
            .ToArray();

        var values = protocols
            .Select(p => actor.ATKProtocolExclusiveTotal(p))
            .ToArray();

        int highlightIndex = Array.IndexOf(protocols, actor.NowBattleProtocol);
        if (highlightIndex < 0 || actor.NowBattleProtocol == BattleProtocol.none) highlightIndex = -1;

        m_Chart.SetValues(values, highlightIndex);
    }
}
