# BaseSkill Custom Editor 実装計画

## 背景

`SkillZoneTraitDrawer`（PropertyDrawer）でZoneTraitのグループ表示＋バリデーション警告を実装済み。
次のステップとして、BaseSkill全体のInspectorをカスタム化し、設定ミスの防止と視認性の向上を図る。

## 目的

- BaseSkillの多数のフィールドをセクション分けして見やすくする
- 必須フィールドの未設定を即座に警告する
- 既存のPropertyDrawer（SkillZoneTraitDrawer）はそのまま活用される
- **テンプレート機能**でよくあるスキルパターンを一発適用できるようにする
- **デフォルト値**で新規スキルの空リストクラッシュを防止する

## 新規ファイル

`Assets/Editor/BaseSkillEditor.cs`

## セクション構成案（v3: サマリパネル追加・10セクション）

旧15セクション → 10セクションに統合。スキル設定時の思考フロー「何者？→どう当たる？→どのくらい痛い？→コストは？→実行の仕組みは？→動作パターンは？→成長は？→付与効果は？」に沿う。

最上部にスキル概要パネルを配置し、必須設定の状態と基本情報を一目で把握可能にする。

```
┌─────────────────────────────────────────────────────┐
│ テンプレート: [単体攻撃（選択）  ▼] [適用]            │
├─────────────────────────────────────────────────────┤
│ ■ スキル概要                                         │
│                                                      │
│  スキル名:  炎の一撃                                  │
│                                                      │
│  ━━ 必須設定 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│  攻撃性質:  Attack | Magic                ✔ 設定済み │
│  範囲性質:  選択単体 + 状況制御 + ランダム ✔ 設定済み │
│  レベル数:  3 (有限) + 無限               ✔ 設定済み │
│                                                      │
│  ━━ 基本情報 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│  連撃性質:  Fixed (3回)                               │
│  精神属性:  Fire                                      │
│  物理属性:  Slash                                     │
│  コスト:    NP: 5 / 属性: 火3                         │
│  威力:      Lv0 = 10.0                                │
│  命中補正:  +15%                                      │
│                                                      │
│  ⚠ ZoneTraitが未設定です！                 ← 問題時  │
│  ⚠ 攻撃性質が未設定です！                  ← のみ表示│
├─────────────────────────────────────────────────────┤
│ ▼ ① 基本情報 — このスキルは何者か                    │
│   SkillName                                          │
│   SkillSpiritual, SkillPhysical                      │
│   Impression, MotionFlavor, SpecialFlags             │
├─────────────────────────────────────────────────────┤
│ ▼ ② スキル性質 — どういう攻撃か、誰に当たるか       │
│   _baseSkillType, ConsecutiveType                    │
│   ZoneTrait (← PropertyDrawer自動適用)               │
│   DistributionType,                                  │
│   PowerRangePercentageDictionary,                    │
│   HitRangePercentageDictionary                       │
├─────────────────────────────────────────────────────┤
│ ▼ ③ 威力・命中・ダメージ — どのくらい痛いか          │
│   _skillHitPer                                       │
│   _mentalDamageRatio, _defAtk                        │
│   _powerSpread, Cantkill                             │
├─────────────────────────────────────────────────────┤
│ ▼ ④ コスト・補正 — 使うのに何が要るか、使い手への影響│
│   RequiredNormalP, RequiredAttrP,                    │
│   RequiredRemainingHPPercent                         │
│   EvasionModifier, AttackModifier,                   │
│   AttackMentalHealPercent, SKillDidWaitCount         │
├─────────────────────────────────────────────────────┤
│ ▼ ⑤ 連撃・ストック・トリガー — 実行の仕組み          │
│   連撃:                                              │
│     _RandomConsecutivePer                            │
│   ストック:                                          │
│     _defaultStockCount, _stockPower,                 │
│     _stockForgetPower                                │
│   トリガー:                                          │
│     _triggerCountMax, CanCancelTrigger,              │
│     _triggerRollBackCount                            │
├─────────────────────────────────────────────────────┤
│ ▼ ⑥ 前のめり — いつ前のめりになるか                  │
│   IsAggressiveCommit,                                │
│   IsReadyTriggerAggressiveCommit,                     │
│   IsStockAggressiveCommit,                            │
│   CanSelectAggressiveCommit                          │
├─────────────────────────────────────────────────────┤
│ ▼ ⑦ ムーブセット — 連撃時の詳細動作パターン          │
│   _a_moveset, _b_moveset                             │
├─────────────────────────────────────────────────────┤
│ ▼ ⑧ スキルレベル — レベルごとの成長データ            │
│   FixedSkillLevelData (リスト)                       │
│   ⚠ 空の場合に警告表示                               │
│   _infiniteSkillPowerUnit,                           │
│   _infiniteSkillTenDaysUnit                          │
├─────────────────────────────────────────────────────┤
│ ▼ ⑨ エフェクト・パッシブ付与 — 何を付与/除去するか   │
│   [付与]                                             │
│     subEffects, subVitalLayers                       │
│   [除去]                                             │
│     canEraceEffectIDs, CanEraceEffectCount,          │
│     canEraceVitalLayerIDs, CanEraceVitalLayerCount   │
│   [スキルパッシブ]                                    │
│     ReactiveSkillPassiveList,                        │
│     AggressiveSkillPassiveList,                      │
│     TargetSelection, ReactionCharaAndSkillList,      │
│     SkillPassiveEffectCount,                         │
│     _skillPassiveGibeSkill_SkillFilter               │
├─────────────────────────────────────────────────────┤
│ ▼ ⑩ バリデーション結果                               │
│   全体の検証結果サマリ                                │
└─────────────────────────────────────────────────────┘
```

