# グローバルハブ整理 リファクタリング計画（やりやすい版）

## 0. この計画の使い方
- 1作業 = 1ファイル/1クラス単位で進める（小さく完結）
- 各作業の最後に「最小テスト」を必ず実施
- 各Phase完了時に使用箇所数を再計測して数字で確認

---

## 1. 目的・ゴール

### 1.1 目的
- Hub依存を減らし、依存関係を明示化
- ユニットテストでモック注入を可能にする
- 初期化順序の問題を解消

### 1.2 成功基準（数値）
- BattleContextHubの使用箇所を **50%以上削減**
- 新規追加コードでHub直接参照を **禁止**（コンストラクタ/Initializeで注入）
- 主要クラスにテスト用コンストラクタ/Initializeを追加

### 1.3 非目標
- 全Hubの完全削除はしない（互換性維持のため残す）
- UIStateHubの廃止はしない（ReactivePropertyとの連携が密なため）

---

## 2. 現状と対象（まず再計測する）

### 2.1 Hub一覧（参考値）
| Hub | 用途 | 使用箇所 | 優先度 |
|-----|------|----------|--------|
| **BattleContextHub** | バトル中のコンテキスト | 10箇所 | 高 |
| **BattleOrchestratorHub** | UI操作の中継 | 6箇所 | 中 |
| **GameContextHub** | ゲーム全体の状態 | 5箇所 | 低 |
| **UIStateHub** | UI状態（ReactiveProperty） | 12箇所 | 低 |

### 2.2 再計測コマンド（作業前に実施）
- `rg "BattleContextHub\.Current" -g'*.cs'`
- `rg "BattleOrchestratorHub\.Current" -g'*.cs'`
- `rg "GameContextHub\.Current" -g'*.cs'`
- `rg "UIStateHub" -g'*.cs'`

---

## 3. 共通ルール（迷わないための型）

### 3.1 Plain class の型（推奨）
```csharp
public class SomeClass
{
    private readonly IBattleContext _context;

    // 本番用：Hub経由（互換性維持）
    public SomeClass() : this(BattleContextHub.Current) { }

    // テスト用：直接注入
    public SomeClass(IBattleContext context)
    {
        _context = context;
    }
}
```

### 3.2 MonoBehaviour の型（推奨）
```csharp
public class SomeMonoBehaviour : MonoBehaviour
{
    private IBattleContext _context;

    // 外部から注入
    public void Initialize(IBattleContext context)
    {
        _context = context;
    }

    // フォールバック（互換性維持）
    private IBattleContext Context => _context ?? BattleContextHub.Current;
}
```

### 3.3 呼び出し元の型
- `new` / `Initialize` 経由で渡す
- 呼び出し元が複数ある場合は、集約ポイント（Factory/Bridge）からまとめて注入

---

## 4. 1ファイル作業テンプレ（毎回これを繰り返す）
1. 対象ファイル内のHub参照を洗い出し
2. 注入点を追加（コンストラクタ/Initialize）
3. Hub参照を注入変数に置換
4. 呼び出し元を更新
5. 最小テストを実施
6. `rg` で使用箇所を確認

---

## 5. 事前準備（チェックリスト）
- [ ] 作業ブランチを作成
- [ ] 現状の使用箇所数を `rg` で記録
- [ ] ビルド/プレイで基準動作を確認
- [ ] 対象クラスの呼び出し元を `rg "new ClassName"` などで洗い出し

---

## 6. フェーズ別タスク（小分け）

### Phase 1: 直参照の局所削除（優先度：高）
**目的:** 基底クラスに既にあるプロパティを使わず直接Hub参照している箇所を修正
**目標:** 10 → 6

#### 1-1 Slaim.cs
- [ ] `BattleContextHub.Current` → 基底の `manager` プロパティに置換（1行変更）
- 完了条件: 戦闘開始〜パッシブ発動が動作

#### 1-2 PartyMemberEffect.cs
- [ ] 「戦闘中チェック」用途のため、`BattleContextHub.IsInBattle` staticプロパティを新設
- [ ] `BattleContextHub.Current != null` → `BattleContextHub.IsInBattle` に置換
- 完了条件: 歩行中のパーティー編成変更が動作

#### 1-3 PartySetEffect.cs
- [ ] `BattleContextHub.Current != null` → `BattleContextHub.IsInBattle` に置換
- 完了条件: 歩行中のパーティー一括設定が動作

#### 1-4 BattleAIBrain.cs
- [ ] コンストラクタ or `Initialize` でIBattleContext受け取り追加
- [ ] Hub参照を注入に置換
- [ ] 呼び出し元の注入対応
- 完了条件: AIの行動が問題なく進行

#### Phase 1 最小テスト
- [ ] 戦闘開始/終了
- [ ] スキル使用
- [ ] パッシブ発動
- [ ] AI行動

