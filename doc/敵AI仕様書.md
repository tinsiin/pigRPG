# 敵AI仕様書（実装準拠）

本書は敵の戦闘AIシステムの**現行実装**に基づく仕様書である。

関連ファイル:
- `Assets/Script/BattleAIBrains/BattleAIBrain.cs` — AI基底クラス
- `Assets/Script/BattleAIBrains/SimpleRandomTestAI.cs` — テスト用派生AI
- `Assets/Script/BaseStates/Battle/BaseStates.BattleBrainSimlate.cs` — ダメージシミュレート
- `Assets/Script/Enemy/NormalEnemy.cs` — 敵クラス（AIの呼び出し元）

関連仕様書:
- `doc/敵思考AI実装計画.md` — 将来構想（協調行動AI等。大部分が未実装）

---

## 1. 全体構造

### 1.1 クラス階層

```
ScriptableObject
  └─ BattleAIBrain（抽象基底）
       └─ SimpleRandomTestAI（テスト用：ランダムスキル選択）
       └─ （今後キャラ固有AIをここに追加）
```

BattleAIBrainはScriptableObjectであり、`NormalEnemy`の`[SerializeField] BattleAIBrain _brain`にアセットとして差し込む。同一アセットを複数の敵で共有可能。

### 1.2 呼び出しフロー

```
NormalEnemy.SkillAI()
  → _brain.SkillActRun(manager)
      → 共通初期化・強制分岐判定
      → Plan(decision) ← 派生クラスが実装
      → CommitDecision(decision, reserved)
          → user.SKillUseCall / RangeWill / Target に反映
```

戦闘後行動:
```
NormalEnemy.BattleEndSkillAI()
  → _brain.PostBattleActRun(self, manager)
      → PostBattlePlan(self, decision) ← 派生クラスが実装
      → Actions順に ApplySkillCoreOutOfBattle で適用
```

---

## 2. 戦闘中AI（SkillActRun）

### 2.1 実行フロー詳細

`SkillActRun(IBattleContext)`が統一入口。以下の順で処理される:

1. **共通初期化**: `manager`, `user`, `_random`の設定、ポリシー検証
2. **Freeze判定**: `user.IsFreeze == true` → `HandleFreezeContinuation()`で連続実行処理（通常思考には進まない）
3. **強制キャンセル判定**: `user.HasCanCancelCantACTPassive == true` → `OnCancelPassiveThink()`でパッシブ除去のみ
4. **使用可能スキル選別**: `MustSkillSelect()`でフィルタ。空なら`DoNothing`
5. **単体先約確認**: `manager.Acts`から先約ターゲットの有無を取得
6. **Plan実行**: `Plan(decision)`を呼び、派生クラスが`AIDecision`に結果を書く
7. **Commit**: `CommitDecision()`で`AIDecision`の内容をゲーム状態に反映

### 2.2 AIDecision（決定バッファ）

派生クラスの`Plan()`はこの構造体に結果を書き込む。

| フィールド | 型 | 用途 |
|---|---|---|
| `Skill` | BaseSkill | 使用スキル |
| `RangeWill` | SkillZoneTrait? | 範囲意志（単体/複数等） |
| `TargetWill` | DirectedWill? | 対象意志（One等） |
| `IsEscape` | bool | 逃走フラグ |

### 2.3 CommitDecision（反映ルール）

- **逃走時**: `user.SelectedEscape = true`のみ設定。スキル反映なし
- **単体先約あり**: `Skill`のみ反映（RangeWill/TargetWillはBM側が制御）
- **通常時**: Skill → RangeWill（正規化あり） → TargetWill の順に反映

### 2.4 MustSkillSelect（使用可能スキル選別）

プレイヤー側の`CanCastNow`/`ZoneTraitAndTypeSkillMatchesUIFilter`と同等のフィルタ:

1. `SkillResourceFlow.CanCastSkill(acter, skill)` — ポイント消費可能か
2. 単体先約がある場合: `IsEligibleForSingleTargetReservation()` — 単体系ZoneTrait+攻撃タイプに限定

