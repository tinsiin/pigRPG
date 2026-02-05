# Extension Hooks (Phase 4a)

## 目的
- 追加仕様をどこに入れるべきかを明確にする
- 既存仕様を壊さず拡張できるフックを整理する
- UI/歩行/AI/ルールの追加点を分離して見通しを良くする

## フック一覧（優先順）
1. Targeting（対象選択）
2. Effects（派生効果/追加効果）
3. AI（意思決定）
4. Session/Flow（進行ルート）
5. UI/Presetation（表示専用）

---

## 1) Targeting フック
**目的**: 対象選択の追加ルール（例: 例外的な優先度、特殊条件時の自動選択）

### 入口
- `Assets/Script/Battle/CoreRuntime/Services/TargetingPolicyRegistry.cs`
- `TargetingPolicyRegistry.Register(ITargetingPolicy)`

### 追加方法
- `ITargetingPolicy` を実装して登録
- `TrySelect(TargetingPolicyContext, out List<BaseStates>)` を返すだけで差し替え可能

### 主な責務
- `TargetingPolicyContext` から `SelectGroup/OurGroup` を参照
- 必要なら `RangeWill` / `TargetingPlan` を利用

### 例
- 単体先約・特殊コンビ時の強制ターゲット
- リアクション系の「直前に殴った相手を優先」など

---

## 2) Effects フック
**目的**: スキル適用後の派生処理（追撃/反撃/連携/補助効果の追加）

### 入口
- `Assets/Script/Battle/CoreRuntime/Services/EffectResolver.cs`
- `Assets/Script/Battle/Effects/SkillEffectChain.cs`
- `Assets/Script/Battle/Effects/SkillEffectPipeline.cs`
- `Assets/Script/Battle/Effects/ISkillComboRule.cs`

### 追加方法
- `ISkillEffect` 実装を追加し、`SkillEffectChain` に登録
- 優先度（Priority）で順序制御
- `ISkillComboRule` を追加して「コンビ仕様」専用の派生処理を差し込める

### 主な責務
- `SkillEffectContext` から情報取得
- `ShouldApply` で条件判定 → `Apply` で実行

### 例
- コンビ仕様: 連携スキルの発火
- 追加の状態異常/追撃/防御反撃

---

## 3) AI フック
**目的**: 敵や自動行動の意思決定ルール追加

### 入口
- `Assets/Script/BattleAIBrains/BattleAIBrain.cs`

### 追加方法
- `Plan(AIDecision decision)` をオーバーライド
- `PostBattlePlan(BaseStates self, PostBattleDecision decision)` で戦闘後行動

### 主な責務
- 結果の「記述」に集中（Commit は共通）
- `decision` に Skill/Range/Target を詰める

### 例
- 特定スキル優先、状態依存行動、同時援護など

---

## 4) Session / Flow フック
**目的**: 進行ルートや入力ルールの追加

### 入口
- `Assets/Script/Battle/Core/BattleSession.cs`
- `Assets/Script/Battle/CoreRuntime/BattleFlow.cs`

### 追加方法
- `BattleSession.ApplyInputAsync` の入力型拡張
- `BattleFlow` に「新規分岐」追加（既存フローを崩さない範囲）

### 例
- 特殊ラウンド（演出のみ/即時終了）
- 連携ルールの専用入力

---

## 5) UI / Presentation フック（表示のみ）
**目的**: 新UIや演出追加（ロジックに触れない）

### 入口
- `Assets/Script/Battle/UI/BattleUiEventAdapter.cs`
- `Assets/Script/Battle/UI/BattleUIBridge.cs`

### 追加方法
- `BattleEventType` に `Ui*` を追加
- Adapter で変換し、UI 側で処理

### 例
- アイコンズーム、専用演出、ログ拡張

---

## 追加時の共通ルール
- CoreRuntime は UI 型に依存しない
- UI は `BattleEvent` 経由で操作する
- 新仕様は「Targeting → Effects → AI → Session」の順で検討する
- 既存挙動に影響が出る場合は `doc/battle/CombatScenarios.md` に追記する
