# ReEncount仕様書

再エンカウント時（`NormalEnemy.ReEncountCallback`）に実行される全処理の仕様。

> **構想段階の内容**: `doc/ReEncountステータス変化構想.md` を参照

## 1. ReEncountCallbackの全体フロー

`EncounterEnemySelector` がエンカウント対象の有効敵全員に対して呼び出す。
初回エンカウントでも呼ばれる（初回は歩数差分0として処理）。

```
ReEncountCallback(globalSteps)
│
├── 歩数差分計算
│   distanceTraveled = |globalSteps - _lastEncounterProgress|
│   （初回は distanceTraveled = 0）
│
├── 【2回目以降のみ】
│   ├── パッシブ歩行効果ループ（distanceTraveled回）
│   │   ├── AllPassiveWalkEffect()          … 既存パッシブのWalkEffect発火
│   │   ├── UpdateWalkAllPassiveSurvival()  … パッシブ歩行残存カウンタ減少
│   │   ├── UpdateAllSkillPassiveWalkSurvival() … スキルパッシブの歩行残存
│   │   └── ResonanceHealingOnWalking()     … 共鳴値の歩行時回復
│   │
│   ├── ApplyGrowth(ReEncount, distanceTraveled) … スキル成長処理
│   │
│   └── NaturalHPRecovery(distanceTraveled)  … HP自然回復 【§2】
│
├── 【全遭遇共通】
│   └── TransitionPowerOnWalkByCharacterImpression() … パワー変化
│
├── _lastEncounterProgress = globalSteps  … 遭遇地点記録
│
└── 死亡判定 → 復活準備（復活タイプかつ未破壊の場合）
```

### 呼び出し元
- `EncounterEnemySelector.ApplyReencountCallbacks(validEnemies, globalSteps)`
  - エンカウント対象の有効敵リスト全員に対して実行

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
heal = MaxHP × Random(0, recoveryRatio)
HP += heal
```

### 定数

| 定数 | 値 | 説明 |
|------|----|------|
| `FullRecoverySteps` | 200 | この歩数で回復率の上限が100%に達する |

### 挙動例

| 経過歩数 | recoveryRatio | 回復量の範囲 |
|---------|---------------|-------------|
| 10歩 | 0.05 | MaxHP × 0〜5% |
| 50歩 | 0.25 | MaxHP × 0〜25% |
| 100歩 | 0.50 | MaxHP × 0〜50% |
| 200歩以上 | 1.00 | MaxHP × 0〜100% |

### 設計意図
- **歩数が少ないほど確実に弱い**: 追撃が報われる
- **歩数が多いほど読めない**: 全快かもしれないし低いかもしれない（不確実性）
- **確定的な最適行動が生まれない**: ランダム幅により「何歩以内に追撃すべき」が計算で出ない

### 条件
- 死亡中の敵には適用しない（復活は `OnReborn()` が担う）
- 2回目以降の遭遇でのみ適用（初回は歩数差分がないため）
- HPのsetterがMaxHPクランプ済みのため、回復量が溢れても問題ない

### 実装箇所
- `NormalEnemy.NaturalHPRecovery(int distanceTraveled)` — 回復計算
- `NormalEnemy.FullRecoverySteps` — 定数（200）
- 呼び出し: `ReEncountCallback` 内、2回目以降の遭遇処理の末尾

### 他のHP回復手段との関係

| 手段 | 回復量 | タイミング |
|------|--------|-----------|
| `OnWin()` | MaxHP × 15%（固定） | 戦闘勝利時 |
| `OnAllyRunOut()` | MaxHP × 30%（固定） | 味方逃走時 |
| `OnReborn()` | 全回復 | 復活歩数到達時 |
| 戦闘後AI | AI依存 | 戦闘終了直後 |
| **HP自然回復** | **MaxHP × Random(0, ratio)** | **再エンカウント時** |

これらは競合せず累積する。典型的な流れ:
1. 戦闘終了 → `OnWin()` で15%回復
2. 戦闘後AI → 任意で追加回復
3. 歩行 → （既存パッシブの効果が消化される）
4. 再エンカウント → **HP自然回復** で歩数ベースの回復
