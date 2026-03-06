# 敵AI仕様書（実装準拠）

本書は敵の戦闘AIシステムの**現行実装**に基づく仕様書である。

関連ファイル:
- `Assets/Script/BattleAIBrains/BattleAIBrain.cs` — AI基底クラス
- `Assets/Script/BattleAIBrains/SimpleRandomTestAI.cs` — テスト用派生AI
- `Assets/Script/BattleAIBrains/BasicTacticalAI.cs` — 基本戦術AI
- `Assets/Script/BaseStates/Battle/BaseStates.BattleBrainSimlate.cs` — ダメージ・命中率シミュレート
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
       └─ BasicTacticalAI（基本戦術AI：ダメージ分析+逃走判断）
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

派生クラスの`Plan()`はこのクラスに結果を書き込む。

| フィールド | 型 | 用途 |
|---|---|---|
| `Skill` | BaseSkill | 使用スキル |
| `RangeWill` | SkillZoneTrait? | 範囲意志（単体/複数等） |
| `TargetWill` | DirectedWill? | 対象意志（One等） |
| `IsEscape` | bool | 逃走フラグ |
| `IsStock` | bool | ストック行動フラグ。trueならSkillをストック対象として扱う |

### 2.3 CommitDecision（反映ルール）

- **逃走時**: `user.SelectedEscape = true`のみ設定。スキル反映なし
- **ストック時**: `IsStock && HasSkill`のとき、満杯チェック（`IsFullStock`→`DoNothing`にフォールバック）の後、`user.NowUseSkill`に直接代入 + `manager.SkillStock = true`。`SKillUseCall`は使わない（ポイント消費・CashMoveSet・ForgetStockの副作用回避）
- **単体先約あり**: `Skill`のみ反映（RangeWill/TargetWillはBM側が制御）
- **通常時**: Skill → RangeWill（正規化あり） → TargetWill の順に反映

### 2.4 CommitDecision分岐順序

```
1. decision == null → DoNothing + エラーログ
2. IsEscape → SelectedEscape
3. IsStock && HasSkill → 満杯チェック → NowUseSkill直接代入 + SkillStock
4. 単体先約あり && !HasSkill → DoNothing + エラーログ
5. 単体先約あり && HasSkill → SKillUseCall(Skill)のみ
6. !HasSkill → DoNothing + エラーログ（スキル未設定の異常パス防止）
7. 通常 → SKillUseCall + RangeWill(置換) + TargetWill
```

※RangeWillはプレイヤー側（UI段階的選択のためAdd/OR）と異なり、AIは一括決定のため置換で適用。ターン開始時にRangeWill=0リセット済みのため実質的な差異はない。

### 2.5 MustSkillSelect（使用可能スキル選別）

プレイヤー側の`CanCastNow`/`ZoneTraitAndTypeSkillMatchesUIFilter`と同等のフィルタ:

1. `SkillResourceFlow.CanCastSkill(acter, skill)` — ポイント消費可能か
2. 単体先約がある場合: `IsEligibleForSingleTargetReservation()` — 単体系ZoneTrait+攻撃タイプに限定

※刃物武器チェック（`IsBlade`）は変数として取得されているが、現時点ではフィルタ条件に含まれていない。

### 2.6 AI思考ログ（LogThinkシステム）

AIの判断過程を追跡するためのログ基盤。Inspector設定でログの詳細度を制御する。

#### 設定

| フィールド | 型 | 説明 |
|---|---|---|
| `_thinkLogLevel` | int (0-3) | Inspectorで設定。このレベル以下のログのみ出力される |

#### ログレベル

| レベル | 名称 | 出力内容 | 用途 |
|---|---|---|---|
| 0 | Result | 最終決定（スキル名/逃走/DoNothing） | リリース・プレイテスト |
| 1 | Candidates | 候補スキル一覧 | プレイテスト |
| 2 | Scored | MustSkillSelectフィルタ結果、Single/Group分析の最適結果+ダメージ値 | AI調整 |
| 3 | Full | 全スキル×ターゲット個別ダメージ試算、除外スキル名 | デバッグ |

#### 使用方法

```csharp
// 基底クラスが提供するログ関数
protected void LogThink(int level, string message)
```

出力フォーマット: `[AI:キャラ名][Tターン数] メッセージ`

デフォルト`_thinkLogLevel=0`のため、明示的に上げない限り最終結果のみが出力される。

#### ログ挿入箇所（現行）

