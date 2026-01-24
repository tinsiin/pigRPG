using System;
using System.Collections.Generic;
using UnityEngine;
using R3;

/// <summary>
/// ブロックする対象の範囲
/// </summary>
public enum BlockScope
{
    /// <summary>MainタブのPlayerContentのみ</summary>
    MainContent,

    /// <summary>Configタブのみ</summary>
    ConfigContent,

    /// <summary>CharaConfigタブのみ</summary>
    CharaConfigContent,

    /// <summary>3タブ全てのコンテンツ（タブ切り替えは可能）</summary>
    AllContents,
}

/// <summary>
/// USERUIの操作ブロックを管理するクラス。
/// - 参照カウント方式でネストしたブロック要求を安全に管理
/// - スコープ別（タブ別）のブロック制御
/// - 名前付きブロック（長時間用、デバッグしやすい）
/// </summary>
public class UIBlocker : MonoBehaviour
{
    public static UIBlocker Instance { get; private set; }

    [Header("ブロック用パネル（各タブのコンテンツを覆う）")]
    [SerializeField] private GameObject mainBlockerPanel;
    [SerializeField] private GameObject configBlockerPanel;
    [SerializeField] private GameObject charaConfigBlockerPanel;

    // スコープ別の参照カウント
    private readonly Dictionary<BlockScope, int> _lockCounts = new()
    {
        { BlockScope.MainContent, 0 },
        { BlockScope.ConfigContent, 0 },
        { BlockScope.CharaConfigContent, 0 },
    };

    // 名前付きブロックの管理（blockId → scope）
    private readonly Dictionary<string, BlockScope> _namedBlocks = new();

    // 状態変更の通知（R3）
    public ReadOnlyReactiveProperty<bool> IsMainBlocked => _isMainBlocked;
    public ReadOnlyReactiveProperty<bool> IsConfigBlocked => _isConfigBlocked;
    public ReadOnlyReactiveProperty<bool> IsCharaConfigBlocked => _isCharaConfigBlocked;

