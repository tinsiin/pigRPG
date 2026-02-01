# エンカウント・敵管理・再遭遇システム リファクタリング計画

## 概要

BattleManager周りのエンカウント、敵管理、再遭遇（Reencount）システムの分析結果と改善提案をまとめたドキュメント。

---

## 0. 目的・ゴール・範囲

### 0.1 目的
- 戦闘〜再遭遇の一連フローを「変更しやすく・テストしやすく・バグを埋め込みにくい」構造にする
- 敵復活（Reencount/Recovely）ロジックの集約で不整合を減らす
- BattleManagerの責務を整理し、将来の機能追加（AI/演出/難易度）に耐える土台を作る

### 0.2 成功基準（定量/定性）
- 再遭遇関連のロジックが **1箇所の統制クラス**（EnemyRebornManager）に集約される
- BattleManagerが **概ね 40〜60% まで縮小** し、責務が明示される
- 主要フロー（遭遇→戦闘→復活準備→再遭遇）の **手動テストが100%通過**
- 既存セーブデータ/戦闘演出/成長挙動が **既存仕様と同等**（差分は意図的に記録）

### 0.3 範囲（In Scope）
- EnemyRebornManagerの新設と復活状態遷移の明示化
- ActionQueueのAPI改善（TryPeek/ActionEntry公開）
- BattleManagerからの責務分割（Escape/Skill/Turn/State）
- 成長ロジックのStrategy化（既存の4系統を統合）
- EncounterEnemySelectorの分割（読みやすさ/テスト容易性）

### 0.4 非目標（Out of Scope）
- UIの全面刷新や演出表現の変更
- 戦闘バランスの全面調整（成長率の調整は可能だが意図的変更のみ）
- 歩行システム全体の刷新
- Save/Load形式の大規模な変更

### 0.5 前提/制約
- `*.meta` / `*.unity` / `*.prefab` は編集しない
- MonoBehaviourのクラス名・ファイル名整合は維持
- 既存のグローバルHubは互換性維持（段階的に縮退）

---

## 1. 現行アーキテクチャ

### 1.1 エンカウント〜バトル〜再エンカウントの流れ

```
WalkingSystemManager (歩行管理)
    ↓ エンカウント判定
EncounterResolver (確率判定)
    ↓ 敵選択
EncounterEnemySelector (敵グループ構築)
    ↓ バトル初期化
BattleInitializer (BattleManager, BattleOrchestrator生成)
    ↓ バトル実行
BattleManager (ターン管理, 敵管理)
    ↓ バトル終了時
BattleGroup.RecovelyStart() (敵復活準備)
    ↓ 再遭遇時
EncounterEnemySelector (復活敵の再選別)
```

### 1.2 関連ファイル一覧

| ファイル | 行数 | 主な責務 |
|---------|------|---------|
| `BattleManager.cs` | 1408 | ターン管理、行動キュー、スキル実行、逃走管理、UI制御 |
| `NormalEnemy.cs` | ~450 | 敵データ、復活ロジック、成長システム |
| `BattleGroup.cs` | ~500 | 敵/味方グループ管理、復活準備 |
| `EncounterEnemySelector.cs` | 217 | 敵グループ選別、復活敵フィルタリング |
| `ActionQueue.cs` | ~300 | 行動順序管理 |
| `WalkingSystemManager.cs` | 534 | 歩行管理、エンカウントトリガー |

---

## 2. 問題点一覧

### 2.1 BattleManager の責務過多（優先度: 高）

**場所:** `Assets/Script/BattleManager.cs`

BattleManagerが1408行で以下の多すぎる責務を持っている:

| 責務 | 関連メソッド | 行番号 |
|------|-----------|--------|
| ターン管理 | `ACTPop()`, `NextTurn()` | 484-1336 |
| 行動キュー管理 | `CharacterAddFromListOrRandom()` | 417-480 |
| 範囲・対象選択 | `SelectTargets()` | 1078-1094 |
| スキル実行 | `SkillACT()` | 1065-1158 |
| 逃走管理 | `EscapeACT()`, `DominoEscapeACT()` | 797-864 |
| 敵グループ管理 | `EnemyGroup.EscapeAndRemove()` 等 | 504-508, 835-856 |
| UI制御 | `uiBridge` 経由での大量のUI操作 | 全体 |
| 状態管理 | `BattleState`, `VoidTurn`, `Wipeout` | 240-295 |

