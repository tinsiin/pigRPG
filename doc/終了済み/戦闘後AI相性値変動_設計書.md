# 戦闘後AI相性値変動 設計書

## 1. 概要

戦闘終了時にBattleMemoryの記録を振り返り、敵同士の相性値を動的に変動させる仕組み。
変動後の相性値はPostBattle行動（戦闘後AI）の分岐に使われ、次回エンカウント時にも引き継がれる。

### 目的

- 「一緒に戦った経験で絆が変わる」をシステムとして表現する
- 戦闘後AIの行動（助けるかどうか）に相性値を反映させる
- 再遭遇時に前回の関係性が引き継がれる

---

## 2. 全体フロー（時系列）

```
戦闘中
  |  BattleMemoryに出来事が自動記録される（既存）
  |  相性値は変わらない（既存通り）
  |
戦闘終了 ─ BattleManager.OnBattleEnd() 内
  |
  +-- (1) 相性値変動の計算 [新規]
  |     BattleMemoryの記録を振り返り、CharaCompatibilityを更新
  |
  +-- (2) 変動値の永続化 [新規]
  |     NormalEnemy個体に変動量を保存
  |
  +-- (3) PostBattle行動 [既存部品のみ]
  |     変動後の相性値を既存の利他行動システム等が読み取り、行動を決定
  |     ※行動の結果は相性値に影響しない（明示ルール）
  |
  +-- (4) 成長処理（既存: OnWin/OnRunOut等）
  |
  +-- クリーンアップ
```

### 明示ルール

- **相性値の変動は戦闘終了時に一括計算**する（戦闘中にリアルタイム変動しない）
- **PostBattle行動の結果は相性値に影響しない**（無限ループ防止、因果関係の明確化）
- **戦闘中の既存ロジック（回避率、連鎖逃走等）には影響しない**（変動はPostBattle前に計算するが、戦闘中の判定には使わない）

---

## 3. 相性値変動の計算（新規）

### 3.1 変動タイミング

`BattleManager.OnBattleEnd()` 内で、`EnemiesBattleEndSkillAI()`（PostBattle行動）の**前**に実行する。

### 3.2 変動トリガーと量（たたき台）

BattleMemoryの4つの記録（被害・行動・カウンター・死亡）を振り返り、各ペアの相性値変動量を算出する。

| トリガー | 変動量 | 参照する記録 | 説明 |
|---------|--------|-------------|------|
| 味方から回復を受けた | +5~10 | ActionRecord（味方のHeal系行動） | 助けてもらった恩 |
| 味方がかばってくれた | +8~15 | DamageRecord（かばい判定） | 身を挺してくれた |
| 敵を殺した | +3~5 | DeathRecord（敵側の死亡記録） | グループ全員同士の相性値が上がる。共闘で敵を倒した連帯感 |
| 味方が逃走した | -10~20 | ActionRecord（WasEscape） | 裏切り・見捨てられた |

※値は仮。実装後にプレイテストで調整する。

### 3.3 計算の流れ

```
CalcPostBattleBondDeltas(BattleGroup group):
  foreach 生存メンバーペア (A, B):
    delta = 0
    Aの記憶とBの記憶を突き合わせてトリガー判定
    delta += 各トリガーの変動量
    group.CharaCompatibility[(A,B)] += delta  // PostBattle行動用に即反映
    A.RecordBondDelta(B.guid, delta)          // 永続化
    B.RecordBondDelta(A.guid, delta)          // 双方向に記録
```

---

## 4. 永続化（新規）

### 4.1 保存先：NormalEnemy個体

BattleMemoryは毎戦闘`Clear()`でリセットされる戦術記憶であり、保存先として不適切。
相性変動は「キャラ同士の関係性」であり、キャラ個体に持たせるのが概念的に正しい。

```csharp
// NormalEnemy に追加
// キー = 相手のInstanceGuid, 値 = CSV基礎値からの累積変動量（+/-）
[NonSerialized] private Dictionary<string, int> _bondDeltas = new();

public void RecordBondDelta(string otherGuid, int delta)
{
    _bondDeltas.TryGetValue(otherGuid, out var current);
    _bondDeltas[otherGuid] = current + delta;
}

public int GetBondDelta(string otherGuid)
{
    _bondDeltas.TryGetValue(otherGuid, out var delta);
    return delta;
}
```

### 4.2 AI(SO)との関係

- AI Brain(SO)は共有リソース（考え方・戦略のテンプレート）
- `_bondDeltas`はNormalEnemy個体のランタイムフィールド（`_aiMemory`と同じ設計）
- SO共有汚染とは無関係

### 4.3 次回エンカウント時の合算

`BuildCompatibilityData()`で、CSV基礎値に永続化された変動値を加算する：

```
最終相性値 = Clamp(
    CSV基礎値(A.精神属性, B.精神属性)
    + (A.GetBondDelta(B.guid) + B.GetBondDelta(A.guid)) / 2,
    0, 160
)
```

- 双方の変動値の平均を取る（一方的に好きでも、もう片方が嫌いなら中間になる）
- 0~160の既存レンジにクランプ

---

## 5. 既存PostBattle部品との関係

### 5.1 本設計の位置づけ

本設計（相性値変動）は、既存のPostBattle部品の**上流にデータを供給する仕組み**。
戦闘後の行動判断を行うヘルパーは既に完成しており、新しい行動ヘルパーは不要。

