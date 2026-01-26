# EventStep統合実装計画

**状態: 実装完了（2026-01-26）**

## 目的

Event Kernel設計に従い、**EventStep**を拡張可能な基盤として再設計する。
これにより、ノベルパート（DialogueSO）を含む全てのゲームシステムを、
歩行システムの全イベントポイントから統一的に起動可能にする。

## 実装完了後の姿

```
【実装完了】
EventDefinitionSO
├── steps: IEventStep[] ([SerializeReference])
│   ├── MessageStep（旧システム互換）
│   ├── NovelDialogueStep（ノベルパート）
│   ├── BattleStep（戦闘起動）
│   ├── EmitEventStep（別EventDefinition呼び出し）
│   ├── EffectStep（Effect適用のみ）
│   └── 将来追加も容易（IEventStep実装のみ）
└── terminalEffects: EffectSO[]

→ 全イベントポイントでEventDefinitionSOを使用
→ NodeSO.centralDialogue削除済み（NovelDialogueStep経由に統一）
→ ForcedEventTrigger.eventDefinitionに統一
```

## 実装ファイル一覧

```
Assets/Script/Walk/EventKernel/
├── IEventStep.cs           (新規) IEventStep, EventContext, IEventRunner
├── EventDefinitionSO.cs    (改修) [SerializeReference] IEventStep[]
├── EventRunner.cs          (改修) IEventRunner実装
├── EventHost.cs            (改修) CreateEventContext, TriggerWithContext
└── Steps/
    ├── MessageStep.cs      (新規) 旧EventStep互換
    ├── NovelDialogueStep.cs(新規) ノベルパート統合、ズーム責務含む
    ├── BattleStep.cs       (新規) 戦闘統合、結果別Effect分岐
    ├── EmitEventStep.cs    (新規) 別EventDefinitionSO呼び出し
    └── EffectStep.cs       (新規) Effect適用のみ
```

---

## 設計方針（重要）

### 1. ズーム責務の一本化

**問題**: ズーム制御が複数箇所に分散する可能性

```
【問題のある状態】
AreaController.RunCentralDialogue() → ズーム呼び出し
NovelDialogueStep.ExecuteAsync()    → ズーム呼び出し（重複）
DialogueRunner内                    → DialogueContext.CentralObjectRTでズーム？

→ 二重ズームや順序崩れのリスク
```

**方針**: **呼び出し側（EventStep）がズームを制御する**

```
【正しい状態】
EventRunner.RunAsync()
    ↓
NovelDialogueStep.ExecuteAsync()
    ├── ズームイン（Step内で制御）
    ├── DialogueRunner.RunDialogueAsync()  ← ズームしない
    └── ズームアウト（Step内で制御）

DialogueRunner: ズーム責務を持たない（会話表示のみ）
```

**理由**:
- ズームが必要かどうかは「起動元コンテキスト」に依存
  - 中央オブジェクトアプローチ → ズームあり
  - 強制イベント → ズームなし（設定次第）
  - サイドオブジェクト → ズームなし
- EventStep側で判断するのが自然

**影響**:
- `DialogueContext.CentralObjectRT` は廃止またはズーム以外の用途に変更
- `NovelDialogueStep` がズームの責務を持つ
- `DialogueRunner` はズーム処理を行わない

---

### 2. EventContextの依存注入

**問題**: EventContextにEventRunnerがない

```csharp
// BattleStep/EmitEventStepで使用
var effects = await context.EventRunner.RunAsync(outcomeEvent, context);
// だがEventContextにEventRunnerフィールドが未定義
```

**方針**: EventContextに必要な依存を全て注入する

```csharp
public sealed class EventContext
{
    // 基本
    public GameContext GameContext { get; set; }

    // UI
    public IEventUI EventUI { get; set; }
    public INovelEventUI NovelUI { get; set; }

    // ランナー（必須）
    public IDialogueRunner DialogueRunner { get; set; }
    public IBattleRunner BattleRunner { get; set; }
    public IEventRunner EventRunner { get; set; }  // ★追加

    // 中央オブジェクト（ズーム用）
    public RectTransform CentralObjectRT { get; set; }

    // Effect収集
    public List<EffectSO> CollectedEffects { get; } = new();
}
```

**生成箇所**: EventContextは以下で生成・注入

| 生成箇所 | 責務 |
|----------|------|
| AreaController | WalkStep中のイベント発火時に生成 |
| EventHost.Trigger | 汎用イベント発火時に生成 |
| WalkingSystemManager | 初期化時に共通依存を設定 |

---

### 3. DialogueSO vs EventDialogueSO（方針確定）

