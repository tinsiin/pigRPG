# Walking Exit Flow (Phase 5a)

## 目的
- 歩行 → 戦闘 → 歩行の起動/終了経路を単一ルート化する
- どの入口から戦闘を開始しても同じ終了処理に合流させる
- UI/演出と戦闘ロジックの境界を固定する

## 現状の起動経路（把握用）
1. `Walking.OnWalkBtn` → `WalkingSystemManager.RunOneStepAsync`
2. `AreaController` / `EncounterResolver` → `UnityBattleRunner.RunBattleAsync`
3. `BattleInitializer.InitializeBattle` → `BattleOrchestrator` 生成
4. `BattleInitializer.SetupInitialBattleUI` → `BattleOrchestrator.StartBattle`
5. `Walking.BeginBattle` で UI を NextWait へ遷移

## 現状の終了経路（把握用）
1. `Walking.OnClickNextWaitBtn` → `BattleOrchestrator.RequestAdvanceAsync`
2. `BattleManager.CharacterActBranching` が `TabState.walk` を返す
3. `BattleOrchestrator.EndBattle` → `BattleManager.OnBattleEnd`
4. UI復帰・ズーム復帰・Hubクリアなどのクリーンアップ

## 目標の単一ルート（Start）
1. `BattleInitializer` が `BattleSession` を生成する
2. `UnityBattleRunner` が `Session.Start()` を呼ぶ
3. `Session.Advance()` の結果で初期UI状態を決定する
4. `Walking.BeginBattle` は `IBattleSession` を受け取り、UI更新だけ行う

## 目標の単一ルート（End）
1. UIからの進行要求は `Session.ApplyInputAsync(BattleInput.Next())` に統一
2. 終了判定は `Session` 側で完結させる
3. `Session.EndAsync()` のみが終了処理を実行する
4. `Walking` は `EndAsync()` 完了後にUIを歩行モードへ戻す

## 追加方針（Phase 5bで実装）
1. `Walking.BeginBattle` を `IBattleSession` 受け取りに拡張
2. `UnityBattleRunner` は `Session.Start/End` を必ず呼ぶ
3. `BattleOrchestrator` は UIステート専用に縮退
4. `BattleManager.OnBattleEnd` の呼び出し口を `Session.EndAsync` に固定