```
本設計が作るもの（データの生産側）:
  BattleMemory → 相性値変動計算 → CharaCompatibility更新 + 永続化
                                        ↓
既存部品が使うもの（データの消費側）:           ↓
  BuildAltruisticTargetList ← CharaCompatibility を読んで候補リスト生成
  SelectPostBattleCandidateSkills ← スキル候補抽出
  ScoreHealSkill / ScoreAddPassiveSkill ← スコアリング
  PostBattlePlan（手書き）← 上記部品を組み合わせてスキル選択・実行
```

### 5.2 既存PostBattle部品一覧（変更なし）

| 部品 | 責務 | 状態 |
|------|------|------|
| `BuildAltruisticTargetList` | **誰を**助けるか。相性値閾値(60)+精神属性のHelpBehaviorProfileで候補リスト生成。各候補に`Compatibility`値が付く | 既存完成 |
| `SelectPostBattleCandidateSkills` | **使えるスキル**の候補抽出（リソース・タイプフィルタ） | 既存完成 |
| `ScoreHealSkill` / `ScoreAddPassiveSkill` | スキルの**優先度**スコアリング | 既存完成 |
| `PostBattleActRun` | スキル実行パイプライン（DeathHeal再試行、リソース管理等） | 既存完成 |
| `PostBattlePlan` 派生実装 | **何を**するか（上記部品を組み合わせたスキル選択と実行） | **手書き領域** |

### 5.3 手書きPlanでの相性値の使い方

`BuildAltruisticTargetList` が返す `TargetCandidate.Compatibility` を直接参照すればよい。
相性値→行動方針の変換は手書きPlan内で行う（キャラ固有の戦略判断のため）。

```
PostBattlePlan(self, decision) の手書き例:

  allies = EnumerateGroupAllies(self, includeDead: true)
  targets = BuildAltruisticTargetList(self, allies)  // 既存部品
  candidates = SelectPostBattleCandidateSkills(self) // 既存部品

  foreach tc in targets:
    if tc.IsSelf:
      自分への回復
      continue

    // tc.Compatibility を直接見てキャラ固有の判断
    if tc.Compatibility >= 90:
      dead → DeathHeal試行
      alive → 最良の回復 + 付与
    elif tc.Compatibility >= 70:
      alive, HP低 → 回復
    else:
      余裕がある時だけ
```

### 5.4 今回の変動が既存部品に与える効果

相性値が変動することで、既存部品の動作が**自動的に変わる**：

- `BuildAltruisticTargetList`: 閾値(60)を超える味方が増減する → 助ける対象が変わる
- `TargetCandidate.Compatibility`: 値が上下する → 手書きPlanの分岐結果が変わる
- 次回エンカウント時: 戦闘中の自動効果（回避率、連鎖逃走等）にも累積的に影響

---

## 6. 既存コードへの影響

### 6.1 変更が必要なファイル

| ファイル | 変更内容 |
|---------|---------|
| `NormalEnemy.cs` | `_bondDeltas` フィールド追加、`RecordBondDelta`/`GetBondDelta` メソッド追加 |
| `BattleManager.cs` | `OnBattleEnd()`に相性値変動計算の呼び出しを追加（`EnemiesBattleEndSkillAI()`の前） |
| `EncounterEnemySelector.cs` | `BuildCompatibilityData()`で `_bondDeltas` を合算 |

### 6.2 変更しないもの

| ファイル | 理由 |
|---------|------|
| `BattleMemory.cs` | 戦術記憶としての役割は変わらない。Clear()も既存通り |
| `BattleGroup.cs` | CharaCompatibilityのDictionaryは既存のまま。書き込みが増えるだけ |
| 戦闘中の相性値参照箇所（回避率、連鎖逃走等） | 変動は戦闘終了時なので影響なし |

### 6.3 新規ファイル（候補）

| ファイル | 内容 |
|---------|------|
| `PostBattleBondCalculator.cs` | BattleMemoryから相性値変動量を計算するロジック（BattleManagerから呼ぶ） |

---

## 7. 相性値の全体像（変動導入後）

```
エンカウント時
  BuildCompatibilityData()
    CSV基礎値 + 永続化された_bondDeltas合算
    → BattleGroup.CharaCompatibility に設定

戦闘中（読み取り専用、既存通り）
    回避率 (>=88)
    連鎖逃走 (>=77)
    利他行動 (>=60)
    親友死亡トラウマ (>=90)
    リベンジボーナス (>=86)
    ヘルプ回復 (>=60)

戦闘終了
    (1) BattleMemory振り返り → 相性値変動計算 [新規]
    (2) CharaCompatibility更新 + _bondDeltas永続化 [新規]
    (3) PostBattle行動（既存の利他行動システムが変動後の相性値を読む）
        ※行動は相性値に影響しない

次回エンカウント
    CSV基礎値 + _bondDeltas → 新しいCharaCompatibility
    → 戦闘中の全閾値に累積的に影響
```

---

## 8. 未決定事項

- [ ] 変動トリガーの具体的な値（プレイテストで調整）
- [ ] _bondDeltasの上限/下限（無限に累積させるか、キャップを設けるか）
- [ ] 双方向平均 vs 一方向（A→BとB→Aを独立にするか平均にするか）
- [ ] 「かばい」判定の具体的な検出方法（BattleMemoryに記録があるか要確認）

---

## 改訂履歴

| 日付 | 内容 |
|------|------|
| 2026-03-08 | 初版作成。会話での議論をもとに全体設計をまとめた |
