/// <summary>
/// ベンチマークシステム全体で使用する定数を一元管理
/// </summary>
public static class BenchmarkConstants
{
    #region Buffer Sizes
    
    /// <summary>
    /// Introメトリクスのリングバッファサイズ
    /// </summary>
    public const int INTRO_BUFFER_SIZE = 32;
    
    /// <summary>
    /// Walkメトリクスのリングバッファサイズ
    /// </summary>
    public const int WALK_BUFFER_SIZE = 32;
    
    /// <summary>
    /// Spanメトリクスのリングバッファサイズ
    /// </summary>
    public const int SPAN_BUFFER_SIZE = 64;
    
    /// <summary>
    /// Jitterメトリクスのリングバッファサイズ
    /// </summary>
    public const int JITTER_BUFFER_SIZE = 32;
    
    #endregion
    
    #region Display Settings
    
    /// <summary>
    /// プリセットログの最大行数
    /// </summary>
    public const int PRESET_LOG_MAX_LINES = 400;
    
    /// <summary>
    /// プリセットサマリの最大文字数
    /// </summary>
    public const int PRESET_SUMMARY_MAX_CHARS = 48;
    
    /// <summary>
    /// Span統計のデフォルトウィンドウサイズ
    /// </summary>
    public const int SPAN_STATS_WINDOW_SIZE = 20;
    
    #endregion
    
    #region Performance Thresholds
    
    /// <summary>
    /// 60FPS環境のフレーム時間しきい値（ミリ秒）
    /// </summary>
    public const float FPS_60_THRESHOLD_MS = 16.7f;
    
    /// <summary>
    /// 30FPS環境のフレーム時間しきい値（ミリ秒）
    /// </summary>
    public const float FPS_30_THRESHOLD_MS = 33.3f;
    
    /// <summary>
    /// 24FPS環境のフレーム時間しきい値（ミリ秒）
    /// </summary>
    public const float FPS_24_THRESHOLD_MS = 41.7f;
    
    #endregion
    
    #region Colors
    
    /// <summary>
    /// パフォーマンス良好時の色（緑）
    /// </summary>
    public const string COLOR_GOOD = "#A5FF7A";
    
    /// <summary>
    /// パフォーマンス警告時の色（黄）
    /// </summary>
    public const string COLOR_WARNING = "#FFD95A";
    
    /// <summary>
    /// パフォーマンス問題時の色（赤）
    /// </summary>
    public const string COLOR_BAD = "#FF6B6B";
    
    #endregion
    
    #region File Settings
    
    /// <summary>
    /// デフォルトのベンチマークファイル名ベース
    /// </summary>
    public const string DEFAULT_BENCHMARK_FILE_BASE = "benchmark";
    
    /// <summary>
    /// Per-Runファイルのサフィックス
    /// </summary>
    public const string PER_RUN_FILE_SUFFIX = "_runs";
    
    /// <summary>
    /// タイムスタンプフォーマット（ファイル名用）
    /// </summary>
    public const string TIMESTAMP_FORMAT_FILE = "yyyyMMdd_HHmmss";
    
    /// <summary>
    /// タイムスタンプフォーマット（表示用）
    /// </summary>
    public const string TIMESTAMP_FORMAT_DISPLAY = "yyyy-MM-dd HH:mm:ss";
    
    /// <summary>
    /// タイムスタンプフォーマット（短縮表示用）
    /// </summary>
    public const string TIMESTAMP_FORMAT_SHORT = "HH:mm:ss";
    
    #endregion
    
    #region Default Values
    
    /// <summary>
    /// デフォルトのベンチマーク繰り返し回数
    /// </summary>
    public const int DEFAULT_BENCHMARK_REPEAT = 100;
    
    /// <summary>
    /// デフォルトの実行間待機時間（秒）
    /// </summary>
    public const float DEFAULT_INTER_RUN_DELAY_SEC = 0.0f;
    
    /// <summary>
    /// FPSサンプリングのデフォルトサンプル数
    /// </summary>
    public const int DEFAULT_FPS_SAMPLE_COUNT = 60;
    
    /// <summary>
    /// Jitterサンプリングの初期容量
    /// </summary>
    public const int JITTER_SAMPLE_INITIAL_CAPACITY = 256;
    
    #endregion
    
    #region Profiler Markers
    
    /// <summary>
    /// ProfilerMarker: 導入準備
    /// </summary>
    public const string PROFILER_MARKER_PREPARE_INTRO = "WUI.PrepareIntro";
    
    /// <summary>
    /// ProfilerMarker: 導入再生
    /// </summary>
    public const string PROFILER_MARKER_PLAY_INTRO = "WUI.PlayIntro";
    
    /// <summary>
    /// ProfilerMarker: 敵配置
    /// </summary>
    public const string PROFILER_MARKER_PLACE_ENEMIES = "WUI.PlaceEnemies";
    
    #endregion
    
    #region Span Names
    
    /// <summary>
    /// Span: ベンチマーク全体
    /// </summary>
    public const string SPAN_RUNNER_TOTAL = "Runner.Total";
    
    /// <summary>
    /// Span: プリセット実行
    /// </summary>
    public const string SPAN_RUNNER_PRESET = "Runner.Preset";
    
    /// <summary>
    /// Span: 単一実行
    /// </summary>
    public const string SPAN_RUNNER_RUN_ONCE = "Runner.RunOnce";
    
    /// <summary>
    /// Span: ループ処理
    /// </summary>
    public const string SPAN_RUNNER_LOOP = "Runner.Loop";
    
    /// <summary>
    /// Span: 導入準備
    /// </summary>
    public const string SPAN_INTRO_PREPARE = "Intro.Prepare";
    
    /// <summary>
    /// Span: 導入再生
    /// </summary>
    public const string SPAN_INTRO_PLAY = "Intro.Play";
    
    /// <summary>
    /// Span: 敵配置
    /// </summary>
    public const string SPAN_PLACE_ENEMIES = "PlaceEnemies";
    
    /// <summary>
    /// Span: 敵生成
    /// </summary>
    public const string SPAN_PLACE_ENEMIES_SPAWN = "PlaceEnemies.Spawn";
    
    /// <summary>
    /// Span: レイアウト
    /// </summary>
    public const string SPAN_PLACE_ENEMIES_LAYOUT = "PlaceEnemies.Layout";
    
    /// <summary>
    /// Span: アクティベート
    /// </summary>
    public const string SPAN_PLACE_ENEMIES_ACTIVATE = "PlaceEnemies.Activate";
    
    #endregion
}