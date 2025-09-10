#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using Unity.Profiling;
using TMPro;
using UnityEngine.UI;
using System.Text;

/// <summary>
/// 画面オーバーレイで実機のパフォーマンスを可視化するHUD。
/// - FPS / CPUフレーム時間 / GPUフレーム時間 / フレームGC
/// - WatchUIUpdateの導入メトリクス（Planned/Actual/Delay/配置）
/// - WatchUIUpdateのProfilerMarker（Prepare/Play/PlaceEnemies）
/// - （ベンチ実行時）Walk合計やベンチ平均（A/W・Jit avg/p95/max）
/// </summary>
public partial class PerformanceHUD : MonoBehaviour
{
    [Header("パフォーマンスHUD  このgameObjectをfalseにすれば完全にゼロコスト\nまた、非developmentビルドではコンパイルすらされない。")]
    // ===== 定数（色／しきい値） =====
    private const string COLOR_RED    = "#FF6B6B";
    private const string COLOR_YELLOW = "#FFD95A";
    private const string COLOR_GREEN  = "#A5FF7A";

    private const float FPS60_THRESHOLD = 1000f / 60f; // 16.7ms
    private const float FPS30_THRESHOLD = 1000f / 30f; // 33.3ms
    private const float FPS24_THRESHOLD = 1000f / 24f; // 41.7ms

    [Header("HUD 出力先")]
    [Tooltip("表示先のTextMeshProUGUI。未指定なら起動時に簡易テキストを自動生成します。")]
    [SerializeField] private TextMeshProUGUI uiText;

    [Header("フレーム計測（表示の安定化）")]
    [Tooltip("FPSの移動平均に用いるサンプル長。大きいほど表示が安定しますが追従性は下がります。")]
    [SerializeField] private int avgSample = 60;

    [Header("GPU計測（対応環境のみ）")]
    [Tooltip("GPUフレーム時間を取得・表示します（未対応環境では0msになることがあります）。")]
    [SerializeField] private bool showGpuMs = true;

    [Header("HUD 出力（表示トグル）")]
    [SerializeField] private bool showFpsLine = true;
    [SerializeField] private bool showFrameLine = true;      // CPU/GPU/GC 行
    [SerializeField] private bool showContextLine = true;
    [SerializeField] private bool showProgressLine = true;   // 進捗/ETA 行
    [SerializeField] private bool showRunnerSpanLine = true; // Runner.Preset/RunOnce
    [SerializeField] private bool showIntroSpanLine = true;  // Intro.Prepare/Play/Enemies
    [SerializeField] private bool showMarkersLine = true;    // ProfilerMarkerの3値
    [SerializeField] private bool showIntroJitterLine = true;// Intro Jitter avg/p95/max
    [SerializeField] private bool showIntroSummaryLine = true; // Planned/Actual/Delay + (A/E)
    [SerializeField] private bool showWalkLine = true;       // Walk Total
    [SerializeField] private bool showBenchSummaryLine = true; // Bench Avg 行
#if METRICS_DISABLED
    [SerializeField] private bool showMetricsDisabledBanner = true; // METRICS_DISABLEDのときのバナー表示
#endif

    [Header("Span 統計（avg/p95/max の色分け表示）")]
    [SerializeField] private bool showRunnerSpanStatsLine = true; // Runner系の統計行
    [SerializeField] private bool showIntroSpanStatsLine = true;  // Intro系の統計行
    [SerializeField] private bool showPlaceSpanStatsLine = true;  // PlaceEnemies サブ統計行
    [Tooltip("統計に用いる直近サンプル数（リングバッファから該当名を最大N件採取）")]
    [SerializeField] private int spanStatsWindow = 20;
    public enum ThresholdPreset { FPS60, FPS30, FPS24, Custom }
    [Header("色分けしきい値（プリセット/カスタム）")]
    [SerializeField] private ThresholdPreset thresholdPreset = ThresholdPreset.FPS60;
    [Tooltip("Custom選択時に使用（yellow）閾値[ms]")]
    [SerializeField] private float spanWarnMs = 16.7f; // Custom時のみ使用
    [Tooltip("Custom選択時に使用（red）閾値[ms]")]
    [SerializeField] private float spanBadMs = 33.3f;  // Custom時のみ使用

