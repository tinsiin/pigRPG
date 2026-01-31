# EventQueue実装計画

## 概要

[EventQueue共通規格設計.md](./EventQueue共通規格設計.md) に基づく実装計画。

---

## Phase 1: 基盤作成

### 1.1 EventQueueEntry作成

**ファイル**: `Assets/Script/Walk/EventQueue/EventQueueEntry.cs`

```csharp
[Serializable]
public sealed class EventQueueEntry
{
    [SerializeField] private string entryId;
    [SerializeField] private EventDefinitionSO eventDefinition;
    [SerializeField] private ConditionSO[] conditions;
    [SerializeField] private bool consumeOnTrigger = true;
    [SerializeField] private int cooldownSteps;
    [SerializeField] private int maxTriggerCount;

    // プロパティ省略
}
```

### 1.2 EventQueueEntryState作成

**ファイル**: `Assets/Script/Walk/EventQueue/EventQueueEntryState.cs`

```csharp
[Serializable]
public sealed class EventQueueEntryState
{
    public string EntryId;
    public bool Consumed;
    public int TriggerCount;
    public int StepsSinceLastTrigger;
}
```

### 1.3 EventQueueStateManager作成

**ファイル**: `Assets/Script/Walk/EventQueue/EventQueueStateManager.cs`

- ForcedEventStateManagerと類似の構造
- Export/Importメソッド（セーブ/ロード用）
- IncrementSteps（歩数進行時にクールダウン更新）

### 1.4 EventQueueResolver作成

**ファイル**: `Assets/Script/Walk/EventQueue/EventQueueResolver.cs`

- ResolveNext: 配列から発火可能なイベントを1つ取得
- CanTrigger: エントリが発火可能かどうか判定
- RecordTrigger: 発火を記録

---

## Phase 2: GameContext統合

### 2.1 GameContextにEventQueueStateManager追加

**ファイル**: `Assets/Script/Walk/GameContext.cs`

```csharp
public EventQueueStateManager EventQueueStateManager { get; } = new();
```

### 2.2 WalkProgressDataにセーブ/ロード追加

**ファイル**: `Assets/Script/Walk/WalkProgressData.cs`

```csharp
public List<EventQueueEntryState> EventQueueStates = new();

// FromContext()
data.EventQueueStates = context.EventQueueStateManager.Export();

// ApplyToContext()
context.EventQueueStateManager.Import(EventQueueStates);
```

### 2.3 歩数進行時のクールダウン更新

**ファイル**: `Assets/Script/Walk/AreaController.cs`

WalkStep()内で:
```csharp
context.EventQueueStateManager.IncrementSteps();
```

---

## Phase 3: 各SOへの適用

### 3.1 CentralObjectSO

**ファイル**: `Assets/Script/Walk/CentralObjects/CentralObjectSO.cs`

```csharp
// Before
[SerializeField] private EventDefinitionSO eventDefinition;

// After
[SerializeField] private EventQueueEntry[] events;

// 後方互換用ヘルパー（移行期間中）
public EventDefinitionSO GetLegacyEventDefinition() =>
    events?.Length > 0 ? events[0].EventDefinition : null;
```

### 3.2 SideObjectSO

**ファイル**: `Assets/Script/Walk/SideObjects/SideObjectSO.cs`

同様の変更。

### 3.3 NodeSO

**ファイル**: `Assets/Script/Walk/NodeSO.cs`

```csharp
// Before
[SerializeField] private EventDefinitionSO onEnterEvent;
[SerializeField] private EventDefinitionSO onExitEvent;

// After
[SerializeField] private EventQueueEntry[] onEnterEvents;
[SerializeField] private EventQueueEntry[] onExitEvents;
```

※ `centralEvent` は存在しない。中央オブジェクトは `CentralObjectSO.eventDefinition` 経由。

### 3.4 GateMarker

**ファイル**: `Assets/Script/Walk/Gate/GateMarker.cs`

```csharp
// Before
[SerializeField] private EventDefinitionSO gateEvent;

// After
[SerializeField] private EventQueueEntry[] gateEvents;
```

### 3.5 EncounterSO

**ファイル**: `Assets/Script/Walk/Encounter/EncounterSO.cs`

