# Battle Lifecycle (現行実装)

## Entry
- `UnityBattleRunner.RunBattleAsync` が `EncounterContext` を受け取る
- `BattleInitializer.InitializeBattle` が `BattleOrchestrator` を生成し `BattleOrchestratorHub.Set` を行う
- `playersSkillUI.OnBattleStart` を呼び、`UIStateHub.EyeState = Battle` の後に `WatchUIUpdate.FirstImpressionZoomImproved` を実行
- `BattleInitializer.SetupInitialBattleUI` -> `BattleOrchestrator.StartBattle` -> `BattleManager.ACTPop` が最初の `TabState` を返す
- `Walking.BeginBattle` によりバトルUIが起動される

## Runtime
- UI側の入力は `BattleOrchestrator.ApplyInput` が受け取り、`UpdateChoiceState` で UI 状態を更新する
- 進行は `BattleOrchestrator.RequestAdvance` -> `StepInternal` -> `BattleManager.CharacterActBranching` -> `BattleFlow.CharacterActBranching`
- `BattleFlow` が `SkillExecutor` / `EscapeHandler` / `ActionSkipExecutor` を選択して進行する

## End
- `BattleFlow.DialogEndAct` が `_onBattleEndCallback` を呼び `BattleManager.OnBattleEnd` を実行
- `BattleManager.OnBattleEnd` が UI を戻し、`BattleContextHub.Clear` と `BattleUIBridge.SetActive(null)` を実行
- `UnityBattleRunner.WaitForBattleEnd` が `BattleOrchestrator.Phase == Completed` を待つ

## Files
- `Assets/Script/Walk/Battle/UnityBattleRunner.cs`
- `Assets/Script/Battle/BattleInitializer.cs`
- `Assets/Script/Battle/UI/BattleOrchestrator.cs`
- `Assets/Script/BattleManager.cs`
- `Assets/Script/Battle/CoreRuntime/BattleFlow.cs`
