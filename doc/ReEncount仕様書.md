# ReEncount仕様書

再エンカウント時（`NormalEnemy.ReEncountCallback`）に実行される全処理の仕様。

> **構想段階の内容**: `doc/ReEncountステータス変化構想.md` を参照

## 1. ReEncountCallbackの全体フロー

`EncounterEnemySelector` がエンカウント対象の有効敵全員に対して呼び出す。
初回エンカウントでも呼ばれる（初回は歩数差分0として処理）。

```
EncounterEnemySelector.Select():
│
├── FilterEligibleEnemies(enemies, globalSteps)    … 有効敵フィルタリング 【§4】
│   ├── 生存敵 → そのまま追加
│   ├── broken → スキップ
│   ├── 友情コンビ相方がDeathHeal持ち → Angel() + 追加（即時蘇生）
│   └── Reborn + CanReborn(2x if combo partner) → 追加（復活）
│
├── ApplyReencountCallbacks(validEnemies, globalSteps)
│   │   全有効敵に対して ReEncountCallback を実行
│   │
│   └── ReEncountCallback(globalSteps)
│       ├── 歩数差分計算
│       │   distanceTraveled = |globalSteps - _lastEncounterProgress|
│       │   （初回は distanceTraveled = 0）
│       │
│       ├── 【2回目以降のみ】
│       │   ├── パッシブ歩行効果ループ（distanceTraveled回）
│       │   │   ├── AllPassiveWalkEffect()          … 既存パッシブのWalkEffect発火
│       │   │   ├── UpdateWalkAllPassiveSurvival()  … パッシブ歩行残存カウンタ減少
│       │   │   ├── UpdateAllSkillPassiveWalkSurvival() … スキルパッシブの歩行残存
│       │   │   └── ResonanceHealingOnWalking()     … 共鳴値の歩行時回復
│       │   │
│       │   ├── ApplyGrowth(ReEncount, distanceTraveled) … スキル成長処理
│       │   │
│       │   └── NaturalHPRecovery(distanceTraveled)  … HP自然回復 【§2】
│       │
│       ├── 【全遭遇共通】
│       │   └── TransitionPowerOnWalkByCharacterImpression() … パワー変化
│       │
│       ├── _lastEncounterProgress = globalSteps  … 遭遇地点記録
│       │
│       └── 死亡判定 → 復活準備（復活タイプかつ未破壊の場合）
│
├── ApplyComboPassiveAccumulation(validEnemies)  … パッシブ蓄積 【§3】
│
└── SelectLeader → バトルグループ結成
```

### 既存パッシブ歩行消化について
前回の戦闘終了時点で敵が保持しているパッシブの歩行効果を、経過歩数分だけ消化する。
**新しいパッシブを付与する機能ではない。** 戦闘後AIが付与したバフの後始末。

なお、メモに「再遭遇時パッシブ生存判定（1/2生存、1/3効果）」という簡易版の構想があったが、
これは歩行消化の仕組みがまだなかった時期の発想であり、現在の歩数分ループが同じ目的をより精密に果たしている。
詳細は `doc/ReEncountステータス変化構想.md` §2.2 を参照。

## 2. HP自然回復

再エンカウントまでの経過歩数に応じて、HPがランダムに回復する。
AIの意志とは無関係な、時間経過による自然治癒。

### 計算式

```
recoveryRatio = min(distanceTraveled / FullRecoverySteps, 1.0)

【通常】
heal = MaxHP × Random(0, recoveryRatio)

【友情コンビ】
lowerRatio = min(ReEncountCount / BondMatureCount, 1.0)
heal = MaxHP × Random(lowerRatio × recoveryRatio, recoveryRatio)
```

友情コンビメンバーは回復の下限が `lowerRatio` で底上げされる。
付き合いが長いほど（ReEncountCountが多いほど）確実に回復する。

### 定数

| 定数 | 値 | 説明 |
|------|----|------|
| `FullRecoverySteps` | 200 | この歩数で回復率の上限が100%に達する |
| `BondMatureCount` | 11 | この再会回数で友情コンビの恩恵が最大に達する（§3と共通） |

### 挙動例（通常）

| 経過歩数 | recoveryRatio | 回復量の範囲 |
|---------|---------------|-------------|
| 10歩 | 0.05 | MaxHP × 0〜5% |
| 50歩 | 0.25 | MaxHP × 0〜25% |
| 100歩 | 0.50 | MaxHP × 0〜50% |
| 200歩以上 | 1.00 | MaxHP × 0〜100% |

### 友情コンビの lowerRatio

| ReEncountCount | lowerRatio | 効果 |
|---------------|------------|------|
| 1回目 | 0.09 | ほぼランダム（恩恵わずか） |
| 3回目 | 0.27 | そこそこ安定 |
| 6回目 | 0.55 | 半分以上は確実 |
| 11回目以降 | 1.00 | 完全成熟（回復率=recoveryRatioで確定） |

