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

### Phase 2: ViewportController分離 ✅ 完了

**目標:** ズーム関連機能を独立クラスに分離

**作業内容:**
1. `ViewportController`クラス作成 ✅
2. zoomContainers管理をViewportControllerへ委譲 ✅
3. WatchUIUpdateにViewportプロパティ追加 ✅
4. IViewportController実装をViewportControllerへ委譲 ✅

**成果物:**
```
Assets/Script/EyeArea/
├── ViewportController.cs       ✅ 作成済み（IViewportController実装）
└── ViewportZoomController.cs   （Phase 1で作成済み、ViewportControllerから利用）
```

**変更ファイル:**
- `WatchUIUpdate.cs` - Viewportプロパティ追加、IViewportController実装を委譲に変更

**影響範囲:**
- WuiZoomControllerAdapter → 変更なし（IViewportController経由で引き続き動作）
- DefaultIntroOrchestrator → 変更なし（IZoomController経由）
- 外部コード → `WatchUIUpdate.Instance.Viewport` で直接アクセス可能に

---

### Phase 3: BattleUIController分離

**目標:** バトル関連UI機能を独立クラスに分離

#### Phase 3a: ActionMarkController分離 ✅ 完了

**成果物:**
- `ActionMarkController.cs` - IActionMarkController実装

**変更:**
- WatchUIUpdateの全ActionMarkメソッドをActionMarkControllerへ委譲
- `ActionMarkCtrl` プロパティ追加

---

#### Phase 3b: KZoomController分離 ✅ 完了

**成果物:**
- `KZoomState.cs` - 内部状態管理クラス
- `KZoomConfig.cs` - SerializeField設定クラス
- `KZoomController.cs` - IKZoomController実装

**変更:**
- WatchUIUpdateのKZoomメソッド（EnterK, ExitK, ForceExitKImmediate）をKZoomControllerへ委譲
- `KZoomCtrl` プロパティ追加

**以下は分離時の分析記録:**

KZoom機能は以下の理由で分離が困難（Func委譲で解決済み）:

**1. SerializeField依存（10個）:**
```csharp
[SerializeField] private RectTransform kZoomRoot;        // ズーム対象ルート
[SerializeField] private RectTransform kTargetRect;      // フィット目標枠
[SerializeField] private float kFitBlend01;              // フィットブレンド
[SerializeField] private float kZoomDuration;            // ズーム時間
[SerializeField] private Ease kZoomEase;                 // イージング
[SerializeField] private TMPTextBackgroundImage kNameText;     // 名前テキスト
[SerializeField] private TMPTextBackgroundImage kPassivesText; // パッシブテキスト
[SerializeField] private float kTextSlideDuration;       // テキストスライド時間
[SerializeField] private float kPassivesFadeDuration;    // パッシブフェード時間
[SerializeField] private bool disableIconClickWhileBattleZoom; // ズーム中クリック無効
// ... その他多数
```

**2. 内部状態（10個以上）:**
```csharp
private bool _isKActive;                    // Kモードアクティブ
private bool _isKAnimating;                 // アニメーション中
private CancellationTokenSource _kCts;      // キャンセルトークン
private Vector2 _kOriginalPos;              // 元位置
private Vector3 _kOriginalScale;            // 元スケール
private bool _kSnapshotValid;               // スナップショット有効
private string _kPassivesTokensRaw;         // パッシブトークン
private TMP_Text _kPassivesTMP;             // TMPキャッシュ
private UIController _kExclusiveUI;         // 対象UI
private bool _actionMarkWasActiveBeforeK;   // ActionMark退避
private bool _schizoWasVisibleBeforeK;      // SchizoLog退避
private List<(UIController, bool)> _kHiddenOtherUIs; // 非対象UI退避
```

**3. 外部依存:**
- `BattleUIBridge.Active?.BattleContext` - 全キャラクター取得
- `SchizoLog.Instance` - 表示切替
- `ActionMarkCtrl` - 表示切替
- バトルズームフラグ（`_isZoomAnimating`, `_isAllySlideAnimating`）

**4. ヘルパーメソッド（7個）:**
```csharp
ComputeKFit()          // フィット計算
SlideInKTexts()        // テキストスライドアニメーション
FadeInKPassives()      // パッシブフェードアニメーション
SetKPassivesText()     // パッシブテキスト設定
FindActorByUI()        // UI→Actor逆引き
BuildKPassivesTokens() // パッシブトークン生成
GetWorldRect()         // ワールド矩形取得
```

**5. LitMotionアニメーション:**
- スケールアニメーション（kZoomRoot.localScale）
- 位置アニメーション（kZoomRoot.anchoredPosition）
- テキストスライド/フェード

**分離戦略（段階的アプローチ）:**

