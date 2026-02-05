# Core Runtime Map (Phase 1a)

## 目的
- CoreRuntime に移す範囲と UI に残す範囲を明確化する
- 依存の境界を固定し、移行中のブレを防ぐ

## 目標フォルダ
- `Assets/Script/Battle/CoreRuntime`  
  戦闘ロジック本体。UI型に依存しないこと。
- `Assets/Script/Battle/UI`  
  UI表示・演出・入力ハンドリング。
- `Assets/Script/Battle/Integration`  
  起動/終了/依存注入/歩行連携の接続点。

## CoreRuntime（移動済み）
- `Assets/Script/Battle/CoreRuntime/BattleFlow.cs`
- `Assets/Script/Battle/CoreRuntime/TurnExecutor.cs`
- `Assets/Script/Battle/CoreRuntime/SkillExecutor.cs`
- `Assets/Script/Battle/CoreRuntime/ActionSkipExecutor.cs`
- `Assets/Script/Battle/CoreRuntime/EscapeHandler.cs`
- `Assets/Script/Battle/CoreRuntime/BattlePresentation.cs`
- `Assets/Script/Battle/CoreRuntime/BattleActionContext.cs`
- `Assets/Script/Battle/CoreRuntime/ActionQueue.cs`
- `Assets/Script/Battle/CoreRuntime/BattleState.cs`
- `Assets/Script/Battle/CoreRuntime/BattleStateManager.cs`
- `Assets/Script/Battle/CoreRuntime/TargetingPlan.cs`
- `Assets/Script/Battle/CoreRuntime/UnderActersEntryList.cs`
- `Assets/Script/Battle/CoreRuntime/BattleEventBus.cs`
- `Assets/Script/Battle/CoreRuntime/Events/BattleEvent.cs`
- `Assets/Script/Battle/CoreRuntime/Events/BattleEventRecorder.cs`
- `Assets/Script/Battle/CoreRuntime/Events/BattleInputRecord.cs`
- `Assets/Script/Battle/CoreRuntime/Events/BattleEventReplayer.cs`
- `Assets/Script/Battle/CoreRuntime/Services/TurnScheduler.cs`
- `Assets/Script/Battle/CoreRuntime/Services/TargetingService.cs`
- `Assets/Script/Battle/CoreRuntime/Services/TargetSelectionHelper.cs`
- `Assets/Script/Battle/CoreRuntime/Services/TargetingPolicyRegistry.cs`
- `Assets/Script/Battle/CoreRuntime/Services/EffectResolver.cs`
- `Assets/Script/Battle/CoreRuntime/Services/IBattleRandom.cs`
- `Assets/Script/Battle/CoreRuntime/Services/SystemBattleRandom.cs`
- `Assets/Script/Battle/CoreRuntime/Services/IBattleLogger.cs`
- `Assets/Script/Battle/CoreRuntime/Services/NoOpBattleLogger.cs`

## UI に残すもの
- `Assets/Script/Battle/UI/BattleUIBridge.cs`
- `Assets/Script/Battle/UI/BattleUiEventAdapter.cs`
- `Assets/Script/Battle/UI/IBattleUiAdapter.cs`
- `Assets/Script/Battle/UI/NoOpBattleUiAdapter.cs`
- `Assets/Script/Battle/UI/IBattleUiBridgeAccessor.cs`
- `Assets/Script/SelectRangeButtons.cs`
- `Assets/Script/SelectTargetButtons.cs`
- `Assets/Script/SelectCancelPassiveButtons.cs`
- `Assets/Script/EYEAREA_UI/BattleIconUI.cs`

## Integration に寄せるもの
- `Assets/Script/BattleManager.cs`
- `Assets/Script/Battle/BattleInitializer.cs`
- `Assets/Script/Walk/Battle/UnityBattleRunner.cs`
- `Assets/Script/Walking.cs`
- `Assets/Script/Battle/UI/BattleOrchestrator.cs`
- `Assets/Script/Battle/Core/IBattleLifecycle.cs`
- `Assets/Script/Battle/Core/BattleServices.cs`
- `Assets/Script/Battle/Core/IBattleContextAccessor.cs`
- `Assets/Script/Battle/Core/BattleOrchestratorHub.cs`
- `Assets/Script/Battle/Core/BattleSession.cs`
- `Assets/Script/Battle/Integration/UnityBattleRandom.cs`
- `Assets/Script/Battle/Integration/UnityBattleLogger.cs`

## 依存ルール（Phase 1 の合意）
- CoreRuntime は `BattleUIBridge` と UI型に依存しない
- UI は `BattleEvent` を購読して表示する
- Integration が Core と UI を接続する

## 既知の UI 依存（Phase 2 で解消）
- CoreRuntime は `BattleEvent.Ui*` を発行して UI 操作を指示している
- UI アダプタ側で `Ui*` の解釈が必要
