# リファクタリング計画: ストックのBM設計をトリガーと統一

## 目的

ストック（Stockpile）のゲームロジックがUI入力処理に埋まっている現状を改善し、トリガー（発動カウント）と同じ「入力はスキル選択+フラグのみ → BM側でロジック実行」の構造に統一する。

これにより:
- ストックロジックが1箇所に集約される（現状3箇所に分散+コピペ重複）
- AIからも通常のCommitDecisionにフラグを足すだけでストック行動が可能になる
- トリガーとストックで一貫した設計になり、コードベースの理解コストが下がる

---

## 現状の構造（Before）

### ストックの処理フロー

```
【UI入口】
AllyClass.OnSkillStockBtnCallBack
  → ActionInput { Kind = StockSkill, Skill = skill }

【Orchestrator（UI用）】                    ← ★ロジックここ(1)
BattleOrchestrator.ApplyStockSkill:
  skill.ATKCountStock()
  他Stockpileスキル.ForgetStock()
  context.SkillStock = true

【Session（リプレイ用）】                    ← ★ロジックここ(2) コピペ重複
BattleSession.ApplyStockSkill:
  skill.ATKCountStock()
  他Stockpileスキル.ForgetStock()
  manager.SkillStock = true

【BM分岐】
CharacterActBranchingAsync:
  if (SkillStock)                            ← フラグ消化だけ
    → ActionSkipExecutor.SkillStockACT:
        前のめり判定(AggressiveOnStock)
        SkillStock = false
        NextTurn → ACTPop
```

### トリガーの処理フロー（参考: これが理想形）

```
【入力】
スキルをNowUseSkillにセットするだけ（通常のスキル選択と同じ）

【BM分岐】
CharacterActBranchingAsync:
  var count = skill.TrigerCount()            ← ★ロジックここ（1箇所のみ）
  if (count >= 0)
    → TriggerAct(count):
        FreezeSkill（キャンセル不可時）
        前のめり判定(AggressiveOnTrigger)
        他スキルのRollBack
        メッセージ表示
        NextTurn → SelectNextActor
```

### 問題点まとめ

| 問題 | 詳細 |
|---|---|
| ロジック分散 | ATKCountStock + ForgetStock が Orchestrator と Session の2箇所にコピペ |
| 責務の混在 | ゲームロジック（ストック数増減）がUI入力処理に埋まっている |
| AI非対応 | UI経由でしかストックできない。CommitDecisionにストック分岐がない |
| トリガーとの不一致 | 同じ「溜めて撃つ」系なのに設計パターンが異なる |

---

## リファクタリング後の構造（After）

### 方針

「入力時はスキル選択+ストックフラグの設定のみ。ストックロジックはBM側で実行する。」

### 新しいストック処理フロー

```
【UI入口】（変更あり）
AllyClass.OnSkillStockBtnCallBack
  → ActionInput { Kind = StockSkill, Skill = skill }

【Orchestrator（UI用）】（簡素化）
BattleOrchestrator.ApplyStockSkill:
  actor.NowUseSkill = skill                  ← スキルをセットするだけ
  context.SkillStock = true                  ← フラグを立てるだけ
  → TabState.NextWait

【Session（リプレイ用）】（簡素化）
BattleSession.ApplyStockSkill:
  actor.NowUseSkill = skill                  ← 同上
  manager.SkillStock = true
  → TabState.NextWait

【BM分岐】（ロジック集約）
CharacterActBranchingAsync:
  if (SkillStock)
    → SkillStockACT:                         ← ★ロジック全部ここ
        満杯チェック（IsFullStock）
        skill.ATKCountStock()
        他Stockpileスキル.ForgetStock()
        前のめり判定(AggressiveOnStock)
        ログ出力
        SkillStock = false
        NextTurn → ACTPop
```

---

## 変更対象ファイルと内容

### 1. BattleOrchestrator.cs — ApplyStockSkill 簡素化

**Before:**
```csharp
private TabState ApplyStockSkill(ActionInput input)
{
    var actor = input.Actor ?? Context.Acter;
    var skill = input.Skill;
    if (actor == null || skill == null) return CurrentUiState;
    if (skill.IsFullStock())
    {
        Debug.Log(skill.SkillName + "をストックが満杯。");
        return CurrentUiState;
    }
    skill.ATKCountStock();
    Debug.Log(skill.SkillName + "をストックしました。");
    var list = actor.SkillList
        .Where(item => !ReferenceEquals(item, skill) && item.HasConsecutiveType(SkillConsecutiveType.Stockpile))
        .ToList();
    foreach (var stockSkill in list) stockSkill.ForgetStock();
    Context.SkillStock = true;
    UpdateChoiceState(TabState.NextWait);
    return CurrentUiState;
}
```

