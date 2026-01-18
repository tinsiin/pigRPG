# Phase 2 実装計画書: GateMarker / TrackConfig / Anchor

> **ステータス**: 🔒 **クローズ** - 実装完了（2025年1月17日）

> **目的**: ゼロトタイプ歩行システム設計書の「Phase 2」に該当する機能を実装し、固定門・順次解除・ループ歩数・巻き戻しを完成させる。

---

## 進捗状況

| フェーズ | 状態 | 備考 |
|----------|------|------|
| Phase 2-A: 基盤 | ✅ 完了 | TrackConfig, GateMarker, GateVisual, GatePositionSpec |
| Phase 2-B: 門ロジック | ✅ 完了 | GateRuntimeState, GateResolver |
| Phase 2-C: 見た目 | ✅ 完了 | CentralObjectPresenter拡張, ExitVisual |
| Phase 2-D: Anchor | ✅ 完了 | WalkAnchor, AnchorManager, RewindToAnchorEffect |
| Phase 2-E: Condition | ✅ 完了 | HasFlagCondition, AllGatesClearedCondition 等 |
| Phase 2-F: 統合 | ✅ 完了 | AreaController統合, 統一スルー操作 |
| Phase 2-G: 永続化 | ✅ 完了 | WalkProgressData拡張 |
| Phase 2-H: Progress UI | ✅ 完了 | ProgressCalculator, ProgressIndicatorUI |
| Phase 2-I: Gate Approach Button | ✅ 完了 | GateApproachButton, WalkApproachUI拡張 |

**最終更新**: 2025年1月17日 - Phase 2 全機能実装完了・クローズ

---

## 現状分析

### 実装済み (Phase 0-1)
| カテゴリ | 完成度 | 備考 |
|----------|--------|------|
| FlowGraph基盤 | 100% | FlowGraphSO, NodeSO, EdgeSO |
| Event Kernel | 100% | EventHost, EventRunner, WalkingEventUI |
| WalkCounters | 100% | globalSteps, nodeSteps, trackProgress |
| Presenter層 | 100% | CentralObjectPresenter, SideObjectPresenter |
| Encounter/SideObject | 100% | Resolver, State管理完備 |
| Battle統合 | 100% | IBattleRunner, UnityBattleRunner |

### 未実装 (Phase 2)
| 機能 | 優先度 | 設計書参照 |
|------|--------|-----------|
| **GateMarker** | 高 | ゼロトタイプ L132-143, L169-175 |
| **TrackConfig** | 高 | ゼロトタイプ L165 |
| **門/出口の見た目** | 高 | ゼロトタイプ L42-69 |
| **Anchor/Rewind機構** | 中 | ゼロトタイプ L122-129 |
| **具体Condition実装** | 中 | ノード設定完全一覧 L241-258 |
| **門と出口の連携** | 中 | ゼロトタイプ L146-154 |

---

## 実装スコープ

### スコープ内
1. GateMarker クラスと NodeSO への統合
2. TrackConfig による進捗管理
3. 中央オブジェクトPresenterの拡張（門/出口/背面画像）
4. 表示モード（HardBlock/SoftBlock/AutoTrigger）
5. 基本的な Condition 実装
6. 巻き戻しAnchor機構
7. Gate状態の永続化と巻き戻し対応
8. 歩数なしリフレッシュ（巻き戻し直後の再抽選のみ処理）
9. Exit候補UIの強制表示
10. ループ門（repeatable）のクールダウン後再出現
11. **統一スルー操作（クリック=接近、歩行ボタン=スルー）**
12. 門スルー処理（HandleGateSkipped + resetOnSkip）
13. 門失敗処理（resetOnFail 分離）
14. 出口スルー処理（HandleExitSkipped + nodeSteps リセット）
15. SaveData への永続化パス（Unity JsonUtility 対応）
16. シード/再現性の保存（NodeSeed, VarietyHistoryIndex）

### スコープ外（将来Phase）
- Overlay（天候・時間帯）
- SpawnSource（共有プール）
- WeightMod / CurveByCounter
- GraphViewエディタ拡張

---

## Step 1: TrackConfig の実装

### 1.1 新規クラス: `TrackConfig.cs`

**ファイル**: `Assets/Script/Walk/TrackConfig.cs`

```csharp
[System.Serializable]
public sealed class TrackConfig
{
    [SerializeField] private int length = 100;       // トラック全長
    [SerializeField] private int stepDelta = 1;      // 1歩あたりの進捗
    [SerializeField] private string progressKey;     // 進捗カウンタ名（オプション）

    public int Length => length;
    public int StepDelta => stepDelta;
    public string ProgressKey => progressKey;
    public bool HasConfig => length > 0;
    public bool HasProgressKey => !string.IsNullOrEmpty(progressKey);
}
```

### 1.2 TrackConfig の適用タイミングと対象

| タイミング | 処理内容 | 対象カウンタ |
|-----------|----------|-------------|
| **ノード進入時** | `trackProgress = 0` にリセット | `WalkCounters.TrackProgress` |
| **毎歩の進捗更新** | `trackProgress += stepDelta` | `WalkCounters.TrackProgress` |
| **progressKey指定時** | `context.CounterValues[progressKey] = trackProgress` | `GameContext.CounterValues` |
| **門失敗(ResetOnSkip)時** | リセット対象を選択（後述） | 選択されたカウンタ |

### 1.3 progressKey の用途

```csharp
// 歩行ループ内での更新処理
private void UpdateTrackProgress()
{
    var config = currentNode.TrackConfig;
    if (config == null || !config.HasConfig) return;

    // trackProgress を stepDelta 分進める
    context.Counters.AdvanceTrackProgress(config.StepDelta);

    // progressKey が指定されていれば GameContext のカウンタにも同期
    if (config.HasProgressKey)
    {
        context.CounterValues[config.ProgressKey] = context.Counters.TrackProgress;
    }
}
```

### 1.4 NodeSO への統合

**変更対象**: `Assets/Script/Walk/NodeSO.cs`

追加フィールド:
```csharp
[SerializeField] private TrackConfig trackConfig;
[SerializeField] private GateMarker[] gates;

public TrackConfig TrackConfig => trackConfig;
public GateMarker[] Gates => gates;
```

---

## Step 2: GateMarker の実装

### 2.1 新規クラス: `GateMarker.cs`

**ファイル**: `Assets/Script/Walk/Gate/GateMarker.cs`

```csharp
[System.Serializable]
public sealed class GateMarker
{
    // === 識別 ===
    [SerializeField] private string gateId;
    [SerializeField] private int order;                    // 解決順序（小さい方が先）

    // === 位置指定 ===
    [SerializeField] private GatePositionSpec positionSpec;

    // === 条件と結果 ===
    [SerializeField] private ConditionSO[] passConditions; // 通過条件
    [SerializeField] private EffectSO[] onPass;            // 通過時Effect
    [SerializeField] private EffectSO[] onFail;            // 失敗時Effect
    [SerializeField] private EventDefinitionSO gateEvent;  // 門イベント（オプション）
    [SerializeField] private GateEventTiming eventTiming;  // イベント発火タイミング

    // === 表示モード ===
    [SerializeField] private GateBlockingMode blockingMode = GateBlockingMode.HardBlock;

    // === 繰り返し制御 ===
    [SerializeField] private bool repeatable;
    [SerializeField] private int cooldownSteps;
    [SerializeField] private bool resetOnSkip = true;      // 歩行ボタンでスルー時に進捗リセット
    [SerializeField] private bool resetOnFail = true;      // 条件失敗時に進捗リセット
    [SerializeField] private GateResetTarget resetTarget;  // リセット対象

    // === 見た目 ===
    [SerializeField] private GateVisual visual;

    // プロパティ
    public string GateId => gateId;
    public int Order => order;
    public GatePositionSpec PositionSpec => positionSpec;
    public ConditionSO[] PassConditions => passConditions;
    public EffectSO[] OnPass => onPass;
    public EffectSO[] OnFail => onFail;
    public EventDefinitionSO GateEvent => gateEvent;
    public GateEventTiming EventTiming => eventTiming;
    public GateBlockingMode BlockingMode => blockingMode;
    public bool Repeatable => repeatable;
    public int CooldownSteps => cooldownSteps;
    public bool ResetOnSkip => resetOnSkip;
    public bool ResetOnFail => resetOnFail;
    public GateResetTarget ResetTarget => resetTarget;
    public GateVisual Visual => visual;
}
```

### 2.2 関連Enum/Struct