※刃物武器チェック（`IsBlade`）は変数として取得されているが、現時点ではフィルタ条件に含まれていない。

---

## 3. 特殊状態の処理

### 3.1 連続実行（Freeze）

スキル復元は`BaseStates.ResumeFreezeSkill()`に統一。主人公側（TurnExecutor）とAI側（HandleFreezeContinuation）の両方から呼ばれ、`FreezeResumeResult`を返す:

| 結果 | 意味 | 主人公側の処理 | AI側の処理 |
|---|---|---|---|
| `Cancelled` | 打ち切りまたはエラー | `DoNothing` + NextWait | `DoNothing` |
| `Resumed` | 復元完了、操作不要 | BMに委譲 | BMに委譲 |
| `ResumedCanOperate` | 復元完了、操作可能 | `DetermineNextUIState` | `OnFreezeOperate(skill)` |

`ResumeFreezeSkill()`の内部処理:
1. **打ち切り予約あり**: `DeleteConsecutiveATK()` → `Cancelled`
2. **FreezeUseSkillがnull**: → `Cancelled`
3. **スキル復元**: `NowUseSkill`と`RangeWill`を復元
4. **操作可能判定**: 2回目以降かつ`CanOprate` → `ResumedCanOperate`、それ以外 → `Resumed`

### 3.2 強制キャンセル（CantACTパッシブ）

`OnCancelPassiveThink()`（virtual、派生でoverride可）:

1. `SelectCancelableCantActPassives()` — `IsCantACT && CanCancel`のパッシブを列挙
2. `RandomSelectCanCancelPassiveOnlyBadPassives()` — `IsBad == true`のもののみからランダムに1つ選択
3. `CancelPassive()` — 選んだパッシブを除去し`PassiveCancel = true`

悪いパッシブがない場合は`DoNothing`。

---

## 4. 思考部品（基底クラス提供）

派生AIは以下の部品を`Plan()`等の中で組み合わせて使う。

### 4.1 ベストダメージ分析

`AnalyzeBestDamage(availableSkills, potentialTargets)` → `BruteForceResult`

#### SkillAnalysisPolicy（シミュレーション設定）

Inspectorで設定するダメージシミュレーションのポリシー:

| 設定 | 型 | 説明 |
|---|---|---|
| `groupType` | TargetGroupType | Single: 単体分析 / Group: 全組み合わせ総当たり |
| `hpType` | TargetHPType | Highest/Lowest/Random（Single時のターゲット選択基準） |
| `damageType` | SimulateDamageType | dmg: 物理ダメージ / mentalDmg: 精神ダメージ |
| `spiritualModifier` | bool | 精神補正を考慮するか |
| `physicalResistance` | bool | 物理耐性による減衰を考慮するか |
| `SimlateVitalLayerPenetration` | bool | バリア層貫通をシミュレートするか |
| `SimlateEnemyDEF` | bool | 敵DEFを考慮するか（基本防御力のみ） |
| `SimlatePerfectEnemyDEF` | bool | 完全DEFシミュレート（パッシブ・AimStyle含む） |

#### 刻み・ブレ段階システム

AIの行動にバリエーションを持たせるための仕組み:

| 設定 | 説明 |
|---|---|
| `damageStep` | ダメージ刻み幅。例: step=10, ダメージ237→230に丸める。1=刻み無効 |
| `variationStages` | 最大刻み値から何段下までを候補にするか。例: max=230, stages=3→{230,220,210} |
| `useWeightedSelection` | true: 上位ほど高い重みで抽選 / false: 候補から均等抽選 |

重み付き抽選の重み: `weight = max(0.1, 1.0 - stepsFromMax * 0.3)`

#### 分析関数の分岐

- **ターゲット1人**: `SingleBestDamageAnalyzer`で直接分析
- **groupType == Group**: `MultiBestDamageAndTargetAnalyzer`で全スキル×全ターゲット総当たり
- **groupType == Single**: `hpType`に応じてターゲット1人を選択→`SingleBestDamageAnalyzer`
- **スキル1つしかない場合**: 分析不要として`null`を返す

