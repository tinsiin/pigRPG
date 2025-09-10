using System;
using UnityEngine;

public sealed class CsvSummaryFormatter : IResultFormatter
{
    // Columns: timestamp,presets,repeat,scenario,preset_collection (header row), then per-line: setting,avg_intro_avg_ms,avg_intro_p95_ms,avg_intro_max_ms,avg_actual_ms,avg_walk_total_ms,ok,total
    public string Header(int presetCount, int repeat)
        => Header(presetCount, repeat, "-", "-");

    public string Header(int presetCount, int repeat, string scenarioName, string presetCollectionName)
    {
        string sc = string.IsNullOrEmpty(scenarioName) ? "-" : scenarioName;
        string pc = string.IsNullOrEmpty(presetCollectionName) ? "-" : presetCollectionName;
        string unity = Application.unityVersion ?? "-";
        string plat = Application.platform.ToString();
        string device = SystemInfo.deviceModel ?? "-";
        string quality = "-";
        try
        {
            int qi = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            quality = (names != null && qi >= 0 && qi < names.Length) ? names[qi] : qi.ToString();
        }
        catch { quality = "-"; }
        // Start with preset collection name instead of timestamp, and remove duplicate preset_collection field.
        return $"# {pc},presets={presetCount},repeat={repeat},scenario={sc},unity_version={unity},platform={plat},device_model={device},quality_level={quality}\nsetting,avg_intro_avg_ms,avg_intro_p95_ms,avg_intro_max_ms,avg_actual_ms,avg_walk_total_ms,ok,total,scenario,preset_index";
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
        int aAvg  = (int)Math.Round(s.AvgIntroAvgMs);
        int aP95  = (int)Math.Round(s.AvgIntroP95Ms);
        int aMax  = (int)Math.Round(s.AvgIntroMaxMs);
        int aA    = (int)Math.Round(s.AvgActualMs);
        int aW    = (int)Math.Round(s.AvgWalkTotalMs);
        string scenario = global::BenchmarkContext.Current?.ScenarioName ?? "-";
        int presetIndex = global::BenchmarkContext.Current?.PresetIndex ?? -1;
        return $"{setting},{aAvg},{aP95},{aMax},{aA},{aW},{s.SuccessCount},{s.RequestCount},{scenario},{presetIndex}";
    }

    public string Footer() => "# [PresetSweep] Finished";

    public string Footer(int rows, int totalOk, int totalReq)
    {
        return $"# [PresetSweep] Finished, rows={rows},ok_sum={totalOk},total_sum={totalReq}";
    }
}