**問題:**
- 変更の影響範囲が広い
- テストが困難
- 責務が不明瞭

**改善案:**
- `TurnExecutor` - ターン実行ロジック
- `EscapeHandler` - 逃走処理
- `SkillExecutor` - スキル実行
- `BattleStateManager` - 状態管理

---

### 2.2 敵復活（Reencount）ロジックの分散（優先度: 高）

敵の復活に関するロジックが4ファイルに分散している:

| ファイル | 処理内容 | メソッド | 行番号 |
|---------|---------|---------|--------|
| NormalEnemy.cs | 復活可否判定 | `CanRebornWhatHeWill()` | 76-96 |
| NormalEnemy.cs | 復活準備 | `ReadyRecovelyStep()` | 67-71 |
| NormalEnemy.cs | 復活時コールバック | `OnReborn()` | 363-372 |
| NormalEnemy.cs | 再遭遇時処理 | `ReEncountCallback()` | 378-430 |
| BattleGroup.cs | 敵グループの復活準備 | `RecovelyStart()` | 449-462 |
| EncounterEnemySelector.cs | 復活敵の選別 | `SelectGroup()` | 32-46 |
| BattleManager.cs | バトル終了時の処理 | `OnBattleEnd()` | 1341-1385 |

#### 潜在的バグ

`BattleGroup.cs:449-462` の `RecovelyStart()` に未使用のLINQ結果がある:

```csharp
public void RecovelyStart(int globalSteps)
{
    List<NormalEnemy> enes = Ours.OfType<NormalEnemy>().ToList();
    // ...
    enes.Where(enemy => enemy.Death() && enemy.Reborn && !enemy.broken).ToList();
    // ↑ この結果が使用されていない（バグの可能性）

    foreach(var ene in enes)
    {
        ene.ReadyRecovelyStep(globalSteps);
    }
}
```

#### 復活カウンター管理の複雑さ

`NormalEnemy.cs` に2つの異なるカウンターが存在:
- `_recovelyStepCount` - バトル内での復活カウント
- `_lastEncountProgressForReborn` - 最後のエンカウント歩数

```csharp
// NormalEnemy.cs:54-100
private int _recovelyStepCount = -1;
private int _lastEncountProgressForReborn = -1;

public bool CanRebornWhatHeWill(int globalSteps)
{
    var distanceTraveled = Math.Abs(globalSteps - _lastEncountProgressForReborn);
    if((_recovelyStepCount -= distanceTraveled) <= 0)
    {
        _recovelyStepCount = 0;
        OnReborn();
        return true;
    }
    // ...
}
```

**問題:**
- 状態遷移が不明瞭（復活前→復活中→復活後）
- 復活ロジックと再遭遇ロジックが混在

**改善案:**
- `EnemyRebornManager` クラスを新設し、復活ロジックを集約
- 状態遷移を明示的に管理（State パターン）

---

### 2.3 ActionQueue の設計問題（優先度: 中）

**場所:** `Assets/Script/Battle/ActionQueue.cs`

個別Getterメソッドが乱立している:

```csharp
public BaseStates GetAtCharacter(int index) { ... }
public allyOrEnemy GetAtFaction(int index) { ... }
public List<ModifierPart> GetAtModifyList(int index) { ... }
public bool GetAtIsFreezeBool(int index) { ... }
public BaseStates GetAtSingleTarget(int index) { ... }
public float GetAtExCounterDEFATK(int index) { ... }
public List<BaseStates> GetAtRaterTargets(int index) { ... }
public float GetAtRaterDamage(int index) { ... }
```

BattleManagerでの使用例 (`BattleManager.cs:420-434`):

```csharp
if (Acts.Count > 0)
{
    UniqueTopMessage = Acts.GetAtTopMessage(0);
    Acter = Acts.GetAtCharacter(0);
    ActerFaction = Acts.GetAtFaction(0);

    List<ModifierPart> modList;
    if ((modList = Acts.GetAtModifyList(0)) != null)
    {
        // ...
    }
    // 以下多数のGetter呼び出し...
}
```

