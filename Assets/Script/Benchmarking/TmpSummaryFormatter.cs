using System;

/// <summary>
/// プリセットごとのサマリ1行を生成するフォーマッター。TMP向けの簡易書式。
/// </summary>
public sealed class TmpSummaryFormatter : IResultFormatter
{
    public string Header(int presetCount, int repeat)
        => $"# {DateTime.Now:yyyy-MM-dd HH:mm:ss} presets={presetCount} repeat={repeat}";

    public string SummaryLine(object preset, BenchmarkSummary s)
    {
        string setting = "-";
        if (preset is WatchUIUpdate.IntroPreset p)
        {
            setting = $"{p.introYieldDuringPrepare.ToString().ToLower()} {p.introYieldEveryN} {p.introPreAnimationDelaySec} {p.introSlideStaggerInterval}";
        }
        // 小数は四捨五入して整数に（既存仕様踏襲）
        int aAvg  = (int)Math.Round(s.AvgIntroAvgMs);
        int aP95  = (int)Math.Round(s.AvgIntroP95Ms);
        int aMax  = (int)Math.Round(s.AvgIntroMaxMs);
        int aA    = (int)Math.Round(s.AvgActualMs);
        int aW    = (int)Math.Round(s.AvgWalkTotalMs);
        return $"{setting}: {aAvg} {aP95} {aMax} {aA} {aW} x{s.SuccessCount}/{s.RequestCount}";
    }

    public string Footer() => "[PresetSweep] Finished";
}
