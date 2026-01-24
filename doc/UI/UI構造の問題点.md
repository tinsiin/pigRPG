# UI構造の問題点

## 概要

SampSceneのUI構造における問題点と、リファクタリングの方向性についてまとめる。

---

## 現状のシーンUI階層

### 調査結果（2026-01-24）

```
AlwaysCanvas（常時表示Canvas）
├── EyeArea（上半分 - 視覚要素）
│   ├── EnemyRandomSpawnArea
│   ├── KZoomArea
│   │   ├── ZoomBackContainer (RectTransform)
│   │   │   └── BackGround ← 共通背景
│   │   │       ├── NoiseSea
│   │   │       ├── cloud
│   │   │       ├── WalkWeb（歩行画面用）
│   │   │       ├── TriggerCount
│   │   │       └── Road（歩行画面用道路）
│   │   ├── MiddleFixedContainer（ズームしない固定レイヤー）
│   │   │   ├── ActionMark
│   │   │   └── ActionMarkSpawnPoint
│   │   ├── ZoomFrontContainer (RectTransform)
│   │   ├── Charas（BassJack, Satelite, Geino）
│   │   ├── BSAmanager（BattleSystemArrowManager）
│   │   └── SchizoLog
│   ├── PerformanceHUD
│   └── PresetPerformanceText
│
├── Message（MessageDropper）
├── KZoomIconTargetRect
├── KZoomPassiveText
└── KZoomNameText

DynamicCanvas（動的Canvas）
├── USERUI（下半分 - UI操作要素）
│   ├── topLine
│   └── ToggleButtons（操作ボタン群）
├── mapImage
├── testProgressStrings
├── bgHeader
├── SelectArea
├── bgLine
├── ModalArea
└── ProgressText
```

---

## 既存のズームアーキテクチャ

**重要: WatchUIUpdateには2つの異なるズーム機構が存在する。**

### 機構1: IZoomController（バトルイントロ用）

```csharp
// Assets/Script/Orchestration/IZoomController.cs
public interface IZoomController
{
    UniTask ZoomInAsync(float duration, CancellationToken ct);
    UniTask ZoomOutAsync(float duration, CancellationToken ct);
    UniTask SetZoomAsync(float target, float duration, CancellationToken ct);
    void CaptureOriginal(RectTransform front, RectTransform back);
    void RestoreImmediate();
    UniTask RestoreAsync(float duration, AnimationCurve curve, CancellationToken ct);
}
```

**用途:** バトル開始時のイントロ演出（敵出現時など）

**対象コンテナ:**
- `zoomBackContainer` → BackGround（背景）をズーム
- `zoomFrontContainer` → EnemyArea（敵エリア）をズーム
- `MiddleFixedContainer` → **ズームしない**（ActionMark等が見えたまま）

**実装:**
- `WuiZoomControllerAdapter` - リフレクションでprivateフィールドを参照（暫定）
- `DefaultIntroOrchestrator` - Prepare→Play→Restoreのライフサイクル管理

### 機構2: KZoom（アイコンタップ詳細表示用）

```csharp
// WatchUIUpdate.cs内
public async UniTask EnterK(RectTransform iconRT, string title);
public async UniTask ExitK();
public void ForceExitKImmediate();
```

**用途:** バトル中にキャラアイコンをタップした時のステータス詳細表示

**対象:**
- `kZoomRoot` = KZoomArea全体をスケール・移動
- 特定アイコンが`kTargetRect`にフィットするよう計算

**動作:**
1. 他キャラのUI → **非表示化** (`SetActive(false)`)
2. ActionMark → **非表示化** (`HideActionMark()`)
3. SchizoLog → **非表示化**
4. kZoomRoot全体をスケール・移動（全子要素が一緒にズーム）
5. K専用テキスト（名前、パッシブ一覧）をスライド表示

### 2つの機構の違い

| 項目 | IZoomController | KZoom |
|------|-----------------|-------|
| 対象 | ZoomBack + ZoomFront (個別) | kZoomRoot (全体) |
| MiddleFixed | ズームしない | 全体と一緒にズーム |
| ActionMark | 表示されたまま | 非表示化 |
| 用途 | バトルイントロ、**ノベルパート** | アイコンタップ詳細 |
| 汎用性 | 高い（共用可能） | KZoom専用 |

### ノベルパートでの使用

ノベルパートの「中央オブジェクトズーム」は**IZoomController**を使用する：
- BackGround（背景）をズーム
- ノベルUI（立ち絵、テキストボックス）はズームしない = MiddleFixedContainerと同じ扱い

