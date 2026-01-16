# SkillZoneTrait リファクタリング計画

> **ステータス: ✅ 完了（2026-01-16）**

## 概要

スキル範囲性質システム（SkillZoneTrait）の保守性・可読性向上のためのリファクタリング計画。

---

## 仕様確定済み項目

### RandomRange + 既選択時の挙動

**結論:** メモと実装は一致（35%上書き）。仕様確定は不要だった。

**実装:**
```csharp
// BattleManager.cs:1159-1168
if(Acter.Target != 0 || Acter.RangeWill != 0)
{
    var RandomCaculatedPer = 35f;
    if(skill.HasZoneTrait(SkillZoneTrait.ControlByThisSituation))
    {
        RandomCaculatedPer = 14f;
    }
    if(!rollper(RandomCaculatedPer)) return; // 35%で通過、65%でリターン

    // 通過した場合、既存選択を初期化
    Acter.Target = 0;
    Acter.RangeWill = 0;
}
```

**備考:** 初回分析時にメモの一部のみを参照し「100%維持」と誤解したが、メモの「性質の組み合わせによる挙動」見出しに35%上書きの記述があり、メモと実装は最初から一致していた。

---

## 完了済み

### バグ修正: DetermineRangeRandomly のビット演算

**ファイル:** `Assets/Script/BattleManager.cs:1203`

**問題:**
```csharp
// 修正前（バグ）
Acter.RangeWill &= MainSelectFromWillTraits;
// これだとサブ性質（CanSelectAlly, CanSelectDeath等）が消えてしまう

// 修正後
Acter.RangeWill &= ~MainSelectFromWillTraits;
// メイン系を除去し、サブ性質は残る
```

**影響を受けていたスキル:**
- RandomRange + CanSelectAlly: 味方巻き込み不可だった
- RandomRange + SelectOnlyAlly: 前のめり区別なしに変換されなかった
- RandomRange + CanSelectDeath: 死者対象不可だった

---

## 提案1: ビット演算ヘルパー関数の導入

### 優先度: 中

### 問題点
ビット演算（`&=`, `|=`, `&= ~`）は直感的でなく、今回のようなバグが入りやすい。

### 解決策
`SkillZoneTrait`専用のヘルパー拡張メソッドを作成する。

### 実装案

**新規ファイル:** `Assets/Script/BaseSkill/SkillZoneTraitExtensions.cs`

```csharp
public static class SkillZoneTraitExtensions
{
    /// <summary>
    /// 指定した性質を追加する
    /// </summary>
    public static SkillZoneTrait Add(this SkillZoneTrait current, SkillZoneTrait toAdd)
        => current | toAdd;

    /// <summary>
    /// 指定した性質を除去する
    /// </summary>
    public static SkillZoneTrait Remove(this SkillZoneTrait current, SkillZoneTrait toRemove)
        => current & ~toRemove;

    /// <summary>
    /// 指定した性質のみを残す（それ以外を除去）
    /// </summary>
    public static SkillZoneTrait KeepOnly(this SkillZoneTrait current, SkillZoneTrait toKeep)
        => current & toKeep;

    /// <summary>
    /// 指定した性質を全て持っているか
    /// </summary>
    public static bool HasAll(this SkillZoneTrait current, SkillZoneTrait traits)
        => (current & traits) == traits;

    /// <summary>
    /// 指定した性質のいずれかを持っているか
    /// </summary>
    public static bool HasAny(this SkillZoneTrait current, SkillZoneTrait traits)
        => (current & traits) != 0;
}
```

### 使用例（修正後）

```csharp
// Before
Acter.RangeWill &= ~MainSelectFromWillTraits;

// After
Acter.RangeWill = Acter.RangeWill.Remove(MainSelectFromWillTraits);
```

### 影響範囲
- `BattleManager.cs` の `DetermineRangeRandomly()`
- `TargetingService.cs`
- `SelectRangeButtons.cs`
- `SelectTargetButtons.cs`

---

## 提案2: 性質グループの定数化

### 優先度: 中

### 問題点
同じ性質グループが複数箇所でハードコードされている。

### 解決策
性質グループを定数として一箇所で定義する。

### 実装案

**新規ファイルまたは既存ファイルに追加:** `Assets/Script/BaseSkill/SkillZoneTraitGroups.cs`

