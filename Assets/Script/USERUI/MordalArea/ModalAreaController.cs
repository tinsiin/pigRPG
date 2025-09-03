using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ModalArea 配下のフルスクリーン・モーダルを一元管理するコントローラ。
/// - ModalArea(ルート)の表示/非表示
/// - EyeArea(ベースUI)の表示/非表示
/// - 子モーダルパネルの単一表示（同時に1つのみ）
/// - Esc クローズ（オプション）
/// - 簡易スタック（Openで積み、CloseCurrentで戻る）
///
/// 使い方：
///   ModalAreaController.Instance.ShowSingle(targetPanel);
///   ModalAreaController.Instance.CloseFor(targetPanel);
///   // or ModalAreaController.Instance.CloseCurrent();
///
/// インスペクタで m_Root(ModalArea) / m_EyeArea / m_Panels を割り当ててください。
/// </summary>
public class ModalAreaController : MonoBehaviour
{
    public static ModalAreaController Instance { get; private set; }

    [Header("Roots")]
    [SerializeField] private GameObject m_Root;     // ModalArea のルート
    //[SerializeField] private GameObject m_EyeArea;  // ベースUI（モーダル時に隠す）
    //最前面に表示するので不要

    [Header("Panels (Children under ModalArea)")]
    [SerializeField] private List<GameObject> m_Panels = new();

    [Header("Behavior")]
    [SerializeField] private bool m_EnableEscClose = true;
    [SerializeField] private bool m_DebugLogs = true;

    private readonly Stack<GameObject> _stack = new();

    private void D(string msg)
    {
        if (m_DebugLogs)
        {
            Debug.Log("[Modal] " + msg);
        }
    }

    private void Awake()
    {
        // シングルトン
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 起動時は閉じておく
        if (m_Root != null) m_Root.SetActive(false);
        D($"Awake: root={(m_Root!=null)}, panels={(m_Panels!=null ? m_Panels.Count : 0)}");
    }

    private void Update()
    {
        if (m_EnableEscClose && Input.GetKeyDown(KeyCode.Escape))
        {
            D("Update: ESC pressed");
            CloseCurrent();
        }
    }

    /// <summary>
    /// スタックをクリアし、指定のパネルを単独で開く（従来のトグル切替に近い挙動）。
    /// </summary>
    public void ShowSingle(GameObject panel)
    {
        if (panel == null) return;
        _stack.Clear();
        D($"ShowSingle: panel={(panel!=null ? panel.name : "null")} -> clear stack");
        Open(panel);
    }

    /// <summary>
    /// パネルをスタックに積んで開く（戻る操作で元に戻れる）。
    /// </summary>
    public void Open(GameObject panel)
    {
        if (panel == null) return;
        EnsureRootOpen();
        var canvas = panel.GetComponentInParent<Canvas>();
        D($"Open: panel={panel.name}, rootActive={(m_Root!=null ? m_Root.activeInHierarchy : false)}, canvasOrder={(canvas!=null ? canvas.sortingOrder : 0)}, stack(before)={_stack.Count}");
        ActivateOnly(panel);
        _stack.Push(panel);
        D($"Open: stack(after)={_stack.Count}");
    }

    /// <summary>
    /// 現在のトップのパネルを閉じる。スタックが空なら全て閉じる。
    /// </summary>
    public void CloseCurrent()
    {
        if (_stack.Count == 0)
        {
            D("CloseCurrent: stack empty -> CloseAll");
            CloseAll();
            return;
        }
        var top = _stack.Pop();
        D($"CloseCurrent: pop={top?.name}, stack(afterPop)={_stack.Count}");
        if (top != null) top.SetActive(false);

        if (_stack.Count > 0)
        {
            // 直前のパネルを表示状態に戻す
            ActivateOnly(_stack.Peek());
            D($"CloseCurrent: restore peek={_stack.Peek()?.name}");
        }
        else
        {
            D("CloseCurrent: stack empty after pop -> CloseAll");
            CloseAll();
        }
    }

    /// <summary>
    /// 指定のパネルを閉じる。現在表示中であれば CloseCurrent 相当。
    /// </summary>
    public void CloseFor(GameObject panel)
    {
        if (panel == null)
        {
            D("CloseFor: panel is null -> ignore");
            return;
        }
        if (_stack.Count == 0)
        {
            if (panel.activeSelf) panel.SetActive(false);
            D($"CloseFor: stack empty, just deactivate panel={panel.name} and CloseAll");
            CloseAll();
            return;
        }
        if (_stack.Peek() == panel)
        {
            D($"CloseFor: target is top -> CloseCurrent ({panel.name})");
            CloseCurrent();
            return;
        }
        // スタックから除去（順序を崩さない）
        var tmp = new Stack<GameObject>();
        bool removed = false;
        while (_stack.Count > 0)
        {
            var p = _stack.Pop();
            if (!removed && p == panel)
            {
                if (p != null) p.SetActive(false);
                removed = true;
                D($"CloseFor: removed from middle stack panel={panel.name}");
                continue;
            }
            tmp.Push(p);
        }
        while (tmp.Count > 0) _stack.Push(tmp.Pop());
        if (_stack.Count == 0)
        {
            D("CloseFor: stack empty after removal -> CloseAll");
            CloseAll();
        }
    }

    /// <summary>
    /// 全て閉じる（ルート非表示、EyeArea復帰、全パネル非表示）。
    /// </summary>
    public void CloseAll()
    {
        foreach (var p in m_Panels)
        {
            if (p != null) p.SetActive(false);
        }
        if (m_Root != null) m_Root.SetActive(false);
        //if (m_EyeArea != null) m_EyeArea.SetActive(true);
        D("CloseAll: deactivated all panels and root=false");
    }

    private void EnsureRootOpen()
    {
        if (m_Root != null) m_Root.SetActive(true);
        //if (m_EyeArea != null) m_EyeArea.SetActive(false);
        D($"EnsureRootOpen: rootActive={(m_Root!=null ? m_Root.activeInHierarchy : false)}");
    }

    private void ActivateOnly(GameObject panel)
    {
        for (int i = 0; i < m_Panels.Count; i++)
        {
            var p = m_Panels[i];
            if (p == null) continue;
            p.SetActive(p == panel);
        }
        // リスト未登録でも表示は行う（ただし登録推奨）
        if (!m_Panels.Contains(panel))
        {
            panel.SetActive(true);
        }
        D($"ActivateOnly: active={panel?.name}, totalPanels={m_Panels.Count}");
    }
}
