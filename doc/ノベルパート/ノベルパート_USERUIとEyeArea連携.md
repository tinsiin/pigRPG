# ノベルパート USERUIとEyeArea連携

**状態:** Step 1完了 + 方針B完了（USERUI側・EyeArea側・Runner統合・Presenter配置・UI要素配置・参照アサイン・EyeAreaState切替システム全て完了）

## 概要

ノベルパートUIを実装する際に、既存のUSERUI/EyeAreaの仕組みとどう連携するかの設計。

## 関連ドキュメント

- [ノベルパート設計.md](./ノベルパート設計.md)
- [ノベルパートUIシーン配置.md](./ノベルパートUIシーン配置.md)
- [ノベルパート未実装機能一覧.md](./ノベルパート未実装機能一覧.md) - UI関連の未実装項目
- [リアクションシステム実装計画.md](./リアクションシステム実装計画.md) - リアクションボタン（EyeArea側）
- [UIBlocker設計.md](../終了済み/UIBlocker設計.md) - USERUI操作ブロック機構（実装完了）
- [ゼロトタイプ歩行システム設計書.md](../歩行システム設計/ゼロトタイプ歩行システム設計書.md)
- [ズーム仕様書.md](../ズーム仕様書.md)

---

## 現状のUI構造

```
AlwaysCanvas（常時表示）
└── EyeArea（上半分 - 視覚要素）
    ├── ViewportArea
    │   ├── ZoomBackContainer   ← ズームされる（背景）
    │   ├── MiddleFixedContainer ← ズームされない（ノベルUIはここ）
    │   └── ZoomFrontContainer   ← ズームされる（前景）
    コンポーネント: WatchUIUpdate

DynamicCanvas（動的）
└── USERUI（下半分 - UI操作要素）
    ├── topLine
    └── ToggleButtons
    コンポーネント: Walking
```

### 現在の状態管理

```csharp
// Walking.cs - USERUIの状態
public ReactiveProperty<TabState> USERUI_state = new();

// TabState（現在はバトル用のみ）
public enum TabState { walk, TalkWindow, NextWait, Skill, SelectTarget, SelectRange }

// UIStateHub.cs - グローバルアクセス
public static ReactiveProperty<TabState> UserState { get; }
```

---

## 設計方針

### 3つの方針とその関係

```
方針C（統合状態管理） ← 最終ゴール
├── 方針A（TabState拡張）           ← 必須の土台（USERUI操作UI切替）
├── 方針B（EyeArea演出の共通IF化）  ← 必要なら追加
└── UIStateManager                  ← C独自の統合層
```

| 方針 | 内容 | 位置づけ |
|------|------|----------|
| A | TabStateにFieldDialogue/EventDialogue追加 | Cの土台（必須） |
| B | EyeAreaStateでWalk/Novel/Battle切替 | Cの構成要素（必要時） |
| C | UIStateManagerで一括制御 | 最終ゴール |

**方針Bの詳細:**
- EyeArea専用のstate（`EyeAreaState` enum）を導入
- Walk/Novel/Battleの3状態で親GameObjectをSetActive切替
- NovelContent内部の演出（立ち絵トランジション等）はPresenterが直接制御

### 方針Cが最終ゴールである理由

今後の拡張（推理パート、取引パート等）を考えると、統合管理が必要になる：

```
【方針A/Bのみの場合】
ノベルパート  → USERUI_state変更 + EyeArea個別呼び出し
推理パート   → USERUI_state変更 + EyeArea個別呼び出し  ← 毎回同じパターン
取引パート   → USERUI_state変更 + EyeArea個別呼び出し

【方針Cの場合】
ノベルパート  → UIStateManager.SetState(Novel)      ← 1行で済む
推理パート   → UIStateManager.SetState(Inference)
取引パート   → UIStateManager.SetState(Trade)
```

### 段階的な進め方

