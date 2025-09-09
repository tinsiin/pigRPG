#if !METRICS_DISABLED
using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// MetricsHub のリファクタリング版
/// RingBufferを使用して重複コードを削減
/// </summary>
public sealed class MetricsHubRefactored
{
    private static readonly MetricsHubRefactored _instance = new MetricsHubRefactored();
    public static MetricsHubRefactored Instance => _instance;

    // Runtime toggles
    public static bool Enabled = true;
    public static bool EnableSpan = true;
    public static bool EnableJitter = true;

    // Events
    public event Action<IntroMetricsEvent> OnIntro;
    public event Action<WalkMetricsEvent> OnWalk;
    public event Action<SpanMetricsEvent> OnSpan;
    public event Action<JitterCompletedEvent> OnJitterCompleted;

    // Latest snapshots
    private readonly Dictionary<string, SpanMetricsEvent> _latestSpanByName = new Dictionary<string, SpanMetricsEvent>(8);
    private readonly Dictionary<string, JitterCompletedEvent> _latestJitterByName = new Dictionary<string, JitterCompletedEvent>(8);

    // Ring buffers (統一実装)
    private const int IntroBufferSize = 32;
    private const int WalkBufferSize = 32;
    private const int SpanBufferSize = 64;
    private const int JitterBufferSize = 32;
    
    private readonly RingBuffer<IntroMetricsEvent> _introBuf = new RingBuffer<IntroMetricsEvent>(IntroBufferSize);
    private readonly RingBuffer<WalkMetricsEvent> _walkBuf = new RingBuffer<WalkMetricsEvent>(WalkBufferSize);
    private readonly RingBuffer<SpanMetricsEvent> _spanBuf = new RingBuffer<SpanMetricsEvent>(SpanBufferSize);
    private readonly RingBuffer<JitterCompletedEvent> _jitBuf = new RingBuffer<JitterCompletedEvent>(JitterBufferSize);

    private MetricsHubRefactored() { }

    public IntroMetricsEvent LatestIntro => _introBuf.GetLatest();
    public WalkMetricsEvent LatestWalk => _walkBuf.GetLatest();
    
    public SpanMetricsEvent LatestSpan(string name)
        => (name != null && _latestSpanByName.TryGetValue(name, out var ev)) ? ev : null;
    
    public JitterCompletedEvent LatestJitter(string name)
        => (name != null && _latestJitterByName.TryGetValue(name, out var ev)) ? ev : null;

    public void RecordIntro(IntroMetricsEvent ev)
    {
        if (!Enabled || ev == null) return;
        _introBuf.Add(ev);
        OnIntro?.Invoke(ev);
    }

    public void RecordWalk(WalkMetricsEvent ev)
    {
        if (!Enabled || ev == null) return;
        _walkBuf.Add(ev);
        OnWalk?.Invoke(ev);
    }

    public void RecordSpan(SpanMetricsEvent ev)
    {
        if (!Enabled || !EnableSpan || ev == null) return;
        _spanBuf.Add(ev);
        if (!string.IsNullOrEmpty(ev.Name)) 
            _latestSpanByName[ev.Name] = ev;
        OnSpan?.Invoke(ev);
    }

    public void RecordJitter(JitterCompletedEvent ev)
    {
        if (!Enabled || !EnableJitter || ev == null) return;
        _jitBuf.Add(ev);
        if (!string.IsNullOrEmpty(ev.Name)) 
            _latestJitterByName[ev.Name] = ev;
        OnJitterCompleted?.Invoke(ev);
    }

    // 統一されたRecent取得メソッド
    public int CopyRecentIntros(List<IntroMetricsEvent> dest, int max)
        => _introBuf.CopyRecent(dest, max);

    public int CopyRecentWalks(List<WalkMetricsEvent> dest, int max)
        => _walkBuf.CopyRecent(dest, max);

    public int CopyRecentSpans(List<SpanMetricsEvent> dest, int max)
        => _spanBuf.CopyRecent(dest, max);

    public int CopyRecentJitters(List<JitterCompletedEvent> dest, int max)
        => _jitBuf.CopyRecent(dest, max);

    // Span統計（最適化版）
    public readonly struct SpanStats
    {
        public readonly int Count; 
        public readonly double AvgMs; 
        public readonly double P95Ms; 
        public readonly double MaxMs;
        
        public SpanStats(int count, double avg, double p95, double max) 
        { 
            Count = count; 
            AvgMs = avg; 
            P95Ms = p95; 
            MaxMs = max; 
        }
    }