```csharp
/// <summary>
/// SkillZoneTraitのグループ定義
/// </summary>
public static class SkillZoneTraitGroups
{
    /// <summary>
    /// ランダム分岐用性質（実行時には不要）
    /// RandomTargetALLSituation, RandomTargetMultiOrSingle, etc.
    /// </summary>
    public static readonly SkillZoneTrait RandomBranchTraits =
        SkillZoneTrait.RandomTargetALLSituation |
        SkillZoneTrait.RandomTargetALLorMulti |
        SkillZoneTrait.RandomTargetALLorSingle |
        SkillZoneTrait.RandomTargetMultiOrSingle;

    /// <summary>
    /// 実際の範囲性質（TargetingServiceで処理される）
    /// AllTarget, RandomMultiTarget, etc.
    /// </summary>
    public static readonly SkillZoneTrait ActualRangeTraits =
        SkillZoneTrait.AllTarget |
        SkillZoneTrait.RandomMultiTarget |
        SkillZoneTrait.RandomSelectMultiTarget |
        SkillZoneTrait.RandomSingleTarget;

    /// <summary>
    /// メイン選択性質（SelectTargetFromWillの分岐で使用）
    /// </summary>
    public static readonly SkillZoneTrait MainSelectTraits =
        SkillZoneTrait.CanSelectSingleTarget |
        SkillZoneTrait.RandomSingleTarget |
        SkillZoneTrait.ControlByThisSituation |
        SkillZoneTrait.CanSelectMultiTarget |
        SkillZoneTrait.RandomSelectMultiTarget |
        SkillZoneTrait.RandomMultiTarget |
        SkillZoneTrait.AllTarget;

    /// <summary>
    /// サブ性質（オプション的な性質）
    /// CanSelectAlly, CanSelectDeath, etc.
    /// </summary>
    public static readonly SkillZoneTrait SubTraits =
        SkillZoneTrait.CanSelectAlly |
        SkillZoneTrait.CanSelectDeath |
        SkillZoneTrait.CanSelectMyself |
        SkillZoneTrait.SelectOnlyAlly |
        SkillZoneTrait.CanSelectRange;

    /// <summary>
    /// 単体系性質
    /// </summary>
    public static readonly SkillZoneTrait SingleTargetTraits =
        SkillZoneTrait.CanPerfectSelectSingleTarget |
        SkillZoneTrait.CanSelectSingleTarget |
        SkillZoneTrait.RandomSingleTarget |
        SkillZoneTrait.ControlByThisSituation;

    /// <summary>
    /// 優先的性質（他の性質より優先して処理される）
    /// </summary>
    public static readonly SkillZoneTrait PriorityTraits =
        SkillZoneTrait.SelfSkill |
        SkillZoneTrait.SelectOnlyAlly;
}
```

### 使用例

```csharp
// Before
SkillZoneTrait randomTraits = SkillZoneTrait.RandomTargetALLSituation |
                             SkillZoneTrait.RandomTargetALLorMulti |
                             SkillZoneTrait.RandomTargetALLorSingle |
                             SkillZoneTrait.RandomTargetMultiOrSingle;

// After
using static SkillZoneTraitGroups;
// ...
Acter.RangeWill = Acter.RangeWill.Remove(RandomBranchTraits);
```

### 既存定数の置換/削除工程

**二重定義となっている既存定数:**

| ファイル | 定数名 | 内容 |
|----------|--------|------|
| `CommonCalc.cs:95` | `SingleZoneTrait` | 単体系性質 |
| `SkillFilterPresets.cs:8` | `SingleTargetZoneTraitMask` | 単体系性質（同一内容） |
| `BaseSkill.HasMethod.cs:73` | ローカル定義 | 参照箇所 |

**移行手順:**
1. `SkillZoneTraitGroups.SingleTargetTraits` を作成（提案2の実装案）
2. `CommonCalc.SingleZoneTrait` の参照箇所を `SkillZoneTraitGroups.SingleTargetTraits` に置換
3. `SkillFilterPresets.SingleTargetZoneTraitMask` の参照箇所を同様に置換
4. 旧定数を `[Obsolete]` マークまたは削除
5. `SkillFilterPresets.MatchesSingleTargetReservation()` は残すが、内部で新定数を使用

**影響ファイル:**
- `Assets/Script/CommonCalc.cs`
- `Assets/Script/SkillFilterPresets.cs`
- `Assets/Script/BaseSkill/BaseSkill.HasMethod.cs`
- その他参照箇所（Grep検索で特定）

---

## 提案3: TargetingServiceの分割（Strategyパターン）