```csharp
public enum GateBlockingMode
{
    HardBlock,   // 歩行入力/サイド更新/遭遇を停止し門に専念
    SoftBlock    // 表示のみ、歩行は続行可能
}

/// <summary>
/// GateEvent の発火タイミング
/// </summary>
public enum GateEventTiming
{
    OnAppear,    // 門が表示された時
    OnPass,      // 通過成功時（onPass Effect の前）
    OnFail       // 通過失敗時（onFail Effect の前）
}

/// <summary>
/// ResetOnSkip 時のリセット対象
/// </summary>
public enum GateResetTarget
{
    NodeStepsOnly,       // nodeSteps のみリセット
    TrackProgressOnly,   // trackProgress のみリセット
    Both,                // nodeSteps と trackProgress 両方
    ProgressKeyOnly      // progressKey で指定されたカウンタのみ
}

[System.Serializable]
public struct GatePositionSpec
{
    public enum PositionType { AbsSteps, Percent, Range }

    [SerializeField] private PositionType type;
    [SerializeField] private int absSteps;      // type=AbsSteps用
    [SerializeField] private float percent;      // type=Percent用 (0-1)
    [SerializeField] private int rangeMin;       // type=Range用
    [SerializeField] private int rangeMax;       // type=Range用

    public PositionType Type => type;
    public int AbsSteps => absSteps;
    public float Percent => percent;
    public int RangeMin => rangeMin;
    public int RangeMax => rangeMax;

    /// <summary>
    /// 実際の位置を計算
    /// </summary>
    /// <param name="trackLength">トラック全長</param>
    /// <param name="nodeSeed">ノード単位のシード</param>
    /// <param name="gateId">ゲートID（Range型のseed分離用）</param>
    public int ResolvePosition(int trackLength, uint nodeSeed, string gateId)
    {
        switch (type)
        {
            case PositionType.AbsSteps:
                return absSteps;
            case PositionType.Percent:
                return Mathf.RoundToInt(trackLength * percent);
            case PositionType.Range:
                // gateId を混ぜて各ゲートで異なる位置を確定
                var combinedSeed = HashCombine(nodeSeed, gateId.GetHashCode());
                var rng = new System.Random((int)combinedSeed);
                return rng.Next(rangeMin, rangeMax + 1);
            default:
                return 0;
        }
    }

    private static uint HashCombine(uint seed, int value)
    {
        // FNV-1a 風のハッシュ合成
        unchecked
        {
            return (seed ^ (uint)value) * 16777619u;
        }
    }
}
```

---

## Step 3: 門/出口の見た目仕様

### 3.1 GateVisual 構造体

**ファイル**: `Assets/Script/Walk/Gate/GateVisual.cs`

```csharp
[System.Serializable]
public struct GateVisual
{
    // === 前面（門本体） ===
    [SerializeField] private Sprite sprite;
    [SerializeField] private Vector2 size;
    [SerializeField] private Vector2 offset;
    [SerializeField] private Color tint;

    // === 背面（門の装飾/出口の光） ===
    [SerializeField] private Sprite backSprite;
    [SerializeField] private Color backTint;
    [SerializeField] private Vector2 backOffset;
    [SerializeField] private Vector2 backSize;

    // === ラベル ===
    [SerializeField] private string label;

    // === アニメーション ===
    [SerializeField] private GateAppearAnimation appearAnim;
    [SerializeField] private GateHideAnimation hideAnim;
    [SerializeField] private string sfxOnAppear;
    [SerializeField] private string sfxOnPass;
    [SerializeField] private string sfxOnFail;

    // プロパティ
    public Sprite Sprite => sprite;
    public Vector2 Size => size;
    public Vector2 Offset => offset;
    public Color Tint => tint.a > 0f ? tint : Color.white;
    public bool HasSprite => sprite != null;

    public Sprite BackSprite => backSprite;
    public Color BackTint => backTint.a > 0f ? backTint : Color.white;
    public Vector2 BackOffset => backOffset;
    public Vector2 BackSize => backSize;
    public bool HasBackSprite => backSprite != null;

    public string Label => label;
    public GateAppearAnimation AppearAnim => appearAnim;
    public GateHideAnimation HideAnim => hideAnim;
    public string SfxOnAppear => sfxOnAppear;
    public string SfxOnPass => sfxOnPass;
    public string SfxOnFail => sfxOnFail;
}

public enum GateAppearAnimation
{
    None,
    FadeIn,
    ScaleUp,
    SlideFromTop
}

public enum GateHideAnimation
{
    None,
    FadeOut,
    ScaleDown,
    Explode
}
```

### 3.2 CentralObjectPresenter 拡張

**変更対象**: `Assets/Script/Walk/Presentation/CentralObjectPresenter.cs`

#### 追加要素

| 要素 | 用途 |
|------|------|
| `BackImage` | 門/出口の背面スプライト表示 |
| `Label (TMP_Text)` | 門名/出口名の表示 |
| `CanvasGroup` | フェード/ブロック制御 |
| `Button` | クリック判定（HardBlock時） |
| `DisplayMode` | 表示モード管理 |

#### 表示モード

```csharp
public enum CentralDisplayMode
{
    Hidden,      // 非表示
    HardBlock,   // 歩行停止・クリック待ち
    SoftBlock,   // 表示のみ・歩行続行
    AutoTrigger  // 表示と同時にイベント実行
}
```

#### 拡張メソッド

```csharp
public sealed class CentralObjectPresenter
{
    // 既存フィールド + 追加
    private Image backImage;
    private TMP_Text labelText;
    private CanvasGroup canvasGroup;
    private Button button;
    private CentralDisplayMode currentMode;

    // === 門表示 ===
    public void ShowGate(GateVisual visual, CentralDisplayMode mode)
    {
        EnsureViewObject();
        currentMode = mode;

        // 背面画像
        if (visual.HasBackSprite)
        {
            EnsureBackImage();
            backImage.sprite = visual.BackSprite;
            backImage.color = visual.BackTint;
            // backOffset, backSize適用
        }

        // 前面画像
        image.sprite = visual.HasSprite ? visual.Sprite : GetFallbackSprite();
        image.color = visual.Tint;
        rectTransform.sizeDelta = visual.Size;
        rectTransform.anchoredPosition = visual.Offset;

        // ラベル
        if (!string.IsNullOrEmpty(visual.Label))
        {
            EnsureLabelText();
            labelText.text = visual.Label;
        }

        // アニメーション
        PlayAppearAnimation(visual.AppearAnim);

        viewObject.SetActive(true);
    }

    // === 出口表示 ===
    public void ShowExit(ExitVisual visual, bool allGatesCleared)
    {
        // 門が全て解除された場合のみ出口を表示
        if (!allGatesCleared) return;

        ShowGate(visual.ToGateVisual(), CentralDisplayMode.HardBlock);
    }

    // === クリック or 歩行ボタン待機（統一スルー操作） ===
    /// <summary>
    /// HardBlock時の入力待機
    /// - クリック → Approached（接近/対話）
    /// - 歩行ボタン → Skipped（スルー）
    /// </summary>
    public async UniTask<CentralInteractionResult> WaitForInteraction(IWalkInputProvider walkInput)
    {
        if (currentMode != CentralDisplayMode.HardBlock)
            return CentralInteractionResult.Approached;

        EnsureButton();

        // クリックと歩行ボタンを同時に待機
        var clickTask = button.OnClickAsync(default);
        var walkTask = walkInput.WaitForWalkButtonAsync(default);

        var (winIndex, _, _) = await UniTask.WhenAny(clickTask, walkTask);

        return winIndex == 0
            ? CentralInteractionResult.Approached  // クリック → 接近
            : CentralInteractionResult.Skipped;    // 歩行ボタン → スルー
    }
}

/// <summary>
/// 門/出口への操作結果
/// </summary>
public enum CentralInteractionResult
{
    Approached,  // クリックで接近（対話モードへ）
    Skipped      // 歩行ボタンでスルー
}
```

### 3.3 IWalkInputProvider インターフェース

**ファイル**: `Assets/Script/Walk/IWalkInputProvider.cs`

```csharp
/// <summary>
/// 歩行入力の抽象化
/// 門/出口のスルー操作に使用
/// </summary>
public interface IWalkInputProvider
{
    /// <summary>
    /// 歩行ボタンの押下を待機
    /// </summary>
    UniTask WaitForWalkButtonAsync(CancellationToken ct);

    /// <summary>
    /// 現在歩行ボタンが押されているか
    /// </summary>
    bool IsWalkButtonPressed { get; }
}
```

### 3.4 出口の見た目（ExitVisual）

**ファイル**: `Assets/Script/Walk/ExitVisual.cs`

```csharp
[System.Serializable]
public struct ExitVisual
{
    [SerializeField] private Sprite sprite;          // 出口画像
    [SerializeField] private Sprite backSprite;      // 出口背面（光など）
    [SerializeField] private Vector2 size;
    [SerializeField] private Vector2 offset;
    [SerializeField] private Color tint;
    [SerializeField] private Color backTint;
    [SerializeField] private string label;           // "次のエリアへ" など

    public GateVisual ToGateVisual()
    {
        // GateVisualに変換して中央Presenterで表示
        return new GateVisual
        {
            sprite = this.sprite,
            size = this.size,
            offset = this.offset,
            tint = this.tint,
            backSprite = this.backSprite,
            backTint = this.backTint,
            label = this.label
        };
    }
}
```

### 3.4 見た目仕様のまとめ

#### 描画順序（後ろから前へ）
```
1. BackImage（門/出口の背面 - 光・装飾）
2. CentralObject（門/出口本体）
3. Label（門名/出口名 - オプション）
4. Button（HardBlock時のクリック判定）
```

#### Prefab構成（設計書準拠）
```
CentralObjectPresenter (RectTransform)
├── BackImage (Image)           // 背面スプライト
├── MainImage (Image)           // 門/出口本体
├── Label (TMP_Text)            // ラベル（オプション）
├── Button (Button)             // クリック判定
└── CanvasGroup                 // フェード/ブロック制御
```

---

## Step 4: GateResolver（門判定ロジック）

