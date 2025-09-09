# ズーム構成システム

## 概要
pigRPGプロジェクトにおけるズームシステムは、戦闘開始時の演出を管理する重要な機能です。このシステムは、背景と敵のズーム、味方アイコンのスライドイン、そしてパフォーマンス測定を統合的に管理します。

## アーキテクチャ

### 階層構造
```
Walking.cs (エンカウント)
    ↓
WatchUIUpdate.cs (FirstImpressionZoomImproved)
    ↓
IIntroOrchestrator (導入演出の調整)
    ├── IZoomController (ズーム制御)
    └── IEnemyPlacer (敵配置)
```

## 主要コンポーネント

### 1. WatchUIUpdate.cs
**中心的な演出管理クラス**
- `FirstImpressionZoomImproved()`: 改良版ズーム実行メソッド（Walking.cs:176で呼び出し）
- `RestoreZoomViaOrchestrator()`: Orchestrator経由でのズーム復帰（WatchUIUpdate.cs:57）

### 2. IZoomController インターフェース
**ズーム制御の抽象化**（IZoomController.cs）
```csharp
public interface IZoomController
{
    UniTask ZoomInAsync(float duration, CancellationToken ct);    // ズームイン
    UniTask ZoomOutAsync(float duration, CancellationToken ct);   // ズームアウト
    UniTask SetZoomAsync(float target, float duration, CancellationToken ct); // 任意倍率
    void Reset();                                                  // 即時リセット
    void CaptureOriginal(RectTransform front, RectTransform back); // 原状キャプチャ
    void RestoreImmediate();                                       // 即時復元
    UniTask RestoreAsync(float duration, AnimationCurve curve, CancellationToken ct); // アニメ付き復元
}
```

### 3. WuiZoomControllerAdapter
**具体的なズーム実装**（WuiZoomControllerAdapter.cs）
- zoomFrontContainer/zoomBackContainerのRectTransformを直接操作
- スケール変換によるズーム効果の実現
- 原状保存と復元機能

## ズーム設定パラメータ

### IntroPreset構造体（WatchUIUpdate.cs:273）
ズーム演出の詳細設定を管理：
- `introYieldDuringPrepare`: 準備中のYield制御（フレーム分割）
- `introYieldEveryN`: N回ごとにYieldを挿入（負荷分散）
- `introPreAnimationDelaySec`: アニメーション前の遅延時間
- `introSlideStaggerInterval`: スライドの段差間隔（味方アイコンの順次登場）

### ズーム関連の内部パラメータ
WatchUIUpdate.csで管理される設定値：
- `_gotoScaleXY`: ズーム目標倍率
- `_gotoPos`: ズーム時の位置調整
- `_firstZoomSpeedTime`: ズーム速度（時間）
- `_firstZoomAnimationCurve`: アニメーションカーブ

## 実行フロー

### 戦闘開始時のズーム処理
1. **エンカウント検出**（Walking.cs:150 `Encount()`）
   - 敵グループの生成
   - 味方グループの選出

2. **ズーム演出開始**（Walking.cs:176）
   ```csharp
   await wui.FirstImpressionZoomImproved();
   ```

3. **Orchestrator初期化**（WatchUIUpdate.cs:75）
   - DefaultIntroOrchestratorの生成
   - WuiZoomControllerAdapterの設定
   - WuiEnemyPlacerAdapterの設定

4. **コンテキスト構築**（WatchUIUpdate.cs:85）
   - シナリオ名、プリセット情報の設定
   - RectTransform参照の取得
   - アニメーション設定の適用

5. **ズーム実行**
   - 背景（zoomBackContainer）のスケール変更
   - 前景（zoomFrontContainer）のスケール変更
   - 敵UI要素の配置（並行実行）

### ズーム復帰処理
1. **RestoreZoomViaOrchestrator呼び出し**
   - animated: アニメーション有無の指定
   - duration: アニメーション時間

2. **原状復帰**（WuiZoomControllerAdapter.cs:95）
   - 保存された原状へのアニメーション
   - 位置とスケールの同時補間

## パフォーマンス計測

### ズーム処理の測定ポイント
- **Intro.Prepare**: ズーム準備処理
- **Intro.Play**: ズーム実行とアニメーション
- **PlaceEnemies**: 敵配置処理
  - Spawn: 敵オブジェクト生成
  - Layout: レイアウト計算
  - Activate: アクティベーション

### メトリクス収集
MetricsHubを通じて以下を記録：
- PlannedMs: 理論所要時間
- ActualMs: 実測時間
- DelayMs: 遅延（実測-理論）
- EnemyPlacementMs: 敵配置の実測時間
- IntroFrameAvgMs/P95Ms/MaxMs: フレーム時間統計

## 最適化技術

### 1. 並行処理
- ズームと敵UI生成を並行実行
- UniTask.WhenAllによる待機処理

### 2. Yield制御
- introYieldEveryNによる負荷分散
- フレーム分割による滑らかな描画

### 3. バッチアクティベーション
- 敵オブジェクトの一括有効化
- SetActive呼び出しの最小化

### 4. アニメーションカーブ
- カスタムカーブによる自然な動き
- LerpUnclampedによる補間計算

## 設定と調整

### ScriptableObjectでの管理
1. **IntroPresetCollection**
   - 複数のプリセット設定を保持
   - ベンチマークでの比較テスト用

2. **MetricsSettings**
   - 計測の有効/無効切り替え
   - オーバーヘッド管理

### デバッグとモニタリング
- PerformanceHUDでのリアルタイム表示
- 色分けによるパフォーマンス可視化
- 統計情報（avg/p95/max）の表示

## 今後の拡張性

### インターフェース設計の利点
- IZoomControllerによる実装の差し替え可能性
- 異なるズーム戦略の実装（例：カメラベース、シェーダーベース）
- テスト用モック実装の容易な作成

### Orchestratorパターン
- 複雑な演出シーケンスの管理
- 各コンポーネントの独立性維持
- 新しい演出要素の追加が容易

## まとめ
ズーム構成システムは、戦闘演出の中核を担う重要な機能です。インターフェースベースの設計により柔軟性を保ちながら、詳細なパフォーマンス計測により最適化の余地を常に把握できる構造となっています。Orchestratorパターンの採用により、今後の拡張や改善が容易に行える設計となっています。