```
【Step 1】状態をまとめる構造体作成
KZoomState {
    IsActive, IsAnimating, OriginalPos, OriginalScale, ...
}

【Step 2】設定をまとめるクラス作成
KZoomConfig {
    Duration, Ease, FitBlend, TextSlideDuration, ...
}

【Step 3】KZoomControllerクラス作成
KZoomController {
    - KZoomState state
    - KZoomConfig config
    - 参照（kZoomRoot, kTargetRect, etc.）
    - EnterK() / ExitK() / ForceExitKImmediate()
    - ヘルパーメソッド群
}

【Step 4】外部依存の注入
- Func<IEnumerable<BaseStates>> getAllCharacters（BattleUIBridge経由）
- Action<bool> setSchizoVisible（SchizoLog制御）
- IActionMarkController actionMark
```

---

#### Phase 3c: 敵配置分離 ✅ 完了

**成果物:**
- `IEnemyPlacementController.cs` - インターフェース
- `EnemyPlacementConfig.cs` - SerializeField設定クラス
- `EnemyPlacementController.cs` - IEnemyPlacementController実装

**変更:**
- WatchUIUpdateのPlaceEnemiesFromBattleGroupをEnemyPlacementControllerへ委譲
- `EnemyPlacementCtrl` プロパティ追加
- 旧実装コードは`#if false`で無効化（後方互換性のため保持）

**以下は分離時の分析記録:**

**1. SerializeField依存（7個）:**
```csharp
[SerializeField] private RectTransform enemySpawnArea;   // 配置エリア
[SerializeField] private Transform enemyBattleLayer;    // 配置レイヤー
[SerializeField] private UIController enemyUIPrefab;    // 敵UIプレハブ
[SerializeField] private Vector2 hpBarSizeRatio;        // HPバーサイズ比
[SerializeField] private float enemyMargin;             // 配置余白
[SerializeField] private bool throttleEnemySpawns;      // スロットル有効
[SerializeField] private int enemySpawnBatchSize;       // バッチサイズ
[SerializeField] private int enemySpawnInterBatchFrames; // バッチ間フレーム
```

**2. ズームパラメータ依存:**
```csharp
private Vector2 _gotoScaleXY;  // ズーム倍率
private Vector2 _gotoPos;      // ズーム位置
// → WorldToPreZoomLocal()で使用
```

**3. ヘルパーメソッド:**
```csharp
GetRandomPreZoomLocalPosition() // ランダム位置計算
WorldToPreZoomLocal()           // 座標変換（ズーム考慮）
PlaceEnemyUI()                  // 個別敵UI配置
EraceEnemyUI()                  // 敵UI削除
```

**分離戦略:**

```
【Step 1】設定構造体作成
EnemyPlacementConfig {
    HpBarSizeRatio, Margin, ThrottleEnabled, BatchSize, InterBatchFrames
}

【Step 2】EnemyPlacementController作成
EnemyPlacementController {
    - EnemyPlacementConfig config
    - 参照（spawnArea, battleLayer, prefab）
    - ズームパラメータ取得用デリゲート
    - PlaceEnemiesAsync() / ClearEnemies()
}
```

---

#### Phase 3d: BattleUIController統合 ✅ 完了

**現在の構成:**
WatchUIUpdateが3つのコントローラーのファサードとして機能:
```
WatchUIUpdate
├── ActionMarkCtrl      → ActionMarkController  ✅
├── KZoomCtrl           → KZoomController       ✅
├── EnemyPlacementCtrl  → EnemyPlacementController ✅
└── IntroOrchestrator参照（既存）
```

**決定事項:**
- 新規BattleUIControllerクラスは作成せず、WatchUIUpdateがファサードとして継続
- 各コントローラーはWatchUIUpdate.Instance経由でアクセス可能
- 完全な分離はPhase 6（EyeAreaManager統合）で実施予定

**BattleUIBridgeからのアクセス:**
- `WatchUIUpdate.Instance.ActionMarkCtrl` - ActionMark操作
- `WatchUIUpdate.Instance.KZoomCtrl` - KZoom操作
- `WatchUIUpdate.Instance.EnemyPlacementCtrl` - 敵配置操作

---

### Phase 4: WalkingUIController分離 ✅ 完了

**目標:** 歩行システム関連を独立クラスに分離

**成果物:**
- `IWalkingUIController.cs` - インターフェース
- `WalkingUIController.cs` - 実装

**変更:**
- WatchUIUpdateのApplyNodeUIをWalkingUIControllerへ委譲
- `WalkingUICtrl` プロパティ追加
- IActionMarkControllerにSetStageThemeColor追加

---

### Phase 5: ベンチマーク機能削除 ✅ 完了