### 4.1 GateRuntimeState（永続化対応）

**ファイル**: `Assets/Script/Walk/Gate/GateRuntimeState.cs`

```csharp
/// <summary>
/// 門のランタイム状態（永続化対応）
/// </summary>
[System.Serializable]
public sealed class GateRuntimeState
{
    public string GateId;
    public int ResolvedPosition;
    public bool IsCleared;
    public int CooldownRemaining;
    public int FailCount;

    public GateRuntimeState() { }

    public GateRuntimeState(string gateId, int resolvedPosition)
    {
        GateId = gateId;
        ResolvedPosition = resolvedPosition;
        IsCleared = false;
        CooldownRemaining = 0;
        FailCount = 0;
    }

    /// <summary>
    /// スナップショット作成（巻き戻し用）
    /// </summary>
    public GateRuntimeState Clone()
    {
        return new GateRuntimeState
        {
            GateId = this.GateId,
            ResolvedPosition = this.ResolvedPosition,
            IsCleared = this.IsCleared,
            CooldownRemaining = this.CooldownRemaining,
            FailCount = this.FailCount
        };
    }
}
```

### 4.2 新規クラス: `GateResolver.cs`

**ファイル**: `Assets/Script/Walk/Gate/GateResolver.cs`

```csharp
public sealed class GateResolver
{
    private readonly Dictionary<string, GateRuntimeState> gateStates = new();

    /// <summary>
    /// ノード進入時の初期化
    /// </summary>
    public void InitializeForNode(NodeSO node, uint nodeSeed)
    {
        gateStates.Clear();
        if (node.Gates == null) return;

        var trackLength = node.TrackConfig?.Length ?? 100;
        foreach (var gate in node.Gates)
        {
            // gateId を混ぜて各ゲートで異なるseedを使用
            var position = gate.PositionSpec.ResolvePosition(trackLength, nodeSeed, gate.GateId);
            gateStates[gate.GateId] = new GateRuntimeState(gate.GateId, position);
        }
    }

    /// <summary>
    /// 保存済み状態から復元（再入場/巻き戻し用）
    /// </summary>
    public void RestoreFromSnapshot(Dictionary<string, GateRuntimeState> snapshot)
    {
        gateStates.Clear();
        foreach (var kvp in snapshot)
        {
            gateStates[kvp.Key] = kvp.Value.Clone();
        }
    }

    /// <summary>
    /// 現在の状態をスナップショットとして取得（永続化用）
    /// </summary>
    public Dictionary<string, GateRuntimeState> TakeSnapshot()
    {
        var snapshot = new Dictionary<string, GateRuntimeState>();
        foreach (var kvp in gateStates)
        {
            snapshot[kvp.Key] = kvp.Value.Clone();
        }
        return snapshot;
    }

    /// <summary>
    /// 次に判定すべき門を取得
    /// </summary>
    public GateMarker GetNextGate(NodeSO node, int trackProgress)
    {
        if (node.Gates == null) return null;

        // 位置に達していて未クリアの門を order 順で探す
        return node.Gates
            .Where(g => gateStates.TryGetValue(g.GateId, out var state) && !state.IsCleared)
            .Where(g => gateStates[g.GateId].ResolvedPosition <= trackProgress)
            .Where(g => gateStates[g.GateId].CooldownRemaining <= 0)
            .OrderBy(g => g.Order)
            .FirstOrDefault();
    }

    public bool AllGatesCleared(NodeSO node)
    {
        if (node.Gates == null || node.Gates.Length == 0) return true;
        return node.Gates.All(g =>
            gateStates.TryGetValue(g.GateId, out var state) && state.IsCleared);
    }

    /// <summary>
    /// 門をクリア状態にする
    /// repeatable=true の場合はクリアせずクールダウンを設定
    /// </summary>
    public void MarkCleared(GateMarker gate)
    {
        if (!gateStates.TryGetValue(gate.GateId, out var state)) return;

        if (gate.Repeatable)
        {
            // ループ門: クリアせずクールダウンを設定して再出現可能にする
            state.CooldownRemaining = gate.CooldownSteps;
            // IsCleared は false のまま
        }
        else
        {
            // 通常門: クリア状態にする
            state.IsCleared = true;
        }
    }

    public void MarkFailed(GateMarker gate)
    {
        if (!gateStates.TryGetValue(gate.GateId, out var state)) return;

        state.FailCount++;
        state.CooldownRemaining = gate.CooldownSteps;
    }

    /// <summary>
    /// 毎歩呼び出し: クールダウンを1減らす
    /// </summary>
    public void TickCooldowns()
    {
        foreach (var state in gateStates.Values)
        {
            if (state.CooldownRemaining > 0)
                state.CooldownRemaining--;
        }
    }

    public GateRuntimeState GetState(string gateId)
    {
        return gateStates.TryGetValue(gateId, out var state) ? state : null;
    }
}
```

---

## Step 5: Anchor / Rewind 機構

### 5.1 Anchor クラス（Gate状態を含む）

**ファイル**: `Assets/Script/Walk/Anchor/WalkAnchor.cs`

```csharp
[System.Serializable]
public sealed class WalkAnchor
{
    public string AnchorId { get; }
    public string NodeId { get; }
    public WalkCountersSnapshot CountersSnapshot { get; }
    public Dictionary<string, bool> FlagsSnapshot { get; }
    public Dictionary<string, int> CountersSnapshotMap { get; }
    public Dictionary<string, GateRuntimeState> GateStatesSnapshot { get; }  // 追加: 門状態
    public AnchorScope Scope { get; }

    public WalkAnchor(
        string anchorId,
        string nodeId,
        WalkCountersSnapshot counters,
        Dictionary<string, bool> flags,
        Dictionary<string, int> counterMap,
        Dictionary<string, GateRuntimeState> gateStates,
        AnchorScope scope)
    {
        AnchorId = anchorId;
        NodeId = nodeId;
        CountersSnapshot = counters;
        FlagsSnapshot = new Dictionary<string, bool>(flags);
        CountersSnapshotMap = new Dictionary<string, int>(counterMap);
        // 門状態のディープコピー
        GateStatesSnapshot = new Dictionary<string, GateRuntimeState>();
        foreach (var kvp in gateStates)
        {
            GateStatesSnapshot[kvp.Key] = kvp.Value.Clone();
        }
        Scope = scope;
    }
}

public enum AnchorScope
{
    Node,    // ノード内のみ有効
    Region,  // タグで括った範囲
    Graph    // グラフ全体
}

public enum RewindMode
{
    PositionOnly,      // 進捗位置だけ戻す
    PositionAndState   // 一部状態も復元（門の解除状態を含む）
}
```

### 5.2 AnchorManager（Gate状態復元対応）

**ファイル**: `Assets/Script/Walk/Anchor/AnchorManager.cs`

```csharp
public sealed class AnchorManager
{
    private readonly Dictionary<string, WalkAnchor> anchors = new();
    private readonly Stack<string> anchorHistory = new();

    public bool HasAnchor(string anchorId) => anchors.ContainsKey(anchorId);

    public void CreateAnchor(string anchorId, GameContext context, AnchorScope scope)
    {
        var anchor = new WalkAnchor(
            anchorId,
            context.WalkState.CurrentNodeId,
            context.Counters.TakeSnapshot(),
            new Dictionary<string, bool>(context.Flags),
            new Dictionary<string, int>(context.CounterValues),
            context.GateResolver.TakeSnapshot(),  // 門状態もスナップショット
            scope);

        anchors[anchorId] = anchor;
        anchorHistory.Push(anchorId);
    }

    public void RewindToAnchor(string anchorId, GameContext context, RewindMode mode)
    {
        if (!anchors.TryGetValue(anchorId, out var anchor)) return;

        // 位置の復元（常に実行）
        context.Counters.RestoreFrom(anchor.CountersSnapshot);
        context.WalkState.SetCurrentNodeId(anchor.NodeId);

        // 状態の復元（PositionAndStateの場合のみ）
        if (mode == RewindMode.PositionAndState)
        {
            foreach (var kvp in anchor.FlagsSnapshot)
            {
                context.Flags[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in anchor.CountersSnapshotMap)
            {
                context.CounterValues[kvp.Key] = kvp.Value;
            }
            // 門の解除状態を復元
            context.GateResolver.RestoreFromSnapshot(anchor.GateStatesSnapshot);
        }
    }

    public void JumpToAnchor(string anchorId, GameContext context)
    {
        // 位置のみジャンプ（状態は復元しない）
        RewindToAnchor(anchorId, context, RewindMode.PositionOnly);
    }

    public void ClearAnchorsInScope(AnchorScope scope)
    {
        var toRemove = anchors
            .Where(kvp => kvp.Value.Scope == scope)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            anchors.Remove(key);
        }
    }
}
```

### 5.3 巻き戻し用Effect

**ファイル**: `Assets/Script/Walk/Effects/RewindToAnchorEffect.cs`

