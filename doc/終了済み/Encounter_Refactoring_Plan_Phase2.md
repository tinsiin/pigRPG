# エンカウント周りリファクタリング計画 Phase 2

## 0. 概要

前回の `Encounter_Refactoring_Plan.md` で実施したリファクタリング（Phase 1〜5）の継続として、残存する設計問題を改善する計画。

**ブランチ:** `refactor/global-hub-cleanup` の方針（グローバルHub依存削減＋注入可能化）に準拠。

---

## 1. 現状の問題点一覧

| 優先度 | 問題 | 場所 | 影響 |
|--------|------|------|------|
| **高** | EnemyRebornManager がシングルトン | `EnemyRebornManager.cs:24` | テスト困難、メモリリーク可能性 |
| **高** | EncounterEnemySelector が全て static | `EncounterEnemySelector.cs` | モック不可、テスト困難 |
| **中** | EncounterResolver の内部 static メソッド | `EncounterResolver.cs:110-175` | 部分的なテスト困難 |
| **中** | 命名の不統一（RecovelySteps） | `NormalEnemy.cs:37,44` | 可読性低下、タイポ |
| **中** | テストがReflection依存 | `EncounterEnemySelectorTests.cs:206-207` | テスト脆弱性 |
| **低** | GameContext の EncounterOverlayStack プロキシ | `GameContext.cs:255-272` | 冗長なラッパー |

---

## 2. 詳細分析

### 2.1 EnemyRebornManager のシングルトン依存（優先度: 高）

**現状:**
```csharp
// EnemyRebornManager.cs:24
public static EnemyRebornManager Instance { get; } = new EnemyRebornManager();

// 使用箇所（4箇所）
BattleGroup.cs:455         → EnemyRebornManager.Instance.OnBattleEnd(...)
NormalEnemy.cs:60          → EnemyRebornManager.Instance.ReadyRecovelyStep(...)
NormalEnemy.cs:68          → EnemyRebornManager.Instance.CanReborn(...)
EncounterEnemySelector.cs:79 → EnemyRebornManager.Instance.CanReborn(...)
```

**問題:**
- テスト時に状態がグローバルに残る（テストの独立性が損なわれる）
- 敵インスタンスが破棄されても辞書 `_infos` に残り続ける（メモリリーク）
- モック注入が困難

**改善案:**
- `IEnemyRebornManager` インターフェースを導入
- コンストラクタ/メソッド引数でDI可能に
- `Instance` は互換性のためフォールバックとして残す

---

### 2.2 EncounterEnemySelector が全て static（優先度: 高）

**現状:**
```csharp
// EncounterEnemySelector.cs:7,16
public static class EncounterEnemySelector
{
    public static BattleGroup SelectGroup(...) { ... }
    private static NormalEnemy SelectLeader(...) { ... }
    private static bool TryResolveManualEnd(...) { ... }
    private static bool TryResolveAutoEnd(...) { ... }
}
```

**問題:**
- クラス全体が static のため、サブクラス化・モック化が不可能
- テストで Reflection を使用せざるを得ない（`EncounterEnemySelectorTests.cs:206-207`）
- `EnemyRebornManager.Instance` への直接参照がハードコード

**改善案:**
- インスタンスクラスに変更
- `IEnemyRebornManager` を注入可能に
- 旧 static メソッドは互換性のため薄いラッパーとして残す

---

### 2.3 命名の不統一（優先度: 中）

**現状:**
```csharp
// NormalEnemy.cs:37,44
public int RecovelySteps = -1;  // タイポ: Recovery or Reborn が正しい
public bool Reborn { get => RecovelySteps >= 0; }

// EnemyRebornManager.cs:49
info.RemainingSteps = enemy.RecovelySteps;

// 関連用語の混在
- RecovelySteps（フィールド名、タイポ）
- Reborn（プロパティ）
- OnReborn()（メソッド）
- CanReborn()（メソッド）
- ReadyRecovelyStep()（メソッド、タイポ）
- EnemyRebornState（enum）
```

