# Walking System Integration (現行実装)

## Primary API
- `IBattleRunner.RunBattleAsync(EncounterContext)` が歩行・イベント系の共通入口
- `GameContext.BattleRunner` は `WalkingSystemManager.EnsureBattleRunner` が設定

## Integration Points
- `AreaController.RunEncounter` が `context.BattleRunner.RunBattleAsync` を呼ぶ
- `BattleStep.ExecuteAsync` が `BattleRunner` を使って戦闘を起動し、結果に応じて Effect または Encounter の Event を実行
- `NovelDialogueStep` の ReactionType.Battle が `BattleRunner` を経由して戦闘を実行
- `LaunchBattleEffect` が `BattleRunner` を直接呼ぶ

## Flow
- `WalkingSystemManager` が `UnityBattleRunner` を生成し `GameContext.BattleRunner` に登録
- `UnityBattleRunner.RunBattleAsync` が `BattleInitializer` で `BattleOrchestrator` を生成
- `walking.BeginBattle` で戦闘UI開始、`WaitForBattleEnd` で完了待機
- `BattleResult` が歩行側に返り、AreaController / EventStep が結果に応じた処理を行う

## Files
- `Assets/Script/Walk/Battle/IBattleRunner.cs`
- `Assets/Script/Walk/Battle/UnityBattleRunner.cs`
- `Assets/Script/Walk/WalkingSystemManager.cs`
- `Assets/Script/Walk/AreaController.cs`
- `Assets/Script/Walk/EventKernel/Steps/BattleStep.cs`
- `Assets/Script/Walk/EventKernel/Steps/NovelDialogueStep.cs`
- `Assets/Script/Walk/Effects/LaunchBattleEffect.cs`