### 設計意図
- **歩数が少ないほど確実に弱い**: 追撃が報われる
- **歩数が多いほど読めない**: 全快かもしれないし低いかもしれない（不確実性）
- **確定的な最適行動が生まれない**: ランダム幅により「何歩以内に追撃すべき」が計算で出ない
- **友情コンビは確実性が増す**: 長い付き合いほど安定して回復する

### 条件
- 死亡中の敵には適用しない（復活は `OnReborn()` が担う）
- 2回目以降の遭遇でのみ適用（初回は歩数差分がないため）
- HPのsetterがMaxHPクランプ済みのため、回復量が溢れても問題ない

### 実装箇所
- `NormalEnemy.NaturalHPRecovery(int distanceTraveled)` — 回復計算
- `NormalEnemy.FullRecoverySteps` — 定数（200）
- `FriendshipComboSaveData.BondMatureCount` — 友情コンビ成熟定数（11）
- `FriendshipComboSaveData.LowerRatio` — lowerRatio算出プロパティ
- 呼び出し: `ReEncountCallback` 内、2回目以降の遭遇処理の末尾

### 他のHP回復手段との関係

| 手段 | 回復量 | タイミング |
|------|--------|-----------|
| `OnWin()` | MaxHP × 15%（固定） | 戦闘勝利時 |
| `OnAllyRunOut()` | MaxHP × 30%（固定） | 味方逃走時 |
| `OnReborn()` | 全回復 | 復活歩数到達時 |
| 戦闘後AI | AI依存 | 戦闘終了直後 |
| **HP自然回復** | **MaxHP × Random(lower, ratio)** | **再エンカウント時** |

これらは競合せず累積する。典型的な流れ:
1. 戦闘終了 → `OnWin()` で15%回復
2. 戦闘後AI → 任意で追加回復
3. 歩行 → （既存パッシブの効果が消化される）
4. 再エンカウント → **HP自然回復** で歩数ベースの回復

## 3. パッシブ蓄積（友情コンビ専用）

友情コンビのメンバーが、エンカウント間に **お互いにパッシブをかけ合っていた前提** でステータスに反映する。
既存の歩行消化（減らす側）と合わせて **パッシブの新陳代謝** を実現する。

### 処理タイミング

`ReEncountCallback` の **後**、`EncounterEnemySelector.ApplyComboPassiveAccumulation` として実行。
`ReEncountCallback` 内ではなく外にある理由は、相方の `NormalEnemy` インスタンスへのアクセスが必要なため。

### スキル選定条件

相方の持つスキルから「無条件で味方にかけて弊害がないもの」を抽出:
1. `SkillType.HasFlag(addPassive)` — パッシブ付与スキルである
2. `SkillZoneTrait.HasFlag(SelectOnlyAlly)` or `SelfSkill` — 味方向けである
3. 付与されるパッシブの `IsBad == false` — デバフではなくバフである
4. `CanApplyPassive()` — 既存の適合条件（OkType/OkImpression）で自動フィルタ（ApplyPassiveByID内部）

#### SelfSkillの包含について
`SelfSkill`（自己バフスキル）も蓄積対象に含まれる。
本来は「自分にしかかけないスキル」だが、友情コンビの親密さにより相方にも効果が及ぶ、という扱い。
プレイヤーから見ると「なぜ自己バフが仲間にかかっている？」というケレン味のある挙動になる。

### 条件
- 友情コンビメンバーのみ（非コンビ敵には適用しない）
- 2回目以降の遭遇でのみ（初回は distanceTraveled = 0）
- 死亡中の敵にはスキップ（復活待ちも含む。提供する側にもならない）
- 生存メンバーが2人以上いる場合のみ実行
- 重ね掛け（既に同一IDのパッシブを所持）は新規扱いしない — 歩行消化シミュレーションの対象外

### 処理フロー

```
ApplyComboPassiveAccumulation(validEnemies):
  各コンボについて（重複防止あり）:
    生存メンバーを収集（2人以上必要）
    双方向に AccumulateComboPassives を実行

AccumulateComboPassives(partner, distanceTraveled):
  ├── 1. 既存パッシブのスナップショット取得
  ├── 2. 相方のスキルから対象パッシブを全て付与（ポイント無視）
  ├── 3. 新規パッシブを特定（スナップショットとの差分）
  ├── 4. simulatedSteps = NextInt(lowerRatio × distanceTraveled, distanceTraveled + 1)
  │       「相方がsimulatedSteps前にパッシブをかけた」前提
  ├── 5. 新規パッシブのみを対象にsimulatedSteps分の歩行消化を実行
  │       （WalkEffect + UpdateWalkSurvival）
  └── 6. 残存したパッシブだけが戦闘に持ち込まれる
```

