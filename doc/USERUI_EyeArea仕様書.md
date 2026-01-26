# USERUI・EyeArea 仕様書

## 概要

本プロジェクトのUIは大きく2つの領域に分かれる：
- **USERUI**: 下半分の操作UI領域（DynamicCanvas配下）
- **EyeArea**: 上半分の視覚表示領域（AlwaysCanvas配下）

それぞれ異なる状態管理方式を持つ。

---

## 設計原則

### 1. 全てのイベントはUSERUI・EyeAreaを通じて表現する

ゲーム中の全てのイベント（歩行、バトル、ノベルパート、将来の推理・取引パート等）は、
**必ずUSERUI（操作UI）とEyeArea（視覚要素）を使用して表現する**。

- 新しいイベント種別を追加する際も、この2つの領域を通じてUIを構築する
- 独自のCanvas/UIシステムを作らず、既存の構造に統合する

### 2. Stateは大枠的な切替のみ、具体的な操作は各イベントロジック内で行う

**TabState / EyeAreaState の役割:**
- トランジションや複雑なUI由来の変化を必要としない、大枠的な状態の切替
- 例: Walk → Battle、Walk → Novel といったモード変更

**各イベントロジック内で行うこと:**
- ズーム演出（バトル開始時、ノベル開始時など）
- 立ち絵・背景のトランジション
- モード内での細かい表示切替（ディノイド→立ち絵など）
- その他の演出・アニメーション

```csharp
// 例: BattleInitializer での実装パターン

// 1. 大枠的なstate切替（EyeAreaState）
UIStateHub.EyeState.Value = EyeAreaState.Battle;

// 2. 具体的な操作は各ロジック内で行う
using (UIBlocker.Instance?.Acquire(BlockScope.AllContents))
{
    await _watchUIUpdate.FirstImpressionZoomImproved(); // ズーム演出
}
```

**この分離の意図:**
- Stateはあくまで「どのContentをactiveにするか」だけを決める
- ズームやトランジションのタイミング・演出は、BattleManager/NovelPartRunner等の各システムが制御する
- これにより、同じstate内でも多様な演出が可能になる

---

## USERUI

### 3タブ構造

USERUIは**3つのタブ**で構成される。全ての操作UIはこの3タブのいずれかに属する。

| タブ | 内容 | 切替方式 |
|------|------|---------|
| **PlayerContent** | ゲームプレイ用UI（歩行、バトル、ノベル等） | TabStateで内部切替 |
| **ConfigContent** | 設定UI | タブボタンで切替 |
| **CharaConfigContent** | キャラクター設定UI | タブボタンで切替 |

**重要:** 新しいUI要素を追加する際は、必ずこの3タブのいずれかに配置する。

### 構造

```
DynamicCanvas
└── USERUI
    └── ToggleButtons
        ├── PlayerContent ← MainContent.cs（TabStateで切替）
        │   ├── WalkObject
        │   ├── SkillObject
        │   ├── SelectTargetObject
        │   ├── FieldDialogueObject
        │   ├── EventDialogueObject
        │   └── NovelChoiceObject
        ├── ConfigContent
        ├── CharaConfigContent
        └── UIBlocker ← タブ単位のブロック制御
```

### UIBlocker: タブ単位の操作ブロック

USERUIには**UIBlocker**による操作ブロック機構がある。
特定のタブだけをブロックしたり、全タブをブロックしたりできる。

```csharp
public enum BlockScope
{
    MainContent,        // PlayerContentのみブロック
    ConfigContent,      // Configタブのみブロック
    CharaConfigContent, // CharaConfigタブのみブロック
    AllContents,        // 3タブ全てブロック
}
```

**使用例:**

```csharp
// 短時間ブロック（アニメーション中）
using (UIBlocker.Instance.Acquire(BlockScope.AllContents))
{
    await PlayTransitionAsync();
}

// 長時間ブロック（ノベルパート全体など）
UIBlocker.Instance.BeginBlock("NovelPart", BlockScope.AllContents);
try { await RunNovelPartAsync(); }
finally { UIBlocker.Instance.EndBlock("NovelPart"); }
```

**特徴:**
- 参照カウント方式でネストしたブロック要求を安全に管理
- タブ切り替え自体は常に可能（ブロックするのは各タブ内のコンテンツ操作のみ）
- R3 ReactivePropertyで状態変更を購読可能

**詳細:** [UIBlocker設計.md](./終了済み/UIBlocker設計.md)

### 状態管理: TabState

USERUIは**TabStateによる排他的切替**を行う。

```csharp
public enum TabState
{
    // 歩行
    walk,

    // バトル
    TalkWindow, NextWait, Skill, SelectTarget, SelectRange,

    // ノベルパート
    FieldDialogue, EventDialogue, NovelChoice,
}
```

**特徴:**
- 1つのTabStateにつき、1つのGameObjectのみactive
- 完全に排他的（同時に複数activeにならない）
- `UIStateHub.UserState` で購読可能

---

## EyeArea

### 前提: ズーム階層構造

EyeAreaには**演出ズーム**のための階層構造がある。
これは全ての状態管理の前提となる。

```
EyeArea
└── ViewportArea
    ├── ZoomBackContainer    ← ズームされる（背景層）
    ├── MiddleFixedContainer ← ズームされない（固定層）
    ├── ZoomFrontContainer   ← ズームされる（前景層）
    └── FrontFixedContainer  ← ズームされない（最前面固定層）
```

| 階層 | ズーム | 用途 |
|------|--------|------|
| ZoomBackContainer | される | 背景、サイドオブジェクト |
| MiddleFixedContainer | されない | ActionMark、ノベルUI |
| ZoomFrontContainer | される | 敵UI、エフェクト |
| FrontFixedContainer | されない | 最前面に固定したいUI |

