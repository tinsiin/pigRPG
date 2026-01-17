# スキルUI リファクタリング計画

**ステータス: ✅ 完了（2026-01-17）**

## 概要

スキル選択UIからBattleManagerまでのコードフローにおける重複・散在の解消計画。

## 完了サマリー

| 提案 | 状態 |
|------|------|
| 提案1: SelectRangeButtonsヘルパー抽出 | ✅ 完了 |
| 提案2: DetermineNextUIState統合 | ✅ 完了 |
| 提案3: RangeWill初期化集約 | ✅ 完了 |
| 提案4: 旧UI経路削除 | ✅ 完了 |
| 提案5: SelectTargetButtonsヘルパー抽出 | ✅ 完了 |

---

## 提案1: ボタン生成ヘルパーの抽出

### 優先度: 高

### 問題点

SelectRangeButtons.OnCreated()内で、同じボタン生成パターンが10回以上繰り返されている。

```csharp
// このパターンが全ての性質で繰り返し（約20行 × 10箇所 = 200行の重複）
var button = Instantiate(buttonPrefab, transform);
var rect = button.GetComponent<RectTransform>();
if (currentX + buttonSize.x / 2 > parentSize.x / 2) {
    currentX = startX;
    currentY -= (buttonSize.y + verticalPadding);
}
rect.anchoredPosition = new Vector2(currentX, currentY);
currentX += (buttonSize.x + horizontalPadding);
button.onClick.AddListener(() => OnClickRangeBtn(button, trait));
button.GetComponentInChildren<TextMeshProUGUI>().text = "テキスト";
buttonList.Add(button);
```

### 解決策

ヘルパーメソッドを抽出し、ボタン生成を1行で呼び出せるようにする。

### 実装案

**ファイル:** `Assets/Script/SelectRangeButtons.cs`

```csharp
/// <summary>
/// 範囲選択ボタンを生成する共通メソッド
/// </summary>
private Button CreateRangeButton(
    SkillZoneTrait trait,
    string text,
    ref float currentX,
    ref float currentY,
    bool isOption = false)
{
    var button = Instantiate(buttonPrefab, transform);
    var rect = button.GetComponent<RectTransform>();

    // 親オブジェクトの右端を超える場合は次の行に移動
    if (currentX + buttonSize.x / 2 > parentSize.x / 2)
    {
        currentX = isOption ? optionStartX : startX;
        currentY -= (buttonSize.y + verticalPadding);
    }

    rect.anchoredPosition = new Vector2(currentX, currentY);
    currentX += (buttonSize.x + horizontalPadding);

    if (isOption)
    {
        button.onClick.AddListener(() => OnClickOptionRangeBtn(button, trait));
    }
    else
    {
        button.onClick.AddListener(() => OnClickRangeBtn(button, trait));
    }

    button.GetComponentInChildren<TextMeshProUGUI>().text =
        text + AddPercentageTextOnButton(trait);
    buttonList.Add(button);

    return button;
}
```

### 使用例（リファクタリング後）

```csharp
// Before: 20行
if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))
{
    var button = Instantiate(buttonPrefab, transform);
    var rect = button.GetComponent<RectTransform>();
    // ... 省略（15行以上）
}

// After: 3行
if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))
{
    CreateRangeButton(SkillZoneTrait.CanSelectSingleTarget,
        "前のめりまたはそれ以外のどちらかを狙う", ref currentX, ref currentY);
}
```

### 影響範囲

- `SelectRangeButtons.cs` - OnCreated()全体
- `SelectTargetButtons.cs` - 同様のパターンがあれば適用

### 削減効果

約200行 → 約50行（150行削減）

### ⚠️ buttonList の初期化・クリア方針

**問題**: ヘルパー抽出時にbuttonListの管理を明確にしないと、再生成時の二重Destroyや参照残りが発生する。

**実装方針**:

