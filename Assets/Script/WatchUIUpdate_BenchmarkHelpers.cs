using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// WatchUIUpdate のベンチマーク関連ヘルパーメソッド群
/// 巨大メソッドをリファクタリングして責務ごとに分割
/// </summary>
public partial class WatchUIUpdate
{
    #region Validation Methods
    
    /// <summary>
    /// プリセットスイープの実行可能性を検証
    /// </summary>
    private bool ValidatePresetSweepPrerequisitesImpl(out IntroPreset[] effPresets)
    {
        effPresets = null;
        
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PresetSweep] Play Mode で実行してください");
            return false;
        }
        
        if (IsBenchmarkRunning)
        {
            Debug.LogWarning("[PresetSweep] すでにベンチマーク実行中です");
            return false;
        }
        
        if (presetConfig == null)
        {
            Debug.LogWarning("[PresetSweep] presetConfig (IntroPresetCollection) が未割り当てです（SOを割り当ててください）");
            return false;
        }
        
        effPresets = presetConfig.items;
        if (effPresets == null || effPresets.Length == 0)
        {
            Debug.LogWarning("[PresetSweep] presetConfig.items が空です（SOにプリセットを追加してください）");
            return false;
        }
        
        return true;
    }
    
    // ===== Thin wrappers for main partial class (to match original call sites) =====
    private bool ValidatePresetSweepPrerequisites(out IntroPreset[] effPresets)
    {
        return ValidatePresetSweepPrerequisitesImpl(out effPresets);
    }

    private System.Progress<global::BenchmarkProgress> CreateProgressReporter(
        System.DateTime startedAt,
        int totalRuns,
        int presetCount)
    {
        return CreateProgressReporterImpl(startedAt, totalRuns, presetCount);
    }

    private BenchmarkFormatters CreateBenchmarkFormatters()
    {
        return CreateBenchmarkFormattersImpl();
    }

    private void ProcessPerRunResult(
        BenchmarkFormatters formatters,
        int runIndex,
        IntroPreset preset,
        global::BenchmarkRunResult result,
        string scenarioName,
        string perRunCsvPath,
        string perRunJsonPath,
        bool perRunStream,
        ref int perRunRows,
        ref int perRunOk)
    {
        ProcessPerRunResultImpl(formatters, runIndex, preset, result, scenarioName,
            perRunCsvPath, perRunJsonPath, perRunStream, ref perRunRows, ref perRunOk);
    }

    private void SaveBenchmarkResults(
        BenchmarkFormatters formatters,
        string scenarioName,
        string presetCollectionName,
        int repeat,
        int presetCount,
        int sinkRows,
        int sinkOkSum,
        int sinkTotalSum,
        int perRunRows,
        int perRunOk,
        string perRunCsvPath,
        string perRunJsonPath,
        bool perRunStream)
    {
        SaveBenchmarkResultsImpl(formatters, scenarioName, presetCollectionName, repeat, presetCount,
            sinkRows, sinkOkSum, sinkTotalSum, perRunRows, perRunOk, perRunCsvPath, perRunJsonPath, perRunStream);
    }
    #endregion
    
    #region Formatter Creation
    
    /// <summary>
    /// ベンチマーク出力用フォーマッター群を生成
    /// </summary>
    private struct BenchmarkFormatters
    {
        public global::TmpSummaryFormatter tmpFormatter;
        public global::CsvSummaryFormatter csvFormatter;
        public global::JsonSummaryFormatter jsonFormatter;
        public global::PerRunCsvFormatter perRunCsvFormatter;
        public global::PerRunJsonFormatter perRunJsonFormatter;
        public StringBuilder csvSb;
        public StringBuilder jsonSb;
        public StringBuilder perRunCsvSb;
        public StringBuilder perRunJsonSb;
        public bool useCsv;
        public bool useJson;
        public bool usePerRunCsv;
        public bool usePerRunJson;
        public bool writeToFile;
        public string outputFolder;
        public string fileBaseName;
    }
    
    private BenchmarkFormatters CreateBenchmarkFormattersImpl()
    {
        var formatters = new BenchmarkFormatters();
        
        formatters.tmpFormatter = new global::TmpSummaryFormatter();
        formatters.useCsv = outputSettings?.enableCsv ?? false;
        formatters.useJson = outputSettings?.enableJson ?? false;
        formatters.usePerRunCsv = outputSettings?.enablePerRunCsv ?? false;
        formatters.usePerRunJson = outputSettings?.enablePerRunJson ?? false;
        formatters.writeToFile = outputSettings?.writeToFile ?? false;
        formatters.outputFolder = outputSettings?.outputFolder ?? string.Empty;
        formatters.fileBaseName = outputSettings?.fileBaseName ?? "benchmark";
        
        if (formatters.useCsv)
        {
            formatters.csvFormatter = new global::CsvSummaryFormatter();
            formatters.csvSb = new StringBuilder(1024);
        }
        
        if (formatters.useJson)
        {
            formatters.jsonFormatter = new global::JsonSummaryFormatter();
            formatters.jsonSb = new StringBuilder(1024);
        }
        
        if (formatters.usePerRunCsv)
        {
            formatters.perRunCsvFormatter = new global::PerRunCsvFormatter();
            formatters.perRunCsvSb = new StringBuilder(2048);
        }
        
        if (formatters.usePerRunJson)
        {
            formatters.perRunJsonFormatter = new global::PerRunJsonFormatter();
            formatters.perRunJsonSb = new StringBuilder(2048);
        }
        
        return formatters;
    }
    
    #endregion
    
    #region Progress Management
    
    /// <summary>
    /// プリセットスイープの進捗管理を初期化
    /// </summary>
    private System.Progress<global::BenchmarkProgress> CreateProgressReporterImpl(
        System.DateTime startedAt, 
        int totalRuns, 
        int presetCount)
    {
        return new System.Progress<global::BenchmarkProgress>(bp =>
        {
            try
            {
                int completed = bp.PresetIndex * bp.RunCount + bp.RunIndex;
                double elapsed = (System.DateTime.Now - startedAt).TotalSeconds;
                double avgPerRun = completed > 0 ? (elapsed / completed) : 0.0;
                double remain = System.Math.Max(0.0, totalRuns - completed);
                
                _lastSweepProgress = new SweepProgressSnapshot
                {
                    PresetIndex = bp.PresetIndex,
                    PresetCount = bp.PresetCount,
                    RunIndex = bp.RunIndex,
                    RunCount = bp.RunCount,
                    CompletedRuns = System.Math.Max(0, System.Math.Min(totalRuns, completed)),
                    TotalRuns = totalRuns,
                    StartedAt = startedAt,
                    ElapsedSec = elapsed,
                    ETASec = avgPerRun * remain,
                };
            }
            catch { /* no-op */ }
        });
    }
    
    #endregion
    
    #region Summary Callbacks
    
    /// <summary>
    /// プリセットごとのサマリ処理コールバックを生成
    /// </summary>
    private Action<IntroPreset, global::BenchmarkSummary> CreateSummaryCallbackImpl(
        BenchmarkFormatters formatters,
        ref int sinkRows,
        ref int sinkOkSum,
        ref int sinkTotalSum)
    {
        var rows = sinkRows;
        var okSum = sinkOkSum;
        var totalSum = sinkTotalSum;
        
        return (p, s) =>
        {
            if (formatters.useCsv)
                formatters.csvSb.AppendLine(formatters.csvFormatter.SummaryLine(p, s));
            if (formatters.useJson)
                formatters.jsonSb.AppendLine(formatters.jsonFormatter.SummaryLine(p, s));
            
            rows++;
            okSum += s.SuccessCount;
            totalSum += s.RequestCount;
        };
    }
    
    #endregion
    
    #region Per-Run Processing
    
    /// <summary>
    /// 実行ごとの詳細ログ処理
    /// </summary>
    private void ProcessPerRunResultImpl(
        BenchmarkFormatters formatters,
        int runIndex,
        IntroPreset preset,
        global::BenchmarkRunResult result,
        string scenarioName,
        string perRunCsvPath,
        string perRunJsonPath,
        bool perRunStream,
        ref int perRunRows,
        ref int perRunOk)
    {
        string sc = global::BenchmarkContext.Current?.ScenarioName ?? scenarioName;
        int pi = global::BenchmarkContext.Current?.PresetIndex ?? -1;
        string ps = global::BenchmarkContext.Current?.PresetSummary ?? "-";
        
        // Tags を "k=v;..." に整形
        string tagsStr = FormatContextTags(global::BenchmarkContext.Current?.Tags);
        
        if (formatters.usePerRunCsv)
        {
            var line = formatters.perRunCsvFormatter.RunLine(sc, pi, ps, runIndex, result, tagsStr);
            if (perRunStream && !string.IsNullOrEmpty(perRunCsvPath))
                File.AppendAllText(perRunCsvPath, line + "\n", Encoding.UTF8);
            else
                formatters.perRunCsvSb?.AppendLine(line);
        }
        
        if (formatters.usePerRunJson)
        {
            var line = formatters.perRunJsonFormatter.RunLine(sc, pi, ps, runIndex, result, tagsStr);
            if (perRunStream && !string.IsNullOrEmpty(perRunJsonPath))
                File.AppendAllText(perRunJsonPath, line + "\n", Encoding.UTF8);
            else
                formatters.perRunJsonSb?.AppendLine(line);
        }
        
        perRunRows++;
        if (result != null && result.Success) perRunOk++;
    }
    
    private string FormatContextTags(Dictionary<string, string> tags)
    {
        if (tags == null || tags.Count == 0)
            return string.Empty;
        
        var keys = new List<string>(tags.Keys);
        keys.Sort();
        var sbt = new StringBuilder(64);
        
        for (int i = 0; i < keys.Count; i++)
        {
            string k = keys[i];
            string v = tags[k] ?? string.Empty;
            if (i > 0) sbt.Append(';');
            sbt.Append(k).Append('=').Append(v);
        }
        
        return sbt.ToString();
    }
    
    #endregion
    
    #region File Output
    
    /// <summary>
    /// ベンチマーク結果をファイルに保存
    /// </summary>
    private void SaveBenchmarkResultsImpl(
        BenchmarkFormatters formatters,
        string scenarioName,
        string presetCollectionName,
        int repeat,
        int presetCount,
        int sinkRows,
        int sinkOkSum,
        int sinkTotalSum,
        int perRunRows,
        int perRunOk,
        string perRunCsvPath,
        string perRunJsonPath,
        bool perRunStream)
    {
        if (!formatters.writeToFile)
            return;
        
        string dir = string.IsNullOrEmpty(formatters.outputFolder) 
            ? Application.persistentDataPath 
            : formatters.outputFolder;
        Directory.CreateDirectory(dir);
        string ts = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        // CSV保存
        if (formatters.useCsv && formatters.csvSb != null)
        {
            formatters.csvSb.AppendLine(formatters.csvFormatter.Footer(sinkRows, sinkOkSum, sinkTotalSum));
            string path = ResolveOutputPath(dir, formatters.fileBaseName, scenarioName, 
                presetCollectionName, ts, repeat, presetCount, "csv");
            File.WriteAllText(path, formatters.csvSb.ToString(), Encoding.UTF8);
            Debug.Log($"[PresetSweep] CSV saved: {path}");
        }
        
        // JSON保存
        if (formatters.useJson && formatters.jsonSb != null)
        {
            formatters.jsonSb.AppendLine(formatters.jsonFormatter.Footer(sinkRows, sinkOkSum, sinkTotalSum));
            string path = ResolveOutputPath(dir, formatters.fileBaseName, scenarioName, 
                presetCollectionName, ts, repeat, presetCount, "json");
            File.WriteAllText(path, formatters.jsonSb.ToString(), Encoding.UTF8);
            Debug.Log($"[PresetSweep] JSON saved: {path}");
        }
        
        // Per-Run 保存
        SavePerRunResultsImpl(formatters, perRunStream, perRunCsvPath, perRunJsonPath, 
            perRunRows, perRunOk, scenarioName, presetCollectionName, ts, repeat, presetCount);
    }
    
    private void SavePerRunResultsImpl(
        BenchmarkFormatters formatters,
        bool perRunStream,
        string perRunCsvPath,
        string perRunJsonPath,
        int perRunRows,
        int perRunOk,
        string scenarioName,
        string presetCollectionName,
        string ts,
        int repeat,
        int presetCount)
    {
        if (!formatters.writeToFile || (!formatters.usePerRunCsv && !formatters.usePerRunJson))
            return;
        
        if (perRunStream)
        {
            // ストリーム書き出し済みの場合はフッタのみ追記
            if (formatters.usePerRunCsv && !string.IsNullOrEmpty(perRunCsvPath))
            {
                File.AppendAllText(perRunCsvPath, 
                    formatters.perRunCsvFormatter.Footer(perRunRows, perRunOk) + "\n", Encoding.UTF8);
                Debug.Log($"[PerRun] CSV saved (stream): {perRunCsvPath}");
            }
            if (formatters.usePerRunJson && !string.IsNullOrEmpty(perRunJsonPath))
            {
                File.AppendAllText(perRunJsonPath, 
                    formatters.perRunJsonFormatter.Footer(perRunRows, perRunOk) + "\n", Encoding.UTF8);
                Debug.Log($"[PerRun] JSON saved (stream): {perRunJsonPath}");
            }
        }
        else
        {
            // 一括保存
            string dir = string.IsNullOrEmpty(formatters.outputFolder) 
                ? Application.persistentDataPath 
                : formatters.outputFolder;
            Directory.CreateDirectory(dir);
            string baseRuns = formatters.fileBaseName + "_runs";
            
            if (formatters.usePerRunCsv && formatters.perRunCsvSb != null)
            {
                formatters.perRunCsvSb.AppendLine(formatters.perRunCsvFormatter.Footer(perRunRows, perRunOk));
                string path = ResolveOutputPath(dir, baseRuns, scenarioName, 
                    presetCollectionName, ts, repeat, presetCount, "csv");
                File.WriteAllText(path, formatters.perRunCsvSb.ToString(), Encoding.UTF8);
                Debug.Log($"[PerRun] CSV saved: {path}");
            }
            
            if (formatters.usePerRunJson && formatters.perRunJsonSb != null)
            {
                formatters.perRunJsonSb.AppendLine(formatters.perRunJsonFormatter.Footer(perRunRows, perRunOk));
                string path = ResolveOutputPath(dir, baseRuns, scenarioName, 
                    presetCollectionName, ts, repeat, presetCount, "json");
                File.WriteAllText(path, formatters.perRunJsonSb.ToString(), Encoding.UTF8);
                Debug.Log($"[PerRun] JSON saved: {path}");
            }
        }
    }
    
    #endregion
}