    [Header("表示調整（省略など）")]
    [Tooltip("ContextのPresetSummary最大文字数（超過は'…'で省略）")]
    [SerializeField] private int presetSummaryMaxChars = 48;
    
    [Header("更新制御")]
    [Tooltip("HUDテキストの更新間隔（秒）。0なら毎フレーム更新。")]
    [SerializeField] private float updateIntervalSec = 0f;

    // 位置調整はUI上で直接RectTransformを編集してください（コード側では固定しません）

    // ProfilerRecorders（OnEnableで開始し、OnDisableでDispose）
    private ProfilerRecorder _gcRecorder;
    private ProfilerRecorder _prepRecorder;    // WUI.PrepareIntro
    private ProfilerRecorder _playRecorder;    // WUI.PlayIntro
    private ProfilerRecorder _placeRecorder;   // WUI.PlaceEnemies

    private float _fpsAcc;
    private int _fpsCount;
    private float _fpsMin = float.MaxValue;
    private float _fpsMax = 0f;
    private float _hudNextUpdateTime = 0f; // 更新間引きの次回更新時刻（unscaledTime基準）

    private void Awake()
    {
        TryAutoCreateText();
    }

    private void OnEnable()
    {
        // GC / カスタムマーカー収集開始
        _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        _prepRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "WUI.PrepareIntro");
        _playRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "WUI.PlayIntro");
        _placeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "WUI.PlaceEnemies");

        // TextMeshPro 設定の明示（Inspectorで既に設定済みでも安全側で上書き）
        if (uiText != null)
        {
            uiText.enableWordWrapping = false;
            uiText.overflowMode = TextOverflowModes.Overflow;
            uiText.isRightToLeftText = false;
            uiText.alignment = TextAlignmentOptions.TopLeft;
            uiText.enableAutoSizing = false;
        }
    }

    private void OnDisable()
    {
        _gcRecorder.Dispose();
        _prepRecorder.Dispose();
        _playRecorder.Dispose();
        _placeRecorder.Dispose();
    }

    private void Update()
    {
        // 更新間引き（必要な場合のみ）
        if (updateIntervalSec > 0f)
        {
            float now = Time.unscaledTime;
            if (now < _hudNextUpdateTime) return;
            _hudNextUpdateTime = now + updateIntervalSec;
        }

        // 収集（FPS/GPU/GC）
        float cpuMs = Time.unscaledDeltaTime * 1000f;
        var fpsMetrics = UpdateFpsMetrics();
        float gpuMs = fpsMetrics.GpuMs;
        float gcKB = fpsMetrics.GcKB;

        // カスタムマーカー（ns -> ms）
        var markersTuple = GetProfilerMarkers();
        float prepMs = markersTuple.prepMs;
        float playMs = markersTuple.playMs;
        float placeMs = markersTuple.placeMs;

        // 導入メトリクス（MetricsHub優先 → Fallback to WUI）
        var wui = WatchUIUpdate.Instance;
        double planned = 0, actual = 0, delay = 0, enemyMs = 0;
        int allyCount = 0, enemyCount = 0;
        string when = "-";
        double introAvg = 0, introP95 = 0, introMax = 0; // アニメ滑らかさ
        var introData = GetIntroMetrics();
        planned   = introData.PlannedMs;
        actual    = introData.ActualMs;
        delay     = introData.DelayMs;
        enemyMs   = introData.EnemyPlacementMs;
        allyCount = introData.AllyCount;
        enemyCount= introData.EnemyCount;
        when      = introData.Timestamp;
        introAvg  = introData.AvgMs;
        introP95  = introData.P95Ms;
        introMax  = introData.MaxMs;

        // Walk 全体計測（MetricsHub優先 → Fallback to WUI）
        var walkData = GetWalkMetrics();
        double walkTotal = walkData.TotalMs;
        string whenWalk = walkData.Timestamp;

        // ベンチマーク平均（指定回数の実処理を実行後に集計）
        double bAvgA = 0, bAvgW = 0, bAvgJitAvg = 0, bAvgJitP95 = 0, bAvgJitMax = 0;
        int bReq = 0, bOk = 0, bNg = 0;
        string bWhen = "-";
        float bInter = 0f;
        if (wui != null && wui.LastBenchMetrics != null)
        {
            var bm = wui.LastBenchMetrics;
            bReq = bm.Requested;
            bOk  = bm.SuccessCount;
            bNg  = bm.FailCount;
            bAvgA = bm.AvgActualMs;
            bAvgW = bm.AvgWalkTotalMs;
            bAvgJitAvg = bm.AvgJitAvgMs;
            bAvgJitP95 = bm.AvgJitP95Ms;
            bAvgJitMax = bm.AvgJitMaxMs;
            bWhen = bm.Timestamp.ToString("HH:mm:ss");
            bInter = bm.InterDelaySec;
        }

        if (uiText != null)
        {
            // MetricsContext 表示（優先度: 最新Intro -> 最新Walk -> BenchmarkContext.Current）
            var ctx = GetContextData();
            string ctxScenario = ctx.ScenarioName;
            string ctxPreset   = ctx.PresetSummary;
            int ctxPi = ctx.PresetIndex;
            int ctxRi = ctx.RunIndex;

            // Intro 1行目が枠幅を超える場合は「Delay」の直前で強制改行
            string introLeft = $"Intro Planned:{planned:F2} ms | Actual:{actual:F2} ms";
            string introDelayWithSep = $" | Delay:{delay:F2} ms";

            float rectW = uiText.rectTransform.rect.width;
            if (rectW <= 1f) rectW = 512f; // レイアウト未確定時のフォールバック
            float fullW = uiText.GetPreferredValues(introLeft + introDelayWithSep).x;

            string intro1 = (fullW > rectW)
                ? (introLeft + "\n" + $"Delay:{delay:F2} ms")
                : (introLeft + introDelayWithSep);

            string intro2 = $"(A:{allyCount} E:{enemyCount}) @{when}";

            // 進捗（プリセット x ラン）とETA
            string progressLine = BuildProgressLine();

            // Runnerスパン（最新値）取得
            var lastPresetSpan = global::MetricsHub.Instance?.LatestSpan("Runner.Preset");
            var lastRunOnceSpan = global::MetricsHub.Instance?.LatestSpan("Runner.RunOnce");
            var lastLoopSpan   = global::MetricsHub.Instance?.LatestSpan("Runner.Loop");
            var lastTotalSpan  = global::MetricsHub.Instance?.LatestSpan("Runner.Total");
            string spanRunnerLine =
                $"Runner Span Preset:{(lastPresetSpan!=null ? lastPresetSpan.DurationMs.ToString("F2") : "-")} ms | " +
                $"RunOnce:{(lastRunOnceSpan!=null ? lastRunOnceSpan.DurationMs.ToString("F2") : "-")} ms | " +
                $"Loop:{(lastLoopSpan!=null ? lastLoopSpan.DurationMs.ToString("F2") : "-")} ms | " +
                $"Total:{(lastTotalSpan!=null ? lastTotalSpan.DurationMs.ToString("F2") : "-")} ms";

            // Intro系スパン（最新値）取得
            var lastIntroPrep  = global::MetricsHub.Instance?.LatestSpan("Intro.Prepare");
            var lastIntroPlay  = global::MetricsHub.Instance?.LatestSpan("Intro.Play");
            var lastPlace      = global::MetricsHub.Instance?.LatestSpan("PlaceEnemies");
            string spanIntroLine =
                $"Intro Span Prepare:{(lastIntroPrep!=null ? lastIntroPrep.DurationMs.ToString("F2") : "-")} ms | " +
                $"Play:{(lastIntroPlay!=null ? lastIntroPlay.DurationMs.ToString("F2") : "-")} ms | " +
                $"Enemies:{(lastPlace!=null ? lastPlace.DurationMs.ToString("F2") : "-")} ms";

            // PlaceEnemies サブスパン（最新値）
            var lastSpawn  = global::MetricsHub.Instance?.LatestSpan("PlaceEnemies.Spawn");
            var lastLayout = global::MetricsHub.Instance?.LatestSpan("PlaceEnemies.Layout");
            var lastActive = global::MetricsHub.Instance?.LatestSpan("PlaceEnemies.Activate");
            string spanPlaceLine =
                $"Place Span Spawn:{(lastSpawn!=null ? lastSpawn.DurationMs.ToString("F2") : "-")} ms | " +
                $"Layout:{(lastLayout!=null ? lastLayout.DurationMs.ToString("F2") : "-")} ms | " +
                $"Activate:{(lastActive!=null ? lastActive.DurationMs.ToString("F2") : "-")} ms";

            // 有効なしきい値を決定（プリセット優先）
            ResolveThresholds(out float effWarn, out float effBad);

            var stPreset = global::MetricsHub.Instance.GetSpanStats("Runner.Preset", spanStatsWindow);
            var stRun    = global::MetricsHub.Instance.GetSpanStats("Runner.RunOnce", spanStatsWindow);
            var stLoop   = global::MetricsHub.Instance.GetSpanStats("Runner.Loop", spanStatsWindow);
            var stTotal  = global::MetricsHub.Instance.GetSpanStats("Runner.Total", spanStatsWindow);
            string spanRunnerStatsLine = $"Runner Stats {FormatSpanStats("Preset", stPreset, effWarn, effBad)} | {FormatSpanStats("RunOnce", stRun, effWarn, effBad)} | {FormatSpanStats("Loop", stLoop, effWarn, effBad)} | {FormatSpanStats("Total", stTotal, effWarn, effBad)}";

            var stPrep   = global::MetricsHub.Instance.GetSpanStats("Intro.Prepare", spanStatsWindow);
            var stPlay   = global::MetricsHub.Instance.GetSpanStats("Intro.Play", spanStatsWindow);
            var stEnemy  = global::MetricsHub.Instance.GetSpanStats("PlaceEnemies", spanStatsWindow);
            string spanIntroStatsLine  = $"Intro Stats {FormatSpanStats("Prepare", stPrep, effWarn, effBad)} | {FormatSpanStats("Play", stPlay, effWarn, effBad)} | {FormatSpanStats("Enemies", stEnemy, effWarn, effBad)}";

            var stSpawn  = global::MetricsHub.Instance.GetSpanStats("PlaceEnemies.Spawn", spanStatsWindow);
            var stLayout = global::MetricsHub.Instance.GetSpanStats("PlaceEnemies.Layout", spanStatsWindow);
            var stActive = global::MetricsHub.Instance.GetSpanStats("PlaceEnemies.Activate", spanStatsWindow);
            string spanPlaceStatsLine = $"Place Stats {FormatSpanStats("Spawn", stSpawn, effWarn, effBad)} | {FormatSpanStats("Layout", stLayout, effWarn, effBad)} | {FormatSpanStats("Activate", stActive, effWarn, effBad)}";

            // 1行ごとの責務を明確化（読みやすさ優先）
            string fpsLine = $"FPS avg:{fpsMetrics.Average:F1} (min:{fpsMetrics.Min:F1} max:{fpsMetrics.Max:F1})";
            string frameLine = $"CPU frame:{cpuMs:F2} ms | GPU:{gpuMs:F2} ms | GC/frame:{gcKB:F1} KB";
            string contextLine = $"Context: {ctxScenario} | Preset:{TruncateString(ctxPreset, presetSummaryMaxChars)} (idx:{ctxPi}) | Run:{ctxRi}";
            string markersLine = $"Marker Prepare:{prepMs:F2} ms | Play:{playMs:F2} ms | Enemies:{placeMs:F2} ms";
            string introJitterLine = $"Intro Jit:{introAvg:F1}/{introP95:F1}/{introMax:F1} ms (avg/p95/max)";
            string introSummaryBlock = intro1 + "\n" + intro2; // 2行構成
            string walkLineStr = $"Walk Total:{walkTotal:F2} ms @{whenWalk}";
            string benchLine = (bReq > 0)
                ? $"Bench Avg A/W:{bAvgA:F1}/{bAvgW:F1} ms | Jit:{bAvgJitAvg:F1}/{bAvgJitP95:F1}/{bAvgJitMax:F1} x{bOk}/{bReq-bNg}+{bNg} @{bWhen} (dly {bInter:F3}s)"
                : string.Empty;

            // Metrics Disabled バナー
            string metricsBanner = string.Empty;
#if METRICS_DISABLED
            metricsBanner = showMetricsDisabledBanner ? "[METRICS DISABLED]\n" : string.Empty;
#endif

            // 表示順を微調整：Progressを先頭へ（StringBuilderで構築）
            uiText.text = BuildHudText(
                metricsBanner,
                progressLine,
                fpsLine,
                frameLine,
                contextLine,
                spanRunnerLine,
                spanRunnerStatsLine,
                spanIntroLine,
                spanPlaceLine,
                spanIntroStatsLine,
                spanPlaceStatsLine,
                markersLine,
                introJitterLine,
                introSummaryBlock,
                walkLineStr,
                benchLine);
        }
        // 位置・アンカーはユーザーがRectTransformで調整する前提（ここでは触らない）
    }

    // ===== Helper Methods (extracted) =====
    private string BuildProgressLine()
    {
        var wui = WatchUIUpdate.Instance;
        var wuiProg = wui?.LastSweepProgress;
        if (wuiProg == null || wuiProg.TotalRuns <= 0) return string.Empty;
        int pi = Mathf.Clamp(wuiProg.PresetIndex, 0, Mathf.Max(0, wuiProg.PresetCount - 1));
        int pc = Mathf.Max(1, wuiProg.PresetCount);
        int ri = Mathf.Clamp(wuiProg.RunIndex, 0, wuiProg.RunCount);
        int rc = Mathf.Max(1, wuiProg.RunCount);
        int done = Mathf.Clamp(wuiProg.CompletedRuns, 0, wuiProg.TotalRuns);
        int totalAll = Mathf.Max(1, wuiProg.TotalRuns);
        double eta = System.Math.Max(0.0, wuiProg.ETASec);
        int etaMin = (int)(eta / 60.0);
        int etaSec = (int)(eta % 60.0);
        return $"Progress: Preset {pi+1}/{pc} | Run {ri}/{rc} | Done {done}/{totalAll} | ETA {etaMin:00}:{etaSec:00}";
    }

    private void ResolveThresholds(out float warn, out float bad)
    {
        switch (thresholdPreset)
        {
            case ThresholdPreset.FPS60: warn = FPS60_THRESHOLD; bad = FPS60_THRESHOLD * 2f; break;
            case ThresholdPreset.FPS30: warn = FPS30_THRESHOLD; bad = FPS30_THRESHOLD * 2f; break;
            case ThresholdPreset.FPS24: warn = FPS24_THRESHOLD; bad = FPS24_THRESHOLD * 2f; break;
            default: warn = spanWarnMs; bad = spanBadMs; break;
        }
    }

    private string ColorizeMs(double v, float warnThreshold, float badThreshold)
    {
        string val = v.ToString("F2");
        if (v >= badThreshold) return $"<color={COLOR_RED}>{val}</color>";
        if (v >= warnThreshold) return $"<color={COLOR_YELLOW}>{val}</color>";
        return $"<color={COLOR_GREEN}>{val}</color>";
    }

    private string FormatSpanStats(string label, global::MetricsHub.SpanStats s, float warn, float bad)
    {
        if (s.Count <= 0) return $"{label}:-";
        return $"{label}:{ColorizeMs(s.AvgMs, warn, bad)}/{ColorizeMs(s.P95Ms, warn, bad)}/{ColorizeMs(s.MaxMs, warn, bad)} ms";
    }

    private string TruncateString(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || max <= 3) return s ?? "-";
        return (s.Length <= max) ? s : (s.Substring(0, max - 1) + "…");
    }

    private static float ToMs(ProfilerRecorder r)
    {
        if (!r.Valid) return 0f;
        // ProfilerRecorder は nanoseconds を返す
        return (float)(r.LastValue / 1_000_000.0);
    }

    private void TryAutoCreateText()
    {
        if (uiText != null) return;
        // 既存Canvasにアタッチ、なければ簡易Canvasを作る
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("PerformanceHUD_Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
        }
        var textGO = new GameObject("PerformanceHUD_Text");
        textGO.transform.SetParent(canvas.transform, false);
        uiText = textGO.AddComponent<TextMeshProUGUI>();
        uiText.fontSize = 20f;
        uiText.color = new Color(1f, 1f, 1f, 0.9f);
        uiText.enableWordWrapping = false;
        uiText.overflowMode = TextOverflowModes.Overflow;
        uiText.isRightToLeftText = false;
        uiText.alignment = TextAlignmentOptions.TopLeft;
        uiText.raycastTarget = false;
        uiText.text = "HUD";
    }

    private string BuildHudText(
        string metricsBanner,
        string progressLine,
        string fpsLine,
        string frameLine,
        string contextLine,
        string spanRunnerLine,
        string spanRunnerStatsLine,
        string spanIntroLine,
        string spanPlaceLine,
        string spanIntroStatsLine,
        string spanPlaceStatsLine,
        string markersLine,
        string introJitterLine,
        string introSummaryBlock,
        string walkLineStr,
        string benchLine)
    {
        var sb = new StringBuilder(2048);
        sb.Append(metricsBanner);
        if (showProgressLine && !string.IsNullOrEmpty(progressLine)) sb.AppendLine(progressLine);
        if (showFpsLine) sb.AppendLine(fpsLine);
        if (showFrameLine) sb.AppendLine(frameLine);
        if (showContextLine) sb.AppendLine(contextLine);
        if (showRunnerSpanLine) sb.AppendLine(spanRunnerLine);
        if (showRunnerSpanStatsLine) sb.AppendLine(spanRunnerStatsLine);
        if (showIntroSpanLine) sb.AppendLine(spanIntroLine);
        if (showPlaceSpanStatsLine) sb.AppendLine(spanPlaceLine);
        if (showIntroSpanStatsLine) sb.AppendLine(spanIntroStatsLine);
        if (showPlaceSpanStatsLine) sb.AppendLine(spanPlaceStatsLine);
        if (showMarkersLine) sb.AppendLine(markersLine);
        if (showIntroJitterLine) sb.AppendLine(introJitterLine);
        if (showIntroSummaryLine) sb.AppendLine(introSummaryBlock);
        if (showWalkLine) sb.AppendLine(walkLineStr);
        if (showBenchSummaryLine && !string.IsNullOrEmpty(benchLine)) sb.Append(benchLine);
        return sb.ToString();
    }

    // ===== Data structures (for refactor) =====
    private struct FpsMetrics
    {
        public float Current;
        public float Average;
        public float Min;
        public float Max;
        public float GpuMs;
        public float GcKB;
    }

    private struct IntroMetricsData
    {
        public double PlannedMs;
        public double ActualMs;
        public double DelayMs;
        public double EnemyPlacementMs;
        public int AllyCount;
        public int EnemyCount;
        public string Timestamp;
        public double AvgMs;
        public double P95Ms;
        public double MaxMs;
    }

    private struct WalkMetricsData
    {
        public double TotalMs;
        public string Timestamp;
    }

    private struct ContextData
    {
        public string ScenarioName;
        public string PresetSummary;
        public int PresetIndex;
        public int RunIndex;
    }

    // ===== Collectors (for future use in Update()) =====
    private FpsMetrics UpdateFpsMetrics()
    {
        FpsMetrics m = new FpsMetrics();
        float dt = Time.unscaledDeltaTime;
        float fps = 1f / Mathf.Max(1e-6f, dt);
        _fpsAcc += fps;
        _fpsCount++;
        _fpsMin = Mathf.Min(_fpsMin, fps);
        _fpsMax = Mathf.Max(_fpsMax, fps);
        if (_fpsCount > Mathf.Max(1, avgSample))
        {
            _fpsAcc = fps;
            _fpsCount = 1;
            _fpsMin = fps;
            _fpsMax = fps;
        }
        m.Current = fps;
        m.Average = _fpsAcc / Mathf.Max(1, _fpsCount);
        m.Min = _fpsMin;
        m.Max = _fpsMax;
        // GPU
        if (showGpuMs)
        {
            UnityEngine.FrameTimingManager.CaptureFrameTimings();
            var frames = new UnityEngine.FrameTiming[1];
            if (UnityEngine.FrameTimingManager.GetLatestTimings(1, frames) > 0)
            {
                m.GpuMs = (float)frames[0].gpuFrameTime;
            }
        }
        // GC KB
        m.GcKB = _gcRecorder.Valid ? _gcRecorder.LastValue / 1024f : 0f;
        return m;
    }

    private IntroMetricsData GetIntroMetrics()
    {
        var data = new IntroMetricsData();
        var wui = WatchUIUpdate.Instance;
        var latestIntro = global::MetricsHub.Instance?.LatestIntro;
        if (latestIntro != null)
        {
            data.PlannedMs = latestIntro.PlannedMs;
            data.ActualMs = latestIntro.ActualMs;
            data.DelayMs = latestIntro.DelayMs;
            data.EnemyPlacementMs = latestIntro.EnemyPlacementMs;
            data.AllyCount = latestIntro.AllyCount;
            data.EnemyCount = latestIntro.EnemyCount;
            data.Timestamp = latestIntro.Timestamp.ToString("HH:mm:ss");
            data.AvgMs = latestIntro.IntroFrameAvgMs;
            data.P95Ms = latestIntro.IntroFrameP95Ms;
            data.MaxMs = latestIntro.IntroFrameMaxMs;
        }
        else if (wui != null && wui.LastIntroMetrics != null)
        {
            var m = wui.LastIntroMetrics;
            data.PlannedMs = m.PlannedMs;
            data.ActualMs = m.ActualMs;
            data.DelayMs = m.DelayMs;
            data.EnemyPlacementMs = m.EnemyPlacementMs;
            data.AllyCount = m.AllyCount;
            data.EnemyCount = m.EnemyCount;
            data.Timestamp = m.Timestamp.ToString("HH:mm:ss");
            data.AvgMs = m.IntroFrameAvgMs;
            data.P95Ms = m.IntroFrameP95Ms;
            data.MaxMs = m.IntroFrameMaxMs;
        }
        else
        {
            data.Timestamp = "-";
        }
        return data;
    }

    private WalkMetricsData GetWalkMetrics()
    {
        var data = new WalkMetricsData();
        var wui = WatchUIUpdate.Instance;
        var latestWalk = global::MetricsHub.Instance?.LatestWalk;
        if (latestWalk != null)
        {
            data.TotalMs = latestWalk.TotalMs;
            data.Timestamp = latestWalk.Timestamp.ToString("HH:mm:ss");
        }
        else if (wui != null && wui.LastWalkMetrics != null)
        {
            data.TotalMs = wui.LastWalkMetrics.TotalMs;
            data.Timestamp = wui.LastWalkMetrics.Timestamp.ToString("HH:mm:ss");
        }
        else
        {
            data.Timestamp = "-";
        }
        return data;
    }

    private ContextData GetContextData()
    {
        var data = new ContextData();
        var latestIntro = global::MetricsHub.Instance?.LatestIntro;
        var latestWalk  = global::MetricsHub.Instance?.LatestWalk;
        var ctxSrc = latestIntro?.Context
                    ?? latestWalk?.Context
                    ?? global::BenchmarkContext.Current;
        if (ctxSrc != null)
        {
            data.ScenarioName = string.IsNullOrEmpty(ctxSrc.ScenarioName) ? "-" : ctxSrc.ScenarioName;
            data.PresetSummary = string.IsNullOrEmpty(ctxSrc.PresetSummary) ? "-" : ctxSrc.PresetSummary;
            data.PresetIndex = ctxSrc.PresetIndex;
            data.RunIndex = ctxSrc.RunIndex;
        }
        else
        {
            data.ScenarioName = "-";
            data.PresetSummary = "-";
            data.PresetIndex = -1;
            data.RunIndex = -1;
        }
        return data;
    }

    private (float prepMs, float playMs, float placeMs) GetProfilerMarkers()
    {
        return (
            ToMs(_prepRecorder),
            ToMs(_playRecorder),
            ToMs(_placeRecorder)
        );
    }
}
#endif