1. **OnCreated()冒頭でリスト初期化とクリア**
```csharp
public void OnCreated()
{
    // 既存ボタンをクリア（再生成対策）
    ClearAllButtons();

    // リスト初期化
    if (buttonList == null)
        buttonList = new List<Button>();

    // ... ボタン生成処理
}

private void ClearAllButtons()
{
    if (buttonList != null)
    {
        foreach (var button in buttonList)
        {
            if (button != null)
                Destroy(button.gameObject);
        }
        buttonList.Clear();
    }
}
```

2. **OnDestroy()でのクリーンアップ**
```csharp
private void OnDestroy()
{
    ClearAllButtons();
}
```

3. **SelectTargetButtonsも同様にAllybuttonList/EnemybuttonListのクリア処理を統一**

---

## 提案2: DetermineNextUIStateのTargetingPlan統合

### 優先度: 高

### 問題点

`AllyClass.DetermineNextUIState()`と`TargetingPlan.ToTabState()`が同じ判定ロジックを持っている。

**AllyClass.DetermineNextUIState (現状):**
```csharp
public static TabState DetermineNextUIState(BaseSkill skill)
{
    if (skill.HasZoneTrait(SkillZoneTrait.CanSelectRange)
        && !skill.HasZoneTrait(SkillZoneTrait.SelfSkill))
        return TabState.SelectRange;

    if ((skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget) ||
         skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget) ||
         skill.HasZoneTrait(SkillZoneTrait.CanSelectMultiTarget))
        && !skill.HasZoneTrait(SkillZoneTrait.SelfSkill))
        return TabState.SelectTarget;

    // ...
}
```

**TargetingPlan.ToTabState (実装済み):**
```csharp
public TabState ToTabState()
{
    if (ShowRangeSelection) return TabState.SelectRange;
    if (ShowTargetSelection) return TabState.SelectTarget;
    return TabState.NextWait;
}
```

### 解決策

`DetermineNextUIState`を`TargetingPlan`に委譲する。

### 実装案

**ファイル:** `Assets/Script/Players/Runtime/AllyClass.cs`

```csharp
/// <summary>
/// スキルの性質に基づいて、次に遷移すべき画面状態を判定する
/// </summary>
public static TabState DetermineNextUIState(BaseSkill skill)
{
    if (skill == null) return TabState.NextWait;

    var plan = TargetingPlan.FromSkill(skill);
    return plan.ToTabState();
}
```

### 影響範囲

- `AllyClass.cs:497-522` - メソッド本体の置き換え
- 呼び出し元は変更不要（シグネチャ維持）

### メリット

- 判定ロジックの一元化
- TargetingPlanとの整合性保証
- 将来の性質追加時の変更箇所が1箇所に

---

## 提案3: RangeWill初期化の集約

### 優先度: 中

### 問題点

RangeWillの初期化（正規化）が複数箇所に分散している。

| ファイル | 行 | 条件 | 処理 |
|----------|-----|------|------|
| `BattleOrchestrator.ApplySkillSelect` | 173-178 | !CanSelectRange | 正規化して代入 |
| `SelectTargetButtons.OnCreated` | 82-87 | 旧UI && !CanSelectRange | 正規化して代入 |
| `BattleManager.SkillACT` | 1076-1079 | RangeWill == 0 | 正規化して代入 |

### 解決策

旧UI経路を削除し、BattleOrchestrator経由を標準化することで、初期化箇所を削減。

### 実装手順

1. 提案4（旧UI経路削除）を先に実施
2. SelectTargetButtons.OnCreated内の初期化コードを削除
3. BattleManager.SkillACT内の初期化はフォールバックとして残す

### 影響範囲

- `SelectTargetButtons.cs:82-87` - 削除
- `BattleOrchestrator.cs` - 変更なし（正）
- `BattleManager.cs` - 変更なし（フォールバック）

---

## 提案4: 旧UI経路の削除

### 優先度: 高

### 問題点

`BattleOrchestratorHub.Current == null`のフォールバック経路が複数ファイルに残存している。

**該当箇所:**

