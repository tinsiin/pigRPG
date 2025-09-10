using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

public partial class WatchUIUpdate
{
    // ===== 敵UIプリセット（C案: 導入アニメと分離） =====
    [System.Serializable]
    public struct EnemySpawnPreset
    {
        public bool throttleEnemySpawns;          // 生成のフレーム分散
        public int  enemySpawnBatchSize;          // 1フレームの生成数（>=1）
        public int  enemySpawnInterBatchFrames;   // バッチ間待機フレーム（>=0）
        public bool enableVerboseEnemyLogs;       // 詳細ログ
    }

    public struct EnemySpawnSettingsSnapshot
    {
        public bool throttleEnemySpawns;
        public int  enemySpawnBatchSize;
        public int  enemySpawnInterBatchFrames;
        public bool enableVerboseEnemyLogs;
    }

    [Header(
        "敵UIプリセットスイープ（SO 必須）\n" +
        "・手順: 1) EnemySpawnPresetCollection を作成→ items, repeatCount, interRunDelaySec を設定\n" +
        "          2) 本フィールドに割り当て\n" +
        "          3) （任意）BenchmarkOutputSettings を割り当ててCSV/JSONを有効化\n" +
        "          4) 実行（StartEnemyPresetSweep/ContextMenu）\n" +
        "・依存: MetricsSettings が割当済みなら計測ON/OFF/種別が反映されます（任意）。\n" +
        "・結果: 画面のTMP（任意）へ1行サマリ、（任意）CSV/JSONの生成と保存。")]
    [Tooltip("敵UIプリセット/回数/待機のSO。未割り当ての場合は実行できません。")]
    [SerializeField] private EnemySpawnPresetCollection enemyPresetConfig;

    // SO 構成状態（UI連携用の公開プロパティ）
    public bool HasEnemyPresetConfig => (enemyPresetConfig != null && enemyPresetConfig.items != null && enemyPresetConfig.items.Length > 0);

    [Header("敵プリセットスイープ時の測定オプション")]
    [Tooltip("スイープ中のみズーム演出を一時停止（終了時に元の設定へ復元）")]
    [SerializeField] private bool enemySweepDisableZoom = true;

    public EnemySpawnSettingsSnapshot SaveCurrentEnemySpawnSettings()
    {
        return new EnemySpawnSettingsSnapshot
        {
            throttleEnemySpawns = this.throttleEnemySpawns,
            enemySpawnBatchSize = this.enemySpawnBatchSize,
            enemySpawnInterBatchFrames = this.enemySpawnInterBatchFrames,
            enableVerboseEnemyLogs = this.enableVerboseEnemyLogs,
        };
    }

    public void ApplyEnemySpawnPreset(EnemySpawnPreset p)
    {
        this.throttleEnemySpawns = p.throttleEnemySpawns;
        this.enemySpawnBatchSize = Mathf.Max(1, p.enemySpawnBatchSize);
        this.enemySpawnInterBatchFrames = Mathf.Max(0, p.enemySpawnInterBatchFrames);
        this.enableVerboseEnemyLogs = p.enableVerboseEnemyLogs;
    }

    public void RestoreEnemySpawnSettings(EnemySpawnSettingsSnapshot s)
    {
        this.throttleEnemySpawns = s.throttleEnemySpawns;
        this.enemySpawnBatchSize = s.enemySpawnBatchSize;
        this.enemySpawnInterBatchFrames = s.enemySpawnInterBatchFrames;
        this.enableVerboseEnemyLogs = s.enableVerboseEnemyLogs;
    }

    [ContextMenu("Run Enemy Preset Sweep (EnemyUI)")]
    public async void RunEnemyPresetSweepContext()
    {
        await RunEnemyPresetSweepBenchmark();
    }

    // UIのOnClickなどから直接呼べるvoidラッパー
    public void StartEnemyPresetSweep()
    {
        RunEnemyPresetSweepBenchmark().Forget();
    }