### v1→v2 の変更理由

| 変更 | 理由 |
|------|------|
| 攻撃タイプ + 範囲性質 + 分散性質 → **②スキル性質** | 全て「どう当たるか」の設定。ZoneTraitと分散は一緒に見たい |
| コスト + 戦闘補正 → **④コスト・補正** | 「使うとどうなるか」で1グループ |
| 連撃 + ストック + トリガー → **⑤** | 全て「実行の仕組み」。ConsecutiveType選んだ直後にストック値を設定できる |
| エフェクト + スキルパッシブ → **⑨** | 全て「付与効果」。サブグループ[付与]/[除去]/[スキルパッシブ]で整理 |
| 各セクションに日本語の一行説明 | 何を設定するセクションか一目瞭然 |

## バリデーション項目

### Error（赤・設定しないとクラッシュ or 動作不能）
| チェック内容 | 表示 |
|-------------|------|
| `FixedSkillLevelData` が空 | スキルレベルデータが未設定です |
| `ZoneTrait == 0` | ZoneTraitが未設定です（PropertyDrawerでも表示） |

### Warning（黄・意図しない動作の可能性）
| チェック内容 | 表示 |
|-------------|------|
| `SkillName` がデフォルト値のまま | スキル名を設定してください |
| `_baseSkillType == 0` | スキルの攻撃性質が未設定です（攻撃判定がfalseになります） |
| `ControlByThisSituation` + 事故フラグなし | 状況制御の事故用フラグを設定してください（PropertyDrawerでも表示） |
| `SkillZoneTraitNormalizer.Validate()` 失敗 | Normalizerの出力メッセージ（PropertyDrawerでも表示） |

## デフォルト値（Reset）

BaseSkillに `Reset()` メソッドを追加。Unityがコンポーネント追加時・Inspector右クリック→Reset時に自動呼出。
「設定しないと実行時に例外でクラッシュするフィールド」のみを初期化する。

```csharp
// BaseSkill.Core.cs
void Reset()
{
    // FixedSkillLevelData — 空だと_skillPower(), TenDayValues()等でArgumentOutOfRangeException
    if (FixedSkillLevelData == null || FixedSkillLevelData.Count == 0)
        FixedSkillLevelData = new List<SkillLevelData> { new SkillLevelData() };

    // _powerSpread — nullだとPowerSpreadプロパティ経由でNullReferenceException
    if (_powerSpread == null)
        _powerSpread = new float[0];

    // ムーブセット — nullだとDecideNowMoveSet_A0_B1()でNullReferenceException
    if (_a_moveset == null) _a_moveset = new List<MoveSet>();
    if (_b_moveset == null) _b_moveset = new List<MoveSet>();
}
```