```csharp
[CreateAssetMenu(menuName = "Walk/Effects/RewindToAnchor")]
public sealed class RewindToAnchorEffect : EffectSO
{
    [SerializeField] private string anchorId;
    [SerializeField] private RewindMode mode = RewindMode.PositionOnly;
    [SerializeField] private int fallbackSteps = 10; // アンカーがない場合の歩数巻き戻し
    [SerializeField] private bool triggerRefreshAfterRewind = true; // 巻き戻し後にリフレッシュ

    public override async UniTask Apply(GameContext context)
    {
        var anchorManager = context.AnchorManager;

        if (anchorManager.HasAnchor(anchorId))
        {
            anchorManager.RewindToAnchor(anchorId, context, mode);
        }
        else
        {
            // フォールバック: 固定歩数巻き戻し
            context.Counters.Rewind(fallbackSteps);
        }

        // 巻き戻し後の「歩数なしリフレッシュ」フラグを立てる
        if (triggerRefreshAfterRewind)
        {
            context.RequestRefreshWithoutStep = true;
        }

        await UniTask.CompletedTask;
    }
}
```

---

## Step 6: Condition 実装

### 6.1 基本Condition一覧

| クラス名 | 用途 |
|----------|------|
| `HasFlagCondition` | フラグ所持判定 |
| `HasCounterCondition` | カウンタ値判定 |
| `StepsCondition` | 歩数判定 |
| `ChanceCondition` | 確率判定 |
| `AllGatesClearedCondition` | 全門クリア判定 |
| `AndCondition` | AND合成 |
| `OrCondition` | OR合成 |
| `NotCondition` | 否定 |

### 6.2 実装例

**ファイル**: `Assets/Script/Walk/Conditions/HasFlagCondition.cs`

```csharp
[CreateAssetMenu(menuName = "Walk/Conditions/HasFlag")]
public sealed class HasFlagCondition : ConditionSO
{
    [SerializeField] private string flagKey;
    [SerializeField] private bool expectedValue = true;

    public override bool IsMet(GameContext context)
    {
        if (!context.Flags.TryGetValue(flagKey, out var value))
            return !expectedValue; // フラグがなければfalse扱い

        return value == expectedValue;
    }
}
```

**ファイル**: `Assets/Script/Walk/Conditions/AllGatesClearedCondition.cs`

```csharp
[CreateAssetMenu(menuName = "Walk/Conditions/AllGatesCleared")]
public sealed class AllGatesClearedCondition : ConditionSO
{
    public override bool IsMet(GameContext context)
    {
        var gateResolver = context.GateResolver;
        var currentNode = context.CurrentNode;
        return gateResolver.AllGatesCleared(currentNode);
    }
}
```

---

## Step 7: AreaController への統合

### 7.1 歩行サイクル更新（設計書準拠）

```
1) サイドオブジェクト更新（左右ペアの見た目更新）
2) 中央オブジェクト更新（門/出口の出現判定と背面描画）
3) 遭遇判定（BattleManager起動の有無を決定）
4) 遭遇が発生した場合は戦闘へ移行し、復帰後に同じサイド/中央へアプローチ選択
5) 進捗更新と次歩行へ
6) クールダウン更新（TickCooldowns）
```

### 7.2 AreaController 変更点

```csharp
// 追加フィールド
private GateResolver gateResolver;
private AnchorManager anchorManager;

// === 歩行ループ本体 ===
private async UniTask WalkStep()
{
    // 「歩数なしリフレッシュ」の場合はサイド/中央の再抽選のみ行い、
    // 歩数カウンタや遭遇判定をスキップ
    if (context.RequestRefreshWithoutStep)
    {
        context.RequestRefreshWithoutStep = false;
        await RefreshWithoutStep();
        return;
    }

    // 通常の歩行処理
    await UpdateSideObjects();
    await CheckGates();
    await CheckExit();
    await CheckEncounter();
    UpdateTrackProgress();

    // クールダウンを毎歩減らす
    gateResolver.TickCooldowns();
}

/// <summary>
/// 歩数を進めないリフレッシュ（巻き戻し直後用）
/// 設計書: 「サイド/中央の再抽選のみ、遭遇ロールはスキップ」
/// </summary>
private async UniTask RefreshWithoutStep()
{
    // サイドオブジェクトの再抽選
    await UpdateSideObjects();

    // 中央オブジェクトの再表示（門/出口の判定は行うが歩数は進めない）
    await CheckGates();
    await CheckExit();

    // 遭遇判定はスキップ
    // 歩数カウンタの更新もスキップ
    // クールダウンの更新もスキップ
}

// === 門チェック（統一スルー操作対応） ===
/// <summary>
/// 門の処理フロー:
/// 1. 門が出現位置に達しているか確認
/// 2. 門を表示
/// 3. 入力待機（クリック or 歩行ボタン）
///    - クリック → 接近して条件判定
///    - 歩行ボタン → スルー（resetOnSkip適用）
/// </summary>
private async UniTask CheckGates()
{
    var gate = gateResolver.GetNextGate(currentNode, context.Counters.TrackProgress);
    if (gate == null) return;

    // GateEvent: OnAppear タイミング
    if (gate.GateEvent != null && gate.EventTiming == GateEventTiming.OnAppear)
    {
        await eventHost.Trigger(gate.GateEvent);
    }

    // 門表示
    centralPresenter.ShowGate(gate.Visual,
        gate.BlockingMode == GateBlockingMode.HardBlock
            ? CentralDisplayMode.HardBlock
            : CentralDisplayMode.SoftBlock);

    // === 入力待機（クリック or 歩行ボタン） ===
    if (gate.BlockingMode == GateBlockingMode.HardBlock)
    {
        var interaction = await centralPresenter.WaitForInteraction(walkInput);

        if (interaction == CentralInteractionResult.Skipped)
        {
            // 歩行ボタン → スルー
            await HandleGateSkipped(gate);
            centralPresenter.Hide();
            return;
        }
        // クリック → 接近して条件判定へ進む
    }

    // === 条件判定（接近時のみ実行） ===
    var passed = gate.PassConditions == null ||
                 gate.PassConditions.Length == 0 ||
                 gate.PassConditions.All(c => c.IsMet(context));

    if (passed)
    {
        // GateEvent: OnPass タイミング
        if (gate.GateEvent != null && gate.EventTiming == GateEventTiming.OnPass)
        {
            await eventHost.Trigger(gate.GateEvent);
        }

        await ApplyEffects(gate.OnPass);
        gateResolver.MarkCleared(gate);
    }
    else
    {
        // GateEvent: OnFail タイミング
        if (gate.GateEvent != null && gate.EventTiming == GateEventTiming.OnFail)
        {
            await eventHost.Trigger(gate.GateEvent);
        }

        await ApplyEffects(gate.OnFail);
        gateResolver.MarkFailed(gate);

        // 条件失敗時もリセット（設計書: 失敗時は戻される）
        if (gate.ResetOnFail)
        {
            ApplyGateReset(gate.ResetTarget);
        }
    }

    centralPresenter.Hide();
}

/// <summary>
/// 門スルー時の処理
/// 設計書: 歩行ボタンで門をスルーした場合
/// </summary>
private async UniTask HandleGateSkipped(GateMarker gate)
{
    // スルー時のリセット処理
    if (gate.ResetOnSkip)
    {
        ApplyGateReset(gate.ResetTarget);
    }

    // GateEvent: OnSkip タイミング（オプション）
    // ※ 必要に応じて GateEventTiming.OnSkip を追加可能
}

/// <summary>
/// Gate失敗時のリセット処理
/// </summary>
private void ApplyGateReset(GateResetTarget target)
{
    switch (target)
    {
        case GateResetTarget.NodeStepsOnly:
            context.Counters.ResetNodeSteps();
            break;
        case GateResetTarget.TrackProgressOnly:
            context.Counters.ResetTrackProgress();
            break;
        case GateResetTarget.Both:
            context.Counters.ResetNodeSteps();
            context.Counters.ResetTrackProgress();
            break;
        case GateResetTarget.ProgressKeyOnly:
            var key = currentNode.TrackConfig?.ProgressKey;
            if (!string.IsNullOrEmpty(key))
            {
                context.CounterValues[key] = 0;
            }
            break;
    }
}

// === 出口判定（シグネチャ整合） ===
private async UniTask CheckExit()
{
    var allGatesCleared = gateResolver.AllGatesCleared(currentNode);
    if (!CanShowExit(allGatesCleared)) return;

    // 出口表示と選択UI
    await ShowExitAndSelectDestination();
}

/// <summary>
/// 出口出現判定（allGatesCleared を受け取る）
/// </summary>
private bool CanShowExit(bool allGatesCleared)
{
    return currentNode.ExitSpawn.ShouldSpawn(
        context.Counters.PeekNext(),
        allGatesCleared);
}

/// <summary>
/// 出口表示と選択UI（統一スルー操作対応）
/// 設計書: 「出口候補が1つでも選択UIを必ず表示する」
///
/// フロー（門と統一）:
/// 1. 出口の見た目を表示
/// 2. 入力待機（クリック or 歩行ボタン）
///    - クリック → 接近して選択UIへ
///    - 歩行ボタン → スルー（カウンタリセット）
/// 3. 選択UIを表示（接近時のみ）
/// 4. 選択して遷移
/// </summary>
private async UniTask ShowExitAndSelectDestination()
{
    var exits = currentNode.Exits;
    if (exits == null || exits.Length == 0) return;

    // 1. 出口の見た目を表示
    centralPresenter.ShowExit(currentNode.ExitVisual, allGatesCleared: true);

    // 2. 入力待機（クリック or 歩行ボタン）- 門と同じ操作モデル
    var interaction = await centralPresenter.WaitForInteraction(walkInput);

    if (interaction == CentralInteractionResult.Skipped)
    {
        // 歩行ボタン → スルー
        HandleExitSkipped();
        centralPresenter.Hide();
        return;
    }

    // 3. クリック → 接近して選択UIを表示
    var selectedExit = await ShowExitSelectionUI(exits);

    centralPresenter.Hide();

    // 4. 選択して遷移
    if (selectedExit != null)
    {
        await TransitionTo(selectedExit.ToNodeId);
    }
}

/// <summary>
/// 出口スルー時の処理
/// 設計書: 出口をスルーした場合のカウンタリセット
/// </summary>
private void HandleExitSkipped()
{
    var exitSpawn = currentNode.ExitSpawn;

    // Steps モードの場合、nodeSteps をリセットして出口の再出現を待つ
    if (exitSpawn.Mode == ExitSpawnMode.Steps)
    {
        context.Counters.ResetNodeSteps();
    }
    // Probability モードの場合は何もしない（毎歩確率判定のため）
}

/// <summary>
/// 出口選択UI（候補が1つでも表示）
/// ※スルー操作は入力待機レベル（WaitForInteraction）で処理済み
/// </summary>
private async UniTask<ExitCandidate> ShowExitSelectionUI(ExitCandidate[] exits)
{
    // 条件を満たす出口のみフィルタ
    var validExits = exits
        .Where(e => e.Conditions == null ||
                    e.Conditions.Length == 0 ||
                    e.Conditions.All(c => c.IsMet(context)))
        .ToArray();

    if (validExits.Length == 0)
    {
        return null;
    }

    // 1つでも選択UIを表示（設計書の仕様）
    return await exitSelectionUI.ShowAndSelect(validExits);
}

/// <summary>
/// TrackProgress の更新
/// </summary>
private void UpdateTrackProgress()
{
    var config = currentNode.TrackConfig;
    if (config == null || !config.HasConfig) return;

    // trackProgress を stepDelta 分進める
    context.Counters.AdvanceTrackProgress(config.StepDelta);

    // progressKey が指定されていれば GameContext のカウンタにも同期
    if (config.HasProgressKey)
    {
        context.CounterValues[config.ProgressKey] = context.Counters.TrackProgress;
    }
}
```

