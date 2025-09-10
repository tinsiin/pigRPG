using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

public readonly struct BenchmarkProgress
{
    public readonly int PresetIndex;    // 0-based
    public readonly int PresetCount;
    public readonly int RunIndex;       // 1-based
    public readonly int RunCount;       // total per preset
    public BenchmarkProgress(int pi, int pc, int ri, int rc)
    { PresetIndex = pi; PresetCount = pc; RunIndex = ri; RunCount = rc; }
}

public static class BenchmarkRunner
{
    public static async UniTask<BenchmarkSummary> RunRepeatAsync(
        IBenchmarkScenario scenario,
        int repeat,
        float interDelaySec,
        int presetIndex,
        int presetCount,
        Action<int, BenchmarkRunResult> onEachRun,
        IProgress<BenchmarkProgress> progress,
        CancellationToken ct)
    {
        int req = Math.Max(1, repeat);
        int ok = 0, ng = 0;
        double sumIntroAvg = 0, sumIntroP95 = 0, sumIntroMax = 0, sumA = 0, sumW = 0;

        // Preserve outer context (e.g., preset info) and add RunIndex per iteration
        var outerCtx = global::BenchmarkContext.Current;
        for (int i = 0; i < req; i++)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var ctx = new MetricsContext
                {
                    ScenarioName = outerCtx?.ScenarioName ?? scenario?.Name ?? "",
                    PresetSummary = outerCtx?.PresetSummary,
                    PresetIndex = outerCtx?.PresetIndex ?? -1,
                    RunIndex = i + 1,
                    Tags = outerCtx?.Tags,
                };
                global::BenchmarkContext.Current = ctx;
                BenchmarkRunResult lastRun = null;
                using (global::MetricsHub.Instance.BeginSpan("Runner.Loop", ctx))
                {
                    using (global::MetricsHub.Instance.BeginSpan("Runner.RunOnce", ctx))
                    {
                        var r = await scenario.RunOnceAsync(ct);
                        lastRun = r;
                        if (r != null && r.Success)
                        {
                            sumIntroAvg += r.IntroAvgMs;
                            sumIntroP95 += r.IntroP95Ms;
                            sumIntroMax += r.IntroMaxMs;
                            sumA        += r.ActualMs;
                            sumW        += r.WalkTotalMs;
                            ok++;
                        }
                        else
                        {
                            ng++;
                        }
                    }
                }
                // After Loop span ends, record its duration to the run result
                var loopSpan = global::MetricsHub.Instance?.LatestSpan("Runner.Loop");
                if (lastRun != null && loopSpan != null)
                {
                    lastRun.LoopMs = loopSpan.DurationMs;
                }
                // Call per-run after the Loop span is finished so loop duration is finalized
                onEachRun?.Invoke(i + 1, lastRun);
                // Report progress after each run
                progress?.Report(new BenchmarkProgress(presetIndex, presetCount, i + 1, req));
            }
            catch
            {
                ng++;
            }

            if (interDelaySec > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(interDelaySec), cancellationToken: ct);
            }
        }
        // Restore outer context
        global::BenchmarkContext.Current = outerCtx;

        int denom = Math.Max(1, ok);
        return new BenchmarkSummary
        {
            RequestCount   = req,
            SuccessCount   = ok,
            FailCount      = ng,
            AvgIntroAvgMs  = sumIntroAvg / denom,
            AvgIntroP95Ms  = sumIntroP95 / denom,
            AvgIntroMaxMs  = sumIntroMax / denom,
            AvgActualMs    = sumA        / denom,
            AvgWalkTotalMs = sumW        / denom,
        };
    }

    public static async UniTask RunPresetSweepAsync<TPreset>(
        IBenchmarkScenario scenario,
        TPreset[] presets,
        ISettingsApplier<TPreset> applier,
        int repeat,
        float interDelaySec,
        IResultFormatter formatter,
        Action<string> sinkAppendLine,
        Action<TPreset, BenchmarkSummary> onEachSummary,
        Action<int, TPreset, BenchmarkRunResult> onEachRun,
        Func<TPreset, Dictionary<string, string>> buildTags,
        IProgress<BenchmarkProgress> progress,
        CancellationToken ct)
    {
        if (presets == null || presets.Length == 0) return;

        applier.SaveCurrent();
        try
        {
            using (global::MetricsHub.Instance.BeginSpan("Runner.Total", new MetricsContext { ScenarioName = scenario?.Name ?? string.Empty, PresetIndex = -1, RunIndex = 0 }))
            {
                for (int pi = 0; pi < presets.Length; pi++)
                {
                    if (ct.IsCancellationRequested) break;
                    var p = presets[pi];
                    applier.Apply(p);

                    // Set base context (ScenarioName + Preset info) for this preset
                    var saved = global::BenchmarkContext.Current;
                    var tags = buildTags != null ? buildTags(p) : null;
                    global::BenchmarkContext.Current = new MetricsContext
                    {
                        ScenarioName = scenario?.Name ?? "",
                        PresetSummary = BuildPresetSummary(p),
                        PresetIndex = pi,
                        RunIndex = 0,
                        Tags = tags,
                    };

                    BenchmarkSummary summary;
                    using (global::MetricsHub.Instance.BeginSpan("Runner.Preset", global::BenchmarkContext.Current))
                    {
                        summary = await RunRepeatAsync(
                            scenario,
                            repeat,
                            interDelaySec,
                            pi,
                            presets.Length,
                            (ri, r) => onEachRun?.Invoke(ri, p, r),
                            progress,
                            ct);
                    }
                    var line = formatter.SummaryLine(p, summary);
                    sinkAppendLine?.Invoke(line);
                    onEachSummary?.Invoke(p, summary);
                    // Report preset-level completion (RunIndex=RunCount to indicate preset done)
                    progress?.Report(new BenchmarkProgress(pi, presets.Length, repeat, repeat));
                    global::BenchmarkContext.Current = saved;
                }
            }
        }
        finally
        {
            applier.Restore();
        }
    }

    private static string BuildPresetSummary<TPreset>(TPreset preset)
    {
        if (preset is WatchUIUpdate.IntroPreset p)
        {
            return $"{p.introYieldDuringPrepare.ToString().ToLower()} {p.introYieldEveryN} {p.introPreAnimationDelaySec} {p.introSlideStaggerInterval}";
        }
        else if (preset is WatchUIUpdate.EnemySpawnPreset ep)
        {
            // 敵UI用はキー付きで表現
            int bs = System.Math.Max(1, ep.enemySpawnBatchSize);
            int ib = System.Math.Max(0, ep.enemySpawnInterBatchFrames);
            string thr = ep.throttleEnemySpawns.ToString().ToLower();
            string vlog = ep.enableVerboseEnemyLogs.ToString().ToLower();
            return $"thr={thr} bs={bs} if={ib} vlog={vlog}";
        }
        return "-";
    }
}
