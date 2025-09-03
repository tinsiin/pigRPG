using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ステータス係数テーブルの共通描画処理を担うベースクラス。
/// ・共通係数 + 複数の排他グループ（任意）を二分割またはページ切替で描画
/// ・セルは TextMeshProUGUI / 罫線は Image をプール再利用
/// ・RectTransform 変化時に再レイアウト
/// 派生クラスは『辞書の供給（見出し名含む）』のみを実装します。
/// </summary>
[DisallowMultipleComponent]
public abstract class PowerCoefficientsTableViewBase : MonoBehaviour
{
    // 内部ルート名/ヘルパー名の定数
    protected const string RootLeftName   = "CellsLeft";
    protected const string RootRightName  = "CellsRight";
    protected const string RootLinesName  = "GridLines";
    protected const string MeasureName    = "TMP_Measure";
    protected const string LegacyStaticName = "StaticInstance";

    [Header("描画先: 未指定=自分のRectTransform。通常は未設定でOK。")]
    [SerializeField] private RectTransform m_GridRoot; // 未指定なら自分

    [Header("文字色/整列")]
    [SerializeField] private Color m_FontColor = new Color(1f,1f,1f,1f);
    [SerializeField] private TextAlignmentOptions m_TextAlignment = TextAlignmentOptions.Left;

    [Header("余白/行間/分割ギャップ")]
    [SerializeField] private Vector4 m_Padding = new Vector4(8,8,8,8); // L,T,R,B
    [SerializeField] private Vector2 m_CellPadding = new Vector2(6,2);  // 左右/上下
    [SerializeField] private float m_RowSpacing = 0f;
    [SerializeField] private float m_SplitGap = 24f;

    [Header("列幅設定")]
    [SerializeField] private ColumnWidthMode m_ColumnWidthMode = ColumnWidthMode.AutoFromHeader;
    [SerializeField] private float m_LabelColumnWidth = 120f;
    [SerializeField] private float m_ValueColumnWidth = 72f;
    [SerializeField] private float m_ColumnWidthScale = 1.0f;
    [SerializeField] private float m_MinColumnWidth = 48f;

    [Header("罫線設定")]
    [SerializeField] private bool m_ShowGridLines = true;
    [SerializeField] private Color m_GridColor = new Color(1f,1f,1f,0.6f);
    [SerializeField] private float m_GridThickness = 1f;

    [Header("Fit/Scale 設定")]
    [SerializeField] private FitMode m_Fit = FitMode.None; // 自動フィット方法
    [SerializeField, Min(0.01f)] private float m_UserScale = 1f; // 手動スケール（Fitと乗算）
    [SerializeField] private bool m_ScaleSplitGap = true; // 分割ギャップもスケールする

    [Header("フォントサイズ設定")] 
    [SerializeField] private FontSizingMode m_FontSizing = FontSizingMode.Manual;
    [SerializeField] private float m_FontSize = 20f; // Manual
    [SerializeField] private float m_RuntimeMaxManualFontSizeNoShrink = 0f; // 実行時参考
    [SerializeField] private float m_AutoReferenceFontSize = 20f; // AutoFitMax

    [Header("整列（親Rect内での配置）")]
    [SerializeField] private bool m_CenterHorizontally = true;
    [SerializeField] private bool m_CenterVertically = false;

    [Header("表示方式: SplitHalf=左右同時表示 / Paged=ページ切替")]
    [SerializeField] private DisplayMode m_DisplayMode = DisplayMode.SplitHalf;
    [SerializeField] private Button m_PageButton;
    [SerializeField] private string m_Page1Label = "1";
    [SerializeField] private string m_Page2Label = "2";
    [SerializeField] private TextMeshProUGUI m_PageButtonLabelTMP;

    [Header("静的モード（ベイク再生）")]
    [SerializeField] private bool m_StaticMode = false;

    protected enum FitMode { None, FitWidth, FitHeight, FitBoth }
    protected enum ColumnWidthMode { AutoFromHeader, Absolute }
    protected enum DisplayMode { SplitHalf, Paged }
    protected enum FontSizingMode { Manual, AutoFitMax }