**目標:** ベンチマーク測定UI・実行コードを完全削除（最適化コード自体は保持）

#### 削除対象

**シーン上のGameObject（手動削除）:**
- [ ] `PerformanceHUD` - FPS/メトリクス表示オーバーレイ
- [ ] `PresetPerformanceText` - ベンチ結果表示TMP（WatchUIUpdate.presetLogTMPに割当）

**スクリプトファイル削除:**
```
Assets/Script/Debug/
- [ ] PerformanceHUD.cs

Assets/Script/Benchmarking/  ← フォルダごと削除
- [ ] BenchmarkRunner.cs
- [ ] BenchmarkSummary.cs
- [ ] BenchmarkContext.cs
- [ ] BenchmarkRunResult.cs
- [ ] IBenchmarkScenario.cs
- [ ] WalkOneStepScenario.cs
- [ ] ScenarioSelector.cs
- [ ] ScenarioCatalog.cs
- [ ] IResultFormatter.cs
- [ ] CsvSummaryFormatter.cs
- [ ] JsonSummaryFormatter.cs
- [ ] TmpSummaryFormatter.cs
- [ ] PerRunCsvFormatter.cs
- [ ] PerRunJsonFormatter.cs
- [ ] ISettingsApplier.cs
- [ ] IntroSettingsApplier.cs
- [ ] EnemySpawnSettingsApplier.cs
- [ ] SystemInfoProvider.cs

Assets/Script/Configs/
- [ ] BenchmarkOutputSettings.cs
- [ ] IntroPresetCollection.cs
- [ ] MetricsSettings.cs  ← MetricsHub非依存、削除可

Assets/Script/USERUI/
- [ ] PresetSweepUIBinder.cs

Assets/Script/
- [ ] WatchUIUpdate_BenchmarkHelpers.cs  (partial class)
- [ ] WatchUIUpdate_EnemySpawnPreset.cs  (partial class)
```

**WatchUIUpdate.cs内の削除対象コード（約200行）:**
- [ ] ベンチマーク設定セクション（benchmarkRepeatCount等）
- [ ] BenchMetricsData クラス
- [ ] RunBenchmarkNow() / RunBenchmarkNowContext()
- [ ] プリセットスイープセクション（IntroPreset構造体、presetLogTMP等）
- [ ] RunPresetSweepBenchmark() / StartPresetSweep() / CancelPresetSweep()
- [ ] ApplyIntroPreset()
- [ ] AppendPresetLogLine()
- [ ] MetricsSettings参照と関連プロパティ

**Metrics関連も削除:**
```
Assets/Script/Metrics/  ← フォルダごと削除
- [ ] MetricsHub.cs
- [ ] MetricsContext.cs
- [ ] MetricsLoop.cs
- [ ] MetricsHub_Refactored.cs
- [ ] MetricsEvents.cs
- [ ] RingBuffer.cs
```

**BeginSpan呼び出しの削除（計測コードであり最適化ではない）:**
- [ ] WatchUIUpdate.cs内のBeginSpan呼び出し削除
- [ ] EnemyPlacementController.cs内のBeginSpan呼び出し削除
- [ ] DefaultIntroOrchestrator.cs内のBeginSpan呼び出し削除
- [ ] BuildMetricsContext()メソッド削除

---

### Phase 6: EyeAreaManager統合 ✅ 完了

**目標:** 分離したコントローラーを統合管理

**成果物:**
- `EyeAreaManager.cs` - 統合ファサード（シングルトン）

**アクセス方法:**
```csharp
// 直接コントローラーアクセス
EyeAreaManager.Instance.Viewport
EyeAreaManager.Instance.ActionMark
EyeAreaManager.Instance.KZoom
EyeAreaManager.Instance.EnemyPlacement
EyeAreaManager.Instance.WalkingUI

// 便利プロパティ
EyeAreaManager.Instance.IsKZoomActive
EyeAreaManager.Instance.CanEnterKZoom
EyeAreaManager.Instance.EnemySpawnArea
EyeAreaManager.Instance.SideObjectRoot
```

**決定事項:**
- WatchUIUpdateはMonoBehaviourとしてSerializeField保持の役割を継続
- EyeAreaManagerは非MonoBehaviourのファサードとして機能
- 後方互換性のため、WatchUIUpdate.Instance経由のアクセスも引き続き可能