**現状**: 設計書では2種類のダイアログSOを区別

| SO | 用途 | 戻れるか |
|----|------|----------|
| DialogueSO | フィールド会話 | 戻れない |
| EventDialogueSO | イベント会話 | 戻れる |

**確定方針**: SOは分離しない。NovelDialogueStep.allowBacktrackで制御

```csharp
[Serializable]
public sealed class NovelDialogueStep : IEventStep
{
    [SerializeField] private DialogueSO fieldDialogue;
    [SerializeField] private bool allowBacktrack = false;

    // allowBacktrack = false → フィールド会話（戻れない）
    // allowBacktrack = true  → イベント会話（戻れる）
}
```

**採用理由:**
- SOレベルでの分離は過剰設計
- Step単位で「戻れるか」を制御できれば十分
- 同じDialogueSOを「フィールド会話として使う」「イベント会話として使う」が可能

**注意:** TabState切り替え責務はDialogueRunner側で完結（EventStep側でUI状態に触れない）

---

## アーキテクチャ

### データ構造

```
EventDefinitionSO
├── steps: IEventStep[] ([SerializeReference])
│   ├── MessageStep
│   │   ├── message: string
│   │   ├── choices: EventChoice[]
│   │   └── effects: EffectSO[]
│   │
│   ├── NovelDialogueStep
│   │   ├── dialogueRef: DialogueSO (参照)
│   │   ├── displayMode: DisplayMode
│   │   ├── zoomOnApproach: bool
│   │   └── focusArea: FocusArea
│   │
│   ├── BattleStep
│   │   ├── encounter: EncounterSO
│   │   └── onWin/onLose/onEscape: EffectSO[]
│   │
│   └── EmitEventStep（別EventDefinitionを呼び出す）
│       └── eventRef: EventDefinitionSO
│
├── terminalEffects: EffectSO[]
└── zoom設定（非推奨: NovelDialogueStepに移行）
```

### 実行フロー

```
EventHost.Trigger(EventDefinitionSO, context)
    ↓
EventRunner.Run(definition, context)
    ↓
foreach step in definition.steps:
    effects += await step.Execute(context)
    ↓
    [MessageStep] → IEventUI.ShowMessage()
    [NovelDialogueStep] → IDialogueRunner.RunDialogueAsync()
    [BattleStep] → IBattleRunner.RunBattleAsync()
    ↓
EffectApplier.ApplyAll(effects + terminalEffects)
```

---

## 実装フェーズ

### Phase 1: IEventStep基盤（必須）

**目的**: EventStepを多態的に拡張可能にする

**変更ファイル**:
- `Assets/Script/Walk/EventKernel/IEventStep.cs` (新規)
- `Assets/Script/Walk/EventKernel/EventDefinitionSO.cs` (改修)
- `Assets/Script/Walk/EventKernel/EventRunner.cs` (改修)

**作業内容**:

1. **IEventStep インターフェース定義**
```csharp
public interface IEventStep
{
    UniTask<EffectSO[]> ExecuteAsync(EventContext context);
}
```

2. **MessageStep（旧EventStep互換）**
```csharp
[Serializable]
public sealed class MessageStep : IEventStep
{
    [TextArea(2, 6)]
    [SerializeField] private string message;
    [SerializeField] private EventChoice[] choices;
    [SerializeField] private EffectSO[] effects;

    public async UniTask<EffectSO[]> ExecuteAsync(EventContext context)
    {
        await context.EventUI.ShowMessageAsync(message, choices);
        return effects ?? Array.Empty<EffectSO>();
    }
}
```

3. **EventDefinitionSO改修**
```csharp
[CreateAssetMenu(menuName = "Walk/Event Definition")]
public sealed class EventDefinitionSO : ScriptableObject
{
    [SerializeReference]
    private IEventStep[] steps;

    [SerializeField] private EffectSO[] terminalEffects;

    // ※以下のズーム設定は非推奨（課題1・2解決後に削除予定）
    // ズーム責務はNovelDialogueStepに移行
    [Header("Central Object Zoom (Obsolete)")]
    [Obsolete("Use NovelDialogueStep zoom settings instead")]
    [SerializeField] private bool zoomOnApproach = true;
    [Obsolete("Use NovelDialogueStep zoom settings instead")]
    [SerializeField] private FocusArea focusArea;

    public IEventStep[] Steps => steps;
    public EffectSO[] TerminalEffects => terminalEffects;
    [Obsolete] public bool ZoomOnApproach => zoomOnApproach;
    [Obsolete] public FocusArea FocusArea => focusArea;
}
```