| ファイル | 行 | 内容 |
|----------|-----|------|
| `AllyClass.OnSkillBtnCallBack` | 322-338 | 旧経路でSKillUseCall直接呼び出し |
| `AllyClass.OnSkillStockBtnCallBack` | 369-393 | 旧経路でストック処理直接実行 |
| `SelectTargetButtons.OnCreated` | 82-87 | 旧経路でRangeWill直接設定 |
| `SelectRangeButtons.OnClickRangeBtn` | 旧経路分岐あり | 要確認 |

### 解決策

旧UI経路を完全に削除し、BattleOrchestrator経由に一本化する。

### 前提条件

- BattleOrchestratorHubが常に存在することを保証
- 全てのバトル開始時にBattleOrchestratorが初期化されていること

### ⚠️ 生成保証・フェイルセーフの実装手順

旧UI経路削除後、`BattleOrchestratorHub.Current`が未初期化だと入力が全て無視されるため、以下の対策を実装する。

#### 1. 起動時アサート（開発時検出）

**ファイル:** `Assets/Script/Battle/BattleInitializer.cs`

```csharp
public static BattleSetupResult Initialize(...)
{
    // ... 初期化処理

    BattleOrchestratorHub.Set(result.Orchestrator);

    // 開発時アサート
    Debug.Assert(BattleOrchestratorHub.Current != null,
        "BattleOrchestrator initialization failed");

    return result;
}
```

#### 2. UI側のフェイルセーフ（ユーザー向けエラー表示）

**ファイル:** `Assets/Script/Players/Runtime/AllyClass.cs`

```csharp
public void OnSkillBtnCallBack(int skillListIndex)
{
    var orchestrator = BattleOrchestratorHub.Current;
    if (orchestrator == null)
    {
        Debug.LogError("[CRITICAL] BattleOrchestrator is not initialized. " +
            "This should never happen during battle.");
        // オプション: UIにエラー表示、またはバトル強制終了
        return;
    }
    // ...
}
```

#### 3. 生成保証の確認ポイント

| チェック箇所 | ファイル | 確認内容 |
|-------------|----------|---------|
| バトル開始 | `BattleInitializer.cs:88` | `BattleOrchestratorHub.Set()` が呼ばれること |
| バトル終了 | `BattleOrchestrator.cs:77` | `BattleOrchestratorHub.Clear()` が呼ばれること |
| 歩行→バトル遷移 | `UnityBattleRunner.cs` | 初期化フローが正しいこと |

#### 4. テストケース追加

```csharp
[Test]
public void BattleInitializer_ShouldSetOrchestratorHub()
{
    // Arrange
    BattleOrchestratorHub.Clear(null);

    // Act
    var result = BattleInitializer.Initialize(...);

    // Assert
    Assert.IsNotNull(BattleOrchestratorHub.Current);
}
```

### 実装案

**ファイル:** `Assets/Script/Players/Runtime/AllyClass.cs`

```csharp
// Before
public void OnSkillBtnCallBack(int skillListIndex)
{
    var skill = SkillList[skillListIndex];
    var orchestrator = BattleOrchestratorHub.Current;
    if (orchestrator != null)
    {
        // 新UI経路
        var input = new ActionInput { ... };
        var state = orchestrator.ApplyInput(input);
        // ...
        return;
    }

    // 旧UI経路（削除対象）
    SKillUseCall(skill);
    var nextState = manager.Acts.GetAtSingleTarget(0) != null
        ? TabState.NextWait
        : DetermineNextUIState(NowUseSkill);
    // ...
}

// After
public void OnSkillBtnCallBack(int skillListIndex)
{
    var skill = SkillList[skillListIndex];
    var orchestrator = BattleOrchestratorHub.Current;

    if (orchestrator == null)
    {
        Debug.LogError("BattleOrchestrator is not initialized");
        return;
    }

    var input = new ActionInput
    {
        Kind = ActionInputKind.SkillSelect,
        RequestId = orchestrator.CurrentChoiceRequest.RequestId,
        Actor = this,
        Skill = skill
    };
    var state = orchestrator.ApplyInput(input);

    var uiBridge = BattleUIBridge.Active;
    if (uiBridge != null)
    {
        uiBridge.SetUserUiState(state, false);
    }
}
```