**最終構成:**
```
Assets/Script/EyeArea/
├── EyeAreaManager.cs              ✅ 統合ファサード
├── IViewportController.cs         ✅
├── ViewportController.cs          ✅
├── ViewportZoomController.cs      ✅
├── IActionMarkController.cs       ✅
├── ActionMarkController.cs        ✅
├── IKZoomController.cs            ✅
├── KZoomController.cs             ✅
├── KZoomConfig.cs                 ✅
├── KZoomState.cs                  ✅
├── IEnemyPlacementController.cs   ✅
├── EnemyPlacementController.cs    ✅
├── EnemyPlacementConfig.cs        ✅
├── IWalkingUIController.cs        ✅
└── WalkingUIController.cs         ✅
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

### Phase 2: ViewportController分離 ✅ 完了
- [x] `ViewportController.cs`作成（IViewportController実装）
- [x] zoomContainers管理をViewportControllerへ委譲
- [x] WatchUIUpdateにViewportプロパティ追加
- [x] IViewportController実装をViewportControllerへ委譲
- [x] コンパイル確認
- [ ] ズーム動作確認

### Phase 3: BattleUIController分離 ✅ 完了

#### Phase 3a: ActionMarkController ✅ 完了
- [x] `ActionMarkController.cs`作成
- [x] IActionMarkController実装
- [x] WatchUIUpdateからの委譲
- [x] コンパイル確認

#### Phase 3b: KZoomController ✅ 完了
- [x] `KZoomState.cs`作成（内部状態をまとめる）
- [x] `KZoomConfig.cs`作成（SerializeField設定をまとめる）
- [x] `KZoomController.cs`作成
- [x] 外部依存の注入設計（Func委譲パターン）
- [x] WatchUIUpdateからの委譲
- [x] コンパイル確認

#### Phase 3c: EnemyPlacementController ✅ 完了
- [x] `IEnemyPlacementController.cs`作成
- [x] `EnemyPlacementConfig.cs`作成
- [x] `EnemyPlacementController.cs`作成
- [x] ズームパラメータ依存の解決（デリゲート注入）
- [x] WatchUIUpdateからの委譲
- [x] コンパイル確認

#### Phase 3d: BattleUIController統合 ✅ 完了
- [x] WatchUIUpdateがファサードとして3コントローラーを統合
- [x] ActionMarkCtrl, KZoomCtrl, EnemyPlacementCtrlプロパティ追加
- [x] コンパイル確認

### Phase 4: WalkingUIController分離 ✅ 完了
- [x] `IWalkingUIController.cs`作成
- [x] `WalkingUIController.cs`作成
- [x] ApplyNodeUI委譲
- [x] IActionMarkControllerにSetStageThemeColor追加
- [x] コンパイル確認

### Phase 5: ベンチマーク・計測機能削除 ✅ 完了
- [ ] シーンからPerformanceHUD, PresetPerformanceText削除（手動）
- [x] Assets/Script/Debug/PerformanceHUD.cs 削除
- [x] Assets/Script/Benchmarking/ フォルダ削除
- [x] Assets/Script/Metrics/ フォルダ削除
- [x] Assets/Script/Configs/ からベンチマーク系SO削除（BenchmarkOutputSettings, IntroPresetCollection, MetricsSettings, EnemySpawnPresetCollection）
- [x] Assets/Script/USERUI/PresetSweepUIBinder.cs 削除
- [x] Assets/Script/USERUI/EnemyPresetSweepUIBinder.cs 削除
- [x] Assets/Editor/PresetSweepSafeStop.cs 削除
- [x] WatchUIUpdate partial class削除（_BenchmarkHelpers, _EnemySpawnPreset）
- [x] WatchUIUpdate.cs内ベンチマーク関連コード削除（IntroMetricsData, BenchMetricsData, WalkMetricsData, ApplyMetricsToggles等）
- [x] BeginSpan/StartJitter呼び出し削除（WatchUIUpdate全箇所）
- [x] BuildMetricsContext削除
- [x] コンパイル確認

### Phase 6: EyeAreaManager統合 ✅ 完了
- [x] `EyeAreaManager.cs`作成
- [x] 全コントローラーへの統合アクセス提供
- [x] シングルトンパターン実装
- [x] コンパイル確認

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
| 2026-01-24 | Phase 2完了（ViewportController分離、委譲パターン適用） |
| 2026-01-24 | Phase 3a完了（ActionMarkController分離） |
| 2026-01-24 | Phase 3b/3c分析完了（KZoom/敵配置の詳細計画策定） |
| 2026-01-24 | Phase 3b完了（KZoomController分離、State/Config分離パターン） |
| 2026-01-24 | Phase 3c完了（EnemyPlacementController分離） |
| 2026-01-24 | Phase 3d完了（WatchUIUpdateをファサードとして統合） |
| 2026-01-24 | Phase 4完了（WalkingUIController分離） |
| 2026-01-24 | Phase 6完了（EyeAreaManager統合ファサード作成） |
| 2026-01-24 | Phase 5完了（ベンチマーク/計測コード削除） |
| 2026-01-24 | **リファクタリング完了** |
