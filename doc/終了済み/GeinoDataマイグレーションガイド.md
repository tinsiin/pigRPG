# Geinoデータ マイグレーションガイド

## 概要

PlayersBootstrapper.Init_geinoに保存されていたGeinoの初期データを、
CharacterDataSO形式に移行するためのガイド。

---

## 設計方針

CharacterDataSOは以下のシンプルな構造：

```
CharacterDataSO
├── _id: "geino"
├── _isInitialPartyMember: true
└── _template: AllyClass      ← 全てのキャラクターデータはここ
```

---

## 1. 旧データ（Init_geino）の完全な内容

### 基本ステータス

| フィールド | 値 |
|-----------|-----|
| CharacterName | ジーノ |
| _hp | 60 |
| _maxhp | 60 |
| _mentalHP | 0.34 |
| _p | 0 |

### 耐性値

| フィールド | 値 |
|-----------|-----|
| HeavyResistance | 2.91 |
| voltenResistance | 4.91 |
| DishSmackRsistance | 6.28 |

### 基礎能力値

| フィールド | 値 |
|-----------|-----|
| b_b_atk | 4 |
| b_b_def | 4 |
| b_b_eye | 4 |
| b_b_agi | 4 |

### 十日能力（TenDayTemplate）

| キー | 値 |
|------|-----|
| 7 | 45 |
| 17 | 3 |

### 精神属性・パワー

| フィールド | 数値 | enum名 |
|-----------|------|--------|
| _myImpression | 2 | **Pillar** |
| DefaultImpression | 256 | **BaleDrival** |
| NowPower | 1 | **medium** |

### その他

| フィールド | 値 |
|-----------|-----|
| _machineBrokenRate | 0.3 |
| _thinkingFactor | 17.2 |
| MyType | 0 |
| InitWeaponID | 0 |
| EmotionalAttachmentSkillID | 0 |
| ValidSkillIDList | [0, 1, 2, 3, 4] |

---

## 2. スキルデータ（5つ）- 完全版

### enum参照表

#### SpiritualProperty（キャラクターの_myImpression, DefaultImpression用）
| 数値 | enum名 |
|------|--------|
| 0 | none |
| 1 | Doremis |
| 2 | Pillar |
| 8 | LiminalWhiteTile |
| 16 | Sacrifaith |
| 256 | BaleDrival |

#### SkillImpression（スキルのImpression用 - SpiritualPropertyとは別のenum！）
| 数値 | enum名 |
|------|--------|
| 0 | TLOA_PHANTOM |
| 1 | HalfBreak_TLOA |
| 2 | Assault_Machine |
| 3 | SubAssult_Machine |

#### PhysicalProperty
| 数値 | enum名 |
|------|--------|
| 0 | heavy |
| 1 | volten |
| 2 | dishSmack |
| 3 | none |

#### SkillType (ビットフラグ)
| 数値 | enum名 |
|------|--------|
| 1 | Attack |
| 512 | Manual1_BadHitCalc |

#### SkillZoneTrait (ビットフラグ)
| 数値 | enum名 |
|------|--------|
| 1 | CanPerfectSelectSingleTarget |
| 32 | RandomMultiTarget |
| 16384 | ControlByThisSituation |
| 16416 | ControlByThisSituation \| RandomMultiTarget |

---

### スキル0: ラビットキャット (ID: 0)

**重要スキル - アグレッシブコミット型**

| フィールド | 数値 | enum名/説明 |
|-----------|------|-------------|
| _iD | 0 | |
| SkillName | "ラビットキャット" | |
| SkillSpiritual | 1 | **Doremis** |
| Impression | 2 | **Assault_Machine** (SkillImpression) |
| SkillPhysical | 3 | **none** |
| _baseSkillType | 512 | **Manual1_BadHitCalc** |
| ZoneTrait | 1 | **CanPerfectSelectSingleTarget** |
| ConsecutiveType | 0 | |
| IsAggressiveCommit | 1 (true) | アグレッシブコミット有効 |
| IsReadyTriggerAggressiveCommit | 0 (false) | |
| IsStockAggressiveCommit | 0 (false) | |
| CanSelectAggressiveCommit | 0 (false) | |
| _defaultStockCount | 1 | |
| _stockPower | 1 | |
| _stockForgetPower | 1 | |
| RequiredNormalP | 0 | |
| EvasionModifier | 1 | |
| AttackModifier | 1.6 | **攻撃力1.6倍** |
| AttackMentalHealPercent | 80 | |
| _skillHitPer | 68 | **命中率68%** |
| SkillPassiveEffectCount | 1 | |
| CanCancelTrigger | 1 (true) | |
| _triggerCountMax | 0 | |
| _triggerRollBackCount | 0 | |
| FixedSkillLevelData | (空リスト) | |

---

### スキル1: アキレスと亀-混沌時間 (ID: 1)

**重要スキル - リアクティブ/待機型**