```
ノベルパートズーム時:
├── ZoomBackContainer    ← ズームする（背景）
├── MiddleFixedContainer ← ズームしない
├── ZoomFrontContainer   ← ズームする
└── NovelPartUI          ← ズームしない（MiddleFixedと同階層に配置すればOK）
```

---

## 重要な認識

### 背景 = 共通リソース

`BackGround`（歩行画面の背景）は、複数のシステムで共通して使用される：

```
BackGround（共通背景）
    │
    ├─ KZoom（バトル中アイコンタップ時のズーム）
    │
    ├─ ノベルパート（中央オブジェクトズーム）
    │
    └─ 今後のシステム（トランジション等）

すべて同じBackGroundをズームする
```

### ズーム処理 = 共通化可能

```
現状:
IZoomController + WuiZoomControllerAdapter
    ↓
KZoom（バトル用）で使用

今後:
同じIZoomControllerを
├─ KZoom
├─ ノベルパート中央オブジェクトズーム
└─ その他のズーム演出
で共用可能
```

### ノベルパートのズーム仕様（設計書より）

```
中央オブジェクト由来の会話 → ズームあり/なし選択可能

ズームあり:
1. 中央オブジェクトにアプローチ
2. 歩行画面の背景ごとズームイン
3. 会話
4. ズームアウトして歩行画面に戻る

ポイント:
- 歩行画面の背景ごとズーム（= BackGroundごとズーム）
- ノベルパートのUI（立ち絵、テキストボックス）はズームしない
```

---

## 問題点

### 1. 歴史的経緯

- 元々BattleManagerのみで開発
- 歩行システムは後から追加
- BackGroundがKZoomArea内にあるが、実際は共通リソース

### 2. 命名と構造の不一致

- `KZoomArea`という名前だが、バトル専用ではない
- BackGroundは「KZoom用」ではなく「共通背景」

### 3. 参照方法の問題

- `WuiZoomControllerAdapter`がリフレクションでprivateフィールドにアクセス（暫定実装）
- 他システムからズーム機能を使いにくい

### 4. 抽象化レイヤーの不足

- EyeArea/USERUIを統一的に制御する仕組みがない
- 状態（歩行中/バトル中/ノベル中）に応じたUI切替が困難

### 5. WatchUIUpdateの肥大化（神クラス問題）

後述の「WatchUIUpdateの現状分析」セクションで詳述。

---

## WatchUIUpdateの現状分析

### 基本情報

| 項目 | 内容 |
|------|------|
| ファイル | `Assets/Script/WatchUIUpdate.cs` |
| 行数 | 約2,500行 + 2つのpartialファイル |
| パターン | シングルトン (`WatchUIUpdate.Instance`) |
| 参照元 | 10以上のシステムから直接参照 |

### 責務一覧（多すぎる）

```
WatchUIUpdate（神クラス）
├── ベンチマーク系
│   ├── RunBenchmarkNow()
│   ├── RunPresetSweepBenchmark()
│   └── IntroMetricsData
│
├── バトルイントロ系
│   ├── FirstImpressionZoomImproved()
│   ├── WaitBattleIntroAnimations()
│   └── RestoreZoomViaOrchestrator()
│
├── ズームコンテナ管理
│   ├── zoomBackContainer (private)
│   ├── zoomFrontContainer (private)
│   └── MiddleFixedContainer
│
├── KZoom（アイコンタップ詳細）
│   ├── EnterK() / ExitK()
│   ├── kZoomRoot
│   └── K専用テキスト表示
│
├── ActionMark制御
│   ├── MoveActionMarkToIcon()
│   ├── MoveActionMarkToActor()
│   └── ShowActionMark() / HideActionMark()
│
├── 敵配置
│   ├── PlaceEnemiesFromBattleGroup()
│   ├── EraceEnemyUI()
│   └── enemySpawnArea
│
├── 味方スライドイン
│   ├── AllySlideIn系
│   └── allySpawnPositions
│
└── 歩行システム連携
    ├── ApplyNodeUI()
    └── SideObjectRoot
```

### 参照元システム

| システム | 使用クラス | 使用機能 |
|----------|-----------|----------|
| バトル | `BattleInitializer` | イントロ、敵配置 |
| バトルUI | `BattleUIBridge` | ActionMark、KZoom |
| キャラUI | `UIController` | KZoom (EnterK) |
| 歩行 | `WalkingSystemManager` | ApplyNodeUI |
| 歩行UI | `Walking` | ForceExitKImmediate |
| ズーム | `WuiZoomControllerAdapter` | zoomContainers (リフレクション) |
| 敵配置 | `WuiEnemyPlacerAdapter` | PlaceEnemies |
| デバッグ | `PerformanceHUD` | ベンチマーク、メトリクス |
| ベンチ | `WalkOneStepScenario` | ベンチマーク実行 |
| 基盤 | `BaseStates` | wui参照 |

