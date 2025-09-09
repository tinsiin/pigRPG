# BaseSkillシステム 詳細仕様ドキュメント

## 1. 連続攻撃システム

### NextConsecutiveATK メソッド (行1253-1281)
連続攻撃の継続判定を行う中核メソッド

#### FixedConsecutive（固定回数連続）の処理
```csharp
if(HasConsecutiveType(SkillConsecutiveType.FixedConsecutive))
{
    if (_atkCountUP >= ATKCount) // 設定値に達成したら
    {
        _atkCountUP = 0; // 値初期化
        return false; // 終わり
    }
    return true; // まだ達成してないから次の攻撃がある
}
```

#### RandomPercentConsecutive（確率連続）の処理
```csharp
if(HasConsecutiveType(SkillConsecutiveType.RandomPercentConsecutive))
{
    if (_atkCountUP >= ATKCount) // 最大回数チェック
    {
        _atkCountUP = 0;
        return false;
    }
    
    if(RandomEx.Shared.NextFloat(1) < _RandomConsecutivePer) // 確率判定
    {
        return true;
    }
    return false;
}
```

### トリガーカウントシステム

#### TrigerCount メソッド (行1140-1150)
スキル発動回数を管理
- **_triggerCountMax > 0**: 設定された回数だけ発動可能
- **_triggerCountMax = 0**: 無制限に発動可能（-1を返す）

#### ATKCountStock メソッド (行1196-1202)
攻撃回数のストック機能
```csharp
public void ATKCountStock()
{
    _nowStockCount += GetStcokPower();
    if(_nowStockCount > DefaultAtkCount)
        _nowStockCount = DefaultAtkCount; // 最大値を超えないように
}
```

### 連続攻撃関連フィールド
- **_doCount**: 実行回数
- **_doConsecutiveCount**: 連続実行回数
- **_hitCount**: ヒット回数
- **_hitConsecutiveCount**: 連続ヒット回数
- **_atkCountUP**: 攻撃カウントアップ値
- **_RandomConsecutivePer**: ランダム連続確率
- **_nowStockCount**: 現在のストック数
- **_stockPower**: ストック威力

## 2. スキルパッシブシステム

### SelectSkillPassiveAddTarget メソッド (行1737-1851)
スキルパッシブの付与対象を選択する重要メソッド

#### 3つの選択方式

##### 1. Select（直接選択方式）
```csharp
if(TargetSelection == SkillPassiveTargetSelection.Select)
{
    // 敵ならAIで選択（未実装）
    if(manager.GetCharacterFaction(Doer) == allyOrEnemy.Enemyiy)
    {
        // AI処理
    }
    
    // 味方はUI選択
    if(manager.GetCharacterFaction(Doer) == allyOrEnemy.alliy)
    {
        // フィルター条件で絞り込み
        if(_skillPassiveGibeSkill_SkillFilter != null && 
           _skillPassiveGibeSkill_SkillFilter.HasAnyCondition)
        {
            targetSkills = targetSkills.Where(s => 
                s.MatchFilter(_skillPassiveGibeSkill_SkillFilter)).ToList();
        }
        
        // UIで選択ボタンを表示
        var result = await PlayersStates.Instance.
            GoToSelectSkillPassiveTargetSkillButtonsArea(
                targetSkills, SkillPassiveEffectCount);
        
        return result;
    }
}
```

##### 2. Reaction（反応式）
```csharp
if(TargetSelection == SkillPassiveTargetSelection.Reaction)
{
    var correctReactSkills = new List<BaseSkill>();
    
    // ReactionCharaAndSkillListに登録されたキャラとスキルの組み合わせと一致確認
    foreach(var targetSkill in targetSkills)
    {
        foreach(var hold in ReactionCharaAndSkillList)
        {
            // キャラ名が違っていたら飛ばす
            if(target.CharacterName != hold.CharaName) continue;
            
            // スキル名まで一致したら
            if(targetSkill.SkillName == hold.SkillName)
            {
                correctReactSkills.Add(targetSkill);
                break;
            }
        }
    }
    return correctReactSkills;
}
```