| フィールド | 数値 | enum名/説明 |
|-----------|------|-------------|
| _iD | 1 | |
| SkillName | "アキレスと亀-混沌時間" | |
| SkillSpiritual | 8 | **LiminalWhiteTile** |
| Impression | 2 | **Assault_Machine** (SkillImpression) |
| SkillPhysical | 3 | **none** |
| _baseSkillType | 512 | **Manual1_BadHitCalc** |
| ZoneTrait | 1 | **CanPerfectSelectSingleTarget** |
| ConsecutiveType | 0 | |
| IsAggressiveCommit | 0 (false) | **リアクティブ型** |
| IsReadyTriggerAggressiveCommit | 0 (false) | |
| IsStockAggressiveCommit | 0 (false) | |
| CanSelectAggressiveCommit | 0 (false) | |
| _defaultStockCount | 1 | |
| _stockPower | 1 | |
| _stockForgetPower | 1 | |
| RequiredNormalP | 0 | |
| SKillDidWaitCount | 2 | **2ターン待機** |
| EvasionModifier | 1.1 | **回避1.1倍** |
| AttackModifier | 1 | |
| AttackMentalHealPercent | 80 | |
| _skillHitPer | 0 | 攻撃スキルではない |
| SkillPassiveEffectCount | 1 | |
| CanCancelTrigger | 1 (true) | |
| _triggerCountMax | 0 | |
| _triggerRollBackCount | 0 | |
| FixedSkillLevelData | (空リスト) | |

---

### スキル2: テストシンプル攻撃スキル (ID: 2)

| フィールド | 数値 | enum名/説明 |
|-----------|------|-------------|
| _iD | 2 | |
| SkillName | "テストシンプル攻撃スキル" | |
| SkillSpiritual | 8 | **LiminalWhiteTile** |
| Impression | 0 | **TLOA_PHANTOM** (SkillImpression) |
| SkillPhysical | 0 | **heavy** |
| _baseSkillType | 1 | **Attack** |
| ZoneTrait | 16416 | **ControlByThisSituation \| RandomMultiTarget** |
| ConsecutiveType | 0 | |
| IsAggressiveCommit | 1 (true) | |
| _defaultStockCount | 1 | |
| _stockPower | 1 | |
| _stockForgetPower | 1 | |
| RequiredNormalP | 2 | **必要P: 2** |
| EvasionModifier | 1 | |
| AttackModifier | 1 | |
| AttackMentalHealPercent | 80 | |
| _skillHitPer | 100 | **命中率100%** |
| SkillPassiveEffectCount | 1 | |
| CanCancelTrigger | 1 (true) | |
| **FixedSkillLevelData[0]** | | |
| → SkillPower | 10 | **威力10** |
| → OptionSkillHitPer | -1 | |

---

### スキル3: テスト完全単体選択スキル (ID: 3)

| フィールド | 数値 | enum名/説明 |
|-----------|------|-------------|
| _iD | 3 | |
| SkillName | "テスト完全単体選択スキル" | |
| SkillSpiritual | 1 | **Doremis** |
| Impression | 0 | **TLOA_PHANTOM** (SkillImpression) |
| SkillPhysical | 0 | **heavy** |
| _baseSkillType | 1 | **Attack** |
| ZoneTrait | 1 | **CanPerfectSelectSingleTarget** |
| ConsecutiveType | 0 | |
| IsAggressiveCommit | 1 (true) | |
| _defaultStockCount | 1 | |
| _stockPower | 1 | |
| _stockForgetPower | 1 | |
| RequiredNormalP | 1 | **必要P: 1** |
| EvasionModifier | 1 | |
| AttackModifier | 1 | |
| AttackMentalHealPercent | 80 | |
| _mentalDamageRatio | 22 | **精神ダメージ22** |
| _skillHitPer | 99 | **命中率99%** |
| SkillPassiveEffectCount | 1 | |
| CanCancelTrigger | 1 (true) | |
| **FixedSkillLevelData[0]** | | |
| → SkillPower | 55 | **威力55** |
| → OptionSkillHitPer | 0 | |

---

### スキル4: テストシンプル攻撃スキル (ID: 4)

| フィールド | 数値 | enum名/説明 |
|-----------|------|-------------|
| _iD | 4 | |
| SkillName | "テストシンプル攻撃スキル" | |
| SkillSpiritual | 16 | **Sacrifaith** |
| Impression | 0 | **TLOA_PHANTOM** (SkillImpression) |
| SkillPhysical | 0 | **heavy** |
| _baseSkillType | 1 | **Attack** |
| ZoneTrait | 16416 | **ControlByThisSituation \| RandomMultiTarget** |
| ConsecutiveType | 0 | |
| IsAggressiveCommit | 1 (true) | |
| _defaultStockCount | 1 | |
| _stockPower | 1 | |
| _stockForgetPower | 1 | |
| RequiredNormalP | 2 | **必要P: 2** |
| EvasionModifier | 1 | |
| AttackModifier | 1 | |
| AttackMentalHealPercent | 80 | |
| _skillHitPer | 100 | **命中率100%** |
| SkillPassiveEffectCount | 1 | |
| CanCancelTrigger | 1 (true) | |
| **FixedSkillLevelData[0]** | | |
| → SkillPower | 5 | **威力5** |
| → OptionSkillHitPer | -1 | |

---

## 3. 登録手順

### CharacterDataRegistryへの登録

1. CharacterDataRegistry.assetを作成（Create > Character > Character Data Registry）
2. GeinoDataを_charactersリストに追加
3. PlayersBootstrapperのCharacter Registryに設定

---

## 4. 確認事項

- PlayersBootstrapper.CharacterRegistry が設定されているか
- CharacterDataRegistry._characters に GeinoData があるか
- GeinoData._isInitialPartyMember が true になっているか
- GeinoData._template._myImpression が **Pillar** になっているか
- GeinoData._template.DefaultImpression が **BaleDrival** になっているか
- スキルリストに5つのスキルがあるか