**改善案:**
- `RecovelySteps` → `RebornSteps` に統一（`Reborn` 系に揃える）
- `ReadyRecovelyStep()` → `PrepareReborn()` または `ReadyRebornStep()`

---

### 2.4 EncounterResolver の内部 static メソッド（優先度: 中）

**現状:**
```csharp
// EncounterResolver.cs
public sealed class EncounterResolver
{
    // インスタンスメソッド
    public EncounterRollResult Resolve(...) { ... }

    // static helper（テストしにくい）
    private static void TickState(...) { ... }
    private static EncounterSO PickEncounter(...) { ... }
    private static bool AreConditionsMet(...) { ... }
}
```

**問題:**
- `PickEncounter` や `AreConditionsMet` が static のため、個別テストが困難
- ログ用の static メソッドは許容範囲だが、ロジック系はインスタンス化推奨

**改善案:**
- ロジック系 static メソッドをインスタンスメソッドに変更
- 必要なら `IEncounterPicker` などのインターフェースで分離

---

### 2.5 テストの Reflection 依存（優先度: 中）

**現状:**
```csharp
// EncounterEnemySelectorTests.cs:206-207
var method = GetSelectorMethod("FilterEligibleEnemies");
return (List<NormalEnemy>)method.Invoke(null, new object[] { enemies, globalSteps });
```

**問題:**
- private メソッドを Reflection でテストしている
- メソッド名変更でテストが壊れる
- 型安全性がない

**改善案:**
- クラスをインスタンス化して internal メソッドに変更
- または public なヘルパークラスに分離

---

### 2.6 GameContext の EncounterOverlayStack プロキシ（優先度: 低）

**現状:**
```csharp
// GameContext.cs（推定）
public void PushEncounterOverlay(...) { encounterOverlays.Push(...); }
public void PopEncounterOverlay(...) { encounterOverlays.Pop(...); }
public float GetEncounterMultiplier() { return encounterOverlays.GetMultiplier(); }
public void AdvanceEncounterOverlays() { encounterOverlays.Advance(); }
```

**問題:**
- 単なるプロキシメソッドが多数存在
- API が二重に存在する形

**改善案:**
- `EncounterOverlayStack` を `public` プロパティとして公開
- プロキシメソッドは段階的に deprecated に

---

## 3. リファクタリング計画

### Phase 2-1: IEnemyRebornManager インターフェース導入（高優先度）

**目的:** EnemyRebornManager を DI 可能にしてテスト容易性を向上

**タスク:**
1. `IEnemyRebornManager` インターフェースを作成
2. `EnemyRebornManager` にインターフェースを実装
3. 使用箇所にフォールバック付き注入パターンを適用
4. テストでモック注入可能に

**新規ファイル:**
- `Assets/Script/Battle/IEnemyRebornManager.cs`

**変更ファイル:**
- `Assets/Script/Battle/EnemyRebornManager.cs` - インターフェース実装
- `Assets/Script/BattleGroup.cs` - 注入パターン適用
- `Assets/Script/Enemy/NormalEnemy.cs` - 注入パターン適用
- `Assets/Script/Walk/Encounter/EncounterEnemySelector.cs` - 引数で受け取り

**API設計:**
```csharp
public interface IEnemyRebornManager
{
    void OnBattleEnd(IReadOnlyList<NormalEnemy> enemies, int globalSteps);
    void ReadyRecovelyStep(NormalEnemy enemy, int globalSteps);
    bool CanReborn(NormalEnemy enemy, int globalSteps);
    void Clear(NormalEnemy enemy);
}
```

**完了条件:**
- 使用箇所4件がフォールバック付き注入パターンに変更
- テストでモック注入が可能

---

### Phase 2-2: EncounterEnemySelector のインスタンス化（高優先度）

