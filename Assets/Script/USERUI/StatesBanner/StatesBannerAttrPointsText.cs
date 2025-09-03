using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 属性ポイントをテキスト列「Name:Value」で横並び表示し、
/// 各セグメント直下にオーブ色の下線を描く UI。
/// - 頻度の低い更新トリガ（StatesBannerController.Bind）でのみ再構築する想定。
/// - 文字色は固定、色表現は下線のみ。
/// </summary>
public class StatesBannerAttrPointsText : MonoBehaviour
{
    [Header("Target TMP")]
    [SerializeField] private TextMeshProUGUI m_TargetTMP; // 手動割当（自動探索しない）

    [Header("Text")]
    [SerializeField] private string m_Separator = "  "; // セパレータ（例: 半角スペース2つ）

    [Header("Underline")] 
    [SerializeField, Min(0f)] private float m_UnderlineThickness = 2f;
    [SerializeField] private float m_UnderlineYOffset = 0f; // テキスト下端からのオフセット(+でさらに下)

    [Header("Anchors (Optional)")]
    [SerializeField] private bool m_ForceTopLeftOnSelf = false; // ONでこのGOを左上基準に強制
    [SerializeField] private bool m_ForceTopLeftOnTMP = false;  // ONでTMPを左上基準に強制

    [Header("Display Names (Override)")]
    [SerializeField] private List<NameOverride> m_NameOverrides = new List<NameOverride>();
    [System.Serializable]
    private struct NameOverride
    {
        public SpiritualProperty Attr;
        public string Display;
    }
    private readonly Dictionary<SpiritualProperty, string> _nameMap = new Dictionary<SpiritualProperty, string>();
    
    // 差分更新用キャッシュ
    private BaseStates _lastActor;
    private string _lastFullText;

    void Awake()
    {
        RebuildNameMap();
        // （任意）このGameObject自体を左上基準に
        var rt = GetComponent<RectTransform>();
        if (rt != null && m_ForceTopLeftOnSelf)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
        }