### 問題点の詳細

#### 1. 神クラス（God Class）

```
問題:
┌─────────────────────────────────────────────────────────────┐
│                    WatchUIUpdate                            │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐           │
│  │Benchmark│ │BattleUI │ │ KZoom   │ │Walking  │ ...       │
│  └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘           │
└───────┼──────────┼──────────┼──────────┼───────────────────┘
        ↑          ↑          ↑          ↑
        全システムが Instance 経由で直接アクセス
```

- 無関係な責務が1クラスに集約
- 変更の影響範囲が広い
- 理解・保守が困難

#### 2. シングルトン依存

```csharp
// 現状: 全システムがこのパターン
var wui = WatchUIUpdate.Instance;
wui.SomeMethod();
```

- グローバル状態への依存
- テスト時にモック化できない
- 依存関係が暗黙的

#### 3. ノベルパートから見た使いにくさ

```csharp
// ノベルパートがズームしたい場合の問題
var wui = WatchUIUpdate.Instance;

// Q1: 何を呼べばいい？
// - EnterK? → これはバトル専用
// - RestoreZoomViaOrchestrator? → 名前からわからない
// - zoomContainers? → private...

// Q2: 結局どうする？
// 現状: WuiZoomControllerAdapterがリフレクションで無理やりアクセス
var adapter = new WuiZoomControllerAdapter();
adapter.ZoomInAsync(...); // 内部でリフレクション使用
```

### 理想的な構造

```
┌────────────────┐  ┌────────────────┐  ┌────────────────┐
│ IZoomController│  │ IActionMark    │  │ IEnemyPlacer   │
│ (ズーム制御)    │  │ (マーカー制御)  │  │ (敵配置)       │
└───────┬────────┘  └───────┬────────┘  └───────┬────────┘
        │                   │                   │
        └───────────────────┼───────────────────┘
                            ↓
                ┌───────────────────────┐
                │   EyeAreaController   │
                │  (インターフェース実装) │
                └───────────────────────┘
                            ↑
        ┌───────────────────┼───────────────────┐
        │                   │                   │
   BattleSystem      WalkingSystem       NovelPartSystem
   (必要なI/Fのみ)    (必要なI/Fのみ)     (必要なI/Fのみ)
```

**メリット:**
- 各システムは必要なインターフェースのみに依存
- テスト時にモック可能
- 責務が明確に分離

### WatchUIUpdateの段階的改善案

| フェーズ | 作業 | 効果 |
|----------|------|------|
| Phase 1 | zoomContainersをpublic化 | リフレクション解消、ノベルパートから使える |
| Phase 2 | IViewportController実装 | 統一アクセス、依存の明示化 |
| Phase 3 | 責務別にインターフェース分離 | IActionMark, IEnemyPlacer等 |
| Phase 4 | WatchUIUpdateを複数クラスに分割 | 神クラス解消（大規模リファクタ） |

**現実的な優先度:**
- Phase 1は即座に対応可能（小規模変更）
- Phase 2はノベルパート実装時に必要になったら
- Phase 3-4は時間があるときに段階的に

---

## リファクタリングタスク

### タスク1: 背景を「共通リソース」として再定義

**問題:** BackGroundがKZoomArea内にあるが、これは共通リソース

**作業内容:**
- BackGroundを「共通背景」として位置づけを明確化
- 物理的な移動より、参照方法と認識の整理を優先
- ドキュメント・コメントでの明示

**検討事項:**
- KZoomAreaを汎用名（ZoomableAreaなど）に変更するか
- BackGroundへの参照を一元化するか

**依存:** なし

---

### タスク2: IZoomControllerの汎用化

**問題:** IZoomControllerは既にあるが、KZoom専用の暫定実装

**作業内容:**
- `WuiZoomControllerAdapter`のリフレクション依存を解消
- front/backコンテナへの参照を公開API化
- ノベルパート等からも使えるようにする

**関連ファイル:**
- `Assets/Script/Orchestration/IZoomController.cs`
- `Assets/Script/Orchestration/Adapters/WuiZoomControllerAdapter.cs`
- `Assets/Script/WatchUIUpdate.cs`

**依存:** タスク1（認識の整理）

---

### タスク3: WatchUIUpdateの段階的改善