##### 3. Random（ランダム方式）
```csharp
if(TargetSelection == SkillPassiveTargetSelection.Random)
{
    var randomSkills = new List<BaseSkill>();
    
    if(_skillPassiveGibeSkill_SkillFilter != null && 
       _skillPassiveGibeSkill_SkillFilter.HasAnyCondition)
    {
        // フィルター条件で絞り込んでから抽選
        var candidates = targetSkills.Where(s => 
            s.MatchFilter(_skillPassiveGibeSkill_SkillFilter)).ToList();
        
        int selectCount = Math.Min(SkillPassiveEffectCount, candidates.Count);
        for(int i = 0; i < selectCount; i++)
        {
            var item = RandomEx.Shared.GetItem(candidates.ToArray());
            randomSkills.Add(item);
            candidates.Remove(item);
        }
    }
    else
    {
        // 全体からランダムに選ぶ単純な方式
        int selectCount = Math.Min(SkillPassiveEffectCount, targetSkills.Count);
        for(int i = 0; i < selectCount; i++)
        {
            var item = RandomEx.Shared.GetItem(targetSkills.ToArray());
            randomSkills.Add(item);
            targetSkills.Remove(item); // 重複を防ぐため削除
        }
    }
    
    return randomSkills;
}
```

### スキルパッシブ関連フィールド
- **ReactiveSkillPassiveList**: リアクティブスキルパッシブのリスト
- **AggressiveSkillPassiveList**: アグレッシブスキルパッシブのリスト
- **BufferApplyingSkillPassiveList**: バッファー適用スキルパッシブのリスト
- **_skillPassiveGibeSkill_SkillFilter**: スキルパッシブ付与時のフィルター
- **SkillPassiveEffectCount**: スキルパッシブ効果数
- **ReactionCharaAndSkillList**: 反応するキャラとスキルのリスト
- **TargetSelection**: ターゲット選択方式

## 3. 十日能力システム

### TenDayValues メソッド (行932-963)
スキルレベルに応じた十日能力値を返す

#### 有限レベル範囲の処理
```csharp
if(FixedSkillLevelData.Count > Level)
{
    return FixedSkillLevelData[Level].TenDayValues;
}
```

#### 無限レベル範囲の処理
```csharp
else
{
    // 有限リストの最終値を基礎値にする
    var BaseTenDayValues = FixedSkillLevelData[FixedSkillLevelData.Count - 1].TenDayValues;
    
    // 有限リストの超過分
    var InfiniteLevelMultiplier = Level - (FixedSkillLevelData.Count - 1);
    
    // 基礎値に無限単位に超過分を掛けたものを加算
    return BaseTenDayValues + _infiniteSkillTenDaysUnit * InfiniteLevelMultiplier;
}
```

### CalcCradleSkillLevel メソッド (行522-560)
TLOAスキル専用の「ゆりかご」レベル計算

#### 計算プロセス
1. **ルート位置の算出**
   ```csharp
   // 強さの比較（十日能力補正込み）
   var TendayAdvantage = Doer.TenDayValuesSum(true) / underAtker.TenDayValuesSum(false);
   
   // 実効ライバハル = ライバハル ÷ 敵に対する強さ
   var EffectiveRivahal = Doer.Rivahal / Mathf.Max(1, TendayAdvantage);
   
   // ルート位置算出
   var root = EffectiveRivahal + _nowSkillLevel / 
              RandomEx.Shared.NextFloat(1, skillLevelRandomDivisorMax);
   ```

2. **調子の範囲決定**
   ```csharp
   // 精神補正値による固定範囲
   var ratio = 0.7f;
   var BaseMoodRange = GetEstablishSpiritualMoodRange(
       BaseStates.GetOffensiveSpiritualModifier(Doer, underAtker).GetValue(ratio), 
       ratio);
   
   // パーティ属性と自分の属性相性による調子の範囲の補正
   var MoodRange = ModifyMoodByAttributeCompatibility(
       BaseMoodRange, 
       Doer.MyImpression, 
       manager.MyGroup(Doer).OurImpression);
   ```

3. **上下レート算出**
   ```csharp
   // スキルの印象構造と同じ攻撃者の十日能力値の総量を取得
   var AtkerTenDaySumMatchingSkill = 0f;
   foreach(var tenDay in TenDayValues())
   {
       AtkerTenDaySumMatchingSkill += 
           Doer.TenDayValues(true).GetValueOrZero(tenDay.Key);
   }
   
   // 「どのくらいスキルを使いこなしているか」が指標
   var MoodRangeRate = AtkerTenDaySumMatchingSkill / TenDayValuesSum;
   MoodRangeRate = Mathf.Max(MoodRangeRate - 2, RandomEx.Shared.NextFloat(2));
   ```

4. **最終的なゆりかごレベル**
   ```csharp
   _cradleSkillLevel = (int)(root + MoodRange * MoodRangeRate);
   ```

### 十日能力関連フィールド
- **_infiniteSkillTenDaysUnit**: 無限スキルの十日単位
- **_cradleSkillLevel**: ゆりかご用スキルレベル
- **TenDayValuesSum**: 十日能力値の総和（プロパティ）

## 4. ターゲティングシステム

### MoveSetキャッシュシステム

