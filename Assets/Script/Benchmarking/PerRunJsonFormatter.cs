using System;
using UnityEngine;

public sealed class PerRunJsonFormatter
{
    private static string Esc(string s)
        => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

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
        return "{\"type\":\"run\",\"scenario\":\"" + Esc(sc) + "\",\"preset_index\":" + presetIndex + ",\"preset_summary\":\"" + Esc(ps) + "\",\"run_index\":" + runIndex
            + ",\"success\":" + ok
            + ",\"intro_avg_ms\":" + iAvg
            + ",\"intro_p95_ms\":" + iP95
            + ",\"intro_max_ms\":" + iMax
            + ",\"actual_ms\":" + aMs
            + ",\"walk_total_ms\":" + wMs
            + ",\"loop_ms\":" + lMs
            + ",\"tags\":\"" + Esc(tg) + "\""
            + ",\"error\":\"" + Esc(err) + "\"}";
    }

    public string Footer(int rows, int ok)
    {
        return "{\"type\":\"footer\",\"message\":\"[PerRun] Finished\",\"rows\":" + rows + ",\"ok\":" + ok + "}";
    }
}
