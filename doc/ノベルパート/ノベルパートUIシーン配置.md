# ノベルパートUIシーン配置

## 概要

ノベルパートUIをシーンに配置するためのガイド。
リアクションシステムのテスト（Phase R6）を行うための前提条件。

## 関連ドキュメント

- [ノベルパート設計.md](./ノベルパート設計.md) - 設計本体
- [リアクションシステム実装計画.md](./リアクションシステム実装計画.md) - Phase R6: 結合テストの前提

---

## 進捗状況

### 作成済みSO

| ファイル | 状態 | 備考 |
|----------|------|------|
| `Assets/ScriptableObject/ReactionSystemTest/TestEncounter_Reaction.asset` | ✅ 作成済み | テスト用エンカウンター |
| `Assets/ScriptableObject/ReactionSystemTest/TestDialogue_Reaction.asset` | ✅ 作成済み | テスト用ダイアログ（リアクション付き） |

### SOの設定状況

**TestEncounter_Reaction.asset:**
- id: `test_reaction_encounter`
- uiLabel: `リアクションテスト敵`
- enemyCount: 1
- escapeRate: 100
- enemyList: ⬜ 未設定（Inspectorで設定必要）

**TestDialogue_Reaction.asset:**
- dialogueId: `test_reaction_dialogue`
- defaultMode: Dinoid
- steps: ⬜ 未設定（Inspectorで以下を設定必要）

```
steps[0]:
  speaker: "テスト"
  text: "あのオレンジ色の敵を倒せ"
  reactions[0]:
    text: "オレンジ色の敵"
    startIndex: 2
    color: #FF8000
    type: Battle
    encounter: TestEncounter_Reaction
```

---

## 必要なGameObject構成

### 最小構成（テスト用）

```
EyeArea
└── NovelPartUI_Test              ← NovelPartEventUI
    └── TextBoxes                 ← TextBoxPresenter
        └── DinoidTextBox         ← CanvasGroup
            ├── Icon              ← Image（任意）
            └── Text              ← TMP_Text + ReactionTextHandler
```

### フル構成

```
NovelPartUI                        ← NovelPartEventUI コンポーネント
├── Background                     ← BackgroundPresenter
│   └── BackgroundImage            ← Image
│
├── Portraits                      ← PortraitPresenter
│   ├── LeftPortrait               ← Image
│   └── RightPortrait              ← Image
│
├── NoiseContainer                 ← NoisePresenter
│   └── (動的生成される雑音テキスト)
│
├── TextBoxes                      ← TextBoxPresenter
│   ├── DinoidTextBox              ← CanvasGroup
│   │   ├── Icon                   ← Image
│   │   └── Text                   ← TMP_Text + ReactionTextHandler
│   │
│   └── PortraitTextBox            ← CanvasGroup
│       ├── SpeakerName            ← TMP_Text
│       └── Text                   ← TMP_Text + ReactionTextHandler
│
├── BacklogPanel                   ← バックログUI
└── BackButton                     ← 戻るボタン
```

### 必要なコンポーネント

| コンポーネント | 役割 | 配置先 |
|---------------|------|--------|
| NovelPartEventUI | 全体統合 | NovelPartUI |
| BackgroundPresenter | 背景表示 | Background |
| PortraitPresenter | 立ち絵表示 | Portraits |
| NoisePresenter | 雑音表示 | NoiseContainer |
| TextBoxPresenter | テキストボックス | TextBoxes |
| ReactionTextHandler | リアクションクリック検出 | 各Text |

---

## シーン配置チェックリスト

### GameObject配置

- [ ] NovelPartUIをEyeArea以下に作成
- [ ] TextBoxPresenterをアタッチ
- [ ] DinoidTextBoxにTMP_Textを配置
- [ ] ReactionTextHandlerをアタッチ
- [ ] NovelPartEventUIのSerializeFieldを設定

### WalkingSystemManagerへの接続

- [ ] WalkingSystemManager.EnsureDialogueRunner()でNovelPartEventUIを参照
- [ ] GameContext.DialogueRunnerにNovelPartDialogueRunnerを設定

### テスト発火設定

- [ ] NodeSOのForcedEventTriggersにテストトリガーを追加
- [ ] Dialogue参照にTestDialogue_Reactionを設定

---

## 配置先の検討

現在のシーン構造を考慮した配置先は別途検討中。
詳細は [UI構造の問題点.md](../UI/UI構造の問題点.md) を参照。

---

## 備考

- このドキュメントはテスト準備の進捗管理用
- UI構造の根本的な問題については別ドキュメントで扱う