**注意:** EventDefinitionSOのズーム設定は非推奨。
- ズーム責務はNovelDialogueStep内で完結する設計
- AreaController.RunCentralEvent()からズーム処理を削除後、これらの設定は参照されなくなる
- 詳細: [ノベルパート課題一覧.md](../ノベルパート/ノベルパート課題一覧.md) 課題1・2

4. **EventRunner改修**
```csharp
public async UniTask<EffectSO[]> RunAsync(EventDefinitionSO definition, EventContext context)
{
    var collectedEffects = new List<EffectSO>();

    foreach (var step in definition.Steps)
    {
        if (step == null) continue;
        var effects = await step.ExecuteAsync(context);
        if (effects != null)
            collectedEffects.AddRange(effects);
    }

    if (definition.TerminalEffects != null)
        collectedEffects.AddRange(definition.TerminalEffects);

    return collectedEffects.ToArray();
}
```

**検証**:
- 既存のEventDefinitionSOがMessageStepとして動作することを確認
- EventRunnerが新しいインターフェースで動作することを確認

---

### Phase 2: NovelDialogueStep（ノベルパート統合）

**目的**: DialogueSOをEventStepとして使用可能にする

**変更ファイル**:
- `Assets/Script/Walk/EventKernel/Steps/NovelDialogueStep.cs` (新規)

**作業内容**:

1. **NovelDialogueStep実装**

**重要**: ズーム責務はこのStep内で完結する（DialogueRunnerはズームしない）

```csharp
[Serializable]
public sealed class NovelDialogueStep : IEventStep
{
    [Header("ダイアログ参照")]
    [SerializeField] private DialogueSO fieldDialogue;
    [SerializeField] private EventDialogueSO eventDialogue;  // 将来対応

    [Header("ズーム設定")]
    [SerializeField] private bool overrideZoom;
    [SerializeField] private bool zoomOnApproach = true;
    [SerializeField] private FocusArea focusArea;

    public async UniTask<EffectSO[]> ExecuteAsync(EventContext context)
    {
        var dialogue = fieldDialogue;  // 現状はDialogueSOのみ
        if (dialogue == null) return Array.Empty<EffectSO>();

        // ズーム設定の決定（Step側 or SO側）
        var shouldZoom = overrideZoom ? zoomOnApproach : dialogue.ZoomOnApproach;
        var focus = overrideZoom ? focusArea : dialogue.FocusArea;
        var canZoom = shouldZoom && context.NovelUI != null && context.CentralObjectRT != null;

        // ★ズームイン（Step内で制御）
        if (canZoom)
        {
            await context.NovelUI.ZoomToCentralAsync(context.CentralObjectRT, focus);
        }

        // ダイアログ実行（DialogueRunnerはズームしない）
        var dialogueContext = new DialogueContext(dialogue, context.GameContext, dialogue.DefaultMode);
        // 注: CentralObjectRTは渡さない（ズーム責務はStep側）
        var result = await context.DialogueRunner.RunDialogueAsync(dialogueContext);

        // ★ズームアウト（Step内で制御）
        if (canZoom)
        {
            await context.NovelUI.ExitZoomAsync();
        }

        // リアクション結果をEffectとして返す
        return ConvertReactionToEffects(context, result);
    }

    private EffectSO[] ConvertReactionToEffects(EventContext context, DialogueResult result)
    {
        if (!result.IsReactionEnded) return Array.Empty<EffectSO>();

        var reaction = result.TriggeredReaction;
        if (reaction.Type == ReactionType.Battle && reaction.Encounter != null)
        {
            // 戦闘起動Effectを生成して返す
            // または直接バトルを実行してその結果Effectを返す
            // 詳細はPhase 2実装時に決定
        }

        return Array.Empty<EffectSO>();
    }
}
```

**ズーム責務の明確化**:
- `NovelDialogueStep`: ズームイン/アウトを制御
- `DialogueRunner`: 会話表示のみ（ズームしない）
- `DialogueContext.CentralObjectRT`: **使用しない**（または廃止検討）

**Inspector表示の工夫**:
- fieldDialogueにDialogueSOをドラッグ＆ドロップ
- overrideZoomをONにすると、SO側の設定を上書き可能

**検証**:
- EventDefinitionSOのstepsにNovelDialogueStepを追加できる
- DialogueSOの内容が正しく再生される
- ズームイン→会話→ズームアウトの順序が正しい

---

### Phase 3: EventContext整備

**目的**: 各Stepに必要な情報を渡すコンテキストを整備

**変更ファイル**:
- `Assets/Script/Walk/EventKernel/EventContext.cs` (新規または改修)