#### CashMoveSet メソッド (行876-926)
スキルレベルに応じたMoveSetをキャッシュ

```csharp
public void CashMoveSet()
{
    var A_cash = _a_moveset;
    var B_cash = _b_moveset;
    
    // スキルレベルが有限範囲なら
    if(FixedSkillLevelData.Count > _nowSkillLevel)
    {
        // 現在レベルから逆順に探索
        for(int i = _nowSkillLevel; i >= 0; i--)
        {
            if(FixedSkillLevelData[i].OptionA_MoveSet != null)
            {
                A_cash = FixedSkillLevelData[i].OptionA_MoveSet;
                break;
            }
        }
        // B_MoveSetも同様
    }
    else
    {
        // 有限範囲以降なら、最終値から後ろまで回して探索
        for(int i = FixedSkillLevelData.Count - 1; i >= 0; i--)
        {
            if(FixedSkillLevelData[i].OptionA_MoveSet != null)
            {
                A_cash = FixedSkillLevelData[i].OptionA_MoveSet;
                break;
            }
        }
        // B_MoveSetも同様
    }
    
    // キャッシュする
    A_MoveSet_Cash = A_cash;
    B_MoveSet_Cash = B_cash;
}
```

#### DecideNowMoveSet_A0_B1 メソッド (行1523-1543)
AまたはBのMoveSetから現在の動作セットを決定

```csharp
public void DecideNowMoveSet_A0_B1(int aOrB)
{
    if(aOrB == 0)
    {
        if(A_MoveSet_Cash.Count == 0)
        {
            NowMoveSetState = new();
            return;
        }
        NowMoveSetState = A_MoveSet_Cash[RandomEx.Shared.NextInt(A_MoveSet_Cash.Count)];
    }
    else if(aOrB == 1)
    {
        if(B_MoveSet_Cash.Count == 0)
        {
            NowMoveSetState = new();
            return;
        }
        NowMoveSetState = B_MoveSet_Cash[RandomEx.Shared.NextInt(B_MoveSet_Cash.Count)];
    }
}
```

### ターゲティング関連フィールド
- **_a_moveset / _b_moveset**: 基本MoveSetリスト
- **A_MoveSet_Cash / B_MoveSet_Cash**: キャッシュされたMoveSet
- **NowMoveSetState**: 現在選択されているMoveSet
- **_nowSingleAimStyle**: 現在の単体狙いスタイル
- **_defAtk**: 防御無視値

### ターゲティング判定メソッド
- **IsEligibleForSingleTargetReservation()**: 単体対象予約の適格性判定
- **HasAnySingleTargetTrait()**: いずれかの単体対象特性を持つか
- **HasAllSingleTargetTraits()**: すべての単体対象特性を持つか
- **SetSingleAimStyle()**: 単体狙いスタイルの設定
- **NowAimStyle()**: 現在の狙いスタイル取得
- **NowAimDefATK()**: 現在の狙い防御無視値取得

## 5. 命中判定システム

### SkillHitCalc メソッド (行1390-1420)
スキル命中判定の中核処理

#### 特殊条件
1. **割り込みカウンター**: `Doer.HasPassive(1)`なら確実に命中
2. **魔法スキルのかすり判定**: 完全回避時、1/3の確率でGraze（かすり）に変更

#### 命中計算式
```csharp
// ボーナスがある場合ランダムで3%~0%引かれる
var rndMin = RandomEx.Shared.NextInt(3);
if(supremacyBonus > rndMin)
    supremacyBonus -= rndMin;

// 命中判定
var result = RandomEx.Shared.NextInt(100) < supremacyBonus + SkillHitPer ? 
             hitResult : HitResult.CompleteEvade;

// 魔法スキルのかすり処理
if(result == HitResult.CompleteEvade && IsMagic)
{
    if(RandomEx.Shared.NextInt(3) == 0) 
        result = HitResult.Graze;
}
```

## システム設計の特徴

### 1. レベルスケーリング
- 有限レベル（配列定義）と無限レベル（数式計算）の2段階システム
- レベルごとに異なるパラメータセットを持つ柔軟な設計

### 2. 確率と制御の共存
- ランダム要素（確率連続、ランダムターゲット）
- 制御要素（固定回数、直接選択）
- 両者を組み合わせた複雑な挙動の実現

### 3. キャッシュシステム
- MoveSetのキャッシュによるパフォーマンス最適化
- レベル変更時の動的更新対応

### 4. フィルタリングシステム
- SkillFilterによる柔軟な条件指定
- Any/Allモードによる条件の組み合わせ制御

### 5. TLOAスペシャルシステム
- TLOAスキル専用の「ゆりかご」レベル計算
- キャラクター間の相性や強さを考慮した動的レベル調整