# BaseStates クラス詳細分析ドキュメント

## ファイル情報
- **ファイルパス**: `Assets/Script/BaseStates.cs`
- **総行数**: 約11,498行
- **クラス定義**: 行15-11498
- **クラス種別**: 抽象クラス（abstract class）

## 目次
1. [基本構造](#基本構造)
2. [UI管理](#ui管理)
3. [ステータスシステム](#ステータスシステム)
4. [パッシブシステム](#パッシブシステム)
5. [十日能力システム](#十日能力システム)
6. [スキルシステム](#スキルシステム)
7. [戦闘システム](#戦闘システム)
8. [属性ポイントシステム](#属性ポイントシステム)
9. [人間状況システム](#人間状況システム)
10. [その他の機能](#その他の機能)

---

## 基本構造

### クラス宣言
- **位置**: 行16-17
- **定義**: `[Serializable] public abstract class BaseStates`
- **説明**: 基礎ステータスのクラス、クラスそのものは使用しないので抽象クラス

### 重要なフィールド定義
- **BattleManager参照**: 行65 - `protected BattleManager manager => Walking.Instance.bm;`
- **SchizoLog参照**: 行66 - `protected SchizoLog schizoLog => SchizoLog.Instance;`

---

## UI管理

### UIコントローラー
- **UIController定義**: 行24 - `public UIController UI { get; private set; }`
- **UIバインド機能**: 行29-44
  ```csharp
  public void BindUIController(UIController ui)
  {
      UI = ui;
      UI.BindUser(this);
      // 属性ポイントリングUIを探し、無ければ追加して初期化
      if (UI != null && UI.Icon != null)
      {
          var ring = UI.GetComponentInChildren<AttrPointRingUIController>(true);
          if (ring == null)
          {
              ring = UI.gameObject.AddComponent<AttrPointRingUIController>();
          }
          ring.Initialize(this, UI.Icon.rectTransform);
      }
  }
  ```

### HPバー連携
- **HPBar参照**: 行3178 - `CombinedStatesBar HPBar => UI?.HPBar;`
- **HP更新時のUI反映**: 行3192-3195
- **精神HP更新時のUI反映**: 行3231-3235

---

## ステータスシステム

### HP（ヒットポイント）
- **プライベートフィールド**: 行3180 - `private float _hp;`
- **パブリックプロパティ**: 行3181-3203
  - 最大値クランプ処理
  - UIバー更新処理
- **最大HP**: 行3205-3206 - `private float _maxhp;` / `public float MaxHP => _maxhp;`

### 精神HP
- **フィールド定義**: 行3209-3210 - `float _mentalHP;`
- **プロパティ**: 行3214-3236
- **最大値計算**: 行3241-3255
  - パワーが高い時: HP×1.3 + MaxHP×0.08
  - それ以外: 現在HPと同じ
- **回復処理**:
  - 攻撃時回復: 行3259-3262 - b_ATK分回復
  - ターン時回復: 行3263-3266 - Rain十日能力分回復

### ポイントシステム
- **最大ポイント計算**: 行2116 - `public int MAXP => (int)_maxhp / PlayersStates.Instance.HP_TO_MaxP_CONVERSION_FACTOR;`
- **現在ポイント**: 行2119-2137
- **パワーによる初期化**: 行2142-2150

### 4大基礎ステータス
- **基礎値定義**: 行1350-1354
  ```csharp
  [Header("4大ステの基礎基礎値")]
  public float b_b_atk = 4f;
  public float b_b_def = 4f;
  public float b_b_eye = 4f;
  public float b_b_agi = 4f;
  ```

### ステータス計算

#### 攻撃力（ATK）
- **基礎攻撃力計算**: 行1801-1831 - `public StatesPowerBreakdown b_ATK`
- **シミュレート機能**: 行1832-1859
- **プロトコル排他計算**: 行1864-1877
- **最終攻撃力計算**: 行6263-6319 - `public virtual StatesPowerBreakdown ATK()`

#### 防御力（DEF）
- **基礎防御力計算**: 行1950-1968 - `public StatesPowerBreakdown b_DEF(AimStyle? SimulateAimStyle = null)`
- **AimStyle別計算**: 行1889-1921
- **最終防御力計算**: 行6325-6350 - `public virtual StatesPowerBreakdown DEF(float minusPer=0f, AimStyle? SimulateAimStyle = null)`
- **精神防御力**: 行6354-6364

#### 命中率（EYE）
- **基礎命中率計算**: 行1969-1988 - `public StatesPowerBreakdown b_EYE`
- **最終命中率計算**: 行6196-6230 - `public virtual StatesPowerBreakdown EYE()`

#### 敏捷性（AGI）
- **基礎敏捷性計算**: 行1778-1796 - `public StatesPowerBreakdown b_AGI`
- **最終敏捷性計算**: 行6231-6262 - `public virtual StatesPowerBreakdown AGI()`

---

## パッシブシステム

### パッシブリスト管理
- **プライベートリスト**: 行203 - `List<BasePassive> _passiveList = new();`
- **パブリックアクセサ**: 行204 - `public List<BasePassive> Passives => _passiveList;`
- **読み取り専用リスト**: 行263 - `public IReadOnlyList<BasePassive> PassiveList => _passiveList;`

### パッシブ操作
- **ID確認**: 行267-270 - `public bool HasPassive(int id)`
- **パッシブ取得**: 行284-287 - `public BasePassive GetPassiveByID(int passiveId)`
- **パッシブ適用**: 行301-330 - `public void ApplyPassiveByID(int id,BaseStates grantor = null)`
- **パッシブ除去**: 行503-536

### バッファシステム
- **バッファリスト**: 行215 - `List<(BasePassive passive,BaseStates grantor)> BufferApplyingPassiveList = new();`
- **バッファ適用**: 行219-227 - `void ApplyBufferApplyingPassive()`
- **戦闘中の遅延適用**: 行243-248

### パッシブイベントハンドラ
- **割り込みカウンター時**: 行375-384
- **ダメージ前**: 行388-396
- **ダメージ前発動チェック**: 行401-411
- **ダメージ後**: 行415-423
- **攻撃後**: 行428-436
- **次ターン進行時**: 行490-498

### パッシブ生存管理
- **ターン経過時**: 行573-581
- **前のめり解除時**: 行586-594
- **歩行時**: 行599-607
- **死亡時**: 行608-616

### パッシブ効果
- **ダメージ無効化**: 行635-656
- **ダメージ減衰**: 行662-676
- **ターゲット確率**: 行682-703
- **スキル発動率**: 行710-722
- **回復補正率**: 行726-763

---

## 十日能力システム

### 基本定義
- **テンプレート**: 行1361-1362 - `[SerializeField] TenDayAbilityDictionary _tenDayTemplate = new();`
- **ランタイム辞書**: 行1366-1367 - `[NonSerialized] TenDayAbilityDictionary _baseTenDayValues = new();`
- **アクセサ**: 行1372-1374

### 十日能力計算
- **総量計算**: 行1505 - `public float TenDayValuesSum(bool IsSkillEffect)`
- **ランダム取得**: 行1509-1512
- **成長処理**: 行1606-1626
- **割合成長**: 行1633-1653

### 勝利ブースト
- **成長値記録**: 行1518 - `protected TenDayAbilityDictionary battleGain = new();`
- **倍率計算**: 行1522-1576
  - 倍率テーブル（6割以下2.6倍～48割超でratio-7倍）
- **ブースト適用**: 行1581-1601

---

## スキルシステム

### スキルリスト
- **抽象プロパティ**: 行6175 - `public abstract IReadOnlyList<BaseSkill> SkillList { get; }`
- **TLOAスキルリスト**: 行6179 - `public List<BaseSkill> TLOA_SkillList => SkillList.Where(x => x.IsTLOA).ToList();`

### スキル実行
- **現在使用スキル**: 行2042 - `public BaseSkill NowUseSkill;`
- **強制続行スキル**: 行2055 - `public BaseSkill FreezeUseSkill;`
- **スキル使用コールバック**: 行2063-2086
- **連続実行カウント**: 行2091-2110

### 連続攻撃システム
- **FreezeConsecutive判定**: 行9503-9513
- **削除フラグ**: 行9517-9521
- **連続攻撃削除**: 行9526-9535

### 非ダメージ敵対行動
- **処理メイン**: 行8514-8604
- **パッシブ付与/除去**: 行8530-8596
- **かすりヒット時50%発動**: 行8525-8528

### スキルグルーピング（慣れ補正）
- **ドレミス方式**: 行10002-10013 - 最初6個、以降7個ごと
- **リーミナル方式**: 行10025-10042 - 素数境界
- **シークイエスト方式**: 行10047-10059 - 10個ごと
- **自己犠牲方式**: 行10091-10149 - 素数間に乱数挿入

---

## 戦闘システム

### ダメージ計算
- **基礎山型分布**: 行6589-6611
  - 8d5501ダイスによる±22%補正
  - -15%以下で「攻撃が乱れた」判定
- **AimStyle不一致クランプ**: 行6615-6638
- **がむしゃらブースト**: 行6642-6650

### 思えダメージ
- **定数定義**: 行7523-7539
  - 精神属性合致補正: 1.29倍
  - 発動しきい値: 1.4倍
  - 基礎ダメージ: 0.06
- **メイン処理**: 行7568-7700
- **ランダマイズ**: 行7543-7563

### 慣れ補正システム
- **基礎慣れ値**: 行10528-10533 - 0.0004 × b_EYE
- **下限しきい値**: 行10537-10541
- **EYE基準の下限計算**: 行10549-10594
- **目の瞬き機能**: 行10605-10639
- **メイン処理**: 行10647-10850

### 互角一撃生存
- **メイン判定**: 行6383-6407
- **パワー条件チェック**: 行6413-6455
- **人間状況別基本値**: 行6458-6497
- **特定組み合わせ上書き**: 行6500-6582

---

## 属性ポイントシステム

### 基本構造
- **内部ストレージ**: 行2235-2236
- **追加履歴**: 行2239
- **チャンク構造体**: 行2241-2250

### ポイント操作
- **取得**: 行2255-2258
- **追加（DropNew）**: 行2265-2279
- **消費**: 行2338-2359
- **上限変更**: 行2363-2411

### バッチ処理
- **開始/終了**: 行2201-2227
- **変更通知**: 行2439-2461

### UI用スナップショット
- **構造体定義**: 行2528-2534
- **取得メソッド**: 行2539-2558
- **最新順取得**: 行2565-2601

### スキル使用時変換
- **ノーマル倍率**: 行2608-2609 - 1.48～1.66倍
- **変換処理**: 行2618-2690

---

## 人間状況システム

### 状況定義
- **現在状況**: 行3708-3709 - `public Demeanor NowCondition;`
- **連続ターン**: 行3714-3715
- **累積ターン**: 行3720-3721

### 状況遷移
- **精神属性別初期化**: 行3785-4008
- **時間経過変化**: 行4012-4099
  - 覚悟→高揚（17ターン）
  - 怒り→普調（10ターン）/高揚（23ターン）
  - その他各種パターン

### イベント時変化
- **仲間死亡時**: 行4104-4512
- **敵撃破時**: 行4516-5601
- **仲間復活時**: 行5606-5700

---

## その他の機能

### パワーシステム
- **現在パワー**: 行922 - `public PowerLevel NowPower = PowerLevel.Medium;`
- **パワー上昇**: 行927-938
- **パワー下降**: 行943-953
- **歩行時変化**: 行957-1248

### VitalLayer（追加HP）
- **リスト管理**: 行253
- **初期IDリスト**: 行257
- **所持確認**: 行909-912
- **除去**: 行913-920

### リカバリターン
- **基礎設定値**: 行3060
- **最大値計算**: 行3076-3082
- **戦場復帰判定**: 行3121-3138
- **カウント開始**: 行3142-3147

### 死亡・復活
- **死亡判定**: 行9574-9586
- **死亡コールバック**: 行9605-9632
- **復活処理**: 行9590-9601
- **破壊フラグ**: 行9544
- **オーバーキル破壊率**: 行9551-9568

### 記憶システム
- **AimStyle記憶**: 行2036
- **精神属性記憶**: 行10855-10885
- **永続記憶**: 行10936-10958

### 思えの値
- **最大値計算**: 行3556-3569
- **現在値**: 行3582-3596
- **リセット**: 行3600
- **回復**: 行3604-3609
- **歩行時回復**: 行3618-3621

### ターゲット意思
- **DirectedWill**: 行3628
- **RangeWill**: 行3634
- **範囲意思判定**: 行3640-3648

---

## 使用上の注意

### 継承時の実装必須項目
1. `SkillList`プロパティの実装（行6175）
2. 各種virtual関数のオーバーライド検討

### 初期化順序
1. PassiveManager初期化前のアクセス注意（行307-311）
2. テンプレートからランタイム辞書へのコピー（行1356-1367）
3. UIコントローラーのバインド（行29-44）

### パフォーマンス考慮点
- パッシブリストのコピー作成（途中Remove対策）
- 十日能力計算のキャッシュ
- 慣れ補正の段階的計算

---

## 関連ファイル・クラス
- `UIController`: UI管理
- `BattleManager`: 戦闘管理
- `PassiveManager`: パッシブマスタ管理
- `WeaponManager`: 武器マスタ管理
- `StatesPowerBreakdown`: ステータス内訳管理
- `TenDayAbilityDictionary`: 十日能力辞書
- 各種Config（AttackPowerConfig, DefencePowerConfig等）

---

## 追加発見項目（2025/01/09追記）

### キャラクタータイプ定義
- **列挙型定義**: 行3757
  ```csharp
  public enum CharacterType
  {
      Enemy = 0,
      Player = 1,
      Phantom = 2,
      SubCharacter = 3,
      TLOA = 4
  }
  ```

### 精神属性定義
- **列挙型定義**: 行3765-3776
  ```csharp
  public enum SpiritualProperty
  {
      dream,      // 夢うつつ
      raincoat,   // レインコート
      Cquiest,    // シークイエスト
      dokumamushi, // 独身毒虫
      kindergarten,// 幼稚園の男の子
      nothing,    // 属性なし
      selfSacrifice,// 自己犠牲
      dormis,     // ドーミス
      reiminal    // リーミナル
  }
  ```

### 耐性値定義
- **耐性プロパティ**: 行3781-3784
  ```csharp
  public float PosionResistance;     // 毒耐性
  public float ParalysisResistance;  // 麻痺耐性
  public float BreakResistance;      // 破壊耐性
  public float MadnessResistance;    // 狂化耐性
  ```

### CSVデータ読み込みシステム
- **精神属性修正辞書**: 行11308-11406
  - CSV形式でスキルvs精神属性の修正値を管理
  - 3×3マトリックスで相性を定義
  - GetSkillVsCharaSpiritualModifierでアクセス

### 防御変換しきい値辞書
- **定義位置**: 行11236-11269
  - AimStyle別の防御変換境界値
  - 人間状況別の特定値設定

### ディープコピー機能
- **CreateDeepCopy**: 行11434-11498
  - ステータスの完全複製生成
  - ポインタ・参照を除く全値コピー

### 落ち着きシステム
- **カウント管理**: 行7804-7841
  ```csharp
  int CalmDownCount = 0;           // 落ち着きカウント
  int CalmDownCountMax;             // 最大値（4-8のランダム）
  public void CalmDownSet()         // カウント開始準備
  void CalmDownCountDec()           // カウントダウン
  void CalmDown()                   // 即座に落ち着かせる
  ```
- **効果適用**: 行7763-7793
  - 思えダメージの減衰に影響
  - スキル使用時にセット（行9353）
  - 死亡時リセット（行9630）
  - ターン終了時デクリメント（行9869）

### ケレン行動（KerenACT）システム
- **デフォルト値**: CommonCalc.cs 行10 - `KerenACTRateDefault = 4.4f`
- **パッシブ連携**: 行767-786
  ```csharp
  public float PassivesAttackACTKerenACTRate()   // 攻撃側ケレン率
  public float PassivesDefenceACTKerenACTRate()  // 防御側ケレン率
  ```
- **最小命中率保証**: 行7869-7897
  - 攻撃/防御側のケレン率から最小命中率計算
  - 両側が高い場合シナジー効果

### TLOA（True Line of Acceptance）システム
- **リバハル管理**: 行1328-1347
  ```csharp
  public float Rivahal;  // TLOAダメージ累積値
  public void RivahalDream()  // TLOA攻撃時の累積処理
  ```
- **威力減衰**: 行7061-7074
  - HP38%以下で0.7倍まで減衰
- **精神補正**: 行7279-7280
  - TLOA以外: 20%の精神補正
  - TLOA: 40%の精神補正
- **ゆりかご考慮**: 行7294-7296
  - TLOAスキルは特別なレベル計算

### スキルリソース消費システム
- **TryConsumeForSkillAtomic**: 行2366-2444
  - トランザクション的な属性ポイント消費
  - 失敗時は全ロールバック
  - AttributeCondition構造体で条件定義

### ダメージ記録システム
- **FocusedSkillImpressionAndUser内部クラス**: 行11572-11617
  ```csharp
  float _topDmg;                    // 最高ダメージ記録
  public float TopDmg => _topDmg;   // 公開プロパティ
  public void DamageMemory(float)   // ダメージ記録メソッド
  ```
- **使用箇所**: 行10661 - 慣れ補正の優先順位決定用

### 前のめり関連
- **状態判定**: BattleManager経由で管理
  - IsVanguard()での判定（行3125, 6265, 7919, 8008）
- **効果**:
  - リカバリターン2倍進行（行3124-3125）
  - 回避率補正（行6265）
  - 爆破攻撃のかすり化（行8008）

### 戦闘開始処理
- **OnBattleStartNoArgument**: 行9906-9941
  - ダメージ記録辞書初期化
  - パッシブ持続ターンリセット
  - 精神HP最大値セット
  - スキルコールバック呼び出し
- **ApplyConditionOnBattleStart**: 行3853-4008
  - 精神属性による人間状況初期化

---

最終更新: 2025/01/09
ファイルバージョン: pigRPG/Assets/Script/BaseStates.cs