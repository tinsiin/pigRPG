# EyeAreaState設計: 階層軸 vs State軸

## 概要

EyeArea内のGameObject構造について、2つの設計アプローチがある。
どちらを採用すべきか判断を求める。

---

## 背景

### EyeAreaの構造

EyeAreaには**演出ズーム**のための4つの階層がある：

```
ViewportArea
├── ZoomBackContainer    ← ズームされる（背景層）
├── MiddleFixedContainer ← ズームされない（固定層）
├── ZoomFrontContainer   ← ズームされる（前景層）
└── FrontFixedContainer  ← ズームされない（最前面固定層）
```

ズーム処理は`ZoomBackContainer`と`ZoomFrontContainer`をコンテナ単位でスケール変換する。
コンテナの中にある子要素は親と一緒にズームされる（Unityの標準動作）。

**参考:** [ズーム仕様書.md](./ズーム仕様書.md)

### EyeAreaState

EyeAreaの表示状態を管理するenum：

```csharp
public enum EyeAreaState
{
    Walk,    // 歩行中
    Novel,   // ノベルパート
    Battle,  // バトル中
}
```

各Stateに応じて、表示するGameObjectを切り替える。
レイヤー方式を採用：Walkは常時表示、Novel/Battleは上に重ねる（排他）。

**参考:** [USERUI_EyeArea仕様書.md](./USERUI_EyeArea仕様書.md)

---

## 前提条件

| 項目 | 増えやすさ | 理由 |
|------|-----------|------|
| ズーム階層 | **低い（ほぼ固定）** | 4階層で完結。構造的に増やす理由がない |
| EyeAreaState | **高い** | 推理パート、取引パート、ミニゲーム等で追加される可能性 |

**結論: EyeAreaStateの方が圧倒的に増えやすい。**

---

## 2つの設計アプローチ

### 案A: 階層軸（ズーム階層を親とする）

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

**スクリプト構造:**
```csharp
// EyeAreaToggle.cs
[SerializeField] private EyeAreaContents zoomBackContent;      // 4フィールド
[SerializeField] private EyeAreaContents middleFixedContent;
[SerializeField] private EyeAreaContents zoomFrontContent;
[SerializeField] private EyeAreaContents frontFixedContent;

// EyeAreaMainContent.cs（各階層にアタッチ）
[SerializeField] protected GameObject walkContent;    // 3フィールド
[SerializeField] protected GameObject novelContent;
[SerializeField] protected GameObject battleContent;
```

**EyeAreaState追加時の作業:**
1. 4つの階層それぞれに新しいContentを追加
2. EyeAreaMainContent.csに新しいフィールド追加
3. SwitchContent()にcase追加

---

### 案B: State軸（EyeAreaStateを親とする）

```
ZoomBackContainer
├── WalkContent.ZoomBackPart
├── NovelContent.ZoomBackPart
└── BattleContent.ZoomBackPart

MiddleFixedContainer
├── WalkContent.MiddleFixedPart
├── NovelContent.MiddleFixedPart
└── BattleContent.MiddleFixedPart

ZoomFrontContainer
├── WalkContent.ZoomFrontPart
├── NovelContent.ZoomFrontPart
└── BattleContent.ZoomFrontPart

FrontFixedContainer
├── WalkContent.FrontFixedPart
├── NovelContent.FrontFixedPart
└── BattleContent.FrontFixedPart
```

**スクリプト構造:**
```csharp
// EyeAreaToggle.cs
[SerializeField] private EyeAreaStateContent walkContent;    // 3フィールド
[SerializeField] private EyeAreaStateContent novelContent;
[SerializeField] private EyeAreaStateContent battleContent;

// EyeAreaStateContent.cs（各Stateごとに1つ）
[SerializeField] private GameObject zoomBackPart;      // 4フィールド
[SerializeField] private GameObject middleFixedPart;
[SerializeField] private GameObject zoomFrontPart;
[SerializeField] private GameObject frontFixedPart;
```

**EyeAreaState追加時の作業:**
1. EyeAreaToggle.csに新しいフィールド追加
2. 新しいEyeAreaStateContent用のGameObjectを4階層に配置
3. SwitchContent()にcase追加

