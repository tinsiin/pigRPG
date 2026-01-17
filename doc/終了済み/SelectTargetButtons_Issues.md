# SelectTargetButtons 潜在的問題点

**ステータス: ✅ クローズ（2026-01-17）**

## 概要

SelectTargetButtons の対象選択フローにおいて、以下の問題点を調査・解決した。

## 解決サマリー

| 問題 | 状態 | 対応 |
|------|------|------|
| 問題1: 敵1人スキップ時のunders空 | ✅ 解決済み | CashUnders追加 + DirectedWill.One設定 |
| 問題2: 複数選択途中終了 | ✅ 発生しない | 終了条件がCount < 1のため |
| 問題3: CanSelect* + CanSelectAlly | 🔶 設計上の制限 | ドキュメント化 |

## 問題1: 対象が1人しかいない場合のスキップ処理 【解決済み】

### 現象（修正前）

CanPerfectSelectSingleTarget で対象が1人しかいない場合、SelectTarget画面をスキップして BattleManager に処理が移るが、`unders`（対象者リスト）に何も追加されない。

### コードフロー

```
SelectTargetButtons.OnCreated()
  ↓
selects.Count < 2 && !AllyTargeting && !MySelfTargeting  (行294)
  ↓ YES
ReturnNextWaitView() を直接呼ぶ（ボタンを作らない）
  ↓
CashUnders は空のまま → ActionInput.Targets も空
  ↓
BattleOrchestrator.ApplyTargetSelect()
  ↓
unders.CharaAdd() は呼ばれない（Targets が空なので）
  ↓
BattleManager.DoSkillAction()
  ↓
TargetingService.SelectTargets()
  ↓
unders.Count < 1 なので、ua で計算した対象を unders.SetList(ua) で上書き
```

### 影響

CanPerfectSelectSingleTarget の特権である「UIで選択した対象が `unders` に残り、RandomRange で追加攻撃が可能」という機能が、対象が1人しかいない場合に無効化される。

### 関連ファイル

- `SelectTargetButtons.cs:294-297`
- `BattleOrchestrator.cs:ApplyTargetSelect()`
- `TargetingService.cs:303-307`

### 対策

スキップ時に、唯一の対象を `CashUnders` に追加し、`selectedTargetWill = DirectedWill.One` を設定してから `ReturnNextWaitView()` を呼ぶ。

**修正箇所**: `SelectTargetButtons.cs` の `EnemyTargeting` スキップ処理（行294付近）

**修正前**:
```csharp
if (selects.Count < 2 && !AllyTargeting && !MySelfTargeting)
{
    ReturnNextWaitView();
}
```

**修正後**:
```csharp
if (selects.Count < 2 && !AllyTargeting && !MySelfTargeting)
{
    // 対象が1人でもUI選択と同様の結果になるよう、リストに追加してから終了
    // これにより CanPerfectSelectSingleTarget + RandomRange の特権（選択済み対象が残る）が維持される
    if (selects.Count == 1)
    {
        CashUnders.Add(selects[0]);
        selectedTargetWill = DirectedWill.One;
    }
    ReturnNextWaitView();
}
```

**コメント文の意図**:
- 「1人でもUI選択と同様の結果になるようリストに入れる」という文脈を明示
- CanPerfectSelectSingleTarget の特権（RandomRange 後も選択済み対象が `unders` に残る）を維持するため

### 解決理由

修正により以下が実現される：

1. **`selectedTargetWill = DirectedWill.One`** を設定
   - `ApplyTargetSelect()` で `actor.Target = One` (= 2) になる
   - `DetermineRangeRandomly()` の条件 `Acter.Target != 0` を満たす
   - 35%でランダム範囲上書き、65%で既存選択維持のロジックに入る

2. **`CashUnders.Add(selects[0])`** で対象を追加
   - `ApplyTargetSelect()` で `unders.CharaAdd()` される
   - `DetermineRangeRandomly()` では **unders は初期化されない**（行1175のコメント参照）
   - RandomRange が発動しても選択した対象が維持される

これにより、敵が1人でも「UIで選択した」扱いになり、CanPerfectSelectSingleTarget + RandomRange の特権が維持される。

---

## 問題2: 複数選択可能なのに選択途中で終了 【発生しない】

### 現象（当初の懸念）

`NeedSelectCountAlly > 1` または `NeedSelectCountEnemy > 1` の場合、複数人を選択できるはずだが、ボタンが1つしか残らなくなると強制終了し、残りの対象を選択できない。

### 発生しない理由

終了条件は `Count < 1`（ボタンが0個）なので、**ボタンが1個残っていればまだ選択可能**。

```csharp
// 終了条件（行462）
if (AllybuttonList.Count < 1 || NeedSelectCountAlly <= 0)
{
    ReturnNextWaitView();
}
```

**例: NeedSelectCountAlly = 2、味方2人の場合**

| 操作 | Count | NeedSelectCount | 終了? |
|------|-------|-----------------|-------|
| 初期 | 2 | 2 | - |
| 1人目選択後 | 1 | 1 | Count<1? No, <=0? No → **継続** |
| 2人目選択後 | 0 | 0 | Count<1? Yes → **終了** |