**問題:**
- データアクセスが多数の個別メソッドに分散
- `ActionEntry` が非公開でカプセル化されているが、アクセスが煩雑

**改善案:**
- `ActionEntry` を公開して直接アクセス可能にする
- または `TryPeekEntry(out ActionEntry entry)` パターンを導入

---

### 2.4 UnderActersEntryList の設計問題（優先度: 中）

**場所:** `BattleManager.cs:38-179`

```csharp
public class UnderActersEntryList
{
    BattleManager Manager;  // 親への強い依存
    // CharaAdd() メソッドで Manager.Acter.NowUseSkill にアクセス
    // CashSpread キャッシュで複雑なロジック
}
```

**問題:**
- BattleManager に直接依存している
- スキルの分散値計算ロジックが複雑
- CashSpread キャッシュの状態管理がしにくい

**改善案:**
- 依存性注入でBattleManagerとの結合を緩める
- キャッシュロジックを別クラスに分離

---

### 2.5 敵成長ロジックの複雑さ（優先度: 中）

**場所:** `NormalEnemy.cs:189-312`

4種類の成長メカニズムが存在:

| メソッド | トリガー | 成長率 |
|---------|---------|--------|
| `GrowSkillsNotEnabledOnWin()` | 勝利時 | 0.88f |
| `GrowSkillsNotEnabledOnRunOut()` | 敵逃走時 | 0.33f |
| `GrowSkillsNotEnabledOnAllyRunOut()` | 味方逃走時 | 0.66f |
| `GrowSkillsNotEnabledOnReEncount()` | 再遭遇時 | 可変 |

```csharp
// 共通ロジック: GetGrowSkillsSortedByDistance() (164-183行)
private IEnumerable<BaseSkill> GetGrowSkillsSortedByDistance()
{
    // ...
}
```

**問題:**
- 成長率がハードコードされている
- 類似ロジックが4メソッドに重複

**改善案:**
- Strategy パターンで `IGrowthStrategy` インターフェースを導入
- 成長率を設定可能にする

---

### 2.6 EncounterEnemySelector の複雑さ（優先度: 中）

**場所:** `Assets/Script/Walk/EncounterEnemySelector.cs`

```csharp
public static BattleGroup SelectGroup(
    IReadOnlyList<NormalEnemy> enemies,
    int globalSteps,
    int number = -1,
    IEnemyMatchCalculator matchCalc = null)
{
    // 1) 有効な敵の選別 (32-46行)
    // 2) ReEncountCallback実行 (48-52行)
    // 3) リーダー敵の選別 (59-63行)
    // 4) 複雑なマッチング・ループ (81-163行)
}
```

**問題:**
- 複雑な条件分岐が多い（81-163行のループ構造）
- 初期化と再遭遇処理が分離されていない

**改善案:**
- 選別ロジックを小さなメソッドに分割
- 初期化処理を明確に分離

---

### 2.7 グローバルハブへの依存（優先度: 低）

複数のグローバルハブが散在:

```csharp
BattleContextHub.Set(this);           // BattleManager.cs:334
BattleUIBridge.SetActive(uiBridge);   // BattleManager.cs:332
BattleOrchestratorHub.Set(...);       // BattleInitializer.cs:92
UIStateHub.EyeState                   // BattleManager.cs:1375
GameContextHub.Set(gameContext);      // WalkingSystemManager.cs:136
```

**問題:**
- グローバル状態への依存が多い
- 初期化順序が重要だが、明示されていない
- テストが困難

---

### 2.8 重複コード

#### 敵の逃走処理

`BattleManager.cs:835-857`:
```csharp
// 単体逃走
EnemyGroup.EscapeAndRemove(VoluntaryRunOutEnemy);

// ドミノ逃走
foreach(var enemy in DominoRunOutEnemies)
{
    EnemyGroup.EscapeAndRemove(enemy);
}
```

#### 敵初期化の重複

`NormalEnemy.cs:127-145` と `EncounterEnemySelector.cs:168-188` で初期化ロジックが分散。

---

## 3. 設計パターンの問題