### ~~優先度: 低~~ → **実装しない**

### 実装しない理由

Strategyパターンを導入しても、新しい性質を追加する際の作業手順数は変わらない。

| 現状 | Strategyパターン導入後 |
|------|----------------------|
| 1. enum追加 | 1. enum追加 |
| 2. Groups更新 | 2. Groups更新 |
| 3. Normalizer更新 | 3. Normalizer更新 |
| 4. TargetingServiceのif-else追加 | 4. 新Strategyクラス作成 or 既存修正 |
| 5. TargetingPlan更新 | 5. TargetingPlan更新 |
| 6. UI更新 | 6. UI更新 |

ファイル数が増えるだけで保守性は向上しない。現状のif-elseチェーンは、正規化レイヤ（提案6）とTargetingPlan（提案7）があれば十分管理可能。

<details>
<summary>元の提案内容（参考）</summary>

### 問題点
`TargetingService.SelectTargets()` が大きなif-else連鎖になっており、可読性が低い。

### 解決策
各ターゲティング方式をStrategyパターンで分離する。

### 実装案

```csharp
// インターフェース
public interface ITargetingStrategy
{
    bool CanHandle(SkillZoneTrait rangeWill);
    List<BaseStates> SelectTargets(TargetingContext context);
}

// コンテキスト
public class TargetingContext
{
    public BaseStates Acter;
    public BattleGroup SelectGroup;
    public BattleGroup OurGroup;
    public SkillZoneTrait RangeWill;
    public DirectedWill Target;
}

// 各Strategy実装
public class SingleTargetStrategy : ITargetingStrategy { ... }
public class MultiTargetStrategy : ITargetingStrategy { ... }
public class AllTargetStrategy : ITargetingStrategy { ... }
public class ControlByThisSituationStrategy : ITargetingStrategy { ... }

// TargetingService（リファクタリング後）
public class TargetingService
{
    private readonly List<ITargetingStrategy> strategies;

    public TargetingService()
    {
        strategies = new List<ITargetingStrategy>
        {
            new SingleTargetStrategy(),
            new MultiTargetStrategy(),
            new AllTargetStrategy(),
            new ControlByThisSituationStrategy(),
        };
    }

    public void SelectTargets(...)
    {
        var context = new TargetingContext { ... };
        var strategy = strategies.FirstOrDefault(s => s.CanHandle(context.RangeWill));
        if (strategy != null)
        {
            var targets = strategy.SelectTargets(context);
            unders.SetList(targets);
        }
    }
}
```

### 注意点
- 大規模な変更になるため、十分なテストが必要
- 現状で動作しているなら無理に変更する必要はない

</details>

---

## 提案4: 競合組み合わせの禁止/正規化

### 優先度: 高

### 問題点
SelectOnlyAllyと前のめり/後衛系性質（CanSelectSingleTarget, CanSelectMultiTarget）が同居すると、
UI遷移とボタン生成で不整合が発生する。

**具体的な問題:**
1. `AllyClass.DetermineNextUIState()` はCanSelectSingleTarget等があれば`TabState.SelectTarget`に遷移
2. しかし`SelectTargetButtons.OnCreated()`はSelectOnlyAllyの場合、CanPerfectSelectSingleTargetのボタンしか作らない
3. 結果: **SelectOnlyAlly + CanSelectSingleTarget だとボタンが0個の空画面になる**

**参照箇所:**
- `AllyClass.cs:506` - 遷移判定
- `SelectTargetButtons.cs:100-108` - SelectOnlyAlly時のボタン生成

### 解決策

#### 案A: スキル定義時のバリデーション

スキル作成時に競合する組み合わせを警告/エラーにする。

```csharp
// BaseSkill.cs に追加
public bool ValidateZoneTrait()
{
    if (HasZoneTrait(SkillZoneTrait.SelectOnlyAlly))
    {
        // SelectOnlyAllyと競合する前のめり/後衛系
        var conflictTraits = SkillZoneTrait.CanSelectSingleTarget |
                            SkillZoneTrait.CanSelectMultiTarget |
                            SkillZoneTrait.RandomSelectMultiTarget;

        if (HasZoneTraitAny(conflictTraits))
        {
            Debug.LogWarning($"[{SkillName}] SelectOnlyAllyと前のめり/後衛系は競合します");
            return false;
        }
    }
    return true;
}
```

#### 案B: 実行時の自動正規化

