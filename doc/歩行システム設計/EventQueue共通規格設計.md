# EventQueue共通規格設計

## 概要

歩行システムのすべてのイベント発火箇所で使える**共通のイベントキューシステム**を設計する。

### 現状の問題

| イベント発火箇所 | 現状 | 問題 |
|-----------------|------|------|
| ForcedEventTrigger | 配列で複数設定可、条件/消費/クールダウン対応 | ✅ 問題なし |
| CentralObjectSO | EventDefinitionSO 1つのみ | ❌ 複数不可 |
| SideObjectSO | EventDefinitionSO 1つのみ | ❌ 複数不可 |
| GateMarker.gateEvent | EventDefinitionSO 1つのみ | ❌ 複数不可 |
| EncounterSO.onWin/onLose/onEscape | EventDefinitionSO 1つのみ | ❌ 複数不可 |
| NodeSO.onEnterEvent/onExitEvent | EventDefinitionSO 1つのみ | ❌ 複数不可 |

### 目標

1. **複数のイベントを持てる**
2. **それぞれで再発火の有無を選べる**（1回限り or 何度でも）
3. **フラグ/条件によって発火可否を制御できる**
4. **条件がなければ順番に発火**（1回の発火につき1個ずつ）
5. **消費済みなら次のイベントへスキップ**
6. **すべてのイベント発火箇所で共通の仕組み**

---

## 設計

### EventQueueEntry（キューエントリ）

```csharp
[Serializable]
public sealed class EventQueueEntry
{
    [Header("識別")]
    [SerializeField] private string entryId;

    [Header("イベント内容")]
    [SerializeField] private EventDefinitionSO eventDefinition;

    [Header("発火条件")]
    [SerializeField] private ConditionSO[] conditions;

    [Header("発火制御")]
    [SerializeField] private bool consumeOnTrigger = true;  // 1回限り
    [SerializeField] private int cooldownSteps;             // クールダウン（0=即再発火可）
    [SerializeField] private int maxTriggerCount;           // 最大発火回数（0=無制限）

    public string EntryId => entryId;
    public EventDefinitionSO EventDefinition => eventDefinition;
    public ConditionSO[] Conditions => conditions;
    public bool ConsumeOnTrigger => consumeOnTrigger;
    public int CooldownSteps => cooldownSteps;
    public int MaxTriggerCount => maxTriggerCount;

    public bool HasConditions => conditions != null && conditions.Length > 0;
}
```

### EventQueueState（発火状態管理）

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

### EventQueueResolver（発火判定・実行）

```csharp
public sealed class EventQueueResolver
{
    /// <summary>
    /// 配列から発火可能なイベントを1つ取得する。
    /// 順番に評価し、最初に条件を満たすものを返す。
    /// </summary>
    public EventDefinitionSO ResolveNext(
        EventQueueEntry[] entries,
        string hostKey,
        GameContext context,
        EventQueueStateManager stateManager)
    {
        if (entries == null || entries.Length == 0) return null;

        foreach (var entry in entries)
        {
            if (CanTrigger(entry, hostKey, context, stateManager))
            {
                // 発火を記録
                stateManager.RecordTrigger(hostKey, entry.EntryId, entry.ConsumeOnTrigger);
                return entry.EventDefinition;
            }
        }
        return null;
    }

    /// <summary>
    /// エントリが発火可能かどうか判定する。
    /// </summary>
    public bool CanTrigger(
        EventQueueEntry entry,
        string hostKey,
        GameContext context,
        EventQueueStateManager stateManager)
    {
        if (entry?.EventDefinition == null) return false;

        var state = stateManager.GetState(hostKey, entry.EntryId);

        // 消費済みチェック
        if (entry.ConsumeOnTrigger && state.Consumed) return false;

        // 最大回数チェック
        if (entry.MaxTriggerCount > 0 && state.TriggerCount >= entry.MaxTriggerCount) return false;

        // クールダウンチェック
        if (entry.CooldownSteps > 0 && state.StepsSinceLastTrigger < entry.CooldownSteps) return false;

        // 条件チェック
        if (entry.HasConditions)
        {
            foreach (var cond in entry.Conditions)
            {
                if (cond != null && !cond.IsMet(context)) return false;
            }
        }

        return true;
    }
}
```