**作業内容**:

```csharp
public sealed class EventContext
{
    // 基本
    public GameContext GameContext { get; set; }

    // UI
    public IEventUI EventUI { get; set; }
    public INovelEventUI NovelUI { get; set; }

    // ランナー（全て必須）
    public IDialogueRunner DialogueRunner { get; set; }
    public IBattleRunner BattleRunner { get; set; }
    public IEventRunner EventRunner { get; set; }  // ★EmitEventStep/BattleStep用

    // 中央オブジェクト（ズーム用）
    public RectTransform CentralObjectRT { get; set; }

    // Effect収集
    public List<EffectSO> CollectedEffects { get; } = new();

    /// <summary>
    /// 必須依存のバリデーション。
    /// EventRunner起動前に呼び出して未設定を検出。
    /// </summary>
    public void ValidateRequired()
    {
        if (GameContext == null) throw new InvalidOperationException("GameContext is required");
        if (EventRunner == null) throw new InvalidOperationException("EventRunner is required");
        // DialogueRunner, BattleRunnerはStep使用時にnullチェック
    }
}
```

**生成・注入箇所**:

```csharp
// AreaController等での生成例
private EventContext CreateEventContext()
{
    return new EventContext
    {
        GameContext = context,
        EventUI = context.EventUI,
        NovelUI = context.EventUI as INovelEventUI,
        DialogueRunner = context.DialogueRunner,
        BattleRunner = context.BattleRunner,
        EventRunner = eventHost.Runner,  // EventHostからRunnerを取得
        CentralObjectRT = centralPresenter?.GetCurrentRectTransform()
    };
}
```

---

### Phase 4: 既存イベントポイントの統合

**目的**: 全てのイベントポイントでEventDefinitionSOを使用

**変更対象**:

| イベントポイント | 現状 | 変更後 |
|-----------------|------|--------|
| NodeSO.OnEnter | EventDefinitionSO | そのまま（新Step使用可能に） |
| NodeSO.OnExit | EventDefinitionSO | そのまま |
| NodeSO.CentralEvent | EventDefinitionSO | そのまま |
| NodeSO.CentralDialogue | DialogueSO | **削除**（EventDefinitionSOに統合） |
| GateMarker.GateEvent | EventDefinitionSO | そのまま |
| EncounterSO.OnWin/OnLose | EventDefinitionSO | そのまま |
| SideObjectSO.Event | EventDefinitionSO | そのまま |
| ForcedEventTrigger.Dialogue | DialogueSO | **EventDefinitionSOに変更** |

**NodeSOの変更**:
```csharp
// Before
[SerializeField] private EventDefinitionSO centralEvent;
[SerializeField] private DialogueSO centralDialogue; // 削除

// After
[SerializeField] private EventDefinitionSO centralEvent;
// centralDialogueは不要。centralEventのstepsにNovelDialogueStepを追加すればよい
```

**ForcedEventTriggerの変更**:
```csharp
// Before
[SerializeField] private DialogueSO dialogue;

// After
[SerializeField] private EventDefinitionSO eventDefinition;
```

---

### Phase 5: BattleStep（戦闘統合）

**目的**: 戦闘を全イベントポイントから起動可能にする

**変更ファイル**:
- `Assets/Script/Walk/EventKernel/Steps/BattleStep.cs` (新規)

**詳細**: 「戦闘システム統合（BattleStep）」セクション参照

**作業内容**:
1. BattleStepクラス実装
2. EncounterSO参照
3. 結果別Effect分岐（overrideOutcomeEffects）
4. EventRunner連携（EncounterSO側のoutcome処理）

---

### Phase 6: 追加Step型（将来拡張）

以下は必要に応じて追加：

1. **EmitEventStep** - 別のEventDefinitionSOを呼び出す
```csharp
[Serializable]
public sealed class EmitEventStep : IEventStep
{
    [SerializeField] private EventDefinitionSO eventRef;

    public async UniTask<EffectSO[]> ExecuteAsync(EventContext context)
    {
        if (eventRef == null) return Array.Empty<EffectSO>();
        return await context.EventRunner.RunAsync(eventRef, context);
    }
}
```

2. **EffectStep** - Effectだけを実行（Stepの中で分岐用）
```csharp
[Serializable]
public sealed class EffectStep : IEventStep
{
    [SerializeField] private EffectSO[] effects;

    public UniTask<EffectSO[]> ExecuteAsync(EventContext context)
    {
        return UniTask.FromResult(effects ?? Array.Empty<EffectSO>());
    }
}
```

