# 命中回避と IsBad 仕様書

スキルの効果（付与・除去）が「良いアクション」か「悪い攻撃」かによって、
命中回避の計算方法が変わる仕組みを定義する仕様書。

関連仕様書:
- `doc/パッシブ仕様書.md` — パッシブ本体・スキルパッシブの定義
- `doc/総合戦闘仕様書.md` — 戦闘フロー全体
- `doc/スキル/効果解決方法仕様書.md` — Packagedモード（旧Manual1）による命中判定の特殊経路

---

## §1 基本原則：良いアクションと悪い攻撃

スキルの各効果は、対象にとって「良いこと」か「悪いこと」かで命中計算が異なる。

| 分類 | 例 | 命中計算 |
|------|-----|---------|
| **悪い攻撃** | 悪いパッシブ付与、良いパッシブ除去 | **IsReactHIT**（EYE/AGI命中回避）＋ **SkillHitCalc**（スキル命中率） |
| **良いアクション** | 良いパッシブ付与、悪いパッシブ除去、回復 | **SkillHitCalc** のみ（スキル命中率だけ） |

**設計意図**: 悪いことをされるなら対象は抵抗できる（回避判定あり）。
良いことをされるなら受け入れるだけ（スキルの精度だけが問題）。

> **SkillHitCalc（スキル命中率）は「スキル自体が正しく発動するか」の判定であり、対象の回避（IsReactHIT）とは別の概念。** 命中率が低い＝術者側の問題でスキルが失敗する。

### §1.1 命中計算の2段階

```
[悪い攻撃の場合]
  ① IsReactHIT: 攻撃者EYE vs 防御者AGI → Critical / Hit / Graze / CompleteEvade
  ② SkillHitCalc: スキル自体の命中率 → Hit / CompleteEvade
  → 両方を通過して初めて効果が適用される

[良いアクションの場合]
  ① SkillHitCalc のみ
  → スキル命中さえすれば効果が適用される
```

加えて、どちらの場合も **MixAllyEvade**（味方別口回避）が発生しうる。
これはスキルの良し悪しに関係なく、味方からの干渉として独立して判定される。

---

## §2 「良い/悪い」の判断場所 — パッシブとスキルパッシブの違い

ここがこの仕様の核心。**通常パッシブとスキルパッシブでは、良し悪しの判断をする場所が異なる。**

### §2.1 通常パッシブ（BasePassive）：パッシブ側で判断

通常パッシブは **パッシブ自体に `IsBad` プロパティ** を持つ。
スキルは `SkillType.addPassive` / `SkillType.RemovePassive` という **1種類のタイプ** で、
実行時に各パッシブの `IsBad` を見て良い/悪いを振り分ける。

```
スキル (addPassive)
  └─ SubEffects: [パッシブA(IsBad=true), パッシブB(IsBad=false), ...]
                            ↓
        敵対的処理で付与          友好的処理で付与
        (命中回避計算あり)         (スキル命中のみ)
```

**付与の流れ:**
- 敵対的経路: `BadPassiveHit()` → `IsBad == true` のパッシブだけ付与
- 友好的経路: `GoodPassiveHit()` → `IsBad == false` のパッシブだけ付与

**除去の流れ:**
- 敵対的経路: `GoodPassiveRemove()` → `IsBad == false`（良いパッシブ）を除去 = 悪い攻撃
- 友好的経路: `BadPassiveRemove()` → `IsBad == true`（悪いパッシブ）を除去 = 良いアクション

> **ポイント**: 1つのスキルに良いパッシブと悪いパッシブが混在できる。
> それぞれが別の命中計算を通る。除去も同様に1個ずつIsBadを確認する。

### §2.2 スキルパッシブ（BaseSkillPassive）：スキル側で判断

スキルパッシブの除去は **スキルタイプ自体が2種類に分かれている**。

```
removeGoodSkillPassive (SkillType)  ← 良いスキルパッシブを消す = 悪い攻撃
removeBadSkillPassive  (SkillType)  ← 悪いスキルパッシブを消す = 良いアクション
```

除去処理 `SkillPassiveRemove()` の中では **個々のスキルパッシブのIsBadを一切見ない**。
対象スキルのパッシブを `Clear()` で一括消去する。

