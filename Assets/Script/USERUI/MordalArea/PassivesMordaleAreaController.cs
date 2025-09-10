using System.Text;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// パッシブ詳細をモーダルエリアに全文表示するコントローラ。
/// - Characonfig 側から ShowFor(actor) で開く
/// - 表示は省略なし、画面幅で折り返し。ただしトークン <xxx> の途中で改行しないように事前整形
/// - モーダルエリア全体をタップで閉じる（EventTrigger 的挙動: IPointerClickHandler）
/// </summary>
public class PassivesMordaleAreaController : MonoBehaviour, IPointerClickHandler
{
    [Header("Roots")]
    [SerializeField] private GameObject m_Root;           // このモーダルのルート（ModalArea配下のパネル）

    [Header("Navigation")]
    [SerializeField] private Button m_LeftButton;
    [SerializeField] private Button m_RightButton;

    [Header("Views per Page (aTMP / bTMP)")]
    [SerializeField] private TextMeshProUGUI m_FirstText;   // aTMP
    [SerializeField] private TextMeshProUGUI m_SecondText;  // bTMP（次ページがある場合は省略付き）

    [Header("Fit Settings (Height)")]
    [SerializeField] private float m_FitSafety = 1.0f;       // 高さ方向のセーフティ（px相当）
    [SerializeField] private int m_EllipsisDotCount = 4;     // 省略ドット数

    [Header("Logs")]
    [SerializeField] private bool m_DebugLogs = true;

    [Header("Debug (Dummy Tokens)")]
    [SerializeField] private bool m_DebugMode = false;
    [SerializeField] private int m_DebugCount = 100;
    [SerializeField] private string m_DebugPrefix = "pas";

    private int m_PageIndex = 0;
    private List<(string a, string b)> _pages;

    private void Awake()
    {
        if (m_LeftButton != null) m_LeftButton.onClick.AddListener(Prev);
        if (m_RightButton != null) m_RightButton.onClick.AddListener(Next);
    }

    public void ShowFor(BaseStates actor)
    {
        if (m_Root == null || m_FirstText == null || m_SecondText == null)
        {
            Debug.LogWarning("[PassivesModal] m_Root or m_FirstText/m_SecondText is null.");
            return;
        }
        if (actor == null)
        {
            Debug.LogWarning("[PassivesModal] actor is null.");
            return;
        }

        // TMP 設定（描画条件と測定条件を一致）
        PassiveTextUtils.SetupTmpBasics(m_FirstText, truncate: true);
        PassiveTextUtils.SetupTmpBasics(m_SecondText, truncate: true);

        // トークン列を生成（<> リテラル表示。トークン間は半角スペース）
        string allTokens = m_DebugMode
            ? PassiveTextUtils.BuildDummyPassivesTokens(m_DebugCount, m_DebugPrefix)
            : PassiveTextUtils.BuildPassivesTokens(actor);

        // ページ構築（aTMPに詰め、続きはbTMPへ。さらに残る場合はbTMPを省略付きにし、次ページへ持ち越し）
        BuildPages(allTokens);

        // 初期ページ表示
        m_PageIndex = 0;
        ApplyPage();
        UpdateNavInteractable();

        // 表示
        ModalAreaController.Instance?.ShowSingle(m_Root);
    }

    private void D(string msg)
    {
        if (m_DebugLogs) Debug.Log("[PassivesModal] " + msg);
    }

    /// <summary>
    /// Characonfig 側のデバッグ設定を流し込むためのAPI。
    /// </summary>
    public void SetDebug(bool enabled, int count, string prefix)
    {
        m_DebugMode = enabled;
        m_DebugCount = count;
        m_DebugPrefix = string.IsNullOrEmpty(prefix) ? "pas" : prefix;
    }

    // SetupTmpBasics / BuildPassivesTokens / BuildDummyPassivesTokens は PassiveTextUtils を使用

