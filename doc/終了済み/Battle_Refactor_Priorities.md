# Battle Refactor Priorities

## 目的
戦闘周辺の複雑性・副作用・結合度を下げ、テスト可能性と変更容易性を上げる。

## 優先度高（P0/P1）
### P0: BattleManager に責務が集中
- 現状: 戦闘進行、UI、グローバル状態の管理が単一クラスに混在。
- 根拠: 初期化/DI、UI通知、戦闘終了処理、メッセージ生成などが同居。
  - `Assets/Script/BattleManager.cs:325`
  - `Assets/Script/BattleManager.cs:522`
  - `Assets/Script/BattleManager.cs:563`
  - `Assets/Script/BattleManager.cs:714`
- 影響: テスト不能、変更時の影響範囲が巨大。

### P0: グローバル状態 (BattleContextHub / BattleUIBridge.Active)
- 現状: 戦闘コンテキストを静的に保持。
- 根拠:
  - `Assets/Script/Battle/Core/BattleContextHub.cs:1`
  - `Assets/Script/Battle/UI/BattleUIBridge.cs:6`
- 影響: 複数バトル同時実行やテスト隔離が難しい。

### P1: UnderActersEntryList が戦闘状態に依存
- 現状: 内部で `Acter.NowUseSkill` 依存の分散ロジックを保持。
- 根拠: `Assets/Script/BattleManager.cs:34`
- 影響: ターゲティング/分散計算の責務が UI/Manager 側に漏れている。

### P1: TargetingService の巨大分岐
- 現状: 選択ロジックが1メソッドに集中。
- 根拠: `Assets/Script/Battle/Services/TargetingService.cs:12`
- 影響: SkillZoneTrait 追加時の保守性低下。

### P1: TurnExecutor / SkillExecutor が BattleManager 直依存
- 現状: BattleManager の内部状態へ直接アクセスする構造。
- 根拠:
  - `Assets/Script/Battle/TurnExecutor.cs:12`
  - `Assets/Script/Battle/SkillExecutor.cs:29`
- 影響: テスト不能、置換困難。

### P1: EffectResolver が派生効果を抱え過ぎ
- 現状: 攻撃実行と派生効果発火が同居。
- 根拠: `Assets/Script/Battle/Services/EffectResolver.cs:14`
- 影響: ルール追加で肥大化。

### P2: ActionQueue が低レベル生成のみ
- 現状: ActionEntry の不正値を防げない。
- 根拠: `Assets/Script/Battle/Core/ActionQueue.cs:18`
- 影響: 予約行動の整合性が壊れやすい。

---

## BattleManager 分割の具体案（案）

### 1. BattleFlow（戦闘進行専用）
- 役割: ターン進行、勝敗判定、ステート遷移。
- 移管候補:
  - `ACTPop`/`NextTurn`/`TriggerACT`/`SkillACT` のコントロールフロー
  - `BattleStateManager` のラップ

### 2. BattleActionContext（戦闘状態保持）
- 役割: 現在アクター、ターゲット、行動キュー、戦闘カウントの保持。
- 移管候補:
  - `Acter` / `unders` / `Acts` / `BattleTurnCount`
  - `ActerFaction` / `Wipeout` / `EnemyGroupEmpty` / `AlliesRunOut`

### 3. BattlePresentation（UI/メッセージ専用）
- 役割: UI 表示・ログ・演出。
- 移管候補:
  - `CreateBattleMessage` / `SetUniqueTopMessage` / `uiBridge` 操作
  - `OnBattleStart` / `OnBattleEnd` の UI 部分

### 4. BattleRules（ターゲティング・効果系）
- 役割: 戦闘ロジック単体。
- 移管候補:
  - `TargetingService` / `EffectResolver` / `UnderActersEntryList`

### 5. BattleManager をファサード化
- 役割: 既存 API の入口を維持しつつ、内部を委譲。
- 例:
  - `BattleManager.SkillACT()` -> `BattleFlow.ExecuteSkill()`
  - `BattleManager.ACTPop()` -> `BattleFlow.SelectNextActor()`

---

## 想定される分割順序（安全性重視）
1) **UI/ログ** を `BattlePresentation` に切り出す（副作用を隔離）
2) **状態保持** を `BattleActionContext` に集約（依存を集める）
3) **ターン進行** を `BattleFlow` に移管
4) **Targeting/Effect** を `BattleRules` に整理
5) BattleManager を薄く保つ

---

## 影響が大きい呼び出し元（確認候補）
- `Assets/Script/Battle/UI/BattleOrchestrator.cs`
- `Assets/Script/Players/Runtime/AllyClass.cs`
- `Assets/Script/WatchUIUpdate.cs`

---

## BattleManager 分割の具体案（詳細）

### 分割後の構成（提案）
- `BattleManager` (Facade)
  - 既存 API を維持しつつ内部に委譲
  - 直接ロジックを持たない
- `BattleActionContext`
  - 戦闘中の状態を集約し、他のサービスへ渡す
- `BattleFlow`
  - ターン進行・勝敗遷移・戦闘終了フロー
- `BattlePresentation`
  - UI / Log / Message の集約
- `BattleRules`
  - Targeting / Effect / 分散ロジックを束ねる

### BattleActionContext に移す候補
- 状態
  - `Acter`, `unders`, `Acts`, `BattleTurnCount`
  - `ActerFaction`, `Wipeout`, `EnemyGroupEmpty`, `AlliesRunOut`
  - `DoNothing`, `PassiveCancel`, `SkillStock`, `VoidTurn`