    private readonly ReactiveProperty<bool> _isMainBlocked = new(false);
    private readonly ReactiveProperty<bool> _isConfigBlocked = new(false);
    private readonly ReactiveProperty<bool> _isCharaConfigBlocked = new(false);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 初期状態は全て非表示
        SetPanelActive(BlockScope.MainContent, false);
        SetPanelActive(BlockScope.ConfigContent, false);
        SetPanelActive(BlockScope.CharaConfigContent, false);
    }

    #region 短時間ブロック（using構文）

    /// <summary>
    /// スコープに基づいて一時的にブロックを有効化します。
    /// using パターンで確実に解除されます。
    /// </summary>
    /// <example>
    /// <code>
    /// // アニメーション中だけブロック
    /// using (UIBlocker.Instance.Acquire(BlockScope.AllContents))
    /// {
    ///     await PlayTransitionAsync();
    /// }
    /// </code>
    /// </example>
    public IDisposable Acquire(BlockScope scope = BlockScope.AllContents)
    {
        if (scope == BlockScope.AllContents)
        {
            // 全タブをブロック
            IncrementLock(BlockScope.MainContent);
            IncrementLock(BlockScope.ConfigContent);
            IncrementLock(BlockScope.CharaConfigContent);
            return new AllContentsHandle(this);
        }
        else
        {
            IncrementLock(scope);
            return new SingleScopeHandle(this, scope);
        }
    }

    #endregion

    #region 長時間ブロック（名前付き）

    /// <summary>
    /// 名前付きでブロックを開始します。
    /// EndBlock で同じ blockId を指定して解除します。
    /// </summary>
    /// <example>
    /// <code>
    /// // ノベルパート全体をブロック
    /// UIBlocker.Instance.BeginBlock("NovelPart", BlockScope.AllContents);
    /// try
    /// {
    ///     await RunNovelPartAsync();
    /// }
    /// finally
    /// {
    ///     UIBlocker.Instance.EndBlock("NovelPart");
    /// }
    /// </code>
    /// </example>
    public void BeginBlock(string blockId, BlockScope scope = BlockScope.AllContents)
    {
        if (_namedBlocks.ContainsKey(blockId))
        {
            Debug.LogWarning($"[UIBlocker] BeginBlock: blockId '{blockId}' is already active. Ignoring.");
            return;
        }

        _namedBlocks[blockId] = scope;

        if (scope == BlockScope.AllContents)
        {
            IncrementLock(BlockScope.MainContent);
            IncrementLock(BlockScope.ConfigContent);
            IncrementLock(BlockScope.CharaConfigContent);
        }
        else
        {
            IncrementLock(scope);
        }

        Debug.Log($"[UIBlocker] BeginBlock: '{blockId}' scope={scope}");
    }

    /// <summary>
    /// 名前付きブロックを解除します。
    /// </summary>
    public void EndBlock(string blockId)
    {
        if (!_namedBlocks.TryGetValue(blockId, out var scope))
        {
            Debug.LogWarning($"[UIBlocker] EndBlock: blockId '{blockId}' not found. Ignoring.");
            return;
        }

        _namedBlocks.Remove(blockId);

        if (scope == BlockScope.AllContents)
        {
            DecrementLock(BlockScope.MainContent);
            DecrementLock(BlockScope.ConfigContent);
            DecrementLock(BlockScope.CharaConfigContent);
        }
        else
        {
            DecrementLock(scope);
        }

        Debug.Log($"[UIBlocker] EndBlock: '{blockId}'");
    }

    #endregion

    #region 状態確認

    /// <summary>
    /// 指定スコープがブロック中かどうかを返します。
    /// </summary>
    public bool IsBlocked(BlockScope scope)
    {
        if (scope == BlockScope.AllContents)
        {
            return _lockCounts[BlockScope.MainContent] > 0
                && _lockCounts[BlockScope.ConfigContent] > 0
                && _lockCounts[BlockScope.CharaConfigContent] > 0;
        }
        return _lockCounts[scope] > 0;
    }

    /// <summary>
    /// いずれかのスコープがブロック中かどうかを返します。
    /// </summary>
    public bool IsAnyBlocked()
    {
        return _lockCounts[BlockScope.MainContent] > 0
            || _lockCounts[BlockScope.ConfigContent] > 0
            || _lockCounts[BlockScope.CharaConfigContent] > 0;
    }

    #endregion

    #region 内部処理

    private void IncrementLock(BlockScope scope)
    {
        _lockCounts[scope]++;
        if (_lockCounts[scope] == 1)
        {
            SetPanelActive(scope, true);
            UpdateReactiveProperty(scope, true);
        }
    }

    private void DecrementLock(BlockScope scope)
    {
        _lockCounts[scope] = Math.Max(0, _lockCounts[scope] - 1);
        if (_lockCounts[scope] == 0)
        {
            SetPanelActive(scope, false);
            UpdateReactiveProperty(scope, false);
        }
    }

    private void SetPanelActive(BlockScope scope, bool active)
    {
        var panel = scope switch
        {
            BlockScope.MainContent => mainBlockerPanel,
            BlockScope.ConfigContent => configBlockerPanel,
            BlockScope.CharaConfigContent => charaConfigBlockerPanel,
            _ => null
        };

        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

    private void UpdateReactiveProperty(BlockScope scope, bool blocked)
    {
        switch (scope)
        {
            case BlockScope.MainContent:
                _isMainBlocked.Value = blocked;
                break;
            case BlockScope.ConfigContent:
                _isConfigBlocked.Value = blocked;
                break;
            case BlockScope.CharaConfigContent:
                _isCharaConfigBlocked.Value = blocked;
                break;
        }
    }

    #endregion

    #region Handle classes

    private sealed class SingleScopeHandle : IDisposable
    {
        private UIBlocker _owner;
        private BlockScope _scope;

        public SingleScopeHandle(UIBlocker owner, BlockScope scope)
        {
            _owner = owner;
            _scope = scope;
        }

        public void Dispose()
        {
            if (_owner != null)
            {
                _owner.DecrementLock(_scope);
                _owner = null;
            }
        }
    }

    private sealed class AllContentsHandle : IDisposable
    {
        private UIBlocker _owner;

        public AllContentsHandle(UIBlocker owner) => _owner = owner;

        public void Dispose()
        {
            if (_owner != null)
            {
                _owner.DecrementLock(BlockScope.MainContent);
                _owner.DecrementLock(BlockScope.ConfigContent);
                _owner.DecrementLock(BlockScope.CharaConfigContent);
                _owner = null;
            }
        }
    }

    #endregion
}
