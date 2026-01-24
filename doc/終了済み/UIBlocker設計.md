# UIBlocker設計

**状態:** 完了

## 概要

USERUIの操作をブロックするための統一的な仕組み。
バトル、ノベルパート、将来のシステムから同じ方法で使える。

## 関連ドキュメント

- [ノベルパート_USERUIとEyeArea連携.md](./ノベルパート_USERUIとEyeArea連携.md)
- [BusyOverlay.cs](../../Assets/Script/USERUI/BusyOverlay.cs) - 既存の参照カウント方式ブロック機構（未使用）

---

## 背景

### 現状の問題

1. **BusyOverlayが未使用**: 実装されているが実際には呼び出されていない
2. **タブ単位の制御ができない**: USERUI全体を覆う方式のみ
3. **長時間ブロックに対応していない**: using構文のみで、名前付きブロックがない

### ユースケース

| シーン | ブロック対象 | 期間 |
|--------|-------------|------|
| エンカウントズーム | 全タブ | 数秒（アニメーション中） |
| ノベルパート全体 | 全タブ | 長時間（会話終了まで） |
| 立ち絵トランジション | 全タブ | 数秒（アニメーション中） |
| 将来のシステム | 任意のタブ | 任意 |

---

## 設計

### BlockScope

```csharp
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
```

**重要:** タブ切り替え自体は常に可能。ブロックするのは各タブ内のコンテンツ操作のみ。

### UIBlocker クラス

```csharp
// Assets/Script/USERUI/UIBlocker.cs（新規）
using System;
using System.Collections.Generic;
using UnityEngine;
using R3;

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
    /// // アニメーション中だけブロック
    /// using (UIBlocker.Instance.Acquire(BlockScope.AllContents))
    /// {
    ///     await PlayTransitionAsync();
    /// }
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
```

---

## 使用例

### 短時間ブロック（アニメーション中）

```csharp
// エンカウントズーム中
using (UIBlocker.Instance.Acquire(BlockScope.AllContents))
{
    await wui.FirstImpressionZoomImproved();
}

// 立ち絵トランジション中
using (UIBlocker.Instance.Acquire(BlockScope.AllContents))
{
    await portraitPresenter.PlayTransitionAsync();
}

// Mainタブのみブロック
using (UIBlocker.Instance.Acquire(BlockScope.MainContent))
{
    await DoSomethingAsync();
}
```

### 長時間ブロック（システム全体）

```csharp
// ノベルパート全体
public async UniTask RunNovelPartAsync()
{
    UIBlocker.Instance.BeginBlock("NovelPart", BlockScope.AllContents);
    try
    {
        // 会話進行
        while (!isConversationEnd)
        {
            // トランジション中は追加でブロック（ネストOK）
            using (UIBlocker.Instance.Acquire(BlockScope.AllContents))
            {
                await PlayPortraitTransition();
            }

            await WaitForPlayerInput();
        }
    }
    finally
    {
        UIBlocker.Instance.EndBlock("NovelPart");
    }
}

// バトル全体
public async UniTask RunBattleAsync()
{
    UIBlocker.Instance.BeginBlock("Battle", BlockScope.AllContents);
    try
    {
        await ExecuteBattleLoop();
    }
    finally
    {
        UIBlocker.Instance.EndBlock("Battle");
    }
}
```

### R3による状態監視

```csharp
// ブロック状態の変化を監視
UIBlocker.Instance.IsMainBlocked.Subscribe(blocked =>
{
    Debug.Log($"MainContent blocked: {blocked}");
}).AddTo(this);
```

---

## シーン配置

### 現状（BusyOverlay）

```
USERUI/ToggleButtons
├── ConfigContent
├── CharaConfigContent
├── PlayerContent
├── BusyOverlay (BusyOverlay.cs)  ← 削除予定
│   └── panel (Image)
│       └── Text (TMP) "WAIT"
└── ToggleButtonGroup
```

### 変更後（UIBlocker）

```
USERUI/ToggleButtons
├── ConfigContent
│   ├── ... (既存のUI要素)
│   └── BlockerPanel (Image)      ← 新規追加（最前面）
│       └── Text (TMP) "WAIT"
├── CharaConfigContent
│   ├── ... (既存のUI要素)
│   └── BlockerPanel (Image)      ← 新規追加（最前面）
│       └── Text (TMP) "WAIT"
├── PlayerContent
│   ├── ... (既存のUI要素)
│   └── BlockerPanel (Image)      ← 新規追加（最前面）
│       └── Text (TMP) "WAIT"
├── UIBlocker (UIBlocker.cs)      ← 新規追加（シングルトン）
└── ToggleButtonGroup
```

### BlockerPanelの設定

既存のBusyOverlay/panelと同じ見た目:

| 項目 | 設定 |
|------|------|
| Image | raycastTarget = true、背景色（半透明など） |
| Text (TMP) | "WAIT" テキスト表示 |
| 初期状態 | active = false |
| 配置 | 各Contentの最後の子（Sibling順で最前面） |

### UIBlockerコンポーネントの設定

| SerializeField | 割り当て |
|----------------|---------|
| mainBlockerPanel | PlayerContent/BlockerPanel |
| configBlockerPanel | ConfigContent/BlockerPanel |
| charaConfigBlockerPanel | CharaConfigContent/BlockerPanel |

---

## BusyOverlayとの関係

| 項目 | BusyOverlay（既存） | UIBlocker（新規） |
|------|-------------------|-----------------|
| スコープ | USERUI全体のみ | タブ別 or 全タブ |
| 名前付きブロック | なし | あり |
| R3対応 | なし | あり |
| 使用状況 | 未使用 | 新規 |

**移行方針:**
- UIBlockerに完全移行
- BusyOverlay.cs と BusyOverlay GameObjectは削除

---

## 実装ファイル

### 新規作成

| ファイル | 内容 |
|---------|------|
| `Assets/Script/USERUI/UIBlocker.cs` | UIBlocker本体（BlockScope enumも含む） |

### 削除

| ファイル | 理由 |
|---------|------|
| `Assets/Script/USERUI/BusyOverlay.cs` | UIBlockerに完全移行 |

### シーン変更

| 変更内容 |
|---------|
| BusyOverlay GameObject削除 |
| UIBlocker GameObject追加（ToggleButtons直下） |
| 各Content（PlayerContent, ConfigContent, CharaConfigContent）にBlockerPanel追加 |

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-24 | 初版作成 |
| 2026-01-24 | UIBlocker.cs実装完了、BusyOverlay.cs削除 |
| 2026-01-24 | BattleInitializerにUIBlocker統合、動作確認完了 |
