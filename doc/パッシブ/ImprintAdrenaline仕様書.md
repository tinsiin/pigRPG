# Imprint Adrenaline 仕様書

同じスキル印象（SkillImpression）のスキルを連続行動で使い続けるとコンボが蓄積し、
汎用クリティカル率が段階的に上昇するパッシブ。コンボ上限到達でバースト効果が発動する。

関連ドキュメント:
- `doc/ImprintAdrenaline構想書.md` — 設計意図・未実装事項（UI・エフェクト）
- `doc/パッシブ仕様書.md` — パッシブシステム全般

---

## §1 概要

### §1.1 実装クラス

| 項目 | 値 |
|------|-----|
| クラス | `ImprintAdrenaline : BasePassive` |
| ファイル | `Assets/Script/Passive/ImprintAdrenaline.cs` |
| 方式 | BasePassive派生クラス（`[SerializeReference, SelectableSerializeReference]` パターン） |
| プレイヤー版 | DurationTurn付きの時限パッシブ（スキルで付与） |
| 敵版 | DurationTurn=-1 の永続パッシブ |

### §1.2 コンボ定数

| 定数 | 値 | 用途 |
|------|-----|------|
| `ComboMax` | 5 | コンボ上限。6回目でバースト |
| `BurstDamageMultiplier` | 1.4f | バースト時の確定威力倍率 |
| `PermanentCritPerBurst` | 2f | バーストごとの恒常クリ率UP（%） |
| `GenericCriticalMultiplier` | 1.9f | 汎用クリティカル発動時のダメージ/効果倍率（※BaseStates定数。ImprintAdrenalineからは参照のみ） |

### §1.3 コンボ段階とクリティカル率テーブル

```csharp
static readonly float[] ComboRateTable = { 0f, 0f, 3f, 6f, 8f, 12f, 14f };
```

| コンボ数 | テーブルインデックス | クリティカル率 |
|---------|-------------------|--------------|
| 1（初回） | 1 | 0% |
| 2 | 2 | 3% |
| 3 | 3 | 6% |
| 4 | 4 | 8% |
| 5 | 5 | 12% |
| 6（バースト） | 6 | 14% + バースト効果 |

> テーブルインデックス0（0%）はコンボ0の初期状態用。
> `Mathf.Min(_comboCount, ComboRateTable.Length - 1)` で配列境界を保護。

---

## §2 コンボ判定ロジック

### §2.1 判定タイミング

`OnBeforeSkillAction()` — AttackCharaの `BeginSkillHitAggregation()` **前**、ターゲットループ **外** で1回だけ呼ばれる。

呼び出しチェーン:
```
AttackChara()
  → PassivesOnBeforeSkillAction()      ← ここ
  → BeginSkillHitAggregation()
  → for each target { ... }
```

### §2.2 連続行動条件

```csharp
bool isConsecutiveTurn = _lastActedTurnCount >= 0
    && (currentTurnCount - _lastActedTurnCount == 1);
```

- `_lastActedTurnCount`: パッシブ内管理。初期値 `-1`。
- `currentTurnCount`: `manager.BattleTurnCount`（managerがnullなら0）
- 初回アクション時は `-1 >= 0` が `false` → 短絡評価で連続判定されない（安全）

### §2.3 コンボ更新フロー

```
if 連続ターン AND 同一スキル印象:
    _comboCount++
else:
    _comboCount = 1  （途切れた or 初回）

if _comboCount > ComboMax:
    → バースト処理（§3）
    → _comboCount = 0, _hasLastImpression = false

_lastImpression = 今回のスキル印象
_lastActedTurnCount = currentTurnCount
```

---

## §3 バースト効果

コンボ6回目（`_comboCount > ComboMax`）で発動。2つの効果が同時発生。

### §3.1 瞬間効果: 確定威力1.4倍

`GetBurstMultiplier()` → `BurstDamageMultiplier(1.4f)` を返す。
ダメージ計算（`ApplyGenericCritical`）および非ダメージ系（`GetGenericCriticalMultiplierForNonDamage`）の
両方で最初に適用される。

