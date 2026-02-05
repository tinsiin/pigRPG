# Regression Checklist (Phase 6a)

## 目的
- 重大シナリオを短手順で再現できることを確認する
- 変更後の挙動ズレを最小コストで検出する

## 前提
- `BattleEventRecorder` が `BattleManager` で有効になっている
- `doc/battle/CombatScenarios.md` のシナリオが実施可能

## 記録手順（最小）
1. 通常戦闘/逃走/全滅のいずれかを実行
2. 戦闘終了後に `BattleManager.EventRecorder` から `Inputs` を取得
3. 戦闘結果（勝利/逃走/敗北）とターン数を控える

## 再生手順（最小）
1. 同条件で新規戦闘を開始
2. `BattleEventReplayer(session, inputs)` を生成
3. `ReplayAsync()` を実行
4. `BattleEnded` 到達と結果一致を確認

補足
- `BattleOrchestrator.ReplayInputsAsync(inputs)` を使っても良い

## 合格基準
- `BattleEnded` が必ず発行される
- UIが歩行状態へ戻る
- 記録時と結果が一致する