**目的:** static クラスをインスタンスクラスに変更してテスト容易性を向上

**タスク:**
1. `static class` → 通常の `class` に変更
2. `IEnemyRebornManager` をコンストラクタで受け取り
3. public static メソッドは互換性のため薄いラッパーとして残す
4. テストを Reflection なしに書き換え

**変更ファイル:**
- `Assets/Script/Walk/Encounter/EncounterEnemySelector.cs`
- `Assets/Editor/Tests/EncounterEnemySelectorTests.cs`

**API設計:**
```csharp
public class EncounterEnemySelector
{
    private readonly IEnemyRebornManager _rebornManager;

    // テスト用
    public EncounterEnemySelector(IEnemyRebornManager rebornManager)
    {
        _rebornManager = rebornManager;
    }

    // 互換性維持用（フォールバック）
    public EncounterEnemySelector() : this(EnemyRebornManager.Instance) { }

    // 旧 static メソッドの互換ラッパー
    public static BattleGroup SelectGroupStatic(...) => new EncounterEnemySelector().SelectGroup(...);
}
```

**完了条件:**
- `EnemyRebornManager.Instance` への直接参照が除去
- テストから Reflection が除去

---

### Phase 2-3: 命名統一（中優先度）

**目的:** `RecovelySteps` のタイポを修正し、命名を `Reborn` 系に統一

**タスク:**
1. `RecovelySteps` → `RebornSteps` にリネーム
2. `ReadyRecovelyStep()` → `PrepareReborn()` にリネーム
3. 関連テスト・ドキュメントの更新

**変更ファイル:**
- `Assets/Script/Enemy/NormalEnemy.cs`
- `Assets/Script/Battle/EnemyRebornManager.cs`
- `Assets/Editor/Tests/EnemyRebornManagerTests.cs`
- `Assets/Editor/Tests/EncounterEnemySelectorTests.cs`

**影響範囲:**
- フィールド名変更のため、既存セーブデータとの互換性に注意
- ScriptableObject で `RecovelySteps` を参照している場合は対応が必要

**完了条件:**
- `RecovelySteps` の使用箇所が 0 件
- ビルド・テスト通過

---

### Phase 2-4: EncounterResolver の static メソッド整理（中優先度）

**目的:** ロジック系 static メソッドをインスタンスメソッドに変更

**タスク:**
1. `TickState` をインスタンスメソッドに変更
2. `PickEncounter` をインスタンスメソッドに変更
3. `AreConditionsMet` をインスタンスメソッドに変更（または public helper に分離）

**変更ファイル:**
- `Assets/Script/Walk/Encounter/EncounterResolver.cs`

**完了条件:**
- ロジック系 static メソッドがインスタンスメソッドに変更
- 既存動作に影響なし

---

### Phase 2-5: テストの Reflection 除去（中優先度）

**目的:** テストコードから Reflection 依存を除去

**タスク:**
1. Phase 2-2 完了後、`EncounterEnemySelectorTests` の Reflection を除去
2. 必要なメソッドを `internal` + `[InternalsVisibleTo]` で公開
3. または public なヘルパークラスに分離

**変更ファイル:**
- `Assets/Editor/Tests/EncounterEnemySelectorTests.cs`
- `Assets/Editor/Tests/EnemyRebornManagerTests.cs`（必要に応じて）

**完了条件:**
- テストから `GetMethod` / `Invoke` が除去
- テストが型安全に

---

### Phase 2-6: EncounterOverlayStack 公開（低優先度）

**目的:** プロキシメソッドの冗長性を解消

**タスク:**
1. `GameContext.EncounterOverlays` プロパティを public に
2. 旧プロキシメソッドに `[Obsolete]` 属性を付与
3. 呼び出し側を段階的に直接アクセスに移行

**変更ファイル:**
- `Assets/Script/Walk/GameContext.cs`

