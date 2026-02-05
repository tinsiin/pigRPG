# BattleSession (Phase 3a)

## 目的
- 戦闘の開始/進行/入力/終了を単一のセッションに集約する
- BattleManager の肥大化を抑え、依存注入の入口を統一する
- UI/歩行/AI など外部システムからの操作口を固定する

## 役割
- 戦闘ライフサイクルの統括（Start/Advance/ApplyInput/End）
- 戦闘コンテキストの公開（IBattleContext）
- BattleEvent の発行と購読の窓口提供
- 依存注入のルートを一本化

## API 概要（案）
```csharp
public interface IBattleSession
{
    IBattleContext Context { get; }
    BattleEventBus EventBus { get; }

    void Start();
    TabState Advance();
    UniTask<TabState> ApplyInputAsync(BattleInput input);
    UniTask EndAsync();
}
```

### 操作フロー
1. `Start()` で初期化・開始演出準備・BattleStarted を発行
2. `Advance()` で次アクター選択（ACTPop 相当）
3. `ApplyInputAsync()` で入力/AI を適用して次状態へ
4. `EndAsync()` で終了処理・クリーンアップ・BattleEnded を発行

## BattleInput（案）
- UI/AI からの入力を統一する DTO
- TabState に依存しない形で「やりたいこと」を表現する

```csharp
public enum BattleInputType
{
    SelectSkill,
    SelectRange,
    SelectTarget,
    StockSkill,
    Escape,
    DoNothing,
    Next,
    Cancel
}

public readonly struct BattleInput
{
    public BattleInputType Type { get; }
    public BaseStates Actor { get; }
    public BaseSkill Skill { get; }
    public DirectedWill TargetWill { get; }
    public IReadOnlyList<BaseStates> Targets { get; }
    public SkillZoneTrait RangeWill { get; }
    public bool IsOption { get; }

    // 省略: 生成ヘルパー
}
```

## BattleSetupResult との関係
- BattleSetupResult は「起動条件/初期配置/依存注入」までを担当
- BattleSession は「起動後の進行/入力/終了」までを担当
- Setup は Session を返すファクトリに寄せる

## 依存注入方針
- 現行実装は `BattleManager` を受け取るラッパーとして開始する
- 将来的に BattleServices/IBattleMetaProvider を Session に集約する
- UI/歩行/AI からは IBattleSession のみを参照する

## 既存構造への差し込み点
- BattleManager の内部処理を BattleSession に段階的に移譲
- BattleUIBridge.Active / BattleContextHub 依存を Session 参照に置換
- Walking/BattleRunner からは Session.Start/End を呼ぶ

## 受け入れ条件
- Hub/Active に触れずに戦闘が進行できる
- UI差し替え時の修正が BattleSession 外に漏れない
- セッション単位でテスト可能になる