### 影響範囲

- `AllyClass.cs` - OnSkillBtnCallBack, OnSkillStockBtnCallBack
- `SelectTargetButtons.cs` - OnCreated内の旧経路分岐
- `SelectRangeButtons.cs` - OnClickRangeBtn内の旧経路分岐（要確認）

### 削除対象コード量

約100行

---

## 提案5: SelectTargetButtonsのボタン生成ヘルパー

### 優先度: 中

### 問題点

SelectRangeButtonsと同様に、SelectTargetButtonsでもボタン生成パターンが繰り返されている。

### 解決策

提案1と同様のヘルパーメソッドを作成。

### 実装案

```csharp
private Button CreateTargetButton(
    BaseStates target,
    string text,
    DirectedWill will,
    allyOrEnemy faction,
    ref float currentX,
    ref float currentY)
{
    var button = Instantiate(buttonPrefab, transform);
    // ... 共通処理
    button.onClick.AddListener(() => OnClickSelectTarget(target, button, faction, will));
    return button;
}
```

---

## 実装優先順位

| 順位 | 提案 | 効果 | 難易度 | 依存関係 |
|------|------|------|--------|----------|
| 1 | 提案4: 旧UI経路削除 | 複雑度低減 | 中 | なし |
| 2 | 提案2: DetermineNextUIState統合 | 重複解消 | 低 | なし |
| 3 | 提案1: SelectRangeButtonsヘルパー | コード量削減 | 低 | なし |
| 4 | 提案3: RangeWill初期化集約 | バグリスク低減 | 低 | 提案4 |
| 5 | 提案5: SelectTargetButtonsヘルパー | コード量削減 | 低 | なし |

---

## 関連ファイル一覧

| ファイル | 役割 |
|----------|------|
| `Assets/Script/Players/Runtime/AllyClass.cs` | スキルボタンハンドラ、UI遷移判定 |
| `Assets/Script/Battle/UI/BattleOrchestrator.cs` | 入力処理の中継 |
| `Assets/Script/SelectRangeButtons.cs` | 範囲選択UI |
| `Assets/Script/SelectTargetButtons.cs` | 対象選択UI |
| `Assets/Script/Battle/TargetingPlan.cs` | 範囲性質の解釈結果 |
| `Assets/Script/BattleManager.cs` | スキル実行 |

---

## テスト計画

各提案のリファクタリング後、仕様が保たれていることを確認するためのテスト。

### 前提: テストファイル構成

**ファイル:** `Assets/Tests/EditMode/SkillUIFlowTests.cs`

```csharp
// Assembly-CSharpへの参照問題のため、有効化フラグでガード
#if ENABLE_SKILL_UI_FLOW_TESTS
using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class SkillUIFlowTests
{
    // テストケースはここに配置
}
#endif
```

---

### 提案2用テスト: DetermineNextUIState整合性

**目的**: リファクタリング後もDetermineNextUIStateが同じ結果を返すこと

