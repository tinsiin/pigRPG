# 割り込みカウンター SingleTarget 未接続

## 概要

割り込みカウンター発生時に、`ActionQueue.Add()` の `SingleTarget` 引数が `null` のまま渡されている。
受け取り側のロジックは実装済みだが、送り側が未接続のため、カウンター対象者の固定とVoidTurn判定が機能していない。

## 現状

### 送り側（未接続）

`BaseStates.BattleEvent.cs` L953:

```csharp
manager.Acts.Add(this, manager.GetCharacterFaction(this), "割り込みカウンター", null, isfreeze, null, CounterDEFATK);
//                                                                                         ↑ SingleTarget = null
```

ここで `attacker`（カウンター対象 = 攻撃してきた敵）を渡すべきだが、`null` になっている。

### 受け取り側（実装済み）

`TurnExecutor.CharacterAddFromListOrRandom()` L182-186:

```csharp
var singleTarget = entry?.SingleTarget;
if (singleTarget != null && singleTarget.Death())
{
    _context.VoidTurn = true;
}
```

SingleTargetが非nullかつ死亡していれば `VoidTurn = true` → ターンが消し飛ぶ。
この分岐は正しく書かれているが、SingleTargetが常にnullなので到達しない。

`SkillExecutor.SkillACT()` L36-41:

```csharp
var singleTarget = _context.Acts.TryPeek(out var entry) ? entry.SingleTarget : null;
if (singleTarget != null)
{
    acter.Target = DirectedWill.One;
    _context.Unders.CharaAdd(singleTarget);
}
```

SingleTargetが非nullならターゲットを単体に強制して対象者リストに追加する。
これも正しいが、同様にSingleTargetが常にnullなので到達しない。

`BattleOrchestrator` / `BattleSession` のUI側:

SingleTargetがある場合はスキル選択UIをスキップ（`TabState.NextWait`）し、
スキルボタンも単体攻撃系に制限する処理が組まれている。こちらも実装済みだが到達しない。

## 期待される修正

`BaseStates.BattleEvent.cs` L953 の `Acts.Add` 呼び出しで、6番目の引数に `attacker`（カウンターの起因となった攻撃者）を渡す:

```csharp
manager.Acts.Add(this, manager.GetCharacterFaction(this), "割り込みカウンター", null, isfreeze, attacker, CounterDEFATK);
```

## 修正後に有効になる仕様

以下は先約リストメモ・割り込みカウンターメモに記載された仕様で、SingleTarget接続後に機能する:

1. **カウンター対象者が死亡 → VoidTurn**: カウンターのために用意されたターンなのに対象がいないなら、ターン自体を消す
2. **スキル範囲に関わらず単体強制**: どの `SkillZoneTrait` でも `DirectedWill.One` でカウンター対象のみを攻撃
3. **UI制限**: 自由にスキルを選べる場合、単体攻撃系スキルのみボタンが有効になる

## 関連ドキュメント

| ドキュメント | 場所 |
|---|---|
| 先約リストメモ | `notes/豚岬フォルダ/先約リスト.md` |
| 割り込みカウンターメモ | `notes/豚岬フォルダ/割り込みカウンター.md` |