```
Step 1: 方針A実装（ノベルパート実装時）
  └── TabStateにFieldDialogue/EventDialogue追加
  └── USERUI側: 既存Observerで操作UI（タップ領域/左右ボタン）を切替
  └── EyeArea側: ConversationRunnerがトランジション付きで直接制御

Step 2: パターンを発見（複数システム実装後）
  └── 「USERUI変更 + EyeArea演出」の組み合わせパターンが見えてくる
  └── 方針Bの必要性を判断（演出の共通IF化）

Step 3: 方針C実装（パターンが固まったら）
  └── UIStateManager.SetState(GameUIState.FieldDialogue) で一括化
  └── 新システム追加が1行で済むようになる
```

---

## 課題一覧

### 課題1: TabStateの拡張 ✅ 実装完了

~~- 現状のTabStateはバトル用のみ~~
~~- ノベルパート用のTabState値が未定義~~

**実装済み** (`Assets/Script/Toggle/TabContents.cs`):

```csharp
public enum TabState
{
    // 既存
    walk, TalkWindow, NextWait, Skill, SelectTarget, SelectRange,

    // ノベルパート用（実装済み）
    FieldDialogue,  // フィールド会話（タップで進むのみ、戻れない）
    EventDialogue,  // イベント会話（左右ボタンで戻れる）
    NovelChoice,    // 選択肢表示中（選択肢ボタンのみ）
}
```

**追加されたGameObjectフィールド** (`TabContents.cs`):
- `FieldDialogueObject` - タップ領域のみ
- `EventDialogueObject` - 左右ボタン
- `NovelChoiceObject` - 選択肢ボタン群

**MainContent.SwitchContent()** も対応済み。PlayerContentへのアサイン完了。

**重要: USERUIだけがTabStateで切り替わる**

| TabState | USERUI側の表示 |
|----------|---------------|
| FieldDialogue | タップ領域のみ（進むだけ） |
| EventDialogue | 左右ボタン（進む/戻る） |
| NovelChoice | 選択肢ボタンのみ |

**リアクションボタンの表示条件:**
- NovelChoice以外のノベル系state（FieldDialogue/EventDialogue）で表示可能
- stateに縛られず、該当ステップにリアクションがあれば表示
- 選択肢表示中は非表示（選択に集中させるため）

EyeArea側（立ち絵、テキストボックス、背景）はTabStateで切り替え**ない**。
詳細は後述の「USERUI vs EyeAreaの責務分離」を参照。

### 課題2: ノベルUIの配置場所

| UI要素 | 配置先候補 | 備考 |
|--------|-----------|------|
| 立ち絵 | EyeArea/MiddleFixedContainer | ズームされない |
| テキストボックス | USERUI or EyeArea | 要検討 |
| 選択肢ボタン | USERUI | 既存ToggleButtonsと同階層？ |

### 課題3: ズームシステムとの連携

- `EyeAreaManager.Instance.PlayZoomAsync()` との統合
- ノベルUIがズームされない配置の確認
- 中央オブジェクトズーム時の挙動

### 課題4: 歩行→ノベル→歩行の状態遷移

```csharp
// フィールド会話開始
UIStateHub.UserState.Value = TabState.FieldDialogue;
// → USERUI側: タップ領域表示（+ リアクションボタンがあれば表示）
// → EyeArea側: ConversationRunnerがトランジション付きで立ち絵等を表示

// イベント会話に移行（フィールド会話から）
UIStateHub.UserState.Value = TabState.EventDialogue;
// → USERUI側: 左右ボタン表示に切替（+ リアクションボタンがあれば表示）
// → EyeArea側: 変化なし（同じ部品を使い続ける）

// 選択肢表示
UIStateHub.UserState.Value = TabState.NovelChoice;
// → USERUI側: 選択肢ボタンのみ表示（リアクションボタン非表示）
// → EyeArea側: 変化なし

// 選択肢選択後、会話に戻る
UIStateHub.UserState.Value = TabState.FieldDialogue; // or EventDialogue
// → USERUI側: タップ領域 or 左右ボタンに戻る

// 会話終了
UIStateHub.UserState.Value = TabState.walk;
// → USERUI側: 通常のPlayerContent表示
// → EyeArea側: ConversationRunnerがトランジション付きで立ち絵等を退場
```