### 設定するもの（nullや空だと例外でクラッシュ）

| フィールド | 初期値 | クラッシュ箇所 |
|---|---|---|
| `FixedSkillLevelData` | 1エントリ自動生成（SkillPower=0, TenDayValues=空） | `_skillPower()`, `TenDayValues()`, `SkillHitPer` |
| `_powerSpread` | `new float[0]` | `PowerSpread`プロパティ → 分散計算 |
| `_a_moveset` | `new List<MoveSet>()` | `DecideNowMoveSet_A0_B1()` |
| `_b_moveset` | `new List<MoveSet>()` | `DecideNowMoveSet_A0_B1()` |

### 設定しないもの（あえて警告で促す）

| フィールド | 理由 |
|---|---|
| `ZoneTrait` | 0のまま → PropertyDrawerの赤警告で設定を強制（実装済み） |
| `_baseSkillType` | 0だと攻撃判定falseになるが、意図的な非攻撃スキルもある → バリデーション警告で対応 |
| `SkillName` | デフォルト文字列のまま → バリデーション警告で対応 |

## テンプレート機能

Custom Editorの最上部にドロップダウン＋適用ボタンを配置。
よくあるスキルパターンの ZoneTrait + FixedSkillLevelData[0].SkillPower を一括適用する。

### テンプレート定義

エディタコード内にハードコードで定義（ScriptableObject管理は大掛かりすぎるため不採用）。

| テンプレート名 | ZoneTrait | SkillPower初期値 |
|---|---|---|
| 単体攻撃（選択） | CanSelectSingleTarget + ControlByThisSituation + RandomSingleTarget | 10 |
| 単体攻撃（ランダム） | RandomSingleTarget | 10 |
| 範囲攻撃（選択） | CanSelectMultiTarget + ControlByThisSituation + RandomMultiTarget | 7 |
| 全体攻撃 | AllTarget | 5 |
| 回復（単体味方） | CanSelectSingleTarget + SelectOnlyAlly | 10 |
| 回復（全体味方） | AllTarget + SelectOnlyAlly | 5 |
| 自己スキル | SelfSkill | 0 |

### テンプレートが適用する項目

- **ZoneTrait** — 対象選択フラグ一式
- **FixedSkillLevelData[0].SkillPower** — 初期パワー値（リストが空なら1エントリ自動生成）

### テンプレートが適用しない項目（スキル固有のため）

- SkillName
- TenDayValues（印象構造）
- コスト系（RequiredNormalP, RequiredAttrP等）
- MentalDamageRatio
- スキルパッシブ系
- _infiniteSkillPowerUnit / _infiniteSkillTenDaysUnit

### UI動作

1. ドロップダウンからテンプレートを選択
2. 「適用」ボタンを押すと ZoneTrait と FixedSkillLevelData を上書き
3. SerializedProperty経由なので Undo(Ctrl+Z) で元に戻せる
4. 既に設定済みのフィールドも上書きされる（意図的な再設定に対応）

## スキル概要パネル仕様

テンプレート行の直下、セクション①の上に配置。スキルの現在状態を一目で把握するためのパネル。

### 必須設定（強調表示）

設定状態に応じて色を変える。未設定なら赤文字、設定済みなら緑の✔。

| 表示項目 | 参照フィールド | 未設定の判定 |
|---|---|---|
| 攻撃性質 | `_baseSkillType` | `== 0` |
| 範囲性質 | `ZoneTrait` | `== 0` |
| レベル数 | `FixedSkillLevelData` | `.Count == 0` |

**表示フォーマット例:**
- 設定済み: `攻撃性質:  Attack | Magic  ✔`（緑系）
- 未設定:   `攻撃性質:  未設定  ⚠`（赤系）
- レベル数: `レベル数:  3 (有限) + 無限  ✔` / `レベル数:  0  ⚠`
  - 有限 = `FixedSkillLevelData.Count`
  - `+ 無限` は `_infiniteSkillPowerUnit > 0` の場合のみ表示

