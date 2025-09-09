# BaseSkillとBaseStates 詳細連携仕様書

## 目次
1. [スキル実行フロー](#1-スキル実行フロー)
2. [ダメージ計算の連携](#2-ダメージ計算の連携)
3. [パッシブシステムの相互作用](#3-パッシブシステムの相互作用)
4. [連続攻撃システムの連携](#4-連続攻撃システムの連携)
5. [ターゲティングシステムの連携](#5-ターゲティングシステムの連携)
6. [ステータス参照パターン](#6-ステータス参照パターン)
7. [ライフサイクル詳細連携](#7-ライフサイクル詳細連携)
8. [特殊システムの連携](#8-特殊システムの連携)

---

## 1. スキル実行フロー

### スキル選択から実行までの流れ

#### Step 1: スキル選択
```csharp
// BaseStates側でスキルを選択
BaseStates.NowUseSkill = selectedSkill;  // 行2040-2041
```

#### Step 2: スキル威力計算
```csharp
// BaseStates.cs 行7295
var skillPower = skill.SkillPowerCalc(skill.IsTLOA) * modifierForSkillPower;

// BaseSkill.cs 行1365-1373
public virtual float SkillPowerCalc(bool IsCradle = false)
{
    var pwr = GetSkillPower(IsCradle);  // 基礎パワー
    return pwr;
}

// BaseSkill.cs 行684-711 (_skillPower内部処理)
protected virtual float _skillPower(bool IsCradle)
{
    var Level = _nowSkillLevel;
    var powerMultiplier = SkillPassiveSkillPowerRate();  // スキルパッシブ補正
    
    if(IsCradle)
    {
        Level = _cradleSkillLevel;  // ゆりかご用レベル
    }
    
    // レベルに応じた威力計算
    if (FixedSkillLevelData.Count > Level)
    {
        return FixedSkillLevelData[Level].SkillPower * powerMultiplier;
    }
    else
    {
        // 無限レベル計算
        var baseSkillPower = FixedSkillLevelData[FixedSkillLevelData.Count - 1].SkillPower;
        var infiniteLevelMultiplier = Level - (FixedSkillLevelData.Count - 1);
        return (baseSkillPower + _infiniteSkillPowerUnit * infiniteLevelMultiplier) * powerMultiplier;
    }
}
```

#### Step 3: スキル命中判定
```csharp
// BaseSkill.cs 行1390-1420
public virtual HitResult SkillHitCalc(BaseStates target, float supremacyBonus = 0, ...)
{
    // Doerのパッシブチェック（割り込みカウンター）
    if(Doer.HasPassive(1)) return hitResult;
    
    // 通常命中計算
    var rndMin = RandomEx.Shared.NextInt(3);
    if(supremacyBonus > rndMin) supremacyBonus -= rndMin;
    
    var result = RandomEx.Shared.NextInt(100) < supremacyBonus + SkillHitPer ? 
                 hitResult : HitResult.CompleteEvade;
    
    // 魔法スキルのかすり判定
    if(result == HitResult.CompleteEvade && IsMagic)
    {
        if(RandomEx.Shared.NextInt(3) == 0) 
            result = HitResult.Graze;
    }
    
    return result;
}
```

---

## 2. ダメージ計算の連携

### BaseStatesからのスキル威力取得
```csharp
// BaseStates.cs 行8926（分散を含む計算例）
var skillPower = skill.SkillPowerCalc(skill.IsTLOA) * modifierForSkillPower * spread;
```

### スキルパッシブによる威力補正
```csharp
// BaseSkill.cs 行723-728
public float SkillPassiveSkillPowerRate()
{
    // 初期値を1にして、すべてのかかってるスキルパッシブのSkillPowerRateを掛ける
    var rate = ReactiveSkillPassiveList.Aggregate(1.0f, 
        (acc, pas) => acc * (1.0f + pas.SkillPowerRate));
    return rate;
}
```

### 精神ダメージ計算
```csharp
// BaseSkill.cs 行1374-1380
public virtual float SkillPowerForMentalCalc()
{
    return GetSkillPowerForMental() * MentalDamageRatio;
}

// MentalDamageRatioの階層的取得（行739-770）
public float MentalDamageRatio
{
    get
    {
        // レベルに応じたオプション値を優先的に取得
        // 見つからない場合はデフォルト値（_mentalDamageRatio）を返す
    }
}
```

---

## 3. パッシブシステムの相互作用

### BaseStatesのパッシブがBaseSkillに与える影響

#### 割り込みカウンターパッシブ（ID:1）
```csharp
// BaseSkill.cs 行1408（SkillHitCalc内）
if(Doer.HasPassive(1)) return hitResult;  // 確実に命中
```

#### スキルパッシブリスト
```csharp
// BaseSkill.cs 行1654-1665
public List<SkillPassive> ReactiveSkillPassiveList = new();  // リアクティブ
public List<SkillPassive> AggressiveSkillPassiveList = new();  // アグレッシブ

// 威力計算での使用（行723-728）
var rate = ReactiveSkillPassiveList.Aggregate(1.0f, 
    (acc, pas) => acc * (1.0f + pas.SkillPowerRate));
```

### BaseSkillからBaseStatesへのパッシブ操作

#### スキルパッシブ付与
```csharp
// BaseSkill.cs 行1685-1688
public void ApplySkillPassive(SkillPassive pas)
{
    ReactiveSkillPassiveList.Add(pas);
}

// ターゲット選択（行1737-1851）
public async UniTask<List<BaseSkill>> SelectSkillPassiveAddTarget(BaseStates target)
{
    var targetSkills = target.SkillList.ToList();
    
    // 3つの選択方式
    // 1. Select: UI選択（味方）またはAI（敵）
    // 2. Reaction: 特定キャラ・スキルの組み合わせに反応
    // 3. Random: ランダム選択
    
    // フィルター適用
    if(_skillPassiveGibeSkill_SkillFilter != null && 
       _skillPassiveGibeSkill_SkillFilter.HasAnyCondition)
    {
        targetSkills = targetSkills.Where(s => 
            s.MatchFilter(_skillPassiveGibeSkill_SkillFilter)).ToList();
    }
}
```

---

## 4. 連続攻撃システムの連携

### BaseSkillの連続攻撃判定
```csharp
// BaseSkill.cs 行1253-1281
public bool NextConsecutiveATK()
{
    if(HasConsecutiveType(SkillConsecutiveType.FixedConsecutive))
    {
        if (_atkCountUP >= ATKCount)
        {
            _atkCountUP = 0;
            return false;  // 終了
        }
        return true;  // 継続
    }
    else if(HasConsecutiveType(SkillConsecutiveType.RandomPercentConsecutive))
    {
        // 確率判定
        if(RandomEx.Shared.NextFloat(1) < _RandomConsecutivePer)
        {
            return true;
        }
        return false;
    }
    
    return false;
}
```

### BaseStatesのFreezeConsecutive管理
```csharp
// BaseStates.cs 行9495-9536
// FreezeConsecutive（ターンをまたぐ連続攻撃）の管理

public bool IsDeleteMyFreezeConsecutive = false;

public bool IsNeedDeleteMyFreezeConsecutive()
{
    if(NowUseSkill != null)
    {
        if(NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
        {
            return true;
        }
    }
    return false;
}

public void TurnOnDeleteMyFreezeConsecutiveFlag()
{
    Debug.Log("TurnOnDeleteMyFreezeConsecutiveFlag を呼び出しました。");
    IsDeleteMyFreezeConsecutive = IsNeedDeleteMyFreezeConsecutive();
}

// 削除処理
public void DeleteFreezeConsecutive()
{
    FreezeUseSkill = null;
    IsDeleteMyFreezeConsecutive = false;
}
```

### 連続攻撃実行フロー
```csharp
// BaseStates.cs 行8184
if(atker.NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
{
    // ターンをまたぐ連続攻撃の処理
}

// BaseStates.cs 行8273
if(NowUseSkill.NowConsecutiveATKFromTheSecondTimeOnward() && 
   NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
{
    // 2回目以降の連続攻撃
}
```

---

## 5. ターゲティングシステムの連携

### BaseStatesのターゲット意思
```csharp
// BaseStates.cs 行3628-3700
public DirectedWill Target = 0;        // 単体向け意思
public SkillZoneTrait RangeWill = 0;   // 範囲意思

// 範囲意思の判定メソッド
public bool HasRangeWill(params SkillZoneTrait[] skills)
{
    SkillZoneTrait combinedSkills = 0;
    foreach (SkillZoneTrait skill in skills)
    {
        combinedSkills |= skill;
    }
    return (RangeWill & combinedSkills) == combinedSkills;
}

// 単体対象判定
public bool HasAnySingleRangeWillTrait()
{
    return (RangeWill & CommonCalc.SingleZoneTrait) != 0;
}
```

### BaseSkillのターゲティング属性
```csharp
// BaseSkill.cs 行1626-1631
public SkillZoneTrait ZoneTrait = 0;  // スキルの範囲性質

// BaseSkill.cs 行189-203（DirectedWill列挙型）
public enum DirectedWill
{
    InstantVanguard,  // 前のめり
    BacklineOrAny,    // 後衛または前のめりいない集団
    One,              // 単一ユニット
}
```

### ターゲット選択の連携
```csharp
// BaseStates.cs 行7262
if(Atker.Target == DirectedWill.One)
{
    // 単体対象の処理
}

// BaseStates.cs 行9301
if(HasAnySingleRangeWillTrait() || randomRangeSpecialBool)
{
    // 単体スキルの範囲意志を持ってる場合
}

// BaseStates.cs 行9316
if(HasRangeWill(SkillZoneTrait.AllTarget))
{
    // 全体攻撃の場合
}
```

---

## 6. ステータス参照パターン

### BaseSkillからBaseStatesステータスへのアクセス

#### 十日能力値
```csharp
// BaseSkill.cs 行527-528（ゆりかごレベル計算）
var TendayAdvantage = Doer.TenDayValuesSum(true) / underAtker.TenDayValuesSum(false);

// BaseSkill.cs 行543-547
var AtkerTenDaySumMatchingSkill = 0f;
foreach(var tenDay in TenDayValues())
{
    AtkerTenDaySumMatchingSkill += Doer.TenDayValues(true).GetValueOrZero(tenDay.Key);
}
```

#### 精神属性
```csharp
// BaseSkill.cs 行574
var MoodRange = ModifyMoodByAttributeCompatibility(
    BaseMoodRange, 
    Doer.MyImpression,  // Doerの精神属性
    manager.MyGroup(Doer).OurImpression);
```

#### リバハル（TLOA累積値）
```csharp
// BaseSkill.cs 行530（ゆりかごレベル計算）
var EffectiveRivahal = Doer.Rivahal / Mathf.Max(1, TendayAdvantage);
```

### BaseStatesからBaseSkillパラメータへのアクセス

#### スキルタイプ
```csharp
// HasType()メソッド経由でのチェック
if(skill.HasType(SkillType.Attack, SkillType.Heal))
```

#### スキル範囲性質
```csharp
// HasZoneTrait()メソッド経由でのチェック
if(skill.HasZoneTrait(SkillZoneTrait.AllTarget))
```

---

## 7. ライフサイクル詳細連携

### 初期化フェーズ
```csharp
// 1. BaseStatesがスキルリストを構築
// 2. 各スキルに対してOnInitialize()を呼び出し

// BaseSkill.cs 行1320-1325
public void OnInitialize(BaseStates owner)
{
    Debug.Log($"スキル{SkillName}の初期化{owner.CharacterName}をDoerとして記録");
    Doer = owner;
    ResetStock();  // ストックカウント初期化
}
```

### 戦闘開始フェーズ
```csharp
// BaseStates側
// 行9906-9941 OnBattleStartNoArgument()
// - 精神HP最大値セット
// - ダメージ記録辞書初期化
// - パッシブ持続ターンリセット

// BaseSkill側
// 行1335-1339 OnBattleStart()
public void OnBattleStart()
{
    // バグ防止のため仮のムーブセットを決定
    DecideNowMoveSet_A0_B1(0);
}

// MoveSetキャッシュ
// BaseSkill.cs 行876-926
public void CashMoveSet()
{
    // レベルに応じたMoveSetをキャッシュ
}
```

### スキル使用フェーズ
```csharp
// 1. BaseStates.NowUseSkillに設定
// 2. 威力計算（SkillPowerCalc）
// 3. 命中判定（SkillHitCalc）
// 4. ダメージ計算
// 5. 連続攻撃判定（NextConsecutiveATK）
```

### 死亡フェーズ
```csharp
// BaseStates側（行9605-9632）
// - 落ち着きカウントリセット
// - FreezeConsecutive削除
// - 各種フラグリセット

// BaseSkill側（行1329-1334）
public void OnDeath()
{
    ResetStock();       // ストックリセット
    ResetAtkCountUp();  // 攻撃カウントリセット
    ReturnTrigger();    // トリガーカウント初期化
}
```

### 戦闘終了フェーズ
```csharp
// BaseSkill.cs 行1343-1360
public void OnBattleEnd()
{
    _doCount = 0;
    _doConsecutiveCount = 0;
    _hitCount = 0;
    _hitConsecutiveCount = 0;
    _cradleSkillLevel = -1;
    ResetAtkCountUp();
    ReturnTrigger();
    _tmpSkillUseTurn = -1;
    ResetStock();
    
    // スキルパッシブの終了処理
    foreach(var pas in ReactiveSkillPassiveList.Where(pas => pas.DurationWalkTurn < 0))
    {
        RemoveSkillPassive(pas);
    }
}
```

---

## 8. 特殊システムの連携

### ゆりかごレベル（TLOA専用）
```csharp
// BaseSkill.cs 行522-560
public void CalcCradleSkillLevel(BaseStates underAtker)
{
    if(!IsTLOA) return;
    
    // Doerとターゲットの十日能力比較
    var TendayAdvantage = Doer.TenDayValuesSum(true) / underAtker.TenDayValuesSum(false);
    
    // リバハル補正
    var EffectiveRivahal = Doer.Rivahal / Mathf.Max(1, TendayAdvantage);
    
    // 精神属性による調子補正
    var ratio = 0.7f;
    var BaseMoodRange = GetEstablishSpiritualMoodRange(
        BaseStates.GetOffensiveSpiritualModifier(Doer, underAtker).GetValue(ratio), 
        ratio);
    
    // 最終的なゆりかごレベル計算
    _cradleSkillLevel = (int)(root + MoodRange * MoodRangeRate);
}
```

### 慣れ補正との連携
```csharp
// BaseStatesの慣れ補正記録がBaseSkillの使用に影響
// SkillImpressionとMotionFlavorが慣れ補正の計算に使用される

// BaseSkill.cs
public SkillImpression Impression;    // 行333
public MotionFlavorTag MotionFlavor;  // 行337

// これらがBaseStatesの慣れ補正計算（行10647-10850）で参照される
```

### 属性ポイント消費
```csharp
// BaseSkill.cs 行459-470
public int RequiredNormalP = 0;  // 通常ポイント消費
public SerializableDictionary<SpiritualProperty, int> RequiredAttrP;  // 属性別消費

// BaseStatesでの消費処理（行2366-2444）
public bool TryConsumeForSkillAtomic(AttributeCondition condition)
{
    // トランザクション的な消費処理
    // 失敗時は全ロールバック
}
```

### Manager経由の共有機能
```csharp
// 両クラスが同じBattleManagerを参照
// BaseStates.cs 行65
protected BattleManager manager => Walking.Instance.bm;

// BaseSkill.cs 行324（Doer経由）
public BattleManager manager => Doer?.manager;

// 共通機能
// - キャラクター陣営判定
// - グループ情報取得
// - 前のめり状態判定
// - ターゲット選択管理
```

---

## 連携の設計思想

### 1. 責任分離
- **BaseStates**: キャラクター状態とリソース管理
- **BaseSkill**: スキル定義と実行ロジック

### 2. 双方向参照の管理
- BaseStates → SkillList（所有）
- BaseSkill → Doer（実行者参照）

### 3. パラメータの階層的取得
- レベル別オプション値 → デフォルト値のフォールバック
- null安全な実装

### 4. トランザクション的処理
- 属性ポイント消費のアトミック性
- 失敗時の完全ロールバック

### 5. イベント駆動の同期
- ライフサイクルイベントでの状態同期
- 戦闘フェーズごとの適切な処理

この詳細な連携により、複雑なRPGシステムが実現され、拡張性と保守性が確保されている。