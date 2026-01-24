# WatchUIUpdateリファクタリング計画

## 概要

WatchUIUpdate（約3,200行の神クラス）を責務別に分離し、各システムから使いやすくする。

---

## 現状分析

### ファイル構成

| ファイル | 行数 | 内容 |
|----------|------|------|
| `WatchUIUpdate.cs` | 約2,500行 | メイン |
| `WatchUIUpdate_BenchmarkHelpers.cs` | 423行 | ベンチマーク補助 |
| `WatchUIUpdate_EnemySpawnPreset.cs` | 280行 | 敵スポーンプリセット |
| **合計** | **約3,200行** | |

### 責務の分類

現在のWatchUIUpdateは以下の責務を持っている:

```
WatchUIUpdate（神クラス）
│
├── 【A】ビューポート/ズーム制御
│   ├── zoomBackContainer / zoomFrontContainer
│   ├── IZoomController関連
│   └── RestoreZoomViaOrchestrator()
│
├── 【B】KZoom（アイコンタップ詳細）
│   ├── kZoomRoot / kTargetRect
│   ├── EnterK() / ExitK()
│   ├── kNameText / kPassivesText
│   └── K専用テキスト表示ロジック
│
├── 【C】ActionMark制御
│   ├── actionMark / actionMarkSpawnPoint
│   ├── MoveActionMarkToIcon()
│   ├── MoveActionMarkToActor()
│   └── Show/HideActionMark()
│
├── 【D】敵配置
│   ├── enemySpawnArea / enemyUIPrefab
│   ├── PlaceEnemiesFromBattleGroup()
│   ├── EraceEnemyUI()
│   └── スロットリング設定
│
├── 【E】味方スライドイン
│   ├── allySpawnPositions / allySlideStartOffset
│   └── スライドアニメーション
│
├── 【F】バトルイントロ統合
│   ├── IIntroOrchestrator
│   ├── FirstImpressionZoomImproved()
│   ├── WaitBattleIntroAnimations()
│   └── IntroMetricsData
│
├── 【G】ベンチマーク/プリセット
│   ├── RunBenchmarkNow()
│   ├── RunPresetSweepBenchmark()
│   ├── IntroPreset / IntroPresetCollection
│   └── BenchMetricsData / WalkMetricsData
│
├── 【H】歩行システム連携
│   ├── StagesString / MapImg
│   ├── ApplyNodeUI()
│   └── SideObjectRoot
│
└── 【I】シングルトン/ライフサイクル
    ├── Instance
    ├── Awake() / OnValidate()
    └── メトリクス設定
```

### 依存関係マップ

```
                    ┌─────────────────┐
                    │  WatchUIUpdate  │
                    │   (Instance)    │
                    └────────┬────────┘
                             │
    ┌────────────────────────┼────────────────────────┐
    │                        │                        │
    ▼                        ▼                        ▼
┌─────────┐          ┌─────────────┐          ┌─────────────┐
│ Battle  │          │   Walking   │          │   KZoom     │
│ System  │          │   System    │          │  (UICtrl)   │
├─────────┤          ├─────────────┤          ├─────────────┤
│敵配置    │          │ApplyNodeUI  │          │EnterK/ExitK │
│イントロ  │          │SideObjRoot  │          │             │
│ActionMark│          │             │          │             │
└─────────┘          └─────────────┘          └─────────────┘
    │
    ▼
┌─────────────────┐
│WuiZoomController│
│    Adapter      │
├─────────────────┤
│zoomContainers   │
│(リフレクション)  │
└─────────────────┘
```

---

## 分離方針

### 新しいクラス構成

```
【分離後】

┌──────────────────────────────────────────────────────────┐
│                      EyeAreaManager                       │
│  (MonoBehaviour, シーンに配置、旧WatchUIUpdateの代替)      │
│                                                           │
│  - 各コントローラーへの参照を保持                          │
│  - シングルトンアクセス提供（後方互換用）                   │
│  - ライフサイクル管理（Awake等）                           │
└───────────────────────────┬──────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
        ▼                   ▼                   ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│ Viewport      │   │ BattleUI      │   │ WalkingUI     │
│ Controller    │   │ Controller    │   │ Controller    │
├───────────────┤   ├───────────────┤   ├───────────────┤
│IViewport      │   │ActionMark制御  │   │ApplyNodeUI    │
│Controller実装 │   │KZoom制御       │   │StagesString   │
│zoomContainers │   │敵配置          │   │MapImg         │
│IZoomController│   │味方スライド    │   │SideObjectRoot │
└───────────────┘   │イントロ統合    │   └───────────────┘
                    └───────────────┘
                            │
                    ┌───────┴───────┐
                    ▼               ▼
            ┌───────────┐   ┌───────────────┐
            │ActionMark │   │ KZoomDisplay  │
            │ Controller│   │  Controller   │
            └───────────┘   └───────────────┘
```