DetermineRangeRandomlyや遷移判定時に自動的にマスク/変換する。

```csharp
// SelectOnlyAllyなら前のめり/後衛系をマスク
if (skill.HasZoneTrait(SkillZoneTrait.SelectOnlyAlly))
{
    // RandomSelectMultiTarget → RandomMultiTarget に変換
    if (rangeWill.HasFlag(SkillZoneTrait.RandomSelectMultiTarget))
    {
        rangeWill &= ~SkillZoneTrait.RandomSelectMultiTarget;
        rangeWill |= SkillZoneTrait.RandomMultiTarget;
    }

    // CanSelectSingleTarget/CanSelectMultiTargetを除去
    rangeWill &= ~(SkillZoneTrait.CanSelectSingleTarget |
                   SkillZoneTrait.CanSelectMultiTarget);
}
```

### 競合ルール一覧

| 優先性質 | 競合する性質 | 処理 |
|---------|-------------|------|
| SelfSkill | 全ての他性質 | 他性質は無視される（既存動作OK） |
| SelectOnlyAlly | CanSelectSingleTarget | 除去またはエラー |
| SelectOnlyAlly | CanSelectMultiTarget | 除去またはエラー |
| SelectOnlyAlly | RandomSelectMultiTarget | RandomMultiTargetに変換 |

### 影響範囲
- `BattleManager.cs` - DetermineRangeRandomly
- `AllyClass.cs` - DetermineNextUIState
- `SelectTargetButtons.cs` - OnCreated
- スキルデータ定義箇所

---

## 提案5: ユニットテストの追加

### 優先度: 中〜高

### 問題点
各性質の組み合わせが正しく動作するかを手動で確認するのは困難。

### 解決策
EditModeテストで各組み合わせを検証する。

### テストケース案

**新規ファイル:** `Assets/Tests/EditMode/TargetingServiceTests.cs`

```csharp
using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class TargetingServiceTests
{
    private TargetingService service;

    [SetUp]
    public void Setup()
    {
        service = new TargetingService();
    }

    [Test]
    public void SelfSkill_ShouldTargetOnlySelf()
    {
        // Arrange
        var acter = CreateMockActer();
        acter.RangeWill = SkillZoneTrait.SelfSkill;
        var unders = new UnderActersEntryList(null);

        // Act
        service.SelectTargets(acter, allyOrEnemy.alliy, allyGroup, enemyGroup, unders, null);

        // Assert
        Assert.AreEqual(1, unders.Count);
        Assert.AreEqual(acter, unders[0]);
    }

    [Test]
    public void SelectOnlyAlly_ShouldTargetOnlyAllies()
    {
        // ...
    }

    [Test]
    public void CanSelectAlly_ShouldIncludeAlliesInTargets()
    {
        // ...
    }

    [Test]
    public void RandomRange_WithCanSelectAlly_ShouldPreserveAllyOption()
    {
        // DetermineRangeRandomly後もCanSelectAllyが残っていることを確認
    }

    [Test]
    public void CanSelectDeath_ShouldIncludeDeadCharacters()
    {
        // ...
    }

    [Test]
    public void ControlByThisSituation_WithoutVanguard_ShouldTriggerAccident()
    {
        // ...
    }
}
```

### テストすべき組み合わせ

| 組み合わせ | 期待動作 |
|-----------|---------|
| SelfSkill | 自分のみ |
| SelectOnlyAlly | 味方のみ |
| SelectOnlyAlly + CanSelectMyself | 味方（自分含む） |
| SelectOnlyAlly + !CanSelectMyself | 味方（自分除く） |
| CanSelectAlly | 敵+味方 |
| RandomRange + CanSelectAlly | ランダム範囲で敵+味方 |
| RandomRange + SelectOnlyAlly | ランダム範囲で味方のみ（前のめり区別なし） |
| ControlByThisSituation + Vanguard存在 | 前のめりのみ |
| ControlByThisSituation + Vanguard不在 | 事故発生 |

### 追加テスト: UI遷移とRangeWill初期化

**問題:**
RangeWill初期化の経路が2系統あり、整合性の確認が必要。

| 経路 | ファイル | 処理 |
|------|----------|------|
| 旧UI経路 | `SelectTargetButtons.cs:82-85` | `CanSelectRange`がなければ直接代入 |
| Orchestrator経路 | `BattleOrchestrator.cs:173-176` | 同様の処理 |

