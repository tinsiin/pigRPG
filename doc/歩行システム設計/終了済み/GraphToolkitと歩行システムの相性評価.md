# Graph Toolkit と歩行システムの相性評価

本書は Unity Graph Toolkit を歩行システムの編集基盤として採用することの妥当性を評価したもの。

評価日: 2026-01-23

---

## 結論

**Graph Toolkit は歩行システムの編集基盤としては不向き。可視化ツールとしてのみ有用。**

| 用途 | 評価 |
|------|------|
| 編集基盤（データのソース） | ❌ 不向き |
| 可視化ツール（認識補助） | ✅ 有用 |

---

## 背景：歩行システムのイベント設計

ゼロトタイプ歩行システムでは、様々なゲームシステム（バトル、会話、推理など）が **特定の箇所に依存せず、複数の呼び出し元から起動される** 設計となっている。

### イベントの呼び出し元

```
アプローチ可能オブジェクト
├─ サイドオブジェクト（左右の寄り道）
├─ 中央オブジェクト（中央の任意イベント）
├─ イベント門（突破すべき障壁）
└─ 出口（ノード遷移）

強制イベント
├─ onEnter（ノード入場時）
├─ onExit（ノード退場時）
└─ 歩数トリガー（特定歩数到達時）
```

### 呼び出されるゲームシステム

```
EventDefinitionSO → EventHost → EventRunner
                         ↓
    ┌─────────────────────────────────────┐
    │ BattleManager（戦闘）               │
    │ DialogueRunner（会話・ノベル）       │
    │ PuzzleSystem（推理・パズル）         │
    │ TradeSystem（取引・ショップ）        │
    │ その他カスタムシステム               │
    └─────────────────────────────────────┘
```

### 重要な設計原則

- イベントは **呼び出し元に依存しない**
- 同じ EventDefinitionSO をサイドオブジェクトからもイベント門からも呼べる
- イベント門での推理結果（フラグ）が、後続のイベント門での戦闘相手に影響する
- この柔軟性を実現するには **SO参照** が核心

---

## Graph Toolkit の技術的制約

### Node Options で設定可能な型

```
✅ プリミティブ型（int, float, string, bool）
✅ enum
✅ 一部の Unity 型（Color, Vector2）
```

### Node Options で設定不可能な型

```
❌ ScriptableObject 参照
❌ 配列
❌ Sprite / Texture 参照
❌ カスタムクラス・構造体
```

### サブアセットの制約

ScriptedImporter で生成されたサブアセット（NodeSO等）は **読み取り専用** であり、Inspector で編集できない。

```
.flowgraph (ソースファイル)
    ↓ インポート時に毎回再生成
FlowGraphSO + NodeSO (サブアセット) ← 編集不可
```

---

## 歩行システムとの相性問題

### 問題1: イベント参照が設定できない

```csharp
// 歩行システムで必要な設定
NodeSO {
    EventDefinitionSO onEnterEvent;     // ← 設定不可
    EventDefinitionSO centralEvent;     // ← 設定不可
    SideObjectTableSO sideObjectTable;  // ← 設定不可
    EncounterTableSO encounterTable;    // ← 設定不可
}

GateMarker {
    ConditionSO[] passConditions;       // ← 設定不可
    EffectSO[] onPass;                  // ← 設定不可
    EventDefinitionSO gateEvent;        // ← 設定不可
}
```

### 問題2: 編集場所が分散する

Graph Toolkit を使う場合、以下のような二重管理が発生する：

```
.flowgraph で編集:
  - ノード接続
  - 基本設定（ID、名前、トラック長など）

別のSOで編集:
  - EventDefinitionSO（イベント内容）
  - SideObjectTableSO（サイドオブジェクト候補）
  - EncounterTableSO（遭遇候補）
  - ConditionSO / EffectSO（条件・効果）

結局どこかで紐付けが必要:
  - .flowgraph では SO を参照できない
  - 生成された NodeSO は編集不可
  - → 詰み
```

### 問題3: イベントシステムの核心がSO参照

```
歩行システムの本質:
  「どの箇所で」「どのイベントを」起動するか

Graph Toolkit でできること:
  「どの箇所で」← ノード接続で表現可能
  「どのイベントを」← SO参照なので設定不可
```

---

## 推奨される運用

### 編集基盤：従来のSO方式を維持

```
FlowGraphSO (手動作成 or スクリプト生成)
├─ NodeSO[] nodes
│   ├─ EventDefinitionSO onEnterEvent  ← Inspector で直接設定
│   ├─ SideObjectTableSO sideObjects   ← Inspector で直接設定
│   ├─ GateMarker[] gates              ← Inspector で直接設定
│   └─ ...
└─ startNodeId
```

利点:
- 全フィールドが編集可能
- SO参照を自由に設定
- 配列も問題なく扱える
- 既存の EventHost / EventRunner との連携がスムーズ

### 可視化ツール：Graph Toolkit を補助利用

```
用途:
  - ノード接続の全体像を視覚的に確認
  - 新規ステージの構造設計（ラフスケッチ）
  - 接続ミスの発見

注意:
  - データのソースとしては使わない
  - 生成された SO は参照のみ
  - 実際のゲームデータは FlowGraphSO を直接編集
```

---

## 将来の代替案

### 案1: カスタム可視化エディタ

SO ベースのデータを維持しつつ、読み取り専用のグラフビューワーを作成。

```
FlowGraphSO (編集可能)
    ↓
カスタムエディタウィンドウ
    - ノード配置を自動計算
    - 接続線を描画
    - クリックで NodeSO の Inspector を開く
```

### 案2: Graph Toolkit の将来バージョン待ち

Unity Graph Toolkit は experimental (0.4.0-exp.2) であり、将来的に SO 参照のサポートが追加される可能性がある。ただし、現時点では期待できない。

---

## まとめ

| 観点 | Graph Toolkit | 従来のSO方式 |
|------|--------------|-------------|
| ノード接続の可視化 | ✅ 優秀 | ❌ なし |
| SO参照の設定 | ❌ 不可 | ✅ 自由 |
| 配列の編集 | ❌ 不可 | ✅ 自由 |
| サブアセットの編集 | ❌ 読み取り専用 | ✅ 自由 |
| イベントシステム連携 | ❌ 間接的 | ✅ 直接 |
| 開発効率 | ⚠️ 二重管理 | ✅ 一箇所で完結 |

**歩行システムの編集基盤としては従来の SO 方式を維持し、Graph Toolkit は構造確認の補助ツールとして位置付けるのが妥当。**

---

## 関連ドキュメント

- `ゼロトタイプ歩行システム設計書.md` - 歩行システムの設計全体
- `GraphToolkitエディタ構想.md` - Graph Toolkit 導入の初期構想
- `GraphToolkit未実装機能一覧.md` - 実装状況の詳細