3. **ConditionalStep** - 条件分岐
```csharp
[Serializable]
public sealed class ConditionalStep : IEventStep
{
    [SerializeField] private ConditionSO condition;
    [SerializeReference] private IEventStep onTrue;
    [SerializeReference] private IEventStep onFalse;
}
```

4. **PuzzleStep**, **MiniGameStep** 等（将来）

---

## 移行手順

### 既存データの移行

1. **自動移行ツール作成**（オプション）
   - 旧EventStep[]をMessageStep[]に変換
   - centralDialogueをNovelDialogueStep内包のEventDefinitionSOに変換

2. **手動移行**
   - 小規模なら手動で問題なし
   - EventDefinitionSOのstepsに旧データをMessageStepとして再設定

### 互換性

- **後方互換**: 旧EventStepはMessageStepに自動マッピング（または手動変換）
- **API互換**: EventRunnerのシグネチャは維持

---

## Inspector UX改善（オプション）

### カスタムPropertyDrawer

[SerializeReference]の配列をより使いやすくするDrawer:

```csharp
[CustomPropertyDrawer(typeof(IEventStep), true)]
public class EventStepDrawer : PropertyDrawer
{
    // 型選択ドロップダウン + 各型に応じたフィールド表示
}
```

### 型追加メニュー

Inspector上で「＋」ボタンから型を選択できるように。

---

## チェックリスト

### Phase 1
- [x] IEventStepインターフェース作成
- [x] MessageStep実装（旧互換）
- [x] EventDefinitionSO改修（[SerializeReference]）
- [x] EventRunner改修
- [x] IEventRunnerインターフェース追加
- [x] EventContext整備（Phase 3から前倒し）
- [x] ValidateRequired()実装（Phase 3から前倒し）
- [ ] 既存機能が動作することを確認（要Unityテスト）

### Phase 2
- [x] NovelDialogueStep実装
- [x] DialogueSOとの連携確認（コード上は完了）
- [x] ズーム責務の一本化確認（Step内でのみズーム）
- [x] リアクション→Effect変換の実装（戦闘起動対応）
- [ ] DialogueRunnerからズーム処理を除去（または呼ばない確認）
- [ ] DialogueContext.CentralObjectRT使用箇所の整理

### Phase 3
- [x] EventContext整備 → Phase 1で実装済
- [x] IEventRunnerフィールド追加 → Phase 1で実装済
- [x] ValidateRequired()実装 → Phase 1で実装済
- [x] 生成・注入箇所の実装（EventHost.CreateEventContext, TriggerWithContext）
- [ ] 各Stepに必要な情報が渡ることを確認（要Unityテスト）

### Phase 4（移行フェーズ）
- [x] NodeSO.centralDialogue削除
- [x] ForcedEventTrigger.dialogue → eventDefinition変更
- [x] AreaController等の呼び出し元修正（RunCentralDialogue削除、CheckForcedEvents簡略化）
- [ ] 全イベントポイントでの動作確認（要Unityテスト）

### Phase 5: BattleStep
- [x] BattleStepクラス作成
- [x] EncounterContextとの連携
- [x] BattleResult処理
- [x] overrideOutcomeEffects分岐
- [x] EventRunner経由でのEncounterSO結果処理
- [ ] 各イベントポイントからの起動確認（要Unityテスト）:
  - [ ] OnEnterEvent
  - [ ] CentralEvent
  - [ ] GateEvent (OnAppear/OnPass/OnFail)
  - [ ] SideObject.Event
  - [ ] ForcedEvent
  - [ ] EncounterSO.OnWin/OnLose/OnEscape
- [ ] 会話→戦闘→会話の連続実行確認（要Unityテスト）
- [ ] 連続戦闘（Wave形式）確認（要Unityテスト）

### Phase 6（追加Step型）
- [x] EmitEventStep実装
- [x] EffectStep実装
- [ ] ConditionalStep実装（将来）
- [ ] その他必要なStep追加（将来）

---

## 期待される効果

1. **統一性**: 全イベントポイントから同じ方法でどのシステムも起動可能
2. **拡張性**: 新しいゲームシステムはIEventStepを実装するだけ
3. **便利さ**: DialogueSOはそのまま使える（NovelDialogueStepで参照）
4. **メンテナンス性**: イベントポイントごとに別フィールドを追加する必要がない
5. **設計準拠**: Event Kernel設計書の意図を完全に実現

---

## 完成形アーキテクチャ図

