# BaseSkill.cs リファクタリング計画書

## 概要
BaseSkill.csは現在約2000行のコードを含む大規模なファイルです。機能ごとに分割することで、保守性と可読性を向上させます。

## 追加調査結果

### アーキテクチャ特性
- **BaseSkillクラス**: `[Serializable]`属性付きの通常クラス（ScriptableObjectではない）
- **MoveSetクラス**: `ISerializationCallbackReceiver`インターフェースを実装
- **依存関係**:
  - BaseStates系（BaseStates.SkillManager.cs）
  - BattleAIBrain.cs
  - PlayersStates.cs
  - NormalEnemy.cs
  - SelectSkillPassiveTargetSkillButtons.cs（UI関連）

## 現在のファイル構造

### 主要クラス
1. **BaseSkill** (メインクラス、320-1993行)
2. **MoveSet** (動作セット管理、1997-2065行)
3. **SkillLevelData** (スキルレベルデータ、255-318行)
4. **SkillFilter** (フィルタリング機能、2096-2130行)
5. **SkillFilterUtil** (フィルタリングユーティリティ、2131-2251行)

### Enum定義
- **SkillType** (スキルタイプ定義)
- **SkillSpecialFlag** (特殊フラグ)
- **SkillImpression** (印象)
- **DirectedWill** (志向性)
- **SkillConsecutiveType** (連続攻撃タイプ)
- **AttackDistributionType** (攻撃分散タイプ)
- **SkillZoneTrait** (ゾーン特性)
- **ContainMode** (包含モード)

## 機能分類

### 1. コアスキル機能
- **基本プロパティ管理**
  - スキル名、印象、モーションフレーバー
  - 物理/精神属性
  - 必要ポイント（RequiredNormalP、RequiredAttrP）

- **スキルタイプ判定**
  - HasType、HasTypeAny
  - HasConsecutiveType
  - HasZoneTrait、HasZoneTraitAny
  - HasSpecialFlag
  - IsMagic、IsBlade、IsTLOA

### 2. スキルレベル管理
- **レベル計算**
  - _nowSkillLevel
  - CalcCradleSkillLevel
  - FixedSkillLevelData
  - _cradleSkillLevel

- **スキルレベルデータ**
  - SkillLevelData (クラス全体)
  - TenDayValues管理

### 3. パワー計算システム
- **基本パワー計算**
  - _skillPower
  - GetSkillPower、GetSkillPowerForMental
  - SkillPowerCalc、SkillPowerForMentalCalc
  - _infiniteSkillPowerUnit、_infiniteSkillTenDaysUnit

- **パワー修正**
  - MentalDamageRatio
  - PowerSpread
  - SkillPassiveSkillPowerRate

### 4. 命中率システム
- **命中率計算**
  - SkillHitPer
  - SkillHitCalc
  - HitRangePercentageDictionary
  - PowerRangePercentageDictionary

### 5. 連続攻撃システム
- **カウント管理**
  - DoCount、HitCount、TriggerCount
  - DoConsecutiveCount、HitConsecutiveCount
  - _doCount、_hitCount、_triggerCount等のフィールド

- **連続攻撃制御**
  - ATKCount、ATKCountUP
  - NextConsecutiveATK
  - ConsecutiveFixedATKCountUP
  - NowConsecutiveATKFromTheSecondTimeOnward

### 6. ストック機能
- **ストック管理**
  - _nowStockCount、_defaultStockCount
  - ResetStock、ForgetStock、IsFullStock
  - _stockPower、_stockForgetPower
  - GetStcokPower、GetStcokForgetPower

### 7. トリガー機能
- **トリガー制御**
  - IsTriggering
  - TrigerCount
  - RollBackTrigger、ReturnTrigger
  - _triggerCountMax、_triggerRollBackCount
  - CanCancelTrigger

### 8. MoveSet管理
- **動作セット**
  - _a_moveset、_b_moveset
  - A_MoveSet_Cash、B_MoveSet_Cash
  - CashMoveSet
  - DecideNowMoveSet_A0_B1
  - NowMoveSetState

### 9. ターゲティング
- **ターゲット選択**
  - TargetSelection
  - SetSingleAimStyle、NowAimStyle
  - NowAimDefATK、DEFATK
  - IsEligibleForSingleTargetReservation
  - HasAnySingleTargetTrait、HasAllSingleTargetTraits

### 10. スキルパッシブ
- **パッシブ効果管理**
  - ReactiveSkillPassiveList
  - AggressiveSkillPassiveList
  - ApplySkillPassive、RemoveSkillPassive
  - BufferApplyingSkillPassiveList
  - SelectSkillPassiveAddTarget

### 11. バトルライフサイクル
- **イベントハンドラ**
  - OnInitialize
  - OnBattleStart
  - OnBattleEnd
  - OnDeath
  - SetDeltaTurn

### 12. エフェクト管理
- **サブエフェクト**
  - subEffects、bufferSubEffects
  - SetBufferSubEffects、EraseBufferSubEffects
  - canEraceEffectIDs、canEraceVitalLayerIDs
  - CanEraceEffectCount、CanEraceVitalLayerCount

### 13. フィルタリング
- **スキルフィルタ**
  - SkillFilter (クラス全体)
  - SkillFilterUtil (クラス全体)
  - MatchFilter

### 14. ユーティリティ
- **列挙・取得**
  - EnumerateSkillTypes
  - EnumerateSpecialFlags
  - EnumerateTenDayAbilities

- **初期化・複製**
  - InitDeepCopy