### 基本情報（通常表示）

必須設定の下に、薄めの色でスキルの主要パラメータを読み取り専用で表示。

| 表示項目 | 参照フィールド | 表示フォーマット |
|---|---|---|
| スキル名 | `SkillName` | そのまま表示 |
| 連撃性質 | `ConsecutiveType` | enum名（ATKCount表示も可能なら追加） |
| 精神属性 | `SkillSpiritual` | Flags展開表示 |
| 物理属性 | `SkillPhysical` | Flags展開表示 |
| コスト | `RequiredNormalP`, `RequiredAttrP` | `NP: 5 / 属性: 火3` 形式 |
| 威力 | `FixedSkillLevelData[0].SkillPower` | `Lv0 = 10.0` |
| 命中補正 | `_skillHitPer` | `+15%` / `0%`（0なら非表示でも可） |

### 警告エリア

パネル最下部に、バリデーション項目のError/Warningを `EditorGUILayout.HelpBox()` で表示。
問題がなければ何も表示しない（パネルがコンパクトになる）。

### 実装メモ

- パネル全体を `EditorGUILayout.BeginVertical(EditorStyles.helpBox)` で囲んで枠線付きに
- 必須設定行は `EditorGUILayout.BeginHorizontal()` で「ラベル : 値 : 状態アイコン」の3列
- 色の切り替えは `GUI.color` で一時的に変更、描画後に復元
- パネルは折りたたみ不可（常に表示して概要を見失わない）

## 実装方針

- `[CustomEditor(typeof(BaseSkill), true)]` — `true` でサブクラス（AllySkill等）にも適用
- 各セクションは `EditorGUILayout.BeginFoldoutHeaderGroup` で折りたたみ可能
- `serializedObject.FindProperty()` で各フィールドを取得、`EditorGUILayout.PropertyField` で描画
- ZoneTraitフィールドは `PropertyField` 経由で既存の `SkillZoneTraitDrawer` が自動適用される
- `SerializedProperty` ベースで実装し、Undo/Redo・マルチオブジェクト編集に対応
- テンプレートドロップダウンは `EditorGUILayout.Popup` + ボタンで実装
- テンプレート適用時は `Undo.RecordObject` でUndo対応
- `Reset()` は BaseSkill.Core.cs に追加（Editorコードではない）
- スキル概要パネルは `OnInspectorGUI()` の先頭（テンプレート行の直後）に描画

## 既存インフラ

| クラス | 用途 |
|--------|------|
| `SkillZoneTraitDrawer` (`Editor/SkillZoneTraitDrawer.cs`) | ZoneTraitフィールドのPropertyDrawer（実装済み） |
| `SkillTraitValidator` (`Editor/SkillTraitValidator.cs`) | スキル全体のバリデーション |
| `SkillZoneTraitNormalizer` | 矛盾検出 |

## 注意点

- BaseSkillは `partial class` で16ファイルに分散している:
  - Core, CallBack, CountRecord, SkillHit, SkillLevel, SkillPowe, SkillPassive, TenDays
  - Trigger, PowerSpread, Consecutive, MoveSet, DEFATK, Effects, Distribution
  - MentalDamageRatio, AggressiveCommit, HasMethod, AimStyle
- `[SerializeField]` の private フィールドも `serializedObject.FindProperty("_fieldName")` でアクセス可能
- AllySkillなどサブクラス固有のフィールドは `CustomEditor(true)` で自動的に末尾に表示されるが、必要なら個別の `AllySkillEditor` を作成

## 全シリアライズフィールド一覧

実装時の `FindProperty()` 参照用。セクション番号は構成案v2に対応。`[NonSerialized]` は除外。

### ① 基本情報
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `SkillName` | string | スキル名 | — | Core |
| `SkillSpiritual` | SpiritualProperty | 精神属性 | `[Header("スキルの精神属性")]` | Core |
| `SkillPhysical` | PhysicalProperty | 物理属性 | — | Core |
| `Impression` | SkillImpression | スキル印象 | — | Core |
| `MotionFlavor` | MotionFlavorTag | 動作的雰囲気 | — | Core |
| `SpecialFlags` | SkillSpecialFlag | 特殊判別性質 | — | Core |

