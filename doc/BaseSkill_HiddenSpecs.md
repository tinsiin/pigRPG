# BaseSkillシステム 隠れた仕様と見逃しやすい詳細

## 1. パラメータ優先度システム

### MentalDamageRatio プロパティ (行739-770)
精神ダメージ比率の階層的取得システム
```csharp
// 取得優先順位：
// 1. 現在レベルから逆向きに検索して最初に有効な値を見つける
// 2. 有限範囲外の場合、最終値から逆向きに検索
// 3. それでも見つからない場合はデフォルト値（_mentalDamageRatio）
```

### PowerSpread プロパティ (行780-813)
威力分散の階層的取得（MentalDamageRatioと同様のロジック）
- レベルごとにオプションで異なる分散パターンを設定可能
- null/空配列チェックを含む堅牢な実装

### SkillHitPer プロパティ
命中率も同様の階層的取得システムを使用

## 2. サブエフェクトとバイタルレイヤーシステム

### SubEffects プロパティ (行1463-1467)
```csharp
public List<int> SubEffects
{
    get { return (subEffects ?? Enumerable.Empty<int>())
                    .Concat(bufferSubEffects ?? Enumerable.Empty<int>()).ToList(); }
}
```
- **基本エフェクト（subEffects）**と**バッファーエフェクト（bufferSubEffects）**を結合
- null安全な実装（Enumerable.Empty使用）

### バイタルレイヤー消去システム
```csharp
// 消去可能なバイタルレイヤーのID管理
public List<int> canEraceVitalLayerIDs = new();
public int CanEraceVitalLayerCount;  // 消去可能数
public int Now_CanEraceVitalLayerCount;  // 現在の消去可能数

// 消去可能なエフェクトのID管理（同様の構造）
public List<int> canEraceEffectIDs = new();
public int CanEraceEffectCount;
public int Now_CanEraceEffectCount;
```

### RefilCanEraceCount メソッド (行1505-1509)
消去カウントのリフィル処理
```csharp
public void RefilCanEraceCount()
{
    Now_CanEraceEffectCount = CanEraceEffectCount;
    Now_CanEraceVitalLayerCount = CanEraceVitalLayerCount;
}
```

## 3. 精神・物理属性の相性システム

### ModifyMoodByAttributeCompatibility メソッド (行586-661)
キャラクターの精神属性とパーティ属性の相性による調子補正

#### 相性表の例
```csharp
case SpiritualProperty.liminalwhitetile:
    if (partyProperty == PartyProperty.Flowerees)
        decrease = true;  // 相性悪い：-1
    else if (partyProperty == PartyProperty.Odradeks)
        increase = true;  // 相性良い：+1
```

#### 精神属性一覧
- liminalwhitetile
- kindergarden
- sacrifaith
- cquiest
- devil
- doremis
- godtier
- baledrival
- pysco

#### パーティ属性一覧
- Flowerees
- Odradeks
- TrashGroup
- HolyGroup
- MelaneGroup

## 4. 必要ポイントシステム

### RequiredNormalP フィールド (行459-464)
```csharp
/// <summary>
/// スキル実行に必要なノーマルポイント。
/// 0ならノーマルP消費なし。
/// </summary>
public int RequiredNormalP = 0;
```

### RequiredAttrP フィールド (行470)
```csharp
public SerializableDictionary<SpiritualProperty, int> RequiredAttrP = 
    new SerializableDictionary<SpiritualProperty, int>();
```
- 精神属性ごとの必要ポイントを辞書管理
- SerializableDictionaryによるUnityシリアライズ対応

## 5. ライフサイクルメソッドの詳細

### OnInitialize (行1320-1325)
```csharp
public void OnInitialize(BaseStates owner)
{
    Debug.Log($"スキル{SkillName}の初期化{owner.CharacterName}をDoerとして記録");
    Doer = owner;  // 管理者を記録
    ResetStock();  // _nowstockは最初は0になってるので、初期化でdefaultstockと同じ数にする
}
```

### OnDeath (行1329-1334)
死亡時のリセット処理
```csharp
public void OnDeath()
{
    ResetStock();
    ResetAtkCountUp();
    ReturnTrigger();
}
```

### OnBattleStart (行1335-1339)
```csharp
public void OnBattleStart()
{
    // カウントが専ら参照されるので、バグ出ないようにとりあえず仮のムーブセットを決めておく
    DecideNowMoveSet_A0_B1(0);
}
```

### OnBattleEnd (行1343-1360)
戦闘終了時の包括的リセット
```csharp
public void OnBattleEnd()
{
    _doCount = 0;
    _doConsecutiveCount = 0;
    _hitCount = 0;
    _hitConsecutiveCount = 0;
    _cradleSkillLevel = -1;  // ゆりかご用スキルレベルをエラーにする
    ResetAtkCountUp();
    ReturnTrigger();  // 発動カウントはカウントダウンするから最初っから
    _tmpSkillUseTurn = -1;  // 前回とのターン比較用の変数をnullに
    ResetStock();
    
    // スキルパッシブの終了時の処理
    foreach(var pas in ReactiveSkillPassiveList.Where(pas => pas.DurationWalkTurn < 0))
    {
        RemoveSkillPassive(pas);
    }
}
```

