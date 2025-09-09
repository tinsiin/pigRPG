# BaseSkillシステム アーキテクチャドキュメント

## 概要
BaseSkillはpigRPGにおけるスキルシステムの中核となるクラスです。このドキュメントではBaseSkill.csファイルに実装されているスキルシステムの詳細な構造と機能について解説します。

## ファイル構成
- **ファイルパス**: `Assets/Script/BaseSkill.cs`
- **行数**: 約2130行
- **主要クラス数**: 5クラス + 8つのEnum定義

## Enumeration定義

### 1. SkillSpecialFlag (行13-19)
スキルの特殊判別性質を定義するフラグ列挙体
```csharp
[Flags]
public enum SkillSpecialFlag
{
    TLOA = 1 << 0,  // TLOA系スキル
    Magic = 1 << 1, // 魔法スキル
    Blade = 1 << 2, // 刃物系スキル
}
```

### 2. SkillType (行24-49)
スキルの攻撃性質を定義するフラグ列挙体
- **Attack**: 攻撃スキル
- **Heal**: 回復スキル
- **addPassive**: パッシブ付与
- **RemovePassive**: パッシブ除去
- **DeathHeal**: 死亡時回復
- **AddVitalLayer**: バイタルレイヤー追加
- **RemoveVitalLayer**: バイタルレイヤー除去
- **MentalHeal**: 精神回復
- **Manual1_GoodHitCalc**: 良い命中計算
- **Manual1_BadHitCalc**: 悪い命中計算
- **addSkillPassive**: スキルパッシブ付与
- **removeGoodSkillPassive**: 良いスキルパッシブ除去
- **removeBadSkillPassive**: 悪いスキルパッシブ除去

### 3. SkillZoneTrait (行53-140)
スキルの範囲性質を定義する詳細なフラグ列挙体
#### 単体対象系
- **CanPerfectSelectSingleTarget**: 完全選択可能な単体対象
- **CanSelectSingleTarget**: 前のめり/後衛から選択可能な単体
- **RandomSingleTarget**: ランダムな単体対象

#### 範囲対象系
- **CanSelectMultiTarget**: 前のめり/後衛で選択可能な範囲
- **RandomSelectMultiTarget**: ランダムに選ばれる範囲
- **RandomMultiTarget**: ランダムな範囲（1-3人）
- **AllTarget**: 全範囲攻撃

#### 特殊範囲系
- **RandomTargetALLSituation**: 全シチュエーションでのランダム範囲
- **RandomTargetMultiOrSingle**: 範囲または単体のランダム
- **RandomTargetALLorSingle**: 全体または単体のランダム
- **RandomTargetALLorMulti**: 全体または範囲のランダム

#### 選択制限系
- **CanSelectRange**: 範囲選択可能性
- **CanSelectDeath**: 死亡対象選択可能性
- **CanSelectAlly**: 自陣選択可能性
- **ControlByThisSituation**: 状況による制御
- **CanSelectMyself**: 自分自身を選択可能
- **SelectOnlyAlly**: 自陣のみ選択可能
- **SelfSkill**: 自分自身のためのスキル

### 4. SkillConsecutiveType (行144-183)
連続攻撃のタイプを定義
- **CanOprate**: 毎コマンド選択可能
- **CantOprate**: 最初のみ選択可能（通常の連続攻撃）
- **FreezeConsecutive**: ターンをまたいだ連続攻撃
- **SameTurnConsecutive**: 同一ターンでの連続攻撃
- **RandomPercentConsecutive**: ランダム確率での連続
- **FixedConsecutive**: 固定回数の連続攻撃
- **Stockpile**: スキル保存性質（攻撃保存）

### 5. DirectedWill (行189-203)
対象の向きを定義
- **InstantVanguard**: 前のめり
- **BacklineOrAny**: 後衛または前のめりいない集団
- **One**: 単一ユニット

### 6. AttackDistributionType (行208-228)
攻撃分散タイプを定義
- **Random**: 完全ランダム分配
- **Beam**: 放射系統（前のめり優先）
- **Explosion**: 爆発（前衛と後衛への割合）
- **Throw**: 投げる（前のめりが最後）

### 7. SkillImpression (行232-250)
スキルの印象を定義
- **TLOA_PHANTOM**: TLOAファントム
- **HalfBreak_TLOA**: 半壊TLOA
- **Assault_Machine**: アサルト機械
- **SubAssult_Machine**: サブアサルト機械