| 箇所 | レベル | 内容 |
|---|---|---|
| SkillActRun: Freeze分岐 | 0 | 「Freeze継続」 |
| SkillActRun: Cancel分岐 | 0 | 「キャンセル行動」 |
| SkillActRun: スキル空 | 0 | 「使用可能スキルなし」 |
| SkillActRun: Plan結果 | 0 | 最終決定（スキル名/逃走/ストック） |
| SkillActRun: Plan結果なし | 0 | 「Plan結果なし → DoNothing」 |
| SkillActRun: Plan前 | 1 | 候補スキル名一覧 |
| MustSkillSelect | 2 | フィルタ前後件数 |
| MustSkillSelect | 3 | 除外スキル名 |
| SingleBestDamageAnalyzer | 2 | 最適スキル+ダメージ値 |
| SingleBestDamageAnalyzer | 3 | 各スキルの個別試算ダメージ（※Step版ではDamageStepAnalysisHelper内部処理のため出力なし） |
| SingleBestDamageAnalyzer (Step) | 2 | 最適スキル（Step版） |
| MultiBestDamageAndTargetAnalyzer | 2 | 最適組み合わせ |
| MultiBestDamageAndTargetAnalyzer | 3 | 全組み合わせの個別試算ダメージ |
| MultiBestDamageAndTargetAnalyzer (Step) | 2 | 最適組み合わせ（Step版） |

#### 拡張方針

Phase 1以降で新しい思考部品（逃走判断、命中率シミュレート、トラウマ率等）を追加する際、その判断過程にも`LogThink()`を挿入していく。インフラとして全AI部品に横断的に適用される。

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
| `useExpectedDamage` | bool | trueなら期待ダメージ(ダメージ×命中率)で評価 |
| `considerVanguardForHit` | bool | 前のめり状態を命中率計算に反映するか |

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

### 4.4 ストック・トリガー情報取得ユーティリティ

派生AIがストック・トリガースキルの状態を簡易に取得するためのヘルパー。「いつ溜めるか」「いつ撃つか」の判断自体は派生AIのPlan()で手書きする領域。

| 関数 | 用途 |
|---|---|
| `GetStockpileSkills(skills)` | Stockpileフラグを持つスキルを列挙 |
| `GetStockInfo(skill)` → `StockInfo` | ストック状態の一括取得（現在値/最大値/充填率/満杯まで何回か等） |
| `GetTriggerSkills(skills)` | 発動カウント付きスキルを列挙（`TriggerMax > 0`） |
| `GetTriggerInfo(skill)` → `TriggerInfo` | トリガー状態の一括取得（残りターン/巻き戻し量/キャンセル可否等） |

#### StockInfo

| フィールド | 説明 |
|---|---|
| `Current` | 現在のストック数 |
| `Max` | 最大値（DefaultAtkCount） |
| `Default` | デフォルト値（DefaultStockCount） |
| `IsFull` | 満杯か |
| `StockPower` | 1回のストックで増える量 |
| `TurnsToFull` | 満杯まであと何回ストックが必要か |
| `FillRate` | 充填率（Current / Max） |

#### TriggerInfo

| フィールド | 説明 |
|---|---|
| `CurrentCount` | 現在のカウント |
| `MaxCount` | 最大カウント |
| `IsTriggering` | カウント中か |
| `RemainingTurns` | 発動まであと何ターンか |
| `RollBackCount` | 巻き戻し量 |
| `CanCancel` | キャンセル可能か |

**ストック行動のコミット方法:** ユーティリティ関数ではなく`AIDecision`経由。`decision.Skill = s; decision.IsStock = true;` とPlanに書くだけで、CommitDecisionがBM側SkillStockACTに委譲する。

### 4.5 Phase 1 基盤ユーティリティ

派生AIが`Plan()`内で利用できる基盤ヘルパー群。BattleAIBrain基底クラスに定義。

#### 自キャラ状態ヘルパー (1-1)

| プロパティ/関数 | 型 | 説明 |
|---|---|---|
| `HpRatio` | float | HP / MaxHP (0.0~1.0) |
| `MentalHpRatio` | float | MentalHP / MentalMaxHP (0.0~1.0) |
| `IsLowHP(threshold)` | bool | HpRatio < threshold (デフォルト0.25) |

#### ターン数取得 (1-2)

| プロパティ | 型 | 説明 |
|---|---|---|
| `TurnCount` | int | `manager.BattleTurnCount`（未設定時は0） |

#### 敵グループ列挙 (1-3)

`GetPotentialTargets(includeDeadTargets)` — AIから見た攻撃対象（相手Faction）の生存キャラリストを返す。`includeDeadTargets=true`で死亡キャラも含む。