**テストケース:**
```csharp
[Test]
public void RangeWillInitialization_LegacyUI_ShouldMatchOrchestrator()
{
    // 旧UIとOrchestratorで同じRangeWillになることを確認
}

[Test]
public void DetermineNextUIState_WithSelfSkill_ShouldNotGoToSelectRange()
{
    // SelfSkill + CanSelectRange でも SelectRange に遷移しないことを確認
}

[Test]
public void DetermineNextUIState_SelectOnlyAlly_WithCanSelectSingleTarget_ShouldHandleGracefully()
{
    // 競合組み合わせでの遷移を確認（提案4関連）
}
```

### 追加テスト: FreezeRangeWill（連続実行）

**問題:**
スキル強制続行中（FreezeConsecutive）は`FreezeRangeWill`にキャッシュされるが、テストされていない。

**参照:** `BattleManager.cs:1125` - `Acter.SetFreezeRangeWill(skill.ZoneTrait)`

**テストケース:**
```csharp
[Test]
public void FreezeRangeWill_DuringConsecutiveAttack_ShouldPreserveRangeWill()
{
    // 連続攻撃中にFreezeRangeWillが正しく維持されることを確認
}

[Test]
public void FreezeRangeWill_AfterConsecutiveComplete_ShouldBeCleared()
{
    // 連続攻撃完了後にFreezeRangeWillがクリアされることを確認
}
```

---

## 提案6: Trait正規化レイヤの追加（SkillZoneTraitNormalizer）

### 優先度: 高（提案4の発展形として推奨）

### 概要
範囲性質の競合解消を1箇所に集約し、RangeWillに入る前に必ず正規化する。

### メリット
- 命中対象のブレやUI/実処理の食い違いを減らせる
- 正規化ロジックが散在しない
- テスト容易

### 実装案

**新規ファイル:** `Assets/Script/BaseSkill/SkillZoneTraitNormalizer.cs`

```csharp
public static class SkillZoneTraitNormalizer
{
    /// <summary>
    /// 範囲性質を正規化する。RangeWillへ代入する直前に必ず呼ぶこと。
    /// </summary>
    public static SkillZoneTrait Normalize(SkillZoneTrait traits)
    {
        var result = traits;

        // SelfSkillは最優先、他は無視
        if (result.HasFlag(SkillZoneTrait.SelfSkill))
        {
            return SkillZoneTrait.SelfSkill;
        }

        // SelectOnlyAllyなら前のめり/後衛系を無効化
        if (result.HasFlag(SkillZoneTrait.SelectOnlyAlly))
        {
            // RandomSelectMultiTarget → RandomMultiTarget
            if (result.HasFlag(SkillZoneTrait.RandomSelectMultiTarget))
            {
                result &= ~SkillZoneTrait.RandomSelectMultiTarget;
                result |= SkillZoneTrait.RandomMultiTarget;
            }

            // CanSelectSingleTarget/CanSelectMultiTarget を除去
            result &= ~(SkillZoneTrait.CanSelectSingleTarget |
                       SkillZoneTrait.CanSelectMultiTarget);
        }

        return result;
    }

    /// <summary>
    /// RandomRange処理後の正規化（メイン系を除去してランダム結果を追加）
    /// </summary>
    public static SkillZoneTrait NormalizeAfterRandomRange(
        SkillZoneTrait original,
        SkillZoneTrait randomResult)
    {
        var result = original;

        // ランダム分岐用性質を除去
        result &= ~SkillZoneTraitGroups.RandomBranchTraits;

        // 実際の範囲性質を除去
        result &= ~SkillZoneTraitGroups.ActualRangeTraits;

        // メイン選択性質を除去
        result &= ~SkillZoneTraitGroups.MainSelectTraits;

        // ランダム結果を追加
        result |= randomResult;

        // 最終正規化
        return Normalize(result);
    }
}
```

### 呼び出し箇所

| タイミング | ファイル | 現状 |
|------------|----------|------|
| スキル選択時 | `AllyClass.cs` | RangeWill代入前 |
| RandomRange決定後 | `BattleManager.cs` | DetermineRangeRandomly内 |
| Freeze再開時 | `BattleManager.cs` | FreezeRangeWill適用時 |
| Orchestrator経由 | `BattleOrchestrator.cs` | RangeWill代入前 |

---

## 提案7: RangeIntent/TargetingPlanの導入

### 優先度: 中（提案3の代替案）

### 概要
範囲性質の解釈結果を値オブジェクトとして1回だけ生成し、UI/AI/TargetingServiceが共通利用する。