**ポイント:** TabState変更はUSERUIの操作UIだけを切り替える。EyeArea側の演出は別途ConversationRunner等が制御。

### 課題5: テキストボックスの2モード対応

- ディノイドモード用テキストボックス
- 立ち絵モード用テキストボックス
- モード切り替え時の表示切替

### 課題6: MessageDropperとの連携

- フィールド会話時のログ表示
- 既存MessageDropperとの統合

---

## MCP調査結果：実際のシーン構造

### USERUIの構造（DynamicCanvas配下）

```
USERUI
└── ToggleButtons
    ├── ConfigContent (active: false)
    ├── CharaConfigContent (active: false)
    ├── PlayerContent (active: true) ← MainContent.cs
    ├── BusyOverlay
    └── ToggleButtonGroup
```

**切り替えパターン（MainContent.cs）:**

```csharp
public override void SwitchContent(TabState state)
{
    switch (state)
    {
        case TabState.walk:
            WalkObject.SetActive(true);
            TalkObject.SetActive(false);
            NextObject.SetActive(false);
            SkillObject.SetActive(false);
            SelectTargetObject.SetActive(false);
            SelectRangeObject.SetActive(false);
            break;
        // 他のcaseも同様のパターン
    }
}
```

**購読パターン（ToggleButtons.cs）:**

```csharp
userState.Subscribe(state => {
    _tabContentsChanger.GetViewFromKind(TabContentsKind.Players).SwitchContent(state);
    _tabContentsChanger.GetViewFromKind(TabContentsKind.CharactorConfig).SwitchContent(state);
}).AddTo(this);
```

### EyeAreaの構造（AlwaysCanvas配下）

```
EyeArea
└── ViewportArea
    ├── ZoomBackContainer (子: BackGround)
    ├── MiddleFixedContainer (子: ActionMark, ActionMarkSpawnPoint)
    ├── ZoomFrontContainer (子: EnemyArea)
    ├── Charas (キャラクター3体)
    ├── BSAmanager
    └── SchizoLog
```

### USERUI vs EyeAreaの責務分離

**核心的な違い:**

| 領域 | 切り替え方式 | 理由 |
|------|-------------|------|
| USERUI | TabStateでSetActive切替 | 操作方式が排他的（タップ or 左右ボタン） |
| EyeArea | トランジション付きで中身を変更 | 同じ部品が滑らかに変化する |

### なぜEyeAreaはTabStateで切り替えないのか

**EyeArea側の要素は全部同じ部品:**
- 立ち絵表示エリア、テキストボックス、背景 → 全部同じGameObject
- ディノイド→立ち絵、背景変化、表情変化 → **トランジション付きで中身（スプライト等）が変わるだけ**
- 同じ時間に存在しながらアニメーションで滑らかに切り替わる
- SetActive(true/false)でパッパ切り替えるものではない

**ノベルパート設計.mdより:**
```
ディノイド → 立ち絵:
1. ディノイド用テキストボックスが閉じる（点滅/回転などの演出）
2. 立ち絵の登場トランジション
3. 立ち絵用テキストボックスが開く
```

これらは「同じGameObject」の中でトランジション付きで変化する。
WalkContent/NovelContentのように分けると、このトランジションが実現できない。

### USERUIだけが排他的な理由

**唯一排他的なのは「イベントか否か」:**

| 会話種類 | USERUI側 | 特徴 |
|----------|----------|------|
| フィールド会話 | タップ領域のみ | 進むだけ、戻れない |
| イベント会話 | 左右ボタン | 進む/戻るが可能 |

- イベントの間は絶対にフィールド会話に切り替わらない
- トランジションとは無関係に「イベント中かどうか」で決まる
- だからUSERUIはSetActive切替でOK

### EyeArea側の実装方針（方針B: EyeAreaState）✅ 実装完了

**EyeAreaState enumで大分類を切り替え、内部はトランジション制御:**

