# Walk関数ベンチマークシステム詳細仕様書

## 1. システム概要

### 1.1 目的
pigRPGプロジェクトにおけるWalk関数（ゲーム内の一歩移動処理）の包括的なパフォーマンス計測・最適化システム。戦闘エンカウント時の演出パフォーマンスを詳細に分析し、最適な設定値を見つけることを目的とする。

### 1.2 主要機能
- リアルタイムパフォーマンス計測
- 複数プリセット設定での自動ベンチマーク実行
- 詳細なメトリクス収集とレポート生成
- 視覚的なパフォーマンスHUD表示
- CSV/JSON形式での結果出力

## 2. アーキテクチャ詳細

### 2.1 コンポーネント階層

```
┌─────────────────────────────────────────────────────────┐
│                    Application Layer                      │
├─────────────────────────────────────────────────────────┤
│  Walking.cs                                               │
│  └── Walk(int footnumber)                                │
│      ├── BeginWalkCycleTiming()                         │
│      ├── Encount() → FirstImpressionZoomImproved()      │
│      └── EndWalkCycleTiming()                           │
├─────────────────────────────────────────────────────────┤
│                  Orchestration Layer                      │
├─────────────────────────────────────────────────────────┤
│  IIntroOrchestrator / DefaultIntroOrchestrator           │
│  ├── PrepareAsync()                                      │
│  ├── PlayAsync()                                         │
│  └── RestoreAsync()                                      │
│                                                           │
│  IZoomController / WuiZoomControllerAdapter              │
│  └── Zoom制御実装                                        │
├─────────────────────────────────────────────────────────┤
│                   Benchmark Layer                         │
├─────────────────────────────────────────────────────────┤
│  BenchmarkRunner                                         │
│  ├── RunRepeatAsync()                                    │
│  └── RunPresetSweepAsync()                              │
│                                                           │
│  WalkOneStepScenario                                     │
│  └── RunOnceAsync()                                      │
├─────────────────────────────────────────────────────────┤
│                    Metrics Layer                          │
├─────────────────────────────────────────────────────────┤
│  MetricsHub (Singleton)                                  │
│  ├── Event Recording (Intro/Walk/Span/Jitter)          │
│  ├── Span Measurement                                    │
│  └── Statistics Calculation                             │
├─────────────────────────────────────────────────────────┤
│                  Visualization Layer                      │
├─────────────────────────────────────────────────────────┤
│  PerformanceHUD                                          │
│  └── Real-time metrics display                          │
└─────────────────────────────────────────────────────────┘
```

## 3. データ構造詳細

### 3.1 IntroPreset構造体
```csharp
public struct IntroPreset
{
    public bool introYieldDuringPrepare;    // 準備中のYield制御（負荷分散）
    public int introYieldEveryN;           // N回ごとにYieldを挿入（1-100）
    public float introPreAnimationDelaySec; // アニメーション前遅延（0-1秒）
    public float introSlideStaggerInterval; // スライド段差間隔（0-0.2秒）
}
```

### 3.2 BenchmarkRunResult構造体
```csharp
public class BenchmarkRunResult
{
    public double IntroAvgMs;     // 導入アニメ平均フレーム時間
    public double IntroP95Ms;     // 95パーセンタイル
    public double IntroMaxMs;     // 最大フレーム時間
    public double ActualMs;       // 実測時間
    public double WalkTotalMs;    // Walk全体時間
    public double LoopMs;         // ループ処理時間（待機含む）
    public bool Success;          // 成功フラグ
    public string ErrorMessage;   // エラー詳細
}
```

### 3.3 MetricsContext構造体
```csharp
public sealed class MetricsContext
{
    public string ScenarioName;      // シナリオ名（例："Walk(1)"）
    public string PresetSummary;     // プリセット要約（例："true 4 0.01 0.06"）
    public int PresetIndex;          // プリセットインデックス（0-based）
    public int RunIndex;             // 実行回数（1-based）
    public Dictionary<string, string> Tags; // 追加タグ情報
}
```

## 4. 計測ポイント（Span）仕様

### 4.1 Runner系Span
| Span名 | 計測範囲 | 説明 |
|--------|----------|------|
| Runner.Total | 全ベンチマーク | プリセットスイープ全体の実行時間 |
| Runner.Preset | プリセット単位 | 1プリセットの全実行時間 |
| Runner.RunOnce | 単一実行 | 1回のシナリオ実行時間 |
| Runner.Loop | ループ処理 | 1回の実行＋待機時間 |

### 4.2 Intro系Span
| Span名 | 計測範囲 | 説明 |
|--------|----------|------|
| Intro.Prepare | 準備処理 | ズーム準備・初期化時間 |
| Intro.Play | 再生処理 | ズームアニメーション実行時間 |
| Intro.Zoom.Prepare | ズーム準備 | 原状キャプチャ処理 |
| Intro.Zoom.Play | ズーム再生 | ズームアニメーション |
| Intro.Zoom.Restore | ズーム復元 | 原状復帰処理 |