### 既存の歩行消化との関係

| 処理 | 対象 | 役割 | タイミング |
|------|------|------|-----------|
| 歩行消化ループ（ReEncountCallback内） | 既に持っているパッシブ | 減らす側 | ReEncountCallback内 |
| パッシブ蓄積（本システム） | 新規付与パッシブのみ | 増やす側 | ReEncountCallback後 |

自前ループは **新規付与パッシブのみ** に限定。既存パッシブは消化済みなので巻き込むと二重消化になる。

### lowerRatio（§2と共通）

```
lowerRatio = min(ReEncountCount / BondMatureCount, 1.0)
```

§2のHP自然回復ボーナスと同じ計算式・同じ定数（`BondMatureCount = 11`）。

### 実装箇所
- `NormalEnemy.AccumulateComboPassives(NormalEnemy partner, int distanceTraveled)` — 蓄積ロジック
- `NormalEnemy.LastDistanceTraveled` — ReEncountCallbackで保存した移動距離
- `EncounterEnemySelector.ApplyComboPassiveAccumulation(validEnemies)` — オーケストレーション
- 呼び出し: `EncounterEnemySelector.Select()` 内、`ApplyReencountCallbacks` の直後

## 4. 友情コンビ死亡メンバー支援

友情コンビのメンバーが死亡している場合、相方の存在によって復活が有利になる。
戦闘後AIの「意志による蘇生」とは別の、**友情コンビという関係性そのものから来る汎用的な恩恵**。

### 処理タイミング

`EncounterEnemySelector.FilterEligibleEnemies` 内で、死亡敵のフィルタリング時に実行。
`ReEncountCallback` より前の段階で蘇生/復活判定を行う。

### a. 相方のDeathHealによる即時蘇生

友情コンビの相方が `DeathHeal` スキルを持っている場合、Rebornを待たずに即座に蘇生する。

- **条件**: 相方が生存（`!Death() && !broken`）かつ `SkillType.DeathHeal` を持つスキルがある
- **蘇生処理**: `Angel()` でHP=ε復活
- **蘇生後のHP回復**: `ReEncountCallback` 内の `NaturalHPRecovery` が歩数ベースで回復
- **戦闘後AIで蘇生済みの場合**: `Death()` が false → FilterEligibleEnemiesの生存敵分岐で処理（自動スキップ）

#### 蘇生後のHP流れ

1. `FilterEligibleEnemies`: `Angel()` → HP=ε（ほぼ0）
2. `ReEncountCallback`: `NaturalHPRecovery(distanceTraveled)` → 歩数に応じて回復
3. 結果: 蘇生直後はHPが低いが、歩数が長ければそこそこ回復

### b. Reborn歩数の2倍速加速

友情コンビの相方が生存している場合、死亡メンバーの `RebornSteps` による復活カウントダウンが **固定2倍速** になる。

- **条件**: `ene.Reborn == true` かつ相方が生存（`!Death() && !broken`）
- **実装**: `EnemyRebornManager.CanReborn(enemy, globalSteps, stepMultiplier: 2)` で歩数消化に2倍の倍率を適用
- **相方がいない場合**: 通常の1倍速（`stepMultiplier: 1`）

### 優先順位

DeathHealが優先。DeathHealで蘇生されたらRebornは不要。

```
FilterEligibleEnemies:
  Dead + broken → skip
  Dead + combo partner has DeathHeal → Angel() + add（即時蘇生）
  Dead + Reborn + CanReborn(2x if combo partner alive) → add（復活）
  Dead + otherwise → skip
```

### 戦闘後AIとの関係

| システム | 性質 | タイミング |
|----------|------|-----------|
| 戦闘後AI（DeathHeal） | 敵の意志で「その場で蘇生を試みて確率的に諦める」 | 戦闘終了直後 |
| **本システム（DeathHeal即時蘇生）** | **友情コンビの関係性による自動蘇生（セーフティネット）** | **再エンカウント時** |
| **本システム（Reborn 2倍速）** | **仲間がそばにいることで復活が早まる** | **再エンカウント時** |

### 実装箇所

- `EncounterEnemySelector.FilterEligibleEnemies` — DeathHeal即時蘇生 + Reborn 2倍速の分岐
- `EncounterEnemySelector.FindLivingComboPartner` — 死亡コンボメンバーの生存相方を探すヘルパー
- `IEnemyRebornManager.CanReborn(enemy, globalSteps, stepMultiplier)` — stepMultiplierパラメータ（デフォルト1）
- `EnemyRebornManager.CanReborn` — distanceTraveled × stepMultiplier の適用