```csharp
// 方針B: EyeArea専用のstate（TabStateとは別）
public enum EyeAreaState
{
    Walk,    // 歩行中（ActionMark等）
    Novel,   // ノベルパート（全モード含む：ディノイド、立ち絵、背景あり/なし）
    Battle,  // バトル中（EnemyArea等）
}
```

**シーン構造（USERUI同様に親GameObjectで分ける）:**

```
EyeArea
└── ViewportArea
    ├── ZoomBackContainer
    │   └── BackGround
    │
    ├── MiddleFixedContainer
    │   ├── WalkContent        ← EyeAreaState.Walk時にactive
    │   │   └── （歩行専用要素があれば）
    │   │
    │   ├── NovelContent       ← EyeAreaState.Novel時にactive
    │   │   ├── PortraitArea（立ち絵）
    │   │   ├── TextBoxArea（テキストボックス）
    │   │   └── BackgroundArea（背景）
    │   │
    │   └── BattleContent      ← EyeAreaState.Battle時にactive
    │       └── ActionMark, ActionMarkSpawnPoint
    │
    └── ZoomFrontContainer
        └── EnemyArea
```

**注意:** リアクションボタンはUSERUI側（SelectRangeButtons等と同じパターン）

**切り替え方式:**

| 切り替え対象 | 方式 |
|-------------|------|
| WalkContent / NovelContent / BattleContent | EyeAreaStateでSetActive切替 |
| NovelContent内部（立ち絵、背景、モード） | トランジション付きで中身を変更 |

**ポイント:**
- EyeAreaStateはTabStateとは別のstate
- NovelContentの中に全てのノベルモード要素が入る（ディノイド、立ち絵、背景あり/なし全て）
- NovelContent内部の切り替えはSetActiveではなく、Presenterがトランジション付きで制御

### ズーム制御について

ズーム演出はEyeAreaの表示切替とは独立：
- BattleManager等のロジック側が`PlayZoomAsync()`を呼ぶタイミングで制御
- TabStateとは無関係
- 中央オブジェクト由来の会話でのみ発生（選択可能）

---

## 既存の使える仕組み

### USERUI側

```csharp
// 状態変更（操作UIの切替）
UIStateHub.UserState.Value = TabState.FieldDialogue;  // タップのみ
UIStateHub.UserState.Value = TabState.EventDialogue;  // 左右ボタン

// 状態監視（既存のSubscribe）
USERUI_state.Subscribe(state => {
    // TabContents.SwitchContent(state) が自動で呼ばれる
    // → FieldDialogue/EventDialogue用のUIが表示される
});
```

### EyeArea側

| 機能 | インターフェース | 使い方 |
|------|-----------------|--------|
| ズーム演出 | `IIntroOrchestratorFacade` | `EyeAreaManager.Instance.PlayZoomAsync()` |
| ビューポート参照 | `IViewportController` | `EyeAreaManager.Instance.Viewport.FixedLayer` |
| ActionMark | `IActionMarkController` | `EyeAreaManager.Instance.ActionMark.Hide()` |

---

## 将来の拡張（方針C実装時）

```csharp
public enum GameUIState
{
    Walking,
    Battle,
    FieldDialogue,   // フィールド会話
    EventDialogue,   // イベント会話
    Inference,       // 推理パート
    Trade,           // 取引パート
    // ...
}

public class UIStateManager
{
    public void SetState(GameUIState state)
    {
        switch (state)
        {
            case GameUIState.FieldDialogue:
                USERUI_state.Value = TabState.FieldDialogue;
                // EyeArea側はConversationRunnerが直接制御
                break;
            case GameUIState.EventDialogue:
                USERUI_state.Value = TabState.EventDialogue;
                break;
        }
    }
}
```

**注:** EyeArea側の演出（立ち絵、背景等）はTabStateではなく、ConversationRunner等が直接トランジション制御する。

---

## 未実装UI機能との関連

本ドキュメントの設計に基づいて実装が必要なUI要素。
詳細は [ノベルパート未実装機能一覧.md](./ノベルパート未実装機能一覧.md) を参照。