**問題:** WatchUIUpdateが神クラス化しており、複数システムから使いにくい

**Phase 1: 即座に対応（小規模）**
- `zoomBackContainer` / `zoomFrontContainer` をpublic化
- `WuiZoomControllerAdapter`のリフレクション依存を解消

**Phase 2: ノベルパート実装時**
- `IViewportController`インターフェース追加
- WatchUIUpdateに実装させる

**Phase 3: 将来的（大規模）**
- 責務別にインターフェース分離（IActionMark, IEnemyPlacer等）
- WatchUIUpdateを複数クラスに分割

**関連ファイル:**
- `Assets/Script/WatchUIUpdate.cs`
- `Assets/Script/Orchestration/Adapters/WuiZoomControllerAdapter.cs`

**依存:** タスク1, 2

---

### タスク4: EyeArea/USERUIの状態管理

**問題:** 状態（歩行中/バトル中/ノベル中）に応じたUI切替が困難

**作業内容:**
- UI状態を管理する仕組みの設計
- 各状態で表示/非表示にするUI要素の定義
- 状態遷移時のアニメーション管理

**依存:** タスク3（WatchUIUpdate改善後）

---

## 関連コード

| ファイル | 役割 |
|----------|------|
| `WatchUIUpdate.cs` | EyeArea制御、ズームコンテナ保持 |
| `IZoomController.cs` | ズーム制御インターフェース |
| `WuiZoomControllerAdapter.cs` | ズーム実装（暫定） |
| `DefaultIntroOrchestrator.cs` | ズームオーケストレーション |
| `Walking.cs` | 歩行UI |

---

## 関連ドキュメント

- [ノベルパート設計.md](../ノベルパート/ノベルパート設計.md) - ズーム仕様
- [ノベルパートUIシーン配置.md](../ノベルパート/ノベルパートUIシーン配置.md) - テスト用UI配置

---

## 結論: シーン構造は良好、問題は命名と参照方法

### 調査結果

KZoomAreaの内部構造を詳細に調査した結果、**現在のシーン構造は既に適切に設計されている**ことが判明した。

```
KZoomArea (= kZoomRoot for KZoom)
├── ZoomBackContainer     ← IZoomController対象（背景）
│   └── BackGround           └ 共通背景（全システムで共用）
├── MiddleFixedContainer  ← IZoomController対象外（固定レイヤー）
│   ├── ActionMark           └ バトル中表示、K中は非表示
│   └── ActionMarkSpawnPoint
├── ZoomFrontContainer    ← IZoomController対象（前景）
│   └── EnemyArea            └ 敵表示用
├── Charas                ← どちらのズームにも影響されない
├── BSAmanager
└── SchizoLog
```

**ポイント:**
- **IZoomController**（バトルイントロ/ノベルパート用）: Back + Frontをズーム、Middleは固定
- **KZoom**（アイコンタップ用）: kZoomRoot全体をズーム、ActionMark等は非表示化
- ノベルパートUIを**MiddleFixedContainerと同階層**に置けばIZoomControllerでズームされない
- 物理的なシーン構造の変更は不要

### 本質的な問題

1. **命名の問題**: `KZoomArea`という名前がバトル専用に見える
2. **参照方法の問題**: リフレクションでprivateフィールドにアクセスしている
3. **KZoom特有の機構**: MiddleFixedの非表示化、他UIの非表示化はKZoom固有の要件

### 提案: IViewportControllerインターフェース

シーン構造を維持しつつ、汎用的なビューポート制御を実現する。

```csharp
/// <summary>
/// 歩行画面のビューポート（視覚領域）を抽象化するインターフェース。
/// IZoomController（Back/Frontズーム）へのアクセスと、レイヤー参照を提供する。
///
/// 注意: KZoom（kZoomRoot全体ズーム、アイコンタップ詳細）はバトル専用であり、
/// このインターフェースには含めない。
/// </summary>
public interface IViewportController
{
    /// <summary>ズーム制御（既存のIZoomController - Back/Frontを個別にズーム）</summary>
    IZoomController Zoom { get; }

    /// <summary>ズームする背景レイヤー（ZoomBackContainer）</summary>
    RectTransform BackLayer { get; }

    /// <summary>ズームしない固定レイヤー（MiddleFixedContainer）</summary>
    RectTransform FixedLayer { get; }

    /// <summary>ズームする前景レイヤー（ZoomFrontContainer）</summary>
    RectTransform FrontLayer { get; }

    /// <summary>共通背景への参照（BackGround）</summary>
    Transform Background { get; }
}
```

### KZoomとIViewportControllerの住み分け

