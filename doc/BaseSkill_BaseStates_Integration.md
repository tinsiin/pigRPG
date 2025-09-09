# BaseSkillとBaseStatesの連携仕様書

## 概要
BaseSkillクラスとBaseStatesクラスは、pigRPGのコアシステムにおいて密接に連携して動作します。
BaseStatesがキャラクターの状態を管理し、BaseSkillがそのキャラクターが使用するスキルを定義します。

## アーキテクチャ関係図
```
BaseStates (キャラクター状態)
    ├── Doer として BaseSkill に参照される
    ├── SkillList を保持（BaseSkillのコレクション）
    └── スキル実行時の各種パラメータを提供

BaseSkill (スキル定義)
    ├── Doer フィールドで実行者を参照
    ├── BaseStatesの各種メソッドを呼び出し
    └── ターゲットもBaseStates型で管理
```

## 1. 所有関係と参照

### BaseStatesからBaseSkillへの参照

#### SkillListプロパティ
```csharp
// BaseStates.cs 行6174
public abstract IReadOnlyList<BaseSkill> SkillList { get; }

// 派生の例
// BaseStates.cs 行6178
public List<BaseSkill> TLOA_SkillList => SkillList.Where(x => x.IsTLOA).ToList();
```

#### 現在使用中のスキル管理
```csharp
// BaseStates.cs 行2040-2041
[NonSerialized]
public BaseSkill NowUseSkill;  // 現在使用中のスキル

// BaseStates.cs 行2054
public BaseSkill FreezeUseSkill;  // 強制続行スキル（連続攻撃用）
```

### BaseSkillからBaseStatesへの参照

#### Doerフィールド（スキル実行者）
```csharp
// BaseSkill.cs 行421-422
[NonSerialized]
public BaseStates Doer;  // 行使者

// 初期化時の設定
// BaseSkill.cs 行1320-1325
public void OnInitialize(BaseStates owner)
{
    Debug.Log($"スキル{SkillName}の初期化{owner.CharacterName}をDoerとして記録");
    Doer = owner;  // 管理者を記録
    ResetStock();
}
```

## 2. 十日能力システムの連携

### BaseStatesの十日能力値
```csharp
// BaseStates.cs 行1361-1374
[SerializeField] TenDayAbilityDictionary _tenDayTemplate = new();
[NonSerialized] TenDayAbilityDictionary _baseTenDayValues = new();
public TenDayAbilityDictionary TenDayValues(bool IsSkillEffect)

// 十日能力総量計算
// BaseStates.cs 行1505
public float TenDayValuesSum(bool IsSkillEffect)
```

### BaseSkillでの十日能力参照
```csharp
// BaseSkill.cs 行932-963
public TenDayAbilityDictionary TenDayValues(bool IsCradle = false)
{
    if(Doer == null)
    {
        Debug.LogError("Doerがnullです-" + SkillName);
        return null;
    }
    // Doerの十日能力値を参照してスキルの十日能力を計算
}

// ゆりかごレベル計算での使用
// BaseSkill.cs 行527-528
var TendayAdvantage = Doer.TenDayValuesSum(true) / underAtker.TenDayValuesSum(false);
```

## 3. 精神属性の連携

### BaseStatesの精神属性定義
```csharp
// BaseStates_Analysis.md より
public enum SpiritualProperty
{
    liminalwhitetile,  // リミナルホワイトタイル
    kindergarden,      // 幼稚園
    sacrifaith,        // 自己犠牲
    cquiest,          // シークイエスト
    devil,            // デビル
    doremis,          // ドレミス
    godtier,          // ゴッドティア
    baledrival,       // ベイルドライバル
    pysco,            // サイコ
}

// キャラクターの精神属性
public SpiritualProperty MyImpression;
```

### BaseSkillでの精神属性参照
```csharp
// BaseSkill.cs 行325-329
[Header("スキルの精神属性")]
public SpiritualProperty SkillSpiritual;

// 精神補正値の取得（静的メソッド経由）
// BaseSkill.cs 行571
var ratio = 0.7f;
var BaseMoodRange = GetEstablishSpiritualMoodRange(
    BaseStates.GetOffensiveSpiritualModifier(Doer, underAtker).GetValue(ratio), 
    ratio);

// 属性相性による調整
// BaseSkill.cs 行574
var MoodRange = ModifyMoodByAttributeCompatibility(
    BaseMoodRange, 
    Doer.MyImpression,  // Doerの精神属性を参照
    manager.MyGroup(Doer).OurImpression);
```

## 4. スキル実行時のパラメータ連携

### 命中判定での連携
```csharp
// BaseSkill.cs 行1390-1420
public virtual HitResult SkillHitCalc(BaseStates target, float supremacyBonus = 0, ...)
{
    // 割り込みカウンター判定（Doerのパッシブを確認）
    if(Doer.HasPassive(1)) return hitResult;
    
    // 通常の命中計算
    // ...
}
```

### 威力計算での連携
```csharp
// BaseSkill.cs 行543（ゆりかごレベル計算内）
// スキルの印象構造と同じ攻撃者の十日能力値の総量を取得
var AtkerTenDaySumMatchingSkill = 0f;
foreach(var tenDay in TenDayValues())
{
    // Doerの十日能力値を参照
    AtkerTenDaySumMatchingSkill += Doer.TenDayValues(true).GetValueOrZero(tenDay.Key);
}
```