#### 逃走判断 (1-4)

Inspector設定:
- `_canEscape` (bool): 逃走可能フラグ
- `_escapeChance` (float, 0~1): 逃走確率

`ShouldEscape()` (virtual): `_canEscape && Roll(_escapeChance)`。派生AIでHP減少率やトラウマ率に基づく高度な逃走判断にoverride可能。

#### 命中率シミュレート + 期待ダメージ (1-5, 1-6)

**SimulateHitRate** (`BaseStates.BattleBrainSimlate.cs`に配置):

IsReactHIT + SkillHitCalcを確率論的に近似。乱数を使わず確定的な期待命中率(0.0~1.0)を返す。

計算手順:
1. キャラ間命中回避: `(eye - minusChance) / (eye + agi)` （後衛命中低下時、分子のみ減算。分母は生のeye）
2. スキル命中率: `SkillHitPer/100` + 命中凌駕補正
3. 統合: charHitRate × skillHitRate
4. 特殊補正（独立判定、両方該当すれば重複適用）: 爆破型+前のめりかすり化(70%近似)、魔法かすり(33%近似)

**意図的に再現しない要素:** ミニマムヒットチャンス、落ち着きカウント(1.0固定)、味方別口回避、パッシブ由来回避、先手攻撃補正

**HitSimulatePolicy** 構造体:

| フィールド | 説明 |
|---|---|
| `considerVanguard` | 前のめり状態を考慮するか |
| `attackerIsVanguard` | 攻撃者が前のめりか |
| `targetIsVanguard` | ターゲットが前のめりか |

ファクトリ: `FromBattleState(ctx, attacker, target)` / `Minimal`（前のめり無視）

**BattleAIBrain基底ラッパー:**

| 関数 | 説明 |
|---|---|
| `EstimateHitRate(target, skill)` | ポリシーに応じた命中率推定 |
| `EstimateExpectedDamage(target, skill)` | ダメージ × 命中率 |
| `EvaluateDamage(target, skill)` | Analyzer内部用。useExpectedDamage時は期待ダメージ、それ以外は純ダメージ |

**Analyzerとの統合:** SingleBestDamageAnalyzer / MultiBestDamageAndTargetAnalyzer 内部で`EvaluateDamage()`を使用。`useExpectedDamage=false`（デフォルト）なら従来通りの純ダメージ評価。

#### スキル名検索ヘルパー (1-7)

`FindSkill(string name)` — `availableSkills`からスキル名で検索。決め打ちAI等で使用。

### 4.6 BasicTacticalAI

`BasicTacticalAI.cs` — Phase 1の実証用派生AI。SimpleRandomTestAIの上位互換。

**思考フロー:**
1. 逃走判断（`ShouldEscape()`が`true`なら即逃走）
2. ダメージ分析（`AnalyzeBestDamage`でスキル＋ターゲット選定）
3. フォールバック（スキル1つの場合やダメージ分析不可時はランダム選択）

**Inspector設定で生まれる個性:**
- `SkillAnalysisPolicy`: ダメージ評価方法（物理/精神、DEF考慮、期待ダメージ、刻み・ブレ段階）
- `_canEscape` + `_escapeChance`: 逃走行動
- `damageStep` + `variationStages`: 行動のバリエーション幅

---

## 5. シミュレート関数（BaseStates側）

配置: `BaseStates.BattleBrainSimlate.cs`

`BaseStates.SimulateDamage(attacker, skill, policy)` — AI用のダメージ試算関数。
`BaseStates.SimulateHitRate(attacker, skill, policy)` — AI用の命中率試算関数。セクション4.5参照。

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

### BasicTacticalAI

SimpleRandomTestAIの上位互換。Inspector設定でダメージ分析ポリシーと逃走パラメータを調整するだけで多様な個性を実現する基本戦術AI。