### 3.1 Facade パターンの過度な使用

`BattleUIBridge` が Facade として機能しているが、メソッドが多すぎる:

```csharp
// BattleManager.cs での使用例
uiBridge.DisplayLogs();
uiBridge.MoveActionMarkToActorScaled();
uiBridge.NextArrow();
uiBridge.SwitchAllySkillUiState();
// ... その他多数
```

### 3.2 Strategy パターンの欠落

- ターン選別戦略が分散
- スキル成長戦略が複数メソッドに分散

### 3.3 State パターンの欠落

`BattleState` 構造体があるが、状態遷移が明確でない:

```csharp
private readonly BattleState battleState = new BattleState();

// 各プロパティで分散管理
bool Wipeout { get => battleState.Wipeout; ... }
bool EnemyGroupEmpty { get => battleState.EnemyGroupEmpty; ... }
```

---

## 4. リファクタリング優先度

| 優先度 | 項目 | 効果 | 難易度 | 推定影響範囲 |
|--------|------|------|--------|-------------|
| **高** | BattleManager を複数クラスに分割 | 保守性大幅向上 | 高 | 広い |
| **高** | 敵復活ロジックの集約 | バグ減少、可読性向上 | 中 | 中程度 |
| **中** | ActionQueue の設計改善 | 可読性向上 | 中 | 限定的 |
| **中** | UnderActersEntryList の独立化 | テスト可能性向上 | 中 | 限定的 |
| **中** | 敵成長ロジックの統合 | 重複削減 | 低 | 限定的 |
| **中** | EncounterEnemySelector の分割 | 可読性向上 | 中 | 限定的 |
| **低** | グローバルハブの整理 | 依存性削減 | 高 | 広い |

---

## 5. 推奨リファクタリング手順

### Phase 1: 敵復活ロジックの集約（推奨開始点）

1. `EnemyRebornManager` クラスを新設
2. `NormalEnemy` から復活関連メソッドを移動
3. `BattleGroup.RecovelyStart()` のバグ修正
4. 状態遷移の明示化

### Phase 2: ActionQueue の改善

1. `ActionEntry` を公開または `TryPeek` パターン導入
2. BattleManager の Getter 呼び出しを整理

### Phase 3: BattleManager の責務分割

1. `EscapeHandler` を分離
2. `SkillExecutor` を分離
3. `TurnExecutor` を分離
4. 段階的にテストを追加

### Phase 4: 成長ロジックの統合

1. `IGrowthStrategy` インターフェース定義
2. 各成長タイプを Strategy として実装
3. 成長率を設定可能にする

---

## 6. 注意事項

- `*.meta` ファイルは編集禁止
- `*.unity`, `*.prefab` ファイルも編集禁止
- MonoBehaviour 継承クラスはファイル名とクラス名を一致させる
- 既存のグローバルハブパターンとの互換性を維持する

---

## 7. 関連ドキュメント

- `doc/終了済み/BattleManager_Split_Design.md` - BattleManager分割設計（過去の検討）
- `doc/BaseStates_Refactoring_Guide.md` - BaseStatesリファクタリングガイド
- `doc/歩行システム設計/ゼロトタイプ歩行システム設計書.md` - 歩行システム設計

---

## 8. 詳細設計案（追加）

### 8.1 EnemyRebornManager（新規）

**責務**
- 復活可否の判定、復活カウンタ更新、再遭遇時の復活確定を一本化
- 復活状態の遷移を明示化（State化）

**状態案（例）**
- `Idle`（通常/死亡していない）
- `Counting`（死亡後、歩数で復活待ち）
- `Ready`（復活条件達成済み、再遭遇可能）
- `Reborned`（再遭遇直後、1回だけイベントを発火）

**主なAPI案**
- `void OnBattleEnd(BattleGroup group, int globalSteps)`
- `void OnWalkStepAdvanced(int globalSteps)`
- `IReadOnlyList<NormalEnemy> FilterRebornCandidates(IReadOnlyList<NormalEnemy> all, int globalSteps)`
- `void OnReencountResolved(BattleGroup selectedGroup)`