### ② スキル性質
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `_baseSkillType` | SkillType | 攻撃性質（private） | `[Header("スキルの攻撃性質")]` | Core |
| `ConsecutiveType` | SkillConsecutiveType | 連撃性質 | — | Core |
| `ZoneTrait` | SkillZoneTrait | 範囲性質（PropertyDrawer適用） | `[Header("スキルの範囲性質")]` | Core |
| `DistributionType` | AttackDistributionType | 分散性質 | `[Header("分散性質")]` | Distribution |
| `PowerRangePercentageDictionary` | SerializableDictionary | 威力の範囲別割合差分 | — | Distribution |
| `HitRangePercentageDictionary` | SerializableDictionary | 命中率の範囲別補正 | — | Distribution |

### ③ 威力・命中・ダメージ
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `_skillHitPer` | int | 命中補正（%） | `[Header("命中補正")]` | SkillHit |
| `_mentalDamageRatio` | float | 精神攻撃率 | `[Header("通常の精神攻撃率")]` | MentalDamageRatio |
| `_defAtk` | float | 防御無視率 | — | DEFATK |
| `_powerSpread` | float[] | 分散割合設定値 | `[Header("通常分散割合の設定値")]` | PowerSpread |
| `Cantkill` | bool | 殺せないスキル（1残る） | — | Core |

### ④ コスト・補正
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `RequiredNormalP` | int | 必要ノーマルポイント | `[Header("必要なコスト")]` | Core |
| `RequiredAttrP` | SerializableDictionary | 必要属性ポイント内訳 | — | Core |
| `RequiredRemainingHPPercent` | float | 必要残りHP割合 | `[Range(0f, 100f)]` | Core |
| `EvasionModifier` | float | 回避補正率 | `[Header("戦闘補正")]` | Core |
| `AttackModifier` | float | 攻撃補正率 | — | Core |
| `AttackMentalHealPercent` | float | 攻撃時精神HP回復% | — | Core |
| `SKillDidWaitCount` | int | 追加硬直値 | — | Core |

### ⑤ 連撃・ストック・トリガー
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `_RandomConsecutivePer` | float | ランダム連撃継続率（%） | `[Header("ランダム連撃")]` | Consecutive |
| `_defaultStockCount` | int | デフォルトストック数 | `[Header("ストック系")]` | Consecutive |
| `_stockPower` | int | ストック単位 | — | Consecutive |
| `_stockForgetPower` | int | ストック忘れ単位 | — | Consecutive |
| `_triggerCountMax` | int | トリガー必要カウント数 | `[Header("トリガーカウント")]` | Trigger |
| `CanCancelTrigger` | bool | トリガー中断可否 | — | Trigger |
| `_triggerRollBackCount` | int | 他スキル選択時の巻き戻りカウント | — | Trigger |

### ⑥ 前のめり
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `IsAggressiveCommit` | bool | スキル利用時に前のめり（default: true） | `[Header("前のめり設定")]` | AggressiveCommit |
| `IsReadyTriggerAggressiveCommit` | bool | 発動カウント時に前のめり | — | AggressiveCommit |
| `IsStockAggressiveCommit` | bool | ストック時に前のめり | — | AggressiveCommit |
| `CanSelectAggressiveCommit` | bool | 前のめり選択可 | — | AggressiveCommit |

### ⑦ ムーブセット
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `_a_moveset` | List\<MoveSet\> | 戦闘規格Aムーブセット | `[Header("戦闘規格 A ムーブセット...")]` | MoveSet |
| `_b_moveset` | List\<MoveSet\> | 戦闘規格Bムーブセット | `[Header("戦闘規格 B ムーブセット...")]` | MoveSet |

### ⑧ スキルレベル
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `FixedSkillLevelData` | List\<SkillLevelData\> | スキルレベルデータ（最低1つ必須） | `[Header("有限のスキルレベルの設定データ...")]` | SkillLevel |
| `_infiniteSkillPowerUnit` | float | 無限スキル威力単位 | `[Header("無限スキル威力単位")]` | SkillLevel |
| `_infiniteSkillTenDaysUnit` | float | 無限スキル10日能力単位 | — | SkillLevel |

