using System;
using UnityEngine;

public sealed class JsonSummaryFormatter : IResultFormatter
{
    private static string Esc(string s)
        => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    public string Header(int presetCount, int repeat)
        => Header(presetCount, repeat, "-", "-");

    public string Header(int presetCount, int repeat, string scenarioName, string presetCollectionName)
    {
        var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
        return "{\"type\":\"meta\",\"timestamp\":\"" + Esc(ts) + "\",\"presets\":" + presetCount + ",\"repeat\":" + repeat + ",\"scenario\":\"" + Esc(sc) + "\",\"preset_collection\":\"" + Esc(pc) + "\",\"unity_version\":\"" + Esc(unity) + "\",\"platform\":\"" + Esc(plat) + "\",\"device_model\":\"" + Esc(device) + "\",\"quality_level\":\"" + Esc(quality) + "\"}";
    }

    public string SummaryLine(object preset, BenchmarkSummary s)
    {
        string setting = "-";
        if (preset is WatchUIUpdate.IntroPreset p)
        {
            setting = $"{p.introYieldDuringPrepare.ToString().ToLower()} {p.introYieldEveryN} {p.introPreAnimationDelaySec} {p.introSlideStaggerInterval}";
        }
        int aAvg  = (int)Math.Round(s.AvgIntroAvgMs);
        int aP95  = (int)Math.Round(s.AvgIntroP95Ms);
        int aMax  = (int)Math.Round(s.AvgIntroMaxMs);
        int aA    = (int)Math.Round(s.AvgActualMs);
        int aW    = (int)Math.Round(s.AvgWalkTotalMs);
        string scenario = global::BenchmarkContext.Current?.ScenarioName ?? "-";
        int presetIndex = global::BenchmarkContext.Current?.PresetIndex ?? -1;
        return "{\"type\":\"row\",\"setting\":\"" + Esc(setting)
            + "\",\"avg_intro_avg_ms\":" + aAvg
            + ",\"avg_intro_p95_ms\":" + aP95
            + ",\"avg_intro_max_ms\":" + aMax
            + ",\"avg_actual_ms\":" + aA
            + ",\"avg_walk_total_ms\":" + aW
            + ",\"ok\":" + s.SuccessCount
            + ",\"total\":" + s.RequestCount
            + ",\"scenario\":\"" + Esc(scenario) + "\""
            + ",\"preset_index\":" + presetIndex
            + "}";
    }

    public string Footer()
    {
        return "{\"type\":\"footer\",\"message\":\"[PresetSweep] Finished\"}";
    }

    public string Footer(int rows, int totalOk, int totalReq)
    {
        return "{\"type\":\"footer\",\"message\":\"[PresetSweep] Finished\",\"rows\":" + rows + ",\"ok_sum\":" + totalOk + ",\"total_sum\":" + totalReq + "}";
    }
}