---

## Step 8: ExitSpawnRule 拡張

### 8.1 門との連携（シグネチャ統一）

**変更対象**: `Assets/Script/Walk/ExitSpawnRule.cs`

```csharp
[Serializable]
public sealed class ExitSpawnRule
{
    [SerializeField] private ExitSpawnMode mode = ExitSpawnMode.Steps;
    [SerializeField] private int steps = 1;
    [SerializeField] private float rate = 1f;
    [SerializeField] private bool requireAllGatesCleared = true;

    public ExitSpawnMode Mode => mode;
    public int Steps => steps;
    public float Rate => rate;
    public bool RequireAllGatesCleared => requireAllGatesCleared;

    /// <summary>
    /// 出口出現判定
    /// </summary>
    /// <param name="nextCounters">次ステップのカウンタ</param>
    /// <param name="allGatesCleared">全門クリア済みか</param>
    public bool ShouldSpawn(WalkCountersSnapshot nextCounters, bool allGatesCleared)
    {
        // 門クリアが必要な場合、全門クリアまで出口は出現しない
        if (requireAllGatesCleared && !allGatesCleared)
            return false;

        switch (mode)
        {
            case ExitSpawnMode.None:
                return false;
            case ExitSpawnMode.Steps:
                if (steps <= 0) return true;
                return nextCounters.NodeSteps >= steps;
            case ExitSpawnMode.Probability:
                return RandomEx.Shared.NextFloat(0f, 1f) < Mathf.Clamp01(rate);
            default:
                return false;
        }
    }
}
```

---

## Step 9: GameContext 拡張

### 9.1 追加フィールド

**変更対象**: `Assets/Script/Walk/GameContext.cs`

```csharp
public sealed class GameContext
{
    // 既存フィールド...

    // 追加
    public GateResolver GateResolver { get; }
    public AnchorManager AnchorManager { get; }
    public NodeSO CurrentNode { get; set; }

    /// <summary>
    /// 次の歩行を「歩数なしリフレッシュ」として実行するフラグ
    /// </summary>
    public bool RequestRefreshWithoutStep { get; set; }
}
```

---

## Step 10: SaveData 永続化パス

### 10.1 WalkProgressData への統合

**変更対象**: `Assets/Script/Walk/WalkProgressData.cs`（既存または新規）

```csharp
/// <summary>
/// 歩行進捗のセーブデータ
/// </summary>
[System.Serializable]
public sealed class WalkProgressData
{
    // 既存フィールド
    public string CurrentNodeId;
    public int GlobalSteps;
    public int NodeSteps;
    public int TrackProgress;
    public Dictionary<string, int> CounterValues;
    public Dictionary<string, bool> Flags;

    // === Phase 2 追加 ===
    public List<GateRuntimeStateData> GateStates;
    public List<WalkAnchorData> Anchors;
}

/// <summary>
/// GateRuntimeState のシリアライズ用
/// </summary>
[System.Serializable]
public sealed class GateRuntimeStateData
{
    public string GateId;
    public int ResolvedPosition;
    public bool IsCleared;
    public int CooldownRemaining;
    public int FailCount;

    public GateRuntimeStateData() { }

    public GateRuntimeStateData(GateRuntimeState state)
    {
        GateId = state.GateId;
        ResolvedPosition = state.ResolvedPosition;
        IsCleared = state.IsCleared;
        CooldownRemaining = state.CooldownRemaining;
        FailCount = state.FailCount;
    }

    public GateRuntimeState ToRuntimeState()
    {
        return new GateRuntimeState
        {
            GateId = this.GateId,
            ResolvedPosition = this.ResolvedPosition,
            IsCleared = this.IsCleared,
            CooldownRemaining = this.CooldownRemaining,
            FailCount = this.FailCount
        };
    }
}

/// <summary>
/// Unity JsonUtility 対応のシリアライズ用 Pair
/// ※ KeyValuePair は Unity でシリアライズ不可
/// </summary>
[System.Serializable]
public struct StringBoolPair
{
    public string Key;
    public bool Value;

    public StringBoolPair(string key, bool value)
    {
        Key = key;
        Value = value;
    }
}

[System.Serializable]
public struct StringIntPair
{
    public string Key;
    public int Value;

    public StringIntPair(string key, int value)
    {
        Key = key;
        Value = value;
    }
}

/// <summary>
/// WalkAnchor のシリアライズ用
/// </summary>
[System.Serializable]
public sealed class WalkAnchorData
{
    public string AnchorId;
    public string NodeId;
    public int GlobalSteps;
    public int NodeSteps;
    public int TrackProgress;
    public List<StringBoolPair> Flags;      // ← KeyValuePair から変更
    public List<StringIntPair> CounterValues;  // ← KeyValuePair から変更
    public List<GateRuntimeStateData> GateStates;
    public AnchorScope Scope;

    /// <summary>
    /// Dictionary から変換
    /// </summary>
    public static WalkAnchorData FromAnchor(WalkAnchor anchor)
    {
        var data = new WalkAnchorData
        {
            AnchorId = anchor.AnchorId,
            NodeId = anchor.NodeId,
            GlobalSteps = anchor.CountersSnapshot.GlobalSteps,
            NodeSteps = anchor.CountersSnapshot.NodeSteps,
            TrackProgress = anchor.CountersSnapshot.TrackProgress,
            Flags = new List<StringBoolPair>(),
            CounterValues = new List<StringIntPair>(),
            GateStates = new List<GateRuntimeStateData>(),
            Scope = anchor.Scope
        };

        foreach (var kvp in anchor.FlagsSnapshot)
            data.Flags.Add(new StringBoolPair(kvp.Key, kvp.Value));

        foreach (var kvp in anchor.CountersSnapshotMap)
            data.CounterValues.Add(new StringIntPair(kvp.Key, kvp.Value));

        foreach (var kvp in anchor.GateStatesSnapshot)
            data.GateStates.Add(new GateRuntimeStateData(kvp.Value));

        return data;
    }

    /// <summary>
    /// Dictionary に変換
    /// </summary>
    public Dictionary<string, bool> ToFlagsDictionary()
    {
        var dict = new Dictionary<string, bool>();
        foreach (var pair in Flags)
            dict[pair.Key] = pair.Value;
        return dict;
    }

    public Dictionary<string, int> ToCountersDictionary()
    {
        var dict = new Dictionary<string, int>();
        foreach (var pair in CounterValues)
            dict[pair.Key] = pair.Value;
        return dict;
    }
}
```

### 10.2 セーブ/ロードのエントリポイント

