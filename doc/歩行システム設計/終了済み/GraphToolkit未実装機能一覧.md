# Graph Toolkit エディタ 未実装機能一覧

本書は既存の NodeSO システムと Graph Toolkit エディタの機能差分をまとめたもの。
`ノード設定項目_現行実装.md` を基準に調査。

調査日: 2026-01-23
更新日: 2026-01-23（exitSpawn, exitSelectionMode, maxExitChoices, progressKey, uiHints, retainUnselectedSide 実装）

---

## 実装状況サマリ

### FlowAreaNode（通常ノード）

| カテゴリ | 実装済み | 未実装 |
|---------|---------|--------|
| 基本情報 | 3/3 | 0 |
| サイドオブジェクト | 1/3 | 2 |
| 遭遇関連 | 1/2 | 1 |
| イベント関連 | 0/4 | 4 |
| 出口関連 | 5/6 | 1 |
| 進捗・門関連 | 3/4 | 1 |
| **合計** | **13/22** | **9** |

---

## 詳細: 実装済みフィールド

### 1. 基本情報（全て実装済み）

| フィールド | 型 | 状態 | 備考 |
|-----------|-----|------|------|
| nodeId | string | ✅ 実装済 | |
| displayName | string | ✅ 実装済 | |
| uiHints | NodeUIHints | ✅ 実装済 | 個別フィールドとして展開 |

#### NodeUIHints の実装状況

| フィールド | 状態 |
|-----------|------|
| useThemeColors | ✅ 実装済 |
| frameArtColor | ✅ 実装済 |
| twoColor | ✅ 実装済 |
| useActionMarkColor | ✅ 実装済 |
| actionMarkColor | ✅ 実装済 |
| backgroundId | ✅ 実装済 |

---

### 2. サイドオブジェクト関連

| フィールド | 型 | 状態 | 備考 |
|-----------|-----|------|------|
| sideObjectTable | SideObjectTableSO | ❌ 未実装 | SO参照 |
| fixedSideObjects | FixedSideObjectPair | ❌ 未実装 | 左右のSO参照 |
| retainUnselectedSide | bool | ✅ 実装済 | |

---

### 3. 遭遇関連

| フィールド | 型 | 状態 | 備考 |
|-----------|-----|------|------|
| encounterTable | EncounterTableSO | ❌ 未実装 | SO参照（Node Options非対応） |
| encounterRateMultiplier | float | ✅ 実装済 | |

---

### 4. イベント関連（全て未実装）

| フィールド | 型 | 状態 | 備考 |
|-----------|-----|------|------|
| onEnterEvent | EventDefinitionSO | ❌ 未実装 | SO参照 |
| onExitEvent | EventDefinitionSO | ❌ 未実装 | SO参照 |
| centralEvent | EventDefinitionSO | ❌ 未実装 | SO参照 |
| centralVisual | CentralObjectVisual | ❌ 未実装 | Sprite参照を含む構造体 |

#### CentralObjectVisual の内容

```
sprite: Sprite      ← SO参照のため未対応
size: Vector2
offset: Vector2
tint: Color
```

---

### 5. 出口関連

| フィールド | 型 | 状態 | 備考 |
|-----------|-----|------|------|
| exitSpawn | ExitSpawnRule | ✅ 実装済 | 個別フィールドとして展開 |
| exits | ExitCandidate[] | ✅ 部分実装 | 接続のみ、conditions未実装 |
| exitSelectionMode | ExitSelectionMode | ✅ 実装済 | ShowAll/WeightedRandom |
| maxExitChoices | int | ✅ 実装済 | 最大表示数 |
| exitVisual | ExitVisual | ❌ 未実装 | Sprite参照を含む構造体 |

#### ExitSpawnRule の実装状況

| フィールド | 状態 |
|-----------|------|
| mode | ✅ 実装済 |
| steps | ✅ 実装済 |
| rate | ✅ 実装済 |
| requireAllGatesCleared | ✅ 実装済 |

#### ExitVisual の内容（未実装）

```
sprite: Sprite          ← SO参照のため未対応
backSprite: Sprite      ← SO参照のため未対応
size: Vector2
offset: Vector2
tint: Color
backTint: Color
label: string
sfxOnAppear: string
```

---

### 6. 進捗・門関連