### EventQueueStateManager（状態管理）

```csharp
public sealed class EventQueueStateManager
{
    private readonly Dictionary<string, EventQueueEntryState> states = new();

    /// <summary>
    /// 状態キーを生成する。
    /// hostKey: "{soType}:{soId}" 例: "central:npc_merchant"
    /// entryId: "first_meet"
    /// → fullKey: "central:npc_merchant:first_meet"
    /// </summary>
    private static string MakeKey(string hostKey, string entryId)
        => $"{hostKey}:{entryId}";

    public EventQueueEntryState GetState(string hostKey, string entryId)
    {
        var fullKey = MakeKey(hostKey, entryId);
        if (string.IsNullOrEmpty(fullKey)) return new EventQueueEntryState();

        if (!states.TryGetValue(fullKey, out var state))
        {
            state = new EventQueueEntryState { EntryId = fullKey };
            states[fullKey] = state;
        }
        return state;
    }

    public void RecordTrigger(string hostKey, string entryId, bool consume)
    {
        var fullKey = MakeKey(hostKey, entryId);
        if (string.IsNullOrEmpty(fullKey)) return;

        var state = GetState(hostKey, entryId);
        state.TriggerCount++;
        state.StepsSinceLastTrigger = 0;
        if (consume) state.Consumed = true;
    }

    public void IncrementSteps()
    {
        foreach (var state in states.Values)
        {
            state.StepsSinceLastTrigger++;
        }
    }

    // セーブ/ロード用
    public List<EventQueueEntryState> Export() { ... }
    public void Import(List<EventQueueEntryState> dataList) { ... }
}
```

---

## 適用方針

### 方針A: 既存フィールドをEventQueueSOに置換

```csharp
// Before
public EventDefinitionSO eventDefinition;

// After
public EventQueueSO eventQueue;
```

**メリット**: シンプル、一貫性
**デメリット**: 既存データの移行が必要、単一イベントでもSO作成が必要

### 方針B: 既存フィールドを維持しつつEventQueue配列を追加

```csharp
// 既存を維持（後方互換）
public EventDefinitionSO eventDefinition;

// 追加（複数イベント用）
public EventQueueEntry[] eventQueue;
```

**メリット**: 後方互換、移行不要
**デメリット**: 二重管理、どちらを優先するか混乱の可能性

### 方針C: EventQueueEntryを直接配列で持つ（インライン）

```csharp
// 既存フィールドを廃止し、配列に統一
public EventQueueEntry[] events;
```

**メリット**: SOを別途作成不要、インスペクタで直接編集可能
**デメリット**: 共有不可（同じキューを複数箇所で使い回せない）

### 推奨: 方針C（インライン配列）

理由:
- **EventQueueSOの共有ニーズは低い**（各箇所で異なるイベントを設定する）
- **インスペクタ編集が直感的**（SOを別途作成する手間がない）
- **ForcedEventTriggerと同じパターン**（既存設計との一貫性）
- **既存データは移行スクリプトで対応**（1イベント→1要素の配列に変換）

---

## 全イベント発火箇所一覧

### EventQueue対象

| クラス | フィールド | 発火タイミング |
|--------|-----------|---------------|
| NodeSO | `onEnterEvent` | ノード入場時 |
| NodeSO | `onExitEvent` | ノード退場時 |
| CentralObjectSO | `eventDefinition` | アプローチ時 |
| SideObjectSO | `eventDefinition` | 選択時 |
| GateMarker | `gateEvent` | 門出現/通過/失敗時 |
| EncounterSO | `onWin` | 戦闘勝利時 |
| EncounterSO | `onLose` | 戦闘敗北時 |
| EncounterSO | `onEscape` | 戦闘逃走時 |

