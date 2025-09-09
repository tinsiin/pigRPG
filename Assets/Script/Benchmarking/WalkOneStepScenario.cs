using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Walk(1) を1回実行し、WatchUIUpdate のメトリクスから結果を収集するシナリオ。
/// </summary>
public sealed class WalkOneStepScenario : IBenchmarkScenario
{
    public string Name => "Walk(1)";

    public async UniTask<BenchmarkRunResult> RunOnceAsync(CancellationToken ct)
    {
        try
        {
            var walking = global::Walking.Instance ?? GameObject.FindObjectOfType<global::Walking>();
            if (walking == null) throw new Exception("Walking.Instance is null（シーンに Walking が存在しません）");

            await walking.RunOneWalkStepForBenchmark();

            var wui = global::WatchUIUpdate.Instance;
            var im = wui?.LastIntroMetrics;
            var wm = wui?.LastWalkMetrics;

            return new BenchmarkRunResult
            {
                IntroAvgMs = im?.IntroFrameAvgMs ?? 0,
                IntroP95Ms = im?.IntroFrameP95Ms ?? 0,
                IntroMaxMs = im?.IntroFrameMaxMs ?? 0,
                ActualMs   = im?.ActualMs ?? 0,
                WalkTotalMs = wm?.TotalMs ?? 0,
                Success = true,
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            return new BenchmarkRunResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