```
スキル (removeGoodSkillPassive)     ← スキルタイプの時点で「悪い攻撃」と確定
  └─ 命中回避計算: 敵対的（IsReactHIT + SkillHitCalc）
  └─ 除去処理: 対象スキルのパッシブを全消し（IsBad不問）

スキル (removeBadSkillPassive)      ← スキルタイプの時点で「良いアクション」と確定
  └─ 命中計算: 友好的（SkillHitCalcのみ）
  └─ 除去処理: 対象スキルのパッシブを全消し（IsBad不問）
```

> **ポイント**: スキルパッシブの除去は「浸透的」—— 一回命中すればそのスキルの
> パッシブが全部消える。個別にIsBadを確認する必要がないので、
> 良し悪しの判断をスキルタイプに前倒しできる。

### §2.3 付与も同じ構造

付与についても同じパターンが成り立つ:

| 操作 | 通常パッシブ | スキルパッシブ |
|------|------------|-------------|
| **付与** | `addPassive`（1タイプ）→ 各パッシブのIsBadで分岐 | `addSkillPassive`（1タイプ）→ 各スキルパッシブのIsBadで分岐 |
| **除去** | `RemovePassive`（1タイプ）→ 各パッシブのIsBadで分岐 | `removeGood/removeBad`（2タイプ）→ スキルタイプで分岐 |

スキルパッシブの**付与**は通常パッシブと同じく `addSkillPassive` 1タイプで、
中で `IsBad` を見て振り分ける（`BadSkillPassiveHit` / `GoodSkillPassiveHit`）。

**除去だけがスキルタイプ分離方式になっている。**

---

## §3 なぜスキルパッシブの除去だけ方式が違うのか

### §3.1 除去の粒度の違い

| | 通常パッシブ | スキルパッシブ |
|---|---|---|
| 除去単位 | **1個ずつ**（CanEraceEffectCountで個数制限） | **スキル単位で一括**（Clear） |
| 除去時のIsBad確認 | **必要**（良いのだけ/悪いのだけ選んで消す） | **不要**（全部消すから） |

通常パッシブは「この中から悪いのだけ3個消す」のような個数制御があるため、
除去の瞬間にIsBadを見る必要がある → パッシブ側にIsBadが必要。

スキルパッシブは「このスキルに張り付いてるもの全部消す」という一括除去なので、
個別のIsBad確認が不要 → 命中計算の良し悪し判断をスキルタイプに前倒しできる。

### §3.2 スキルパッシブの「固定的な役割」

パッシブ仕様書§7.4より:

> スキルパッシブ除去系統はたった一回命中で浸透するイメージ（固定）なので、
> 解除される複数個パッシブのisBadの違いによってスキルのhitの違いが出るのは、
> スキルパッシブの固定的な役割（skillRock）、特定の身体部位にかけられる感（区切り）
> からして、よくない

つまり設計思想として「スキルパッシブは固定的で浸透的」という性質があり、
その性質と「スキルタイプで良し悪しを宣言する」方式が噛み合っている。

---

## §4 全スキルタイプの命中計算一覧

### §4.1 敵対的経路（相手にとって悪いこと）

| SkillType | 何をする | 命中計算 | IsBad確認 |
|-----------|---------|---------|----------|
| `Attack` | ダメージ | IsReactHIT (ATKType経由) | — |
| `addPassive` | 悪いパッシブ付与 | IsReactHIT相当 | パッシブ側 `IsBad==true` |
| `AddVitalLayer` | 悪い追加HP付与 | IsReactHIT相当 | VitalLayer側 `IsBad==true` |
| `RemovePassive` | 良いパッシブ除去 | IsReactHIT相当 | パッシブ側 `IsBad==false` |
| `RemoveVitalLayer` | 良い追加HP除去 | IsReactHIT相当 | VitalLayer側 `IsBad==false` |
| `removeGoodSkillPassive` | 良いスキルパッシブ除去 | IsReactHIT相当 | **不要（スキルタイプで確定）** |
| `addSkillPassive` | 悪いスキルパッシブ付与 | IsReactHIT相当 | スキルパッシブ側 `IsBad==true` |

※ 敵対的経路では全タイプ共通で、まず90%のランダム発動判定（`rollper(rndFrequency)`）を通過する必要がある。

