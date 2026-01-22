# EdgeSO廃止計画

**ステータス: 完了（2026-01-22）**

## 概要

EdgeSO（グラフレベルの遷移定義）を廃止し、ExitCandidate（ノードレベルの出口定義）に統一した。

### 廃止理由

1. **機能の重複**: EdgeSOとExitCandidateはほぼ同じ機能（toNodeId, weight, conditions）
2. **Graph Toolkit非互換**: ワイヤーにメタデータを載せられないため、EdgeSOは不適合
3. **データの分散**: 遷移情報がグラフとノードに分散して管理が複雑
4. **フォールバック専用**: 現状ExitCandidateがあればEdgeSOは無視される

### 統一先

ExitCandidateに統一。uiLabelを持ち、ノード内にインラインで保存されるため安定。

---

## Phase 1: コード削除

### 削除対象ファイル

| ファイル | 対応 |
|----------|------|
| `Assets/Script/Walk/EdgeSO.cs` | 完全削除 |

### 修正対象ファイル

| ファイル | 修正内容 |
|----------|----------|
| `Assets/Script/Walk/FlowGraphSO.cs` | `edges[]`フィールド削除、`GetEdgesFrom()`削除 |
| `Assets/Script/Walk/ExitResolver.cs` | EdgeSOフォールバック処理削除、`FromEdge()`削除、`IsFromEdge`フィールド削除 |
| `Assets/Editor/Walk/GraphValidator.cs` | Edge関連のバリデーション削除 |

### ExitResolver.cs 修正詳細

```csharp
// 削除: ResolvedExit.IsFromEdge フィールド
// 削除: ResolvedExit.FromEdge() メソッド
// 削除: GatherCandidates() 内のEdgeフォールバック処理（94-105行目）
```

### GraphValidator.cs 修正詳細

```csharp
// 削除: ValidateNode() 内のEdge検証（80-81行目、106-114行目）
// 削除: ValidateReachability() 内のEdge追跡（146-155行目）
```

---

## Phase 2: ドキュメント修正

### 修正対象ドキュメント

| ファイル | 記述箇所 | 対応 |
|----------|----------|------|
| `GraphToolkitエディタ構想.md` | 全体 | EdgeSO → ExitCandidateに書き換え |
| `ゼロトタイプ歩行システム設計書.md` | 多数 | Edge関連記述を削除/修正 |
| `ノード設定項目_現行実装.md` | なし | 変更不要（ExitCandidateのみ記載） |
| `歩行システム動作解説.md` | なし | 変更不要（Edge記述なし） |
| `ゼロトタイプ歩行システム仕様書.md` | なし | 変更不要（Edge記述なし） |

### 古いドキュメント（参照のみ/廃止検討）

以下は初期設計時のドキュメントで、現行実装と乖離している可能性:

| ファイル | 状態 |
|----------|------|
| `event_kernel_拡張可能イベント機構_設計ドキュメント_v_0.md` | 初期設計、UnlockEdge等の記述あり |
| `flow_graph_中核仕様_v_0_1_（暫定・歩行_分岐_遭遇の基盤設計）.md` | 初期設計、EdgeSO定義あり |
| `flow_graph_拡張案_v_0_1_（gate_track_pool_anchor_ほか）.md` | 初期設計 |
| `歩行システム導入アプローチ（会話先行・最小コア）v_0.md` | 初期設計、Edge遷移ベース |
| `統合分析レポート_既存システムとの共存可能性.md` | 分析レポート、EdgeSO多数 |
| `批判的評価_最小構成から段階的拡張の妥当性.md` | 評価レポート |
| `拡張案_各ゲームシステム統合インターフェース.md` | 拡張案 |

→ これらは「終了済み」フォルダに移動するか、冒頭に「このドキュメントは初期設計であり、現行実装と異なる」注記を追加

---

## Phase 3: アセット確認

### 確認対象

```
Assets/ScriptableObject/WalkSO/**/Edge*.asset
```

既存のEdgeSOアセットがあれば削除対象。

---

## 実行順序

1. [x] **Phase 1-1**: EdgeSO.cs削除
2. [x] **Phase 1-2**: FlowGraphSO.cs修正（edges[]、GetEdgesFrom()削除）
3. [x] **Phase 1-3**: ExitResolver.cs修正（フォールバック削除）
4. [x] **Phase 1-4**: GraphValidator.cs修正（Edge検証削除）
5. [x] **Phase 1-5**: WalkConditionCollector.cs修正（Edge収集削除）
6. [x] **Phase 1-6**: AreaController.cs修正（ResolveExitsシグネチャ変更）
7. [x] **Phase 2-1**: GraphToolkitエディタ構想.md修正
8. [x] **Phase 2-2**: ゼロトタイプ歩行システム設計書.md修正
9. [x] **Phase 2-3**: 歩行システム動作解説.md確認 → 変更不要
10. [x] **Phase 2-4**: ゼロトタイプ歩行システム仕様書.md確認 → 変更不要
11. [x] **Phase 2-5**: 古いドキュメントの整理（終了済みフォルダに移動）
12. [x] **Phase 3**: EdgeSOアセット確認/削除（Edge_Entrance_to_TestBranch.asset削除）
13. [ ] **最終確認**: テスト実行、動作確認

---

## 影響範囲

### 影響なし

- ランタイムの歩行ロジック（ExitCandidateで動作）
- 既存のNodeSOアセット（ExitCandidateを使用）
- バトルシステム、UI等

### 影響あり

- FlowGraphSOの構造（edgesフィールド削除）
- 既存のEdgeSOアセット（あれば削除）
- ドキュメント（多数修正）

---

## 備考

- Graph Toolkit導入時にはExitCandidateベースで設計する
- ワイヤー（線）は接続情報のみ、メタデータはノード上のフィールドで管理
- この変更により、遷移情報が一元管理されシンプルになる
