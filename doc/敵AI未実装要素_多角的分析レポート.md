# 敵AI未実装要素 多角的分析レポート

本書は敵AI仕様書（`doc/敵AI仕様書.md`）セクション11の未実装要素について、5つの異なる視点からエージェントチームが分析した結果をまとめたものである。

分析日: 2026-03-07
分析対象: 敵AI仕様書 セクション11（未実装・将来構想）
前提: 11.6「AI思考要素の実装粒度の判断基準」（情報取得/判断ロジック/操作の3層フレームワーク）

---

## 目次

1. [エージェントA: 決め打ちAI + 逃走思考の3層分析](#1-エージェントa-決め打ちai--逃走思考の3層分析)
2. [エージェントB: 命中回避シミュレート設計提案](#2-エージェントb-命中回避シミュレート設計提案)
3. [エージェントC: 被害記録・行動記録・トラウマ率設計提案](#3-エージェントc-被害記録行動記録トラウマ率設計提案)
4. [エージェントD: 全体ロードマップと優先順位](#4-エージェントd-全体ロードマップと優先順位)
5. [エージェントE: Freeze中断 + パッシブ読み取り分析](#5-エージェントe-freeze中断--パッシブ読み取り分析)
6. [横断的発見・論点](#6-横断的発見論点)
7. [統合ロードマップ](#7-統合ロードマップ)

---

## 1. エージェントA: 決め打ちAI + 逃走思考の3層分析

**担当:** 11.1.3 決め打ちAI、11.1.4 逃走思考
**視点:** 「今すぐ作れる部品」として何をユーティリティ化し、何を手書きに残すか

### 1.1 決め打ちAI（11.1.3）

#### 情報取得層

| 想定パターン | 必要情報 | アクセス手段 | ユーティリティ化要否 |
|---|---|---|---|
| HPが半分以下で特定スキル | `user.HP`, `user.MaxHP` | public | 不要（直接アクセス可能） |
| Nターン目に固定スキル | 現在のターン数 | **存在しない** | **必要（重要発見）** |
| スキル1つしかない | `availableSkills.Count` | protected | 不要 |

**重要発見: ターン数取得が基底クラスに存在しない。** `IBattleContext`にターン数getterがあるか確認が必要。なければ基底カウンタを追加すべき。決め打ちAIだけでなく、多くのAIパターンで「Nターン目に何をする」は必須の前提情報。

#### 判断ロジック層

仕様書の「Plan()に直書きで十分」は**概ね正しい**。

Inspector化テーブル（条件→スキルのマッピング）を基底に置く案も検討したが、以下の理由で不要:
- 仕様書11.6が「エコシステム型を断念した」と明記しており、条件テーブルはエコシステムの部分実装に相当
- 決め打ちの条件分岐は典型的に3〜10行程度。Plan()直書きコストは極めて低い
- 条件が複雑化するキャラでは結局overrideが必要になり、二重管理になる

**ただし1つだけ部品化の余地がある:** 特定スキルを名前/IDで引くヘルパー。毎回`.FirstOrDefault(s => s.SkillName == "xxx")`を書くのはボイラープレート。

```csharp
// 候補案
protected BaseSkill FindSkill(string name)
    => availableSkills?.FirstOrDefault(s => s != null && s.SkillName == name);
protected BaseSkill FindSkill(int id)
    => availableSkills?.FirstOrDefault(s => s != null && s.SkillID == id);
```

#### 操作・コミット層

既存パイプラインで完全対応可能。`decision.Skill = fixedSkill;` で済む。

#### 決め打ちAI まとめ

| 層 | 提案 | 優先度 |
|---|---|---|
| 情報取得 | ターン数取得の整備 | **高**（多くのAIで必要） |
| 情報取得 | `FindSkill(name/id)` | 中 |
| 判断ロジック | 手書き（Plan()直書き）。テーブル化不要 | — |
| 操作 | 変更不要 | — |

---

### 1.2 逃走思考（11.1.4）

#### 情報取得層

| レベル | 必要情報 | アクセス手段 | ユーティリティ化要否 |
|---|---|---|---|
| 基本（単純確率） | Inspector設定値のみ | SerializeField | 不要 |
| 上級（HP減少率） | 前ターンHP | **未保持** | **必要** |
| 上級（被害履歴） | 被害記録 | **未実装** | 将来（被害記録システムが先決） |
| 高度（トラウマ率） | トラウマ率 | **未実装** | 将来 |

HP減少率について: 現行BattleAIBrainは前ターンHPを記録する仕組みがない。逃走以外にも「HPが急減したら行動を変える」は汎用パターンなので、基底に置く価値がある。

```csharp
// 候補案
protected float HpRatio => user.MaxHP > 0 ? user.HP / user.MaxHP : 0f;
protected float HpDropThisTurn => _lastTurnHP - user.HP;
protected float HpDropRate => _lastTurnHP > 0 ? HpDropThisTurn / _lastTurnHP : 0f;
```

#### 判断ロジック層（最重要の分析ポイント）

**基本レベル（単純確率逃走）はヘルパー化すべき。**

11.6の問いに当てはめる:
1. 「毎回同じボイラープレートを書くか？」→ **Yes。** 逃走可能な敵を複数作る場合、毎回`if (Roll(escapeChance)) { decision.IsEscape = true; return; }`を書く
2. 「キャラ固有の戦略に依存するか？」→ **No（基本レベルに限り）。** 「毎ターンX%で逃げる」はパラメータの違いでキャラ差を出せる

**これはAnalyzeBestDamageと同じ構造のヘルパー化正例。**

```csharp
// 設計案
[Header("逃走設定")]
[SerializeField, Range(0f, 1f)] float _escapeChance = 0f;
[SerializeField] bool _canEscape = false;

/// <summary>
/// 基本逃走判定。上級判断をしたいキャラはoverrideする。
/// </summary>
protected virtual bool ShouldEscape()
{
    if (!_canEscape) return false;
    return Roll(_escapeChance);
}
```

注意: `ShouldEscape()`をPlan()の外で自動実行するかは慎重に判断。仕様書に「逃走コールバック」が上級想定として挙げられているので、**呼び出しタイミングは派生AIに委ねる**（道具として提供するが、いつ使うかはPlan()内の手書き）のが適切。

**上級・高度レベルは手書き。** HP減少率やトラウマ率による逃走判断はキャラごとに判断軸が異なる。ストック溜め判断と同じ構造。

#### 操作・コミット層

既存パイプラインで完全対応可能。`decision.IsEscape = true;`で済む。

#### 逃走思考 まとめ

| 層 | 提案 | 優先度 |
|---|---|---|
| 情報取得 | `HpRatio` プロパティ | **高**（逃走以外にも広く使う） |
| 情報取得 | `_lastTurnHP` + `HpDropRate` | 中（上級逃走の前提） |
| 判断ロジック | `ShouldEscape()` virtual + Inspector | **高**（ヘルパー化正例） |
| 判断ロジック | 上級・高度は手書き（override） | — |
| 操作 | 変更不要 | — |

#### 仕様書11.6への修正提案

現行:
> 逃走思考（11.1.4）: 判断ロジック → 「いつ逃げるか」→手書き

修正案:
> 逃走思考（11.1.4）: 判断ロジック → **基本レベル（単純確率）はヘルパー化**（`ShouldEscape()` virtual + Inspector）。**上級・高度レベルは手書き**（override）

---

## 2. エージェントB: 命中回避シミュレート設計提案

**担当:** 11.1.6 命中回避シミュレート部品
**視点:** AnalyzeBestDamageとの統合設計、BMの命中回避ロジック調査

### 2.1 BMの命中回避ロジック全容

コードを精読した結果、BMの命中回避は**3段階**で構成されている。

#### 第1段階: キャラクター間の命中回避計算 (IsReactHIT)

場所: `BaseStates.ReactionSkill.cs` 61行目

入力要素:
- **攻撃者EYE**: `Attacker.EYE().Total` — 基礎命中 + パッシブ補正 + 範囲意志ボーナス + 単体攻撃時AGI/6補正
- **被害者AGI**: `AGI().Total` — 基礎回避 + パッシブ補正
- **前のめり(攻撃者)**: 非前のめり時かつ`skill.AggressiveOnExecute.canSelect`なら被害者AGI*0.2を命中から減算
- **回避補正率**: `SkillEvasionModifier` — スキルによる回避変動 + 落ち着きカウントで線形減衰
- **先手補正**: 先手攻撃時は回避率を`AGI*0.7`に固定
- **パッシブ回避補正**: `PassivesEvasionPercentageModifierByAttacker()`

計算式:
```
EvasionRate = AGI * SkillEvasionModifier * PassivesModifier
hitChance = rand(0, EYE + EvasionRate) < (EYE - minusMyChance)
```

#### 第2段階: スキル命中率 (SkillHitCalc)

場所: `BaseSkill.SkillHit.cs` 26行目

入力要素:
- **スキル命中率**: `SkillHitPer` (百分率整数)
- **命中凌駕ボーナス**: `AccuracySupremacy(EYE, AGI)` — EYEがAGIの2.5倍超過分の1/2
- **魔法フラグ**: `IsMagic` — 失敗時に1/3でかすり

#### 第3段階: 特殊ケース

- ミニマムヒットチャンス（ケレン行動率ベース）
- 爆破型+被害者前のめり → 完全回避がかすりに格下げ
- 味方別口回避（パーティー属性依存）
- 善意攻撃の簡略化

### 2.2 AnalyzeBestDamageとの関係

現状の`AnalyzeBestDamage`は「当たった場合のダメージ」だけを返し、命中率の概念が一切入っていない。

**設計判断: 独立関数として作り、AnalyzeBestDamageからフラグで呼べるようにする。**

AnalyzeBestDamageに直接組み込まない理由:
1. 「純ダメージで最強を選ぶ」という明確な責務を持っており、命中率を混ぜると意味が変わる
2. 命中率を加味すると「低ダメだが確実に当たるスキル」が選ばれうるが、それは別の判断軸
3. 刻み・ブレ段階システムとの組み合わせが複雑化
4. 派生AIによっては「命中率無視で最大ダメージ」を選びたい場面がある

### 2.3 設計案

#### SimulateHitRate（情報取得層）

配置: `BaseStates.BattleBrainSimlate.cs` に追加

```csharp
/// <summary>
/// AI用の簡易命中率シミュレート。
/// IsReactHIT + SkillHitCalc を確率論的に近似。
/// 乱数を使わず、確定的な期待命中率(0.0~1.0)を返す。
/// </summary>
public float SimulateHitRate(BaseStates attacker, BaseSkill skill, HitSimulatePolicy policy)
{
    // 第1段階: キャラ間命中回避
    float eye = attacker.EYE().Total;
    float agi = AGI().Total;

    float minusChance = 0f;
    if (policy.considerVanguard && !policy.attackerIsVanguard
        && skill.AggressiveOnExecute.canSelect)
    {
        minusChance = agi * 0.2f;
    }
    minusChance = Mathf.Min(minusChance, eye);

    float evasionRate = agi; // 簡易版: SkillEvasionModifier=1.0f想定
    float effectiveEye = eye - minusChance;
    float charHitRate = Mathf.Clamp01(
        effectiveEye / Mathf.Max(effectiveEye + evasionRate, 0.001f));

    // 第2段階: スキル命中率
    float skillHitRate = skill.SkillHitPer / 100f;
    float supremacy = (eye >= agi * 2.5f) ? (eye - agi * 2.5f) / 2f : 0f;
    skillHitRate = Mathf.Clamp01(skillHitRate + supremacy / 100f);

    // 統合
    float baseHitRate = charHitRate * skillHitRate;

    // 第3段階: 特殊補正（簡易近似）
    if (policy.considerVanguard
        && skill.DistributionType == AttackDistributionType.Explosion
        && policy.targetIsVanguard)
    {
        float evadeRate = 1f - baseHitRate;
        baseHitRate += evadeRate * 0.7f; // かすり化近似
    }
    if (skill.IsMagic)
    {
        float evadeRate = 1f - baseHitRate;
        baseHitRate += evadeRate * 0.33f; // 魔法かすり
    }

    return Mathf.Clamp01(baseHitRate);
}
```

#### HitSimulatePolicy

```csharp
[Serializable]
public struct HitSimulatePolicy
{
    public bool considerVanguard;
    public bool attackerIsVanguard;
    public bool targetIsVanguard;

    public static HitSimulatePolicy FromBattleState(
        IBattleContext ctx, BaseStates attacker, BaseStates target)
    {
        return new HitSimulatePolicy
        {
            considerVanguard = true,
            attackerIsVanguard = ctx.IsVanguard(attacker),
            targetIsVanguard = ctx.IsVanguard(target),
        };
    }

    public static HitSimulatePolicy Minimal => new HitSimulatePolicy
    {
        considerVanguard = false,
    };
}
```

#### BattleAIBrain基底のラッパー

```csharp
protected float EstimateHitRate(BaseStates target, BaseSkill skill, bool considerVanguard = true)
{
    var policy = considerVanguard
        ? HitSimulatePolicy.FromBattleState(manager, user, target)
        : HitSimulatePolicy.Minimal;
    return target.SimulateHitRate(user, skill, policy);
}

protected float EstimateExpectedDamage(BaseStates target, BaseSkill skill, bool considerVanguard = true)
{
    float damage = target.SimulateDamage(user, skill, _damageSimulatePolicy);
    float hitRate = EstimateHitRate(target, skill, considerVanguard);
    return damage * hitRate;
}
```

#### SkillAnalysisPolicyへのフラグ追加

```csharp
// 既存構造体に追加
[Header("命中率シミュレーション")]
[Tooltip("trueの場合、期待ダメージ(ダメージ x 命中率)で評価する")]
public bool useExpectedDamage;
[Tooltip("前のめり状態を命中率計算に反映するか")]
public bool considerVanguardForHit;
```

`useExpectedDamage = false`（デフォルト）なら従来通りの純ダメージ評価。`true`にすると命中率込みの期待値で評価。既存動作に影響なし。

### 2.4 意図的に再現しない要素

| BMの要素 | シミュレートでの扱い | 理由 |
|---|---|---|
| ミニマムヒットチャンス | **無視** | 確率的揺らぎ。「当たると思ったら外れた」を演出 |
| 落ち着きカウント | **無視**（1.0固定） | ターン経過で変動。スナップショットでは不正確 |
| 味方別口回避 | **無視** | AI思考要素としての価値が低い |
| パッシブ由来回避補正 | **無視** | 仕様書で「パッシブは部品に組み込まない」と明記 |
| 先手攻撃補正 | **無視** | 初ターンのみの一時的効果 |
| 爆破型+前のめりかすり化 | **簡易近似** | 前のめりの主要影響の一つ |
| 魔法かすり | **近似** | 1/3かすり化を確率加算 |

この「意図的な不完全さ」が仕様書11.6の設計思想（「予想と違った！」感）と整合する。

### 2.5 実装ステップ

1. `HitSimulatePolicy`構造体を定義
2. `BaseStates.BattleBrainSimlate.cs`に`SimulateHitRate`追加
3. `BattleAIBrain.cs`に`EstimateHitRate`/`EstimateExpectedDamage`ラッパー追加
4. `SkillAnalysisPolicy`にフラグ追加
5. Analyzerのdamage取得部分を拡張（フラグ分岐）

### 2.6 関連ファイルパス

- `BaseStates.ReactionSkill.cs` — BM命中回避 `IsReactHIT`（61行目）
- `BaseStates.EvasionCalc.cs` — 回避率計算 `EvasionRate`, `SkillEvasionModifier`
- `BaseStates.HitCalc.cs` — 命中凌駕 `AccuracySupremacy`
- `BaseSkill.SkillHit.cs` — スキル命中率 `SkillHitCalc`, `SkillHitPer`
- `BaseStates.StatesNumber.cs` — `EYE()`(456行), `AGI()`(513行)

---

## 3. エージェントC: 被害記録・行動記録・トラウマ率設計提案

**担当:** 11.2 記録・履歴系、11.4 トラウマ率
**視点:** 記録の保持場所問題、トラウマ率の計算式、思慮レベルとの連携

### 3.1 記録の蓄積場所

| 候補 | メリット | 致命的な問題 |
|---|---|---|
| BattleAIBrain (SO) | 戦闘間で永続、Inspector設定と同居 | 同一SOアセットを複数NormalEnemyが共有 → 記録が混ざる |
| BaseStates | 個体に紐づく | ゲームリスタート時に消える |
| **NormalEnemyにBattleMemory** | 個体固有、SO共有問題なし | 参照経路の追加が必要 |

**結論: NormalEnemyに`BattleMemory`インスタンスを保持。**

NormalEnemyは`_enemyGuid`で個体識別済み。BrainのPlan()からは`user`（= NormalEnemy）経由でアクセス。

```csharp
// BaseStates に追加（基底はnull。主人公側は使わない）
public virtual BattleMemory BattleMemory => null;

// NormalEnemy で override
[NonSerialized] private BattleMemory _battleMemory = new();
public override BattleMemory BattleMemory => _battleMemory;
```

Brainには**パラメータ**（しきい値・思慮レベル等）だけを持たせ、**状態**（記録・トラウマ率）は個体に持たせる。`user`フィールドが毎ターン差し替わる既存設計と整合する。

### 3.2 BattleMemory データ構造

```csharp
[Serializable]
public class BattleMemory
{
    // ===== 被害記録 =====
    private readonly DamageRecord[] _damageRecords = new DamageRecord[16]; // リングバッファ
    private int _damageRecordIndex = 0;
    private int _damageRecordCount = 0;

    public int InterruptCounterCount { get; private set; }      // カウンター発動回数（累計）
    public int InterruptCounterTrialCount { get; private set; }  // カウンター判定回数（累計）

    // ===== 行動記録 =====
    private readonly ActionRecord[] _actionRecords = new ActionRecord[16]; // リングバッファ
    private int _actionRecordIndex = 0;
    private int _actionRecordCount = 0;

    // ===== トラウマ率 =====
    public float TraumaRate { get; private set; }          // 0.0〜1.0
    public float LastBattleHpLossRatio { get; private set; } // 再戦時即逃走判定用
}
```

#### DamageRecord（被害記録1件）

```csharp
[Serializable]
public struct DamageRecord
{
    public string AttackerName;
    public string SkillName;
    public float PhysicalDamage;
    public float MentalDamage;
    public float DefAtkRate;           // 防御無視率（カウンター発動確率に直結）
    public bool WasInterruptCountered;
    public int ConsecutiveHitIndex;    // 連続攻撃の何回目か（0=初回/単発）
    public bool WasFreezeConsecutive;
}
```

#### ActionRecord（行動記録1件）

```csharp
[Serializable]
public struct ActionRecord
{
    public string SkillName;
    public float TotalDamageDealt;       // 物理ダメージ合計
    public float TotalMentalDamageDealt; // 精神ダメージ合計
    public bool AnyHit;
    public bool WasInterrupted;          // カウンターで中断されたか
}
```

#### サイズ上限

リングバッファで直近16件固定。根拠:
- 1戦闘のターン数は10〜30程度。16あれば直近1〜2戦分を保持可能
- 固定長structでGCアロケーションゼロ
- トラウマ率自体は累積値なので、個別記録が上書きされても蓄積結果は失われない

### 3.3 記録の書き込みフック

| タイミング | 場所 | 記録内容 |
|---|---|---|
| 被害を受けた時 | `BaseStates.ReactionSkill.cs` DamageOnBattle後 | `RecordDamage(...)` |
| カウンター判定時 | `BaseStates.BattleEvent.cs` TryInterruptCounter内 | `RecordInterruptCounterTrial(succeeded)` |
| 自分の攻撃完了時 | SkillExecutor完了後 | `RecordAction(...)` |
| 戦闘終了時 | OnBattleEnd | `UpdateTraumaOnBattleEnd(...)` |

BM側への変更は各箇所1行の呼び出し追加のみ。

### 3.4 情報取得ユーティリティ（BattleAIBrain基底に追加）

```csharp
protected BattleMemory GetMemory() => user?.BattleMemory;

protected float GetInterruptCounterRate()
{
    var mem = GetMemory();
    if (mem == null || mem.InterruptCounterTrialCount == 0) return 0f;
    return (float)mem.InterruptCounterCount / mem.InterruptCounterTrialCount;
}

protected float GetTraumaRate() => GetMemory()?.TraumaRate ?? 0f;

/// <summary>
/// スキルの「代表防御無視率」。連続攻撃は全MoveSetの最大DefAtk。
/// </summary>
protected float GetRepresentativeDefAtk(BaseSkill skill) { ... }
```

BaseSkillにも追加が必要:

```csharp
public float BaseDefAtk => FixedSkillLevelData[_levelIndex].DefAtk;
public float GetMaxMoveSetDefAtk() { ... } // 全MoveSetのDEFATK最大値
```

### 3.5 トラウマ率の計算式

#### 蓄積（戦闘終了時）

```csharp
private float CalculateTraumaIncrement(float hpLossRatio, float interruptRate)
{
    // カウンター寄与（主要因）
    float counterTrauma = interruptRate * 0.15f;
    // HP急減寄与（補助因、HPが半分以上減った場合のみ）
    float hpTrauma = hpLossRatio > 0.5f ? (hpLossRatio - 0.5f) * 0.10f : 0f;
    return counterTrauma + hpTrauma;
}
```

#### 減衰（戦闘開始時）

```csharp
public void DecayTrauma(float decayRate = 0.05f)
{
    TraumaRate = Mathf.Max(0f, TraumaRate - decayRate);
}
```

#### 連続攻撃のトラウマ回避しきい値

```
使用可否 = スキルの代表防御無視率 < トラウマしきい値
トラウマしきい値 = BaseThreshold - TraumaRate * ThresholdDropRate
```

```csharp
// BattleAIBrain Inspector設定
[SerializeField, Range(0f, 1f)] float _traumaBaseThreshold = 0.8f;
[SerializeField, Range(0f, 1f)] float _traumaThresholdDropRate = 0.5f;

protected float GetTraumaAvoidanceThreshold()
{
    return _traumaBaseThreshold - GetTraumaRate() * _traumaThresholdDropRate;
}

protected bool IsTraumaAvoided(BaseSkill skill) { ... }
```

具体例:

| トラウマ率 | しきい値(Base=0.8, Drop=0.5) | 効果 |
|---|---|---|
| 0.0（初見） | 0.8 | ほぼ制限なし |
| 0.3（少しビビり） | 0.65 | 高DefAtkスキルを回避 |
| 0.6（かなりビビり） | 0.5 | 中程度でも回避 |
| 1.0（完全トラウマ） | 0.3 | 大半の連続攻撃を使えない |

### 3.6 思慮レベルとの連携

**核心的な切り分け:**

| | 思慮レベル不要 | 思慮レベル必要 |
|---|---|---|
| **トラウマ回避**（連続攻撃忌避） | 感情反応（本能）→ 全キャラ発動 | |
| **被害記録→戦略変更**（防御vs回復切替） | | 分析→思慮レベル高で発動 |

根拠: 仕様書11.4に「トラウマ率は思慮推測のレベル（頭の良さ）とは独立した、感情・経験由来のパラメータ」と明記。ビビっている個体は頭が悪くてもビビる。一方、被害記録を分析して戦略を変えるのは頭の良さ。

```csharp
[Header("思慮推測レベル（0=本能のみ, 1=基本, 2=中程度, 3=高度）")]
[SerializeField, Range(0, 3)] int _deliberationLevel = 0;
protected int DeliberationLevel => _deliberationLevel;

// トラウマ回避は思慮レベル不問 → IsTraumaAvoided() は常に使用可能
// 被害記録の高度利用は思慮レベルで制御
protected bool CanAnalyzeDamageRecords => _deliberationLevel >= 2;
```

### 3.7 3層フレームワーク適用まとめ

| 層 | 内容 | 方針 |
|---|---|---|
| 情報取得 | GetMemory, GetInterruptCounterRate, GetTraumaRate, GetRepresentativeDefAtk, GetRecentDamageRecords, GetBestPerformingAction | **常に必要。基底に配置** |
| 判断ロジック | トラウマによる連続攻撃回避 | **ヘルパー化**（キャラ共通基礎判断。Inspector設定可能） |
| 判断ロジック | 被害記録からの防御/回復切替 | **手書き**（キャラ固有戦略） |
| 判断ロジック | 再戦時即逃走判定 | **ヘルパー化候補**（逃走判断と一緒に設計） |
| 判断ロジック | 行動記録からのリソース配分 | **手書き**（キャラ固有戦略） |
| 操作 | 全て既存パイプラインで対応可能 | 変更不要 |

---

## 4. エージェントD: 全体ロードマップと優先順位

**担当:** 全未実装要素の俯瞰
**視点:** 依存関係、コスト/効果評価、フェーズ分け、次に作るべきAI

### 4.1 依存関係マップ

```
[前提基盤]
  ├── (A) 自HP/精神HP情報取得ユーティリティ
  │     ├──→ (D) 逃走思考
  │     └──→ (G) 思慮推測レベル基盤
  │
  ├── (B) 被害記録参照ユーティリティ
  │     ├──→ (E) トラウマ率
  │     ├──→ (H) 行動記録参照
  │     └──→ (G) 思慮推測レベル基盤
  │
  ├── (C) 命中回避シミュレート部品
  │     └──→ (I) 期待ダメージ統合
  │
  ├── (F) スキル使用率
  └── (J) 決め打ちAI → (A)が前提

[中間層]
  ├── (D) 逃走思考 → (A)前提、(B)(E)でリッチに
  ├── (E) トラウマ率 → (B)前提
  ├── (I) 期待ダメージ = (C) × AnalyzeBestDamage
  └── (G) 思慮推測レベル基盤 → 全情報取得部品が前提

[高度層]
  ├── (K) Freeze中断判断 → (G) + 高度な組み合わせ
  ├── (L) 精神レベル → (G)と独立軸
  ├── (M) AIAPI → 全部品完成後
  └── (N) 戦闘後AI自動選定エンジン → 現行方式で十分なうちは不要
```

### 4.2 優先順位テーブル

| # | 要素 | コスト | 効果 | 依存される度 | Phase |
|---|---|---|---|---|---|
| A | 自HP/精神HP情報取得 | **低**(20-40行) | 中 | **極高** | **1** |
| B | 被害記録参照ユーティリティ | **低**(30-60行) | 中 | **高** | **1** |
| C | 命中回避シミュレート | **中**(80-150行) | **高** | **高** | **1** |
| D | 逃走思考（基本） | **低**(30-50行) | **高** | 低 | **1** |
| F | スキル使用率（重み付け） | **低**(40-60行) | 中 | 低 | **1** |
| I | 期待ダメージ統合 | **低-中**(30-50行) | **高** | 中 | **2** |
| H | 行動記録参照 | **低**(30-50行) | 低-中 | 中 | **2** |
| E | トラウマ率 | **中**(100-200行) | 中 | 中 | **2** |
| G | 思慮推測レベル基盤 | **中-高**(150-300行) | 中 | **高** | **3** |
| L | 精神レベル | **中**(80-120行) | 中 | 低 | **3** |
| K | Freeze中断判断 | **高**(200-400行) | 低 | なし | **3** |
| N | 戦闘後AI自動選定 | **高**(段階的) | 低 | なし | **3+** |
| M | AIAPI構想 | **極高** | 高 | なし | **4** |

精神レベルの効果: 精神分析力に基づくdamageType切り替え（dmg/mentalDmg）、spiritualModifier活用判断。思慮推測レベルとは独立した軸（6.3参照）。

### 4.3 今の基底クラスに足りないもの

#### 情報取得の欠落（最重要）

| 不足 | 影響 |
|---|---|
| 自キャラ状態のヘルパーがない | `HPRatio`, `IsLowHP(threshold)` 等。全派生AIでボイラープレート |
| 敵グループ列挙が不便 | `potentialTargets`を取る標準的手段がない |
| 被害記録への窓口がない | `user.damageDatas`はpublicだが集約関数がない |
| 行動記録への窓口がない | 同上 |
| 前のめり状態の簡易取得がない | 命中回避シミュレートの前提不足 |

#### Plan()のコンテキスト不足

| 不足 | 影響 |
|---|---|
| ターン数が取れない | 決め打ちパターンが書けない |
| 先約ターゲット情報がPlan()に渡されない | 先約対象への最適スキル判断ができない |

#### 構造的な不便さ

| 不足 | 影響 |
|---|---|
| 敵グループ列挙関数がない | EnumerateGroupAllies（味方列挙）はあるが対称が欠如 |
| AnalyzeBestDamageの結果にTarget情報を渡すパスがない | グループ分析でTarget決定してもコミットできない |
| **PostBattleDecision.AddAction()のSkills未設定** | AddAction()で追加したアクションが実質空になるバグの可能性 |

### 4.4 SimpleRandomTestAIの次: BasicTacticalAI

Phase 1の部品が揃った後に作る最小限の賢いAI:

```
BasicTacticalAI（60-100行）
  Plan(decision):
    1. [逃走] HPRatio < threshold && Roll(chance) → IsEscape
    2. [ストック] ストック可能でFillRate < 1.0 → IsStock
    3. [スキル選択] AnalyzeBestDamage で最大ダメージスキル
    4. [範囲/対象] ZoneTraitに応じた基本設定
    5. [フォールバック] availableSkills[0]

  OnFreezeOperate: 基本的な範囲/対象の再設定
  PostBattlePlan: BuildAltruisticTargetList → SelectMostEffectiveHealSkill
```

SimpleRandomTestAIとの違い:
- ダメージ最大化（ブルートフォース分析）
- 生存判断（HP低下時逃走）
- ストック活用
- 戦闘後回復
- Phase 2で命中率統合・トラウマ追加により段階的に賢くなる設計

---

## 5. エージェントE: Freeze中断 + パッシブ読み取り分析

**担当:** 11.1.5 Freeze中断判断、11.2 敵パッシブ読み取り
**視点:** 高難度要素の設計課題、タイミング問題、AIAPI接点

### 5.1 Freeze中断の現状

**実行パス:**
```
NormalEnemy.SkillAI()
  → BattleAIBrain.SkillActRun()
    → user.IsFreeze == true
      → HandleFreezeContinuation()
        → user.ResumeFreezeSkill()
          → IsDeleteMyFreezeConsecutive==true → DeleteConsecutiveATK() → Cancelled
          → それ以外 → NowUseSkill/RangeWill復元 → Resumed or ResumedCanOperate
```

**重要な制約:** Freeze中はPlan()に到達しない。判断できるタイミングは`HandleFreezeContinuation()`内部のみ。

### 5.2 中断判断に必要な情報

| カテゴリ | 具体的要素 | 現状 |
|---|---|---|
| カウンター危険度 | 対象のカウンター可能状態、自スキルDEFATK、対象PowerLevel | 部分的にpublic |
| 残り攻撃回数 | ATKCount - ATKCountUP | public |
| 対象HP | 残量、あと何発で倒せるか | public |
| 行動履歴 | 被カウンター頻度 | **未実装** |
| ポイント推測 | 相手のリソース状況 | **アクセス不可**（「推測」の領域） |

### 5.3 タイミング問題: ShouldAbortFreeze() virtualフック

**推奨設計:**

```csharp
protected void HandleFreezeContinuation()
{
    // 中断判断フック
    if (ShouldAbortFreeze())
    {
        user.IsDeleteMyFreezeConsecutive = true; // 即時中断
    }

    var result = user.ResumeFreezeSkill();
    // ... 既存処理
}

/// <summary>
/// Freeze中断判断フック。デフォルトは常にfalse。高知能キャラのみoverride。
/// </summary>
protected virtual bool ShouldAbortFreeze() => false;
```

利点:
- 既存構造を壊さない
- 派生AIは`ShouldAbortFreeze()`だけoverrideすればよい
- `IsDeleteMyFreezeConsecutive`直接操作で即時中断可能（publicフィールド）

### 5.4 Freeze情報取得ユーティリティ

```csharp
protected struct FreezeInfo
{
    public BaseSkill Skill;
    public int RemainingHits;
    public int TotalHits;
    public float ProgressRate;
    public bool CanOperate;
    public float RepresentativeDEFATK;
}

protected FreezeInfo GetFreezeInfo() { ... }

protected struct CounterRiskInfo
{
    public bool TargetHasCounterActive;
    public float SkillDEFATK;
    public float EstimatedCounterChance; // DEFATK/3 + Vond差分ベースの推定
}

protected CounterRiskInfo EstimateCounterRisk(BaseStates target, BaseSkill skill) { ... }
```

### 5.5 重要発見: FreezeConsecutiveのカウンター免除

`BaseStates.ReactionSkill.cs` 798行目で確認:
```csharp
if(!skill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
```

**FreezeConsecutive中の攻撃は割り込みカウンター判定がスキップされる。** つまり、Freeze中断の「カウンター回避」理由は、Freeze終了後に通常連続攻撃に切り替わった場合のリスク予測という文脈になる。

### 5.6 パッシブ読み取り

#### パッシブの現状

`target.Passives`はpublicで公開されており、技術的にはAIから完全に読み取り可能。

AIに関係する主要プロパティ:

| カテゴリ | プロパティ |
|---|---|
| 分類 | `IsCantACT`, `CanCancel`, `IsBad`, `IsEternal` |
| ステータス補正 | `ATKFixedValueEffect()`, `DEFFixedValueEffect()` 等 |
| 特殊防御 | `DamageReductionRateOnDamage`, `NormalDontDamageHpMinRatio` |
| 生存条件 | `DurationTurn`, `RemoveOnDamage` 等 |

#### 思慮レベルによる段階的可視性

| 思慮レベル | 読み取れる情報 | 相当するもの |
|---|---|---|
| 0 | パッシブを一切読まない | SimpleRandomTestAI相当 |
| 1 | 行動不能の認知のみ | 外見で分かるレベル |
| 2 | 名前/ID、IsBad/IsCantACT大分類 | UIアイコン拡大レベル |
| 3 | 具体的数値（ATK/DEF倍率、残りターン） | 詳細観察+推測 |
| 4 | 特殊効果（軽減率、VitalLayer構成） | AIAPI向け |

#### 設計案

```csharp
protected struct PassiveSnapshot
{
    public int Id;
    public string Name;
    public bool IsBad;
    public bool IsCantACT;
    public float? DEFModifier;     // レベル3+（nullなら読み取れない）
    public float? ATKModifier;     // レベル3+
    public int? RemainingTurns;    // レベル3+
    public float? DamageReduction; // レベル4+
    public bool? HasVitalLayer;    // レベル4+
}

protected List<PassiveSnapshot> ReadTargetPassives(BaseStates target, int insightLevel) { ... }
```

これは情報取得層の**アクセス制御機構**として位置づけ。

#### パッシブ読みが活きる場面

1. 防御パッシブ検出 → ダメージシミュレート精度向上
2. IsCantACT検出 → 相手が次ターン動けないことを知り安全にFreeze継続
3. ダメージ軽減パッシブ → そのターゲットを避ける
4. VitalLayer検出 → バリア破壊優先度
5. RemoveOnDamage → 攻撃でパッシブを剥がす戦略

### 5.7 AIAPIとの境界設計

| 層 | 手書きAI | AIAPI |
|---|---|---|
| 情報取得 | 同一ユーティリティを使用 | 同じ出力をJSON化してプロンプトに |
| 判断ロジック | C#のif/switch固定判断 | LLMによる柔軟な状況判断 |
| 操作・コミット | 同一パイプライン | 同じ。LLM出力をAIDecisionにマッピング |

情報取得ユーティリティを**手書きAIとAIAPIの共通基盤**として設計することが設計上の核心。返り値がstruct/リストなので、手書きAIはそのまま使い、AIAPIはJSON化してプロンプトに含められる。

---

## 6. 横断的発見・論点

### 6.1 仕様書11.6への修正提案

エージェントAの分析により、逃走思考の分類を精緻化すべき:

> 現行: 逃走思考 → 判断ロジック「いつ逃げるか」→手書き
> 修正案: **基本レベル（単純確率）→ヘルパー化**（ShouldEscape() virtual + Inspector）、**上級・高度→手書き**（override）

これはAnalyzeBestDamageが「基礎判断としてヘルパー化し、SelectSkill()のoverrideで差し替え可能」としている構造と完全に一致。

### 6.2 トラウマの軸の整理

エージェントCの分析で明確化:
- **トラウマ回避 = 感情・本能軸（思慮レベル不問）** — ビビっている個体は頭が悪くてもビビる
- **被害記録の戦略利用 = 知性軸（思慮レベル依存）** — 記録を分析して戦略を変えるのは頭の良さ

仕様書11.4の記述と整合するが、より明確に分離すべき。

### 6.3 精神レベルの分析不足（レポート後の補足）

本レポートの5エージェントはいずれも精神レベルについて具体的な設計提案を出さなかった。エージェントCが2軸（思慮レベル / トラウマ率）の図を描いたが、精神レベルはそこに含まれていない。仕様書11.4の記述が「EQ要素」「精神の削れ具合を見抜く能力」程度で抽象的だったことが原因。

**精神レベルの本質:** 思慮推測レベル（頭の良さ）ともトラウマ率（感情反応）とも異なる、**相手の精神状態を分析する力**。

**本来あるべき3軸:**

```
思慮推測レベル = 頭の良さ（パッシブ読み、行動予測、ポイント推測）
精神レベル    = 精神分析力（敵の精神HPの削れ具合、精神属性相性の見抜き）
トラウマ率    = 感情反応（カウンターされた恐怖、HP急減の記憶）
```

**精神レベルが影響する具体的な場面:**

| 精神レベル | 可能になること | 既存の接点 |
|---|---|---|
| 低 | 精神系の判断はしない。物理ダメージのみで評価 | AnalyzeBestDamageの`damageType = dmg`固定 |
| 中 | 相手の精神HPがどれだけ削れているか見抜ける。精神ダメージを狙う判断が可能 | `damageType = mentalDmg`の選択判断 |
| 中-高 | 精神属性相性を活用したスキル選択 | AnalyzeBestDamageの`spiritualModifier = true`を使うかどうか |
| 高 | 物理で殴るか精神で削るかの最適判断。敵の精神的弱点の把握 | mentalDmgとdmgの比較分析 |

**3層フレームワークへの適用:**
- **情報取得:** 敵の精神HP残量・精神属性の取得ユーティリティ（Phase 1のHP情報取得と同時に整備可能）
- **判断ロジック:** 「精神で攻めるか物理で攻めるか」→ AnalyzeBestDamageのdamageType切り替えとして**ヘルパー化候補**（パラメータ調整で差を出せるキャラ共通基礎判断）
- **操作:** 既存パイプラインで対応可能（スキル選択の結果が変わるだけ）

**思慮推測レベルとの違い:** 思慮レベルが低くても精神レベルが高いキャラは、パッシブは読めないが「こいつ精神的に弱ってるな」は分かる。逆に思慮レベルが高くても精神レベルが低いキャラは、パッシブは読めるが精神攻めの判断は雑。

### 6.4 PostBattleDecision.AddAction()バグ ✅修正済み

エージェントDが発見: `AddAction()`がSkillsフィールドを設定していなかったバグ。`ResolveActionSkills()`はSkillsのみ参照するため、`AddAction()`で追加したアクションが実質空になっていた。→ `Skills = new List<BaseSkill> { skill }` を追加して修正済み。

### 6.5 FreezeConsecutiveのカウンター免除

エージェントEが発見: `BaseStates.ReactionSkill.cs` 798行目で、FreezeConsecutive中はTryInterruptCounterがスキップされる。Freeze中断を「カウンター回避」で理由づける場合、Freeze後の通常連続攻撃に切り替わった場合のリスク予測という文脈になる。

### 6.6 情報取得の窓口不足（共通認識）

エージェントA, C, D, Eが独立して指摘: 現行の基底クラスは**ストック/トリガー以外の情報取得ユーティリティがほぼ皆無**。HpRatio、敵グループ列挙、被害記録アクセス、前のめり状態取得など、あらゆる派生AIが使う基礎情報の窓口がない。Phase 1の最重要作業。

---

## 7. 統合ロードマップ

### Phase 1: 基盤整備（200-400行追加）

**目標:** BasicTacticalAIが書けるだけの部品を揃える

| 順序 | 作業 | 行数目安 | 提案元 |
|---|---|---|---|
| 1-1 | 自HP/精神HP情報取得（HpRatio等） | 20-40行 | A, D |
| 1-2 | ターン数取得の整備 | 10-20行 | A, D |
| 1-3 | 敵グループ列挙ヘルパー | 20-30行 | D |
| 1-4 | ShouldEscape() virtual + Inspector逃走パラメータ | 30-50行 | A |
| 1-5 | SimulateHitRate + HitSimulatePolicy | 80-150行 | B |
| 1-6 | SkillAnalysisPolicy.useExpectedDamage統合 | 30-50行 | B |
| 1-7 | FindSkill(name/id) ヘルパー | 10-15行 | A |
| 1-8 | BasicTacticalAI作成（実証） | 60-100行 | D |

### Phase 2: 記憶・学習（300-500行追加）

**目標:** 敵キャラごとの個性と深みを出す

| 順序 | 作業 | 行数目安 | 提案元 |
|---|---|---|---|
| 2-1 | BattleMemory + DamageRecord/ActionRecord | 100-150行 | C |
| 2-2 | 被害記録の書き込みフック（BM側） | 20-30行 | C |
| 2-3 | 記録参照ユーティリティ（基底クラス） | 40-60行 | C |
| 2-4 | トラウマ率 + IsTraumaAvoided() | 60-100行 | C |
| 2-5 | _lastTurnHP + HpDropRate | 15-25行 | A |
| 2-6 | 行動記録参照 | 30-50行 | C, D |
| 2-7 | 逃走思考（上級: トラウマ連携） | 40-60行 | A |

### Phase 3: 高度な要素（400-800行追加）

**目標:** 高知能キャラの実現、体系的な賢さの管理

| 順序 | 作業 | 行数目安 | 提案元 |
|---|---|---|---|
| 3-1 | ShouldAbortFreeze() virtualフック | 5-10行 | E |
| 3-2 | GetFreezeInfo / EstimateCounterRisk | 60-100行 | E |
| 3-3 | ReadTargetPassives + 思慮レベル可視性 | 80-120行 | E |
| 3-4 | 思慮推測レベル基盤（_deliberationLevel） | 150-300行 | C, E |
| 3-5 | 精神レベル基盤（damageType切り替え、spiritualModifier活用判断） | 80-120行 | D, 補足6.3 |

### Phase 4: 構想段階

- AIAPI（情報取得ユーティリティをJSON化して共通基盤に）
- 戦闘後AI自動選定エンジン（11.7）