**参考:** [ズーム仕様書.md](./ズーム仕様書.md)

### 状態管理: EyeAreaState（設計中）

```csharp
public enum EyeAreaState
{
    Walk,    // 歩行中
    Novel,   // ノベルパート
    Battle,  // バトル中
}
```

### 設計上の課題

#### 課題1: 4つの階層全てに状態を適用するか？

**案A: 全階層に適用**
```
ZoomBackContainer
├── WalkContent
├── NovelContent
└── BattleContent

MiddleFixedContainer
├── WalkContent
├── NovelContent
└── BattleContent

ZoomFrontContainer
├── WalkContent
├── NovelContent
└── BattleContent

FrontFixedContainer
├── WalkContent
├── NovelContent
└── BattleContent
```

**メリット:**
- ズームとEyeAreaStateの組み合わせに完全対応
- 各階層で独立した状態管理が可能

**デメリット:**
- 構造が複雑になる
- GameObjectが増える

**案B: 必要な階層のみ適用**
- 実際に切替が必要な階層のみContentを分ける
- 例: MiddleFixedContainerのみNovelContent/BattleContentを分ける

---

#### 課題2: Walkは共存可能にすべきか？

**現状の観察:**
- バトル開始時、歩行時の背景はそのまま残る
- ノベルパート開始時も、歩行時の背景は引き継がれる
- つまり「Walk要素」は他のstateと共存している

**選択肢:**

**A) 完全排他（USERUIと同じ）**
```
Walk active   → Novel/Battle inactive
Novel active  → Walk/Battle inactive
Battle active → Walk/Novel inactive
```
- シンプルだが、背景の引き継ぎ等で問題が生じる

**B) レイヤー方式（Walk常時表示）**
```
Walk: 常にactive（基盤層）
Novel: Walk の上に重ねる（必要時のみactive）
Battle: Walk の上に重ねる（必要時のみactive）
```
- 背景の引き継ぎが自然
- ただしNovel/Battle間は排他？

**C) ビットフラグ方式**
```csharp
[Flags]
public enum EyeAreaState
{
    None   = 0,
    Walk   = 1 << 0,  // 0001
    Novel  = 1 << 1,  // 0010
    Battle = 1 << 2,  // 0100
}

// Walk + Novel 同時active
currentState = EyeAreaState.Walk | EyeAreaState.Novel;
```
- 柔軟だが複雑

---

#### 課題3: そもそもstate管理が適切か？

**USERUIでstate管理が適切な理由:**
- 操作UIは完全に排他的（タップ領域 or 左右ボタン or 選択肢）
- 同時に複数の操作UIがactiveになることはない

**EyeAreaでstate管理が難しい理由:**
- 視覚要素は重ね合わせが多い
- 背景（Walk）の上にノベルUIを重ねる等
- 階層ごとに状態が異なる可能性

**代替案: Presenterパターン**
- stateではなく、各Presenterが自身の表示/非表示を制御
- 例: `PortraitPresenter.Show()` / `PortraitPresenter.Hide()`
- 柔軟だが、一貫した状態管理が難しい

---

## 決定事項

| 項目 | 決定 | 備考 |
|------|------|------|
| 4階層全てにContent配置 | **A) 全て** | 各階層にWalk/Novel/BattleContent |
| Walkの扱い | **B) レイヤー方式** | Walk常時表示 + Novel/Battle排他 |
| state管理の方式 | **EyeAreaState enum** | TabStateと連動 |
| GameObject構造 | **階層軸（案A）** | ズーム階層を親とする構造 |

### 階層軸採用の理由

ズーム処理が物理的にレイヤー軸（ZoomBackContainer/ZoomFrontContainer）で動作するため、
既存実装との整合性から階層軸を採用。詳細は [EyeAreaState設計_階層軸vsState軸.md](./終了済み/EyeAreaState設計_階層軸vsState軸.md) 参照。

### 採用した構造

```
各ズーム階層（ZoomBack, MiddleFixed, ZoomFront, FrontFixed）
├── WalkContent   ← 常にactive（基盤層）
├── NovelContent  ← Novel時のみactive
└── BattleContent ← Battle時のみactive
```

### 動作

| EyeAreaState | WalkContent | NovelContent | BattleContent |
|--------------|-------------|--------------|---------------|
| Walk | active | inactive | inactive |
| Novel | active | active | inactive |
| Battle | active | inactive | active |

---

## 関連ドキュメント

- [ズーム仕様書.md](./ズーム仕様書.md) - ズーム階層の詳細
- [ノベルパート_USERUIとEyeArea連携.md](./ノベルパート/ノベルパート_USERUIとEyeArea連携.md) - ノベルパート実装時の設計
- [UIBlocker設計.md](./終了済み/UIBlocker設計.md) - USERUI操作ブロック機構の詳細
- [ゼロトタイプ歩行システム設計書.md](./歩行システム設計/ゼロトタイプ歩行システム設計書.md) - 歩行システムとイベント実行基盤

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-25 | 初版作成。USERUI/EyeAreaの構造と課題を整理 |
| 2026-01-25 | 決定: 4階層全てにContent配置、レイヤー方式（Walk常時+Novel/Battle排他） |
| 2026-01-25 | 決定: 階層軸（案A）採用。レビュー完了 |
| 2026-01-27 | 設計原則追加: 全イベントでUSERUI/EyeArea使用、Stateは大枠のみ |
| 2026-01-27 | USERUI: 3タブ構造の説明追加、UIBlockerセクション追加 |