**完了条件:**
- プロキシメソッドに `[Obsolete]` が付与
- 新規コードは直接アクセスを使用

---

## 4. 依存関係と実施順序

```
Phase 2-1 (IEnemyRebornManager)
    ↓
Phase 2-2 (EncounterEnemySelector インスタンス化)
    ↓
Phase 2-5 (テスト Reflection 除去) ← Phase 2-2 に依存
    ↓
Phase 2-3 (命名統一) ← Phase 2-1, 2-2 完了後が望ましい
    ↓
Phase 2-4 (EncounterResolver 整理) ← 独立して実施可能
    ↓
Phase 2-6 (EncounterOverlayStack 公開) ← 独立して実施可能
```

---

## 5. リスクと対策

| リスク | 影響 | 対策 |
|-------|------|------|
| 命名変更でセーブデータ破損 | データ互換性 | `[FormerlySerializedAs]` 属性を使用 |
| static → instance で呼び出し元が大量修正 | 工数増加 | 互換ラッパーを残す |
| テスト変更で挙動差分 | テスト信頼性低下 | 変更前後で同一結果を確認 |

---

## 6. 完了条件（DoD）

- [x] Phase 2-1: `IEnemyRebornManager` 導入、使用箇所4件がフォールバック付き注入に
- [x] Phase 2-2: `EncounterEnemySelector` がインスタンスクラスに
- [x] Phase 2-3: `RecovelySteps` → `RebornSteps` 統一
- [x] Phase 2-4: `EncounterResolver` の static メソッドがインスタンス化
- [x] Phase 2-5: テストから Reflection 除去（Phase 2-2 で完了）
- [x] Phase 2-6: `EncounterOverlayStack` 公開、プロキシに `[Obsolete]`
- [ ] 全テスト通過
- [ ] 手動テスト（エンカウント→バトル→復活→再エンカウント）通過

---

## 7. 変更履歴

| 日付 | 内容 |
|------|------|
| 2026-02-02 | 初版作成 |
| 2026-02-02 | Phase 2-1 完了: IEnemyRebornManager インターフェース導入 |
| 2026-02-02 | Phase 2-2 完了: EncounterEnemySelector インスタンス化 |
| 2026-02-02 | Phase 2-3 完了: 命名統一（RecovelySteps → RebornSteps） |
| 2026-02-02 | Phase 2-4 完了: EncounterResolver の static メソッド整理 |
| 2026-02-02 | Phase 2-6 完了: EncounterOverlayStack 公開 |

---

## 9. 実施記録

### Phase 2-1 実施結果（2026-02-02）

**新規ファイル:**
- `Assets/Script/Battle/IEnemyRebornManager.cs` - インターフェース定義

**変更ファイル:**
- `Assets/Script/Battle/EnemyRebornManager.cs` - `IEnemyRebornManager` を実装
- `Assets/Script/BattleGroup.cs` - `RecovelyStart` にオプショナル引数追加
- `Assets/Script/Enemy/NormalEnemy.cs` - `ReadyRecovelyStep`, `CanRebornWhatHeWill` にオプショナル引数追加
- `Assets/Script/Walk/Encounter/EncounterEnemySelector.cs` - `SelectGroup`, `FilterEligibleEnemies` に引数追加
- `Assets/Editor/Tests/EncounterEnemySelectorTests.cs` - `TestRebornManager` モッククラス追加、DIテスト2件追加

**結果:**
- 使用箇所4件がフォールバック付き注入パターンに変更
- `EnemyRebornManager.Instance` への直接参照が除去（フォールバックのみ）
- モック注入によるテストが可能に

### Phase 2-2 実施結果（2026-02-02）

**変更ファイル:**
- `Assets/Script/Walk/Encounter/EncounterEnemySelector.cs` - static class → instance class に変更
- `Assets/Editor/Tests/EncounterEnemySelectorTests.cs` - Reflection 除去、直接メソッド呼び出しに変更