**移行方針**
- NormalEnemyの `_recovelyStepCount` / `_lastEncountProgressForReborn` はEnemyRebornManager側へ移管
- `CanRebornWhatHeWill()` / `ReadyRecovelyStep()` / `ReEncountCallback()` は薄い委譲に変更

### 8.2 ActionQueue

**改善方針**
- `ActionEntry` を public にして一括アクセス
- 互換維持のため、旧Getterは段階的に deprecate（内部で `TryPeek` を呼ぶ）

**API案**
- `bool TryPeek(out ActionEntry entry)`
- `ActionEntry Peek()`（例外版）
- `void Enqueue(ActionEntry entry)`

### 8.3 BattleManager分割

**候補クラス**
- `TurnExecutor`（NextTurn/ACTPop などのターン進行）
- `SkillExecutor`（SkillACT/SelectTargets/範囲選択）
- `EscapeHandler`（EscapeACT/DominoEscapeACT/敵削除）
- `BattleStateManager`（BattleStateの遷移と不整合検知）

**依存関係**
- BattleManagerは「調停役」に限定し、各Executorを委譲で呼び出す
- UI操作は `BattleUIBridge` に集中、Executorは必要最小のUIAPIのみ参照

### 8.4 UnderActersEntryList

**改善方針**
- BattleManagerへの直接依存を解消
- 必要な情報のみ `IBattleContext` インターフェース経由で取得

**API例**
- `IBattleContext { BaseStates Acter { get; } BaseSkill NowUseSkill { get; } }`

### 8.5 敵成長ロジック（Strategy化）

**設計案**
- `IGrowthStrategy`（例: `float GrowthRate`, `bool ShouldApply(Context)`）
- 4種類の成長トリガーを `GrowthStrategyType` で統一
- 成長率は ScriptableObject に集約（データ編集・差分管理を容易にする）

### 8.6 EncounterEnemySelector

**分割案**
- `FilterEligibleEnemies()`（有効な敵抽出）
- `ApplyReencountCallbacks()`（再遭遇イベント）
- `SelectLeader()`（リーダー決定）
- `MatchGroup()`（マッチングループ）

### 8.7 クラス配置/命名（確定）

**命名ルール**
- クラス名は PascalCase、責務接尾辞は `Manager` / `Handler` / `Executor`
- MonoBehaviour継承クラスはファイル名とクラス名を一致
- 既存の名前空間/フォルダ構成は維持（必要最小限の新規フォルダのみ追加）

**配置先（新規/追加）**

| クラス | 配置ファイル | 備考 |
|-------|--------------|------|
| `EnemyRebornManager` | `Assets/Script/Battle/EnemyRebornManager.cs` | 復活状態遷移とカウンタ管理 |
| `EnemyRebornState` | `Assets/Script/Battle/EnemyRebornManager.cs` | enum。Manager内に同居 |
| `TurnExecutor` | `Assets/Script/Battle/TurnExecutor.cs` | ターン進行（NextTurn/ACTPop） |
| `SkillExecutor` | `Assets/Script/Battle/SkillExecutor.cs` | スキル実行/対象選択 |
| `EscapeHandler` | `Assets/Script/Battle/EscapeHandler.cs` | 逃走処理 |
| `BattleStateManager` | `Assets/Script/Battle/BattleStateManager.cs` | BattleStateの状態遷移 |
| `IBattleContext` | `Assets/Script/Battle/IBattleContext.cs` | UnderActersEntryListから参照 |
| `IGrowthStrategy` | `Assets/Script/Battle/Growth/IGrowthStrategy.cs` | 成長戦略インターフェース |
| `GrowthStrategyType` | `Assets/Script/Battle/Growth/GrowthStrategyType.cs` | enum |
| `GrowthSettings` | `Assets/Script/Battle/Growth/GrowthSettings.cs` | ScriptableObject（成長率/トリガー設定） |
| `EnemyGrowthContext` | `Assets/Script/Battle/Growth/EnemyGrowthContext.cs` | 成長計算コンテキスト |
| `EnemyGrowthUtils` | `Assets/Script/Battle/Growth/EnemyGrowthUtils.cs` | 成長用ユーティリティ |
| `WinGrowthStrategy` | `Assets/Script/Battle/Growth/WinGrowthStrategy.cs` | 勝利時成長 |
| `RunOutGrowthStrategy` | `Assets/Script/Battle/Growth/RunOutGrowthStrategy.cs` | 敵逃走時成長 |
| `AllyRunOutGrowthStrategy` | `Assets/Script/Battle/Growth/AllyRunOutGrowthStrategy.cs` | 味方逃走時成長 |
| `ReEncountGrowthStrategy` | `Assets/Script/Battle/Growth/ReEncountGrowthStrategy.cs` | 再遭遇時成長 |

