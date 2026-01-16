# PlayersProgress 廃止計画

**ステータス: クローズ（2026-01-16完了）**

## 概要

旧歩行システムの遺物である `PlayersProgressTracker` を廃止し、新歩行システムの `GameContext.Counters` に完全統一する。
ゴール: 旧歩行の `NowProgress` / `PlayersProgress` 依存を完全に排除し、歩行進捗・再遭遇/復活判定の基準を `GameContext.Counters` に一本化する。

## 背景

### 現状の問題

新歩行システム移行後も、2つの歩数カウンターが並存している：

| カウンター | 用途 | 状態 |
|---|---|---|
| `GameContext.Counters.GlobalSteps` | 新歩行システムの歩数 | 増える |
| `PlayersProgress.NowProgress` | 旧システムの歩数 | 増えない（0のまま） |

これにより：
- UI表示がズレる可能性がある
- セーブデータと実際の進行状態が一致しない
- コードの複雑性が増している

### 現在の暫定対応

`PlayersStatesBattleMetaProvider` に `nowProgressOverride` を追加し、新歩行システム経由の戦闘では `GameContext.GlobalSteps` を使うようにした。これは暫定対応であり、完全な解決ではない。

## PlayersProgressTracker の現在の使用箇所

| ファイル | 行 | 用途 | 対応方針 |
|---|---|---|---|
| Walking.cs | 357 | UI表示（歩数） | GameContext参照に変更 |
| PlayersSaveService.cs | 11, 148 | セーブ/ロード | セーブ構造変更 |
| BattleManager.cs | 1352 | 敵復活 | 暫定対応済み（オーバーライド） |
| PlayersStatesBattleMetaProvider.cs | 22 | BattleManagerへ渡す | 暫定対応済み |

## 保存対象データの設計

### GameContext が持つ状態

| データ | クラス | 保存 | 理由 |
|---|---|---|---|
| GlobalSteps | WalkCounters | ✅ 必須 | 敵復活計算の基準 |
| NodeSteps | WalkCounters | ❌ 不要 | ノード再開時にリセットで問題なし |
| TrackProgress | WalkCounters | ❌ 不要 | 同上 |
| CurrentNodeId | WalkState | ✅ 必須 | 再開位置の特定 |
| LastExitId | WalkState | ❌ 不要 | ノード再開時は出口から再開しない |
| Flags | GameContext | ✅ 必須 | イベントフラグ等 |
| Counters | GameContext | ✅ 必須 | 汎用カウンター |
| EncounterState | GameContext | ❌ 不要 | エンカウント状態はリセットで許容 |
| StageBonuses | GameContext | ❌ 不要 | ノード進入時に再適用される |

### ロード時のリセット許容事項

以下はロード時にリセットされるが、ゲーム進行に影響しない：

- `NodeSteps` / `TrackProgress`: ノード内の一時的な進行度
- `LastExitId`: 出口選択の履歴
- `EncounterState` (Cooldown/Grace/Pity): エンカウント確率調整（リセットで少し有利になる程度）
- `StageBonuses`: ノード進入イベントで再適用される
- `encounterEnemies` (敵のDeepCopy): 再生成される

※ TrackConfig / Gate を導入する場合は、`NodeSteps` / `TrackProgress` を保存対象に戻すことを検討する。

## シリアライズ方式の選定

### 問題: Unity の JsonUtility は Dictionary をサポートしない

```csharp
// これは保存されない
public Dictionary<string, bool> Flags;
```

### 対策: List + Wrapper 方式

```csharp
[Serializable]
public class WalkProgressData
{
    public int GlobalSteps;
    public string CurrentNodeId;
    public List<FlagEntry> Flags;      // Dictionary の代替
    public List<CounterEntry> Counters; // Dictionary の代替
}

[Serializable]
public class FlagEntry
{
    public string Key;
    public bool Value;
}

[Serializable]
public class CounterEntry
{
    public string Key;
    public int Value;
}
```

### 変換ヘルパー

```csharp
// GameContext → WalkProgressData
public static WalkProgressData ToSaveData(GameContext ctx)
{
    var data = new WalkProgressData
    {
        GlobalSteps = ctx.Counters.GlobalSteps,
        CurrentNodeId = ctx.WalkState.CurrentNodeId,
        Flags = new List<FlagEntry>(),
        Counters = new List<CounterEntry>()
    };
    // flags/counters は GameContext に取得メソッドを追加して変換
    return data;
}

// WalkProgressData → GameContext
public static void ApplyToContext(WalkProgressData data, GameContext ctx)
{
    // Counters.GlobalSteps のセッター追加が必要
    // Flags/Counters の復元
}
```

復元は「置換」を基本とし、既存の flags/counters はクリアしてから適用する。

## GameContext の取得経路

### 現状

