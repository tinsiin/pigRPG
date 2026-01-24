# ノベルパート USERUIとEyeArea連携

**状態:** 設計中

## 概要

ノベルパートUIを実装する際に、既存のUSERUI/EyeAreaの仕組みとどう連携するかの設計。

## 関連ドキュメント

- [ノベルパート設計.md](./ノベルパート設計.md)
- [ノベルパートUIシーン配置.md](./ノベルパートUIシーン配置.md)
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
├── 方針A（TabState拡張）      ← 必須の土台
├── 方針B（EyeArea側状態管理）  ← 必要なら追加
└── UIStateManager             ← C独自の統合層
```

| 方針 | 内容 | 位置づけ |
|------|------|----------|
| A | TabStateにノベル用値を追加 | Cの土台（必須） |
| B | EyeAreaにReactiveProperty追加 | Cの構成要素（必要時） |
| C | UIStateManagerで一括制御 | 最終ゴール |

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
  └── TabStateにNovel系を追加
  └── 既存Observerでノベル用UIを表示/非表示

Step 2: パターンを発見（複数システム実装後）
  └── 「USERUI変更 + EyeAreaズーム + 立ち絵表示」が毎回セットだと気づく
  └── 方針Bの必要性を判断

Step 3: 方針C実装（パターンが固まったら）
  └── UIStateManager.SetState(GameUIState.Novel) で一括化
  └── 新システム追加が1行で済むようになる
```

---

## 課題一覧

### 課題1: TabStateの拡張

- 現状のTabStateはバトル用のみ
- ノベルパート用のTabState値が未定義

```csharp
// 追加が必要な値
public enum TabState
{
    // 既存
    walk, TalkWindow, NextWait, Skill, SelectTarget, SelectRange,

    // ノベルパート用（追加）
    FieldDialogue,  // フィールド会話（ディノイドモード）
    NovelPortrait,  // ノベル（立ち絵モード）
    NovelChoice,    // ノベル（選択肢表示中）
}
```

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
// ノベル開始
UIStateHub.UserState.Value = TabState.NovelPortrait;
await EyeAreaManager.Instance.PlayZoomAsync();

// ノベル終了
await EyeAreaManager.Instance.RestoreZoomAsync(true, 0.4f);
UIStateHub.UserState.Value = TabState.walk;
```

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

### モード別の要素分離（検討案）

| モード | USERUI | EyeArea |
|--------|--------|---------|
| 歩行中 | PlayerContent表示 | ActionMark表示、Charas表示 |
| フィールド会話 | テキストボックス表示 | ActionMark非表示、ディノイド表示 |
| イベント会話（立ち絵） | 選択肢ボタン表示 | 立ち絵表示（MiddleFixedContainer） |
| バトル中 | Skill/Target系表示 | EnemyArea表示 |

### 重要: ズーム制御とGameObject切り替えの分離

```
【ズーム演出】
  → BattleManager等のロジック側がPlayZoomAsync()を呼ぶタイミングで制御
  → EyeAreaContentの責務ではない

【GameObject表示切替】
  → 純粋に「どのGameObjectが見えるか」だけ
  → USERUIのMainContent.SwitchContent()と同じ役割
  → TabStateに応じてSetActive(true/false)するだけ
```

**目的:**
- Unityエディタでシーンを見たときに「このGameObjectはどのシステムで使われるか」が一目で分かる
- ロジックと表示の責務分離

### EyeArea内のシーン構造（提案）

各ZoomContainer内に、USERUIのPlayerContent/ConfigContentと同様に「システム別のコンテナ」を配置：

```
EyeArea
└── ViewportArea
    ├── ZoomBackContainer
    │   ├── WalkBackContent      ← 歩行時の背景
    │   └── NovelBackContent     ← ノベル時の背景（必要なら）
    │
    ├── MiddleFixedContainer
    │   ├── WalkFixedContent     ← ActionMark等
    │   └── NovelFixedContent    ← 立ち絵、テキストボックス
    │
    └── ZoomFrontContainer
        ├── WalkFrontContent     ← 歩行時の前景
        └── BattleFrontContent   ← EnemyArea
```

### EyeAreaContent.cs（提案）

```csharp
// MainContentと同様のパターン
public class EyeAreaContent : MonoBehaviour
{
    [Header("ZoomBackContainer内")]
    [SerializeField] GameObject WalkBackContent;
    [SerializeField] GameObject NovelBackContent;

    [Header("MiddleFixedContainer内")]
    [SerializeField] GameObject WalkFixedContent;
    [SerializeField] GameObject NovelFixedContent;

    [Header("ZoomFrontContainer内")]
    [SerializeField] GameObject WalkFrontContent;
    [SerializeField] GameObject BattleFrontContent;

    public void SwitchContent(TabState state)
    {
        // 視覚的な表示切替のみ（ズーム制御はしない）
        switch (state)
        {
            case TabState.walk:
                SetWalkMode();
                break;
            case TabState.NovelPortrait:
                SetNovelMode();
                break;
            // バトル系はBattleManager側で制御するため、ここでは触らない場合も
        }
    }

    void SetWalkMode()
    {
        WalkBackContent?.SetActive(true);
        NovelBackContent?.SetActive(false);
        WalkFixedContent?.SetActive(true);
        NovelFixedContent?.SetActive(false);
        // ...
    }

    void SetNovelMode()
    {
        WalkBackContent?.SetActive(false);
        NovelBackContent?.SetActive(true);
        WalkFixedContent?.SetActive(false);
        NovelFixedContent?.SetActive(true);
        // ...
    }
}
```

**メリット:**
- USERUIと同じパターンで理解しやすい
- TabStateの購読で自動的に切り替わる
- 方針Aの実装で十分対応可能
- シーン構造を見るだけで「何がどのモードで使われるか」が分かる

**注意点:**
- EyeAreaはWatchUIUpdateが管理しているため、既存ロジックとの整合性確認が必要
- バトル系の表示切替はBattleManager側の責務と重複しないよう調整

---

## 既存の使える仕組み

### USERUI側

```csharp
// 状態変更
UIStateHub.UserState.Value = TabState.NovelPortrait;

// 状態監視（既存のSubscribe）
USERUI_state.Subscribe(state => {
    // TabContents.SwitchContent(state) が自動で呼ばれる
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
    Novel,
    Inference,  // 推理パート
    Trade,      // 取引パート
    // ...
}

public class UIStateManager
{
    public void SetState(GameUIState state)
    {
        switch (state)
        {
            case GameUIState.Novel:
                USERUI_state.Value = TabState.NovelPortrait;
                // EyeArea側の状態変更（方針B実装時）
                break;
        }
    }
}
```

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
