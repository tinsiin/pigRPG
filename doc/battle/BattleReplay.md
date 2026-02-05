# Battle Replay (Phase 6a)

## 目的
- BattleEvent の記録/再生を成立させる
- 重大シナリオの再現を最小手順で可能にする
- 回帰チェックの基盤を整備する

## 記録（Recorder）
- `BattleEventRecorder` が `BattleEventBus` を購読して `BattleEvent` を記録する
- `BattleSession` が入力適用時に `BattleEventRecorder.RecordInput` を呼び、`BattleInputRecord` を記録する
- 入力ログは UI 表示に流さず、再生/分析向けに保持する

## 再生（Replayer: 最小方針）
1. `BattleSession.Start()` を実行
2. `BattleEventReplayer` を `BattleInputRecord` で構築
3. `ReplayAsync()` で入力を順次適用
4. `BattleEnded` を受信したら停止

※ 現時点では乱数や時間の決定性は保証しない（Phase 7/8 で対応）

## デバッグ用呼び出し口
- `BattleOrchestrator.ReplayInputsAsync(inputs)`
- `BattleOrchestrator.ReplayRecordedInputsAsync()`

※ どちらも `Session.Start()` を内部で行うため、同一戦闘で `StartBattle()` を先に呼ばないこと。  
※ 再生完了時に `BattlePhase.Completed` をセットする（UI状態は `TabState.walk` になる）。

## 再生フォーマット（暫定）
`BattleInputRecord` を最小フォーマットとする。

- `Type` : `BattleInputType`
- `ActorName` : 入力者名
- `SkillName` : 使用スキル名（必要時）
- `TargetWill` : 指定ターゲット方針
- `RangeWill` : 範囲指定
- `IsOption` : オプション選択フラグ
- `TargetNames` : 明示ターゲット（任意）
- `TurnCount` : 記録ターン

## 最低限の回帰シナリオ
- 通常戦闘: 開始 → 数ターン進行 → 勝利
- 逃走: 逃走選択 → 戦闘終了
- 全滅: 敵優勢で敗北

## 注意点
- UI専用イベントは再生時に無視して良い
- 再生は「入力の順序」と「結果の一致」を重視する