```csharp
// WalkingSystemManager.cs
private GameContext gameContext;  // private で外部アクセス不可
```

### 対策案

**案A: WalkingSystemManager に公開プロパティを追加**

```csharp
public GameContext GameContext => gameContext;
```

`PlayersSaveService` から `WalkingSystemManager.Instance.GameContext` でアクセス。

**案B: GameContextHub（静的参照）を追加**

```csharp
public static class GameContextHub
{
    public static GameContext Current { get; set; }
}
```

`WalkingSystemManager` 初期化時に登録し、`PlayersSaveService` から参照。

**案C: PlayersContext に GameContext を含める**

`PlayersContext` が既に `PlayersSaveService` から参照されているので、そこに `GameContext` への参照を追加。

**推奨: 案B**
- 既存の `BattleContextHub` パターンと一貫性がある
- `WalkingSystemManager` への直接依存を避けられる
- ライフサイクル: `WalkingSystemManager` が有効化され rootGraph を持つ状態で `Current` をセットし、`OnDisable/OnDestroy` で `null` に戻す
- 複数の `WalkingSystemManager` が存在する場合は、アクティブなシーンで rootGraph を持つものを優先する

## 実装計画

### Phase 1: インフラ準備

**目的**: GameContext のセーブ/ロード基盤を整備

**タスク**:
1. `WalkProgressData` クラスを作成（List方式）
2. `FlagEntry` / `CounterEntry` ヘルパークラスを作成
3. `GameContext` に以下を追加:
   - `GetAllFlags()` / `GetAllCounters()` メソッド
   - `SetGlobalSteps(int)` メソッド（ロード用）
   - `RestoreFlags()` / `RestoreCounters()` メソッド（置換: 既存データをクリアして復元）
4. `GameContextHub` を作成
5. `WalkingSystemManager` 初期化時に `GameContextHub.Current` を設定
6. `WalkingSystemManager` の `OnDisable/OnDestroy` で `GameContextHub.Current = null` を実行

**影響範囲**:
- GameContext.cs
- 新規: WalkProgressData.cs
- 新規: GameContextHub.cs
- WalkingSystemManager.cs

### Phase 2: セーブ/ロード統合

**目的**: PlayersSaveService に GameContext 保存を追加

**タスク**:
1. `PlayersSaveData` に `WalkProgressData WalkProgress` フィールドを追加
2. `PlayersSaveService.Build()` で `GameContextHub.Current` から `WalkProgressData` を生成
3. `PlayersSaveService.Apply()` で `WalkProgressData` を `GameContext` に適用
4. `GameContextHub.Current` が null の場合は保存/復元をスキップ

**影響範囲**:
- PlayersSaveData.cs
- PlayersSaveService.cs

### Phase 3: UI表示の参照先変更

**目的**: Walking.cs の歩数表示を新システム参照に切り替える

**タスク**:
1. Walking.cs に `GameContextHub.Current` への参照を追加
2. 歩数表示を `GameContextHub.Current.Counters.GlobalSteps` から取得するように変更
3. 旧 `playersProgress.NowProgress` 参照を削除

**影響範囲**:
- Walking.cs

### Phase 4: 暫定対応の恒久化

**目的**: オーバーライド方式から直接参照方式に変更

**タスク**:
1. `WalkBattleMetaProvider` を新規作成（新歩行システム専用）
   ```csharp
   public sealed class WalkBattleMetaProvider : IBattleMetaProvider
   {
       public int NowProgress => GameContextHub.Current?.Counters.GlobalSteps ?? 0;
       // ... 他のメソッド
   }
   ```
2. 新歩行経由の戦闘起動は `WalkBattleMetaProvider` を使用
3. 旧システム用の `PlayersStatesBattleMetaProvider` は必要な範囲でのみ残す
4. `nowProgressOverride` は移行完了後に削除

### Phase 5: PlayersProgressTracker の廃止

**目的**: 旧システムのコードを完全に削除

**タスク**:
1. `IPlayersProgress` インターフェースを削除または縮小
2. `PlayersProgressTracker` クラスを削除
3. `PlayersContext` から `Progress` プロパティを削除
4. 関連する参照をすべて削除

**影響範囲**:
- PlayersProgressTracker.cs（削除）
- PlayersStatesInterfaces.cs
- PlayersContext.cs
- PlayersBootstrapper.cs

### Phase 6: 旧システム残存物の完全削除

**目的**: 互換性のために残していた旧システム関連コードを完全に削除

#### Phase 6A: 旧セーブデータ構造の削除

**タスク**:
1. `PlayersProgressSaveData` クラスを削除
2. `PlayersSaveData.Progress` フィールドを削除

**影響範囲**:
- PlayersSaveData.cs

**注意**: 旧セーブデータは読み込めなくなる（開発中なので問題なし）

#### Phase 6B: PlayersBootstrapper の Obsolete コード削除