    [Serializable] private class BakedCell { public string text; public Vector2 size; public Vector2 anchoredPos; public float fontSize; }
    [Serializable] private class BakedLine { public Vector2 size; public Vector2 anchoredPos; }
    [Serializable] private class BakedPage { public List<BakedCell> cells = new List<BakedCell>(); public List<BakedLine> lines = new List<BakedLine>(); }
    [Serializable] private class BakedData { public BakedPage page0 = new BakedPage(); public BakedPage page1 = new BakedPage(); public bool hasRight = false; }

    // 内部ルート/プール
    private RectTransform m_LeftRoot;
    private RectTransform m_RightRoot;
    private RectTransform m_LinesRoot;
    private readonly List<TextMeshProUGUI> m_CellPool = new List<TextMeshProUGUI>();
    private int m_CellUsed = 0;
    private readonly List<Image> m_LinePool = new List<Image>();
    private int m_LineUsed = 0;
    private TextMeshProUGUI m_MeasureText;
    private int m_CurrentPage = 0; // 0=左/1=右（Paged）
    [SerializeField] private BakedData m_BakedData;

    // ===== データ供給（派生が実装） =====
    protected string LabelColumnHeader => "能力";
    protected string CommonColumnHeader => "共通";
    protected abstract IReadOnlyDictionary<TenDayAbility, float> GetCommonMap();
    protected abstract IEnumerable<KeyValuePair<string, IReadOnlyDictionary<TenDayAbility, float>>> GetExclusiveGroups();

    private void OnEnable()
    {
        if (m_PageButton != null)
        {
            m_PageButton.onClick.RemoveListener(OnPageButtonClicked);
            m_PageButton.onClick.AddListener(OnPageButtonClicked);
        }
        UpdatePageButtonLabel();

        if (m_StaticMode)
        {
            if (HasBakedData()) { RenderFromBaked(); return; }
        }
        Render();
    }

    private void OnDisable()
    {
        if (m_PageButton != null)
        {
            m_PageButton.onClick.RemoveListener(OnPageButtonClicked);
        }
    }

    private void OnPageButtonClicked()
    {
        if (m_DisplayMode != DisplayMode.Paged) return;
        m_CurrentPage = (m_CurrentPage == 0) ? 1 : 0;
        UpdatePageButtonLabel();
        if (m_StaticMode && HasBakedData()) RenderFromBaked(); else Render();
    }