    public SpanStats GetSpanStats(string name, int lastK)
    {
        if (!Enabled || !EnableSpan || string.IsNullOrEmpty(name) || lastK <= 0)
            return new SpanStats(0, 0, 0, 0);

        // 名前でフィルタリングしてサンプル取得
        var matchingSpans = _spanBuf.GetRecentWhere(
            span => span.Name == name, 
            lastK
        );
        
        if (matchingSpans.Count == 0) 
            return new SpanStats(0, 0, 0, 0);
        
        // 統計計算
        var samples = new List<double>(matchingSpans.Count);
        double sum = 0, max = 0;
        
        foreach (var span in matchingSpans)
        {
            var v = span.DurationMs;
            samples.Add(v);
            sum += v;
            if (v > max) max = v;
        }
        
        double avg = sum / samples.Count;
        samples.Sort();
        int pIdx = Mathf.Clamp(Mathf.CeilToInt((samples.Count - 1) * 0.95f), 0, samples.Count - 1);
        double p95 = samples[pIdx];
        
        return new SpanStats(samples.Count, avg, p95, max);
    }

    // Span Scope (変更なし)
    public IDisposable BeginSpan(string name, MetricsContext ctx)
    {
        if (!Enabled || !EnableSpan) return EmptyScope.Instance;
        return new SpanScope(this, name ?? "", ctx ?? MetricsContext.None);
    }

    private sealed class SpanScope : IDisposable
    {
        private readonly MetricsHubRefactored _hub;
        private readonly string _name;
        private readonly MetricsContext _ctx;
        private readonly System.Diagnostics.Stopwatch _sw;
        private bool _disposed;
        
        public SpanScope(MetricsHubRefactored hub, string name, MetricsContext ctx)
        {
            _hub = hub; 
            _name = name; 
            _ctx = ctx; 
            _sw = System.Diagnostics.Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            if (_disposed) return; 
            _disposed = true;
            _sw.Stop();
            _hub.RecordSpan(new SpanMetricsEvent
            {
                Name = _name,
                DurationMs = _sw.Elapsed.TotalMilliseconds,
                Timestamp = DateTime.Now,
                Context = _ctx,
            });
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new EmptyScope();
        public void Dispose() { }
    }

    // Jitter Scope (変更なし - 既存実装を維持)
    public JitterScope StartJitter(string name, MetricsContext ctx)
    {
        if (!Enabled || !EnableJitter) 
            return new JitterScope(null, string.Empty, MetricsContext.None);
        return new JitterScope(this, name ?? "", ctx ?? MetricsContext.None);
    }

    public readonly struct JitterStats
    {
        public readonly int Count; 
        public readonly double AvgMs; 
        public readonly double P95Ms; 
        public readonly double MaxMs;
        
        public JitterStats(int count, double avg, double p95, double max) 
        { 
            Count = count; 
            AvgMs = avg; 
            P95Ms = p95; 
            MaxMs = max; 
        }
    }

    public class JitterScope
    {
        private readonly MetricsHubRefactored _hub;
        private readonly string _name;
        private readonly MetricsContext _ctx;
        private readonly List<float> _samples = new List<float>(256);
        private readonly CancellationTokenSource _cts;
        private UniTask _loopTask;
        private readonly DateTime _start;

        internal JitterScope(MetricsHubRefactored hub, string name, MetricsContext ctx)
        {
            _hub = hub; 
            _name = name; 
            _ctx = ctx; 
            _start = DateTime.Now;
            
            if (_hub != null)
            {
                _cts = new CancellationTokenSource();
                _loopTask = UniTask.Create(async () =>
                {
                    try
                    {
                        while (!_cts.IsCancellationRequested)
                        {
                            _samples.Add(Time.unscaledDeltaTime * 1000f);
                            await UniTask.Yield(PlayerLoopTiming.Update);
                        }
                    }
                    catch { /* no-op */ }
                });
            }
        }

        public async UniTask<JitterStats> EndAsync()
        {
            if (_hub == null)
            {
                await UniTask.Yield();
                return new JitterStats(0, 0, 0, 0);
            }
            
            _cts.Cancel();
            try { await _loopTask; } catch { /* no-op */ }

            var stats = ComputeStats(_samples);
            var ev = new JitterCompletedEvent
            {
                Name = _name,
                Count = stats.Count,
                AvgMs = stats.AvgMs,
                P95Ms = stats.P95Ms,
                MaxMs = stats.MaxMs,
                StartTime = _start,
                EndTime = DateTime.Now,
                Context = _ctx,
            };
            _hub.RecordJitter(ev);
            return stats;
        }
    }

    private static JitterStats ComputeStats(List<float> samples)
    {
        if (samples == null || samples.Count == 0) 
            return new JitterStats(0, 0, 0, 0);
        
        double sum = 0, max = 0; 
        int n = samples.Count;
        
        for (int i = 0; i < n; i++) 
        { 
            var v = (double)samples[i]; 
            sum += v; 
            if (v > max) max = v; 
        }
        
        double avg = sum / n;
        var sorted = new List<float>(samples); 
        sorted.Sort();
        int idx = Mathf.Clamp(Mathf.CeilToInt((sorted.Count - 1) * 0.95f), 0, sorted.Count - 1);
        double p95 = sorted[idx];
        
        return new JitterStats(n, avg, p95, max);
    }
}
#endif