    private bool ValidateEnemyPresetSweepPrerequisites(out EnemySpawnPreset[] effPresets)
    {
        effPresets = null;
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[EnemyPresetSweep] Play Mode で実行してください");
            return false;
        }
        if (IsBenchmarkRunning)
        {
            Debug.LogWarning("[EnemyPresetSweep] すでにベンチマーク実行中です");
            return false;
        }
        if (enemyPresetConfig == null)
        {
            Debug.LogWarning("[EnemyPresetSweep] enemyPresetConfig (EnemySpawnPresetCollection) が未割り当てです");
            return false;
        }
        effPresets = enemyPresetConfig.items;
        if (effPresets == null || effPresets.Length == 0)
        {
            Debug.LogWarning("[EnemyPresetSweep] enemyPresetConfig.items が空です（SOにプリセットを追加してください）");
            return false;
        }
        return true;
    }

    public async UniTask RunEnemyPresetSweepBenchmark()
    {
        if (!this.ValidateEnemyPresetSweepPrerequisites(out var effPresets))
            return;

        // Sweep前のズーム状態を保持（後で復元）
        bool prevZoom = this.enableZoomAnimation;

        IsBenchmarkRunning = true;
        try
        {
            // ズーム一時停止（任意）
            bool zoomSuppressed = false;
            if (enemySweepDisableZoom && this.enableZoomAnimation)
            {
                this.enableZoomAnimation = false;
                zoomSuppressed = true;
            }

            int repeat = Mathf.Max(1, enemyPresetConfig.repeatCount);
            float interDelay = enemyPresetConfig.interRunDelaySec;
            int total = effPresets.Length * repeat;
            Debug.Log($"[EnemyPresetSweep] Start {effPresets.Length} presets x {repeat} runs (total {total})");

            // フォーマッター作成
            var formatters = this.CreateBenchmarkFormatters();
            // シナリオ生成 & ヘッダ出力（TMPは日時の代わりにコレクション名を先頭に表示）
            var scenario = (scenarioSelector != null ? scenarioSelector.CreateSelectedScenario() : null) ?? new global::WalkOneStepScenario();
            string scenarioName = scenario.Name;
            string presetCollectionName = enemyPresetConfig != null ? enemyPresetConfig.name : "-";
            AppendPresetLogLine(formatters.tmpFormatter.Header(effPresets.Length, repeat, scenarioName, presetCollectionName));
            if (formatters.useCsv)  formatters.csvSb.AppendLine(formatters.csvFormatter.Header(effPresets.Length, repeat, scenarioName, presetCollectionName));
            if (formatters.useJson) formatters.jsonSb.AppendLine(formatters.jsonFormatter.Header(effPresets.Length, repeat, scenarioName, presetCollectionName));
            if (formatters.usePerRunCsv)  formatters.perRunCsvSb.AppendLine(formatters.perRunCsvFormatter.Header(effPresets.Length, repeat, scenarioName, presetCollectionName));
            if (formatters.usePerRunJson) formatters.perRunJsonSb.AppendLine(formatters.perRunJsonFormatter.Header(effPresets.Length, repeat, scenarioName, presetCollectionName));

            // per-run ストリーム書き出し（任意）
            int flushEvery = (outputSettings != null) ? outputSettings.perRunFlushEvery : 0;
            bool perRunStream = formatters.writeToFile && flushEvery > 0 && (formatters.usePerRunCsv || formatters.usePerRunJson);
            string dirForRuns = string.Empty, tsRuns = string.Empty, baseRunsName = string.Empty, pathRunsCsv = string.Empty, pathRunsJson = string.Empty;
            if (perRunStream)
            {
                dirForRuns = string.IsNullOrEmpty(formatters.outputFolder) ? Application.persistentDataPath : formatters.outputFolder;
                Directory.CreateDirectory(dirForRuns);
                tsRuns = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                baseRunsName = formatters.fileBaseName + "_runs";
                if (formatters.usePerRunCsv)
                {
                    pathRunsCsv = ResolveOutputPath(dirForRuns, baseRunsName, scenarioName, presetCollectionName, tsRuns, repeat, effPresets.Length, "csv");
                    File.WriteAllText(pathRunsCsv, formatters.perRunCsvFormatter.Header(effPresets.Length, repeat, scenarioName, presetCollectionName) + "\n", Encoding.UTF8);
                }
                if (formatters.usePerRunJson)
                {
                    pathRunsJson = ResolveOutputPath(dirForRuns, baseRunsName, scenarioName, presetCollectionName, tsRuns, repeat, effPresets.Length, "json");
                    File.WriteAllText(pathRunsJson, formatters.perRunJsonFormatter.Header(effPresets.Length, repeat, scenarioName, presetCollectionName) + "\n", Encoding.UTF8);
                }
            }

            // 集計（フッタ用）
            int sinkRows = 0;
            int sinkOkSum = 0;
            int sinkTotalSum = 0;
            int perRunRows = 0;
            int perRunOk = 0;

            // 実行
            var applier  = new global::EnemySpawnSettingsApplier(this);
            var startedAt = System.DateTime.Now;
            _sweepCts?.Dispose();
            _sweepCts = new System.Threading.CancellationTokenSource();
            _lastSweepProgress = new SweepProgressSnapshot
            {
                PresetIndex = 0,
                PresetCount = effPresets.Length,
                RunIndex = 0,
                RunCount = repeat,
                CompletedRuns = 0,
                TotalRuns = total,
                StartedAt = startedAt,
                ElapsedSec = 0,
                ETASec = 0,
            };
            var progress = this.CreateProgressReporter(startedAt, total, effPresets.Length);
            await global::BenchmarkRunner.RunPresetSweepAsync(
                scenario,
                effPresets,
                applier,
                repeat,
                interDelay,
                formatters.tmpFormatter,
                AppendPresetLogLine,
                (EnemySpawnPreset p, global::BenchmarkSummary s) =>
                {
                    if (formatters.useCsv)  formatters.csvSb.AppendLine(formatters.csvFormatter.SummaryLine(p, s));
                    if (formatters.useJson) formatters.jsonSb.AppendLine(formatters.jsonFormatter.SummaryLine(p, s));
                    sinkRows++;
                    sinkOkSum   += s.SuccessCount;
                    sinkTotalSum += s.RequestCount;
                },
                (int runIndex, EnemySpawnPreset p, global::BenchmarkRunResult r) =>
                {
                    // Per-run 明細
                    string sc = global::BenchmarkContext.Current?.ScenarioName ?? scenarioName;
                    int pi = global::BenchmarkContext.Current?.PresetIndex ?? -1;
                    string ps = global::BenchmarkContext.Current?.PresetSummary ?? "-";
                    string tagsStr = FormatContextTags(global::BenchmarkContext.Current?.Tags);
                    if (formatters.usePerRunCsv)
                    {
                        var line = formatters.perRunCsvFormatter.RunLine(sc, pi, ps, runIndex, r, tagsStr);
                        if (perRunStream && !string.IsNullOrEmpty(pathRunsCsv))
                            File.AppendAllText(pathRunsCsv, line + "\n", Encoding.UTF8);
                        else
                            formatters.perRunCsvSb?.AppendLine(line);
                    }
                    if (formatters.usePerRunJson)
                    {
                        var line = formatters.perRunJsonFormatter.RunLine(sc, pi, ps, runIndex, r, tagsStr);
                        if (perRunStream && !string.IsNullOrEmpty(pathRunsJson))
                            File.AppendAllText(pathRunsJson, line + "\n", Encoding.UTF8);
                        else
                            formatters.perRunJsonSb?.AppendLine(line);
                    }
                    perRunRows++;
                    if (r != null && r.Success) perRunOk++;
                },
                // Tags: プリセット値 + ズーム抑制状態
                (EnemySpawnPreset p) =>
                {
                    var dict = new System.Collections.Generic.Dictionary<string, string>(8);
                    dict["thr"] = p.throttleEnemySpawns ? "true" : "false";
                    dict["bs"] = Mathf.Max(1, p.enemySpawnBatchSize).ToString();
                    dict["if"] = Mathf.Max(0, p.enemySpawnInterBatchFrames).ToString();
                    dict["vlog"] = p.enableVerboseEnemyLogs ? "true" : "false";
                    dict["zoom"] = zoomSuppressed ? "off" : (this.enableZoomAnimation ? "on" : "off");
                    return dict;
                },
                progress,
                _sweepCts.Token);

            // まとめて保存
            this.SaveBenchmarkResults(
                formatters,
                scenarioName,
                presetCollectionName,
                repeat,
                effPresets.Length,
                sinkRows,
                sinkOkSum,
                sinkTotalSum,
                perRunRows,
                perRunOk,
                pathRunsCsv,
                pathRunsJson,
                perRunStream);
        }
        finally
        {
            IsBenchmarkRunning = false;
            // ズーム設定の復元
            this.enableZoomAnimation = prevZoom;
            _sweepCts?.Dispose();
            _sweepCts = null;
            Debug.Log("[EnemyPresetSweep] Finished");
            var formatter = new global::TmpSummaryFormatter();
            AppendPresetLogLine(formatter.Footer());
        }
    }

}