#### SkillLevelData ネストクラス（各レベルごと）
| フィールド名 | 型 | 説明 |
|---|---|---|
| `TenDayValues` | TenDayAbilityDictionary | 十日能力値 |
| `SkillPower` | float | スキル威力 |
| `OptionMentalDamageRatio` | float? | レベル別精神攻撃率（null=基本値使用） |
| `OptionPowerSpread` | float[]? | レベル別分散割合（null=基本値使用） |
| `OptionSkillHitPer` | int? | レベル別命中補正（null=基本値使用） |
| `OptionA_MoveSet` | List\<MoveSet\>? | レベル別Aムーブセット（null=基本値使用） |
| `OptionB_MoveSet` | List\<MoveSet\>? | レベル別Bムーブセット（null=基本値使用） |

### ⑨ エフェクト・パッシブ付与

#### [付与]
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `subEffects` | List\<int\> | 付与するパッシブID | `[Header("付与するもの")]` | Effects |
| `subVitalLayers` | List\<int\> | 付与する追加HP ID | — | Effects |

#### [除去]
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `canEraceEffectIDs` | List\<int\> | 除去可能パッシブID範囲 | `[Header("除去できるもの")]` | Effects |
| `CanEraceEffectCount` | int | 除去可能パッシブ数 | — | Effects |
| `canEraceVitalLayerIDs` | List\<int\> | 除去可能追加HP ID範囲 | — | Effects |
| `CanEraceVitalLayerCount` | int | 除去可能追加HP数 | — | Effects |

#### [スキルパッシブ]
| フィールド名 | 型 | 説明 | Inspector属性 | ソースファイル |
|---|---|---|---|---|
| `ReactiveSkillPassiveList` | List\<BaseSkillPassive\> | 掛かってるパッシブ | `[Header("掛かってるスキルパッシブ")]` | SkillPassive |
| `AggressiveSkillPassiveList` | List\<BaseSkillPassive\> | 装弾されたパッシブ | `[Header("装弾されたスキルパッシブ")]` | SkillPassive |
| `TargetSelection` | SkillPassiveTargetSelection | パッシブ付与スキル選択方式 | — | SkillPassive |
| `ReactionCharaAndSkillList` | List\<SkillPassiveReactionCharaAndSkill\> | 反応式対象リスト | — | SkillPassive |
| `SkillPassiveEffectCount` | int | パッシブ付与上限数 | — | SkillPassive |
| `_skillPassiveGibeSkill_SkillFilter` | SkillFilter | パッシブ付与対象フィルター | — | SkillPassive |

#### SkillFilter ネストクラス
| フィールド名 | 型 | 説明 |
|---|---|---|
| 各種フィルター条件 | — | 精神属性/物理属性/印象/スキルタイプ等による絞り込み条件 |

### AllySkill固有（サブクラス）
| フィールド名 | 型 | 説明 | Inspector属性 |
|---|---|---|---|
| `_iD` | int | スキルID（有効化判定用） | `[SerializeField]` private |
| `Proficiency` | float | スキル熟練度 | public |

### 非シリアライズ（参考情報）
| フィールド名 | 型 | 説明 |
|---|---|---|
| `_battleContext` | IBattleContext | 戦闘コンテキスト（BindBattleContext()で注入） |
| `bufferSkillType` | SkillType | バッファ用スキルタイプ |
| `_nowSkillLevel` / `_cradleSkillLevel` | int | 実行時スキルレベル（計算結果） |

## 実装順序

1. **Reset()** — BaseSkill.Core.cs にデフォルト値を追加（クラッシュ防止、即効性あり）
2. **BaseSkillEditor 骨格** — 10セクション分け＋フィールド描画
3. **スキル概要パネル** — 必須設定の状態表示 + 基本情報サマリ
4. **バリデーション警告** — パネル下部にError/Warning表示
5. **テンプレート機能** — ドロップダウン＋適用ボタン
