# BaseStates.cs リファクタリング体系図

## 現状: 12,637行の巨大ファイル（45+機能カテゴリ）

## 推奨アーキテクチャ: 機能別モジュール分割

```
BaseStates.cs
│
├── 【コア基盤】
│   ├── BaseStates.Core.cs (500行)
│   │   ├── クラス定義・基本フィールド
│   │   ├── UI連携
│   │   ├── マネージャー参照
│   │   └── 初期化・終了処理
│   │
│   └── BaseStates.DataStructures.cs (500行)
│       ├── DamageOptions
│       ├── SkillApplyPolicy
│       ├── DamageData
│       └── その他構造体・enum定義
│
├── 【戦闘システム群】3,000行
│   ├── BaseStates.Damage.cs (1,400行) ★最優先整理対象
│   │   ├── ダメージポリシー定義
│   │   ├── ダメージ記録・履歴
│   │   ├── ダメージ前処理
│   │   ├── ダメージ計算（基礎/魔法/物理）
│   │   ├── メインダメージ処理
│   │   ├── 特殊ダメージ（身代わり/思え）
│   │   └── バリア・防御層
│   │
│   ├── BaseStates.Combat.cs (800行)
│   │   ├── 戦闘開始・終了処理
│   │   ├── ターン管理
│   │   ├── 割り込みカウンター
│   │   ├── 先制攻撃
│   │   └── 戦闘計算補助
│   │
│   └── BaseStates.Skills.cs (800行)
│       ├── スキルリスト管理
│       ├── スキル実行
│       ├── 連続攻撃システム
│       ├── 非ダメージ敵対行動
│       └── スキルグルーピング
│
├── 【ステータス管理群】2,500行
│   ├── BaseStates.Stats.cs (1,000行)
│   │   ├── 基礎ステータス（ATK/DEF/EYE/AGI）
│   │   ├── ステータス計算
│   │   └── プロトコル処理
│   │
│   ├── BaseStates.HP.cs (500行)
│   │   ├── HP管理
│   │   ├── 精神HP
│   │   ├── 思え値
│   │   └── VitalLayer
│   │
│   └── BaseStates.StatusEffects.cs (1,000行)
│       ├── パワーシステム
│       ├── 人間状況（状態遷移）
│       └── 各種耐性値
│
├── 【パッシブシステム】1,200行
│   └── BaseStates.Passives.cs
│       ├── パッシブ管理
│       ├── パッシブイベント
│       ├── パッシブ効果計算
│       ├── パッシブ生存管理
│       └── バッファシステム
│
├── 【能力システム群】1,500行
│   ├── BaseStates.TenDayAbility.cs (550行)
│   │   ├── 十日能力定義
│   │   ├── 能力計算
│   │   ├── 勝利ブースト
│   │   └── 成長・減少
│   │
│   ├── BaseStates.AttrPoints.cs (800行)
│   │   ├── 属性ポイント管理
│   │   ├── ポイント操作
│   │   ├── バッチ処理
│   │   ├── 歩行時減衰
│   │   └── スキル変換
│   │
│   └── BaseStates.Modifiers.cs (150行)
│       ├── 特殊修正子
│       └── CharaConditionalModifier
│
├── 【行動システム群】1,500行
│   ├── BaseStates.Actions.cs (500行)
│   │   ├── 行動記録
│   │   ├── ターゲット意思
│   │   ├── Freeze/キャンセル
│   │   └── 連続行動管理
│   │
│   ├── BaseStates.Recovery.cs (200行)
│   │   ├── リカバリターン
│   │   └── 戦場復帰
│   │
│   └── BaseStates.Adaptation.cs (800行)
│       ├── 慣れ補正メイン
│       ├── 記憶システム
│       └── 落ち着きシステム
│
├── 【イベント処理群】2,000行
│   └── BaseStates.Events.cs
│       ├── 死亡・復活
│       ├── 歩行時処理
│       ├── 人間状況イベント
│       │   ├── 仲間死亡時
│       │   ├── 敵撃破時
│       │   └── 仲間復活時
│       └── 各種コールバック
│
└── 【ユーティリティ】500行
    └── BaseStates.Utils.cs
        ├── DeepCopy
        ├── CSV読み込み
        └── 各種ヘルパー関数
```