### 4.2 デフォルトスキル選択

`SelectSkill(candidates, potentialTargets)` (virtual):
- ターゲットがいれば`AnalyzeBestDamage`で最大ダメージスキルを選択
- なければ`candidates[0]`をフォールバック

### 4.3 パッシブキャンセル部品

| 関数 | 用途 |
|---|---|
| `SelectCancelableCantActPassives()` | IsCantACT && CanCancelのパッシブ列挙 |
| `RandomSelectCanCancelPassiveOnlyBadPassives(list)` | IsBad=trueからランダム1つ |
| `CancelPassive(passive)` | パッシブ除去＋`PassiveCancel`フラグ設定 |

---

## 5. ダメージシミュレート（BaseStates側）

`BaseStates.SimulateDamage(attacker, skill, policy)` — AI用のダメージ試算関数。

### 5.1 計算フロー

1. **スキル威力計算**: `ComputeSkillPowers`（spread=1.0固定）
   - `spiritualModifier == false`なら素の威力にリセット
2. **防御力計算**: ポリシーに応じて3段階
   - `SimlatePerfectEnemyDEF`: 完全DEF（パッシブ・AimStyle含む）
   - `SimlateEnemyDEF`: 基本防御力のみ（b_b_def + 共通TenDay）
   - どちらもfalse: DEF=0
3. **ダメージ計算**: 魔法/非魔法で分岐
4. **物理耐性減衰**（オプション）
5. **バリア層シミュレート**（オプション）: 全レイヤーを順に貫通計算
6. **結果**: `dmg`または`mentalDmg`をポリシーの`damageType`で返す

### 5.2 バリア層シミュレート

実際のバリア層を変更せずにダメージのみ計算。`BarrierResistanceMode`に応じた4種の破壊後計算を再現:

| モード | 破壊時の残余ダメージ |
|---|---|
| A_SimpleNoReturn | 軽減後の余剰をそのまま使用 |
| B_RestoreWhenBreak | 余剰を耐性率で割り戻す（元威力ベース） |
| C_IgnoreWhenBreak | 元ダメージ - 現LayerHP |
| C_IgnoreWhenBreak_MaxHP | 元ダメージ - 最大LayerHP |

破壊時の追加効果（KereKere依存の破壊慣れ/破壊負け）もシミュレート。

---

## 6. 戦闘後AI（PostBattleActRun）

### 6.1 概要

戦闘終了後に敵が自分や味方に対して回復・付与スキルを使う仕組み。AI内部で完結し、BattleManagerのActer/managerへのコミットは行わない。

### 6.2 実行フロー

1. `PostBattlePlan(self, decision)` — 派生クラスが`PostBattleDecision.Actions`にアクションを詰める
2. 各`PostBattleAction`について:
   - リソース不足なら早期終了
   - `DeathHeal`は特別扱い: 失敗時に`AngelRepeatChance`で再試行、諦め後はHeal/MentalHealスキップ
   - 通常スキルは`ApplySkillCoreOutOfBattle`で適用

### 6.3 PostBattleAction

| フィールド | 型 | 説明 |
|---|---|---|
| `Target` | BaseStates | 実行対象（null不可） |
| `Skills` | List\<BaseSkill\> | スキルシーケンス（最大5） |
| `IsSelf` | bool | 自分かどうか |
| `Options.AngelRepeatChance` | float | DeathHeal再試行確率（0-1） |

### 6.4 思考部品（戦闘後用）

| 関数 | 用途 |
|---|---|
| `SelectPostBattleCandidateSkills(self)` | 消費・武器適合を満たした回復/付与系スキルを抽出 |
| `SelectMostEffectiveHealSkill(self, candidates)` | 回復系で最高スコアのスキルを選択 |
| `SelectBestPassiveGrantSkill(self, candidates)` | 付与系で最高スコアのスキルを選択 |
| `ScoreHealSkill(self, skill)` | 回復スコア（HP回復量 + 精神回復量*0.5 + 死亡時DeathHealボーナス） |
| `ScoreAddPassiveSkill(self, skill)` | 付与スコア（良パッシブ*1.0 + VitalLayer*0.8 + SkillPassive*0.6） |