### メリット
- UI遷移や対象抽出の分岐重複を削減
- 解釈結果が明示的になりデバッグしやすい

### 実装案

```csharp
/// <summary>
/// 範囲性質の解釈結果（不変オブジェクト）
/// </summary>
public readonly struct TargetingPlan
{
    public readonly TargetScope Scope;           // 敵/味方/両方/自分
    public readonly SelectionMode Mode;          // 完全単体/前のめり後衛/ランダム/全体
    public readonly int MaxTargets;              // 最大対象数
    public readonly bool CanSelectDeath;         // 死者選択可
    public readonly bool CanSelectMyself;        // 自分選択可
    public readonly DirectedWill[] AllowedWills; // 許可されるDirectedWill

    public static TargetingPlan FromRangeWill(SkillZoneTrait rangeWill, BaseStates acter)
    {
        // 正規化済みのrangeWillからPlanを生成
        // ...
    }
}

public enum TargetScope { Enemy, Ally, Both, Self }
public enum SelectionMode { PerfectSingle, VanguardBackline, Random, All }
```

### 使用フロー

```
スキル選択
    ↓
SkillZoneTraitNormalizer.Normalize()
    ↓
TargetingPlan.FromRangeWill()  ← 1回だけ解釈
    ↓
┌─────────────────────────────────────┐
│ SelectRangeButtons: Plan参照        │
│ SelectTargetButtons: Plan参照       │
│ TargetingService: Plan参照          │
│ BattleAI: Plan参照                  │
└─────────────────────────────────────┘
```

### 評価
- 提案3（Strategyパターン）より軽量
- 既存コードへの影響が比較的小さい
- 段階的に導入可能

---

## 提案8: スキル資産の検証パイプライン

### 優先度: 中

### 概要
エディタメニューやインポート時にスキルのTrait整合性をチェックし、破綻した組み合わせを事前検出する。

### メリット
- 破綻したTraitの組み合わせが実行時に混入するのを防ぐ
- 範囲性質の仕様を資産レベルで保証

### 実装案

**新規ファイル:** `Assets/Editor/SkillTraitValidator.cs`

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SkillTraitValidator
{
    [MenuItem("Tools/pigRPG/Validate All Skill Traits")]
    public static void ValidateAllSkills()
    {
        var skills = Resources.LoadAll<BaseSkill>("");
        var issues = new List<string>();

        foreach (var skill in skills)
        {
            var result = ValidateSkill(skill);
            if (!result.IsValid)
            {
                issues.Add($"[{skill.SkillName}] {result.Message}");
            }
        }

        if (issues.Count > 0)
        {
            Debug.LogWarning($"Trait検証: {issues.Count}件の問題\n" + string.Join("\n", issues));
        }
        else
        {
            Debug.Log("Trait検証: 全スキル正常");
        }
    }

    public static ValidationResult ValidateSkill(BaseSkill skill)
    {
        var traits = skill.ZoneTrait;

        // SelectOnlyAlly + 前のめり/後衛系の競合
        if (traits.HasFlag(SkillZoneTrait.SelectOnlyAlly))
        {
            if (traits.HasFlag(SkillZoneTrait.CanSelectSingleTarget) ||
                traits.HasFlag(SkillZoneTrait.CanSelectMultiTarget))
            {
                return new ValidationResult(false,
                    "SelectOnlyAllyと前のめり/後衛系は競合します。" +
                    "CanPerfectSelectSingleTargetまたはRandomMultiTargetを使用してください。");
            }
        }

        // 正規化前後で変化があれば警告
        var normalized = SkillZoneTraitNormalizer.Normalize(traits);
        if (normalized != traits)
        {
            return new ValidationResult(false,
                $"正規化により変更されます: {traits} → {normalized}");
        }

        return new ValidationResult(true, "");
    }

    public readonly struct ValidationResult
    {
        public readonly bool IsValid;
        public readonly string Message;
        public ValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }
    }
}
#endif
```

### 拡張案
- ScriptableObjectインポート時のPostprocessor
- CI/CDパイプラインでの自動検証
- 検証結果のレポートファイル出力

---

## 提案9: フラグ分割の構造化（長期目標）

### ~~優先度: 低（破壊的変更のため）~~ → **実装しない**

### 実装しない理由

**TargetingPlan（提案7）が既にこの目的を達成している。**

提案9は「スキル定義側のデータ構造を変える」アプローチだが、TargetingPlanは「実行時に解釈する」アプローチで同じ効果を得ている。

```csharp
// TargetingPlan（実装済み）
public readonly struct TargetingPlan
{
    public readonly TargetScope Scope;      // 敵/味方/両方/自分
    public readonly SelectionMode Mode;     // 完全単体/前のめり後衛/ランダム/全体
    public readonly bool CanSelectDeath;
    public readonly bool CanSelectMyself;
    // ...
}
```

TargetingPlanの利点:
- 既存のスキルデータを変更不要
- 既存コードとの互換性維持
- 同じ「分離された構造」をUI/AI/Serviceが利用可能

<details>
<summary>元の提案内容（参考）</summary>

### 概要
現在の19フラグを「範囲形状」「選択方法」「対象スコープ」「オプション」に分離し、無効な組み合わせを設計レベルで防ぐ。

### 現状の問題
SkillZoneTraitは19個のフラグが混在し、どの組み合わせが有効/無効か判断しにくい。

### 提案する構造

```csharp
/// <summary>
/// 範囲形状（排他的）
/// </summary>
public enum RangeShape
{
    Single,      // 単体
    Multi,       // 複数（2人程度）
    All,         // 全体
    Random1to3   // ランダム1〜3人
}