## 5. ライフサイクルの連携

### スキル初期化
```csharp
// BaseStatesがスキルを取得した際
// 1. BaseStates.SkillListにスキルを追加
// 2. BaseSkill.OnInitialize(this)を呼び出し
// 3. DoerフィールドにBaseStatesを設定
```

### 戦闘開始時
```csharp
// BaseStates側
// BaseStates.cs 行9906-9941 OnBattleStartNoArgument()
// - スキルコールバック呼び出し

// BaseSkill側
// BaseSkill.cs 行1335-1339 OnBattleStart()
public void OnBattleStart()
{
    // 仮のムーブセットを決定（バグ防止）
    DecideNowMoveSet_A0_B1(0);
}
```

### 死亡時
```csharp
// BaseStates側
// BaseStates.cs 行9605-9632 死亡コールバック
// - 落ち着きカウントリセット等

// BaseSkill側
// BaseSkill.cs 行1329-1334 OnDeath()
public void OnDeath()
{
    ResetStock();
    ResetAtkCountUp();
    ReturnTrigger();
}
```

### 戦闘終了時
```csharp
// BaseSkill.cs 行1343-1360 OnBattleEnd()
public void OnBattleEnd()
{
    // 各種カウンターリセット
    _doCount = 0;
    _doConsecutiveCount = 0;
    _hitCount = 0;
    _hitConsecutiveCount = 0;
    _cradleSkillLevel = -1;
    // ...
}
```

## 6. パッシブシステムとの連携

### BaseStatesのパッシブ管理
```csharp
// BaseStates.cs 行203-204
List<BasePassive> _passiveList = new();
public List<BasePassive> Passives => _passiveList;

// パッシブ確認
// BaseStates.cs 行267-270
public bool HasPassive(int id)
```

### BaseSkillからのパッシブ確認
```csharp
// BaseSkill.cs 行1408（SkillHitCalc内）
// 割り込みカウンターパッシブの確認
if(Doer.HasPassive(1)) return hitResult;
```

### スキルパッシブの付与
```csharp
// BaseSkill.cs 行1737-1851
public async UniTask<List<BaseSkill>> SelectSkillPassiveAddTarget(BaseStates target)
{
    var targetSkills = target.SkillList.ToList();  // ターゲットのスキルリストを取得
    
    // 選択方式に応じて処理
    // ...
}
```

## 7. 属性ポイントシステムの連携

### BaseStatesの属性ポイント管理
```csharp
// BaseStates.cs 行2235-2236
// 内部ストレージと追加履歴

// 消費処理
// BaseStates.cs 行2366-2444
public bool TryConsumeForSkillAtomic(AttributeCondition condition)
```

### BaseSkillの必要ポイント定義
```csharp
// BaseSkill.cs 行459-470
public int RequiredNormalP = 0;  // 通常ポイント
public SerializableDictionary<SpiritualProperty, int> RequiredAttrP;  // 属性別ポイント
```

## 8. ダメージ計算での連携

### BaseStatesのステータス提供
```csharp
// BaseStates.cs
public StatesPowerBreakdown ATK()  // 攻撃力
public StatesPowerBreakdown DEF()  // 防御力
public StatesPowerBreakdown EYE()  // 命中率
public StatesPowerBreakdown AGI()  // 敏捷性
```

### BaseSkillでのステータス参照
```csharp
// スキル実行時、Doerのステータスを参照して各種計算を行う
// 例：命中判定、ダメージ計算、精神ダメージ計算等
```

## 9. BattleManager経由の連携

### 共通マネージャー参照
```csharp
// BaseStates.cs 行65
protected BattleManager manager => Walking.Instance.bm;

// BaseSkill.cs 行324
public BattleManager manager => Doer?.manager;
```

### マネージャー経由の機能
- キャラクターの陣営判定（味方/敵）
- グループ情報の取得
- 前のめり状態の判定
- その他の戦闘管理機能

## 10. 慣れ補正システムとの連携

### BaseStatesの慣れ管理
```csharp
// BaseStates_Analysis.md より
// 慣れ補正の記録と計算
// 精神属性記憶: 行10855-10885
// 永続記憶: 行10936-10958
```

### BaseSkillの印象構造
```csharp
// BaseSkill.cs 行333
public SkillImpression Impression;  // スキル印象

// BaseSkill.cs 行337
public MotionFlavorTag MotionFlavor;  // 動作フレーバー
```

これらの印象情報がBaseStatesの慣れ補正計算に使用される。

## まとめ

BaseSkillとBaseStatesの連携は以下の特徴を持つ：

1. **双方向参照**: BaseStatesがSkillListを持ち、BaseSkillがDoerを持つ
2. **パラメータ共有**: 十日能力、精神属性、ステータス値の相互参照
3. **ライフサイクル同期**: 初期化、戦闘開始、死亡、戦闘終了の各タイミングで連携
4. **マネージャー共有**: BattleManager経由で共通の戦闘システムにアクセス
5. **型安全性**: BaseStates型を通じた厳密な型管理

この密接な連携により、複雑なスキルシステムと状態管理が実現されている。