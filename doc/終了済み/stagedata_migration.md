# StageData移行計画（旧StageData/StageCut -> 新歩行システム）

## クローズ: 2026-01-16

### 完了事項
- 旧StageData/StageCut/AreaDateの全機能を新歩行システムに移植完了
- 新システム（Assets/Script/Walk/）は旧システムへの参照を一切持たない
- WalkMigration フォルダのSOに旧データ（色・名前等）を移植済み
- 旧 Stages.cs は Assets/Script/Archive/ にアーカイブ

### 移植済み機能一覧
| 旧機能 | 新システム |
|---|---|
| EnemyCollectAI | EncounterEnemySelector.SelectGroup() |
| StagesBonus | StageBonus + ApplyStageBonusEffect |
| StageThemeColorUI | NodeUIHints |
| SideObject | SideObjectTableSO + SideObjectPresenter |
| EncounterRate | EncounterTableSO + EncounterResolver |
| EscapeRate | EncounterSO.EscapeRate |

---

## 前提
- 仕様の最終決定は「歩行システム設計フォルダ」内のドキュメントを優先する。
- 旧仕様との完全互換は行わない。
- reencount（再遭遇）周りは新システム仕様でOKとする。
- MapLineS / MapLineE / MapSrc は使わない（完全に切り捨て）。
- 移行ツールは削除済み（再作成しない）。

## 目的
- 旧StageData/StageCut/AreaDate の内容を新システムのデータ構造へ移行し、旧ステージ依存を解消する。
- New Walk System を単独で運用できる状態にする。

## 旧データ -> 新データのマッピング方針
| 旧フィールド | 新システムでの置き換え | 備考 |
|---|---|---|
| StageData.StageName | Node表示名 | Node側に名称フィールドを追加する |
| StageData.StageThemeColorUI | NodeのUIヒント（Frame/Two/ActionMark） | 旧StageData参照を削除しNode側に移管 |
| StageData.*_StageBonus | Node.onEnterのEffectで適用 | 旧ボーナスはEventKernelで表現する |
| StageCut.AreaName / Id | NodeId | 旧StageCutは使わずStageData単位でNodeを作る |
| StageCut.EnemyList | EncounterSO.enemyList | ランタイムはDeepCopyして使用 |
| StageCut.EncounterRate | EncounterTableSO.baseRate | 旧の確率を移植 |
| StageCut.EscapeRate | EncounterSO.escapeRate | 旧の逃走率を移植 |
| StageCut._sideObject_Lefts/_Rights | SideObjectTableSO entries | Left-only / Right-only で登録する |
| AreaDate.NextID/NextIDString | ExitCandidate(id/label) | 分岐は ExitCandidate で表現 |
| AreaDate.NextStageID | ExitCandidate(toNodeId) | ノード間遷移で表現 |
| AreaDate.Rest | EventDefinitionSO + Effect | 休憩イベントとして実装 |
| AreaDate.BackSrc | Nodeの背景ヒント | 新UI用に背景IDを持たせる |
| MapLineS/MapLineE/MapSrc | なし | 完全に切り捨て |

## 新システム側に追加する必要がある受け口
1) Nodeの表示名
- Node表示名を追加し、UI側で参照できるようにする

2) Node UIヒント
- Frame/Two/ActionMark 色
- 背景ID（BackSrc相当）

3) StageBonus適用Effect
- 旧StageBonusをEffect化（例: ApplyStageBonusEffect）
- Node.onEnterで適用

4) Rest/回復イベント
- AreaDate.Rest を EventDefinitionSO に置換
- EventKernelで実行

## 移行ステップ（手作業前提）
### Step 1: データ構造の拡張
- NodeSOに「表示名」「UIヒント」「背景ID」などを追加
- StageBonus/Rest をEffect化して EventKernel で実行できるようにする

### Step 2: FlowGraph/Node資産の作成
- StageData 1つにつき NodeSO を1つ作る（Node = Stage）
- FlowGraphSOはNode群の入れ物。現段階は「1 Stage = 1 Node」なので、FlowGraphは1 Stageごと（または1エリア単位）でOK
- FlowGraph同士は直接つながらない。ノード遷移はExitCandidateの toNodeId で表現する
- NodeIdは旧StageCut.Idではなく、StageData側の識別子を新規付与する

### Step 3: 旧データの移行
- EncounterTableSO/EncounterSO を作成し、EnemyList/EncounterRate/EscapeRate を移植
- SideObjectTableSO を作成し、Left/Right prefab を登録
- ExitCandidate を作成し、NextID/NextIDString/NextStageID を反映
- Rest/BackSrc は Event/背景ヒントに移植

### Step 4: 旧ステージ依存の削除
- WalkingSystemManager の StageData 参照を除去
- WatchUIUpdate/ActionMarkUI の更新を Node UIヒント由来に切り替え

### Step 5: 動作確認
- 1ノードで歩行→遭遇→戦闘→復帰が動作する
- ExitCandidateの分岐が正しく動作する
- 色/背景が旧StageDataと同等に反映される
- 旧StageDataを参照しないことを確認

## 実装タスク（MCP利用込み）
### A. データ/コード拡張
- NodeSOに表示名/UIヒント/背景IDフィールドを追加
- StageBonus/Rest のEffect化 + EventKernel対応
- WalkingSystemManager/WatchUIUpdate の参照先を Node UIヒント に置換
- PlayersProgress/WalkCounters など進行系の参照を整理（StageData依存を排除）

### B. 資産作成（手動+MCP）
- 旧StageData/StageCutの一覧を把握（MCP: manage_asset action=search）
- StageDataごとに以下を作成
  - FlowGraphSO / NodeSO
  - EncounterTableSO / EncounterSO
  - SideObjectTableSO（Left/Right）
  - EventDefinitionSO（Rest/StageBonus）
- NodeSOへ参照を設定（Encounter/SideObjects/ExitCandidates/Events）
- 既存のSideObjects/Enemiesはテスト用に既存Prefab/EnemySOを流用

### C. シーン/参照更新（MCP）
- シーン内の WalkingSystemManager を検索し、FlowGraphSO を割り当て（MCP: find_gameobjects + manage_components set_property）
- WalkApproachUI/SideObjectPresenter/CentralObjectPresenter の参照が未設定なら補完

### D. 旧ステージ依存の撤去
- useNewWalkSystem を固定化し、StageData参照コードを削除
- MapLine系のUI更新は削除 or 無効化

### E. 検証
- 1ノードで歩行→遭遇→戦闘→復帰→出口分岐が成立
- 色/背景が旧StageDataと同等に反映
- 旧StageData削除後も動作

## 受け入れ条件
- useNewWalkSystem で旧ステージ関連の参照が不要になる
- 旧StageData を参照しないことを確認済み

## 注意点
- reencountの進捗は新システム仕様（WalkCounters基準）に固定
- MapLine系は破棄するため、旧マップUIは維持しない
- 移行は完了しているため、再変換は行わない
