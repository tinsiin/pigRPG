# ストック・トリガー用AIユーティリティ実装計画

## 背景

ストック（Stockpile）とトリガー（発動カウント）はBM側の仕組みは存在するが、敵AIから利用するためのユーティリティがない。
「いつ溜めるか」「いつ撃つか」の判断ロジック自体は派生AIのPlan()内で手書きする領域であり、汎用部品化の価値は薄い。
ただし、**情報取得のヘルパー**がないと派生AIを書く際に毎回低レベルな処理を書く必要がありめんどくさい。
ストックの操作（コミット）はBMリファクタリング（`doc/リファクタリング_ストックBM設計統一.md`）により、AIDecision.IsStock + CommitDecision経由で行う設計に統一済み。

---

## 追加先

- ユーティリティ関数: `BattleAIBrain.cs`（基底クラス）に protected メソッドとして追加
- BaseSkill側の公開が必要なもの: `BaseSkill.Trigger.cs` にgetter追加

---

## ストック用ユーティリティ（2関数 ※操作系は削除済み）

### 1. GetStockpileSkills

Stockpileフラグを持つスキルを使用可能スキルから列挙する。

```
protected IEnumerable<BaseSkill> GetStockpileSkills(IEnumerable<BaseSkill> skills)
```

- `HasConsecutiveType(SkillConsecutiveType.Stockpile)` でフィルタ
- 引数は `MustSkillSelect()` の結果や `user.SkillList` を想定

**ないとめんどくさい理由:** 毎回 `HasConsecutiveType(Stockpile)` でLINQフィルタを手書きすることになる。

### 2. GetStockInfo

指定スキルのストック状態を一括取得する。

```
protected StockInfo GetStockInfo(BaseSkill skill)

public struct StockInfo
{
    public int Current;        // 現在のストック数（_nowStockCount）
    public int Max;            // 最大値（DefaultAtkCount）
    public int Default;        // デフォルト値（DefaultStockCount）
    public bool IsFull;        // 満杯か（IsFullStock()）
    public int StockPower;     // 1回のストックで増える量（GetStcokPower()）
    public int TurnsToFull;    // 満杯まであと何回ストックが必要か
    public float FillRate;     // 充填率（Current / Max）
}
```

**ないとめんどくさい理由:** `_nowStockCount` はpublicだが、最大値(`DefaultAtkCount`)やストック増加量(`GetStcokPower()`)はスキルレベルデータ経由で、派生AIから「あと何回で満杯か」を計算するのに複数プロパティを組み合わせる必要がある。

### ~~3. CommitStock~~ → 削除（AIDecision方式に統一）

レビューにより、CommitStockユーティリティは**不要**と判断。

**理由:**
- Plan()内でmanagerに直接副作用を起こすのは、Plan()の「結果をバッファ(AIDecision)に書くだけ、副作用なし」原則に反する
- CommitDecisionのパイプラインを迂回するため、将来ログ出力や検証を追加した際にストック行動だけ漏れる

**代替:** BMリファクタリング計画（`doc/リファクタリング_ストックBM設計統一.md`）に従い、`AIDecision.IsStock`フラグ方式でCommitDecision経由でコミットする。派生AIのPlan()では:
```csharp
decision.Skill = stockSkill;
decision.IsStock = true;
```
とするだけ。ストックロジック（ATKCountStock, ForgetStock等）はBM側のSkillStockACTに集約済み。

---

## トリガー用ユーティリティ（2関数 + BaseSkill側1変更）

### 4. GetTriggerSkills

発動カウント付きスキルを列挙する。

```
protected IEnumerable<BaseSkill> GetTriggerSkills(IEnumerable<BaseSkill> skills)
```

- `TriggerCountMax > 0` でフィルタ

### 5. GetTriggerInfo

指定スキルのトリガー状態を一括取得する。

```
protected TriggerInfo GetTriggerInfo(BaseSkill skill)

public struct TriggerInfo
{
    public int CurrentCount;      // 現在のカウント（_triggerCount）
    public int MaxCount;          // 最大カウント（TriggerCountMax）
    public bool IsTriggering;     // カウント中か
    public int RemainingTurns;    // 発動まであと何ターンか（_triggerCount + 1。-1到達で発動のため）
    public int RollBackCount;     // 巻き戻し量（TriggerRollBackCount）
    public bool CanCancel;        // カウント中にキャンセル可能か（CanCancelTrigger）
}
```

**ないとめんどくさい理由:** `_triggerCount` がprivateなので残りターン数を外から取得できない。`IsTriggering` はあるが「あと何ターン」が分からないと判断材料にならない。

### 6. BaseSkill側の変更: _triggerCount のgetter追加

```csharp
// BaseSkill.Trigger.cs に追加
public int CurrentTriggerCount => _triggerCount;
```

`GetTriggerInfo` が `_triggerCount` を参照するために必要。BattleAIBrainはBaseSkillの内部フィールドに直接アクセスできないため。

---

## トリガーに操作系ユーティリティが不要な理由

トリガースキルは `NowUseSkill` にセットするだけでBM側が自動処理する:
- `CharacterActBranchingAsync` 内の `TrigerCount()` で自動的にカウントが減る
- カウントが -1 に達したら `TriggerAct` に入る
- AI側は「どのスキルを選ぶか」だけ決めればいい

ストックのように専用のコミット関数は不要。

---

## 派生AIでの使用イメージ

```csharp
protected override void Plan(AIDecision decision)
{
    // ストック判断
    var stockSkills = GetStockpileSkills(availableSkills);
    foreach (var s in stockSkills)
    {
        var info = GetStockInfo(s);
        if (!info.IsFull && /* キャラ固有の溜め判断 */)
        {
            decision.Skill = s;
            decision.IsStock = true;  // AIDecisionに書くだけ。コミットはCommitDecisionが行う
            return;
        }
        // 満杯なら通常スキルとして撃てる（ATKCountがストック数を返す）
    }

    // トリガー判断
    var triggerSkills = GetTriggerSkills(availableSkills);
    foreach (var t in triggerSkills)
    {
        var info = GetTriggerInfo(t);
        if (!info.IsTriggering && /* キャラ固有の溜め開始判断 */)
        {
            decision.Skill = t; // 選ぶだけでBMが自動処理
            return;
        }
    }

    // 通常のスキル選択
    decision.Skill = SelectSkill(availableSkills, potentialTargets);
}
```

---

## まとめ

| # | 関数名 | 種別 | 追加先 | 重要度 |
|---|---|---|---|---|
| 1 | GetStockpileSkills | 情報取得 | BattleAIBrain | 中 |
| 2 | GetStockInfo | 情報取得 | BattleAIBrain | 中 |
| ~~3~~ | ~~CommitStock~~ | ~~操作~~ | — | **削除**（AIDecision.IsStock方式に統一。BMリファクタリング計画参照） |
| 4 | GetTriggerSkills | 情報取得 | BattleAIBrain | 中 |
| 5 | GetTriggerInfo | 情報取得 | BattleAIBrain | 中 |
| 6 | CurrentTriggerCount | getter追加 | BaseSkill.Trigger.cs | 必須（5の前提） |

**前提:** BMリファクタリング（`doc/リファクタリング_ストックBM設計統一.md`）が先に完了していること。AIからのストック行動は、CommitDecisionの`IsStock`分岐 → BM側の`SkillStockACT`で処理される。