### §3.2 持続報酬: 恒常クリティカル率UP

```csharp
_owner.GenericCriticalRate += PermanentCritPerBurst;  // +2%
```

- `BaseStates.GenericCriticalRate` に直接加算
- パッシブ消失後も残る（BM内永続）
- 重ねがけ可能（2回バーストで+4%、3回で+6%…）

### §3.3 バースト後のリセット

- `_comboCount = 0`
- `_hasLastImpression = false`
- `_isBurstAction` は `OnAfterAttack()` でリセット（全ターゲット処理後）

---

## §4 汎用クリティカルの適用箇所

### §4.1 クリティカル率の合算

```csharp
// BaseStates.StatesNumber.cs
public float TotalGenericCriticalRate()
    => GenericCriticalRate + PassivesGenericCriticalContribution();
```

- `GenericCriticalRate`: 恒常分（バースト到達時に+2%加算、BM内永続）
- `PassivesGenericCriticalContribution()`: 全パッシブの `GetGenericCriticalContribution()` の合計（コンボ段階由来）

### §4.2 ダメージ系（ApplyGenericCritical）

**位置**: DamageOnBattle内、HitDmgCalculation **後**、ApplyTLOADamageReduction **前**

```
BarrierLayers → MentalDispersal → BladeCritical → HitDmgCalculation(1.5倍)
  → 【ApplyGenericCritical(1.9倍)】 → ApplyTLOADamageReduction → ...
```

命中クリティカル(1.5倍) × 汎用クリティカル(1.9倍) = 最大2.85倍。独立した別枠として乗算。

### §4.3 非ダメージ系（GetGenericCriticalMultiplierForNonDamage）

バースト倍率とクリティカル判定を合算した倍率を返すヘルパー。
各効果タイプが**独立にロール**する（ダメージとも独立）。

| スキルタイプ | 適用内容 | 実装箇所 |
|---|---|---|
| Heal | `skillPower × multiplier` | `ExecuteHealFriendlyCore` |
| MentalHeal | `skillPowerForMental × multiplier` | `ExecuteMentalHealFriendlyCore` |
| DeathHeal | `Angel()` 後に `HP × multiplier` | `ExecuteDeathHealFriendlyOnBattle` |
| AddVitalLayer（友好） | `ScaleMaxHP(multiplier)` で新規バリアのMaxHP拡大 | `ExecuteAddVitalLayerFriendlyCore` → `GoodVitalLayerHit` → `ApplyVitalLayer` |
| AddVitalLayer（敵対） | 同上 | `ApplyNonDamageHostileEffects` → `BadVitalLayerHit` |
| AddPassive（友好） | バッファ内パッシブの `DurationTurn × multiplier`（切り捨て） | `ExecuteAddPassiveFriendlyCore` → `GoodPassiveHit` |
| AddPassive（敵対） | 同上 | `ApplyNonDamageHostileEffects` → `BadPassiveHit` |
| AddSkillPassive（友好） | DeepCopy後の `DurationTurn × multiplier`（切り捨て） | `ExecuteAddSkillPassiveFriendlyCore` → `GoodSkillPassiveHit` |
| AddSkillPassive（敵対） | 同上 | `ApplyNonDamageHostileEffects` → `BadSkillPassiveHit` |

### §4.4 適用対象外

| スキルタイプ | 理由 |
|---|---|
| RemovePassive | 「1.9倍」の概念がない |
| RemoveVitalLayer | 同上 |
| SkillPassive.SkillPowerRate | 攻撃時の1.9倍で既にカバー。二重乗算回避 |
| SkillPassive.IsLock | 二値（ON/OFF）なので倍率の概念がない |

### §4.5 戦闘外パス

`ApplySkillCoreOutOfBattle()` ではデフォルト引数（`multiplier=1f`）により汎用クリティカルは自動的に非適用。
ImprintAdrenalineは戦闘パッシブのため、戦闘外でアクティブになることはない。