### 4.3 PlaceEnemies系Span
| Span名 | 計測範囲 | 説明 |
|--------|----------|------|
| PlaceEnemies | 敵配置全体 | 敵UI生成・配置の全体時間 |
| PlaceEnemies.Spawn | 生成処理 | 敵オブジェクト生成 |
| PlaceEnemies.Layout | レイアウト | 配置計算処理 |
| PlaceEnemies.Activate | 有効化 | オブジェクトアクティベート |

## 5. 出力フォーマット詳細

### 5.1 CSV出力フォーマット

#### ヘッダー情報
```csv
# {timestamp},presets={count},repeat={repeat},scenario={name},preset_collection={collection},unity_version={version},platform={platform},device_model={model},quality_level={level}
```

#### データ行
```csv
setting,avg_intro_avg_ms,avg_intro_p95_ms,avg_intro_max_ms,avg_actual_ms,avg_walk_total_ms,ok,total,scenario,preset_index
```

#### Per-Run CSV（詳細実行ログ）
```csv
scenario,preset_index,preset_summary,run_index,success,intro_avg_ms,intro_p95_ms,intro_max_ms,actual_ms,walk_total_ms,loop_ms,tags,error
```

### 5.2 JSON出力フォーマット
```json
{
    "header": {
        "timestamp": "2024-01-15 10:30:00",
        "presets": 10,
        "repeat": 100,
        "scenario": "Walk(1)",
        "preset_collection": "DefaultPresets",
        "unity_version": "2022.3.10f1",
        "platform": "Windows",
        "device_model": "Desktop-PC",
        "quality_level": "High"
    },
    "results": [
        {
            "preset": {
                "introYieldDuringPrepare": true,
                "introYieldEveryN": 4,
                "introPreAnimationDelaySec": 0.01,
                "introSlideStaggerInterval": 0.06
            },
            "summary": {
                "avg_intro_avg_ms": 15.2,
                "avg_intro_p95_ms": 18.5,
                "avg_intro_max_ms": 22.1,
                "avg_actual_ms": 120.5,
                "avg_walk_total_ms": 135.2,
                "success_count": 98,
                "total_count": 100
            }
        }
    ],
    "footer": {
        "total_rows": 10,
        "total_ok": 980,
        "total_requests": 1000
    }
}
```

## 6. パフォーマンスHUD仕様

### 6.1 表示項目

#### 基本情報
- FPS（平均/最小/最大）
- CPUフレーム時間
- GPUフレーム時間
- GC/フレーム（KB）

#### コンテキスト情報
- シナリオ名
- プリセット設定要約
- プリセットインデックス
- 実行回数

#### リアルタイムメトリクス
- 各Spanの最新計測値
- 統計情報（avg/p95/max）
- 色分け表示（緑/黄/赤）

#### 進捗表示
- プリセット進捗（現在/総数）
- 実行回進捗（現在/総数）
- 完了数/総数
- ETA（推定残り時間）

### 6.2 色分けしきい値

| プリセット | 良好（緑） | 警告（黄） | 問題（赤） |
|------------|------------|------------|------------|
| FPS60 | < 16.7ms | 16.7-33.3ms | > 33.3ms |
| FPS30 | < 33.3ms | 33.3-66.7ms | > 66.7ms |
| FPS24 | < 41.7ms | 41.7-83.3ms | > 83.3ms |
| Custom | ユーザー定義 | ユーザー定義 | ユーザー定義 |

## 7. ScriptableObject設定

### 7.1 MetricsSettings
```csharp
[CreateAssetMenu(menuName = "Benchmark/MetricsSettings")]
public sealed class MetricsSettings : ScriptableObject
{
    public bool enableMetrics = true;    // マスタースイッチ
    public bool enableSpan = true;       // Span計測有効化
    public bool enableJitter = true;     // Jitter計測有効化
}
```

### 7.2 IntroPresetCollection
```csharp
[CreateAssetMenu(menuName = "Benchmark/IntroPresetCollection")]
public sealed class IntroPresetCollection : ScriptableObject
{
    public IntroPreset[] items;          // プリセット配列
    public int repeatCount = 100;        // 各プリセットの繰り返し回数
    public float interRunDelaySec = 0f;  // 実行間待機時間
    public string description;           // 説明文
}
```

### 7.3 BenchmarkOutputSettings
```csharp
[CreateAssetMenu(menuName = "Benchmark/BenchmarkOutputSettings")]
public sealed class BenchmarkOutputSettings : ScriptableObject
{
    public bool enableCsv = true;        // CSV出力有効化
    public bool enableJson = true;       // JSON出力有効化
    public bool enablePerRunCsv = false; // 詳細CSV出力
    public bool enablePerRunJson = false;// 詳細JSON出力
    public bool writeToFile = true;      // ファイル保存
    public string outputFolder = "";     // 出力フォルダ（空=persistentDataPath）
    public string fileBaseName = "benchmark"; // ファイル名ベース
    public string fileNameTemplate;      // ファイル名テンプレート
    public bool createSubfolders = true; // サブフォルダ作成許可
    public int perRunFlushEvery = 0;     // ストリーム書き出し頻度
}
```