### 8. ContainMode (行2070)
フィルタリング判定モード
- **Any**: いずれかの条件
- **All**: すべての条件

## 主要クラス構造

### 1. SkillLevelData クラス (行255-318)
レベル別のスキルパラメータを管理するクラス

#### フィールド
- **TenDayValues**: 十日能力の辞書
- **SkillPower**: スキル威力
- **OptionMentalDamageRatio**: 精神ダメージ比率（オプション）
- **OptionPowerSpread**: 威力分散（オプション）
- **OptionSkillHitPer**: スキル命中率（オプション）
- **OptionA_MoveSet**: Aムーブセット（オプション）
- **OptionB_MoveSet**: Bムーブセット（オプション）

#### メソッド
- **Clone()**: ディープコピーを作成

### 2. BaseSkill クラス (行320-1993)
スキルシステムの中核クラス

#### 重要フィールド

##### 基本情報
- **Doer**: スキル行使者 (BaseStates)
- **SkillName**: スキル名
- **Impression**: スキル印象
- **MotionFlavor**: 動作フレーバー

##### 属性情報
- **SkillSpiritual**: 精神属性（SpiritualProperty）
- **SkillPhysical**: 物理属性（PhysicalProperty）
- **SpecialFlags**: 特殊フラグ（TLOA/Magic/Blade）

##### スキルタイプと範囲
- **_baseSkillType**: 基本スキルタイプ
- **bufferSkillType**: バッファースキルタイプ
- **ZoneTrait**: スキル範囲性質
- **ConsecutiveType**: 連続攻撃タイプ
- **DistributionType**: 攻撃分散タイプ

##### レベルシステム
- **_nowSkillLevel**: 現在のスキルレベル
- **_cradleSkillLevel**: ゆりかご用スキルレベル
- **FixedSkillLevelData**: 固定スキルレベルデータのリスト
- **_infiniteSkillPowerUnit**: 無限スキル威力単位
- **_infiniteSkillTenDaysUnit**: 無限スキル十日単位

##### カウンター系
- **_doCount**: 実行回数
- **_doConsecutiveCount**: 連続実行回数
- **_hitCount**: ヒット回数
- **_hitConsecutiveCount**: 連続ヒット回数
- **_triggerCount**: トリガー回数
- **_triggerCountMax**: 最大トリガー回数

##### ストック系
- **_nowStockCount**: 現在のストック数
- **_defaultStockCount**: デフォルトストック数
- **_stockPower**: ストック威力
- **_stockForgetPower**: ストック忘却威力

##### 戦闘パラメータ
- **_skillHitPer**: 基本スキル命中率
- **_powerSpread**: 威力分散
- **_mentalDamageRatio**: 精神ダメージ比率
- **_defAtk**: 防御無視値

##### 必要ポイント
- **RequiredNormalP**: 通常必要ポイント
- **RequiredAttrP**: 属性別必要ポイント辞書

##### スキルパッシブ系
- **ReactiveSkillPassiveList**: リアクティブスキルパッシブリスト
- **AggressiveSkillPassiveList**: アグレッシブスキルパッシブリスト
- **BufferApplyingSkillPassiveList**: バッファー適用スキルパッシブリスト

#### 主要メソッド

##### 威力計算系
- **_skillPower(bool IsCradle)**: 基本威力計算（有限/無限レベル対応）
- **SkillPowerCalc(bool IsCradle)**: 実際の威力計算
- **SkillPowerForMentalCalc()**: 精神ダメージ用威力計算
- **GetSkillPower(bool IsCradle)**: スキル威力取得
- **GetSkillPowerForMental()**: 精神ダメージ威力取得

##### 命中判定系
- **SkillHitCalc()**: 命中判定（完全回避/かすり判定含む）
- **SkillHitPer**: 命中率プロパティ

##### 連続攻撃系
- **NextConsecutiveATK()**: 次の連続攻撃処理
- **ConsecutiveFixedATKCountUP()**: 固定連続攻撃カウントアップ
- **NowConsecutiveATKFromTheSecondTimeOnward()**: 2回目以降の連続攻撃判定

##### フラグチェック系
- **HasType()**: スキルタイプ保持チェック
- **HasTypeAny()**: いずれかのスキルタイプ保持
- **HasZoneTrait()**: ゾーン特性保持チェック
- **HasZoneTraitAny()**: いずれかのゾーン特性保持
- **HasConsecutiveType()**: 連続タイプ保持チェック
- **HasSpecialFlag()**: 特殊フラグ保持チェック