### 15. アグレッシブコミット機能
- **アグレッシブコミット管理**
  - IsAggressiveCommit
  - CanSelectAggressiveCommit
  - IsReadyTriggerAggressiveCommit
  - IsStockAggressiveCommit

### 16. スキル待機機能
- **待機カウント**
  - SKillDidWaitCount
  - _tmpSkillUseTurn
  - DeltaTurn

## 推奨分割方針

### ファイル分割案

#### 1. BaseSkillCore.cs
- 基本プロパティ
- スキルタイプ判定メソッド群
- Enum定義（SkillType、SkillSpecialFlag等）

#### 2. BaseSkillLevel.cs
- SkillLevelDataクラス
- レベル計算ロジック
- TenDayValues管理

#### 3. BaseSkillPower.cs
- パワー計算システム
- ダメージ比率
- パワー拡散

#### 4. BaseSkillHit.cs
- 命中率システム
- 命中判定ロジック

#### 5. BaseSkillConsecutive.cs
- 連続攻撃システム
- ATKカウント管理
- 連続攻撃制御

#### 6. BaseSkillStock.cs
- ストック機能
- ストック管理メソッド

#### 7. BaseSkillTrigger.cs
- トリガー機能
- トリガー制御ロジック

#### 8. BaseSkillMoveSet.cs
- MoveSetクラス
- 動作セット管理
- キャッシュ処理

#### 9. BaseSkillTargeting.cs
- ターゲティングシステム
- 照準モード管理

#### 10. BaseSkillPassive.cs
- スキルパッシブシステム
- パッシブリスト管理

#### 11. BaseSkillBattle.cs
- バトルライフサイクル
- イベントハンドラ

#### 12. BaseSkillEffect.cs
- エフェクト管理
- サブエフェクト処理

#### 13. BaseSkillFilter.cs
- SkillFilterクラス
- SkillFilterUtilクラス
- フィルタリングロジック

#### 14. BaseSkillUtility.cs
- ユーティリティメソッド
- 列挙メソッド
- 初期化・複製

## 実装における注意事項

### 1. 部分クラス（Partial Class）の使用
```csharp
// BaseSkillCore.cs
[Serializable]
public partial class BaseSkill
{
    // コア機能
}

// BaseSkillPower.cs
public partial class BaseSkill
{
    // パワー計算機能
}
```

**重要**: BaseSkillは`ScriptableObject`ではなく、`[Serializable]`属性付きの通常クラスです。

### 2. 依存関係の管理
- 各ファイル間の依存関係を最小限に抑える
- 共通で使用される定数やEnumは BaseSkillCore.cs に配置

### 3. 名前空間の活用
```csharp
namespace PigRPG.Skills
{
    // 各クラスを配置
}
```

### 4. テスト容易性
- 各機能が独立してテスト可能になるよう設計
- モック作成が容易になる構造

## 移行手順

### Phase 1: 準備
1. BaseSkillフォルダ内に新しいファイル構造を作成
2. git branchで作業ブランチを作成

### Phase 2: 分割実装
1. Enum定義とコア機能から開始
2. 依存関係の少ない機能から順次分割
3. 各分割後にコンパイルエラーがないことを確認

### Phase 3: テスト
1. Unity Editor上で動作確認
2. 既存の機能が正常に動作することを確認
3. パフォーマンステスト

### Phase 4: 最適化
1. 不要なpublicメンバーをprivateに変更
2. アクセス修飾子の見直し
3. コメントとドキュメントの追加

## リスクと対策

### リスク
1. **依存関係の破壊**: 他のクラスからの参照が壊れる可能性
2. **シリアライゼーションの問題**: `[Serializable]`属性とUnityシリアライゼーションの整合性
3. **パフォーマンス低下**: ファイル分割によるオーバーヘッド
4. **MoveSetクラスの依存**: `ISerializationCallbackReceiver`実装のため、分割時に注意が必要

### 対策
1. **段階的移行**: 一度にすべてを変更せず、段階的に移行
2. **バックアップ**: 変更前の状態を必ず保存
3. **テスト**: 各段階で徹底的なテストを実施

## 分割後のファイル構成図

```
Assets/Script/BaseSkill/
├── BaseSkill.cs (削除予定)
├── Core/
│   ├── BaseSkillCore.cs
│   ├── BaseSkillEnums.cs
│   └── BaseSkillUtility.cs
├── Combat/
│   ├── BaseSkillPower.cs
│   ├── BaseSkillHit.cs
│   ├── BaseSkillConsecutive.cs
│   └── BaseSkillTargeting.cs
├── System/
│   ├── BaseSkillLevel.cs
│   ├── BaseSkillStock.cs
│   ├── BaseSkillTrigger.cs
│   └── BaseSkillPassive.cs
├── Battle/
│   ├── BaseSkillBattle.cs
│   ├── BaseSkillEffect.cs
│   └── BaseSkillMoveSet.cs
└── Filter/
    ├── BaseSkillFilter.cs
    └── SkillFilterUtil.cs
```

## まとめ

BaseSkill.csの分割により、以下のメリットが期待できます：
- **保守性向上**: 各機能が明確に分離され、修正が容易に
- **可読性向上**: ファイルサイズが適切になり、理解しやすく
- **チーム開発**: 複数人での並行作業が可能に
- **テスト容易性**: 単体テストが書きやすくなる

実装は部分クラス（Partial Class）を使用することで、既存の参照を壊すことなく段階的に進められます。

## 更新履歴
- 初版作成: BaseSkill.csの機能分析と分割計画
- 追加調査: シリアライゼーション特性、依存関係、アグレッシブコミット機能、待機機能を追記