| 機能 | 使用するAPI | 備考 |
|------|-------------|------|
| バトルイントロズーム | `IViewportController.Zoom` (IZoomController) | 汎用 |
| ノベルパートズーム | `IViewportController.Zoom` (IZoomController) | 汎用 |
| アイコンタップ詳細 | `WatchUIUpdate.EnterK/ExitK` | バトル専用、KZoom固有 |

**KZoomの特殊な要件（IViewportControllerには含めない）:**
- kZoomRoot全体のスケール・位置変更
- 他UIの非表示化（`SetActive(false)`）
- ActionMarkの非表示化
- K専用テキスト（名前、パッシブ）の表示

### 実装方針

| 項目 | 作業内容 |
|------|----------|
| リネーム | `KZoomArea` → `ViewportArea`（任意） |
| 新インターフェース | `IViewportController`を追加 |
| 実装クラス | `WatchUIUpdate`に`IViewportController`を実装させる |
| リフレクション解消 | `WuiZoomControllerAdapter`の参照をpublic API経由に変更 |
| 既存コード | `IZoomController`はそのまま活用、KZoomはWatchUIUpdate内に維持 |

### 利用イメージ

```csharp
// ノベルパートからズームを使う（IZoomController経由）
public class NovelPartController
{
    private IViewportController viewport;

    public async UniTask ZoomToCharacterAsync(CancellationToken ct)
    {
        // IZoomController = Back/Frontを個別にズーム
        // MiddleFixedContainerと同階層のノベルUIはズームされない
        await viewport.Zoom.ZoomInAsync(0.5f, ct);
    }
}

// バトルイントロでも同じIZoomControllerを使用
public class BattleIntroController
{
    private IViewportController viewport;

    public async UniTask PlayIntroAsync(CancellationToken ct)
    {
        await viewport.Zoom.ZoomInAsync(0.3f, ct);
        // ... 敵配置など
        await viewport.Zoom.RestoreAsync(0.5f, null, ct);
    }
}

// KZoomはバトル専用のまま（WatchUIUpdate直接使用）
// WatchUIUpdate.Instance.EnterK(iconRT, title);
// WatchUIUpdate.Instance.ExitK();
```

---

## 実装優先度

### 即座に対応すべき（ノベルパート実装のブロッカー）

| 優先度 | 作業 | 理由 |
|--------|------|------|
| 高 | WatchUIUpdateでzoomContainersをpublic化 | リフレクション解消、ノベルパートから使える |
| 高 | WuiZoomControllerAdapterのリフレクション削除 | 暫定実装の正式化 |

### ノベルパート実装時に検討

| 優先度 | 作業 | 理由 |
|--------|------|------|
| 中 | IViewportController定義 | 複数システムからの統一アクセス |
| 中 | ノベルUIの配置場所決定 | MiddleFixedContainerと同階層に配置 |

### 将来的に検討（大規模リファクタ）

| 優先度 | 作業 | 理由 |
|--------|------|------|
| 低 | WatchUIUpdateの責務分離 | 神クラス解消、保守性向上 |
| 低 | KZoomArea → ViewportAreaリネーム | 可読性向上のみ |
| 低 | EyeArea/USERUI状態管理 | 複数システム間のUI調整が必要になったら |

### 汎用化の対象外

- **KZoom（EnterK/ExitK）**: バトル専用機能、WatchUIUpdate内に維持
- **ベンチマーク系**: デバッグ用、分離不要
- **ActionMark制御**: バトル専用、現状維持

---

## 備考

### シーン構造について
- **物理的なシーン構造変更は不要** - 現状で適切に設計されている
- ノベルパートUIはMiddleFixedContainerと同階層に配置すればIZoomControllerでズームされない

### ズーム機構について
- **IZoomController**: 汎用ズーム（バトルイントロ、ノベルパート）
- **KZoom**: バトル専用（アイコンタップ詳細）、汎用化不要
- 問題の本質は「命名」と「参照方法」のみ

### WatchUIUpdateについて
- **現状**: 神クラス化、約2,500行、10以上のシステムから参照
- **問題**: シングルトン依存、責務過多、テスト困難
- **対応**: 段階的に改善（Phase 1: public化 → Phase 2: I/F化 → Phase 3: 分割）
- **優先**: ノベルパート実装に必要な最小限（zoomContainersのpublic化）から着手

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-24 | 初版作成、シーン階層調査 |
| 2026-01-24 | 2つのズーム機構（IZoomController/KZoom）の分析追加 |
| 2026-01-24 | WatchUIUpdate神クラス問題の分析追加 |