        // 参照TMPがあれば、（任意で）左上基準へ。改行禁止/オーバーフローは常に設定
        if (m_TargetTMP != null)
        {
            if (m_ForceTopLeftOnTMP)
            {
                var trt = m_TargetTMP.rectTransform;
                trt.anchorMin = new Vector2(0, 1);
                trt.anchorMax = new Vector2(0, 1);
                trt.pivot = new Vector2(0, 1);
            }
            m_TargetTMP.enableWordWrapping = false;
            m_TargetTMP.overflowMode = TextOverflowModes.Overflow;
        }
    }

    void OnValidate()
    {
        RebuildNameMap();
    }

    public void Bind(BaseStates actor)
    {
        if (actor == null) return;

        if (m_TargetTMP == null)
        {
            Debug.LogError("[StatesBannerAttrPointsText] Target TMP(not set). Inspectorで m_TargetTMP を割り当ててください.", this);
            return;
        }

        if (m_TargetTMP.transform.childCount == 0)
        {
            Debug.LogError("[StatesBannerAttrPointsText] TMPに子オブジェクトが1つもありません。下線の設置先として子(空)を1つ作成してください.", m_TargetTMP);
            return;
        }

        // 下線コンテナ: TMP の最初の子を使用
        var underlineContainer = m_TargetTMP.transform.GetChild(0) as RectTransform;
        if (underlineContainer == null)
        {
            Debug.LogError("[StatesBannerAttrPointsText] TMPの最初の子にRectTransformがありません.", m_TargetTMP);
            return;
        }
        // 左上基準に統一
        underlineContainer.anchorMin = new Vector2(0, 1);
        underlineContainer.anchorMax = new Vector2(0, 1);
        underlineContainer.pivot = new Vector2(0, 1);

        // 並び順は固定（最近順は使わない）
        var snapshot = actor.GetAttrPSnapshot(true);

        // セグメント文字列を組み立て（Amount>0のみ）
        var segments = new List<(SpiritualProperty attr, string text)>();
        foreach (var e in snapshot)
        {
            if (e.Amount <= 0) continue;
            var segText = $"{GetAttrDisplayName(e.Attr)}:{e.Amount}";
            segments.Add((e.Attr, segText));
        }

        // 単一テキストを構築
        string fullText = string.Empty;
        for (int i = 0; i < segments.Count; i++)
        {
            fullText += segments[i].text;
            if (i < segments.Count - 1) fullText += m_Separator;
        }

        // 差分スキップ: アクターが同一 かつ テキストが同一なら再構築を省く
        bool actorChanged = !ReferenceEquals(_lastActor, actor);
        if (!actorChanged && string.Equals(_lastFullText, fullText))
        {
            return;
        }

        // TMPに反映（1行想定）
        m_TargetTMP.enableWordWrapping = false;
        m_TargetTMP.overflowMode = TextOverflowModes.Overflow;
        m_TargetTMP.text = fullText;
        m_TargetTMP.ForceMeshUpdate();

        // 下線を再構築（Destroyせずプール再利用）
        ClearUnderlines(underlineContainer);

        // 配置計算: 左からの累積Xを算出
        float x = 0f;
        var tmpRT = m_TargetTMP.rectTransform;
        // テキストの高さ（下線の基準は下端）
        float textHeight = m_TargetTMP.preferredHeight;

        for (int i = 0; i < segments.Count; i++)
        {
            var (attr, segText) = segments[i];
            // セグメント幅とセパレータ幅
            float segW = m_TargetTMP.GetPreferredValues(segText).x;
            float sepW = (i < segments.Count - 1) ? m_TargetTMP.GetPreferredValues(m_Separator).x : 0f;

            // 下線生成（下端からのオフセットで配置）
            float y = -textHeight - m_UnderlineYOffset;
            EnsureUnderline(underlineContainer, i, x, segW, Mathf.Max(1f, m_UnderlineThickness), y, GetAttrColor(actor, attr));

            // 次セグメントの開始X
            x += segW + sepW;
        }

        // キャッシュ更新
        _lastFullText = fullText;
        _lastActor = actor;
    }

    void ClearUnderlines(RectTransform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i).gameObject;
            if (child.activeSelf) child.SetActive(false);
        }
    }

    void EnsureUnderline(RectTransform parent, int index, float x, float width, float height, float y, Color color)
    {
        // 必要数まで子を増やす（プール）
        while (parent.childCount <= index)
        {
            var goNew = new GameObject("UL", typeof(RectTransform));
            var rtNew = goNew.GetComponent<RectTransform>();
            rtNew.SetParent(parent, false);
            rtNew.anchorMin = new Vector2(0, 1);
            rtNew.anchorMax = new Vector2(0, 1);
            rtNew.pivot = new Vector2(0, 1);
            var imgNew = goNew.AddComponent<Image>();
            imgNew.raycastTarget = false;
            goNew.SetActive(false);
        }

        var rt = parent.GetChild(index) as RectTransform;
        if (rt == null) return;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
        var img = rt.GetComponent<Image>();
        if (img == null) img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = color;
        if (!rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
    }

    // 単一TMP版のためテンプレ適用は不要

    Color GetAttrColor(BaseStates actor, SpiritualProperty attr)
    {
        if (AttrPointRingUIController.TryGetOrbColor(actor, attr, out var c)) return c;
        // フォールバック: 属性のハッシュから安定色（HSV）
        uint seed = (uint)attr * 2654435761u;
        float h = Mathf.Abs(Mathf.Sin(seed * 0.0001f)) % 1f;
        float s = 0.8f;
        float v = 0.95f;
        return Color.HSVToRGB(h, s, v);
    }

    string GetAttrDisplayName(SpiritualProperty attr)
    {
        if (_nameMap != null && _nameMap.TryGetValue(attr, out var s) && !string.IsNullOrEmpty(s)) return s;
        return attr.ToString();
    }

    void RebuildNameMap()
    {
        _nameMap.Clear();
        if (m_NameOverrides == null) return;
        for (int i = 0; i < m_NameOverrides.Count; i++)
        {
            var e = m_NameOverrides[i];
            if (string.IsNullOrEmpty(e.Display)) continue;
            _nameMap[e.Attr] = e.Display;
        }
    }
}