    private void EnsurePageButtonLabelTargets()
    {
        if (m_PageButton == null) return;
        if (m_PageButtonLabelTMP == null) m_PageButtonLabelTMP = m_PageButton.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void UpdatePageButtonLabel()
    {
        if (m_PageButton == null) return;
        EnsurePageButtonLabelTargets();
        string label = (m_CurrentPage == 0) ? m_Page1Label : m_Page2Label;
        if (m_PageButtonLabelTMP != null) m_PageButtonLabelTMP.text = label;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (m_StaticMode) return;
        if (isActiveAndEnabled) Render();
    }

    public void Render()
    {
        if (m_StaticMode) return;
        RenderStructuredGrid();
    }

    private bool HasBakedData()
    {
        return m_BakedData != null && m_BakedData.page0 != null && m_BakedData.page0.cells != null && m_BakedData.page0.cells.Count > 0;
    }

    private void RenderFromBaked()
    {
        var rt = m_GridRoot != null ? m_GridRoot : (RectTransform)transform;
        if (rt == null) return;
        EnsureRoots(rt);
        if (m_LeftRoot != null)  m_LeftRoot.gameObject.SetActive(true);
        if (m_RightRoot != null) m_RightRoot.gameObject.SetActive(true);
        if (m_LinesRoot != null) m_LinesRoot.gameObject.SetActive(true);
        DeactivateAllChildren(m_LeftRoot);
        DeactivateAllChildren(m_RightRoot);
        DeactivateAllChildren(m_LinesRoot);
        ResetPools();

        bool paged = m_DisplayMode == DisplayMode.Paged;
        if (paged)
        {
            int page = (m_CurrentPage == 0 || !m_BakedData.hasRight) ? 0 : 1;
            var target = (page == 0) ? m_BakedData.page0 : m_BakedData.page1;
            RenderBakedPageToRoot(m_LeftRoot, target, true);
            if (m_RightRoot != null && m_RightRoot.gameObject.activeSelf) m_RightRoot.gameObject.SetActive(false);
            RenderBakedLines(m_BakedData, page);
            if (m_PageButton != null)
            {
                bool show = m_BakedData.hasRight;
                if (m_PageButton.gameObject.activeSelf != show) m_PageButton.gameObject.SetActive(show);
            }
        }
        else
        {
            if (m_LeftRoot != null)  m_LeftRoot.gameObject.SetActive(true);
            if (m_RightRoot != null) m_RightRoot.gameObject.SetActive(true);
            RenderBakedPageToRoot(m_LeftRoot,  m_BakedData.page0, true);
            if (m_BakedData.hasRight) RenderBakedPageToRoot(m_RightRoot, m_BakedData.page1, true);
            RenderBakedLines(m_BakedData, -1);
            if (m_PageButton != null && m_PageButton.gameObject.activeSelf) m_PageButton.gameObject.SetActive(false);
        }
        DeactivateUnused();
    }

    private void RenderBakedPageToRoot(RectTransform root, BakedPage page, bool visible)
    {
        if (root == null || page == null) return;
        if (!visible) { if (root.gameObject.activeSelf) root.gameObject.SetActive(false); return; }
        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
        foreach (var bc in page.cells)
        {
            var cell = AcquireCell(root);
            cell.text = bc.text; cell.fontSize = bc.fontSize; cell.color = m_FontColor; cell.alignment = m_TextAlignment;
            var r = cell.rectTransform; r.sizeDelta = bc.size; r.anchoredPosition = bc.anchoredPos;
        }
    }

    private void RenderBakedLines(BakedData data, int page)
    {
        if (!m_ShowGridLines) return;
        if (page == 0 || page == 1)
        {
            var lines = (page == 0) ? data.page0.lines : data.page1.lines;
            foreach (var bl in lines)
            {
                var ln = AcquireLine(m_LinesRoot);
                ln.rectTransform.sizeDelta = bl.size; ln.rectTransform.anchoredPosition = bl.anchoredPos;
            }
        }
        else
        {
            foreach (var bl in data.page0.lines)
            {
                var ln = AcquireLine(m_LinesRoot);
                ln.rectTransform.sizeDelta = bl.size; ln.rectTransform.anchoredPosition = bl.anchoredPos;
            }
            foreach (var bl in data.page1.lines)
            {
                var ln = AcquireLine(m_LinesRoot);
                ln.rectTransform.sizeDelta = bl.size; ln.rectTransform.anchoredPosition = bl.anchoredPos;
            }
        }
    }

    [ContextMenu("Bake Data From Current")]
    public void BakeDataFromCurrent()
    {
        var rt = m_GridRoot != null ? m_GridRoot : (RectTransform)transform;
        if (rt == null) return;
        EnsureRoots(rt);
        bool prevStatic = m_StaticMode; var prevMode = m_DisplayMode; int prevPage = m_CurrentPage; m_StaticMode = false;
        var baked = new BakedData();
        if (prevMode == DisplayMode.Paged)
        {
            m_CurrentPage = 0; Render(); CaptureCurrentPagedBlock(out baked.page0);
            m_CurrentPage = 1; Render(); CaptureCurrentPagedBlock(out baked.page1);
            baked.hasRight = !ArePagesIdentical(baked.page0, baked.page1);
            if (!baked.hasRight) baked.page1 = new BakedPage();
        }
        else
        {
            Render(); CaptureCurrentSplitBlocks(out baked.page0, out baked.page1);
            baked.hasRight = baked.page1 != null && baked.page1.cells != null && baked.page1.cells.Count > 0;
        }
        m_BakedData = baked; m_DisplayMode = prevMode; m_CurrentPage = prevPage; m_StaticMode = prevStatic;
    }

    [ContextMenu("Clear Baked Data")]
    public void ClearBakedData()
    {
        m_BakedData = null;
        var rt = m_GridRoot != null ? m_GridRoot : (RectTransform)transform;
        if (rt != null)
        {
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                var child = rt.GetChild(i) as RectTransform; if (child == null) continue;
                string n = child.name;
                if (n == RootLeftName || n == RootRightName || n == RootLinesName || n == MeasureName || n == LegacyStaticName)
                {
                    DestroySafely(child.gameObject);
                }
            }
            if (m_LeftRoot  != null) { if (m_LeftRoot)  DestroySafely(m_LeftRoot.gameObject);  m_LeftRoot  = null; }
            if (m_RightRoot != null) { if (m_RightRoot) DestroySafely(m_RightRoot.gameObject); m_RightRoot = null; }
            if (m_LinesRoot != null) { if (m_LinesRoot) DestroySafely(m_LinesRoot.gameObject); m_LinesRoot = null; }
            if (m_MeasureText != null) { if (m_MeasureText) DestroySafely(m_MeasureText.gameObject); m_MeasureText = null; }
        }
        m_CellPool.Clear(); m_LinePool.Clear(); ResetPools();
    }