---

## 比較表

| 観点 | 案A（階層軸） | 案B（State軸） |
|------|-------------|---------------|
| **シーン構造** | 各階層の下にState別Content | 各階層にState別Partが散らばる |
| **管理単位** | 階層ごとにまとまる | Stateごとにまとまる |
| **GameObject数** | 4階層 × 3State = 12個 | 3State × 4階層 = 12個（同じ） |
| **EyeAreaState追加時** | 4箇所に追加 | 1コントローラー + 4箇所に追加 |
| **ズーム階層追加時** | 1箇所に追加 | 3箇所に追加 |
| **直感的な管理** | 「この階層に何がある？」 | 「このStateに何がある？」 |

---

## 考慮すべき点

### 1. どちらが増えやすいか（前提条件より）
- **EyeAreaStateの方が増えやすい** → 案Bが有利？

### 2. 実際の作業フロー
- 新しいゲームシステム追加時、「このStateで何を表示するか」を考える
- → State単位でまとまっている方が分かりやすい？

### 3. ズーム処理との整合性
- どちらでもズーム処理は正しく動作する（コンテナの中に配置すればOK）
- 技術的な制約はない

### 4. 既存コードとの一貫性
- USERUI側はTabState（State軸）で管理している
- EyeArea側もState軸にすると一貫性がある？

---

## 現状の実装

現在、**案A（階層軸）** で実装途中：
- `EyeAreaToggle.cs` - 4つの階層フィールドを持つ
- `EyeAreaMainContent.cs` - 3つのStateフィールドを持つ

**参考ファイル:**
- `Assets/Script/Toggle/EyeAreaToggle.cs`
- `Assets/Script/Toggle/EyeAreaMainContent.cs`
- `Assets/Script/Toggle/EyeAreaContents.cs`
- `Assets/Script/Toggle/EyeAreaState.cs`

---

## 結論: 案A（階層軸）を採用

### 決定理由

1. **ズーム処理は物理的にレイヤー軸で動作**
   - ZoomBackContainer/ZoomFrontContainerを直接スケール変換する実装
   - 「レイヤーに紐づく配置」が前提となっている

2. **既存実装が階層軸で揃っている**
   - EyeAreaToggle.csは4階層にEyeAreaContentsを割り当てる前提
   - EyeAreaMainContent.csは「Walk常時＋Novel/Battle排他」をレイヤー単位で処理
   - 変更コストに見合う利点が少ない

3. **作業量は大差ない**
   - どちらの案でも各レイヤーに部品を用意する必要がある
   - State軸にしても4レイヤー分の参照を持つ
   - 「Walk常時表示」の制御がState軸だと複雑になる

4. **ノベルパートの設計との整合性**
   - ノベルパート内部はトランジション制御に任せる（EyeAreaStateを細かく分けない）
   - 必要なレイヤーだけState切替を適用すれば良い
   - 階層軸ならレイヤー別にEyeAreaContentsを差し替える/空にするだけで対応可能

### 補足: ドキュメント間の差分について

- USERUI_EyeArea仕様書.md: 「4階層すべてにWalk/Novel/Battleを置く」
- ノベルパート_USERUIとEyeArea連携.md: 「MiddleFixedのみState切替、ZoomBack/ZoomFrontは固定」

この差分は「軸の問題」ではなく「どの階層にStateを適用するか」の問題。
必要ない階層はEyeAreaContentsの参照を空（null）にすれば柔軟に対応可能。

---

## 関連ドキュメント

- [USERUI_EyeArea仕様書.md](./USERUI_EyeArea仕様書.md) - 全体設計
- [ズーム仕様書.md](./ズーム仕様書.md) - ズーム階層の詳細
- [ノベルパート_USERUIとEyeArea連携.md](./ノベルパート/ノベルパート_USERUIとEyeArea連携.md) - ノベルパート実装時の設計
- [ノベルパート未実装機能一覧.md](./ノベルパート/ノベルパート未実装機能一覧.md) - 現在のタスク状況

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-25 | 初版作成。設計判断のためのレポート |
| 2026-01-25 | **結論追記**: 案A（階層軸）採用を決定。レビュー完了 → 終了済みフォルダへ移動 |