```csharp
#region DetermineNextUIState整合性テスト

[Test]
public void DetermineNextUIState_CanSelectRange_ShouldReturnSelectRange()
{
    // Arrange: CanSelectRangeを持つスキル（SelfSkillなし）
    var skill = CreateMockSkill(SkillZoneTrait.CanSelectRange | SkillZoneTrait.CanSelectSingleTarget);

    // Act
    var result = AllyClass.DetermineNextUIState(skill);

    // Assert
    Assert.AreEqual(TabState.SelectRange, result);
}

[Test]
public void DetermineNextUIState_CanSelectRange_WithSelfSkill_ShouldReturnNextWait()
{
    // Arrange: SelfSkillがある場合は範囲選択をスキップ
    var skill = CreateMockSkill(SkillZoneTrait.CanSelectRange | SkillZoneTrait.SelfSkill);

    // Act
    var result = AllyClass.DetermineNextUIState(skill);

    // Assert
    Assert.AreEqual(TabState.NextWait, result);
}

[Test]
public void DetermineNextUIState_CanPerfectSelectSingleTarget_ShouldReturnSelectTarget()
{
    var skill = CreateMockSkill(SkillZoneTrait.CanPerfectSelectSingleTarget);

    var result = AllyClass.DetermineNextUIState(skill);

    Assert.AreEqual(TabState.SelectTarget, result);
}

[Test]
public void DetermineNextUIState_CanSelectSingleTarget_ShouldReturnSelectTarget()
{
    var skill = CreateMockSkill(SkillZoneTrait.CanSelectSingleTarget);

    var result = AllyClass.DetermineNextUIState(skill);

    Assert.AreEqual(TabState.SelectTarget, result);
}

[Test]
public void DetermineNextUIState_CanSelectMultiTarget_ShouldReturnSelectTarget()
{
    var skill = CreateMockSkill(SkillZoneTrait.CanSelectMultiTarget);

    var result = AllyClass.DetermineNextUIState(skill);

    Assert.AreEqual(TabState.SelectTarget, result);
}

[Test]
public void DetermineNextUIState_AllTarget_ShouldReturnNextWait()
{
    // AllTargetは対象選択不要
    var skill = CreateMockSkill(SkillZoneTrait.AllTarget);

    var result = AllyClass.DetermineNextUIState(skill);

    Assert.AreEqual(TabState.NextWait, result);
}

[Test]
public void DetermineNextUIState_ControlByThisSituation_ShouldReturnNextWait()
{
    var skill = CreateMockSkill(SkillZoneTrait.ControlByThisSituation);

    var result = AllyClass.DetermineNextUIState(skill);

    Assert.AreEqual(TabState.NextWait, result);
}

[Test]
public void DetermineNextUIState_NullSkill_ShouldReturnNextWait()
{
    var result = AllyClass.DetermineNextUIState(null);

    Assert.AreEqual(TabState.NextWait, result);
}

// TargetingPlanとの整合性を確認
[Test]
[TestCase(SkillZoneTrait.CanSelectRange)]
[TestCase(SkillZoneTrait.CanSelectSingleTarget)]
[TestCase(SkillZoneTrait.CanPerfectSelectSingleTarget)]
[TestCase(SkillZoneTrait.CanSelectMultiTarget)]
[TestCase(SkillZoneTrait.AllTarget)]
[TestCase(SkillZoneTrait.SelfSkill)]
public void DetermineNextUIState_ShouldMatchTargetingPlanToTabState(SkillZoneTrait trait)
{
    var skill = CreateMockSkill(trait);

    var determineResult = AllyClass.DetermineNextUIState(skill);
    var planResult = TargetingPlan.FromSkill(skill).ToTabState();

    Assert.AreEqual(planResult, determineResult,
        $"Trait {trait}: DetermineNextUIState={determineResult}, TargetingPlan.ToTabState={planResult}");
}

#endregion
```

---

### 提案4用テスト: BattleOrchestrator初期化保証

**目的**: バトル開始時にBattleOrchestratorHubが確実に初期化されること

```csharp
#region BattleOrchestrator初期化テスト

[Test]
public void BattleInitializer_ShouldSetOrchestratorHub()
{
    // Arrange
    BattleOrchestratorHub.Clear(null);
    Assert.IsNull(BattleOrchestratorHub.Current, "Pre-condition: Hub should be null");

    // Act
    var result = BattleInitializer.Initialize(/* テスト用パラメータ */);

    // Assert
    Assert.IsNotNull(BattleOrchestratorHub.Current,
        "BattleOrchestratorHub.Current should be set after Initialize");
}

[Test]
public void BattleOrchestrator_OnDispose_ShouldClearHub()
{
    // Arrange
    var orchestrator = new BattleOrchestrator(/* テスト用パラメータ */);
    BattleOrchestratorHub.Set(orchestrator);

    // Act
    orchestrator.Dispose();

    // Assert
    Assert.IsNull(BattleOrchestratorHub.Current,
        "BattleOrchestratorHub.Current should be null after Dispose");
}

#endregion
```