```
Plan(decision):
  1. ShouldEscape() → trueなら即逃走（decision.IsEscape = true）
  2. GetPotentialTargets()で攻撃対象列挙
  3. availableSkills >= 2 かつ targets > 0 なら AnalyzeBestDamage でベストスキル+ターゲット選定
  4. 完全単体選択スキルなら decision.TargetWill = DirectedWill.One
  5. フォールバック: スキル1つならそれを使用、複数ならランダム選択（完全単体選択スキルならTargetWill=Oneも設定）
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

## 11. 未実装・将来構想（元メモ由来の全体像）

BM側の仕組みは存在するが、AIの思考部品として未実装のもの、及び着想段階の構想をここにまとめる。友情コンビ協調行動AI（`doc/敵思考AI実装計画.md`）とは別の領域。

---

### 11.1 手書きAI用の未実装部品（今すぐ作れるもの）

#### 11.1.1 ストック行動 ✅実装済み

**BMリファクタリング完了:** ストックロジック（ATKCountStock + ForgetStock + 前のめり判定）がBM側`SkillStockACT`に集約済み。UI入力（Orchestrator/Session）はスキルセット+フラグのみの薄いレイヤーに簡素化。

**AI対応完了:** `AIDecision.IsStock`フラグ + CommitDecisionのストック分岐により、派生AIからストック行動が可能。`decision.Skill = s; decision.IsStock = true;`とPlanに書くだけ。

**情報取得ユーティリティ完了:** `GetStockpileSkills`（スキル列挙）、`GetStockInfo`（充填率・残りターン等の一括取得）を基底クラスに追加済み。BaseSkill側にも`NowStockCount`, `MaxStockCount`, `StockDefault`, `StockPower`のpublic getterを追加。

**BM側の仕組み:**
- `SkillConsecutiveType.Stockpile`フラグを持つスキルがストック対象
- `ATKCountStock()`でストック数が増加、`IsFullStock()`で満杯判定
- ストック時、他のStockpileスキルは`ForgetStock()`で減少（1つしか溜められない）
- `SkillStock = true`を設定するとBM側が`SkillStockACT()`でストックロジック実行→ターン消費
- 撃つ時は通常のスキル選択。`ATKCount`がストック数を返し、連続攻撃回数になる

**判断ロジック自体の汎用部品化の価値は薄い。** 「いつ溜めるか」「いつ撃つか」はキャラの戦略そのものであり、派生AIのPlan()内で手書きする領域（判断基準は11.6「AI思考要素の実装粒度の判断基準」参照）。

#### 11.1.2 発動カウント（トリガー）スキルの意図的選択 ✅情報取得実装済み

発動カウント自体はBM側が自動処理する（AIがスキルを`NowUseSkill`にセットすれば`CharacterActBranchingAsync`内の`TrigerCount()`で自動的に`TriggerAct`に入る）。トリガーはストックと異なり専用のコミット処理は不要（通常のスキル選択と同じ）。

**情報取得ユーティリティ完了:** `GetTriggerSkills`（スキル列挙）、`GetTriggerInfo`（残りターン・巻き戻し量等の一括取得）を基底クラスに追加済み。BaseSkill側にも`CurrentTriggerCount`, `TriggerMax`, `TriggerRollBack`のpublic getterを追加。

**判断ロジック自体の汎用部品化の価値は薄い。** ストック同様、「いつ溜め技を選ぶか」「カウント中にどう動くか」はキャラ固有の戦略であり、派生AIのPlan()内で手書きする領域（判断基準は11.6参照）。

#### 11.1.3 決め打ちAI

ブルートフォースとは異なる、条件固定のスキル選択パターン。Plan()内で条件分岐として実装する想定。

**想定パターン例:**
- HPが半分以下になったターンだけ特定の固定スキルを選ぶ
- Nターン目は必ず決まったスキルを選ぶ
- スキルを1つしか持たないので必ずそれを使う

※部品として汎用化するというよりは、各派生AIのPlan()内に直書きで十分な領域。

#### 11.1.4 逃走思考 ✅基本レベル実装済み

- **基本**: ✅ `ShouldEscape()` virtual + Inspector設定（`_canEscape`, `_escapeChance`）→ セクション4.5参照
- **上級**: 逃走コールバック、HP減少率・被害履歴に基づく逃走判断（Phase 2で`_lastTurnHP`/`HpDropRate`追加後）
- **高度**: トラウマ率（割り込みカウンター頻度、HP急減）に応じた即逃走確率（Phase 2のトラウマ率実装後）

#### 11.1.5 Freeze（ターンまたぎ連続攻撃）中断判断

プレイヤーはターンの間にFreezeConsecutiveを中断できるが、敵は`SkillAI()`がターンごとにしか呼ばれないため、シームレスな中断手段がない。

**現状:**
- `HandleFreezeContinuation()`内で打ち切り予約（`IsDeleteMyFreezeConsecutive`）による消去は実装済み
- `TurnOnDeleteMyFreezeConsecutiveFlag()`を呼べば次ターンで中断される

**課題:**
- 「いつ中断するか」を判断するには、場の状況判断（相手の行動履歴、ポイント残量の推測、次に来そうな技の予測など）が必要
- これは相当思慮深いキャラ（思慮推測レベルが高い、またはAIAPI利用）でないとできない処理
- 仮にNextTurn等で非行動者としてのコールバックを入れても、中断判断のAI自体が複雑すぎるため後回し

#### 11.1.6 命中回避シミュレート部品 ✅実装済み

`SimulateHitRate` + `HitSimulatePolicy` + `EvaluateDamage` → セクション4.5参照。

ベストダメージ分析（セクション4.1）は元々**命中回避を計算しない、純粋な最大ダメージの思考**として設計されていたが、`useExpectedDamage`フラグにより期待ダメージ評価に切替可能になった。Analyzer内部は`EvaluateDamage()`経由で統一されており、フラグ切替で従来動作と完全互換。

**実装済み要素:**
- ✅ 命中率シミュレート関数（`BaseStates.SimulateHitRate`）
- ✅ 前のめり（自分・敵）の影響計算（`HitSimulatePolicy.considerVanguard`）
- ✅ 期待ダメージ統合（`SkillAnalysisPolicy.useExpectedDamage`）

**設計方針（不変）:**
- **敵のパッシブ**: 複雑すぎるため部品には組み込まない
- **意図的な不完全さ**: ミニマムヒットチャンス・落ち着きカウント等を無視し「予想と違った！」感を維持

---

### 11.2 思慮要素（着想段階の一覧）

AIが判断材料として参照しうる要素の全体像。現状のベストダメージ分析で使っているのはごく一部。

#### 敵（攻撃対象）の情報
| 要素 | 実装状況 | 備考 |
|---|---|---|
| 敵HP | シミュレート対応済み | SimulateDamageで使用 |
| 敵精神HP（精神属性・精神レベル） | シミュレート対応済み | mentalDmgモード |
| 敵群総HP / 総精神HP | 未実装 | Group分析時に全体量で判断 |
| 敵精神属性（精神属性相性） | 一部対応 | spiritualModifierオプション |
| 敵パッシブ | 未実装 | 防御系パッシブ等の読み取り |
| 敵のパワー | 未実装 | 相当思慮深くないと活用不可 |
| 敵のリカバリーターン | 未実装 | |
| 敵の前のめり | ✅情報取得実装済み | HitSimulatePolicy.targetIsVanguard（4.5参照） |
| 敵の物理属性 | 未実装 | |

#### 自分・味方の情報
| 要素 | 実装状況 | 備考 |
|---|---|---|
| 自HP / 自精神HP | ✅情報取得実装済み | HpRatio, MentalHpRatio, IsLowHP（4.5参照） |
| 自分のパッシブ、味方のパッシブ | 未実装 | |
| 自分の人間状況 | 未実装 | |
| 味方、自分のリカバリーターン | 未実装 | |
| 味方前のめり連携 | 未実装 | |
| 自分・味方の物理属性 | 未実装 | |
| ポイント残量 | 必然的に使用 | MustSkillSelectでCanCastチェック。高度な判断として、行動記録で戦力優位だったスキルのポイントを温存するか、今の最善手にポイントを使うかのリソース配分判断（着想段階） |

#### 記録・履歴系
| 要素 | 実装状況 | 備考 |
|---|---|---|
| 被害記録 | 未実装 | 割り込みカウンター頻度→トラウマ率へ蓄積、防御無視率の高いスキルを食らった記録→防御パッシブよりも回復にリソースを割く判断（相当思慮深いキャラ限定） |
| 行動記録 | 未実装 | 通った攻撃力が大きいものの記録。リソース配分判断の入力源（下記ポイント残量と連動） |
| トラウマ率 | 未実装 | 被害記録由来。カウンター頻度→防御無視率の高い連続攻撃を回避（11.4参照） |

#### スキル選択の追加考慮
| 要素 | 実装状況 | 備考 |
|---|---|---|
| スキル使用率 | 未実装 | インスペクタ設定の標準ロジック |
| スキル攻撃性質 | 部分的 | ZoneTraitは見ているが攻撃性質全般は未活用 |
| スキル分散性質 | 未実装 | |
| 防御無視率の高いスキル | 未実装 | 被害記録と連動（防御固定パッシブ vs 回復スキル選好） |

#### 思慮結果の出力先
- **スキル選択**: 実装済み（AIDecision.Skill）
- **範囲選択**: 実装済み（AIDecision.RangeWill）
- **対象者選択**: 実装済み（AIDecision.TargetWill）。味方殺しも含め将来的に選択可能

---

### 11.3 思慮推測のレベル

段階ごとに反映される思慮処理が増えていく仕組みの構想。手書きAIの各派生で「このキャラはどこまで賢いか」を表現するための指標。

- **レベルが上がるほど**: 考慮する思慮要素が増える（HP→パッシブ→被害記録→行動予測...）
- 思慮レベルが高いキャラは、11.2の思慮要素をより多く参照して判断する
- 頭のいいキャラなら割り込みカウンターを起こさないように操作する可能性がある
- Freeze中断判断（11.1.5）ができるのも相当思慮深いキャラのみ

### 11.4 精神レベル・トラウマ率（思慮推測とは別軸）

思慮推測のレベル（頭の良さ）とは独立した、感情・経験由来のパラメータ。

#### 精神レベル
- 頭の良さだけでは見抜けないEQ要素
- 敵の精神の削れ具合を見抜く能力など
- 思慮推測レベルが低くても精神レベルが高ければ、精神系の判断は鋭くなりうる

#### トラウマ率
- 被害記録由来の反応パラメータ
- 割り込みカウンター頻度やHP急減に応じて蓄積
- **影響例:**
  - **連続攻撃の回避判断**: 防御無視率が高い攻撃ほど割り込みカウンターされやすい（`TryInterruptCounter`で`DEFATK/3`が発動確率に直結）。トラウマが溜まると、AIは防御無視率の高い連続攻撃を避けるようになる
    - 各連続攻撃の「代表防御無視率」＝各ヒットの防御無視率のうち最大値
    - AIに「この代表防御無視率以上の連続攻撃は使わない」しきい値がある
    - トラウマ率が高いほどしきい値が下がる＝より低い防御無視率でも使わなくなる（臆病になる）
  - **再戦時の即逃走**: HPの減り具合に応じて再戦時の即逃走確率が上がる

### 11.5 AIAPI構想（外部AI利用・将来構想）

元メモでの「AIAPI」は、OpenAI GPT等の外部LLM/AIサービスをゲーム内敵AIの思考に利用する構想。思慮推測レベルや精神レベルとは別の仕組みで、将来的に実装するかもしれない独立した構想。

#### 概要
- 特定の高知能キャラ限定で、外部AIに場の状況を渡して思考結果を受け取る
- 通常の手書きAI部品では実現困難な、複雑な状況判断を外部AIに委譲する

#### 演出面
- 思考中に黒色の渦/WiFiマーク的な文様のアニメーションをループ表示
- アイコンにまとわりつく文様の演出
- 操作不能タイム（API応答待ち）をゲーム的に体感させる

#### 想定される思考内容
- 敵の行動履歴からの次手予測
- ポイント読みからのスキル予測→Freeze中断判断
- 場の総合的な状況判断（複数要素の統合的な評価）
- 割り込みカウンターを意図的に回避する操作

#### パッシブ読み取りの制限ルール案
AIAPIでのパッシブ想像は、アイコン拡大ステータス表示で表示されている範囲内のみ読み取れる。UIに表示されていないパッシブはAIからも見えない、というルール。外部AIに渡すコンテキストの制限としても機能する。

#### 現状
完全に構想段階。手書きAI部品の充実が先。外部API依存のため通信・コスト・レイテンシの課題もある。

---

### 11.6 設計思想メモ

#### エコシステム vs 埋め込みSO独自実装

当初はbool/レベル/場合分けによるエコシステム（汎用AIフレームワーク）を検討したが、以下の理由で**個別SO派生型**を採用:

- 思慮要素が多すぎ、分岐が多すぎ、独自的な要素も組み合わせ上無限にある
- エコシステムのメリット（使い回し回数）が作る労力に見合わない
- 各キャラでスキルに違いを出す→AI処理にも違いが必要→場合分けが無限に近い
- パッシブ等の複雑な要素を汎用化するのは非現実的

**現行の設計:**
- ScriptableObject派生で付け替え可能な頭脳
- 基礎的な思考傾向で分岐ツリーを体系的に把握
- 微細なプロパティ変化程度ならインスペクタから調整可能
- ベストダメージ分析（ブルートフォース）を「判断基準がない時の基礎的な判断要素」として位置づけ

#### シミュレートの設計意図
- 厳密にBM上での実行計算を再現する必要はない
- 簡易シミュレートにすることで「予想と違った！」感が出る
- そのフィードバックは被害記録から得る（将来）

#### AI思考要素の実装粒度の判断基準

思考要素（11.2参照）を実装する際、「基底クラスのヘルパー/ユーティリティにするか」「派生AIで直接手書きか」の判断が必要になる。ストック・トリガー実装の経験から抽出した基準:

**3層に分けて考える:**

| 層 | 内容 | 判断基準 | 例 |
|---|---|---|---|
| **情報取得**（前提層） | 内部状態の読み取り・集約 | **判断ロジックの方針に関係なく、その要素を扱うなら常に必要。** private/protectedなフィールドへのアクセスが必要、または複数プロパティの組み合わせ計算が必要ならユーティリティ化する | `GetStockInfo`（充填率・残りターンの計算）、`GetTriggerInfo`（privateな_triggerCountへのアクセス） |
| **判断ロジック** | いつ・なぜその行動を選ぶか | 下記の「判断ロジックのヘルパー化判断」を参照 | 下記参照 |
| **操作・コミット** | 決定をゲーム状態に反映する | 既存のBMパイプライン（CommitDecision等）で対応できるなら**新しいコミット関数は作らない**。既存パイプラインに副作用の問題がある場合はフラグ方式等で迂回する | トリガー=通常スキル選択と同じ、ストック=AIDecision.IsStockフラグ追加（SKillUseCallの副作用回避） |

> **重要:** 情報取得は「前提層」であり、判断ロジックをヘルパー化するか手書きにするかに関わらず必要になる。手書きAIが`Plan()`内でストックの溜め判断を書く場合でも、`GetStockInfo()`がなければ充填率や残りターンを自力計算する必要があり、BM内部構造への依存が派生AI側に漏れる。情報取得ユーティリティは「手書きAIのための道具」であり、ヘルパー化の判断とは独立した前提として常に検討する。

**判断ロジックのヘルパー化判断（最重要）:**

判断ロジックは一律「手書き」ではない。**その判断がキャラの戦略に依存するかどうか**で分かれる:

| 判断の性質 | 方針 | 例 |
|---|---|---|
| **キャラ共通の基礎判断** — どのキャラでも問う汎用的な問い。パラメータ調整で差を出せる | **ヘルパー化する**（基底クラスにvirtualメソッドとして配置、Inspectorで調整可能に） | `AnalyzeBestDamage`（4.1）: 「どのスキルが最もダメージを出すか」は全キャラ共通の基礎問題。SkillAnalysisPolicyのInspector設定で分析方針を調整でき、SelectSkill()のoverrideで丸ごと差し替えも可能 |
| **キャラ固有の戦略判断** — キャラごとに判断軸自体が異なる。汎用化すると引数が発散する | **手書き**（派生AIのPlan()内で直接記述） | ストックの溜め判断: キャラAはHP条件で溜め、キャラBはターン数で溜め、キャラCは敵パッシブを見て溜める→`ShouldStock(fillRate, hpRatio, turnCount, ...)`のような関数にしても引数が発散し、結局全派生でoverrideするため関数として成立しない |

AnalyzeBestDamageは**ヘルパー化の正の例**（部品化すべきケース）、ストック・トリガーの溜め判断は**負の例**（部品化すべきでないケース）。この両方のケースを踏まえて判断する。

**判断のための問い:**

1. 「派生AIを書くとき、毎回同じボイラープレートを書くことになるか？」→ Yes ならユーティリティ化
2. 「そのロジックはキャラ固有の戦略に依存するか？」
   - **Yes（キャラごとに判断軸が異なる）** → 手書き（Plan()内）
   - **No（どのキャラでも問う基礎判断）** → ヘルパー化。virtualメソッドとして基底に置き、Inspectorのパラメータで調整+必要なら派生でoverride
3. 「既存のBM機構でそのまま実行できるか？」→ Yes なら新しいコミットパスは不要

**具体例: 未実装要素への適用**

| 要素 | 情報取得（常に必要） | 判断ロジック（ヘルパー化要否を検討） | 操作 |
|---|---|---|---|
| 逃走思考（11.1.4） | HP残量・被害履歴の取得→ユーティリティ候補 | 「いつ逃げるか」→手書き | `IsEscape=true`で既存パイプライン |
| 被害記録（11.2） | 記録の蓄積・参照→ユーティリティ候補 | 記録からの行動変更→手書き | — |
| 命中回避シミュレート（11.1.6） | シミュレート関数→ヘルパー候補（AnalyzeBestDamageと同レベルの基礎部品。「命中率×ダメージ＝期待値」は全キャラ共通の問い） | 期待値をどう使うか→手書き | — |
| Freeze中断（11.1.5） | 場の状況取得→ユーティリティ候補 | 「いつ中断するか」→手書き（相当思慮深いキャラ限定） | `TurnOnDeleteMyFreezeConsecutiveFlag()`で既存機構 |

**この基準の根拠:** エコシステム型（前述）を断念した理由と同じ。「判断」のうちキャラ固有の戦略部分は無限の組み合わせがありフレームワーク化が非現実的。一方「情報取得」はBMの内部構造に依存する定型処理であり、派生AIの実装者がBMの詳細を知らなくても使える形にしておく価値がある。「基礎判断」はその中間で、判断を含むが全キャラ共通のためヘルパー化する価値がある（AnalyzeBestDamageが代表例）。

**3層の依存関係:** 情報取得→判断ロジック→操作の順に依存する。特に情報取得は、判断ロジックがヘルパーであれ手書きであれ、その要素をAIが扱う限り必ず必要になる前提層。新しいAI思考要素を実装する際は、まず情報取得ユーティリティを整備し、その上で判断ロジックのヘルパー化要否を検討する、という順序で進める。

---

### 11.7 戦闘後AI将来拡張（自動選定エンジン構想）

現行は「人リスト部品（BuildAltruisticTargetList）＋明示Plan」方式。将来的に部品側で「人→最適スキル」まで自動生成するAPIに拡張可能。

#### 段階的移行ステップ
1. **現行維持**: `BuildAltruisticTargetList`＋明示Plan
2. **最小導入**: `ISkillScorer`導入、選定の共通化（ログ/シード対応）。新規150〜300行
3. **半自動**: Top-K生成API追加でPlan薄型化。新規400〜800行
4. **フル自動**: `GeneratePostBattleActions`相当を実装。新規800〜1500行

#### 自動選定の概要（将来API案）
```
ActionGenPreference（評価重み）
  ├── IncludeDead: 死亡者も候補に含める（DeathHeal想定）
  ├── HealBias: 回復優先度（0〜1）
  ├── BuffBias: 付与優先度（0〜1）
  └── MaxActions: 最大アクション数