**タスク**:
1. `NowStageID` プロパティを削除（Obsolete）
2. `NowAreaID` プロパティを削除（Obsolete）
3. `AddProgress` メソッドを削除（Obsolete）
4. `ProgressReset` メソッドを削除（Obsolete）
5. `SetArea` メソッドを削除（Obsolete）

**影響範囲**:
- PlayersBootstrapper.cs

#### Phase 6C: NowProgress → GlobalSteps 名前統一

**目的**: 旧システム由来の「NowProgress」という名前を新システムの「GlobalSteps」に統一

**変更対象ファイル（計11ファイル）**:

##### 1. IBattleMetaProvider.cs（インターフェース定義）
```csharp
// 変更前
int NowProgress { get; }
// 変更後
int GlobalSteps { get; }
```

##### 2. WalkBattleMetaProvider.cs:14（実装）
```csharp
// 変更前
public int NowProgress => GameContextHub.Current?.Counters?.GlobalSteps ?? 0;
// 変更後
public int GlobalSteps => GameContextHub.Current?.Counters?.GlobalSteps ?? 0;
```

##### 3. BattleOutcomeTracker.cs:12（ラッパー）
```csharp
// 変更前
public int NowProgress => inner != null ? inner.NowProgress : 0;
// 変更後
public int GlobalSteps => inner != null ? inner.GlobalSteps : 0;
```

##### 4. EncounterContext.cs:5,11（プロパティ）
```csharp
// 変更前
public int NowProgress { get; }
NowProgress = gameContext != null ? gameContext.Counters.GlobalSteps : 0;
// 変更後
public int GlobalSteps { get; }
GlobalSteps = gameContext != null ? gameContext.Counters.GlobalSteps : 0;
```

##### 5. BattleManager.cs:1352（呼び出し側）
```csharp
// 変更前
var progress = metaProvider != null ? metaProvider.NowProgress : 0;
// 変更後
var progress = metaProvider != null ? metaProvider.GlobalSteps : 0;
```

##### 6. UnityBattleRunner.cs:37（引数渡し）
```csharp
// 変更前
context.NowProgress,
// 変更後
context.GlobalSteps,
```

##### 7. BattleInitializer.cs:22,32（引数名）
```csharp
// 変更前（行22）
int nowProgress,
// 変更後
int globalSteps,

// 変更前（行32）
var enemyGroup = EncounterEnemySelector.SelectGroup(enemies, nowProgress, enemyNumber);
// 変更後
var enemyGroup = EncounterEnemySelector.SelectGroup(enemies, globalSteps, enemyNumber);
```

##### 8. EncounterEnemySelector.cs:9,28,37（引数名・使用箇所）
```csharp
// 変更前（行9）
public static BattleGroup SelectGroup(IReadOnlyList<NormalEnemy> enemies, int nowProgress, int number = -1)
// 変更後
public static BattleGroup SelectGroup(IReadOnlyList<NormalEnemy> enemies, int globalSteps, int number = -1)

// 変更前（行28）
if (ene.Reborn && ene.CanRebornWhatHeWill(nowProgress))
// 変更後
if (ene.Reborn && ene.CanRebornWhatHeWill(globalSteps))

// 変更前（行37）
ene.ReEncountCallback(nowProgress);
// 変更後
ene.ReEncountCallback(globalSteps);
```

##### 9. NormalEnemy.cs（引数名・変数名）
変更箇所:
- 行67: `ReadyRecovelyStep(int nowProgress)` → `ReadyRecovelyStep(int globalSteps)`
- 行69: `_lastEncountProgressForReborn = nowProgress;` → `_lastEncountProgressForReborn = globalSteps;`
- 行76: `CanRebornWhatHeWill(int nowProgress)` → `CanRebornWhatHeWill(int globalSteps)`
- 行84: `Math.Abs(nowProgress - _lastEncountProgressForReborn)` → `Math.Abs(globalSteps - _lastEncountProgressForReborn)`
- 行93: `_lastEncountProgressForReborn = nowProgress;` → `_lastEncountProgressForReborn = globalSteps;`
- 行378: `ReEncountCallback(int nowProgress)` → `ReEncountCallback(int globalSteps)`
- 行389: `Math.Abs(nowProgress - _lastEncounterProgress)` → `Math.Abs(globalSteps - _lastEncounterProgress)`
- 行420: `_lastEncounterProgress = nowProgress;` → `_lastEncounterProgress = globalSteps;`
- 行427: `ReadyRecovelyStep(nowProgress)` → `ReadyRecovelyStep(globalSteps)`

##### 10. BattleGroup.cs:449,459（引数名・使用箇所）
```csharp
// 変更前（行449）
public void RecovelyStart(int nowProgress)
// 変更後
public void RecovelyStart(int globalSteps)

// 変更前（行459）
ene.ReadyRecovelyStep(nowProgress);
// 変更後
ene.ReadyRecovelyStep(globalSteps);
```