---

## 9. 実装タスク（詳細）

### Phase 1: EnemyRebornManager
- 新規クラス作成（EnemyRebornManager.cs）
- NormalEnemyの復活カウンタを移行
- `BattleGroup.RecovelyStart()` のLINQ未使用バグ修正
- EncounterEnemySelectorの復活フィルタを差し替え

### Phase 1 実施状況（2026-02-01）
- `EnemyRebornManager` を新設し、復活カウンタを集約
- `NormalEnemy` の復活処理をマネージャに委譲
- `BattleGroup.RecovelyStart()` の未使用LINQバグを修正
- `EncounterEnemySelector` の復活判定をマネージャ経由に統一

### Phase 2: ActionQueue
- `ActionEntry` を public 化
- `TryPeek(out ActionEntry entry)` 追加
- BattleManagerのGetter呼び出しを整理

### Phase 2 実施状況（2026-02-01）
- `ActionQueue.TryPeek(out ActionEntry entry)` を追加
- BattleManagerの先約参照を `ActionEntry` 経由に整理
- BattleAIBrain / BattleOrchestrator も `TryPeek` 経由に統一

### Phase 3: BattleManager分割
- EscapeHandler抽出、既存メソッドを薄く委譲化
- SkillExecutor抽出（SelectTargets/SkillACTを移行）
- TurnExecutor抽出（NextTurn/ACTPopを移行）
- BattleStateManager導入（状態遷移を明文化）

### Phase 3 実施状況（2026-02-01）
- EscapeHandler を新設し、BattleManager の EscapeACT / DominoEscapeACT を委譲化
- SkillExecutor を新設し、BattleManager の SkillACT / RandomRange 処理を委譲化
- TurnExecutor を新設し、BattleManager の ACTPop / NextTurn を委譲化
- BattleStateManager を新設し、BattleState へのアクセスを集約
- BattleStateManager に状態遷移メソッド（Wipeout/RunOut/EnemyGroupEmpty 等）を追加
- CharacterActExecutor を新設し、CharacterActBranching を委譲化
- ActionSkipExecutor を新設し、SkillStock / PassiveCancel / DoNothing の処理を委譲化
- BattleStateManager に遷移ガード（矛盾検知ログ）を追加

### Phase 4: 成長ロジック統合
- `IGrowthStrategy` / `GrowthStrategyType` を追加
- 既存4メソッドを統合、成長率は設定化

### Phase 4 実施状況（2026-02-01）
- `EnemyGrowthContext` / `EnemyGrowthUtils` を追加し、成長計算の共通情報を集約
- `Win/RunOut/AllyRunOut/ReEncount` の成長処理を各 Strategy に移植
- `GrowthSettings` を追加し、未指定時は `Default` を使用
- `NormalEnemy` の成長トリガーを `ApplyGrowth` に統一

### Phase 5: EncounterEnemySelector分割
- メソッド分解、責務分離
- 主要ループに最小限のユニットテストを追加

### Phase 5 実施状況（2026-02-01）
- `EncounterEnemySelector.SelectGroup()` を分解し、フィルタ/再遭遇処理/リーダー選択/停止判定を分離
- Editorテストで主要分岐の最小ユニットテストを追加

---

## 10. テスト/検証計画

### 10.1 手動テスト（最低限）
- 1戦後に即再遭遇（復活カウント0想定）
- 復活待ち→歩数経過→再遭遇
- 逃走（単体/ドミノ）後の敵復活
- 味方/敵逃走時の成長反映

### 10.2 自動テスト（可能なら）
- EnemyRebornManagerの状態遷移テスト
- ActionQueue.TryPeek の取り回しテスト
- EncounterEnemySelectorのマッチング分解テスト