| フィールド | 型 | 状態 | 備考 |
|-----------|-----|------|------|
| trackConfig.length | int | ✅ 実装済 | |
| trackConfig.stepDelta | int | ✅ 実装済 | |
| trackConfig.progressKey | string | ✅ 実装済 | |
| gates | GateMarker[] | ⚠️ ContextNodeのみ | FlowAreaNodeでは未実装 |

---

## 詳細: ExitCandidate（出口候補）

現在の ExitPortInfo:

| フィールド | 状態 | 備考 |
|-----------|------|------|
| id | ✅ 実装済 | |
| toNodeId | ✅ 実装済 | ポート接続で自動設定 |
| uiLabel | ✅ 実装済 | 表示名 |
| weight | ✅ 実装済 | 重み |
| conditions | ❌ 未実装 | ConditionSO[]（SO配列のため未対応） |

---

## 詳細: GateBlockNode（門ブロック）

FlowAreaContextNode で使用する GateBlockNode:

| フィールド | 状態 | 備考 |
|-----------|------|------|
| gateId | ✅ 実装済 | |
| order | ✅ 実装済 | |
| positionSpec | ✅ 実装済 | 位置指定 |
| passConditions | ⚠️ 型不一致 | ScriptableObject[] → ConditionSO[] |
| onPass | ⚠️ 型不一致 | ScriptableObject[] → EffectSO[] |
| onFail | ⚠️ 型不一致 | ScriptableObject[] → EffectSO[] |
| gateEvent | ⚠️ 型不一致 | ScriptableObject → EventDefinitionSO |
| eventTiming | ✅ 実装済 | |
| visual | ❌ 未実装 | GateVisual 構造体 |

#### GateVisual の内容（未実装）

```
sprite: Sprite          ← SO参照のため未対応
size: Vector2
offset: Vector2
tint: Color
backSprite: Sprite      ← SO参照のため未対応
backTint: Color
backOffset: Vector2
backSize: Vector2
label: string
appearAnim: GateAppearAnimation
hideAnim: GateHideAnimation
sfxOnAppear: string
sfxOnPass: string
sfxOnFail: string
```

---

## 技術的制約

### Node Options の制約

Graph Toolkit の Node Options は以下の型のみ対応:
- プリミティブ型（int, float, string, bool）
- enum
- 一部の Unity 型（Color, Vector2 等）

**非対応:**
- ScriptableObject 参照
- 配列
- Sprite 参照

### 対応策

1. **SO参照**: 生成された FlowGraphSO を Inspector で追加編集
2. **構造体**: 個別フィールドとして展開（実装済み: ExitSpawnRule, NodeUIHints）
3. **配列**: BlockNode として視覚化（GateBlockNode, ExitBlockNode）
4. **Sprite参照**: Inspector で追加編集が必要

---

## 残りの未実装機能

### 実装不可（Node Options の制約）

以下は Node Options の制約により Graph Toolkit での編集は不可。Inspector での編集が必要:

1. **SO参照系**
   - encounterTable (EncounterTableSO)
   - sideObjectTable (SideObjectTableSO)
   - fixedSideObjects (FixedSideObjectPair)
   - onEnterEvent / onExitEvent / centralEvent (EventDefinitionSO)
   - ExitCandidate.conditions (ConditionSO[])

2. **Sprite参照を含む構造体**
   - centralVisual (CentralObjectVisual)
   - exitVisual (ExitVisual)
   - GateBlockNode.visual (GateVisual)

---

## 運用方針

Graph Toolkit で**構造と基本設定**を編集し、
**SO参照とビジュアル設定**は生成された FlowGraphSO を Inspector で編集する。

```
.flowgraph で編集:
  - ノードの追加・削除・配置
  - ノード間の接続
  - 基本設定（nodeId, displayName, trackConfig 等）
  - 出口設定（exitSpawn, exitSelectionMode, maxExitChoices）
  - UIヒント（テーマカラー等）
  - 門の構造（ContextNode + GateBlockNode）

Inspector で追加編集:
  - SO参照（encounterTable, sideObjectTable, eventDefinition 等）
  - ビジュアル設定（exitVisual, gateVisual, centralVisual）
  - 詳細条件（conditions 配列）
```

---

## 参考

- `doc/歩行システム設計/ノード設定項目_現行実装.md` - 既存システムの全フィールド
- `doc/歩行システム設計/GraphToolkitエディタ構想.md` - 設計ドキュメント