## 6. 見逃しやすい重要プロパティ・メソッド

### IsTriggering プロパティ (行1164-1177)
トリガー発動中かどうかの判定
```csharp
public bool IsTriggering
{
    get{
        // 発動カウントが0以下の場合は即時実行なのでfalse
        if (_triggerCountMax <= 0) return false;
        
        // カウントが開始されていない場合はfalse
        if (_triggerCount >= _triggerCountMax) return false;
        
        // カウントが開始済みで、まだカウントが残っている場合はtrue
        return _triggerCount > -1;
    }
}
```

### IsSingleHitAtk プロパティ (行1296-1302)
単発攻撃かどうかの判定
```csharp
bool IsSingleHitAtk
{
    get
    {
        // movesetのリストが空なら単体攻撃(二回目以降が設定されていないので)
        return _a_moveset.Count <= 0;
    }
}
```

### ManualSkillEffect メソッド (行1425-1428)
```csharp
public virtual void ManualSkillEffect(BaseStates target, HitResult hitResult)
{
    // 仮想メソッド：派生クラスで手動エフェクトを実装
}
```

### MatchFilter メソッド (行1853-1886)
スキルフィルターとの一致判定
```csharp
public bool MatchFilter(SkillFilter filter)
{
    if (filter == null || !filter.HasAnyCondition) return true;
    
    // 基本方式の判定（単一値の一致）
    if (filter.Impressions.Count > 0 && !filter.Impressions.Contains(Impression))
        return false;
    // ... 他の属性も同様
    
    // b方式の判定（複数値・Any/Allモード）
    if (filter.TenDayAbilities.Count > 0 && 
        !SkillFilterUtil.CheckContain(EnumerateTenDayAbilities(), 
                                     filter.TenDayAbilities, 
                                     filter.TenDayMode))
        return false;
    // ... 他のb方式属性も同様
    
    return true;
}
```

### GetEstablishSpiritualMoodRange メソッド (行567-582)
精神補正値から調子範囲を決定
```csharp
int GetEstablishSpiritualMoodRange(float value, float ratio)
{
    if (value <= 95 * ratio)
        return -1;  // 悪い調子
    else if (value <= 100 * ratio)
        return 0;   // 普通の調子
    else
        return 1;   // 良い調子
}
```

## 7. 特殊なフィールド・定数

### レベル除算定数
```csharp
const float TLOA_LEVEL_DIVIDER = ...;      // TLOAスキル用
const float NOT_TLOA_LEVEL_DIVIDER = ...;  // 通常スキル用
```

### アグレッシブコミット関連
```csharp
public bool IsAggressiveCommit;           // アグレッシブコミット状態
public bool IsReadyTriggerAgressiveCommit; // アグレッシブコミット準備完了
public bool IsStockAgressiveCommit;       // ストックアグレッシブコミット
public bool CanSelectAggressiveCommit;    // アグレッシブコミット選択可能
```

### ターン管理
```csharp
private int _tmpSkillUseTurn = -1;  // 前回使用ターンの記録
public int DeltaTurn { get; }       // ターン差分
public int SKillDidWaitCount;       // スキル待機カウント
```

### 辞書型フィールド
```csharp
public Dictionary<int, float> PowerRangePercentageDictionary;  // 威力範囲パーセンテージ
public Dictionary<int, float> HitRangePercentageDictionary;    // 命中範囲パーセンテージ
```

## 8. エラー処理とデバッグ

### null安全処理の例
- SubEffectsプロパティでのnull合体演算子使用
- TenDayValuesメソッドでのDoerのnullチェック
- MoveSetキャッシュでの空リストチェック

### デバッグログの配置
- OnInitializeでの初期化ログ
- TenDayValuesでの有限レベルリスト数ログ
- SelectSkillPassiveAddTargetでの各種警告・エラーログ

## まとめ

BaseSkillシステムは以下の特徴を持つ高度な設計：

1. **階層的パラメータ取得**: レベルに応じた動的なパラメータ解決
2. **null安全設計**: 全体を通じた堅牢なnullチェック
3. **柔軟な拡張性**: virtual/overrideによる派生クラス対応
4. **複雑な相性システム**: 精神属性×パーティ属性の相性マトリクス
5. **多段階ライフサイクル**: 初期化→戦闘開始→死亡→戦闘終了の明確な処理
6. **デバッグ友好的**: 適切なログ出力とエラーハンドリング

これらの仕様により、多様なスキル表現と堅牢な動作を実現している。