**After:**
```csharp
private TabState ApplyStockSkill(ActionInput input)
{
    var actor = input.Actor ?? Context.Acter;
    var skill = input.Skill;
    if (actor == null || skill == null) return CurrentUiState;
    if (skill.IsFullStock())
    {
        Debug.Log(skill.SkillName + "をストックが満杯。");
        return CurrentUiState;
    }
    actor.NowUseSkill = skill;  // 直接代入（SKillUseCallは使わない。後述）
    Context.SkillStock = true;
    UpdateChoiceState(TabState.NextWait);
    return CurrentUiState;
}
```

**注意:** 満杯チェックはUI側に残す（満杯なのにNextWaitに進むのはUX的に不適切なため）。ただしBM側でも防御的にチェックする。

**重要: `SKillUseCall`を使ってはいけない理由（レビューで発覚）:**
`SKillUseCall`は`NowUseSkill`のセット以外に以下の副作用を持つ:
1. `TryConsumeForSkillAtomic` — **スキルポイントを消費する**。ストック行動はスキルを使わず溜めるだけなので、ポイント消費は不正
2. `CashMoveSet` — ムーブセットをキャッシュする。ストック段階でキャッシュするのは早すぎる
3. 他Stockpileスキルの`ForgetStock` — SkillStockACTでも実行するため**二重減算**になる

### 2. BattleSession.cs — ApplyStockSkill 簡素化

**Before:** Orchestratorとほぼ同じコピペコード

**After:**
```csharp
private TabState ApplyStockSkill(BaseStates actor, BaseSkill skill)
{
    if (actor == null || skill == null) return TabState.NextWait;
    if (skill.IsFullStock()) return TabState.NextWait;
    actor.NowUseSkill = skill;  // 直接代入
    _manager.SkillStock = true;
    return TabState.NextWait;
}
```

### 3. ActionSkipExecutor.cs — SkillStockACT にロジック集約

**Before:**
```csharp
public TabState SkillStockACT()
{
    var skill = _context.Acter?.NowUseSkill;
    if (skill != null && skill.AggressiveOnStock.isAggressiveCommit)
    {
        _context.BeVanguard(_context.Acter);
    }
    _context.SkillStock = false;
    _turnExecutor.NextTurn(true);
    return _turnExecutor.ACTPop();
}
```

**After:**
```csharp
public TabState SkillStockACT()
{
    var acter = _context.Acter;
    var skill = acter?.NowUseSkill;
    if (acter == null || skill == null)
    {
        _context.SkillStock = false;
        _turnExecutor.NextTurn(true);
        return _turnExecutor.ACTPop();
    }

    // 満杯チェック（防御的。UI側でも弾いているが念のため）
    if (skill.IsFullStock())
    {
        _context.Logger.Log(skill.SkillName + "のストックが満杯のためスキップ。");
        _context.SkillStock = false;
        _turnExecutor.NextTurn(true);
        return _turnExecutor.ACTPop();
    }

    // ストックロジック（ここに集約）
    skill.ATKCountStock();
    _context.Logger.Log(skill.SkillName + "をストックしました。");

    // 他のStockpileスキルを忘れさせる
    foreach (var other in acter.SkillList)
    {
        if (!ReferenceEquals(other, skill) && other.HasConsecutiveType(SkillConsecutiveType.Stockpile))
        {
            other.ForgetStock();
        }
    }

    // 前のめり判定
    if (skill.AggressiveOnStock.isAggressiveCommit)
    {
        _context.BeVanguard(acter);
    }

    _context.SkillStock = false;
    _turnExecutor.NextTurn(true);
    return _turnExecutor.ACTPop();
}
```

### 4. BattleAIBrain.cs — CommitDecision にストック分岐追加

既存の`CommitDecision`に`AIDecision.IsStock`分岐を追加するだけで、AIからストック行動が可能になる。

```csharp
// AIDecision に追加
public bool IsStock; // trueならSkillフィールドをストック対象として扱う
// ※ StockSkillという別フィールドは作らない。Skillフィールドを共用する。

// HasAny にIsStockは追加しない（IsStock=trueなら必ずSkillもセットされるためHasSkillで十分。
// IsStockだけtrueでSkillがnullという不正状態でHasAny=trueになるサイレントバグを防ぐ）

// CommitDecision 内に追加（IsEscapeの直後、単体先約チェックの前）
if (decision.IsStock && decision.HasSkill)
{
    if (decision.Skill.IsFullStock())
    {
        manager.DoNothing = true;  // 満杯なら何もしない（ターン浪費を防ぐ）
        return;
    }
    user.NowUseSkill = decision.Skill;  // 直接代入（SKillUseCallは使わない）
    manager.SkillStock = true;
    return; // ストック行動としてコミット
}
```