### 対象外

| クラス | フィールド | 理由 |
|--------|-----------|------|
| NodeSO | `forcedEventTriggers[]` | 既に配列で複数対応済み |
| ReactionSegment | `encounter` | ノベルパート内の選択肢であり、歩行システムのイベント発火箇所ではない |

---

## 変更対象一覧

| クラス | 変更内容 |
|--------|----------|
| CentralObjectSO | `eventDefinition` → `EventQueueEntry[] events` |
| SideObjectSO | `eventDefinition` → `EventQueueEntry[] events` |
| GateMarker | `gateEvent` → `EventQueueEntry[] gateEvents` |
| EncounterSO | `onWin/onLose/onEscape` → `EventQueueEntry[] onWinEvents` 等 |
| NodeSO | `onEnterEvent/onExitEvent` → 配列化 |

---

## ForcedEventTriggerとの統合

ForcedEventTriggerは既に類似の仕組みを持っている。統合の選択肢:

### 選択肢1: ForcedEventTriggerをEventQueueEntryベースに統合

```csharp
[Serializable]
public sealed class ForcedEventTrigger
{
    [Header("発火タイミング")]
    [SerializeField] private ForcedEventType type;  // Steps / Probability
    [SerializeField] private int stepCount;
    [SerializeField] private float probability;

    [Header("イベントキュー")]
    [SerializeField] private EventQueueEntry[] events;  // ←ここが配列に
}
```

### 選択肢2: 別システムとして維持

ForcedEventTriggerは「歩数/確率で自動発火」、EventQueueは「アプローチで発火」と役割が異なるため、別システムとして維持する。

### 推奨: 選択肢2（別システム維持）

理由:
- ForcedEventTriggerは「いつ発火するか」（歩数/確率）を持つ
- EventQueueは「何を発火するか」（イベント候補リスト）のみ
- 責務が異なるため、統合すると複雑化する

ただし、ForcedEventTrigger内のeventDefinitionをEventQueueEntry配列にすることは可能（将来対応）。

---

## 状態管理の統合

### 現状

- ForcedEventStateManager: ForcedEventTrigger用
- （新規）EventQueueStateManager: EventQueue用

### hostKey設計

```
hostKey = "{soType}:{soId}"
fullKey = "{hostKey}:{entryId}"

例:
- central:npc_merchant:first_meet
- central:npc_merchant:default
- side:treasure_chest:open
- gate:boss_gate:warning
- encounter:goblin:win_bonus
- node:forest:enter_greeting
```

**重要**: ノードIDは含めない。同じSOなら**ノード間で状態を共有**する。
別状態にしたければ、別のSOを作成する。

### 統合案（オプション）

両者を**1つのマネージャーに統合**することも可能。EntryIdの命名規則で区別:

```
ForcedEvent: "forced:{triggerId}"
EventQueue:  "{soType}:{soId}:{entryId}"

例:
- forced:story_event_01
- central:npc_merchant:greeting
- side:treasure_chest:first_open
```

統合は後回しでも動作する（Phase 7でオプション対応）。

---

## データ構造まとめ

```
EventQueueEntry
├── entryId: string          // 状態管理用の識別子
├── eventDefinition: EventDefinitionSO
├── conditions: ConditionSO[]
├── consumeOnTrigger: bool   // true=1回限り
├── cooldownSteps: int       // 再発火までの歩数
└── maxTriggerCount: int     // 最大発火回数（0=無制限）

EventEntryState（セーブ対象）
├── EntryId: string
├── Consumed: bool
├── TriggerCount: int
└── StepsSinceLastTrigger: int
```

---

## 発火ロジック（疑似コード）