```csharp
// Before
[SerializeField] private EventDefinitionSO onWin;
[SerializeField] private EventDefinitionSO onLose;
[SerializeField] private EventDefinitionSO onEscape;

// After
[SerializeField] private EventQueueEntry[] onWinEvents;
[SerializeField] private EventQueueEntry[] onLoseEvents;
[SerializeField] private EventQueueEntry[] onEscapeEvents;
```

---

## Phase 4: AreaController統合

### 4.1 イベント発火箇所の更新

**ファイル**: `Assets/Script/Walk/AreaController.cs`

各イベント発火箇所で `EventQueueResolver.ResolveNext()` を使用:

```csharp
// 例: 中央オブジェクトアプローチ時
private async UniTask RunCentralEvent(CentralObjectSO central)
{
    var hostKey = $"central:{central.Id}";
    var eventDef = eventQueueResolver.ResolveNext(
        central.Events,          // EventQueueEntry[]
        hostKey,                 // "central:npc_merchant"
        context,
        context.EventQueueStateManager
    );

    if (eventDef == null) return;  // 発火可能なイベントなし

    await eventRunner.RunAsync(eventDef, eventContext);
}
```

### 4.2 対象箇所一覧

| 箇所 | メソッド | hostKey形式 |
|------|----------|-------------|
| ノード入場 | RunOnEnterEvent | `node:{nodeId}:enter` |
| ノード退場 | RunOnExitEvent | `node:{nodeId}:exit` |
| 中央オブジェクト | RunCentralEvent | `central:{centralId}` |
| サイドオブジェクト | RunSideEvent | `side:{sideId}` |
| ゲート | RunGateEvent | `gate:{gateId}` |
| エンカウント勝利 | HandleEncounterOutcome | `encounter:{encounterId}:win` |
| エンカウント敗北 | HandleEncounterOutcome | `encounter:{encounterId}:lose` |
| エンカウント逃走 | HandleEncounterOutcome | `encounter:{encounterId}:escape` |

※ 同じSOで複数の発火箇所がある場合（onEnter/onExit、win/lose/escape）は、hostKeyに区別を含める

---

## Phase 5: fixed系フィールド廃止

### 5.1 廃止対象

| クラス | フィールド |
|--------|-----------|
| NodeSO | `fixedCentralObject` |
| NodeSO | `fixedSideObjects` |

### 5.2 関連コード修正

**ファイル**: `Assets/Script/Walk/NodeSO.cs`
- `fixedCentralObject` フィールド削除
- `fixedSideObjects` フィールド削除
- `FixedCentralObject` プロパティ削除
- `FixedSideObjects` プロパティ削除

**ファイル**: `Assets/Script/Walk/CentralObjects/CentralObjectSelector.cs`
- `isNodeEntry && node?.FixedCentralObject != null` の分岐削除
- `FindEntryByCentralObject` メソッド削除（不要になれば）

**ファイル**: `Assets/Script/Walk/SideObjects/SideObjectSelector.cs`
- `isNodeEntry && node.FixedSideObjects.HasAny` の分岐削除
- 固定ペア処理削除

**ファイル**: `Assets/Script/Walk/AreaController.cs`
- `FixedCentralObject` 参照箇所削除

**ファイル**: `Assets/Script/Walk/SideObjects/FixedSideObjectPair.cs`
- ファイル削除（構造体ごと不要）

### 5.3 データ移行

既存のfixed設定がある場合：
1. 該当NodeSOのfixed設定を確認
2. 対応するテーブルに条件付きエントリを追加（`NodeSteps == 0`）
3. fixed設定をクリア

---

## Phase 6: EventQueueデータ移行

### 6.1 移行スクリプト作成

**ファイル**: `Assets/Editor/Walk/EventQueueMigration.cs`

既存の単一EventDefinitionSOを、EventQueueEntry配列（要素1個）に変換するエディタスクリプト。

```csharp
[MenuItem("Tools/Walk/Migrate EventDefinition to EventQueue")]
public static void MigrateAll()
{
    // CentralObjectSO
    var centrals = AssetDatabase.FindAssets("t:CentralObjectSO");
    foreach (var guid in centrals)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var so = AssetDatabase.LoadAssetAtPath<CentralObjectSO>(path);
        MigrateCentralObject(so);
    }
    // 他のSOも同様
}
```