```csharp
public sealed class WalkProgressManager
{
    /// <summary>
    /// 現在の進捗をセーブデータに変換
    /// </summary>
    public WalkProgressData CreateSaveData(GameContext context)
    {
        var data = new WalkProgressData
        {
            CurrentNodeId = context.WalkState.CurrentNodeId,
            GlobalSteps = context.Counters.GlobalSteps,
            NodeSteps = context.Counters.NodeSteps,
            TrackProgress = context.Counters.TrackProgress,
            CounterValues = new Dictionary<string, int>(context.CounterValues),
            Flags = new Dictionary<string, bool>(context.Flags),
            GateStates = new List<GateRuntimeStateData>(),
            Anchors = new List<WalkAnchorData>()
        };

        // GateResolver の状態をシリアライズ
        var gateSnapshot = context.GateResolver.TakeSnapshot();
        foreach (var kvp in gateSnapshot)
        {
            data.GateStates.Add(new GateRuntimeStateData(kvp.Value));
        }

        // AnchorManager の状態をシリアライズ
        data.Anchors = context.AnchorManager.ExportAnchors();

        return data;
    }

    /// <summary>
    /// セーブデータから進捗を復元
    /// </summary>
    public void LoadFromSaveData(GameContext context, WalkProgressData data)
    {
        context.WalkState.SetCurrentNodeId(data.CurrentNodeId);
        context.Counters.Restore(data.GlobalSteps, data.NodeSteps, data.TrackProgress);
        context.CounterValues = new Dictionary<string, int>(data.CounterValues);
        context.Flags = new Dictionary<string, bool>(data.Flags);

        // GateResolver の状態を復元
        var gateSnapshot = new Dictionary<string, GateRuntimeState>();
        foreach (var stateData in data.GateStates)
        {
            gateSnapshot[stateData.GateId] = stateData.ToRuntimeState();
        }
        context.GateResolver.RestoreFromSnapshot(gateSnapshot);

        // AnchorManager の状態を復元
        context.AnchorManager.ImportAnchors(data.Anchors);
    }
}
```

### 10.3 シード/再現性の保存

```csharp
/// <summary>
/// WalkProgressData への追加フィールド（再現性保証用）
/// </summary>
[System.Serializable]
public sealed class WalkProgressData
{
    // ... 既存フィールド ...

    // === 再現性保証 ===
    public uint NodeSeed;             // 現在ノードのシード（Range型門の位置決定用）
    public int VarietyHistoryIndex;   // 遭遇バリエーション履歴のインデックス
}
```

**用途**:
- `NodeSeed`: Range型のGatePositionSpecで同じ位置を再現
- `VarietyHistoryIndex`: 同じ遭遇パターンを再現（既存のバリエーション管理と連携）

### 10.4 セーブタイミング

| タイミング | 処理 |
|-----------|------|
| ノード遷移時 | 遷移完了後に自動セーブ |
| 戦闘終了時 | 戦闘報酬確定後に自動セーブ |
| 手動セーブ | メニューからの明示的セーブ |
| アプリ中断時 | OnApplicationPause でクイックセーブ |

---

## Step 11: Progress Indicator UI（ゲート/出口進捗表示）

### 11.1 概要

ゲートと出口の進捗状態を統一UIで表示するシステム。次のゲート/出口までの残り歩数や確率をリアルタイムで更新。

**表示例**:
- 進捗シーケンス: `1 → 2 → 3 → (Exit)`
- クリア後: `[1] → 2 → 3 → (Exit)`
- 残り歩数: `Gate 1: 20歩先` または `Exit: 30%`

### 11.2 新規ファイル

#### ProgressEntry.cs

**ファイル**: `Assets/Script/Walk/Progress/ProgressEntry.cs`

```csharp
/// <summary>
/// 進捗表示用のエントリ（ゲートまたは出口）
/// </summary>
public readonly struct ProgressEntry
{
    public enum EntryType { Gate, Exit }

    public EntryType Type { get; }
    public int Order { get; }           // 表示順序
    public string Id { get; }           // GateId or "exit"
    public int Position { get; }        // 発動位置（歩数）
    public bool IsCleared { get; }      // クリア済み
    public bool IsActive { get; }       // 現在アクティブ
    public bool IsCoolingDown { get; }  // クールダウン中
    public string DisplayLabel { get; } // 表示ラベル

    // ファクトリメソッド
    public static ProgressEntry CreateGate(int order, string gateId, int position,
        bool isCleared, bool isActive, bool isCoolingDown);
    public static ProgressEntry CreateExit(int order, int position);
}
```

#### ProgressSnapshot.cs

**ファイル**: `Assets/Script/Walk/Progress/ProgressSnapshot.cs`

```csharp
/// <summary>
/// 進捗状態のスナップショット
/// </summary>
public readonly struct ProgressSnapshot
{
    public IReadOnlyList<ProgressEntry> Entries { get; }
    public int CurrentTrackProgress { get; }
    public int? NextEntryIndex { get; }
    public int? StepsToNextEntry { get; }       // null = 確率モード
    public float? ProbabilityOfNextEntry { get; }
    public bool AllGatesCleared { get; }
    public ExitSpawnMode ExitMode { get; }
    public int RemainingGateCount { get; }

    public bool HasNextEntry => NextEntryIndex.HasValue;
    public ProgressEntry? GetNextEntry();

    public static ProgressSnapshot Empty { get; }
}
```

#### ProgressCalculator.cs

**ファイル**: `Assets/Script/Walk/Progress/ProgressCalculator.cs`

```csharp
/// <summary>
/// ゲートと出口の進捗状態を計算
/// </summary>
public sealed class ProgressCalculator
{
    /// <summary>
    /// 進捗スナップショットを計算
    /// </summary>
    public ProgressSnapshot Calculate(
        NodeSO node,
        GateResolver gateResolver,
        WalkCounters counters);
}
```

**計算ロジック**:
1. GateResolverから全ゲート状態を取得
2. orderでソートしてエントリリストを作成
3. 各ゲートのIsActive, IsCoolingDown状態を判定
4. 出口を最後のエントリとして追加
5. 次のエントリまでの歩数/確率を計算

#### ProgressIndicatorUI.cs

**ファイル**: `Assets/Script/Walk/UI/ProgressIndicatorUI.cs`

```csharp
/// <summary>
/// ゲートと出口の進捗を表示するUI
/// </summary>
public sealed class ProgressIndicatorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text progressText;        // "1 → 2 → 3 → (Exit)"
    [SerializeField] private TMP_Text stepsRemainingText;  // "Gate 1: 5歩先"

    public void UpdateDisplay(ProgressSnapshot snapshot);
    public void Clear();
}
```

**表示フォーマット**:

| 状態 | progressText | stepsRemainingText |
|------|--------------|-------------------|
| 初期 | `1 → 2 → 3 → (Exit)` | `Gate 1: 20歩先` |
| Gate1クリア | `[1] → 2 → 3 → (Exit)` | `Gate 2: 10歩先` |
| クールダウン中 | `(1) → 2 → (Exit)` | `Gate 2: 5歩先` |
| 確率モード | `1 → 2 → (Exit)` | `Exit: 30%` |
| Exitブロック中 | `1 → 2 → (Exit)` | `Exit: Gate残り2` |

### 11.3 GateResolver拡張

**変更対象**: `Assets/Script/Walk/Gate/GateResolver.cs`

```csharp
/// <summary>
/// 全ゲートの状態を取得（Progress UI用）
/// </summary>
public IEnumerable<(GateMarker marker, GateRuntimeState state)> GetAllStates(NodeSO node)
{
    if (node.Gates == null) yield break;

    foreach (var gate in node.Gates)
    {
        if (gateStates.TryGetValue(gate.GateId, out var state))
        {
            yield return (gate, state);
        }
    }
}
```

### 11.4 AreaController統合

**変更対象**: `Assets/Script/Walk/AreaController.cs`

```csharp
// 追加フィールド
private ProgressCalculator progressCalculator = new();
private Action<ProgressSnapshot> onProgressChanged;

// コールバック設定
public void SetProgressCallback(Action<ProgressSnapshot> callback)
{
    onProgressChanged = callback;
}

// 現在の進捗取得
public ProgressSnapshot GetCurrentProgress()
{
    return progressCalculator.Calculate(currentNode, gateResolver, context.Counters);
}

// 進捗更新通知
private void NotifyProgressChanged()
{
    var snapshot = progressCalculator.Calculate(currentNode, gateResolver, context.Counters);
    onProgressChanged?.Invoke(snapshot);
}
```

**呼び出しタイミング**:
- `WalkStep()`の開始時（Advance後）
- ゲート通過/失敗後
- 出口表示時
- ノード遷移時

### 11.5 WalkingSystemManager統合

**変更対象**: `Assets/Script/Walk/WalkingSystemManager.cs`

```csharp
[SerializeField] private ProgressIndicatorUI progressUI;

private void InitializeIfReady()
{
    // ... 既存の初期化 ...

    if (progressUI != null)
    {
        areaController.SetProgressCallback(OnProgressChanged);
        // 初期状態をUIに反映
        var initialSnapshot = areaController.GetCurrentProgress();
        progressUI.UpdateDisplay(initialSnapshot);
    }
}

private void OnProgressChanged(ProgressSnapshot snapshot)
{
    if (progressUI != null)
    {
        progressUI.UpdateDisplay(snapshot);
    }
}
```

### 11.6 バグ修正履歴

**問題1: 初期表示でExitのみ表示される**
- 原因: GateResolverがWalkStep()前に初期化されていなかった
- 修正: AreaControllerのコンストラクタでInitializeGateResolverForCurrentNode()を呼び出し

**問題2: 歩行ごとに2歩進む**
- 原因: UpdateTrackProgress()でStepDeltaを加算 + Advance(1)で二重カウント
- 修正: UpdateTrackProgress()で(StepDelta - 1)のみ追加