### USERUI側（TabStateで切り替え） ✅ 実装完了

| 項目 | TabState | 状態 | 備考 |
|------|----------|------|------|
| タップ進行 | FieldDialogue | ✅ 完了 | FieldDialogueUI.cs |
| 左右ボタン | EventDialogue | ✅ 完了 | EventDialogueUI.cs |
| 選択肢UI | NovelChoice | ✅ 完了 | NovelChoicePresenter.cs |
| ~~リアクションボタン~~ | - | → EyeArea側 | 文字タップ方式採用（USERUI側配置不要） |

**シーン構造:**
```
USERUI/ToggleButtons/PlayerContent
├── WalkObject          ← 既存（歩行時）
├── SkillObject         ← 既存（バトル時）
├── SelectRangeObject   ← 既存（バトル時）
├── SelectTargetObject  ← 既存（バトル時）
├── ...                 ← 既存
├── FieldDialogueObject ← FieldDialogueUI ✅
├── EventDialogueObject ← EventDialogueUI ✅
└── NovelChoiceObject   ← NovelChoicePresenter ✅

AlwaysCanvas/EyeArea
└── NovelPartEventUI    ← 入力UI統合 ✅
```

**実装済みスクリプト:**

| スクリプト | 場所 | 役割 |
|-----------|------|------|
| INovelInputProvider.cs | Assets/Script/Novel/ | 入力インターフェース |
| NovelInputHub.cs | Assets/Script/Novel/ | 入力集約ハブ |
| FieldDialogueUI.cs | Assets/Script/Novel/ | タップ→次へ |
| EventDialogueUI.cs | Assets/Script/Novel/ | 左右ボタン→戻る/次へ |
| DynamicButtonPresenterBase.cs | Assets/Script/UI/ | 動的ボタン共通基底 |
| NovelChoicePresenter.cs | Assets/Script/Novel/ | 選択肢ボタン動的生成 |

**Runner統合済み:**
- NovelPartDialogueRunner.WaitForInput() → NovelInputHub連携
- NovelPartEventUI.ShowChoices() → NovelChoicePresenter連携
- TabState自動切替（FieldDialogue/EventDialogue/NovelChoice）

**リアクションについて（決定済み）:**
- **文字タップ方式採用**: テキストボックス内の色付き文字を直接タップ
- USERUI側にボタン配置不要（EyeArea側テキストボックス内で完結）
- 詳細は [リアクションシステム実装計画.md](./リアクションシステム実装計画.md) 参照

### EyeArea側（トランジション制御、EyeAreaStateで切り替え） ✅ UI要素配置完了

| 項目 | 配置場所 | 状態 | 備考 |
|------|---------|------|------|
| NovelPartEventUI | AlwaysCanvas/EyeArea | ✅ 完了 | 入力UI・Presenter参照アサイン完了 |
| NovelContent | AlwaysCanvas/EyeArea | ✅ 完了 | Presenter親オブジェクト |
| PortraitPresenter | NovelContent/PortraitArea | ✅ 完了 | leftImage/rightImage/Transform全てアサイン |
| TextBoxPresenter | NovelContent/TextBoxArea | ✅ 完了 | 全8フィールドアサイン完了 |
| BackgroundPresenter | NovelContent/BackgroundArea | ✅ 完了 | backgroundImage/Transform全てアサイン |
| NoisePresenter | NovelContent/NoiseArea | ✅ 完了 | noiseContainerアサイン完了 |
| ReactionTextHandler | TextBoxArea配下のText | ⬜ 未配置 | 文字タップ方式（Phase R5で配置） |
| PortraitDatabase | Assets/Data/Novel/ | ✅ 作成済み | NovelPartEventUIにアサイン完了 |
| BackgroundDatabase | Assets/Data/Novel/ | ✅ 作成済み | NovelPartEventUIにアサイン完了 |