### 6.2 移行手順

1. 移行スクリプト実行
2. 全SOをInspectorで確認
3. 問題なければ移行完了

---

## Phase 7: ForcedEventStateManager統合（オプション）

ForcedEventStateManagerとEventQueueStateManagerを統合して、1つの`EventEntryStateManager`にする。

これは後回しでもよい（両者が別々でも動作する）。

---

## 実装順序まとめ

```
Phase 1: 基盤作成
├── 1.1 EventQueueEntry.cs
├── 1.2 EventQueueEntryState.cs
├── 1.3 EventQueueStateManager.cs
└── 1.4 EventQueueResolver.cs

Phase 2: GameContext統合
├── 2.1 GameContext修正
├── 2.2 WalkProgressData修正
└── 2.3 AreaController修正（歩数進行）

Phase 3: 各SOへの適用
├── 3.1 CentralObjectSO
├── 3.2 SideObjectSO
├── 3.3 NodeSO
├── 3.4 GateMarker
└── 3.5 EncounterSO

Phase 4: AreaController統合
├── 4.1 各イベント発火箇所の更新
└── 4.2 hostKey設計

Phase 5: fixed系フィールド廃止
├── 5.1 NodeSO.fixedCentralObject 削除
├── 5.2 NodeSO.fixedSideObjects 削除
├── 5.3 FixedSideObjectPair.cs 削除
├── 5.4 CentralObjectSelector 修正
├── 5.5 SideObjectSelector 修正
└── 5.6 AreaController 修正

Phase 6: EventQueueデータ移行
├── 6.1 移行スクリプト作成
└── 6.2 移行実行

Phase 7: 状態管理統合（オプション）
└── ForcedEventStateManager + EventQueueStateManager → EventEntryStateManager
```

---

## 見積もり

| Phase | 内容 | 規模 |
|-------|------|------|
| 1 | 基盤作成 | 4ファイル新規 |
| 2 | GameContext統合 | 3ファイル修正 |
| 3 | 各SOへの適用 | 5ファイル修正 |
| 4 | AreaController統合 | 1ファイル修正（複数箇所） |
| 5 | fixed系フィールド廃止 | 5ファイル修正 + 1ファイル削除 |
| 6 | EventQueueデータ移行 | 1ファイル新規 + 実行 |
| 7 | 状態管理統合 | オプション |

---

## 注意事項

### entryIdの設計

- **必須**: 状態管理に使用するため、空文字は不可
- **一意性**: 同一SO内で重複しないこと
- **命名規則**: `{目的}_{連番}` を推奨（例: `first_meet`, `quest_complete`, `default`）

### hostKeyの設計

- 状態は `{hostKey}:{entryId}` の組み合わせで管理
- **hostKey = `{soType}:{soId}`**（ノードIDは含めない）
- **同じSOならノード間で状態を共有**する
- 別状態にしたければ、別のSOを作成する

```
hostKey例:
- central:npc_merchant
- side:treasure_chest
- gate:boss_gate
- encounter:goblin
- node:forest（onEnter/onExit用）

fullKey例:
- central:npc_merchant:first_meet
- side:treasure_chest:open
```

### 後方互換

- 移行期間中は旧フィールド（eventDefinition）を参照するコードを残す
- 移行完了後に削除

---

## テスト計画

### 単体テスト

1. EventQueueResolver.CanTrigger が正しく判定する
2. consumeOnTrigger=true で消費される
3. cooldownSteps が正しく機能する
4. maxTriggerCount が正しく機能する
5. conditions が正しく評価される

### 統合テスト

1. 中央オブジェクトに複数イベント設定 → 順番に発火
2. 消費済みイベント → スキップして次へ
3. 条件付きイベント → 条件を満たす時のみ発火
4. セーブ/ロード → 状態が保持される

---

## 関連ドキュメント

- [EventQueue共通規格設計.md](./EventQueue共通規格設計.md) - 設計詳細
- [ゼロトタイプ歩行システム設計書.md](./ゼロトタイプ歩行システム設計書.md) - 既存設計