/// <summary>
/// 選択方法（排他的）
/// </summary>
public enum SelectionMode
{
    Perfect,           // 完全選択（個々指定）
    VanguardBackline,  // 前のめり/後衛選択
    Random,            // ランダム
    ControlBySituation // 状況依存
}

/// <summary>
/// 対象スコープ（排他的）
/// </summary>
public enum TargetScope
{
    EnemyOnly,    // 敵のみ
    AllyOnly,     // 味方のみ
    Both,         // 敵+味方
    SelfOnly      // 自分のみ
}

/// <summary>
/// オプションフラグ（組み合わせ可）
/// </summary>
[Flags]
public enum TargetOptions
{
    None = 0,
    CanSelectDeath = 1 << 0,
    CanSelectMyself = 1 << 1,
    RandomRange = 1 << 2,
    CanSelectRange = 1 << 3
}

/// <summary>
/// 範囲仕様（構造体）
/// </summary>
public struct RangeSpec
{
    public RangeShape Shape;
    public SelectionMode Mode;
    public TargetScope Scope;
    public TargetOptions Options;

    // 既存Traitからの変換
    public static RangeSpec FromZoneTrait(SkillZoneTrait trait) { ... }

    // 既存Traitへの変換（互換性維持）
    public SkillZoneTrait ToZoneTrait() { ... }
}
```

### 移行ステップ
1. RangeSpec構造体を追加（既存コードと並行）
2. 変換関数を実装しテスト
3. 新規スキルはRangeSpecで定義
4. 段階的に既存スキルを移行
5. 最終的にSkillZoneTraitを廃止

### 評価
- **メリット:** 設計レベルで不正組み合わせを防げる
- **デメリット:** 移行コストが非常に高い、既存スキルデータ全ての変換が必要
- **推奨:** 長期目標として保留、当面は提案6（正規化レイヤ）で対応

</details>

---

## 提案10: ルールのデータ駆動化（長期目標）

### ~~優先度: 低~~ → **実装しない**

### 実装しない理由

- **プロジェクト規模に対して過剰**: 個人/小規模チーム開発では、データ駆動化の恩恵（デザイナーがコード変更なしでルール調整）が活きない
- **ルール変更頻度が低い**: 性質追加は稀であり、コード変更で十分対応可能
- **デバッグ困難**: ルールがScriptableObjectに分散すると、問題発生時の追跡が難しくなる
- **現状で十分**: 正規化レイヤ（提案6）とTargetingPlan（提案7）により、ルールは1箇所に集約済み

<details>
<summary>元の提案内容（参考）</summary>

### 概要
「入力Trait → 選択UI/対象決定/優先順位/ランダム分岐」をScriptableObjectで定義し、コード変更なしでルール調整可能にする。

### メリット
- ルール変更がコード修正不要
- UI/AI/TargetingServiceのルール統一が容易

### 懸念点
- 現状の複雑なif-else分岐をテーブル化するのは大規模
- デバッグが難しくなる可能性

### 評価
提案7（TargetingPlan）を先に導入し、その後検討する方が現実的。

</details>

---

## 補足: 前提資料の整合性（スコープ外）

### [P3] CanSelectMyself矛盾

**問題:**
メモ（豚のスキル範囲性質.md:353）では「CanSelectMyself: 未実装」とあるが、
実装では処理が存在する。

**実装箇所:**
- `TargetingService.cs:42-62` - CanSelectMyselfの処理
- `SelectRangeButtons.cs:258-284` - 自分自身ボタン生成

**対応:**
メモの「未実装」記述を削除または「実装済み」に更新する。
（本計画のスコープ外だが、前提資料の整合性として記録）

---

## 実装優先順位

| 順位 | 提案 | 状態 |
|------|------|------|
| 1 | バグ修正 | ✅ **完了** |
| 2 | 仕様確定（RandomRange既選択時） | ✅ **完了**（メモと実装は一致していた） |
| 3 | 競合組み合わせの禁止/正規化（提案4） | ✅ **完了** |
| 4 | Trait正規化レイヤ（提案6） | ✅ **完了** |
| 5 | 性質グループの定数化（提案2） | ✅ **完了** |
| 6 | スキル資産検証パイプライン（提案8） | ✅ **完了** |
| 7 | ユニットテスト（提案5） | ✅ **完了**（雛形作成） |
| 8 | ビット演算ヘルパー（提案1） | ✅ **完了** |
| 9 | TargetingPlan（提案7） | ✅ **完了** |
| 10 | Strategyパターン（提案3） | ❌ **実装しない** - 手順数変わらず効果なし |
| 11 | フラグ分割構造化（提案9） | ❌ **実装しない** - TargetingPlanで代替済み |
| 12 | データ駆動化（提案10） | ❌ **実装しない** - プロジェクト規模に対して過剰 |

---

## 変更時の注意点

1. **既存スキルデータの確認**
   - RandomRange系スキルに付与されているサブ性質を確認
   - 修正により挙動が変わるスキルがないか検証

2. **テスト環境**
   - PlayModeで実際に各スキルを使用して確認
   - 特にRandomRange + CanSelectAllyの組み合わせ

3. **段階的適用**
   - 一度に全て変更せず、提案ごとに適用・テスト

---

## 関連ファイル一覧

| ファイル | 役割 |
|----------|------|
| `Assets/Script/BaseSkill/BaseSkill.Core.cs` | SkillZoneTrait enum定義 |
| `Assets/Script/BattleManager.cs` | DetermineRangeRandomly, SkillACT |
| `Assets/Script/Battle/Services/TargetingService.cs` | ターゲット選択ロジック |
| `Assets/Script/SelectRangeButtons.cs` | 範囲選択UI |
| `Assets/Script/SelectTargetButtons.cs` | 対象選択UI |
| `Assets/Script/Players/Runtime/AllyClass.cs` | DetermineNextUIState |
| `Assets/Script/BaseStates/Battle/BaseStates.SkillACTUtility.cs` | RangeWill関連ヘルパー |

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-16 | 初版作成、バグ修正完了 |
| 2026-01-16 | 外部レビュー反映: 仕様確定項目追加、競合組み合わせ提案追加、既存定数統合工程追加、UI遷移/FreezeRangeWillテスト追加 |
| 2026-01-16 | 外部提案追加: Trait正規化レイヤ(提案6)、TargetingPlan(提案7)、検証パイプライン(提案8)、フラグ分割(提案9)、データ駆動化(提案10)、CanSelectMyself矛盾記録 |
| 2026-01-16 | **実装完了**: 提案1(ビット演算ヘルパー)、提案2(性質グループ定数化)、提案6(正規化レイヤ)、提案8(検証パイプライン)、提案5(テスト雛形)、既存コードへのヘルパー適用 |
| 2026-01-16 | **実装完了**: 提案4(正規化適用) - BattleOrchestrator, SelectRangeButtons, SelectTargetButtons, BattleManager, BattleAIBrainに正規化適用 |
| 2026-01-16 | **実装完了**: 提案7(TargetingPlan) - TargetingPlan値オブジェクト作成、TargetScope/SelectionMode enum追加 |
| 2026-01-16 | **計画完了**: 提案3,9,10を「実装しない」と判定。理由: 提案3は手順数変わらず、提案9はTargetingPlanで代替済み、提案10はプロジェクト規模に対して過剰 |
| 2026-01-16 | **クローズ**: 全提案の実装/判定完了。RandomRangeの追加分離は不要と判断（既にDetermineRangeRandomlyに隔離済み）。doc/completed/ へ移動 |