```
┌─────────────────────────────────────────────────────────────────┐
│                        FlowGraphSO                               │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                        NodeSO                            │    │
│  │                                                          │    │
│  │  ┌──────────────────────────────────────────────────┐   │    │
│  │  │            EventDefinitionSO                      │   │    │
│  │  │  (OnEnter/OnExit/CentralEvent/GateEvent等)       │   │    │
│  │  │                                                   │   │    │
│  │  │  steps: IEventStep[]                             │   │    │
│  │  │  ├── MessageStep        → IEventUI              │   │    │
│  │  │  ├── NovelDialogueStep  → IDialogueRunner       │   │    │
│  │  │  │   └── dialogueRef: DialogueSO          │   │    │
│  │  │  ├── BattleStep         → IBattleRunner         │   │    │
│  │  │  │   └── encounter: EncounterSO                │   │    │
│  │  │  ├── EmitEventStep      → EventRunner (再帰)   │   │    │
│  │  │  └── [将来追加Step]                            │   │    │
│  │  │                                                   │   │    │
│  │  │  terminalEffects: EffectSO[]                     │   │    │
│  │  └──────────────────────────────────────────────────┘   │    │
│  │                                                          │    │
│  │  encounterTable: EncounterTableSO  ← ランダムエンカウント│    │
│  │  │                                  （従来通り並存）     │    │
│  │  └── entries[].encounter: EncounterSO                   │    │
│  │                                                          │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘

【イベントポイント一覧】（全てEventDefinitionSOを使用）

┌─────────────────────┬───────────────────────────────────────────┐
│ イベントポイント    │ 使用可能なStep                            │
├─────────────────────┼───────────────────────────────────────────┤
│ NodeSO.OnEnter      │ Message, NovelDialogue, Battle, Emit, ... │
│ NodeSO.OnExit       │ Message, NovelDialogue, Battle, Emit, ... │
│ NodeSO.CentralEvent │ Message, NovelDialogue, Battle, Emit, ... │
│ GateMarker.OnAppear │ Message, NovelDialogue, Battle, Emit, ... │
│ GateMarker.OnPass   │ Message, NovelDialogue, Battle, Emit, ... │
│ GateMarker.OnFail   │ Message, NovelDialogue, Battle, Emit, ... │
│ SideObject.Event    │ Message, NovelDialogue, Battle, Emit, ... │
│ ForcedEventTrigger  │ Message, NovelDialogue, Battle, Emit, ... │
│ EncounterSO.OnWin   │ Message, NovelDialogue, Battle, Emit, ... │
│ EncounterSO.OnLose  │ Message, NovelDialogue, Battle, Emit, ... │
│ EncounterSO.OnEscape│ Message, NovelDialogue, Battle, Emit, ... │
└─────────────────────┴───────────────────────────────────────────┘

【戦闘起動の2経路】

経路A: ランダムエンカウント（従来通り）
  WalkStep → EncounterResolver → IBattleRunner
  └── NodeSO.encounterTable で確率制御

経路B: EventStep経由（新規）
  任意のイベントポイント → EventDefinitionSO → BattleStep → IBattleRunner
  └── どのタイミングでも任意の敵と戦闘可能
```

---

## 戦闘システム統合（BattleStep）

### 現状の戦闘システムアーキテクチャ

```
【現状のデータ構造】

FlowGraphSO
└── NodeSO
    ├── encounterTable: EncounterTableSO     ← ランダムエンカウント用
    │   ├── baseRate: 0.1 (10%確率)
    │   ├── cooldownSteps: 5
    │   ├── graceSteps: 3
    │   ├── pityIncrement: 0.05
    │   └── entries: EncounterEntry[]
    │       └── encounter: EncounterSO
    │           weight: 1.0
    │           conditions: ConditionSO[]
    │
    └── encounterRateMultiplier: 1.0f        ← ノード固有の倍率

【EncounterSO（敵遭遇定義）】
EncounterSO
├── id: string
├── uiLabel: string
├── enemyList: List<NormalEnemy>             ← 敵データ（BaseStatesベース）
│   └── [SerializeReference] NormalEnemy
│       ├── 基本能力値（HP, 攻撃力等）
│       ├── スキルリスト
│       └── RecoverySteps（復活歩数）
├── enemyCount: 2                            ← 出現数
├── escapeRate: 50f                          ← 逃走成功率
├── onWin: EventDefinitionSO                 ← 勝利時イベント
├── onLose: EventDefinitionSO                ← 敗北時イベント
└── onEscape: EventDefinitionSO              ← 逃走時イベント
```

### 現状の戦闘フロー