- 依存
  - `BattleGroup` / `BattleStateManager` / `TurnScheduler` / `TargetingService` / `EffectResolver`

### BattleFlow に移す候補
- `ACTPop` (or `SelectNextActor`)
- `NextTurn`
- `TriggerACT`
- `SkillACT`
- `DialogEndACT`（戦闘終了の判定部分）
- `EscapeACT` / `DominoEscapeACT`（EscapeHandler 内部呼び出しは残す）

### BattlePresentation に移す候補
- `CreateBattleMessage`
- `SetUniqueTopMessage` / `AppendUniqueTopMessage`
- `OnBattleStart` / `OnBattleEnd` の UI 部分
- `uiBridge` の操作 (`DisplayLogs`, `SetSelectedActor`, `MoveActionMark...` など)

### BattleRules に移す候補
- `UnderActersEntryList` の分散計算
- `TargetingService`
- `EffectResolver`

### 依存方向（目安）
1. `BattleManager` -> `BattleFlow` / `BattlePresentation` / `BattleActionContext`
2. `BattleFlow` -> `BattleActionContext` / `BattleRules`
3. `BattlePresentation` -> `BattleActionContext`
4. `BattleRules` -> `BattleActionContext` (読み取りのみ)

### 移行ステップ（安全順）
1) `BattlePresentation` の抽出（UI/メッセージ副作用を隔離）  
2) 状態を `BattleActionContext` に寄せ、`BattleManager` から直接参照を削減  
3) `BattleFlow` を導入し、`ACTPop`/`NextTurn`/`TriggerACT` を委譲  
4) `BattleRules` の導入（`TargetingService`/`EffectResolver`/分散計算）  
5) `BattleManager` をファサード化して終端  

---

## 計画拡充: (2) BattleActionContext 設計案

### 目的
- 戦闘中の「状態」と「依存」を1か所に集約し、他クラスからの直接参照を減らす。
- 状態変更の入口を限定し、テスト時に差し替えしやすくする。

### 役割（スコープ）
- **保持のみ**: 状態と依存の保持が中心。複雑なロジックは持たない。
- **最低限の整合性**: セッターで軽いバリデーションや同期（例: Faction 変更時の一貫性）だけ。

### 想定API（草案）
```csharp
public sealed class BattleActionContext
{
    // Core groups/state
    public BattleGroup AllyGroup { get; }
    public BattleGroup EnemyGroup { get; }
    public BattleStateManager StateManager { get; }
    public TurnScheduler TurnScheduler { get; }

    // Action/turn state
    public ActionQueue Acts { get; }
    public BaseStates Acter { get; set; }
    public UnderActersEntryList Targets { get; set; }
    public allyOrEnemy ActerFaction { get; set; }
    public int BattleTurnCount { get; set; }

    // Flags
    public bool DoNothing { get; set; }
    public bool PassiveCancel { get; set; }
    public bool SkillStock { get; set; }
    public bool VoidTurn { get; set; }

    // Services
    public TargetingService Targeting { get; }
    public EffectResolver Effects { get; }
}
```

### 実装上の注意点
- `UnderActersEntryList` は **Targets/TargetList** に名称統一し、役割を明確化。
- `ActerFaction` と `Acter` が不整合にならないよう簡易チェックを追加。
- `BattleTurnCount` は `StateManager` と同期させる（片方を正とする）。

### 移行ステップ（ActionContext）
1) `BattleManager` 内で `BattleActionContext` を生成  
2) `BattleManager` の参照を `context.*` に置換（段階的）  
3) `TurnExecutor`/`SkillExecutor` の引数に `BattleActionContext` を渡す  
4) `BattleManager` の public API は `context` への委譲に縮小  

---

## 計画拡充: (3) BattleFlow の API/責務案

### 目的
- 戦闘進行の「入口」を一本化し、副作用と分岐を集中管理する。

### 想定API（草案）
```csharp
public sealed class BattleFlow
{
    public TabState SelectNextActor();     // 現在の ACTPop 相当
    public void NextTurn(bool advance);    // ターン更新
    public UniTask<TabState> ExecuteSkill(); // SkillACT 相当
    public TabState TriggerAct(int count);
    public TabState EscapeAct();
    public TabState DominoEscapeAct();
    public TabState DialogEndAct();
}
```

### 内部で呼ぶ依存
- `BattleActionContext`（状態保持）
- `BattleRules`（Targeting/Effect）
- `BattlePresentation`（ログ/メッセージ/UI）

### 責務の境界
- **BattleFlow**: 進行制御と状態遷移のみ  
- **BattleRules**: ロジック計算（ターゲット、効果、分散）  
- **BattlePresentation**: UI/ログ/メッセージ  

### 移行ステップ（Flow）
1) `ACTPop` を `BattleFlow.SelectNextActor()` に移し、`BattleManager.ACTPop()` は委譲  
2) `NextTurn` を `BattleFlow.NextTurn()` に移す  
3) `SkillACT` を `BattleFlow.ExecuteSkill()` に移す  
4) `DialogEndACT` / `EscapeACT` / `DominoEscapeACT` を移す  
5) `BattleManager` の public API は Flow 経由に統一  

### 受け入れテスト（最低限）
- ターンが進む（行動予約/ランダム選出/スキップ）  
- Skill 実行後にターン処理が正しく進む  
- `Wipeout` / `AlliesRunOut` / `EnemyGroupEmpty` で遷移できる  
