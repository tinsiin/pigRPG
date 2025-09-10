using System;
using UnityEngine;

/// <summary>
/// 単一インスタンスの操作ブロック用オーバーレイ。
/// - このコンポーネントを「隠すオブジェクト（パネル）」にアタッチして使う想定です（同一GameObjectでOK）。
///   シリアライズされた <see cref="blockerPanel"/> へ対象パネル（全画面Imageなど）を割り当ててください（自分自身でも可）。
/// - パネル側は Image.raycastTarget=true 等で入力ブロックを可能にしてください。
/// - 参照カウントでネストに対応（複数箇所からの同時ブロック要求を安全に管理）。
/// </summary>
public class BusyOverlay : MonoBehaviour
{
    [Header("表示/非表示を切り替えるブロック用パネル（全画面 Image 等）")]
    [SerializeField] private GameObject blockerPanel;

    /// <summary>
    /// グローバルアクセス用のインスタンス。
    /// シーンに1つだけ配置する前提です。
    /// </summary>
    public static BusyOverlay Instance { get; private set; }

    // 参照カウント（Acquire/Show で +1、Dispose/Hide で -1）
    private int _lockCount = 0;

    private void Awake()
    {
        // シングルトン確立
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 初期状態は非表示（ただし blockerPanel に自身が割り当てられている場合は自分を消さない）
        if (blockerPanel != null)
        {
            if (blockerPanel == this.gameObject)
            {
                Debug.LogError("BusyOverlay: blockerPanel に自身が割り当てられています。初期非表示にすると自身が無効化されます。子オブジェクトのパネルを割り当ててください。", this);
                // 自身は消さずに、手動で子パネルを割り当て/非表示にしてください
            }
            else
            {
                SetActive(false);
            }
        }
    }


    /// <summary>
    /// スコープに基づいて一時的にブロックを有効化します。
    /// using パターンで確実に解除されます。
    /// 使用例（非同期処理の間だけブロック）:
    /// <code>
    /// using (BusyOverlay.Instance.Acquire())
    /// {
    ///     await DoSomethingAsync();
    /// }
    /// </code>
    /// 使用例（エンカウント〜ズーム演出の間だけブロック）:
    /// <code>
    /// using (BusyOverlay.Instance.Acquire())
    /// {
    ///     await wui.FirstImpressionZoomImproved();
    /// }
    /// </code>
    /// </summary>
    public IDisposable Acquire()
    {
        Show();
        return new Handle(this);
    }

    /// <summary>
    /// 明示的にブロックを開始します（参照カウント +1）。
    /// Show と Hide を組にして使う場合の使用例:
    /// <code>
    /// BusyOverlay.Instance.Show();
    /// try { await DoSomethingAsync(); }
    /// finally { BusyOverlay.Instance.Hide(); }
    /// </code>
    /// 使用例（ネスト: 2箇所から同時に要求された場合、両方の Hide/Dispose が呼ばれるまで非表示になりません）:
    /// <code>
    /// using (BusyOverlay.Instance.Acquire())
    /// {
    ///     BusyOverlay.Instance.Show();
    ///     try { await Task.WhenAll(A(), B()); }
    ///     finally { BusyOverlay.Instance.Hide(); }
    /// }
    /// </code>
    /// </summary>
    public void Show()
    {
        _lockCount++;
        Debug.Log($"[BusyOverlay] Show called. lockCount={_lockCount} panel={(blockerPanel!=null?blockerPanel.name:"<null>")}");
        if (_lockCount == 1)
        {
            SetActive(true);
        }
    }

    /// <summary>
    /// ブロックを1つ解除します（参照カウント -1、0 で非表示）。
    /// Acquire/Show に対応する解放として呼び出してください。
    /// 使用例（Show/Hideのペア）:
    /// <code>
    /// BusyOverlay.Instance.Show();
    /// try { await LoadAssetsAsync(); }
    /// finally { BusyOverlay.Instance.Hide(); }
    /// </code>
    /// </summary>
    public void Hide()
    {
        _lockCount = Math.Max(0, _lockCount - 1);
        Debug.Log($"[BusyOverlay] Hide called. lockCount={_lockCount} panel={(blockerPanel!=null?blockerPanel.name:"<null>")}");
        if (_lockCount == 0)
        {
            SetActive(false);
        }
    }

    /// <summary>
    /// パネルを直接切り替えます（内部用）。
    /// </summary>
    private void SetActive(bool on)
    {
        if (blockerPanel != null)
        {
            var beforeSelf = blockerPanel.activeSelf;
            var beforeHier = blockerPanel.activeInHierarchy;
            blockerPanel.SetActive(on);
            var afterSelf = blockerPanel.activeSelf;
            var afterHier = blockerPanel.activeInHierarchy;
            Debug.Log($"[BusyOverlay] SetActive({on}) panel={blockerPanel.name} self {beforeSelf}->{afterSelf} hier {beforeHier}->{afterHier}");
        }
        else
        {
            Debug.LogWarning("[BusyOverlay] SetActive called but blockerPanel is null", this);
        }
    }

    private sealed class Handle : IDisposable
    {
        private BusyOverlay _owner;
        public Handle(BusyOverlay owner) => _owner = owner;
        public void Dispose()
        {
            if (_owner != null)
            {
                _owner.Hide();
                _owner = null;
            }
        }
    }
}
