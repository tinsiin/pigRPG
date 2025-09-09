#if !METRICS_DISABLED
using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public sealed class MetricsHub
{
    private static readonly MetricsHub _instance = new MetricsHub();
    public static MetricsHub Instance => _instance;

    // Runtime toggles
    public static bool Enabled = true;        // Master switch
    public static bool EnableSpan = true;     // Span only
    public static bool EnableJitter = true;   // Jitter only

    // Events
    public event Action<IntroMetricsEvent> OnIntro;
    public event Action<WalkMetricsEvent> OnWalk;
    public event Action<SpanMetricsEvent> OnSpan;
    public event Action<JitterCompletedEvent> OnJitterCompleted;

    // Latest snapshots
    private IntroMetricsEvent _latestIntro;
    private WalkMetricsEvent _latestWalk;
    private readonly Dictionary<string, SpanMetricsEvent> _latestSpanByName = new Dictionary<string, SpanMetricsEvent>(8);
    private readonly Dictionary<string, JitterCompletedEvent> _latestJitterByName = new Dictionary<string, JitterCompletedEvent>(8);

    // Ring buffers (fixed-size, overwrite)
    private const int IntroBufferSize = 32;
    private const int WalkBufferSize = 32;
    private const int SpanBufferSize = 64;
    private const int JitterBufferSize = 32;
    private readonly IntroMetricsEvent[] _introBuf = new IntroMetricsEvent[IntroBufferSize];
    private readonly WalkMetricsEvent[] _walkBuf = new WalkMetricsEvent[WalkBufferSize];
    private readonly SpanMetricsEvent[] _spanBuf = new SpanMetricsEvent[SpanBufferSize];
    private readonly JitterCompletedEvent[] _jitBuf = new JitterCompletedEvent[JitterBufferSize];
    private int _introIdx = -1;
    private int _walkIdx = -1;
    private int _spanIdx = -1;
    private int _jitIdx = -1;

    private MetricsHub() { }

    public IntroMetricsEvent LatestIntro => _latestIntro;
    public WalkMetricsEvent LatestWalk => _latestWalk;
    public SpanMetricsEvent LatestSpan(string name)
        => (name != null && _latestSpanByName.TryGetValue(name, out var ev)) ? ev : null;
    public JitterCompletedEvent LatestJitter(string name)
        => (name != null && _latestJitterByName.TryGetValue(name, out var ev)) ? ev : null;

    public void RecordIntro(IntroMetricsEvent ev)
    {
        if (!Enabled || ev == null) return;
        _latestIntro = ev;
        _introIdx = (_introIdx + 1) % IntroBufferSize;
        _introBuf[_introIdx] = ev;
        OnIntro?.Invoke(ev);
    }

    public void RecordWalk(WalkMetricsEvent ev)
    {
        if (!Enabled || ev == null) return;
        _latestWalk = ev;
        _walkIdx = (_walkIdx + 1) % WalkBufferSize;
        _walkBuf[_walkIdx] = ev;
        OnWalk?.Invoke(ev);
    }

    public void RecordSpan(SpanMetricsEvent ev)
    {
        if (!Enabled || !EnableSpan || ev == null) return;
        _spanIdx = (_spanIdx + 1) % SpanBufferSize;
        _spanBuf[_spanIdx] = ev;
        if (!string.IsNullOrEmpty(ev.Name)) _latestSpanByName[ev.Name] = ev;
        OnSpan?.Invoke(ev);
    }

    public void RecordJitter(JitterCompletedEvent ev)
    {
        if (!Enabled || !EnableJitter || ev == null) return;
        _jitIdx = (_jitIdx + 1) % JitterBufferSize;
        _jitBuf[_jitIdx] = ev;
        if (!string.IsNullOrEmpty(ev.Name)) _latestJitterByName[ev.Name] = ev;
        OnJitterCompleted?.Invoke(ev);
    }

    // Optional: recent retrieval (simple copy)
    public int CopyRecentIntros(List<IntroMetricsEvent> dest, int max)
    {
        if (dest == null || max <= 0) return 0;
        int count = Math.Min(max, IntroBufferSize);
        for (int i = 0; i < count; i++)
        {
            int idx = (_introIdx - i + IntroBufferSize) % IntroBufferSize;
            var item = _introBuf[idx];
            if (item == null) break;
            dest.Add(item);
        }
        return dest.Count;
    }

    public int CopyRecentWalks(List<WalkMetricsEvent> dest, int max)
    {
        if (dest == null || max <= 0) return 0;
        int count = Math.Min(max, WalkBufferSize);
        for (int i = 0; i < count; i++)
        {
            int idx = (_walkIdx - i + WalkBufferSize) % WalkBufferSize;
            var item = _walkBuf[idx];
            if (item == null) break;
            dest.Add(item);
        }
        return dest.Count;
    }

    public int CopyRecentSpans(List<SpanMetricsEvent> dest, int max)
    {
        if (dest == null || max <= 0) return 0;
        int count = Math.Min(max, SpanBufferSize);
        for (int i = 0; i < count; i++)
        {
            int idx = (_spanIdx - i + SpanBufferSize) % SpanBufferSize;
            var item = _spanBuf[idx];
            if (item == null) break;
            dest.Add(item);
        }
        return dest.Count;
    }

    public int CopyRecentJitters(List<JitterCompletedEvent> dest, int max)
    {
        if (dest == null || max <= 0) return 0;
        int count = Math.Min(max, JitterBufferSize);
        for (int i = 0; i < count; i++)
        {
            int idx = (_jitIdx - i + JitterBufferSize) % JitterBufferSize;
            var item = _jitBuf[idx];
            if (item == null) break;
            dest.Add(item);
        }
        return dest.Count;
    }

    // ===== Span 統計（直近N件） =====
    public readonly struct SpanStats
    {
        public readonly int Count; public readonly double AvgMs; public readonly double P95Ms; public readonly double MaxMs;
        public SpanStats(int count, double avg, double p95, double max) { Count = count; AvgMs = avg; P95Ms = p95; MaxMs = max; }
    }

    public SpanStats GetSpanStats(string name, int lastK)
    {
        if (!Enabled || !EnableSpan || string.IsNullOrEmpty(name) || lastK <= 0)
            return new SpanStats(0, 0, 0, 0);

        var samples = new List<double>(Mathf.Min(lastK, SpanBufferSize));
        int taken = 0;
        for (int i = 0; i < SpanBufferSize; i++)
        {
            int idx = (_spanIdx - i + SpanBufferSize) % SpanBufferSize;
            var item = _spanBuf[idx];
            if (item == null) break;
            if (item.Name == name)
            {
                samples.Add(item.DurationMs);
                taken++;
                if (taken >= lastK) break;
            }
        }
        if (samples.Count == 0) return new SpanStats(0, 0, 0, 0);
        double sum = 0, max = 0; int n = samples.Count;
        for (int i = 0; i < n; i++) { var v = samples[i]; sum += v; if (v > max) max = v; }
        double avg = sum / n;
        samples.Sort();
        int pIdx = Mathf.Clamp(Mathf.CeilToInt((samples.Count - 1) * 0.95f), 0, samples.Count - 1);
        double p95 = samples[pIdx];
        return new SpanStats(n, avg, p95, max);
    }

    // ===== Span Scope =====
    public IDisposable BeginSpan(string name, MetricsContext ctx)
    {
        if (!Enabled || !EnableSpan) return EmptyScope.Instance;
        return new SpanScope(this, name ?? "", ctx ?? MetricsContext.None);
    }

    private sealed class SpanScope : IDisposable
    {
        private readonly MetricsHub _hub;
        private readonly string _name;
        private readonly MetricsContext _ctx;
        private readonly System.Diagnostics.Stopwatch _sw;
        private bool _disposed;
        public SpanScope(MetricsHub hub, string name, MetricsContext ctx)
        {
            _hub = hub; _name = name; _ctx = ctx; _sw = System.Diagnostics.Stopwatch.StartNew();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
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

    // ===== Jitter Scope =====
    public JitterScope StartJitter(string name, MetricsContext ctx)
    {
        if (!Enabled || !EnableJitter) return new JitterScope(null, string.Empty, MetricsContext.None);
        return new JitterScope(this, name ?? "", ctx ?? MetricsContext.None);
    }

    public readonly struct JitterStats
    {
        public readonly int Count; public readonly double AvgMs; public readonly double P95Ms; public readonly double MaxMs;
        public JitterStats(int count, double avg, double p95, double max) { Count = count; AvgMs = avg; P95Ms = p95; MaxMs = max; }
    }

    public class JitterScope
    {
        private readonly MetricsHub _hub;
        private readonly string _name;
        private readonly MetricsContext _ctx;
        private readonly List<float> _samples = new List<float>(256);
        private readonly CancellationTokenSource _cts;
        private UniTask _loopTask;
        private readonly DateTime _start;

        internal JitterScope(MetricsHub hub, string name, MetricsContext ctx)
        {
            _hub = hub; _name = name; _ctx = ctx; _start = DateTime.Now;
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
        if (samples == null || samples.Count == 0) return new JitterStats(0, 0, 0, 0);
        double sum = 0, max = 0; int n = samples.Count;
        for (int i = 0; i < n; i++) { var v = (double)samples[i]; sum += v; if (v > max) max = v; }
        double avg = sum / n;
        var sorted = new List<float>(samples); sorted.Sort();
        int idx = Mathf.Clamp(Mathf.CeilToInt((sorted.Count - 1) * 0.95f), 0, sorted.Count - 1);
        double p95 = sorted[idx];
        return new JitterStats(n, avg, p95, max);
    }
}
#else
using System;
using Cysharp.Threading.Tasks;

// Compile-time disabled MetricsHub stub. All APIs are preserved but become no-ops.
public sealed class MetricsHub
{
    private static readonly MetricsHub _instance = new MetricsHub();
    public static MetricsHub Instance => _instance;

    public static bool Enabled = false;
    public static bool EnableSpan = false;
    public static bool EnableJitter = false;

    public event Action<IntroMetricsEvent> OnIntro;
    public event Action<WalkMetricsEvent> OnWalk;
    public event Action<SpanMetricsEvent> OnSpan;
    public event Action<JitterCompletedEvent> OnJitterCompleted;

    // Latest snapshots (stubs)
    public IntroMetricsEvent LatestIntro => null;
    public WalkMetricsEvent LatestWalk => null;
    public SpanMetricsEvent LatestSpan(string name) => null;
    public JitterCompletedEvent LatestJitter(string name) => null;

    public void RecordIntro(IntroMetricsEvent ev) { }
    public void RecordWalk(WalkMetricsEvent ev) { }
    public void RecordSpan(SpanMetricsEvent ev) { }
    public void RecordJitter(JitterCompletedEvent ev) { }

    private sealed class EmptyScope : IDisposable { public static readonly EmptyScope Instance = new EmptyScope(); public void Dispose() { } }
    public IDisposable BeginSpan(string name, MetricsContext ctx) => EmptyScope.Instance;

    public readonly struct JitterStats { public readonly int Count; public readonly double AvgMs; public readonly double P95Ms; public readonly double MaxMs; public JitterStats(int c, double a, double p, double m) { Count = c; AvgMs = a; P95Ms = p; MaxMs = m; } }
    public class JitterScope { public UniTask<JitterStats> EndAsync() => UniTask.FromResult(new JitterStats(0, 0, 0, 0)); }

    // API parity: SpanStats and GetSpanStats (always zero in stub)
    public readonly struct SpanStats { public readonly int Count; public readonly double AvgMs; public readonly double P95Ms; public readonly double MaxMs; public SpanStats(int c, double a, double p, double m) { Count = c; AvgMs = a; P95Ms = p; MaxMs = m; } }
    public SpanStats GetSpanStats(string name, int lastK) => new SpanStats(0, 0, 0, 0);
}
#endif