**問題3: 最初の一歩で進まない**
- 原因: WalkStep()がasyncで、Advance(1)がHandleApproach()の後に実行されていた
- 修正: Advance(1)をWalkStep()の冒頭に移動

---

## Step 12: Gate/Exit Approach Button（ゲート/出口アプローチボタン）

### 12.1 概要

ゲートと出口に対して、サイドオブジェクト・中央オブジェクトと同様のアプローチボタンを実装する。

**設計方針:**
- 既存の `ApproachLeftButton`, `ApproachRightButton`, `ApproachCenterButton` と同じ構造
- 同じ `WalkApproachUI` を拡張して管理
- ゲート/出口共通のボタン（`GateApproachButton`）
- 抽象的な実装（ボタンクリック→アプローチ、歩行ボタン→スキップ）

### 12.2 既存構造との比較

| 要素 | サイド/中央オブジェクト | ゲート/出口（現行） | ゲート/出口（新規） |
|------|------------------------|---------------------|---------------------|
| 表示 | SideObjectPresenter / CentralObjectPresenter | CentralObjectPresenter.ShowGate() | 同左 |
| 待機 | WalkApproachUI.WaitForSelection() | WaitForInteraction() | WalkApproachUI.WaitForGateSelection() |
| ボタン | ApproachLeftButton等 | 画像自体にButton追加 | GateApproachButton |
| スキップ | TrySkip() → Skip選択 | 歩行ボタン待機 | TrySkip() → Skip選択 |

### 12.3 ApproachChoice 拡張

**変更対象**: `Assets/Script/Walk/UI/WalkApproachUI.cs`

```csharp
public enum ApproachChoice
{
    None,
    Skip,
    Left,
    Right,
    Center,
    Gate    // ← 追加: ゲート/出口アプローチ
}
```

### 12.4 WalkApproachUI 拡張

**変更対象**: `Assets/Script/Walk/UI/WalkApproachUI.cs`

```csharp
public sealed class WalkApproachUI : MonoBehaviour
{
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button centerButton;
    [SerializeField] private Button gateButton;    // ← 追加

    private TMP_Text leftLabel;
    private TMP_Text rightLabel;
    private TMP_Text centerLabel;
    private TMP_Text gateLabel;    // ← 追加

    // ... 既存コード ...

    /// <summary>
    /// ゲート/出口用の選択待機
    /// </summary>
    public UniTask<ApproachChoice> WaitForGateSelection(string labelText)
    {
        ResolveButtons();
        SetGateLabel(labelText);
        SetGateActive(true);

        pending = new UniTaskCompletionSource<ApproachChoice>();
        isAwaiting = true;
        return AwaitGateSelection();
    }

    private async UniTask<ApproachChoice> AwaitGateSelection()
    {
        var result = await pending.Task;
        SetGateActive(false);
        isAwaiting = false;
        pending = null;
        return result;
    }

    private void ResolveButtons()
    {
        // ... 既存コード ...
        if (gateButton == null) gateButton = FindButton("GateApproachButton");
        gateLabel = ResolveLabel(gateButton, gateLabel);
    }

    private void HookButtons()
    {
        // ... 既存コード ...
        if (gateButton != null)
        {
            gateButton.onClick.RemoveAllListeners();
            gateButton.onClick.AddListener(() => Resolve(ApproachChoice.Gate));
        }
    }

    private void SetGateLabel(string text)
    {
        if (gateLabel != null) gateLabel.text = string.IsNullOrEmpty(text) ? "アプローチ" : text;
    }

    private void SetGateActive(bool active)
    {
        if (gateButton != null) gateButton.gameObject.SetActive(active);
    }
}
```

### 12.5 AreaController 統合

**変更対象**: `Assets/Script/Walk/AreaController.cs`

```csharp
/// <summary>
/// 門チェック（アプローチボタン対応版）
/// </summary>
private async UniTask CheckGates()
{
    var gate = gateResolver.GetNextGate(currentNode, context.Counters.TrackProgress);
    if (gate == null) return;

    // GateEvent: OnAppear タイミング
    if (gate.GateEvent != null && gate.EventTiming == GateEventTiming.OnAppear)
    {
        await eventHost.Trigger(gate.GateEvent);
    }

    // 門表示
    centralPresenter.ShowGate(gate.Visual,
        gate.BlockingMode == GateBlockingMode.HardBlock
            ? CentralDisplayMode.HardBlock
            : CentralDisplayMode.SoftBlock);

    // === アプローチボタンで入力待機 ===
    if (gate.BlockingMode == GateBlockingMode.HardBlock && approachUI != null)
    {
        var choice = await approachUI.WaitForGateSelection(gate.Visual.Label ?? "門");

        if (choice == ApproachChoice.Skip)
        {
            // 歩行ボタン → スルー
            await HandleGateSkipped(gate);
            centralPresenter.Hide();
            return;
        }
        // GateApproachButton クリック → 条件判定へ進む
    }

    // === 条件判定（アプローチ時のみ実行） ===
    var passed = gate.PassConditions == null ||
                 gate.PassConditions.Length == 0 ||
                 gate.PassConditions.All(c => c.IsMet(context));

    if (passed)
    {
        // ... 通過処理 ...
    }
    else
    {
        // ... 失敗処理 ...
    }

    centralPresenter.Hide();
}

/// <summary>
/// 出口チェック（アプローチボタン対応版）
/// </summary>
private async UniTask ShowExitAndSelectDestination()
{
    var exits = currentNode.Exits;
    if (exits == null || exits.Length == 0) return;

    // 出口表示
    centralPresenter.ShowExit(currentNode.ExitVisual, allGatesCleared: true);

    // === アプローチボタンで入力待機 ===
    if (approachUI != null)
    {
        var choice = await approachUI.WaitForGateSelection("出口");

        if (choice == ApproachChoice.Skip)
        {
            // 歩行ボタン → スルー
            HandleExitSkipped();
            centralPresenter.Hide();
            return;
        }
        // GateApproachButton クリック → 選択UIへ
    }

    // 選択UIを表示
    var selectedExit = await ShowExitSelectionUI(exits);
    // ...
}
```

### 12.6 シーン配置（MCP）

**GateApproachButton の配置:**

| プロパティ | 値 |
|-----------|-----|
| 名前 | GateApproachButton |
| 親 | WalkObject |
| localPosition | (0, 250, 0) |
| サイズ | (160, 50) |
| コンポーネント | RectTransform, CanvasRenderer, Image, Button |
| 子要素 | Text (TMP) - TextMeshProUGUI |

**配置手順:**
```
1. WalkObject の子として GateApproachButton を作成
2. Button コンポーネント追加
3. 子要素として Text (TMP) を作成
4. WalkApproachUI の SerializeField に登録（オプション）
```

### 12.7 設計のポイント

**抽象的な実装:**
- ボタンは「アプローチ」か「スキップ」かの選択のみを担当
- イベントの中身（条件判定、エフェクト実行）は `AreaController` と `EventHost` に委譲
- サイド/中央オブジェクトと同じパターン

**フロー図:**
```
門/出口表示
    ↓
WalkApproachUI.WaitForGateSelection()
    ├─ GateApproachButton クリック → ApproachChoice.Gate
    │   └→ 条件判定 / 選択UI表示
    │
    └─ 歩行ボタン（TrySkip） → ApproachChoice.Skip
        └→ HandleGateSkipped / HandleExitSkipped
```

---

## 実装順序

### Phase 2-A: 基盤 ✅
1. ✅ `TrackConfig.cs` 新規作成
2. ✅ `GateMarker.cs` 新規作成（GateEventTiming, GateResetTarget 含む）
3. ✅ `GateVisual.cs` 新規作成
4. ✅ `GatePositionSpec` 実装（Seed分離対応）
5. ✅ `NodeSO.cs` 拡張

### Phase 2-B: 門ロジック ✅
6. ✅ `GateRuntimeState.cs` 新規作成（Clone メソッド付き）
7. ✅ `GateResolver.cs` 新規作成（TakeSnapshot/RestoreFromSnapshot 含む）

### Phase 2-C: 見た目 ✅
8. ✅ `CentralObjectPresenter` 拡張（BackImage, Label, Button）
9. ✅ `ExitVisual.cs` 新規作成
10. ✅ 表示モード（HardBlock/SoftBlock/AutoTrigger）実装

### Phase 2-D: Anchor ✅
11. ✅ `WalkAnchor.cs` 新規作成（GateStatesSnapshot 含む）
12. ✅ `AnchorManager.cs` 新規作成（Gate状態復元対応）
13. ✅ `RewindToAnchorEffect.cs` 新規作成（triggerRefreshAfterRewind 含む）

### Phase 2-E: Condition ✅
14. ✅ `HasFlagCondition.cs`
15. ✅ `HasCounterCondition.cs`
16. ✅ `AllGatesClearedCondition.cs`
17. ✅ 合成Condition（And/Or/Not）

