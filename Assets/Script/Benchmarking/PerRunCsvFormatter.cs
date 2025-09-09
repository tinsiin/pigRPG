using System;
using UnityEngine;

public sealed class PerRunCsvFormatter
{
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
        return $"# {DateTime.Now:yyyy-MM-dd HH:mm:ss},presets={presetCount},repeat={repeat},scenario={sc},preset_collection={pc},unity_version={unity},platform={plat},device_model={device},quality_level={quality}\nscenario,preset_index,preset_summary,run_index,success,intro_avg_ms,intro_p95_ms,intro_max_ms,actual_ms,walk_total_ms,loop_ms,tags,error";
    }

    public string RunLine(string scenario, int presetIndex, string presetSummary, int runIndex, BenchmarkRunResult r, string tags)
    {
        string sc = string.IsNullOrEmpty(scenario) ? "-" : scenario;
        string ps = string.IsNullOrEmpty(presetSummary) ? "-" : presetSummary;
        string tg = string.IsNullOrEmpty(tags) ? "" : tags;
        int ok = (r != null && r.Success) ? 1 : 0;
        int iAvg = (int)Math.Round(r?.IntroAvgMs ?? 0);
        int iP95 = (int)Math.Round(r?.IntroP95Ms ?? 0);
        int iMax = (int)Math.Round(r?.IntroMaxMs ?? 0);
        int aMs  = (int)Math.Round(r?.ActualMs ?? 0);
        int wMs  = (int)Math.Round(r?.WalkTotalMs ?? 0);
        int lMs  = (int)Math.Round(r?.LoopMs ?? 0);
        string err = r?.ErrorMessage ?? "";
        // エラーは簡易エスケープ（カンマ->スペース）
        err = err.Replace(',', ' ');
        // プリセット要約のカンマ・改行を抑制
        ps = ps.Replace(',', ' ').Replace('\n', ' ').Replace('\r', ' ');
        // タグも安全化
        tg = tg.Replace(',', ' ').Replace('\n', ' ').Replace('\r', ' ');
        return $"{sc},{presetIndex},{ps},{runIndex},{ok},{iAvg},{iP95},{iMax},{aMs},{wMs},{lMs},{tg},{err}";
    }

    public string Footer(int rows, int ok)
    {
        return $"# [PerRun] rows={rows},ok={ok}";
    }
}