    // ===== ページ生成 =====
    private void BuildPages(string allTokens)
    {
        _pages = new List<(string a, string b)>();
        if (string.IsNullOrEmpty(allTokens))
        {
            _pages.Add((string.Empty, string.Empty));
            return;
        }

        var tokens = allTokens.Split(' ');
        int cursor = 0;
        string ellipsis = new string('•', Mathf.Max(1, m_EllipsisDotCount));

        while (cursor < tokens.Length)
        {
            // aTMP: 省略無しで入るだけ入れる
            string aText = AccumulateTokensToFit(m_FirstText, tokens, ref cursor, withEllipsis: false, null);

            // bTMP: まず省略無しで入るだけ入れる（次ページ判定用）
            int cursorBeforeB = cursor;
            string bTextNoDots = AccumulateTokensToFit(m_SecondText, tokens, ref cursor, withEllipsis: false, null);

            if (cursor < tokens.Length)
            {
                // まだ残っている -> bTMP は省略付きで再計算（bTextNoDotsは捨て、トークン消費は dots 前提で再確定）
                cursor = cursorBeforeB;
                string bTextWithDots = AccumulateTokensToFit(m_SecondText, tokens, ref cursor, withEllipsis: true, ellipsis);
                _pages.Add((aText, bTextWithDots));
            }
            else
            {
                _pages.Add((aText, bTextNoDots));
            }
        }

        D($"BuildPages: pageCount={_pages.Count}");
    }

    // tokens[cursor..] から、候補を半角スペース区切りで連結しつつ、tmp の高さに収まるだけ詰める
    // withEllipsis=true の場合、常に suffix（"••••"）を付けた状態でFit判定し、表示は acc+suffix
    private string AccumulateTokensToFit(TextMeshProUGUI tmp, string[] tokens, ref int cursor, bool withEllipsis, string suffix)
    {
        var acc = new StringBuilder();
        string sep = string.Empty;
        string original = tmp.text;

        while (cursor < tokens.Length)
        {
            string trial = acc.Length == 0 ? tokens[cursor] : acc.ToString() + " " + tokens[cursor];
            string candidate = withEllipsis ? trial + (suffix ?? string.Empty) : trial;
            if (PassiveTextUtils.FitsHeight(tmp, candidate, m_FitSafety))
            {
                acc.Clear();
                acc.Append(trial);
                cursor++;
            }
            else
            {
                break;
            }
        }

        if (withEllipsis)
        {
            if (acc.Length == 0)
            {
                // 1トークンも入らない場合は省略のみ（視覚的に続き有りを示す）
                return suffix ?? string.Empty;
            }
            return acc.ToString() + (suffix ?? string.Empty);
        }
        return acc.ToString();
    }

    // 高さフィット判定は PassiveTextUtils.FitsHeight を使用

    // ===== ページ表示 =====
    private int PageCount => _pages != null ? _pages.Count : 0;

    private void ApplyPage()
    {
        if (_pages == null || _pages.Count == 0)
        {
            if (m_FirstText != null) m_FirstText.text = string.Empty;
            if (m_SecondText != null) m_SecondText.text = string.Empty;
            return;
        }
        m_PageIndex = Mathf.Clamp(m_PageIndex, 0, _pages.Count - 1);
        var page = _pages[m_PageIndex];
        if (m_FirstText != null) m_FirstText.text = page.a ?? string.Empty;
        if (m_SecondText != null) m_SecondText.text = page.b ?? string.Empty;
        D($"ApplyPage: index={m_PageIndex+1}/{_pages.Count}");
    }

    private void UpdateNavInteractable()
    {
        int count = PageCount;
        bool hasMany = count > 1;
        if (m_LeftButton != null) m_LeftButton.interactable = hasMany && (m_PageIndex > 0);
        if (m_RightButton != null) m_RightButton.interactable = hasMany && (m_PageIndex < count - 1);
    }

    public void SetPageIndex(int index)
    {
        int count = PageCount;
        if (count <= 0) return;
        int clamped = Mathf.Clamp(index, 0, count - 1);
        if (clamped == m_PageIndex)
        {
            ApplyPage();
            UpdateNavInteractable();
            return;
        }
        m_PageIndex = clamped;
        ApplyPage();
        UpdateNavInteractable();
    }

    public void Next() => SetPageIndex(m_PageIndex + 1);
    public void Prev() => SetPageIndex(m_PageIndex - 1);

    // モーダル全体をタップで閉じる
    public void OnPointerClick(PointerEventData eventData)
    {
        if (m_Root != null)
        {
            ModalAreaController.Instance?.CloseFor(m_Root);
        }
    }
}