## 8. 実行フロー詳細

### 8.1 単一ベンチマーク実行フロー
1. `WatchUIUpdate.RunBenchmarkNow()` 呼び出し
2. `WalkOneStepScenario` インスタンス生成
3. 指定回数の反復実行
   - `Walking.RunOneWalkStepForBenchmark()` 呼び出し
   - メトリクス収集（IntroMetrics, WalkMetrics）
   - 結果の累積
4. 平均値計算
5. `LastBenchMetrics` への保存

### 8.2 プリセットスイープ実行フロー
1. `WatchUIUpdate.StartPresetSweep()` 呼び出し
2. `IntroPresetCollection` 読み込み
3. 各プリセットでループ
   - `IntroSettingsApplier.Apply()` で設定適用
   - `BenchmarkRunner.RunRepeatAsync()` で反復実行
   - 結果収集とフォーマット
4. CSV/JSON出力
5. TMP表示更新
6. ファイル保存

### 8.3 キャンセル処理
- `CancellationTokenSource` による中断制御
- キャンセル時は即座にズーム復元
- 部分結果の保存（設定による）

## 9. メトリクス収集詳細

### 9.1 リングバッファ実装
```csharp
// MetricsHub内部実装
private const int IntroBufferSize = 32;
private const int WalkBufferSize = 32;
private const int SpanBufferSize = 64;
private const int JitterBufferSize = 32;
```

### 9.2 イベントシステム
```csharp
public event Action<IntroMetricsEvent> OnIntro;
public event Action<WalkMetricsEvent> OnWalk;
public event Action<SpanMetricsEvent> OnSpan;
public event Action<JitterCompletedEvent> OnJitterCompleted;
```

### 9.3 統計計算
- 平均値：単純平均
- P95：95パーセンタイル（ソート後のインデックス計算）
- 最大値：観測された最大値

## 10. 最適化技術

### 10.1 Yield制御による負荷分散
```csharp
if (introYieldDuringPrepare && (i % introYieldEveryN == 0))
{
    await UniTask.Yield(PlayerLoopTiming.Update);
}
```

### 10.2 バッチアクティベーション
- 複数オブジェクトのSetActiveを一括処理
- レイアウト計算の最適化

### 10.3 メモリプール
- StringBuilder再利用
- リストのキャパシティ事前確保

### 10.4 条件コンパイル
```csharp
#if !METRICS_DISABLED
    // メトリクス処理
#else
    // スタブ実装
#endif
```

## 11. エラーハンドリング

### 11.1 例外処理
- try-catch-finallyによる確実なリソース解放
- エラーメッセージの記録
- フェイルセーフによる復元

### 11.2 検証処理
- null チェック
- 範囲チェック
- 状態検証

## 12. 拡張ポイント

### 12.1 カスタムシナリオ実装
`IBenchmarkScenario` インターフェースを実装することで、独自のベンチマークシナリオを追加可能。

### 12.2 カスタムフォーマッター
`IResultFormatter` インターフェースを実装することで、独自の出力形式を追加可能。

### 12.3 カスタムメトリクス
`MetricsHub` のイベントシステムを購読することで、独自のメトリクス処理を追加可能。

## 13. 使用例

### 13.1 基本的な使用方法
```csharp
// 単一ベンチマーク実行
await WatchUIUpdate.Instance.RunBenchmarkNow();

// プリセットスイープ実行
await WatchUIUpdate.Instance.RunPresetSweepBenchmark();

// キャンセル
WatchUIUpdate.Instance.CancelPresetSweep();
```

### 13.2 Unity Inspector設定
1. MetricsSettings SOを作成・割り当て
2. IntroPresetCollection SOを作成・設定
3. BenchmarkOutputSettings SOを作成・設定
4. PerformanceHUD コンポーネントを配置

## 14. パフォーマンス目標

### 14.1 推奨値
- 60FPS環境：フレーム時間 < 16.7ms
- 30FPS環境：フレーム時間 < 33.3ms
- モバイル環境：特別な考慮が必要

### 14.2 最適化指標
- Intro導入時間：100-200ms
- 敵配置時間：50ms以下
- GC割り当て：最小限に抑制

## まとめ
このベンチマークシステムは、Walk関数のパフォーマンスを包括的に計測し、最適化するための完全なソリューションを提供します。リアルタイム計測、自動ベンチマーク、詳細レポート生成により、パフォーマンスボトルネックの特定と改善が容易になります。