    public bool HasBakedDataForEditor() { return HasBakedData(); }

    private void CaptureCurrentPagedBlock(out BakedPage page)
    {
        page = new BakedPage();
        if (m_LeftRoot != null)
        {
            var texts = m_LeftRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (!t.gameObject.activeInHierarchy) continue;
                var r = t.rectTransform; page.cells.Add(new BakedCell { text = t.text, fontSize = t.fontSize, size = r.sizeDelta, anchoredPos = r.anchoredPosition });
            }
        }
        if (m_LinesRoot != null)
        {
            var imgs = m_LinesRoot.GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                if (!img.gameObject.activeInHierarchy) continue;
                var r = img.rectTransform; page.lines.Add(new BakedLine { size = r.sizeDelta, anchoredPos = r.anchoredPosition });
            }
        }
    }

    private void CaptureCurrentSplitBlocks(out BakedPage left, out BakedPage right)
    {
        left = new BakedPage(); right = new BakedPage(); float rightMinX = float.PositiveInfinity;
        if (m_LeftRoot != null)
        {
            var texts = m_LeftRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (!t.gameObject.activeInHierarchy) continue;
                var r = t.rectTransform; left.cells.Add(new BakedCell { text = t.text, fontSize = t.fontSize, size = r.sizeDelta, anchoredPos = r.anchoredPosition });
            }
        }
        if (m_RightRoot != null)
        {
            var texts = m_RightRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (!t.gameObject.activeInHierarchy) continue;
                var r = t.rectTransform; right.cells.Add(new BakedCell { text = t.text, fontSize = t.fontSize, size = r.sizeDelta, anchoredPos = r.anchoredPosition });
                rightMinX = Mathf.Min(rightMinX, r.anchoredPosition.x);
            }
        }
        if (m_LinesRoot != null)
        {
            var imgs = m_LinesRoot.GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                if (!img.gameObject.activeInHierarchy) continue;
                var r = img.rectTransform; var line = new BakedLine { size = r.sizeDelta, anchoredPos = r.anchoredPosition };
                if (r.anchoredPosition.x >= rightMinX - 0.5f) right.lines.Add(line); else left.lines.Add(line);
            }
        }
    }

    private bool ArePagesIdentical(BakedPage a, BakedPage b)
    {
        if (a == null || b == null) return false;
        if (a.cells.Count != b.cells.Count) return false;
        for (int i=0;i<a.cells.Count;i++)
        {
            var ca = a.cells[i]; var cb = b.cells[i];
            if (ca.text != cb.text) return false;
            if (Mathf.Abs(ca.fontSize - cb.fontSize) > 0.01f) return false;
            if ((ca.size - cb.size).sqrMagnitude > 0.01f) return false;
            if ((ca.anchoredPos - cb.anchoredPos).sqrMagnitude > 0.01f) return false;
        }
        if (a.lines.Count != b.lines.Count) return false;
        for (int i=0;i<a.lines.Count;i++)
        {
            var la = a.lines[i]; var lb = b.lines[i];
            if ((la.size - lb.size).sqrMagnitude > 0.01f) return false;
            if ((la.anchoredPos - lb.anchoredPos).sqrMagnitude > 0.01f) return false;
        }
        return true;
    }

    private static string FormatCoeff(float v)
    {
        if (v == 0f) return "0";
        return v.ToString("#0.##;-#0.##;0");
    }

    private void RenderStructuredGrid()
    {
        var rt = m_GridRoot != null ? m_GridRoot : (RectTransform)transform;
        if (rt == null) { Debug.LogWarning(GetType().Name + ": RectTransform not found."); return; }
        EnsureRoots(rt);
        if (m_LeftRoot != null)  m_LeftRoot.gameObject.SetActive(true);
        if (m_RightRoot != null) m_RightRoot.gameObject.SetActive(true);
        if (m_LinesRoot != null) m_LinesRoot.gameObject.SetActive(true);
        EnsureMeasure(rt);
        ResetPools();

        // 列（能力/共通/各グループ）
        var groups = GetExclusiveGroups().ToList();
        var headers = new List<string> { LabelColumnHeader, CommonColumnHeader };
        foreach (var g in groups) headers.Add(g.Key);

        // 能力リスト（存在するものだけを列挙体の宣言順で）
        var abSet = new HashSet<TenDayAbility>();
        var common = GetCommonMap();
        foreach (var kv in common) abSet.Add(kv.Key);
        foreach (var g in groups) foreach (var kv in g.Value) abSet.Add(kv.Key);
        var abilities = Enum.GetValues(typeof(TenDayAbility)).Cast<TenDayAbility>().Where(ab => abSet.Contains(ab)).ToList();

        int colCount = headers.Count;
        var colWidths = new float[colCount];
        float baseFontForLayout = (m_FontSizing == FontSizingMode.AutoFitMax) ? m_AutoReferenceFontSize : m_FontSize;
        if (m_ColumnWidthMode == ColumnWidthMode.Absolute)
        {
            colWidths[0] = Mathf.Max(1f, m_LabelColumnWidth);
            for (int c=1; c<colCount; c++) colWidths[c] = Mathf.Max(1f, m_ValueColumnWidth);
        }
        else
        {
            for (int c=0;c<colCount;c++)
            {
                float w = MeasureWidth(headers[c], baseFontForLayout) + m_CellPadding.x*2f;
                w = Mathf.Max(w * m_ColumnWidthScale, m_MinColumnWidth);
                colWidths[c]=w;
            }
        }
        float blockWidth = 0f; for (int c=0;c<colCount;c++) blockWidth += colWidths[c];
        float rowHeight = Mathf.Ceil(baseFontForLayout) + m_CellPadding.y*2f + m_RowSpacing;

        int totalRows = abilities.Count;
        int leftRows = (totalRows + 1)/2;
        int rightRows = totalRows - leftRows;

        bool paged = m_DisplayMode == DisplayMode.Paged;
        float contentWidthBase = paged ? blockWidth : ((rightRows>0) ? (blockWidth * 2f + m_SplitGap) : blockWidth);
        float leftHeightBase   = (leftRows + 1) * rowHeight;
        float rightHeightBase  = (rightRows>0 ? (rightRows + 1) * rowHeight : leftHeightBase);
        float contentHeightBase = Mathf.Max(leftHeightBase, rightHeightBase);

        float availW = Mathf.Max(1f, rt.rect.width  - (m_Padding.x + m_Padding.z));
        float availH = Mathf.Max(1f, rt.rect.height - (m_Padding.y + m_Padding.w));

        int rowsWithHeaderEff = Mathf.Max(leftRows, rightRows>0 ? rightRows : leftRows) + 1;
        float perRowAvail = availH / Mathf.Max(1, rowsWithHeaderEff);
        float cpad = m_CellPadding.y*2f + m_RowSpacing;
        float maxManualFontNoShrink = Mathf.Floor(perRowAvail - cpad);
        m_RuntimeMaxManualFontSizeNoShrink = Mathf.Max(0f, maxManualFontNoShrink);

        float fitScale = 1f;
        switch (m_Fit)
        {
            case FitMode.FitWidth:  fitScale = contentWidthBase  > 0f ? (availW / contentWidthBase) : 1f; break;
            case FitMode.FitHeight: fitScale = contentHeightBase > 0f ? (availH / contentHeightBase) : 1f; break;
            case FitMode.FitBoth:
                float sx = contentWidthBase > 0f ? (availW / contentWidthBase) : 1f;
                float sy = contentHeightBase > 0f ? (availH / contentHeightBase) : 1f;
                fitScale = Mathf.Min(sx, sy); break;
            case FitMode.None:
            default: fitScale = 1f; break;
        }
        float scale = Mathf.Max(0.01f, fitScale * m_UserScale);

        var colWidthsS = new float[colCount]; for (int c=0;c<colCount;c++) colWidthsS[c] = colWidths[c] * scale;
        float blockWidthS = 0f; for (int c=0;c<colCount;c++) blockWidthS += colWidthsS[c];
        float splitGapS = m_ScaleSplitGap ? (m_SplitGap * scale) : m_SplitGap;
        float cellFontSize = baseFontForLayout * scale;
        float scaleForText = (baseFontForLayout > 0f) ? (cellFontSize / baseFontForLayout) : 1f;
        float rowHeightS = rowHeight * scale;

        float contentWidthS  = paged ? blockWidthS : ((rightRows>0) ? (blockWidthS * 2f + splitGapS) : blockWidthS);
        float leftHeightS    = (leftRows + 1) * rowHeightS;
        float rightHeightS   = (rightRows>0 ? (rightRows + 1) * rowHeightS : leftHeightS);
        float contentHeightS = Mathf.Max(leftHeightS, rightHeightS);

        float xLeft = m_Padding.x + (m_CenterHorizontally ? Mathf.Max(0f, (availW - contentWidthS) * 0.5f) : 0f);
        float yTop  = m_Padding.y + (m_CenterVertically  ? Mathf.Max(0f, (availH - contentHeightS) * 0.5f) : 0f);

        var groupMaps = groups.Select(g => g.Value).ToList();
        if (paged)
        {
            int page = m_CurrentPage; if (rightRows <= 0) page = 0;
            int startIndex = (page==0) ? 0 : leftRows;
            int countRows  = (page==0) ? leftRows : rightRows;
            LayoutBlock(m_LeftRoot, xLeft, yTop, headers, abilities, startIndex, countRows, colWidthsS, rowHeightS, common, groupMaps, cellFontSize, scaleForText);
            if (m_ShowGridLines) DrawGrid(m_LinesRoot, xLeft, yTop, blockWidthS, countRows, colWidthsS, rowHeightS);
            if (m_PageButton != null)
            {
                bool show = rightRows > 0;
                if (m_PageButton.gameObject.activeSelf != show) m_PageButton.gameObject.SetActive(show);
                if (!show && m_CurrentPage != 0) { m_CurrentPage = 0; UpdatePageButtonLabel(); }
            }
        }
        else
        {
            int leftStart = 0, leftCount = leftRows;
            int rightStart = leftRows, rightCount = rightRows;
            LayoutBlock(m_LeftRoot,  xLeft, yTop, headers, abilities, leftStart, leftCount, colWidthsS, rowHeightS, common, groupMaps, cellFontSize, scaleForText);
            if (rightRows>0)
                LayoutBlock(m_RightRoot, xLeft + blockWidthS + splitGapS, yTop, headers, abilities, rightStart, rightCount, colWidthsS, rowHeightS, common, groupMaps, cellFontSize, scaleForText);
            if (m_ShowGridLines)
            {
                DrawGrid(m_LinesRoot, xLeft, yTop, blockWidthS, leftRows,  colWidthsS, rowHeightS);
                if (rightRows>0) DrawGrid(m_LinesRoot, xLeft + blockWidthS + splitGapS, yTop, blockWidthS, rightRows, colWidthsS, rowHeightS);
            }
            if (m_PageButton != null && m_PageButton.gameObject.activeSelf) m_PageButton.gameObject.SetActive(false);
        }
        DeactivateUnused();
    }

    private void LayoutBlock(RectTransform parent, float startX, float startY, List<string> headers, List<TenDayAbility> abilities,
        int startIndex, int count, float[] colWidths, float rowHeight, IReadOnlyDictionary<TenDayAbility, float> common,
        List<IReadOnlyDictionary<TenDayAbility, float>> groupMaps, float cellFontSize, float scaleForText)
    {
        int colCount = colWidths.Length;
        var colXs = new float[colCount]; colXs[0]=0f; for (int c=1;c<colCount;c++) colXs[c]=colXs[c-1]+colWidths[c-1];
        float padX = m_CellPadding.x * scaleForText; float padY = m_CellPadding.y * scaleForText;

        // ヘッダ行
        for (int c=0;c<colCount;c++)
        {
            var cell = AcquireCell(parent);
            cell.text = headers[c]; cell.fontSize = cellFontSize;
            float innerW = Mathf.Max(1f, colWidths[c] - padX*2f); float innerH = Mathf.Max(1f, rowHeight - padY*2f);
            var r = cell.rectTransform; r.sizeDelta = new Vector2(innerW, innerH); r.anchoredPosition = new Vector2(startX + colXs[c] + padX, -(startY + padY));
        }

        // データ行
        for (int i=0;i<count;i++)
        {
            var ab = abilities[startIndex + i];
            common.TryGetValue(ab, out float commonV);
            for (int c=0;c<colCount;c++)
            {
                var cell = AcquireCell(parent);
                string txt;
                if (c==0) txt = ab.ToDisplayShortText();
                else if (c==1) txt = FormatCoeff(commonV);
                else { var map = groupMaps[c-2]; map.TryGetValue(ab, out float v); txt = FormatCoeff(v); }
                cell.text = txt; cell.fontSize = cellFontSize;
                float innerW = Mathf.Max(1f, colWidths[c] - padX*2f); float innerH = Mathf.Max(1f, rowHeight - padY*2f);
                var r = cell.rectTransform; r.sizeDelta = new Vector2(innerW, innerH); r.anchoredPosition = new Vector2(startX + colXs[c] + padX, -(startY + (i+1)*rowHeight + padY));
            }
        }
    }

    private void DrawGrid(RectTransform linesRoot, float startX, float startY, float blockWidth, int dataRowCount, float[] colWidths, float rowHeight)
    {
        int rowsWithHeader = dataRowCount + 1; float totalH = rowsWithHeader * rowHeight;
        for (int r=0;r<=rowsWithHeader;r++)
        {
            var ln = AcquireLine(linesRoot); ln.rectTransform.sizeDelta = new Vector2(blockWidth, m_GridThickness); ln.rectTransform.anchoredPosition = new Vector2(startX, -(startY + r*rowHeight));
        }
        float x=0f; int colCount = colWidths.Length;
        for (int c=0;c<=colCount;c++)
        {
            var ln = AcquireLine(linesRoot); ln.rectTransform.sizeDelta = new Vector2(m_GridThickness, totalH); ln.rectTransform.anchoredPosition = new Vector2(startX + x, -startY);
            if (c<colCount) x += colWidths[c];
        }
    }

    private void EnsureRoots(RectTransform rt)
    {
        if (m_LeftRoot == null)   m_LeftRoot   = FindOrCreateChild(rt, RootLeftName);
        if (m_RightRoot == null)  m_RightRoot  = FindOrCreateChild(rt, RootRightName);
        if (m_LinesRoot == null)  m_LinesRoot  = FindOrCreateChild(rt, RootLinesName);
    }

    private RectTransform CreateChild(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false); r.anchorMin = r.anchorMax = new Vector2(0,1); r.pivot = new Vector2(0,1); r.anchoredPosition = Vector2.zero; r.sizeDelta = Vector2.zero; return r;
    }

    private RectTransform FindOrCreateChild(RectTransform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i) as RectTransform; if (c != null && c.name == name) return c;
        }
        return CreateChild(parent, name);
    }

    private void EnsureMeasure(RectTransform rt)
    {
        if (m_MeasureText != null) return;
        var go = new GameObject(MeasureName, typeof(RectTransform));
        var r = go.GetComponent<RectTransform>(); r.SetParent(rt, false); r.anchorMin = r.anchorMax = new Vector2(0,1); r.pivot = new Vector2(0,1);
        m_MeasureText = go.AddComponent<TextMeshProUGUI>(); m_MeasureText.gameObject.SetActive(false);
    }

    private float MeasureWidth(string s, float fontSize)
    {
        m_MeasureText.fontSize = fontSize; m_MeasureText.enableWordWrapping = false; var v = m_MeasureText.GetPreferredValues(s); return v.x;
    }

    private TextMeshProUGUI AcquireCell(Transform parent)
    {
        int idx = m_CellUsed; TextMeshProUGUI t = (idx < m_CellPool.Count) ? m_CellPool[idx] : null;
        if (t == null) { t = CreateCell(); if (idx < m_CellPool.Count) m_CellPool[idx] = t; else m_CellPool.Add(t); }
        m_CellUsed++;
        if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        if (t.transform.parent != parent) t.rectTransform.SetParent(parent, false);
        t.fontSize = m_FontSize; t.enableWordWrapping=false; t.overflowMode = TextOverflowModes.Overflow; t.alignment = m_TextAlignment; t.color = m_FontColor;
        return t;
    }

    private TextMeshProUGUI CreateCell()
    {
        var go = new GameObject("CellTMP", typeof(RectTransform));
        var t = go.AddComponent<TextMeshProUGUI>(); var r = t.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0,1); r.pivot = new Vector2(0,1); return t;
    }

    private Image AcquireLine(Transform parent)
    {
        int idx = m_LineUsed; Image img = (idx < m_LinePool.Count) ? m_LinePool[idx] : null;
        if (img == null) { img = CreateLine(); if (idx < m_LinePool.Count) m_LinePool[idx] = img; else m_LinePool.Add(img); }
        m_LineUsed++;
        if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
        if (img.transform.parent != parent) img.rectTransform.SetParent(parent, false);
        img.color = m_GridColor; return img;
    }

    private Image CreateLine()
    {
        var go = new GameObject("GridLine", typeof(RectTransform)); var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0,1); r.pivot = new Vector2(0,1); return go.AddComponent<Image>();
    }

    private void ResetPools() { m_CellUsed = 0; m_LineUsed = 0; }

    private void DeactivateUnused()
    {
        for (int i=m_CellUsed;i<m_CellPool.Count;i++) if (m_CellPool[i]!=null) m_CellPool[i].gameObject.SetActive(false);
        for (int i=m_LineUsed;i<m_LinePool.Count;i++) if (m_LinePool[i]!=null) m_LinePool[i].gameObject.SetActive(false);
    }

    private void DeactivateAllChildren(RectTransform root)
    {
        if (root == null) return; for (int i = 0; i < root.childCount; i++) { var c = root.GetChild(i); if (c != null && c.gameObject.activeSelf) c.gameObject.SetActive(false); }
    }

    private static void DestroySafely(GameObject go)
    {
        if (go == null) return; if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
    }
}