---

### Phase 2: BattleOrchestratorHub（優先度：中）
**目標:** 6 → 1

#### 2-1 SelectRangeButtons.cs
- [ ] `Initialize(IBattleOrchestrator)` 追加
- [ ] Hub参照を注入に置換
- [ ] 生成側で `Initialize` 呼び出し
- 完了条件: 範囲選択UIが動作

#### 2-2 SelectTargetButtons.cs
- [ ] `Initialize(IBattleOrchestrator)` 追加
- [ ] Hub参照を注入に置換
- [ ] 生成側で `Initialize` 呼び出し
- 完了条件: 対象選択UIが動作

#### 2-3 AllyClass.cs
- [ ] Hub参照3箇所を引数化
- [ ] 呼び出し元から引数を渡す
- 完了条件: 味方のUI/行動が動作

#### 2-4 BattleUIBridge.cs
- [ ] Orchestrator参照を引数化
- [ ] 既存のHub参照はフォールバックに残す
- 完了条件: UI中継が動作

#### Phase 2 最小テスト
- [ ] 範囲選択UI
- [ ] 対象選択UI
- [ ] 味方操作

---

### Phase 3: 基底クラスの注入化（本丸、優先度：低）
**目的:** Hub依存の本質的な削減。BasePassive/BaseStates/BaseSkillの`manager`プロパティを注入方式に変更
**方針:** 既存 `protected` プロパティは残しつつ、新規メソッドは注入引数を使用

#### 3-1 BasePassive
- [ ] コンストラクタでIBattleContext受け取り（オプショナル）
- [ ] `manager`プロパティを注入優先に変更（フォールバックでHub）
- [ ] 影響の小さい派生クラスから1〜2件ずつ移行
- 完了条件: 該当派生クラスの動作が維持

#### 3-2 BaseStates / BaseSkill
- [ ] 同様の注入化
- [ ] 新規・修正メソッドは `IBattleContext` 引数で受ける
- 完了条件: 既存機能の動作が維持

#### Phase 3 最小テスト
- [ ] 対象派生クラスの機能が動作
- [ ] パッシブ/スキル発動

---

### Phase 4: GameContextHub / UIStateHub（保留）
- 現状維持
- 歩行システム全体の改修時に再検討

---

## 7. リスクと対策
| リスク | 影響 | 対策 |
|-------|------|------|
| 初期化タイミングずれ | NullReference | フォールバックでHub参照を残す |
| 継承クラスへの影響 | 大量修正 | 基底クラスは最後に対応 |
| テスト不足 | リグレッション | Phase毎に最小テストを実施 |

---

## 8. 完了条件（DoD）
- [x] Phase 1完了：BattleContextHub使用箇所 8→5（全てフォールバック形式）
- [x] Phase 2完了：BattleOrchestratorHub使用箇所 9→6（4フォールバック + 2 Assert）
- [x] 新規コードでHub直接参照禁止のルールを運用（Bind/Initialize経由で注入）
- [x] 主要クラスのユニットテスト追加（11件追加）

---

## 9. 変更履歴
| 日付 | 内容 |
|------|------|
| 2026-02-02 | 初版作成 |
| 2026-02-02 | Phase 1の目的を「直参照の局所削除」に明確化、Slaim.csを1行置換に修正、Effect系の対応方針を修正、Phase 3を「本丸」として明記 |
| 2026-02-02 | Phase 1〜3 実装完了、ユニットテスト11件追加 |

---

## 10. 実施記録

### Phase 1 実施結果
- Slaim.cs: `BattleContextHub.Current` → `manager`（基底プロパティ使用）
- PartyMemberEffect.cs / PartySetEffect.cs: `IsInBattle` プロパティ使用に変更
- BattleAIBrain.cs: メソッド引数でIBattleContext受け取り + フォールバック
- BattleContextHub.cs: `IsInBattle` プロパティ追加

### Phase 2 実施結果
- SelectRangeButtons.cs: `Initialize(BattleOrchestrator)` + フォールバック
- SelectTargetButtons.cs: `Initialize(BattleOrchestrator)` + フォールバック
- AllyClass.cs: `InitializeOrchestrator(BattleOrchestrator)` + フォールバック
- BattleUIBridge.cs: `BindOrchestrator(BattleOrchestrator)` + フォールバック

### Phase 3 実施結果
- BaseStates.cs: `BindBattleContext(IBattleContext)` + フォールバック
- BasePassive.cs: `BindBattleContext(IBattleContext)` + フォールバック
- BaseSkill.Core.cs: `BindBattleContext(IBattleContext)` + フォールバック

### 追加テスト
- BattleContextHubTests.cs（4テスト）: **全件OK**
- GlobalHubInjectionTests.cs（7テスト）: **全件OK**
- **合計11テスト全件パス（2026-02-02確認）**