### Phase 2-F: 統合 ✅
18. ✅ `GameContext` 拡張（RequestRefreshWithoutStep 追加）
19. ✅ `ExitSpawnRule` シグネチャ修正
20. ✅ `IWalkInputProvider` インターフェース新規作成
21. ✅ `CentralInteractionResult` enum 新規作成
22. ✅ `AreaController` 統合
    - WalkStep に TickCooldowns 追加
    - RefreshWithoutStep 実装
    - **WaitForInteraction 統一スルー操作**
    - CheckGates に GateEvent 発火ロジック追加
    - CheckGates に HandleGateSkipped 追加（歩行ボタンスルー）
    - MarkCleared に repeatable 対応追加
    - resetOnSkip / resetOnFail 分離
    - CanShowExit シグネチャ修正
    - ShowExitAndSelectDestination に WaitForInteraction 追加
    - HandleExitSkipped（スルー時カウンタリセット）
    - ApplyGateReset 実装
23. ✅ テストノード作成・動作確認

### Phase 2-G: 永続化 ✅
24. ✅ `WalkProgressData` 拡張（GateStates, Anchors, NodeSeed 追加）
25. ✅ `GateRuntimeStateData` シリアライズ用クラス新規作成
26. ✅ `StringBoolPair` / `StringIntPair` シリアライズ用struct新規作成
27. ✅ `WalkAnchorData` シリアライズ用クラス新規作成
28. ✅ `WalkProgressManager` セーブ/ロード実装
29. ✅ セーブタイミングの組み込み

### Phase 2-H: Progress UI ✅
30. ✅ `ProgressEntry.cs` 新規作成（readonly struct）
31. ✅ `ProgressSnapshot.cs` 新規作成（readonly struct）
32. ✅ `ProgressCalculator.cs` 新規作成
33. ✅ `ProgressIndicatorUI.cs` 新規作成（MonoBehaviour）
34. ✅ `GateResolver.GetAllStates()` メソッド追加
35. ✅ `AreaController` にコールバック機構追加
    - SetProgressCallback()
    - GetCurrentProgress()
    - NotifyProgressChanged()
36. ✅ `WalkingSystemManager` に progressUI 接続
37. ✅ EditModeテスト作成（ProgressCalculatorTests.cs）
38. ✅ バグ修正（初期表示、歩数カウント、タイミング問題）

### Phase 2-I: Gate/Exit Approach Button ✅
39. ✅ `ApproachChoice` に `Gate` を追加
40. ✅ `WalkApproachUI` 拡張
    - gateButton, gateLabel フィールド追加
    - WaitForGateSelection() メソッド追加
    - SetGateLabel(), SetGateActive() メソッド追加
41. ✅ `AreaController` 統合
    - CheckGates() をアプローチボタン対応に変更
    - ShowExitAndSelectDestination() をアプローチボタン対応に変更
42. ✅ MCP でシーンに GateApproachButton を配置
    - WalkObject の子として作成
    - Button コンポーネント追加
    - Text (TMP) 子要素作成

---

## 見た目の仕様まとめ

### 設計書との対応表

| 設計書の仕様 | 実装計画 | 状態 |
|-------------|----------|------|
| CentralObjectPresenter.Image（本体Sprite） | MainImage | 計画済 |
| CentralObjectPresenter.BackImage（背面Sprite） | BackImage | **新規追加** |
| CentralObjectPresenter.TMP_Text（ラベル） | Label | **新規追加** |
| CentralObjectPresenter.Button（クリック判定） | Button | **新規追加** |
| CentralObjectPresenter.CanvasGroup（フェード） | CanvasGroup | **新規追加** |
| HardBlock/SoftBlock/AutoTrigger | CentralDisplayMode | **新規追加** |
| 門の appearAnim/hideAnim | GateAppearAnimation/GateHideAnimation | **新規追加** |
| 門の sfx（SE） | GateVisual.sfxOnAppear等 | **新規追加** |
| 出口の背面描画 | ExitVisual.backSprite | **新規追加** |

### 描画順序の確認

設計書 L67-69:
> BackImage（門/出口）→ CentralObject（前面）の順に重ねる。

**実装**:
1. BackImage (sibling index = 0)
2. MainImage (sibling index = 1)
3. Label (sibling index = 2)
4. Button (raycast target, sibling index = 3)

---

## テスト計画

### ユニットテスト
- GatePositionSpec.ResolvePosition の各パターン（Seed分離確認）
- GateResolver.GetNextGate の順序判定
- GateResolver.TakeSnapshot / RestoreFromSnapshot
- AnchorManager.RewindToAnchor の復元確認（Gate状態含む）
- **ProgressCalculatorTests.cs** ✅ 実装済み
  - ProgressEntry.CreateGate / CreateExit のファクトリメソッド
  - ProgressSnapshot.Empty / GetNextEntry
  - ProgressCalculator.Calculate（ノードなし、ゲートなし）
  - ProgressCalculator.Calculate（ゲート順序ソート）
  - ProgressCalculator.Calculate（アクティブゲート判定）
  - ProgressCalculator.Calculate（クールダウン中ゲート判定）
  - ProgressCalculator.Calculate（残り歩数計算）
  - ProgressCalculator.Calculate（残りゲート数カウント）

### 統合テスト
- 門→通過→次の門 の連続動作
- 門失敗→巻き戻し→再挑戦
- 全門クリア→出口出現
- 出口候補1件でもUI表示
- 歩数なしリフレッシュの動作
- TickCooldowns の毎歩呼び出し
- GateEvent の各タイミング発火
- ループ門（repeatable=true）のクールダウン後再出現
- SaveData からのロード→Gate状態復元
- **統一スルー操作**:
  - 門表示中に歩行ボタン → HandleGateSkipped → resetOnSkip適用
  - 門表示中にクリック → 条件判定 → resetOnFail適用（失敗時）
  - 出口表示中に歩行ボタン → HandleExitSkipped → nodeSteps リセット
  - 出口表示中にクリック → 選択UI表示 → 遷移

---

## レビュー指摘対応表

### 第1回レビュー対応

| 指摘 | 対応箇所 |
|------|----------|
| ExitSpawnRule シグネチャ不整合 | Step 8 + Step 7.2 CanShowExit |
| TickCooldowns() 呼び出しなし | Step 7.2 WalkStep 末尾 |
| Gate状態の永続化なし | Step 4.1 GateRuntimeState.Clone + Step 5.1 WalkAnchor.GateStatesSnapshot |
| Seed分離問題 | Step 2.2 GatePositionSpec.ResolvePosition に gateId を追加 |
| progressKey/stepDelta 適用タイミング | Step 1.2 + Step 7.2 UpdateTrackProgress |
| GateEvent 発火タイミング | Step 2.1 GateEventTiming + Step 7.2 CheckGates |
| 歩数なしリフレッシュ | Step 5.3 triggerRefreshAfterRewind + Step 7.2 RefreshWithoutStep |
| Exit候補UI強制表示 | Step 7.2 ShowExitSelectionUI |
| Gate/Track リセット対象定義 | Step 2.2 GateResetTarget + Step 7.2 ApplyGateReset |

### 第2回レビュー対応

| 指摘 | 対応箇所 |
|------|----------|
| 出口「接近」ステップ欠落 | Step 7.2 ShowExitAndSelectDestination (WaitForClick 追加) |
| 出口「スルー」挙動未定義 | Step 7.2 HandleExitSkipped + ExitSelectionResult 構造体 |
| repeatable の具体挙動未定義 | Step 4.2 MarkCleared (repeatable=true でクールダウン設定) |
| SaveData 永続化パス未定義 | Step 10 WalkProgressData / GateRuntimeStateData / WalkProgressManager |

### 第3回レビュー対応

| 指摘 | 対応箇所 |
|------|----------|
| Gate「スルー」定義とResetOnSkip不整合 | Step 2.1 resetOnSkip/resetOnFail分離 + Step 3.2 WaitForInteraction + Step 7.2 HandleGateSkipped |
| KeyValuePair シリアライズ不可 | Step 10.1 StringBoolPair/StringIntPair 構造体 |
| 乱数シード/variety履歴保存 | Step 10.3 NodeSeed/VarietyHistoryIndex 追加 |

---

## 結論

本計画書は「ゼロトタイプ歩行システム設計書」の Phase 2 に該当する機能を網羅し、3回のレビュー指摘を全て反映しています。

**✅ Phase 2 実装完了（2025年1月）**

**見た目の仕様が含まれている箇所**:
- Step 3: 門/出口の見た目仕様（GateVisual, ExitVisual, CentralObjectPresenter拡張）
- 「見た目の仕様まとめ」セクション
- 描画順序の詳細

**設計目標との対応**:
- 固定門（GateMarker + positionSpec.AbsSteps）
- 順次解除（GateMarker.order + passConditions）
- ループ門（repeatable=true + cooldownSteps → クリア後も再出現）
- ループ歩数（resetOnSkip + cooldownSteps + GateResetTarget）
- 巻き戻し（AnchorManager + RewindToAnchorEffect + Gate状態復元）
- 歩数なしリフレッシュ（RequestRefreshWithoutStep + RefreshWithoutStep）
- Exit候補UI強制表示（ShowExitSelectionUI）
- **統一スルー操作（WaitForInteraction: クリック=接近, 歩行ボタン=スルー）**
- 門スルー（HandleGateSkipped + resetOnSkip）
- 門失敗（resetOnFail 分離）
- 出口スルー（HandleExitSkipped + nodeSteps リセット）
- SaveData永続化（WalkProgressData + StringBoolPair/StringIntPair + NodeSeed）
- **Progress UI（ProgressCalculator + ProgressIndicatorUI）** ← Step 11で追加