##### 11. PlayersBootstrapper.cs:120（プロパティ名）
```csharp
// 変更前
public int NowProgress => GameContextHub.Current?.Counters?.GlobalSteps ?? 0;
// 変更後
public int GlobalSteps => GameContextHub.Current?.Counters?.GlobalSteps ?? 0;
```

**除外対象（Archive フォルダ）**:
- `Archive/Stages.cs` - 旧システムのアーカイブなので変更不要

**内部フィールド（変更不要）**:
- `NormalEnemy._lastEncountProgressForReborn` - 内部実装用、外部公開されていない
- `NormalEnemy._lastEncounterProgress` - 同上

**影響範囲サマリ**:
| ファイル | 変更箇所数 |
|----------|-----------|
| IBattleMetaProvider.cs | 1 |
| WalkBattleMetaProvider.cs | 1 |
| BattleOutcomeTracker.cs | 1 |
| EncounterContext.cs | 2 |
| BattleManager.cs | 1 |
| UnityBattleRunner.cs | 1 |
| BattleInitializer.cs | 2 |
| EncounterEnemySelector.cs | 3 |
| NormalEnemy.cs | 9 |
| BattleGroup.cs | 2 |
| PlayersBootstrapper.cs | 1 |
| **合計** | **24** |

## 依存関係

```
Phase 1 (インフラ準備)
    ↓
Phase 2 (セーブ/ロード統合)
    ↓
Phase 3 (UI表示) ← 並行可能 → Phase 4 (暫定恒久化)
    ↓                              ↓
    └──────────→ Phase 5 (廃止) ←──┘
                      ↓
    Phase 6A (旧セーブ構造削除) ← 並行可能 → Phase 6B (Obsolete削除)
                      ↓
            Phase 6C (名前統一・オプション)
```

## リスクと対策

| リスク | 影響 | 対策 |
|---|---|---|
| 既存セーブデータが壊れる | 高 | Phase 6 でマイグレーション対応 |
| UI表示が一時的におかしくなる | 中 | Phase 3 を慎重にテスト |
| 旧歩行システムが動かなくなる | 低 | 旧システムは使わない前提 |

## 受け入れ条件

### Phase 1-5（完了）
- [x] `PlayersProgressTracker` が削除されている
- [x] `GameContext.Counters.GlobalSteps` がセーブ/ロードされる
- [x] UI表示が新システムの歩数を正しく表示する
- [x] 敵復活が正しく動作する（WalkBattleMetaProvider経由）
- [x] `NowProgress` / `PlayersProgress` 参照が歩行/遭遇/復活の基準から消えている（移行用途を除く）
- [x] `GameContextHub` が null の場合にセーブ/ロードが安全にスキップされる
- [x] コンパイルエラーがない
- [x] 既存テストが通る

### Phase 6A: 旧セーブデータ構造削除（完了）
- [x] `PlayersProgressSaveData` クラスが削除されている
- [x] `PlayersSaveData.Progress` フィールドが削除されている

### Phase 6B: Obsolete コード削除（完了）
- [x] `PlayersBootstrapper.NowStageID` が削除されている
- [x] `PlayersBootstrapper.NowAreaID` が削除されている
- [x] `PlayersBootstrapper.AddProgress` が削除されている
- [x] `PlayersBootstrapper.ProgressReset` が削除されている
- [x] `PlayersBootstrapper.SetArea` が削除されている

### Phase 6C: 名前統一（完了）
- [x] `IBattleMetaProvider.NowProgress` → `GlobalSteps` に変更
- [x] `WalkBattleMetaProvider.NowProgress` → `GlobalSteps` に変更
- [x] `BattleOutcomeTracker.NowProgress` → `GlobalSteps` に変更
- [x] `EncounterContext.NowProgress` → `GlobalSteps` に変更
- [x] `BattleManager.cs` の参照が `GlobalSteps` に変更
- [x] `UnityBattleRunner.cs` の参照が `GlobalSteps` に変更
- [x] `BattleInitializer.cs` の引数名が `globalSteps` に変更
- [x] `EncounterEnemySelector.cs` の引数名が `globalSteps` に変更
- [x] `NormalEnemy.cs` の引数名が `globalSteps` に変更（9箇所）
- [x] `BattleGroup.cs` の引数名が `globalSteps` に変更
- [x] `PlayersBootstrapper.NowProgress` → `GlobalSteps` に変更
- [x] コンパイルエラーがない
- [x] 既存テストが通る

## 備考

- 旧歩行システム（Stages.cs）は既に Archive 済み
- 新歩行システムは stagedata_migration.md でクローズ済み
- この計画は新歩行システム完全移行の最終段階