**変更内容:**
- `static class` → `class` に変更
- フィールドとして `_rebornManager`, `_matchCalc` を追加
- コンストラクタで依存性注入（デフォルトコンストラクタはフォールバック）
- `SelectGroup` → `Select` にリネーム（インスタンスメソッド）
- 旧 static `SelectGroup` は互換性維持用ラッパーとして残存
- private メソッドを internal に変更（`FilterEligibleEnemies`, `TryResolveManualEnd`, `TryResolveAutoEnd`, `TryAddCompatibleTarget`, `GetPartyImpression`, `HasSympathy`）

**テスト:**
- Reflection 使用箇所を全て除去
- インスタンス経由で internal メソッドを直接テスト
- 新規テスト2件追加（`Select_ReturnsNullWhenNoEnemies`, `Select_ReturnsBattleGroupWithInjectedDependencies`）

**結果:**
- テストから `GetMethod` / `Invoke` が完全に除去
- 型安全なテストに移行完了

### Phase 2-3 実施結果（2026-02-02）

**変更ファイル:**
- `Assets/Script/Battle/IEnemyRebornManager.cs` - `ReadyRecovelyStep` → `PrepareReborn`
- `Assets/Script/Battle/EnemyRebornManager.cs` - `ReadyRecovelyStep` → `PrepareReborn`, `RecovelySteps` → `RebornSteps`
- `Assets/Script/Enemy/NormalEnemy.cs` - フィールド/メソッド名変更、`[FormerlySerializedAs]` 追加
- `Assets/Editor/Tests/EnemyRebornManagerTests.cs` - `RecovelySteps` → `RebornSteps`
- `Assets/Editor/Tests/EncounterEnemySelectorTests.cs` - 同上

**変更内容:**
- `RecovelySteps` → `RebornSteps`（フィールド名修正）
- `ReadyRecovelyStep()` → `PrepareReborn()`（メソッド名修正）
- `[FormerlySerializedAs("RecovelySteps")]` 追加（セーブデータ互換性維持）
- ログメッセージ内の用語も修正（`recovelyStepCount` → `rebornSteps`）

**結果:**
- タイポが修正され、命名が `Reborn` 系に統一
- セーブデータ互換性は `FormerlySerializedAs` で維持

### Phase 2-4 実施結果（2026-02-02）

**変更ファイル:**
- `Assets/Script/Walk/Encounter/EncounterResolver.cs`

**変更内容:**
- `TickState` を非 static 化（未使用の `table` 引数を削除）
- `PickEncounter` を非 static 化、internal に変更
- `AreConditionsMet` を internal static に変更（純粋関数なので static 維持）
- ログ用メソッド（`IsLogEnabled`, `Log`）は static 維持（副作用なしのユーティリティ）

**結果:**
- ロジック系メソッドがインスタンスメソッドに変更
- internal 化によりテスト可能に

### Phase 2-6 実施結果（2026-02-02）

**変更ファイル:**
- `Assets/Script/Walk/GameContext.cs`

**変更内容:**
- `using System` を追加
- `PushEncounterOverlay` に `[Obsolete]` 属性を付与
- `RemoveEncounterOverlay` に `[Obsolete]` 属性を付与
- `AdvanceEncounterOverlays` に `[Obsolete]` 属性を付与
- `GetEncounterMultiplier` に `[Obsolete]` 属性を付与

**結果:**
- プロキシメソッドが deprecated に
- 新規コードは `EncounterOverlays` プロパティ経由で直接アクセス推奨

---

## 8. 関連ドキュメント

- `doc/終了済み/Encounter_Refactoring_Plan.md` - 前回のリファクタリング計画（Phase 1〜5 完了済み）
- `doc/終了済み/GlobalHub_Refactoring_Plan.md` - グローバルHub整理計画（完了済み）
- `doc/PlayersStates_Global_Dependency_Refactor.md` - グローバル依存リファクタ
