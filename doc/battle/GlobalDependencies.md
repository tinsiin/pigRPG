# Global Dependencies (Battle / UI 周辺)

## Battle Context
- `BattleContextHub.Current` は `BattleManager` が Set/Clear し、`BaseStates` / `BaseSkill` / `BasePassive` / `BattleAIBrain` が参照する

## Battle UI Bridge
- `BattleUIBridge.Active` は UI 側から参照され、`SelectTargetButtons` / `SelectRangeButtons` / `AllyClass` などが利用する
- `BattleOrchestratorHub.Current` は `BattleUIBridge` や UI 入力側のフォールバックに使われる

## UI Singletons
- `WatchUIUpdate.Instance` は `BattleManager` / `BattleInitializer` / `BattleIconUI` / `WalkingSystemManager` が参照する
- `SchizoLog.Instance` は BattleUIBridge のログ表示に使われる
- `BattleSystemArrowManager.Instance` は矢印描画や反応スキルで参照される

## Walking
- `Walking.Instance` は `WalkingSystemManager` がフォールバック参照する

## Notes
- これらは UI 統合の都合で残っている依存であり、Phase 2 でラップ/注入に置き換える対象