### 10.3 実施結果（2026-02-01）
- 10.1 手動テスト完了（全項目OK）
- EncounterEnemySelectorTests（EditMode）完了（OK）
- EnemyRebornManagerTests / ActionQueueTests（EditMode）完了（OK）

### 10.4 追加テスト実行結果（2026-02-02）
- BattleStateManagerTests（9テスト）: **全件OK**
- ActionQueueTests（9テスト）: **全件OK**
- EscapeHandlerTests（12テスト）: **全件OK**
- **合計30テスト全件パス**

---

## 11. リスクと対策

| リスク | 影響 | 対策 |
|-------|------|------|
| 復活状態の移管で挙動差分が出る | 再遭遇頻度の変化 | 旧ロジックと並行比較できるフラグを用意 |
| BattleManager分割でUI更新漏れ | 表示不整合 | 既存UI呼び出し点に最小限の統合テストを追加 |
| 成長率設定化でバランス崩れ | ゲーム体験劣化 | 旧値を初期値として固定し、変更は記録 |

---

## 12. ロールバック/移行方針

- `USE_NEW_REBORN_MANAGER` のフラグ（定数 or Config）で旧/新切替
- ActionQueueの旧Getterは当面維持し、段階的に除去
- BattleManagerは「委譲のみ」に戻せるよう、抽出クラスは薄く開始

---

## 13. Definition of Done（完了条件）

- Phase 1〜3の主要フローが手動テストで通過
- 再遭遇ロジックのソースが EnemyRebornManager に集約されている
- BattleManagerの責務分割が完了し、責務一覧がドキュメント化されている
- 既存仕様からの意図的差分が「変更履歴」に記録されている

---

## 14. 変更履歴（意図的差分）

### 14.1 差分一覧（確定）

| 日付 | 対象 | 既存仕様との差分 | 理由 | 影響/テスト |
|------|------|-------------------|------|-------------|
| 2026-02-01 | `BattleGroup.RecovelyStart()` | 復活準備対象を「死亡 & Reborn & !broken」の敵に限定（従来は全敵に `ReadyRecovelyStep()` が走っていた） | 未使用LINQ結果のバグ修正 | 手動テスト 10.1 |

### 14.2 変更履歴テンプレ（追記用）

| 日付 | 対象 | 既存仕様との差分 | 理由 | 影響/テスト |
|------|------|-------------------|------|-------------|
| 2026-02-01 | ActionQueue | 旧Getter群10メソッド削除（GetAtCharacter等） | TryPeek+ActionEntry方式に完全移行済みで不要 | 外部使用箇所0件を確認 |
| 2026-02-01 | UnderActersEntryList | BattleManager依存→IBattleContext依存に変更 | テスト容易性向上、結合度低減 | 既存動作に影響なし |
| 2026-02-01 | BattleManager | 逃走ロジック→EscapeHandler、状態マーク/ログ→Executor各所にインライン化 | 責務分離、BattleManager薄化（44.5%削減） | 既存動作に影響なし |

---

## 15. 任意の次タスク（今後やるなら）

- ~~ActionQueue の旧 Getter 群（`GetAtCharacter` 等）の段階的削除・置換~~ **完了（2026-02-01）**
- ~~UnderActersEntryList の依存整理（`IBattleContext` 経由に統一されているか再確認）~~ **完了（2026-02-01）**
- ~~BattleManager の委譲をさらに薄く（UI 操作/状態遷移の直接参照削減）~~ **完了（2026-02-01）**
- ~~追加テスト（BattleManager分割後の結合テスト、Escape/Skill/Turn の最小テスト）~~ **完了（2026-02-01）**

### 15.1 ActionQueue旧Getter削除 実施記録（2026-02-01）

**削除したメソッド:**
- `GetAtTopMessage(int index)`
- `GetAtCharacter(int index)`
- `GetAtFaction(int index)`
- `GetAtModifyList(int index)`
- `GetAtIsFreezeBool(int index)`
- `GetAtSingleTarget(int index)`
- `GetAtExCounterDEFATK(int index)`
- `GetAtRaterTargets(int index)`
- `GetAtRaterDamage(int index)`
- `GetAt(int index)` (private helper)

