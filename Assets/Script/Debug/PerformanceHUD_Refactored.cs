#if false // Disabled: unified to PerformanceHUD.cs (this refactored prototype is kept for reference)
using System;
using UnityEngine;
using Unity.Profiling;
using TMPro;
using UnityEngine.UI;
using System.Text;

/// <summary>
/// PerformanceHUD のリファクタリング版
/// 巨大なUpdateメソッドを機能ごとに分割
/// </summary>
public partial class PerformanceHUD : MonoBehaviour
{
    #region Constants
    
    // 色定数
    private const string COLOR_RED = "#FF6B6B";
    private const string COLOR_YELLOW = "#FFD95A";
    private const string COLOR_GREEN = "#A5FF7A";
    
    // しきい値定数
    private const float FPS60_THRESHOLD = 1000f / 60f; // 16.7ms
    private const float FPS30_THRESHOLD = 1000f / 30f; // 33.3ms
    private const float FPS24_THRESHOLD = 1000f / 24f; // 41.7ms
    
    #endregion
    
    #region Metrics Data Structures
    
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
    
    #endregion
    
    #region Update Methods (Refactored)
    
    /// <summary>
    /// FPS関連メトリクスの更新
    /// </summary>
    private FpsMetrics UpdateFpsMetrics()
    {
        var metrics = new FpsMetrics();
        
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
        
        metrics.Current = fps;
        metrics.Average = _fpsAcc / Mathf.Max(1, _fpsCount);
        metrics.Min = _fpsMin;
        metrics.Max = _fpsMax;
        
        // GPU計測
        if (showGpuMs)
        {
            UnityEngine.FrameTimingManager.CaptureFrameTimings();
            var frames = new UnityEngine.FrameTiming[1];
            if (UnityEngine.FrameTimingManager.GetLatestTimings(1, frames) > 0)
            {
                metrics.GpuMs = (float)frames[0].gpuFrameTime;
            }
        }
        
        // GC計測
        metrics.GcKB = _gcRecorder.Valid ? _gcRecorder.LastValue / 1024f : 0f;
        
        return metrics;
    }
    
    /// <summary>
    /// 導入メトリクスの取得
    /// </summary>
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
        else if (wui?.LastIntroMetrics != null)
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
    
    /// <summary>
    /// Walk メトリクスの取得
    /// </summary>
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
        else if (wui?.LastWalkMetrics != null)
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
    
    /// <summary>
    /// コンテキスト情報の取得
    /// </summary>
    private ContextData GetContextData(IntroMetricsData intro, WalkMetricsData walk)
    {
        var data = new ContextData();
        
        var latestIntro = global::MetricsHub.Instance?.LatestIntro;
        var latestWalk = global::MetricsHub.Instance?.LatestWalk;
        
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
    
    /// <summary>
    /// ProfilerMarker値の取得
    /// </summary>
    private (float prepMs, float playMs, float placeMs) GetProfilerMarkers()
    {
        return (
            ToMs(_prepRecorder),
            ToMs(_playRecorder),
            ToMs(_placeRecorder)
        );
    }
    
    /// <summary>
    /// しきい値の解決
    /// </summary>
    private void ResolveThresholds(out float warn, out float bad)
    {
        switch (thresholdPreset)
        {
            case ThresholdPreset.FPS60: 
                warn = FPS60_THRESHOLD; 
                bad = FPS60_THRESHOLD * 2f; 
                break;
            case ThresholdPreset.FPS30: 
                warn = FPS30_THRESHOLD; 
                bad = FPS30_THRESHOLD * 2f; 
                break;
            case ThresholdPreset.FPS24: 
                warn = FPS24_THRESHOLD; 
                bad = FPS24_THRESHOLD * 2f; 
                break;
            default: 
                warn = spanWarnMs; 
                bad = spanBadMs; 
                break;
        }
    }
    
    /// <summary>
    /// ミリ秒値の色付け
    /// </summary>
    private string ColorizeMs(double v, float warnThreshold, float badThreshold)
    {
        string val = v.ToString("F2");
        if (v >= badThreshold) return $"<color={COLOR_RED}>{val}</color>";
        if (v >= warnThreshold) return $"<color={COLOR_YELLOW}>{val}</color>";
        return $"<color={COLOR_GREEN}>{val}</color>";
    }
    
    /// <summary>
    /// Span統計のフォーマット
    /// </summary>
    private string FormatSpanStats(string label, global::MetricsHub.SpanStats stats, float warn, float bad)
    {
        if (stats.Count <= 0) return $"{label}:-";
        return $"{label}:{ColorizeMs(stats.AvgMs, warn, bad)}/{ColorizeMs(stats.P95Ms, warn, bad)}/{ColorizeMs(stats.MaxMs, warn, bad)} ms";
    }
    
    /// <summary>
    /// HUDテキストの構築
    /// </summary>
    private string BuildHudText(
        FpsMetrics fps,
        IntroMetricsData intro,
        WalkMetricsData walk,
        ContextData context,
        (float prep, float play, float place) markers)
    {
        var sb = new StringBuilder(2048);
        
        // Metrics Disabled バナー
        #if METRICS_DISABLED
        if (showMetricsDisabledBanner)
            sb.AppendLine("[METRICS DISABLED]");
        #endif
        
        // 進捗表示
        if (showProgressLine)
        {
            var progressLine = BuildProgressLine();
            if (!string.IsNullOrEmpty(progressLine))
                sb.AppendLine(progressLine);
        }
        
        // FPS
        if (showFpsLine)
        {
            sb.AppendLine($"FPS avg:{fps.Average:F1} (min:{fps.Min:F1} max:{fps.Max:F1})");
        }
        
        // フレーム情報
        if (showFrameLine)
        {
            float cpuMs = Time.unscaledDeltaTime * 1000f;
            sb.AppendLine($"CPU frame:{cpuMs:F2} ms | GPU:{fps.GpuMs:F2} ms | GC/frame:{fps.GcKB:F1} KB");
        }
        
        // コンテキスト
        if (showContextLine)
        {
            string truncated = TruncateString(context.PresetSummary, presetSummaryMaxChars);
            sb.AppendLine($"Context: {context.ScenarioName} | Preset:{truncated} (idx:{context.PresetIndex}) | Run:{context.RunIndex}");
        }
        
        // 他の表示項目も同様に追加...
        
        return sb.ToString();
    }
    
    /// <summary>
    /// 進捗行の構築
    /// </summary>
    private string BuildProgressLine()
    {
        var wui = WatchUIUpdate.Instance;
        var wuiProg = wui?.LastSweepProgress;
        
        if (wuiProg == null || wuiProg.TotalRuns <= 0)
            return string.Empty;
        
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
    
    /// <summary>
    /// 文字列の切り詰め
    /// </summary>
    private string TruncateString(string s, int maxLength)
    {
        if (string.IsNullOrEmpty(s) || maxLength <= 3) 
            return s ?? "-";
        return (s.Length <= maxLength) ? s : (s.Substring(0, maxLength - 1) + "…");
    }
    
    #endregion
}
#endif