GeneratePostBattleActions(self, allies, pref)
  1. 人選（BuildAltruisticTargetListそのまま）
  2. スキル候補抽出
  3. 粗い候補（スキル×対象）生成＋Top-K剪定
  4. 詳細スコアリング（ISkillScorer）
  5. 制約充足（OnlyOthers/MaxActions/GroupHelp）
  6. 説明ログ出力
```

**メリット**: Plan記述量の大幅削減、共通評価基準の統一
**デメリット**: Plan側での裁量が減る（個性の出し方が狭まる）
**推奨**: 必要になるまで現行方式を維持。導入時は低→中→高の順で段階的に

---

## 改訂履歴

| 日付 | 内容 |
|---|---|
| 2026-03-06 | 初版作成。現行実装（BattleAIBrain + SimpleRandomTestAI）に基づく仕様書 |
| 2026-03-06 | Freeze復元統一(ResumeFreezeSkill)反映、AIDecision整理反映、未実装思考部品セクション追加 |
| 2026-03-06 | セクション11を元メモ(敵思考AI.md)の全内容で拡充: 思慮要素一覧、思慮レベル/精神レベル・トラウマ率/AIAPI構想を独立セクション化、設計思想、戦闘後AI将来拡張 |
| 2026-03-07 | ストックBMリファクタリング+AI部品ユーティリティ実装反映: AIDecision.IsStock追加(2.2)、CommitDecisionストック分岐・分岐順序追加(2.3-2.4)、ストック・トリガーユーティリティ追加(4.4)、11.1.1/11.1.2を実装済みに更新 |
| 2026-03-07 | 11.6にAI思考要素の実装粒度の判断基準を追加（情報取得/判断ロジック/操作の3層分類、未実装要素への適用例） |
| 2026-03-07 | 11.6改善: 判断ロジックのヘルパー化判断を追加（AnalyzeBestDamage=正の例、ストック溜め判断=負の例）、問い2を精緻化、11.1.1/11.1.2から11.6への参照追加 |
| 2026-03-07 | 11.6補強: 情報取得を「前提層」として明示。判断ロジックのヘルパー化要否とは独立して常に必要である旨を強調、3層の依存関係と実装順序を追記 |
| 2026-03-07 | 2.6追加: AI思考ログ（LogThinkシステム）の仕様。SkillActRun/MustSkillSelect/Single・Groupダメージ分析にログ挿入 |
| 2026-03-07 | Phase 1基盤整備完了: 4.5(基盤ユーティリティ)・4.6(BasicTacticalAI)追加、5章をシミュレート関数に改題+SimulateHitRate追記、SkillAnalysisPolicy表にuseExpectedDamage/considerVanguardForHit追加、1.1にBasicTacticalAI追加、11.1.4/11.1.6を実装済みに更新、8章にBasicTacticalAI追加 |