```csharp
// アプローチ時の発火判定（CentralObject/SideObject等）
// hostKey例: "central:npc_merchant", "side:treasure_chest"
EventDefinitionSO ResolveEvent(EventQueueEntry[] entries, string hostKey)
{
    foreach (var entry in entries)
    {
        var state = stateManager.GetState(hostKey, entry.EntryId);
        // fullKey = "central:npc_merchant:first_meet"

        // 消費済み → スキップ
        if (entry.ConsumeOnTrigger && state.Consumed) continue;

        // 最大回数到達 → スキップ
        if (entry.MaxTriggerCount > 0 && state.TriggerCount >= entry.MaxTriggerCount) continue;

        // クールダウン中 → スキップ
        if (entry.CooldownSteps > 0 && state.StepsSinceLastTrigger < entry.CooldownSteps) continue;

        // 条件不一致 → スキップ
        if (!CheckConditions(entry.Conditions)) continue;

        // 発火可能 → このイベントを返す
        stateManager.RecordTrigger(hostKey, entry.EntryId, entry.ConsumeOnTrigger);
        return entry.EventDefinition;
    }

    // 全てスキップ → 発火なし
    return null;
}
```

---

## 使用例

### 中央オブジェクト（NPC商人）

```yaml
CentralObjectSO: Merchant
  events:
    - entryId: "first_meet"
      eventDefinition: MerchantFirstMeet
      conditions: [NotFlag("met_merchant")]
      consumeOnTrigger: true

    - entryId: "has_quest_item"
      eventDefinition: MerchantQuestComplete
      conditions: [HasFlag("has_quest_item")]
      consumeOnTrigger: true

    - entryId: "default"
      eventDefinition: MerchantShop
      conditions: []
      consumeOnTrigger: false  # 何度でも発火
```

**動作**:
1. 初回アプローチ → `first_meet` 発火（消費）
2. クエストアイテム持参 → `has_quest_item` 発火（消費）
3. それ以外 → `default` 発火（何度でも）

### サイドオブジェクト（宝箱）

```yaml
SideObjectSO: TreasureChest
  events:
    - entryId: "open"
      eventDefinition: ChestOpen
      conditions: []
      consumeOnTrigger: true
```

**動作**:
1. 初回アプローチ → `open` 発火（消費）
2. 2回目以降 → 発火なし（空の宝箱として表示のみ、または出現しなくなる）

---

---

## 廃止対象: fixed系フィールド

### 廃止理由

`fixedCentralObject` / `fixedSideObjects` は「ノード入場時（1歩目）だけ固定表示」する機能だが、テーブルの条件（Condition）で代替可能。

```yaml
# fixedを使う場合（廃止）
NodeSO:
  fixedCentralObject: NPC_初回挨拶

# テーブル条件で代替（推奨）
CentralObjectTableSO:
  entries:
    - centralObject: NPC_初回挨拶
      conditions: [NodeSteps == 0]  # ノード入場時のみ
      weight: 100
```

### 廃止フィールド

| クラス | フィールド | 代替方法 |
|--------|-----------|----------|
| NodeSO | `fixedCentralObject` | テーブルに条件付きエントリを追加 |
| NodeSO | `fixedSideObjects` | テーブルに条件付きエントリを追加 |

### 移行手順

1. 既存の `fixedCentralObject` / `fixedSideObjects` を調査
2. 該当するテーブルに条件付きエントリを追加
3. fixedフィールドを削除
4. 関連コード（SideObjectSelector, CentralObjectSelector, AreaController）から参照を削除

---

## 関連ドキュメント

- [ゼロトタイプ歩行システム設計書.md](./ゼロトタイプ歩行システム設計書.md)
- [ゼロトタイプ歩行システム仕様書.md](./ゼロトタイプ歩行システム仕様書.md)
- [ノード設定項目_現行実装.md](./ノード設定項目_現行実装.md)
- [基礎設計想起/event_kernel_拡張可能イベント機構_設計ドキュメント_v_0.md](./基礎設計想起/event_kernel_拡張可能イベント機構_設計ドキュメント_v_0.md)