### インターフェース定義

```csharp
// 【A】ビューポート制御
public interface IViewportController
{
    IZoomController Zoom { get; }
    RectTransform BackLayer { get; }
    RectTransform FixedLayer { get; }
    RectTransform FrontLayer { get; }
    Transform Background { get; }
}

// 【C】ActionMark制御
public interface IActionMarkController
{
    void MoveToIcon(RectTransform targetIcon, bool immediate = false);
    void MoveToActor(BaseStates actor, bool immediate = false);
    UniTask MoveToActorScaled(BaseStates actor, bool immediate = false);
    void Show();
    void Hide();
    void ShowFromSpawn(bool zeroSize = true);
}

// 【B】KZoom制御
public interface IKZoomController
{
    bool CanEnterK { get; }
    bool IsKActive { get; }
    bool IsKAnimating { get; }
    bool IsCurrentKTarget(UIController ui);
    UniTask EnterK(RectTransform iconRT, string title);
    UniTask ExitK();
    void ForceExitKImmediate();
}

// 【D】敵配置
public interface IEnemyPlacementController
{
    UniTask PlaceEnemiesAsync(BattleGroup enemyGroup);
    void ClearEnemyUI();
    RectTransform SpawnArea { get; }
}

// 【H】歩行UI
public interface IWalkingUIController
{
    void ApplyNodeUI(string displayName, NodeUIHints hints);
    RectTransform SideObjectRoot { get; }
}
```

---

## 分離フェーズ

### Phase 1: インターフェース抽出（低リスク）

**目標:** 既存コードを変更せず、インターフェースを定義してWatchUIUpdateに実装させる

**作業内容:**
1. `IViewportController`インターフェース作成
2. `IActionMarkController`インターフェース作成
3. `IKZoomController`インターフェース作成
4. WatchUIUpdateにインターフェース実装（既存メソッドをそのまま使用）
5. zoomContainersをpublic化
6. WuiZoomControllerAdapterのリフレクション削除

**成果物:**
```
Assets/Script/EyeArea/
├── IViewportController.cs      ✅ 作成済み
├── IActionMarkController.cs    ✅ 作成済み
├── IKZoomController.cs         ✅ 作成済み
└── ViewportZoomController.cs   ✅ 作成済み（IZoomController実装）
```

**変更ファイル:**
- `WatchUIUpdate.cs` - インターフェース実装追加
- `WuiZoomControllerAdapter.cs` - リフレクション削除

**リスク:** 低（既存コードの動作は変わらない）

---

### Phase 2: ViewportController分離

**目標:** ズーム関連機能を独立クラスに分離

**作業内容:**
1. `ViewportController`クラス作成
2. zoomContainers管理を移動
3. IZoomController実装を移動
4. WatchUIUpdateからの参照を更新

**成果物:**
```
Assets/Script/EyeArea/
└── ViewportController.cs  (IViewportController実装)
```

**影響範囲:**
- WuiZoomControllerAdapter → ViewportController参照に変更
- DefaultIntroOrchestrator → 変更なし（IZoomController経由）

---

### Phase 3: BattleUIController分離

**目標:** バトル関連UI機能を独立クラスに分離

**作業内容:**
1. `BattleUIController`クラス作成
2. ActionMark制御を移動
3. KZoom制御を移動
4. 敵配置を移動
5. 味方スライドを移動
6. イントロ統合を移動

**成果物:**
```
Assets/Script/EyeArea/
├── BattleUIController.cs
├── ActionMarkController.cs  (オプション: さらに分離)
└── KZoomController.cs       (オプション: さらに分離)
```

**影響範囲:**
- BattleUIBridge → BattleUIController参照に変更
- UIController → IKZoomController参照に変更
- BattleInitializer → BattleUIController参照に変更

---

### Phase 4: WalkingUIController分離

**目標:** 歩行システム関連を独立クラスに分離

**作業内容:**
1. `WalkingUIController`クラス作成
2. ApplyNodeUI移動
3. StagesString/MapImg管理を移動
4. SideObjectRoot参照を移動

**成果物:**
```
Assets/Script/EyeArea/
└── WalkingUIController.cs
```

**影響範囲:**
- WalkingSystemManager → WalkingUIController参照に変更

---

### Phase 5: ベンチマーク分離（オプション）

**目標:** ベンチマーク機能を完全分離

**作業内容:**
1. `BenchmarkController`クラス作成
2. ベンチマーク関連を移動
3. プリセット管理を移動

**成果物:**
```
Assets/Script/Debug/
└── BenchmarkController.cs
```

