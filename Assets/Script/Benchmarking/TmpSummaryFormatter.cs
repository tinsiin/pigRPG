using System;

/// <summary>
/// プリセットごとのサマリ1行を生成するフォーマッター。TMP向けの簡易書式。
/// </summary>
public sealed class TmpSummaryFormatter : IResultFormatter
{
    public string Header(int presetCount, int repeat)
        => $"# presets={presetCount} repeat={repeat}";

    /// <summary>
    /// 追加情報付きヘッダ。先頭にプリセットコレクション名（＝SO名）を、続けてシナリオ名を表示します。
    /// 日時は表示しません。
    /// </summary>
    public string Header(int presetCount, int repeat, string scenarioName, string presetCollectionName)
    {
        string sc = string.IsNullOrEmpty(scenarioName) ? "-" : scenarioName;
        string pc = string.IsNullOrEmpty(presetCollectionName) ? "-" : presetCollectionName;
        return $"# {pc} scenario={sc} presets={presetCount} repeat={repeat}";
    }

    public string SummaryLine(object preset, BenchmarkSummary s)
    {
        string setting = "-";
        if (preset is WatchUIUpdate.IntroPreset p)
        {
            setting = $"{p.introYieldDuringPrepare.ToString().ToLower()} {p.introYieldEveryN} {p.introPreAnimationDelaySec} {p.introSlideStaggerInterval}";
        }
        else if (preset is WatchUIUpdate.EnemySpawnPreset ep)
        {
            int bs = System.Math.Max(1, ep.enemySpawnBatchSize);
            int ib = System.Math.Max(0, ep.enemySpawnInterBatchFrames);
            string thr = ep.throttleEnemySpawns.ToString().ToLower();
            string vlog = ep.enableVerboseEnemyLogs.ToString().ToLower();
            setting = $"thr={thr} bs={bs} if={ib} vlog={vlog}";
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
