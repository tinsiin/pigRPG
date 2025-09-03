using System;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ColumnsChart))]
public class AttackColumnsView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ColumnsChart m_Chart;

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

    /// <summary>
    /// テスト用: すべてのプロトコルを一定値で描画し、指定のハイライトを適用。
    /// highlight に BattleProtocol.none を渡した場合はエラーを出してハイライト無しで描画します。
    /// </summary>
    public void BindTest(float constantValue, BattleProtocol highlight, bool errorOnNoneHighlight = true)
    {
        if (m_Chart == null) return;

        // 色とディム挙動（攻撃仕様）
        m_Chart.SetColors(m_NormalColor, m_HighlightColor, m_DimColor, m_DimNonHighlighted);

        var protocols = Enum.GetValues(typeof(BattleProtocol))
            .Cast<BattleProtocol>()
            .Where(p => p != BattleProtocol.none)
            .ToArray();

        if (protocols.Length == 0)
        {
            m_Chart.Clear();
            return;
        }

        float v = Mathf.Max(0f, constantValue);
        var values = new float[protocols.Length];
        for (int i = 0; i < values.Length; i++) values[i] = v;

        int highlightIndex = -1;
        if (highlight == BattleProtocol.none)
        {
            if (errorOnNoneHighlight)
            {
                Debug.LogError("[AttackColumnsView] テストのハイライトに 'none' は指定できません（グラフに 'none' 列が存在しません）。ハイライト無しで描画します。");
            }
            highlightIndex = -1;
        }
        else
        {
            highlightIndex = Array.IndexOf(protocols, highlight);
            if (highlightIndex < 0)
            {
                Debug.LogWarning($"[AttackColumnsView] 指定のハイライト {highlight} は表示対象外です。ハイライト無しで描画します。");
                highlightIndex = -1;
            }
        }

        m_Chart.SetValues(values, highlightIndex);
    }

    public void SetUserScale(float scale)
    {
        if (m_Chart == null) return;
        m_Chart.SetUserScale(scale);
    }
}