```
【ランダムエンカウント（現状）】

WalkStep()
    ↓
EncounterResolver.Resolve(table, context)
    ↓ 確率判定 + 重み抽選
EncounterRollResult { Triggered, Encounter }
    ↓
RunEncounter(EncounterSO)
    ↓
IBattleRunner.RunBattleAsync(EncounterContext)
    ↓
BattleResult { Encountered, Outcome }
    ↓
TriggerEncounterOutcome()
    ├── Victory → encounter.OnWin
    ├── Defeat  → encounter.OnLose
    └── Escape  → encounter.OnEscape
```

### 既存の戦闘起動手段

```csharp
// 1. LaunchBattleEffect（EffectSOとして存在）
[CreateAssetMenu(menuName = "Walk/Effects/LaunchBattle")]
public sealed class LaunchBattleEffect : EffectSO
{
    [SerializeField] private EncounterSO encounter;

    public override async UniTask Apply(GameContext context)
    {
        await context.BattleRunner.RunBattleAsync(
            new EncounterContext(encounter, context)
        );
    }
}

// 2. ReactionSegment（ノベルパートのリアクションから）
public sealed class ReactionSegment
{
    [SerializeField] private ReactionType type;     // Battle
    [SerializeField] private EncounterSO encounter;
}
```

### BattleStep設計

**目的**:
- 戦闘をEventStepとして起動可能にする
- 戦闘結果に応じたEffect分岐をStep内で完結させる
- EncounterSO側のonWin/onLoseとの使い分けを明確にする

```csharp
[Serializable]
public sealed class BattleStep : IEventStep
{
    [Header("戦闘設定")]
    [SerializeField] private EncounterSO encounter;

    [Header("結果別Effect（EncounterSO側を上書きする場合）")]
    [SerializeField] private bool overrideOutcomeEffects;
    [SerializeField] private EffectSO[] onWin;
    [SerializeField] private EffectSO[] onLose;
    [SerializeField] private EffectSO[] onEscape;

    public async UniTask<EffectSO[]> ExecuteAsync(EventContext context)
    {
        if (encounter == null) return Array.Empty<EffectSO>();

        // 戦闘実行
        var battleContext = new EncounterContext(encounter, context.GameContext);
        var result = await context.BattleRunner.RunBattleAsync(battleContext);

        if (!result.Encountered) return Array.Empty<EffectSO>();

        // 結果に応じたEffect決定
        if (overrideOutcomeEffects)
        {
            // Step側の設定を使用
            return result.Outcome switch
            {
                BattleOutcome.Victory => onWin ?? Array.Empty<EffectSO>(),
                BattleOutcome.Defeat => onLose ?? Array.Empty<EffectSO>(),
                BattleOutcome.Escape => onEscape ?? Array.Empty<EffectSO>(),
                _ => Array.Empty<EffectSO>()
            };
        }
        else
        {
            // EncounterSO側のEventDefinitionを実行
            // 注: EventDefinitionSOを実行するにはEventRunner経由が必要
            // ここではEffectとして返す形式に変換するか、
            // 別途EventRunnerを呼び出す
            var outcomeEvent = result.Outcome switch
            {
                BattleOutcome.Victory => encounter.OnWin,
                BattleOutcome.Defeat => encounter.OnLose,
                BattleOutcome.Escape => encounter.OnEscape,
                _ => null
            };

            if (outcomeEvent != null)
            {
                // EventDefinitionSOを実行してEffectを収集
                var effects = await context.EventRunner.RunAsync(outcomeEvent, context);
                return effects;
            }

            return Array.Empty<EffectSO>();
        }
    }
}
```

### EncounterSOとの関係

```
【優先順位と使い分け】

EncounterSO
├── onWin/onLose/onEscape: EventDefinitionSO
│   └── 汎用的な結果処理（デフォルト）
│       例: 敗北時は回復地点へ移動

BattleStep
├── overrideOutcomeEffects: true の場合
│   └── Step側のonWin/onLose/onEscapeを使用
│       例: 特定イベント内での特殊な勝利演出
│
└── overrideOutcomeEffects: false の場合
    └── EncounterSO側を使用（デフォルト）
```

### 使用例

#### 例1: ゲート失敗時に門番戦闘

```yaml
# GateMarker.onFail に設定するEventDefinitionSO
gateFailEvent:
  steps:
    - NovelDialogueStep:
        dialogueRef: "門番怒りの会話"
    - BattleStep:
        encounter: "GateGuardEncounter"
        overrideOutcomeEffects: true
        onWin:
          - SetFlag("gate_guard_defeated")
          - UnlockGate("main_gate")
        onLose:
          - Jump("RecoveryNode")
          - Heal(50)
    - NovelDialogueStep:
        dialogueRef: "門番撃破後の会話"
```

#### 例2: 中央オブジェクトアプローチでボス戦