**結果:**
- ActionQueue.cs: 124行 → 67行（46%削減）
- 外部からの使用箇所: 0件（すでにTryPeek + ActionEntry方式に移行済み）
- 残存API: `Count`, `TryPeek`, `Add`, `RatherAdd`, `RemoveDeathCharacters`, `RemoveAt`

### 15.2 UnderActersEntryList依存整理 実施記録（2026-02-01）

**変更内容:**
- `BattleManager Manager` → `IBattleContext _context` に変更
- コンストラクタ引数を `BattleManager instance` → `IBattleContext context` に変更
- `Manager.Acter` → `_context.Acter` に置換（2箇所）
- `Manager.IsVanguard(chara)` → `_context.IsVanguard(chara)` に置換（4箇所）

**効果:**
- BattleManagerへの直接依存を解消
- テスト時にモックIBattleContextを注入可能に
- 既存の生成箇所（`new UnderActersEntryList(this)`）は変更不要（BattleManagerがIBattleContextを実装しているため）

### 15.3 BattleManager委譲の薄化 実施記録（2026-02-01）

**移動・削除した内容:**

1. **EscapeHandlerへの移動**
   - `GetRunOutRateByCharacterImpression()` - 逃走率計算（switch文をswitch式に簡略化）
   - `GetRunOutEnemies()` - 連鎖逃走敵取得
   - EscapeHandler内で`StateManager`経由に変更（`MarkAlliesRunOut`等）

2. **状態マーク系ラッパーメソッド削除**（6メソッド）
   - `MarkWipeoutForFaction()` - TurnExecutorでインライン化
   - `MarkEnemyGroupEmpty()` - TurnExecutorでインライン化
   - `MarkAlliesRunOut()` - EscapeHandlerでStateManager直接呼び出し
   - `SetVoluntaryRunOutEnemy()` - 同上
   - `AddDominoRunOutEnemy()` - 同上
   - `ClearDominoRunOutEnemies()` - 同上

3. **ログ系メソッド削除**（11メソッド）
   - TurnExecutor/CharacterActExecutorにインライン化
   - `LogActorFromReservation`, `LogActorFromRandom`, `LogRatherAct`
   - `LogAllyActing`, `LogAllyNoForcedSkill`, `LogAllySkillSelect`
   - `LogAllyDoNothing`, `LogAllyConsecutiveOperate`
   - `LogCharacterActBranchingStart`, `LogActerMissing`, `LogNowUseSkillMissing`
   - `AddDialogEndLog`

**結果:**
- BattleManager.cs: 1408行 → 781行（**44.5%削減**）
- 目標（40-60%削減）達成

### 15.4 追加テスト 実施記録（2026-02-01）

**作成したテストファイル:**

1. **BattleStateManagerTests.cs**（新規）
   - `MarkWipeout_SetsWipeoutToTrue`
   - `MarkEnemyGroupEmpty_SetsEnemyGroupEmptyToTrue`
   - `MarkAlliesRunOut_SetsAlliesRunOutToTrue`
   - `AddDominoRunOutEnemy_AddsEnemyToList`
   - `AddDominoRunOutEnemy_IgnoresNullEnemy`
   - `ClearDominoRunOutEnemies_ClearsList`
   - `TurnCount_CanBeSetAndRetrieved`
   - `SetVoluntaryRunOutEnemy_SetsEnemy`
   - `MultipleTerminalStates_AllCanBeSetIndependently`

2. **ActionQueueTests.cs**（拡張）
   - `Add_CreatesActTypeEntry`
   - `RatherAdd_CreatesRatherTypeEntry`
   - `RemoveAt_RemovesEntryAtIndex`
   - `RemoveAt_RemovesFirstEntry`
   - `RemoveDeathCharacters_RemovesEntriesWithDeadActors`
   - `RemoveDeathCharacters_KeepsEntriesWithNullActors`
   - `Count_ReturnsCorrectNumberOfEntries`

3. **EscapeHandlerTests.cs**（新規）
   - 全SpiritualPropertyに対する逃走率テスト（12テスト）
   - privateメソッドをリフレクション経由でテスト

**テスト総数:** 28テスト追加