1個残っていれば選べるため、問題は発生しない。

### 関連ファイル

- `SelectTargetButtons.cs:460-470` (味方)
- `SelectTargetButtons.cs:472-482` (敵)

---

## NeedSelectCount > 1 発生パターン分析

### NeedSelectCountEnemy

| 条件 | 値 |
|------|-----|
| `CanPerfectSelectSingleTarget` | 1 |
| それ以外 | 0 (設定されない) |

**結論: `NeedSelectCountEnemy > 1` は現在のコードでは発生しない**

### NeedSelectCountAlly

| 条件 | 値 | 発生確率 |
|------|-----|---------|
| `SelectOnlyAlly + CanPerfectSelectSingleTarget` | 1 | - |
| `CanPerfectSelectSingleTarget + CanSelectAlly` | 1 | - |
| `CanPerfectSelectSingleTarget + CanSelectMyself` | 1 | - |
| **`CanSelectSingleTarget + CanSelectAlly`** | **1 or 2** | **50%で2** |
| `CanSelectSingleTarget + CanSelectMyself` | 1 | - |
| **`CanSelectMultiTarget + CanSelectAlly`** | **2** | **常に2** |
| `CanSelectMultiTarget + CanSelectMyself` | 1 | - |

**結論: `NeedSelectCountAlly > 1` が発生するケースは2パターン:**

1. **`CanSelectSingleTarget + CanSelectAlly`**
   - 敵を「前のめり/後衛」で選択 + 味方も選べる
   - `NeedSelectCountAlly = Random.Range(1, 3)` → 1 or 2
   - 50%の確率で2人選択が必要

2. **`CanSelectMultiTarget + CanSelectAlly`**
   - 敵を「前のめり/後衛（範囲）」で選択 + 味方も選べる
   - `NeedSelectCountAlly = 2` → 常に2人選択

### 実際の使用状況

コードベース検索結果:
- `CanSelectSingleTarget + CanSelectAlly`: テストファイルにのみ存在
- `CanSelectMultiTarget + CanSelectAlly`: コード内に存在しない

**注意**: ScriptableObject（.asset）ファイルは検索対象外のため、実際のスキルデータで使われている可能性は排除できない。

### 問題の影響範囲

| パターン | 現在使用中？ | 問題2の影響 |
|----------|-------------|------------|
| `CanSelectSingleTarget + CanSelectAlly` | テストのみ | 50%で発生 |
| `CanSelectMultiTarget + CanSelectAlly` | 不明 | 常に発生 |

---

## 問題3: CanSelectSingleTarget/MultiTarget + CanSelectAlly の致命的問題

### 現象

`CanSelectSingleTarget + CanSelectAlly` または `CanSelectMultiTarget + CanSelectAlly` の組み合わせで、**味方への行動が実行されない**。

### 原因1: SelectTargetButtons の即時終了

```csharp
// OnClickSelectVanguardOrBacklines (行409-413)
void OnClickSelectVanguardOrBacklines(Button thisBtn, DirectedWill will)
{
    selectedTargetWill = will;
    ReturnNextWaitView();  // 即座に終了！味方選択の機会なし
}
```

敵の「前のめり」「後衛」ボタンを押すと、味方ボタンが存在していても即座に終了。

### 原因2: TargetingService で ourGroup が追加されない

```
ourGroup（味方）が ua に追加される条件:

| RangeWill                  | ourGroup 追加 |
|----------------------------|---------------|
| RandomSingleTarget         | ✅ される      |
| ControlByThisSituation     | ✅ される      |
| RandomMultiTarget          | ✅ される      |
| AllTarget                  | ✅ される      |
| CanSelectSingleTarget      | ❌ されない    |
| CanSelectMultiTarget       | ❌ されない    |
```

### 結論

`CanSelectSingleTarget/MultiTarget + CanSelectAlly` で：

1. **UI段階**: 敵を先に選ぶと味方選択の機会がない
2. **BattleManager段階**: TargetingService は味方を追加しない
3. **結果**: **2人目（味方）への行動は担保されない**

### 関連ファイル

- `SelectTargetButtons.cs:409-413` (OnClickSelectVanguardOrBacklines)
- `TargetingService.cs:111-145` (CanSelectSingleTarget 処理)
- `TargetingService.cs:210-245` (CanSelectMultiTarget 処理)

---

## 検証が必要な点

1. ~~現在の SkillZoneTrait の組み合わせで `NeedSelectCount > 1` が発生するケースはあるか？~~ → **上記分析で2パターン特定**
2. 対象が1人しかいない場合のスキップは意図的な仕様か？
3. CanPerfectSelectSingleTarget + RandomRange の組み合わせで、対象1人の場合の期待動作は？
4. `CanSelectSingleTarget/MultiTarget + CanSelectAlly` の組み合わせは実際のスキルで使われているか？（.assetファイル要確認）

---

## 関連メモリ

- `.claude/skills/agent-memory/memories/battle-system/skill-zone-trait-architecture.md`
- `.claude/skills/agent-memory/memories/battle-system/skill-ui-flow.md`

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-16 | 初版作成 |
| 2026-01-17 | 問題1解決、問題2は発生しないことを確認、**クローズ** |
