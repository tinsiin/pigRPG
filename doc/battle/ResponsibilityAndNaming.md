# Responsibility & Naming Guide (Phase 6a)

## 目的
- 主要クラスの責務を明確にする
- 命名のブレを抑えて拡張時の迷いを減らす

## 主要クラスの責務（要約）
- `BattleSession`: 戦闘の公開API。入力適用/進行/終了を統一する。
- `BattleFlow`: 戦闘の進行順序を司る。状態遷移のハブ。
- `TurnExecutor`: ターン実行の最小単位を担う。
- `SkillExecutor`: スキル実行と効果解決の窓口。
- `BattleActionContext`: 戦闘中の状態を集約する。
- `BattleEventBus`: コアからUI/分析への唯一の出力経路。
- `BattleEventRecorder`: イベント/入力の記録を担う。
- `BattleEventReplayer`: 入力ログから戦闘を再生する。
- `TargetingPolicyRegistry`: ターゲティング差し替え点の登録先。
- `IBattleRandom`: 戦闘内の乱数源。注入で差し替える。
- `IBattleLogger`: 戦闘内ログの出力口。注入で差し替える。
- `BattleUIBridge`: UI操作の実装（IBattleUiAdapter）。
- `BattleUiEventAdapter`: BattleEvent → UI表示の変換。
- `BattleManager`: 組み立て・依存注入・ライフサイクル管理の中心。
- `BattleInitializer/UnityBattleRunner`: 歩行系からの起動/終了の接続点。

## 命名ルール（簡易）
- `*Manager`: ライフサイクル/集約の責務がある場合のみ。
- `*Service`: 状態を持たないロジック/計算。
- `*Adapter`: 境界層（Core ↔ UI/Integration）の変換。
- `*Context`: 状態の束ね役（参照のみを渡す）。
- `*Registry`: 追加可能なルールの一覧/辞書。
- `*Policy`: 差し替え可能な判断ロジック。
- `*Executor`: 単一ステップの実行担当。
- `*Flow`: 複数ステップの進行制御。
- `*Session`: 戦闘1回の操作単位。
- `*Orchestrator`: UI/演出の調停役。
- `*Runner`: 外部システムからの起動窓口。
- `*Provider`: データ取得/問い合わせ専用。

## 境界の原則
- CoreRuntime は UI 型に依存しない
- UI は BattleEvent から表示を構成する
- Integration が Core と UI を接続する