```yaml
# NodeSO.centralEvent に設定
bossEvent:
  zoomOnApproach: true
  focusArea: UpperHalf
  steps:
    - NovelDialogueStep:
        dialogueRef: "ボス登場演出"
    - BattleStep:
        encounter: "AreaBossEncounter"
        # EncounterSO側の勝敗処理を使用
        overrideOutcomeEffects: false
    - MessageStep:
        message: "ボスを倒した！"
        effects:
          - SetFlag("area_boss_defeated")
```

#### 例3: サイドオブジェクト選択で戦闘

```yaml
# SideObjectSO.eventDefinition に設定
suspiciousShadowEvent:
  steps:
    - MessageStep:
        message: "怪しい影が動いた..."
    - BattleStep:
        encounter: "ShadowEnemy"
        overrideOutcomeEffects: true
        onWin:
          - AddItem("shadow_essence")
        onLose:
          - DamageHP(20)
        onEscape: []  # 逃走時は何もしない
```

#### 例4: 強制イベントで連続戦闘

```yaml
# ForcedEventTrigger.eventDefinition に設定
ambushEvent:
  steps:
    - NovelDialogueStep:
        dialogueRef: "襲撃演出"
    - BattleStep:
        encounter: "AmbushWave1"
    - MessageStep:
        message: "まだ敵がいる！"
    - BattleStep:
        encounter: "AmbushWave2"
    - NovelDialogueStep:
        dialogueRef: "襲撃撃退後の会話"
```

### ランダムエンカウントとの共存

```
【2つの戦闘起動経路】

1. ランダムエンカウント（従来通り）
   NodeSO.encounterTable → EncounterResolver → BattleRunner
   - 確率ベース
   - WalkStepの中で自動判定
   - EncounterSO.onWin/onLose/onEscapeで結果処理

2. EventStep経由（新規）
   EventDefinitionSO.steps[BattleStep] → BattleRunner
   - 任意のタイミングで起動可能
   - OnEnter, CentralEvent, GateEvent, SideObject等から
   - Step側またはEncounterSO側で結果処理
```

### 戦闘関連チェックリスト

#### Phase 5-A: BattleStep基本実装
- [ ] BattleStepクラス作成
- [ ] EncounterContextとの連携
- [ ] BattleResult処理
- [ ] overrideOutcomeEffects分岐

#### Phase 5-B: EncounterSO結果処理との連携
- [ ] EventRunner経由でのEventDefinitionSO実行
- [ ] Effect収集と返却

#### Phase 5-C: 検証
- [ ] OnEnterEventからBattleStep起動
- [ ] CentralEventからBattleStep起動
- [ ] GateEventからBattleStep起動
- [ ] SideObjectEventからBattleStep起動
- [ ] ForcedEventからBattleStep起動
- [ ] 会話→戦闘→会話の連続実行
- [ ] 連続戦闘（Wave形式）

---

## 関連ドキュメント

- [event_kernel_拡張可能イベント機構_設計ドキュメント_v_0.md](./基礎設計想起/event_kernel_拡張可能イベント機構_設計ドキュメント_v_0.md)
- [拡張案_各ゲームシステム統合インターフェース.md](./基礎設計想起/拡張案_各ゲームシステム統合インターフェース.md)
- [ノベルパート設計.md](../ノベルパート/ノベルパート設計.md)
- [ノベルパート未実装機能一覧.md](../ノベルパート/ノベルパート未実装機能一覧.md)
- [リアクションシステム実装計画.md](../ノベルパート/リアクションシステム実装計画.md)

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-26 | 初版作成（実装計画） |
| 2026-01-26 | **Phase 1完了**: IEventStep, EventContext, IEventRunner, MessageStep, EventDefinitionSO改修, EventRunner改修 |
| 2026-01-26 | **Phase 2完了**: NovelDialogueStep（ズーム責務含む） |
| 2026-01-26 | **Phase 3完了**: EventHost.CreateEventContext, TriggerWithContext |
| 2026-01-26 | **Phase 4完了**: NodeSO.centralDialogue削除, ForcedEventTrigger.eventDefinition統一, AreaController簡略化 |
| 2026-01-26 | **Phase 5完了**: BattleStep |
| 2026-01-26 | **Phase 6完了**: EmitEventStep, EffectStep |
| 2026-01-26 | ノベルパート関連ドキュメント更新（設計.md, 未実装機能一覧.md, リアクションシステム実装計画.md）|
| 2026-01-26 | **外部レビュー対応**: EventDialogueSO方針確定（SO分離しない）、EventDefinitionSOズーム設定を非推奨化 |