## リファクタリング優先順位

### 第1段階: ダメージシステム分離 ★最優先
```csharp
// BaseStates.Damage.cs
public partial class BaseStates
{
    #region ■■■ ダメージポリシー定義 ■■■
    public class DamageOptions { ... }
    public class SkillApplyPolicy { ... }
    #endregion

    #region ■■■ ダメージ記録管理 ■■■
    public List<DamageData> damageDatas;
    Dictionary<BaseStates, float> DamageDealtToEnemyUntilKill;
    void RecordDamageDealtToEnemyUntilKill() { ... }
    #endregion

    #region ■■■ ダメージ前処理 ■■■
    void PassivesOnBeforeDamage() { ... }
    bool PassivesOnBeforeDamageActivate() { ... }
    void DontDamagePassiveEffect() { ... }
    void PassivesDamageReductionEffect() { ... }
    #endregion

    #region ■■■ ダメージ計算 ■■■
    bool GetBaseCalcDamageWithPlusMinus22Percent() { ... }
    StatesPowerBreakdown MagicDamageCalculation() { ... }
    StatesPowerBreakdown NonMagicDamageCalculation() { ... }
    #endregion

    #region ■■■ メインダメージ処理 ■■■
    public StatesPowerBreakdown DamageOnBattle() { ... }
    public StatesPowerBreakdown Damage() { ... }
    public float SimulateDamage() { ... }
    #endregion

    #region ■■■ 特殊ダメージ ■■■
    public void RatherDamage() { ... }        // 身代わり
    public void ResonanceDamage() { ... }     // 思えダメージ
    #endregion

    #region ■■■ ダメージ後処理 ■■■
    void PassivesOnAfterDamage() { ... }
    void PassivesOnAfterAlliesDamage() { ... }
    #endregion
}
```

### 第2段階: パッシブシステム分離
- パッシブ管理をBaseStates.Passives.csに移動
- イベントハンドラを整理
- バッファシステムを統合

### 第3段階: ステータス計算分離
- ATK/DEF/EYE/AGI計算をBaseStates.Stats.csに
- HP管理をBaseStates.HP.csに
- 修正子をBaseStates.Modifiers.csに

### 第4段階: スキルシステム分離
- スキル実行ロジックを独立
- 連続攻撃を整理
- 非ダメージ行動を統合

### 第5段階: イベント・状態管理分離
- 人間状況を独立モジュール化
- 死亡・復活処理を整理
- 歩行時処理を統合

## メリット

1. **可読性向上**: 各ファイル2,000行以下で管理しやすい
2. **保守性向上**: 機能ごとに独立して修正可能
3. **テスト容易性**: モジュール単位でテスト可能
4. **並行開発**: チームで異なる機能を同時開発可能
5. **ビルド時間短縮**: 変更箇所のみ再コンパイル

## 実装上の注意点

1. **partial class**を使用して段階的に分割
2. **#region**でセクション明確化
3. **privateメンバー**はInternalVisibleToで共有
4. **依存関係**を最小限に保つ
5. **インターフェース**で疎結合を維持

## 移行計画

### Phase 1 (1週間)
- BaseStates.Damage.cs作成
- ダメージ関連機能を移動
- テスト実施

### Phase 2 (1週間)
- BaseStates.Passives.cs作成
- パッシブ機能を移動
- 統合テスト

### Phase 3 (2週間)
- 残りのモジュール分割
- 全体統合テスト
- パフォーマンス検証

## 最終目標

- 各ファイル最大2,000行
- 機能の重複なし
- 明確な責任分離
- 高い保守性とテスト可能性