---

## §5 DurationTurn延長の端数処理

```csharp
buffered.DurationTurn = Mathf.FloorToInt(buffered.DurationTurn * durationMultiplier);
```

- **切り捨て**（`FloorToInt`）を採用
- `DurationTurn <= 0`（永続=-1、即消滅=0）はガード条件 `> 0` でスキップ

| 元DurationTurn | × 1.9 | 結果 |
|---|---|---|
| 1 | 1.9 | **1**（変化なし） |
| 2 | 3.8 | 3 |
| 3 | 5.7 | 5 |
| 5 | 9.5 | 9 |
| 10 | 19.0 | 19 |

> **注意**: DurationTurn=1 のパッシブは切り捨てにより延長されない。
> 必要に応じて `Mathf.RoundToInt`（四捨五入）に変更すれば `1→2` になる。

---

## §6 VitalLayerスケーリング

- **新規付与時のみ** `ScaleMaxHP(multiplier)` を適用
- 既存VitalLayerへの再付与（同一ID）は `ReplenishHP()` のみ（MaxHPは変更しない）
- これにより複合スケーリング（1.9x × 1.9x = 3.61x）を防止

---

## §7 変更ファイル一覧

| ファイル | 変更内容 |
|---|---|
| `Assets/Script/Passive/ImprintAdrenaline.cs` | **新規**: 派生パッシブクラス本体 |
| `Assets/Script/BasePassive.cs` | DeepCopy修正（Activator.CreateInstance）、virtual追加×3 |
| `Assets/Script/BaseStates/BaseStates.StatesNumber.cs` | GenericCriticalMultiplier定数、GenericCriticalRate、TotalGenericCriticalRate()、TotalBurstMultiplier() |
| `Assets/Script/BaseStates/Battle/BaseStates.Attack.cs` | PassivesOnBeforeSkillAction() 呼び出し追加 |
| `Assets/Script/BaseStates/Battle/BaseStates.Damage.cs` | ApplyGenericCritical() 追加 |
| `Assets/Script/BaseStates/Battle/BaseStates.ReactionSkill.cs` | GetGenericCriticalMultiplierForNonDamage、ApplyDurationExtension/ApplySkillPassiveDurationExtensionヘルパー、各Hit/Coreメソッドにmultiplier引数追加 |
| `Assets/Script/BaseStates/Passive/BaseStates.HasPassives.cs` | PassivesOnBeforeSkillAction等のイテレータ追加 |
| `Assets/Script/BaseVitalLayer.cs` | ScaleMaxHP() 追加 |
| `Assets/Script/BaseStates/BaseStates.VitalLayerManager.cs` | ApplyVitalLayer に hpMultiplier 引数追加 |

---

## §8 既知のリスク・注意事項

### §8.1 DurationTurn=1 の延長無効（低リスク）

切り捨て丸めにより、1ターンパッシブは `floor(1×1.9)=1` で延長されない。
現時点では仕様として許容。四捨五入への変更で対処可能。

### §8.2 PassivesBurstMultiplier の Max 採取（低リスク）

複数の ImprintAdrenaline パッシブが同時に存在し、異なるバースト倍率を持つ場合、
最大値のみが採用される。現時点では全インスタンスが同一倍率(1.4x)のため実害なし。
将来的に異なる倍率のバースト系パッシブが登場した場合は設計見直しが必要。

### §8.3 DeepCopyとランタイム状態

ImprintAdrenalineの `[NonSerialized]` フィールド（コンボカウンタ等）はDeepCopyで引き継がれない。
`OnApply()` で再初期化されるため問題ないが、DeepCopyをoverrideしていない点に留意。

### §8.4 未実装事項

以下は `doc/ImprintAdrenaline構想書.md` §8 に記載。本仕様書のスコープ外。

- バースト時のエフェクト・SE
- コンボカウンタのUI表示（プレイヤー/敵）
- PassiveManagerへの登録（プレイヤー版・敵版のインスタンス作成）