**設計判断（レビューで確定）:**
- `StockSkill`という別フィールドは不要。`IsStock = true`のとき`Skill`をストック対象として扱う
- `SKillUseCall`は使わない（ポイント消費・ForgetStock二重実行・CashMoveSetの副作用があるため）
- Plan()の「結果をバッファに書くだけ、副作用なし」原則に従い、AIからストックを選ぶ際は`decision.Skill = s; decision.IsStock = true;`とするだけ
- `HasAny`に`IsStock`は追加しない（`HasSkill`で十分。不正状態でのサイレントバグ防止）
- CommitDecisionで満杯チェックを行い、満杯なら`DoNothing`にフォールバック（AIがターンを浪費するバグの防止）

---

## 変更しないもの

| ファイル | 理由 |
|---|---|
| AllyClass.OnSkillStockBtnCallBack | ActionInput生成のみで、ロジックは含まない。変更不要 |
| PlayersUIService | ストックボタンのバインドのみ。変更不要 |
| BattleFlow.CharacterActBranchingAsync | `if (SkillStock)` の分岐は既存のまま。ActionSkipExecutor側の変更で対応 |
| BattleInputTypes / BattleInput | StockSkill enum / ファクトリは既存のまま使用 |
| BattleEventReplayer | StockSkill の入力復元はそのまま使用。Session側が対応する |
| BaseSkill.Consecutive.cs | ATKCountStock, ForgetStock, IsFullStock 等の関数自体は変更なし |

---

## トリガーとの構造比較（After）

| 側面 | トリガー | ストック（After） |
|---|---|---|
| 入力時 | NowUseSkillにセット | NowUseSkillにセット + SkillStockフラグ |
| BM分岐 | TrigerCount()でカウント判定→TriggerAct | SkillStockフラグ→SkillStockACT |
| ゲームロジック | TriggerActに集約 | SkillStockACTに集約 |
| AI対応 | CommitDecisionでSkillセットのみ | CommitDecisionでNowUseSkill直接代入+IsStock |
| コード重複 | なし | なし（解消） |

---

## リスク・注意点

- **リプレイ互換性**: BattleSession.ApplyStockSkill の処理が変わるため、リファクタリング前に記録されたリプレイデータがある場合、ストック行動の再生結果が変わる可能性がある。ただし記録されるのは入力（StockSkill + スキル名）であり、実行ロジックはBM側に移動するだけなので、結果は同一になるはず
- **満杯チェックの二重化**: UI側（即座にフィードバック）とBM側（防御的）の両方でチェックする設計。冗長だが安全
- **NowUseSkill直接代入のフェイルセーフ**: ストック時にNowUseSkillがセットされた状態で、何らかの理由でSkillStockフラグが消えた場合、通常スキル実行として進行するリスクがある。ただしCharacterActBranchingAsyncでSkillStockチェックが他の分岐（TrigerCount等）より前にあるため、フラグが正しく立っていれば問題ない
- **SKillUseCallは使わない（レビューで確定）**: ポイント消費・CashMoveSet・ForgetStock二重実行の3つの副作用があり、ストック行動では全て不適切。NowUseSkillへの直接代入のみ行う
- **CashMoveSetの発動タイミング**: ストック行動時はCashMoveSetを呼ばないが、ストック満杯後に通常発動する際は通常のスキル選択パス（SKillUseCall）を経由するため、CashMoveSetは適切なタイミングで呼ばれる。問題なし

---

## 作業順序

1. ActionSkipExecutor.SkillStockACT にストックロジックを集約
2. BattleOrchestrator.ApplyStockSkill を簡素化（ロジック削除、スキルセット+フラグのみ）
3. BattleSession.ApplyStockSkill を簡素化（同上）

**注意:** ステップ1〜3は一括適用すること。ステップ1だけ先に適用すると、Orchestrator/Session側のATKCountStock + SkillStockACT側のATKCountStockで**ストックロジックが二重実行**される中間状態が生まれる。

4. 動作確認（主人公でストック→実行の一連の流れ）
5. BattleAIBrain.CommitDecision にストック分岐追加
6. テスト用派生AIでストック動作確認
