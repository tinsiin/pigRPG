using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// バックログのページ送りUI。
/// EyeArea > FrontFixedContainer > NovelContent 内に配置。
/// UniTaskCompletionSourceで閉じるまで待機する。
/// </summary>
public sealed class BacklogPageView : MonoBehaviour
{
    [Header("表示設定")]
    [SerializeField] private int linesPerPage = 8;
    [SerializeField] private int maxBacktrackPages = 10;

    [Header("UI参照")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private TMP_Text pageIndicatorText;
    [SerializeField] private Button prevPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private Button closeButton;

    private IReadOnlyList<BacklogEntry> entries;
    private int currentPage; // 0 = 最新ページ
    private int totalPages;
    private UniTaskCompletionSource closeTcs;

    private readonly StringBuilder sb = new();

    public int LinesPerPage => linesPerPage;
    public int MaxBacktrackPages => maxBacktrackPages;
    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (prevPageButton != null) prevPageButton.onClick.AddListener(OnPrevPage);
        if (nextPageButton != null) nextPageButton.onClick.AddListener(OnNextPage);
        if (closeButton != null) closeButton.onClick.AddListener(OnClose);

        // panel.SetActive(false) は行わない。
        // panelが自身のGameObjectを指しており、初回ShowAsync時にSetActive(true)が
        // Awakeをトリガーするため、ここでSetActive(false)すると即座に非表示に戻ってしまう。
        // 初期状態はシーン上で非アクティブに設定済み。
    }

    private void OnDestroy()
    {
        if (prevPageButton != null) prevPageButton.onClick.RemoveListener(OnPrevPage);
        if (nextPageButton != null) nextPageButton.onClick.RemoveListener(OnNextPage);
        if (closeButton != null) closeButton.onClick.RemoveListener(OnClose);
    }

    /// <summary>
    /// バックログを表示し、閉じるまで待機する。
    /// </summary>
    public async UniTask ShowAsync(DialogueBacklog backlog)
    {
        if (backlog == null || backlog.Count == 0) return;

        entries = backlog.Entries;
        currentPage = 0;

        var rawPages = Mathf.CeilToInt((float)entries.Count / linesPerPage);
        totalPages = Mathf.Min(rawPages, maxBacktrackPages);

        Render();
        panel.SetActive(true);

        closeTcs = new UniTaskCompletionSource();
        try
        {
            await closeTcs.Task;
        }
        finally
        {
            closeTcs = null;
            panel.SetActive(false);
        }
    }

    /// <summary>
    /// 外部から強制的に閉じる。
    /// </summary>
    public void ForceClose()
    {
        closeTcs?.TrySetResult();
    }

    private void Render()
    {
        var endExclusive = entries.Count - currentPage * linesPerPage;
        var startInclusive = Mathf.Max(0, endExclusive - linesPerPage);

        sb.Clear();
        for (var i = startInclusive; i < endExclusive; i++)
        {
            var entry = entries[i];
            if (!string.IsNullOrEmpty(entry.Speaker))
            {
                sb.Append("<color=#AACCFF>");
                sb.Append(entry.Speaker);
                sb.Append("</color>\n");
            }
            sb.Append(entry.Text);
            if (i < endExclusive - 1) sb.Append("\n\n");
        }

        if (contentText != null) contentText.text = sb.ToString();

        if (pageIndicatorText != null)
        {
            var displayPage = totalPages - currentPage;
            pageIndicatorText.text = $"{displayPage} / {totalPages}";
        }

        if (prevPageButton != null) prevPageButton.interactable = currentPage < totalPages - 1;
        if (nextPageButton != null) nextPageButton.interactable = currentPage > 0;
    }

    private void OnPrevPage()
    {
        if (currentPage < totalPages - 1)
        {
            currentPage++;
            Render();
        }
    }

    private void OnNextPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            Render();
        }
    }

    private void OnClose()
    {
        closeTcs?.TrySetResult();
    }
}