---

### 提案3用テスト: RangeWill正規化の一貫性

**目的**: どの経路を通ってもRangeWillが同じ値に正規化されること

```csharp
#region RangeWill正規化テスト

[Test]
[TestCase(SkillZoneTrait.CanSelectSingleTarget | SkillZoneTrait.CanSelectAlly)]
[TestCase(SkillZoneTrait.AllTarget)]
[TestCase(SkillZoneTrait.CanPerfectSelectSingleTarget)]
[TestCase(SkillZoneTrait.RandomMultiTarget)]
public void RangeWillNormalization_ShouldProduceSameResult(SkillZoneTrait input)
{
    // Arrange: CanSelectRangeがないスキル（自動正規化が走る）
    var skill = CreateMockSkill(input);
    var expected = SkillZoneTraitNormalizer.Normalize(input);

    // Act: ApplySkillSelect経由
    var orchestrator = CreateTestOrchestrator();
    var actor = CreateTestActor();
    actor.RangeWill = 0;

    var actionInput = new ActionInput
    {
        Kind = ActionInputKind.SkillSelect,
        Actor = actor,
        Skill = skill
    };
    orchestrator.ApplyInput(actionInput);

    // Assert
    Assert.AreEqual(expected, actor.RangeWill,
        $"Input: {input}, Expected: {expected}, Actual: {actor.RangeWill}");
}

[Test]
public void RangeWillNormalization_SelfSkill_ShouldBeOnlySelfSkill()
{
    // SelfSkillは他の性質を全て除去
    var input = SkillZoneTrait.SelfSkill | SkillZoneTrait.CanSelectAlly;
    var expected = SkillZoneTrait.SelfSkill;

    var result = SkillZoneTraitNormalizer.Normalize(input);

    Assert.AreEqual(expected, result);
}

#endregion
```

---

### 回帰テスト: UIフロー全体

**目的**: リファクタリング後もUIフローが正しく動作すること（統合テスト）

```csharp
#region UIフロー回帰テスト（PlayMode推奨）

/// <summary>
/// スキル選択 → 範囲選択 → 対象選択 の完全フローテスト
/// </summary>
[Test]
public void SkillFlow_SelectRange_SelectTarget_ShouldComplete()
{
    // Arrange
    var orchestrator = CreateTestOrchestrator();
    var actor = CreateTestActor();
    var skill = CreateMockSkill(
        SkillZoneTrait.CanSelectRange |
        SkillZoneTrait.CanSelectSingleTarget);

    // Act 1: スキル選択
    var state1 = orchestrator.ApplyInput(new ActionInput
    {
        Kind = ActionInputKind.SkillSelect,
        Actor = actor,
        Skill = skill
    });

    // Assert 1: 範囲選択画面へ
    Assert.AreEqual(TabState.SelectRange, state1);

    // Act 2: 範囲選択
    var state2 = orchestrator.ApplyInput(new ActionInput
    {
        Kind = ActionInputKind.RangeSelect,
        Actor = actor,
        RangeWill = SkillZoneTrait.CanSelectSingleTarget
    });

    // Assert 2: 対象選択画面へ
    Assert.AreEqual(TabState.SelectTarget, state2);

    // Act 3: 対象選択
    var target = CreateTestEnemy();
    var state3 = orchestrator.ApplyInput(new ActionInput
    {
        Kind = ActionInputKind.TargetSelect,
        Actor = actor,
        Targets = new List<BaseStates> { target }
    });

    // Assert 3: 実行待機へ
    Assert.AreEqual(TabState.NextWait, state3);
}

/// <summary>
/// SelfSkillは範囲・対象選択をスキップすること
/// </summary>
[Test]
public void SkillFlow_SelfSkill_ShouldSkipSelections()
{
    var orchestrator = CreateTestOrchestrator();
    var actor = CreateTestActor();
    var skill = CreateMockSkill(SkillZoneTrait.SelfSkill);

    var state = orchestrator.ApplyInput(new ActionInput
    {
        Kind = ActionInputKind.SkillSelect,
        Actor = actor,
        Skill = skill
    });

    // SelfSkillは即座にNextWait
    Assert.AreEqual(TabState.NextWait, state);
    Assert.AreEqual(SkillZoneTrait.SelfSkill, actor.RangeWill);
}

/// <summary>
/// AllTargetは対象選択をスキップすること
/// </summary>
[Test]
public void SkillFlow_AllTarget_ShouldSkipTargetSelection()
{
    var orchestrator = CreateTestOrchestrator();
    var actor = CreateTestActor();
    var skill = CreateMockSkill(
        SkillZoneTrait.CanSelectRange |
        SkillZoneTrait.AllTarget);

    // スキル選択 → 範囲選択画面
    orchestrator.ApplyInput(new ActionInput
    {
        Kind = ActionInputKind.SkillSelect,
        Actor = actor,
        Skill = skill
    });

    // 範囲でAllTargetを選択
    var state = orchestrator.ApplyInput(new ActionInput
    {
        Kind = ActionInputKind.RangeSelect,
        Actor = actor,
        RangeWill = SkillZoneTrait.AllTarget
    });

    // AllTargetは対象選択をスキップしてNextWait
    Assert.AreEqual(TabState.NextWait, state);
}

#endregion
```

