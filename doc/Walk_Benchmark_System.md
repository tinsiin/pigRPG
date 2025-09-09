# Walk関数のベンチマーク測定システム

## 概要
pigRPGプロジェクトにおけるWalk関数のベンチマーク測定は、複数の階層で構成された包括的なパフォーマンス計測システムにより実現されています。このシステムは、ゲームプレイ中の一歩の移動処理から戦闘演出まで、あらゆる側面のパフォーマンスを詳細に測定します。

## 測定の階層構造

### 1. Walk関数レベル（最上位）
**Walking.cs: Walk(int footnumber)**
- エントリーポイント: `Walking.cs:200`
- 測定開始: `WatchUIUpdate.BeginWalkCycleTiming()` (Walking.cs:202)
- 測定終了: `WatchUIUpdate.EndWalkCycleTiming()` (Walking.cs:239)
- 計測内容: Walk関数全体の実行時間（TotalMs）

### 2. ベンチマークシナリオレベル
**WalkOneStepScenario.cs**
- 実装: `WalkOneStepScenario.RunOnceAsync()` (WalkOneStepScenario.cs:13)
- 呼び出し: `walking.RunOneWalkStepForBenchmark()` (Walking.cs:285)
- 収集メトリクス:
  - IntroAvgMs: 導入アニメーションの平均フレーム時間
  - IntroP95Ms: 95パーセンタイル
  - IntroMaxMs: 最大フレーム時間
  - ActualMs: 実測時間
  - WalkTotalMs: Walk全体時間

### 3. ベンチマークランナーレベル
**BenchmarkRunner.cs**
- 反復実行管理: `RunRepeatAsync()` (BenchmarkRunner.cs:17)
- プリセットスイープ: `RunPresetSweepAsync()` (BenchmarkRunner.cs:107)
- メトリクスコンテキスト管理:
  - ScenarioName: シナリオ名
  - PresetIndex: プリセットインデックス
  - RunIndex: 実行回数インデックス
  - PresetSummary: プリセット設定の要約

## メトリクス収集システム

### MetricsHub（中央集約）
**MetricsHub.cs**
- シングルトンインスタンス: `MetricsHub.Instance`
- 主要機能:
  - イベント記録: `RecordIntro()`, `RecordWalk()`, `RecordSpan()`
  - Spanベースの計測: `BeginSpan()` によるスコープ計測
  - Jitter計測: `StartJitter()` によるフレーム時間変動の記録
  - 統計計算: `GetSpanStats()` で直近N件の統計（avg/p95/max）

### 計測ポイント（Span）
主要なSpan計測ポイント:
- **Runner.Total**: ベンチマーク全体
- **Runner.Preset**: プリセット単位の実行
- **Runner.RunOnce**: 単一実行
- **Runner.Loop**: ループ処理（待機時間含む）
- **Intro.Prepare**: 導入準備
- **Intro.Play**: 導入アニメーション再生
- **PlaceEnemies**: 敵配置処理
  - PlaceEnemies.Spawn: 生成
  - PlaceEnemies.Layout: レイアウト
  - PlaceEnemies.Activate: アクティベート

## 設定管理

### ScriptableObjectベースの設定
1. **MetricsSettings.cs**
   - enableMetrics: マスタースイッチ
   - enableSpan: Span計測の有効/無効
   - enableJitter: Jitter計測の有効/無効

2. **IntroPresetCollection.cs**
   - items: IntroPreset配列（テストする設定群）
   - repeatCount: 各プリセットの繰り返し回数
   - interRunDelaySec: 実行間の待機時間

3. **BenchmarkOutputSettings.cs**
   - 出力フォーマット設定（CSV/JSON）
   - ファイル保存先設定

### IntroPreset構造体（WatchUIUpdate.cs:273）
```csharp
public struct IntroPreset
{
    public bool introYieldDuringPrepare;   // 準備中のYield制御
    public int introYieldEveryN;           // N回ごとのYield
    public float introPreAnimationDelaySec; // アニメーション前の遅延
    public float introSlideStaggerInterval; // スライドの段差間隔
}
```

## パフォーマンスHUD表示

### PerformanceHUD.cs
リアルタイム表示項目:
- **FPS/フレーム時間**: CPU/GPU/GC情報
- **コンテキスト情報**: 現在のシナリオ/プリセット/実行回
- **Spanメトリクス**: 各処理のリアルタイム計測値
- **統計情報**: 直近N件の avg/p95/max（色分け表示）
  - 緑: 良好（< 16.7ms）
  - 黄: 警告（16.7-33.3ms）
  - 赤: 問題（> 33.3ms）
- **進捗表示**: プリセット/実行回数/ETA

## 実行フロー

### 単一ベンチマーク実行
1. `WatchUIUpdate.RunBenchmarkNow()` を呼び出し
2. `WalkOneStepScenario` を指定回数実行
3. 各実行で `Walking.Walk(1)` を呼び出し
4. メトリクスを収集・集計
5. 結果を `LastBenchMetrics` に保存

### プリセットスイープ実行
1. `WatchUIUpdate.StartPresetSweep()` を呼び出し
2. `IntroPresetCollection` の各プリセットを順次適用
3. 各プリセットで指定回数の実行
4. 結果をCSV/JSONで出力
5. TMPへのリアルタイム表示（設定時）

## コンパイル時制御

### METRICS_DISABLEDマクロ
- MetricsHub.cs内で条件コンパイル
- 無効時はスタブ実装に切り替え
- 本番ビルドでのオーバーヘッド削減

## 測定結果の活用

### BenchmarkRunResult構造体
- IntroAvgMs: 導入アニメの平均フレーム時間
- IntroP95Ms: 95パーセンタイル
- IntroMaxMs: 最大値
- ActualMs: 実測時間
- WalkTotalMs: Walk全体時間
- LoopMs: ループ全体時間（待機含む）
- Success: 成功/失敗フラグ
- ErrorMessage: エラー詳細

### 出力フォーマット
- **CSV**: プリセットごとの平均値を行単位で記録
- **JSON**: 構造化データとして全結果を保存
- **TMP表示**: リアルタイムでの結果表示
- **Console**: デバッグ用のログ出力

## まとめ
Walk関数のベンチマーク測定システムは、多層的な計測アーキテクチャにより、詳細なパフォーマンスプロファイリングを実現しています。ScriptableObjectベースの設定管理により、様々なシナリオでの性能テストが容易に実施でき、リアルタイムHUD表示により即座にボトルネックを特定できます。