### §4.2 友好的経路（相手にとって良いこと）

| SkillType | 何をする | 命中計算 | IsBad確認 |
|-----------|---------|---------|----------|
| `Heal` | HP回復 | SkillHitCalc + MixAllyEvade | — |
| `MentalHeal` | 精神回復 | SkillHitCalc + MixAllyEvade | — |
| `DeathHealOnBattle` | 復活 | SkillHitCalc + MixAllyEvade | — |
| `addPassive` | 良いパッシブ付与 | SkillHitCalc + MixAllyEvade | パッシブ側 `IsBad==false` |
| `AddVitalLayer` | 良い追加HP付与 | SkillHitCalc + MixAllyEvade | VitalLayer側 `IsBad==false` |
| `RemovePassive` | 悪いパッシブ除去 | SkillHitCalc + MixAllyEvade | パッシブ側 `IsBad==true` |
| `RemoveVitalLayer` | 悪い追加HP除去 | SkillHitCalc + MixAllyEvade | VitalLayer側 `IsBad==true` |
| `removeBadSkillPassive` | 悪いスキルパッシブ除去 | SkillHitCalc + MixAllyEvade | **不要（スキルタイプで確定）** |
| `addSkillPassive` | 良いスキルパッシブ付与 | SkillHitCalc + MixAllyEvade | スキルパッシブ側 `IsBad==false` |

---

## §5 まとめ図

```
┌─────────────────────────────────────────────────────────┐
│                    スキル実行                              │
│                       │                                   │
│          ┌────────────┴────────────┐                      │
│          ▼                         ▼                      │
│    【敵対的経路】             【友好的経路】                 │
│   (相手に悪いこと)          (相手に良いこと)                │
│          │                         │                      │
│   IsReactHIT(EYE/AGI)        SkillHitCalcのみ             │
│     + SkillHitCalc                                        │
│          │                         │                      │
│   ┌──────┴──────┐          ┌───────┴───────┐              │
│   ▼             ▼          ▼               ▼              │
│ 通常パッシブ  スキルパッシブ  通常パッシブ   スキルパッシブ    │
│ (付与/除去)   (付与/除去)   (付与/除去)    (付与/除去)      │
│   │             │            │               │            │
│ IsBadで       付与:IsBadで   IsBadで        付与:IsBadで   │
│ フィルタ      フィルタ       フィルタ        フィルタ       │
│              除去:タイプで                   除去:タイプで  │
│              既に確定済み                    既に確定済み    │
└─────────────────────────────────────────────────────────┘
```

**一言でまとめると:**
良し悪しの判断は常に必要だが、「いつ判断するか」が違う。
通常パッシブは効果の実行時にパッシブの `IsBad` で判断し、
スキルパッシブの除去だけはスキルタイプの選択時点で判断が済んでいる。

> **注**: 上記はEffectResolutionMode = Standard（標準パイプライン）の場合。
> Packagedモードでは単一命中判定 → `ManualSkillEffect()` で原子的に処理される。
> 詳細は `doc/スキル/効果解決方法仕様書.md` を参照。

---

## §6 関連コード

| 処理 | ファイル | メソッド |
|------|---------|---------|
| 命中回避計算（EYE/AGI） | `BaseStates.ReactionSkill.cs` | `IsReactHIT()` |
| スキル命中率計算 | `BaseSkill` | `SkillHitCalc()` |
| 味方別口回避 | `BaseStates.ReactionSkill.cs` | `MixAllyEvade()` |
| 敵対的経路の処理 | `BaseStates.ReactionSkill.cs` | `HostileEffect()` |
| 友好的経路の処理 | `BaseStates.ReactionSkill.cs` | `ReactionSkillOnBattle()` 内 |
| 通常パッシブ除去（良い） | `BaseStates.ReactionSkill.cs` | `GoodPassiveRemove()` |
| 通常パッシブ除去（悪い） | `BaseStates.ReactionSkill.cs` | `BadPassiveRemove()` |
| スキルパッシブ除去（共通） | `BaseStates.ReactionSkill.cs` | `SkillPassiveRemove()` |
| パッシブ IsBad定義 | `BasePassive.cs` | `IsBad` フィールド |
| スキルパッシブ IsBad定義 | `BaseSkillPassive.cs` | `IsBad` フィールド |