---

### テストヘルパー

```csharp
#region テストヘルパー

private static BaseSkill CreateMockSkill(SkillZoneTrait traits)
{
    // テスト用のモックスキル作成
    // 実装はプロジェクトの構造に依存
    var skill = ScriptableObject.CreateInstance<BaseSkill>();
    skill.zoneTrait = traits;
    return skill;
}

private static BattleOrchestrator CreateTestOrchestrator()
{
    // テスト用オーケストレーター作成
    // 必要な依存関係をモック化
}

private static AllyClass CreateTestActor()
{
    // テスト用アクター作成
}

private static BaseStates CreateTestEnemy()
{
    // テスト用敵キャラ作成
}

#endregion
```

---

### テスト実行方法

```bash
# EditModeテスト実行（Unity Test Framework）
# Unity Editor内から: Window > General > Test Runner > EditMode

# MCP経由で実行
run_tests(mode="EditMode", test_names=["SkillUIFlowTests"])
```

---

### テスト優先順位

| 優先度 | テスト | 対象提案 | 種類 |
|--------|--------|----------|------|
| 高 | DetermineNextUIState整合性 | 提案2 | EditMode |
| 高 | BattleOrchestrator初期化 | 提案4 | EditMode |
| 中 | RangeWill正規化一貫性 | 提案3 | EditMode |
| 中 | UIフロー回帰テスト | 全体 | PlayMode推奨 |
| 低 | ボタン位置計算 | 提案1,5 | EditMode（ロジック分離後） |

---

### ⚠️ テスト実装時の注意

1. **Assembly-CSharp参照問題**: `Assets/Tests/EditMode`のasmdefでAssembly-CSharpを参照できない場合、`#if ENABLE_SKILL_UI_FLOW_TESTS`でガードする

2. **モック作成**: MonoBehaviour系のテストは依存関係が多いため、ロジック部分をPOCOに切り出すことを検討

3. **PlayModeテスト**: UIフローの完全な統合テストはPlayModeで実行する必要がある

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-16 | 初版作成 |
| 2026-01-16 | 外部レビュー反映: [P2] 提案4に生成保証・フェイルセーフの実装手順を追加、[P3] 提案1にbuttonListの初期化・クリア方針を追加 |
| 2026-01-16 | テスト計画セクション追加: DetermineNextUIState整合性、BattleOrchestrator初期化、RangeWill正規化、UIフロー回帰テスト |
| 2026-01-17 | **全提案の実装完了によりクローズ** |