---

### Phase 6: EyeAreaManager統合

**目標:** 分離したコントローラーを統合管理

**作業内容:**
1. `EyeAreaManager`クラス作成
2. 各コントローラーへの参照を保持
3. 後方互換のためのファサード提供
4. WatchUIUpdate削除または最小化

**最終構成:**
```
Assets/Script/EyeArea/
├── EyeAreaManager.cs           (統合管理、シングルトン)
├── IViewportController.cs
├── ViewportController.cs
├── IActionMarkController.cs
├── IKZoomController.cs
├── BattleUIController.cs       (ActionMark + KZoom + 敵配置 + イントロ)
├── IWalkingUIController.cs
└── WalkingUIController.cs

Assets/Script/Debug/
└── BenchmarkController.cs      (オプション)
```

---

## 移行戦略

### 後方互換性の維持

```csharp
// Phase 1-5の間は後方互換を維持
public class WatchUIUpdate : MonoBehaviour,
    IViewportController,
    IActionMarkController,
    IKZoomController,
    IWalkingUIController
{
    public static WatchUIUpdate Instance { get; private set; }

    // 既存コードはそのまま動作
}

// Phase 6で以下に置換
public class EyeAreaManager : MonoBehaviour
{
    public static EyeAreaManager Instance { get; private set; }

    // 後方互換ファサード（非推奨マーク付き）
    [Obsolete("Use Viewport directly")]
    public IViewportController Viewport => _viewport;

    [Obsolete("Use BattleUI.ActionMark directly")]
    public void MoveActionMarkToActor(...) => _battleUI.ActionMark.MoveToActor(...);
}
```

### 段階的な参照更新

```
Phase 1: WatchUIUpdate.Instance.Method()     ← 既存のまま
    ↓
Phase 2-5: インターフェース経由に徐々に移行
    ↓
Phase 6: EyeAreaManager.Instance.Controller.Method()
```

---

## リスクと対策

| リスク | 影響度 | 対策 |
|--------|--------|------|
| 参照切れでコンパイルエラー | 高 | 1フェーズずつ小さく進める |
| シーン参照のSerializeField破損 | 高 | 各フェーズでUnityエディタ動作確認 |
| ベンチマーク動作不良 | 中 | Phase 5は最後に実施 |
| パフォーマンス低下 | 低 | プロファイル比較 |

---

## 作業順序チェックリスト

### Phase 1: インターフェース抽出 ✅ 完了
- [x] `Assets/Script/EyeArea/`フォルダ作成
- [x] `IViewportController.cs`作成
- [x] `IActionMarkController.cs`作成
- [x] `IKZoomController.cs`作成
- [x] `ViewportZoomController.cs`作成（IZoomController実装）
- [x] WatchUIUpdateにインターフェース実装宣言追加
- [x] zoomContainersをpublic化（プロパティ追加: ZoomBackContainer, ZoomFrontContainer）
- [x] WuiZoomControllerAdapterのリフレクション削除（IViewportController経由に変更）
- [x] コンパイル確認
- [ ] 動作確認（バトル、KZoom）

### Phase 2: ViewportController分離
- [ ] `ViewportController.cs`作成
- [ ] zoomContainers管理を移動
- [ ] WatchUIUpdateからViewportController参照
- [ ] コンパイル確認
- [ ] ズーム動作確認

### Phase 3: BattleUIController分離
- [ ] `BattleUIController.cs`作成
- [ ] ActionMark制御を移動
- [ ] KZoom制御を移動
- [ ] 敵配置を移動
- [ ] イントロ統合を移動
- [ ] コンパイル確認
- [ ] バトル全体動作確認

### Phase 4: WalkingUIController分離
- [ ] `WalkingUIController.cs`作成
- [ ] ApplyNodeUI移動
- [ ] コンパイル確認
- [ ] 歩行画面動作確認

### Phase 5: ベンチマーク分離（オプション）
- [ ] `BenchmarkController.cs`作成
- [ ] ベンチマーク関連を移動
- [ ] コンパイル確認

### Phase 6: EyeAreaManager統合
- [ ] `EyeAreaManager.cs`作成
- [ ] 各コントローラー統合
- [ ] WatchUIUpdate.Instance → EyeAreaManager.Instance移行
- [ ] 後方互換ファサード追加
- [ ] 全体動作確認

---

## 関連ドキュメント

- [UI構造の問題点.md](./UI構造の問題点.md) - 背景分析
- [ノベルパート設計.md](../ノベルパート/ノベルパート設計.md) - ノベルパートからの利用想定

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-24 | 初版作成 |
| 2026-01-24 | Phase 1完了（インターフェース抽出、リフレクション削除） |