**シーン構造:**
```
AlwaysCanvas/EyeArea
├── NovelPartEventUI     ← 入力UI・Presenter統合
└── NovelContent         ← Presenter親オブジェクト
    ├── BackgroundArea   ← BackgroundPresenter + BackgroundImage
    ├── PortraitArea     ← PortraitPresenter + LeftPortrait/RightPortrait
    ├── TextBoxArea      ← TextBoxPresenter + DinoidTextBox/PortraitTextBox
    └── NoiseArea        ← NoisePresenter + NoiseContainer
```

**不要と判断した項目:**
- backlogPanel - 不要（イベント会話は左右ボタンで戻れる、フィールド会話はMessageDropperでログが流れる）
- backButton - 不要（同上）

**次のステップ:**
1. ~~データベースエントリ登録~~ ✅ 完了
2. 動作テスト（テストシナリオで立ち絵・背景表示確認）
3. ReactionTextHandlerシーン配置（文字タップ方式）

---

## 備考

- 方針Aから始めて、段階的にCへ進化させる
- 過剰設計を避け、必要になったら拡張する
- 詳細は各課題を検討時に追記

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-24 | 初版作成（課題一覧） |
| 2026-01-24 | 方針A/B/Cの関係と段階的進め方を追記 |
| 2026-01-24 | MCP調査結果追記（USERUI/EyeAreaシーン構造、モード別分離案） |
| 2026-01-24 | ズーム制御とGameObject切替の分離を明確化、ZoomContainer内構造案追記 |
| 2026-01-24 | 設計修正: EyeAreaはTabStateで切り替えない、トランジション付きで中身を変更する方式に変更 |
| 2026-01-24 | 未実装UI機能との関連セクション追加、リアクションシステム実装計画との連携 |
| 2026-01-24 | 方針B詳細追加: EyeAreaState（Walk/Novel/Battle）で親GameObjectをSetActive切替 |
| 2026-01-24 | TabStateにNovelChoice追加、リアクションボタンの表示条件明確化 |
| 2026-01-24 | UIBlocker設計ドキュメントへのリンク追加 |
| 2026-01-24 | **Step 1実装完了**: TabState拡張（FieldDialogue/EventDialogue/NovelChoice）、MainContent対応、PlayerContentアサイン |
| 2026-01-24 | USERUI側UI部品の実装計画更新: FieldDialogue/EventDialogueシーン配置済み、リアクションボタン保留 |
| 2026-01-25 | **USERUI側スクリプト実装完了**: INovelInputProvider, NovelInputHub, FieldDialogueUI, EventDialogueUI, DynamicButtonPresenterBase, NovelChoicePresenter |
| 2026-01-25 | **Runner統合完了**: INovelEventUI拡張（InputProvider, SetTabState）、NovelPartEventUI更新、NovelPartDialogueRunner入力待ち統合 |
| 2026-01-25 | NovelPartEventUIシーン配置・入力UIアサイン完了、ドキュメント進捗更新 |
| 2026-01-25 | **EyeArea側Presenter配置完了**: NovelContent作成、PortraitArea/TextBoxArea/BackgroundArea/NoiseArea配置・Presenterアタッチ |
| 2026-01-25 | **データベースアサイン完了**: PortraitDatabase・BackgroundDatabase作成・NovelPartEventUIにアサイン |
| 2026-01-25 | **Step 1完全完了**: USERUI側・EyeArea側・Runner統合・Presenter配置・データベースアサイン全て完了 |
| 2026-01-25 | **Presenter UI要素配置完了**: 各Presenter内のImage/Text/CanvasGroup配置・参照アサイン完了、次ステップ更新 |
| 2026-01-25 | **設計決定**: バックログUI不要（イベント会話は左右ボタンで戻れる、フィールド会話はMessageDropperで流れる）、リアクション方式は文字タップ方式採用 |
| 2026-01-25 | **方針B完了**: EyeAreaState切替システム（EyeAreaState.cs, EyeAreaContents.cs, EyeAreaMainContent.cs, EyeAreaToggle.cs）実装・シーン配置・アタッチ完了 |