##### ターゲティング系
- **IsEligibleForSingleTargetReservation()**: 単体対象予約適格性
- **HasAnySingleTargetTrait()**: 単体対象特性保持
- **HasAllSingleTargetTraits()**: 全単体対象特性保持

##### ライフサイクル系
- **OnInitialize()**: 初期化時処理
- **OnDeath()**: 死亡時処理
- **OnBattleStart()**: 戦闘開始時処理
- **OnBattleEnd()**: 戦闘終了時処理

##### スキルパッシブ系
- **ApplySkillPassive()**: スキルパッシブ適用
- **RemoveSkillPassive()**: スキルパッシブ除去
- **ApplyBufferApplyingSkillPassive()**: バッファー適用スキルパッシブ
- **SelectSkillPassiveAddTarget()**: スキルパッシブ追加対象選択

##### ユーティリティ系
- **InitDeepCopy()**: ディープコピー初期化
- **MatchFilter()**: フィルター一致判定
- **EnumerateSkillTypes()**: スキルタイプ列挙
- **EnumerateSpecialFlags()**: 特殊フラグ列挙
- **EnumerateTenDayAbilities()**: 十日能力列挙

### 3. MoveSet クラス (行1997-2065)
スキル動作パターンを管理するクラス

#### フィールド
- **States**: AimStyleのリスト
- **DEFATKList**: 防御無視率のリスト
- **oldSizeDEFATK**: 古いサイズ（シリアライズ用）

#### メソッド
- **GetAtState(int index)**: 指定インデックスの状態取得
- **GetAtDEFATK(int index)**: 指定インデックスの防御無視率取得
- **DeepCopy()**: ディープコピー作成
- **OnBeforeSerialize()**: シリアライズ前処理
- **OnAfterDeserialize()**: デシリアライズ後処理

### 4. SkillFilterUtil クラス (行2071-2090)
スキルフィルタリング用ユーティリティクラス

#### メソッド
- **FlagsToEnumerable<T>()**: フラグをEnumerableに変換
- **CheckContain<T>()**: 包含チェック

### 5. SkillFilter クラス (行2096-2130)
スキルフィルタリング条件定義クラス

#### 基本方式フィールド
- **Impressions**: スキル印象リスト
- **MotionFlavors**: 動作フレーバーリスト
- **MentalAttrs**: 精神属性リスト
- **PhysicalAttrs**: 物理属性リスト
- **AttackTypes**: 攻撃タイプリスト

#### b方式フィールド（複数値対応）
- **TenDayAbilities**: 十日能力リスト
- **TenDayMode**: 十日モード（Any/All）
- **SkillTypes**: スキルタイプリスト
- **SkillTypeMode**: スキルタイプモード（Any/All）
- **SpecialFlags**: 特殊フラグリスト
- **SpecialFlagMode**: 特殊フラグモード（Any/All）

#### プロパティ
- **HasAnyCondition**: 条件設定有無チェック

## 設計の特徴

### 1. フラグベースの設計
- 多くのEnumが`[Flags]`属性を持ち、ビット演算による複数状態の組み合わせが可能
- 柔軟な条件判定とフィルタリングを実現

### 2. レベル成長システム
- 有限レベル（FixedSkillLevelData）と無限レベル（infiniteSkillPowerUnit）の両対応
- レベルごとに異なるパラメータセットを持つことが可能

### 3. 連続攻撃システム
- 7種類の連続攻撃タイプをサポート
- ターン内/ターンまたぎ、固定回数/ランダム等の多様な表現

### 4. ターゲティングシステム
- 19種類のターゲティング特性
- 単体/範囲/全体、選択可能/ランダム、自陣/敵陣等の組み合わせ

### 5. スキルパッシブシステム
- Reactive（反応型）とAggressive（攻撃型）の2種類
- バッファー適用型のパッシブもサポート

### 6. 十日能力システム
- TenDayAbilityDictionaryによる管理
- スキルレベルごとに異なる十日能力設定が可能

## 今後の探索ポイント
1. 連続攻撃システムの詳細な動作フロー
2. スキルパッシブの適用タイミングと条件
3. 十日能力の具体的な効果と計算方法
4. ターゲティングアルゴリズムの実装詳細
5. 命中判定とダメージ計算の完全なフロー