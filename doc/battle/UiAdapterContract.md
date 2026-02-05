# UI Adapter Contract (Phase 2a)

## 目的
- CoreRuntime と UI の境界を固定する
- UI側の責務と出力の受け口を明確にする
- 既存 UI 表示と演出を保ったまま差し替え可能にする

## UIアダプタの責務
- BattleEvent を購読して UI 表示を行う
- UIメッセージとUIログの表示を統一する
- UI演出（矢印/アクションマーク/選択UI）を担当する
- 戦闘コンテキストの参照を UI 側に提供する

## BattleEvent -> UI マッピング
| BattleEventType | UI 操作 | 備考 |
| --- | --- | --- |
| Message | MessageDropper.CreateMessage | 画面に表示するテキスト |
| Log | SchizoLog.AddLog | 表示前に履歴へ追加 |
| UiDisplayLogs | SchizoLog.DisplayAllAsync | 履歴は BattleEventHistory 参照 |
| UiNextArrow | ArrowManager.Next | 矢印キューを進める |
| UiMoveActionMark | IActionMarkController.MoveToActorScaled | Actor/Immediate/WaitForIntro を使用 |
| UiSetSelectedActor | CharaconfigController.SetSelectedByActor | 選択中の俳優表示 |
| UiSwitchAllySkillUiState | IPlayersSkillUI.OnlySelectActs + OnSkillSelectionScreenTransition | Actor/HasSingleTargetReservation を使用 |

## BattleEvent ペイロードの意味
- Actor: UI 側で対象キャラクターを特定する
- Immediate: アクションマーク移動の即時性
- WaitForIntro: Intro 演出の完了待ち
- HasSingleTargetReservation: 単体予約時のスキルUI制限

## 直接呼び出し（非イベント）
- BattleManager: ApplyVanguardEffect / PrepareBattleEnd / RestoreZoomViaOrchestrator / ShowActionMarkFromSpawn / SetArrowColorsFromStage / ClearArrows / HardStopAndClearLogs
- UI入力: SetUserUiState（SelectTargetButtons / SelectRangeButtons / SelectCancelPassiveButtons / AllyClass）
- UI入力: SetSkillUiState（UIStateHub 経由）

## 運用ルール
- CoreRuntime は BattleEvent のみを出力する
- UIアダプタは BattleEvent を購読し、UI副作用をここに閉じ込める
- ログ方針は doc/battle/EventPolicy.md に従う

## 実装メモ
- BattleUiEventAdapter が BattleEvent を IBattleUiAdapter に変換する
- BattleUIBridge は IBattleUiAdapter 実装として表示処理のみ担当する