---

## 7. 利他行動システム

戦闘後、自分だけでなく味方にもスキルを使うかを判定する仕組み。

### 7.1 設定（Brain側）

| フィールド | 説明 |
|---|---|
| `AltruismAffinityThreshold` | 相性値しきい値（0-100）。これ以上の味方がいない場合は自分のみ |
| `UseAltruismComponent` | false→利他行動無効（自分のみ） |

### 7.2 HelpBehaviorProfile（精神属性別・世界固定）

各精神属性に対してハードコードされた確率テーブル:

| パラメータ | 説明 |
|---|---|
| `P_ExtraOthers` | 追加で他人を助ける確率 |
| `P_OnlyOthers` | 自分抜きで味方のみ助ける確率 |
| `P_GroupHelp` | 複数人を一度に助ける確率 |
| `P_FriendFirst` | 味方を自分より先に入れる確率 |
| `FavorAffinityRate` | 相性降順 vs ランダムの切替確率 |
| `MaxOthers` | 助ける味方人数の上限 |

代表例:
- **Sacrifaith**: 最も利他的（P_ExtraOthers=0.7, MaxOthers=3）
- **Psycho/Devil**: 最も利己的（P_ExtraOthers=0.2, MaxOthers=1）

### 7.3 BuildAltruisticTargetList フロー

1. `UseAltruismComponent == false` → 自分のみ
2. 味方の相性値を取得（`group.CharaCompatibility`）
3. しきい値以上の味方がいなければ → 自分のみ
4. `FavorAffinityRate`で並び替え方式決定（相性降順 or ランダム）
5. `P_GroupHelp`で複数選抜か単一か決定
6. `P_ExtraOthers`で追加人数を確率的に決定（上限: `MaxOthers`）
7. `P_OnlyOthers`で自分を含めるか決定
8. `P_FriendFirst`で自分と味方の順序を決定

---

## 8. 現行派生AI

### SimpleRandomTestAI

テスト用の最小実装。戦闘全体フローの動作確認が目的。

```
Plan(decision):
  1. availableSkillsからランダムに1つ選択
  2. decision.Skill に設定
  3. 完全単体選択スキル（CanPerfectSelectSingleTarget）なら decision.TargetWill = DirectedWill.One
```

戦闘後行動: 未実装（基底のデフォルト空実装を使用）

---

## 9. NormalEnemy側のAI関連処理

| 関数 | タイミング | 処理 |
|---|---|---|
| `SkillAI()` | 戦闘中ターン | `_brain.SkillActRun(manager)` |
| `BattleEndSkillAI()` | 戦闘終了時 | `_brain.PostBattleActRun(this, manager)` |
| `BindBrainContext(context)` | 戦闘開始時 | BrainにIBattleContextをバインド |

---

## 10. 拡張ポイント（派生AIで利用可能なvirtual/override）

| メソッド | 用途 |
|---|---|
| `Plan(AIDecision)` | **メイン思考**。スキル/範囲/対象/逃走を決定 |
| `OnFreezeOperate(BaseSkill)` | Freeze中の操作可能スキルの範囲/対象決め直し |
| `OnCancelPassiveThink()` | 強制キャンセル時の思考カスタマイズ |
| `SelectSkill(candidates, targets)` | デフォルトスキル選択ロジックの差し替え |
| `PostBattlePlan(self, decision)` | 戦闘後行動の計画 |
| `GetPostBattlePolicy()` | 戦闘後適用時のポリシー変更 |
| `ScoreHealSkill(self, skill)` | 回復スキルスコアリングの差し替え |
| `ScoreAddPassiveSkill(self, skill)` | 付与スキルスコアリングの差し替え |

---

## 改訂履歴

| 日付 | 内容 |
|---|---|
| 2026-03-06 | 初版作成。現行実装（BattleAIBrain + SimpleRandomTestAI）に基づく仕様書 |
