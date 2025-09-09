using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if !UNITY_EDITOR
// no editor specific usings
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Threading;
using Unity.Profiling;
using System.IO;

/// <summary>
/// WatchUIUpdateクラスに戦闘画面用のレイヤー分離システムを追加しました。
/// 背景と敵は一緒にズームし、味方アイコンは独立してスライドインします。
/// 戦闘エリアはズーム後の座標系で直接デザイン可能です。
/// </summary>
public partial class WatchUIUpdate : MonoBehaviour
{
    // シングルトン参照
    public static WatchUIUpdate Instance { get; private set; }

    // プロファイラーマーカー（HUDでも参照できる固定名）
    private static readonly ProfilerMarker kPrepareIntro = new ProfilerMarker("WUI.PrepareIntro");
    private static readonly ProfilerMarker kPlayIntro    = new ProfilerMarker("WUI.PlayIntro");
    private static readonly ProfilerMarker kPlaceEnemies = new ProfilerMarker("WUI.PlaceEnemies");

    // 導入メトリクス（HUD表示用）
    public sealed class IntroMetricsData
    {
        public double PlannedMs;   // 理論所要（ズーム/スライド/段差+preDelay）
        public double ActualMs;    // 実測（Play開始〜完了）
        public double DelayMs;     // 実測-理論（0未満は0として扱う想定）
        public double EnemyPlacementMs; // 敵UI配置の実測（PlaceEnemiesFromBattleGroup全体）
        public System.DateTime Timestamp; // 計測時刻
        public int AllyCount;
        public int EnemyCount;
        // アニメーション滑らかさ（Play中のみのフレーム時間分布）
        public double IntroFrameAvgMs;   // 平均フレーム時間
        public double IntroFrameP95Ms;   // 95パーセンタイル
        public double IntroFrameMaxMs;   // 最大フレーム時間
    }

    /// <summary>
    /// Orchestrator 経由でズーム原状復帰を行う任意呼び出しの導線。
    /// トグルOFF時は従来の RestoreOriginalTransforms にフォールバックします。
    /// </summary>
    public async UniTask RestoreZoomViaOrchestrator(bool animated = false, float duration = 0f)
    {
        try
        {
            var token = _sweepCts != null ? _sweepCts.Token : System.Threading.CancellationToken.None;
            EnsureOrchestrator();
            var ictx = BuildIntroContextForOrchestrator();
            Debug.Log($"[WatchUIUpdate] RestoreZoomViaOrchestrator animated={animated} duration={duration}");
            await _orchestrator.RestoreAsync(ictx, animated, duration, token);
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("[WatchUIUpdate] RestoreZoomViaOrchestrator canceled");
        }
    }

    

    private void EnsureOrchestrator()
    {
        if (_orchestrator == null)
        {
            // ズーム外出しは次フェーズ。配置はアダプタで既存実装へ委譲
            var zoom = new WuiZoomControllerAdapter();
            _orchestrator = new DefaultIntroOrchestrator(new WuiEnemyPlacerAdapter(), zoom);
        }
    }

    private IIntroContext BuildIntroContextForOrchestrator()
    {
        var bc = BenchmarkContext.Current;
        string sc = bc?.ScenarioName ?? (scenarioSelector != null ? (scenarioSelector.CreateSelectedScenario()?.Name ?? "-") : "-");
        int pi = bc?.PresetIndex ?? -1;
        string ps = bc?.PresetSummary ?? BuildMetricsContext()?.PresetSummary ?? "-";
        var tags = bc?.Tags;
        var frontRect = zoomFrontContainer as RectTransform;
        var backRect  = zoomBackContainer  as RectTransform;
        return new IntroContext(
            sc,
            pi,
            ps,
            tags,
            enemySpawnArea,
            frontRect,
            backRect,
            _gotoScaleXY,
            _gotoPos,
            _firstZoomSpeedTime,
            _firstZoomAnimationCurve
        );
    }

    private IEnemyPlacementContext BuildPlacementContext(BattleGroup enemyGroup)
    {
        int count = enemyGroup?.Ours != null ? enemyGroup.Ours.Count : 0;
        // 既存実装はBatchActivateを内部で持つため、ここではtrueをヒントとして渡す（現状アダプタ側では未使用）
        return new EnemyPlacementContext(enemyGroup, count, batchActivate: true, fixedSizeOverride: null);
    }

    // ===== ベンチ進捗（HUD 用） =====
    public sealed class SweepProgressSnapshot
    {
        public int PresetIndex;    // 0-based（完了レポートでは presets まで到達することあり）
        public int PresetCount;
        public int RunIndex;       // 1-based
        public int RunCount;       // 各プリセットの総ラン数
        public int CompletedRuns;  // 全体で完了したラン数
        public int TotalRuns;      // 全体の総ラン数
        public DateTime StartedAt;
        public double ElapsedSec;
        public double ETASec;
    }
    private SweepProgressSnapshot _lastSweepProgress;
    public SweepProgressSnapshot LastSweepProgress => _lastSweepProgress;

    // スイープのキャンセル制御
    private CancellationTokenSource _sweepCts;

    // ===== SO 構成状態（UI連携用の公開プロパティ） =====
    public bool HasPresetConfig => (presetConfig != null && presetConfig.items != null && presetConfig.items.Length > 0);
    public bool HasOutputSettings => (outputSettings != null);
    public bool HasMetricsSettings => (metricsSettings != null);

    private IntroMetricsData _lastIntroMetrics = new IntroMetricsData();
    public IntroMetricsData LastIntroMetrics => _lastIntroMetrics;

    // Walk全体（ボタン押下→Walk完了）計測
    public sealed class WalkMetricsData
    {
        public double TotalMs;             // Walk() 全体の実測
        public DateTime Timestamp;  // 計測時刻
    }
    private WalkMetricsData _lastWalkMetrics = new WalkMetricsData();
    public WalkMetricsData LastWalkMetrics => _lastWalkMetrics;
    private System.Diagnostics.Stopwatch _walkSw;

    public void BeginWalkCycleTiming()
    {
        if (_walkSw == null) _walkSw = new System.Diagnostics.Stopwatch();
        _walkSw.Reset();
        _walkSw.Start();
    }

    public void EndWalkCycleTiming()
    {
        if (_walkSw == null || !_walkSw.IsRunning) return;
        _walkSw.Stop();
        _lastWalkMetrics.TotalMs = _walkSw.Elapsed.TotalMilliseconds;
        _lastWalkMetrics.Timestamp = System.DateTime.Now;
        // MetricsHubへ記録（コンテキストは現在のIntro設定を要約）
        try
        {
            var ctx = BuildMetricsContext();
            global::MetricsHub.Instance.RecordWalk(new global::WalkMetricsEvent
            {
                TotalMs = _lastWalkMetrics.TotalMs,
                Timestamp = _lastWalkMetrics.Timestamp,
                Context = ctx,
            });
        }
        catch { /* no-op */ }
    }

    // ===== ベンチマーク設定／結果 =====
    [Header(
        "ベンチマーク（単一設定×回数）\n" +
        "・このセクションはSO非依存です（Preset/Output/Metrics のSOとは別系統）。\n" +
        "・手早く1種類の実処理（Walk(1)）をN回回して平均を確認したいときに使用します。\n" +
        "・プリセットスイープ（SO必須）とは出力や実行系が独立しています。")]
    [Tooltip("1プリセットにつき何回繰り返すか（大きいほど平均が安定します）。")]
    [SerializeField] private int benchmarkRepeatCount = 5;           // 反復回数
    [Tooltip("各回のあいだに挟む待機時間（秒）。0で連続実行。状態詰まりの緩和に少量の待機を推奨。")]
    [SerializeField] private float benchmarkInterRunDelaySec = 0.0f; // 反復間の待機（秒）
    public bool IsBenchmarkRunning { get; private set; } = false;

    [Header(
        "Metrics 設定（SO 必須）\n" +
        "・MetricsSettings を割り当ててください（未割り当て時は計測を自動で全無効）。\n" +
        "・本番ビルド等でオーバーヘッドを避けたい場合は SO 側でOFFにできます。\n" +
        "・個別（Span/Jitter）もSO側で切替可能です。")]
    [Tooltip("Metrics 設定のSO。未割り当ての場合、Metricsは無効化されます。")]
    [SerializeField] private MetricsSettings metricsSettings;

    public sealed class BenchMetricsData
    {
        public int Requested;          // 要求反復数
        public int SuccessCount;       // 成功数
        public int FailCount;          // 失敗数
        public double AvgActualMs;     // A の平均
        public double AvgWalkTotalMs;  // W の平均
        public double AvgJitAvgMs;     // IntroFrameAvg の平均
        public double AvgJitP95Ms;     // IntroFrameP95 の平均
        public double AvgJitMaxMs;     // IntroFrameMax の平均
        public System.DateTime Timestamp; // 計測完了時刻
        public float InterDelaySec;    // 反復待機秒
    }

    private BenchMetricsData _lastBenchMetrics = null;
    public BenchMetricsData LastBenchMetrics => _lastBenchMetrics;

    [ContextMenu("Run Benchmark Now (Walk xN)")]
    public async void RunBenchmarkNowContext()
    {
        await RunBenchmarkNow();
    }

    // UIのOnClickなどから直接呼べるvoidラッパー
    public void StartBenchmarkNow()
    {
        RunBenchmarkNow().Forget();
    }

    public async UniTask RunBenchmarkNow()
    {
        IsBenchmarkRunning = true;
        try
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Benchmark] Play Mode で実行してください（Editor停止中は実処理を回せません）");
                return;
            }
            int req = Mathf.Max(1, benchmarkRepeatCount);
            var scenario = (scenarioSelector != null ? scenarioSelector.CreateSelectedScenario() : null) ?? new global::WalkOneStepScenario();
            var summary = await global::BenchmarkRunner.RunRepeatAsync(
                scenario,
                req,
                benchmarkInterRunDelaySec,
                0,
                1,
                onEachRun: null,
                progress: null,
                CancellationToken.None);

            _lastBenchMetrics = new BenchMetricsData
            {
                Requested      = summary.RequestCount,
                SuccessCount   = summary.SuccessCount,
                FailCount      = summary.FailCount,
                AvgActualMs    = summary.AvgActualMs,
                AvgWalkTotalMs = summary.AvgWalkTotalMs,
                AvgJitAvgMs    = summary.AvgIntroAvgMs,
                AvgJitP95Ms    = summary.AvgIntroP95Ms,
                AvgJitMaxMs    = summary.AvgIntroMaxMs,
                Timestamp      = System.DateTime.Now,
                InterDelaySec  = benchmarkInterRunDelaySec,
            };
        }
        finally
        {
            IsBenchmarkRunning = false;
        }
    }

    // ===== プリセットスイープ（配列の各設定 × 指定回数 実行し、行ごとにログ追記） =====
    [System.Serializable]
    public struct IntroPreset
    {
        public bool introYieldDuringPrepare;
        public int introYieldEveryN;
        public float introPreAnimationDelaySec;
        public float introSlideStaggerInterval;
    }

    [Header(
        "ベンチプリセットスイープ（SO 必須）\n" +
        "・手順: 1) IntroPresetCollection を作成→ items, repeatCount, interRunDelaySec を設定\n" +
        "          2) 本フィールドに割り当て\n" +
        "          3) （任意）BenchmarkOutputSettings を割り当ててCSV/JSONを有効化\n" +
        "          4) 実行（StartPresetSweep/ContextMenu）\n" +
        "・依存: MetricsSettings が割当済みなら計測ON/OFF/種別が反映されます（任意）。\n" +
        "・結果: 画面のTMP（任意）へ1行サマリ、（任意）CSV/JSONの生成と保存。")]
    [Tooltip("プリセット/回数/待機のSO。未割り当ての場合は実行できません。")]
    [SerializeField] private IntroPresetCollection presetConfig;

    [Header(
        "ベンチログ表示（任意・TMP出力）\n" +
        "・TMP(TextMeshProUGUI) を割り当てると、プリセットごとの平均結果を1行ずつ表示します。\n" +
        "・未割り当てなら画面表示はスキップ（Consoleには出力します）。")]
    [Tooltip("画面上にログを出す先のTextMeshProUGUI。未指定なら画面表示はスキップ（Consoleのみ）。")]
    [SerializeField] private TextMeshProUGUI presetLogTMP;
    [Tooltip("画面に保持する最大行数。超過すると古い行から自動で削除します。")]
    [SerializeField] private int presetLogMaxLines = 400;
    private readonly List<string> _presetLogBuffer = new List<string>(512);

    [Header(
        "ベンチ出力（SO 推奨）\n" +
        "・BenchmarkOutputSettings を割り当てると、CSV/JSONの生成やファイル保存が可能になります。\n" +
        "・enableCsv/enableJson=true で内部バッファ生成、writeToFile=true でファイル保存。\n" +
        "・outputFolder が空のときは Application.persistentDataPath に保存します。\n" +
        "・未割り当ての場合は出力なし（画面のTMPのみ）。")]
    [Tooltip("出力設定のSO。未割り当ての場合はCSV/JSON出力は行いません。")]
    [SerializeField] private BenchmarkOutputSettings outputSettings;

    [Header("シナリオ選択（任意）")]
    [Tooltip("UIから切替できる ScenarioSelector。未割り当てなら Walk(1) を使用します。")]
    [SerializeField] private ScenarioSelector scenarioSelector;

    // Orchestrator（I/F注入）。当面は内部でデフォルト生成し、段階的に移行する
    private global::IIntroOrchestrator _orchestrator;

    

    private void AppendPresetLogLine(string line)
    {
        _presetLogBuffer.Add(line);
        if (presetLogMaxLines > 0 && _presetLogBuffer.Count > presetLogMaxLines)
        {
            int remove = _presetLogBuffer.Count - presetLogMaxLines;
            _presetLogBuffer.RemoveRange(0, remove);
        }
        if (presetLogTMP != null)
        {
            presetLogTMP.text = string.Join("\n", _presetLogBuffer);
        }
    }

    // ファイル名用に不正文字を '_' に置換し、空や空白のみの場合は "-" を返す
    private static string SanitizeForFile(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "-";
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            bool bad = false;
            for (int j = 0; j < invalid.Length; j++)
            {
                if (c == invalid[j]) { bad = true; break; }
            }
            sb.Append(bad ? '_' : c);
        }
        return sb.ToString();
    }

    // 出力ファイル名テンプレートを適用し、必要に応じてサブフォルダを作成したフルパスを返す
    private string ResolveOutputPath(string dir, string baseName, string scenarioName, string presetCollectionName, string timestamp, int repeat, int presets, string ext)
    {
        string template = (outputSettings != null) ? outputSettings.fileNameTemplate : string.Empty;
        bool allowSub = (outputSettings != null) ? outputSettings.createSubfolders : true;
        string safeBase = SanitizeForFile(baseName);
        string safeSc = SanitizeForFile(scenarioName);
        string safePc = SanitizeForFile(presetCollectionName);

        string fileName;
        if (string.IsNullOrEmpty(template))
        {
            fileName = $"{safeBase}_{safeSc}_{safePc}_{timestamp}";
        }
        else
        {
            fileName = template
                .Replace("{base}", safeBase)
                .Replace("{scenario}", safeSc)
                .Replace("{presetCol}", safePc)
                .Replace("{ts}", timestamp)
                .Replace("{repeat}", repeat.ToString())
                .Replace("{presets}", presets.ToString());
            if (!allowSub)
            {
                fileName = fileName.Replace('/', '_').Replace('\\', '_');
            }
        }
        string full = System.IO.Path.Combine(dir, fileName + "." + ext);
        string targetDir = System.IO.Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(targetDir)) System.IO.Directory.CreateDirectory(targetDir);
        return full;
    }

    public struct IntroSettingsSnapshot
    {
        public bool introYieldDuringPrepare;
        public int introYieldEveryN;
        public float introPreAnimationDelaySec;
        public float introSlideStaggerInterval;
    }

    public IntroSettingsSnapshot SaveCurrentIntroSettings()
    {
        return new IntroSettingsSnapshot
        {
            introYieldDuringPrepare = this.introYieldDuringPrepare,
            introYieldEveryN = this.introYieldEveryN,
            introPreAnimationDelaySec = this.introPreAnimationDelaySec,
            introSlideStaggerInterval = this.introSlideStaggerInterval,
        };
    }

    public void ApplyIntroPreset(IntroPreset p)
    {
        this.introYieldDuringPrepare = p.introYieldDuringPrepare;
        this.introYieldEveryN = Mathf.Max(1, p.introYieldEveryN);
        this.introPreAnimationDelaySec = Mathf.Max(0f, p.introPreAnimationDelaySec);
        this.introSlideStaggerInterval = Mathf.Max(0f, p.introSlideStaggerInterval);
    }

    public void RestoreIntroSettings(IntroSettingsSnapshot s)
    {
        this.introYieldDuringPrepare = s.introYieldDuringPrepare;
        this.introYieldEveryN = s.introYieldEveryN;
        this.introPreAnimationDelaySec = s.introPreAnimationDelaySec;
        this.introSlideStaggerInterval = s.introSlideStaggerInterval;
    }

    [ContextMenu("Run Preset Sweep (All)")]
    public async void RunPresetSweepContext()
    {
        await RunPresetSweepBenchmark();
    }

    // UIのOnClickなどから直接呼べるvoidラッパー
    public void StartPresetSweep()
    {
        RunPresetSweepBenchmark().Forget();
    }

    [ContextMenu("Cancel Preset Sweep (If Running)")]
    public void CancelPresetSweep()
    {
        if (_sweepCts != null && !_sweepCts.IsCancellationRequested)
        {
            _sweepCts.Cancel();
            Debug.Log("[PresetSweep] Cancel requested");
        }
        else
        {
            Debug.Log("[PresetSweep] Not running or already cancelled");
        }
    }

    public async UniTask RunPresetSweepBenchmark()
    {
        // 検証
        if (!this.ValidatePresetSweepPrerequisites(out var effPresets))
            return;

        IsBenchmarkRunning = true;
        try
        {
            int repeat = Mathf.Max(1, presetConfig.repeatCount);
            float interDelay = presetConfig.interRunDelaySec;
            int total = effPresets.Length * repeat;
            Debug.Log($"[PresetSweep] Start {effPresets.Length} presets x {repeat} runs (total {total})");

            // フォーマッター作成
            var formatters = this.CreateBenchmarkFormatters();
            AppendPresetLogLine(formatters.tmpFormatter.Header(effPresets.Length, repeat));
            // シナリオ生成 & ヘッダにシナリオ名とプリセットコレクション名を付与
            var scenario = (scenarioSelector != null ? scenarioSelector.CreateSelectedScenario() : null) ?? new global::WalkOneStepScenario();
            string scenarioName = scenario.Name;
            string presetCollectionName = presetConfig != null ? presetConfig.name : "-";
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

            // 集計（フッタに総OK/総リクエスト/行数を出力するため）
            int sinkRows = 0;
            int sinkOkSum = 0;
            int sinkTotalSum = 0;
            int perRunRows = 0;
            int perRunOk = 0;

            // 実行（上で生成した scenario を使用）
            var applier  = new global::IntroSettingsApplier(this);
            // 進捗初期化
            var startedAt = System.DateTime.Now;
            // キャンセルトークン準備
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
                (global::WatchUIUpdate.IntroPreset p, global::BenchmarkSummary s) =>
                {
                    if (formatters.useCsv)  formatters.csvSb.AppendLine(formatters.csvFormatter.SummaryLine(p, s));
                    if (formatters.useJson) formatters.jsonSb.AppendLine(formatters.jsonFormatter.SummaryLine(p, s));
                    sinkRows++;
                    sinkOkSum   += s.SuccessCount;
                    sinkTotalSum += s.RequestCount;
                },
                (int runIndex, global::WatchUIUpdate.IntroPreset p, global::BenchmarkRunResult r) =>
                {
                    this.ProcessPerRunResult(
                        formatters,
                        runIndex,
                        p,
                        r,
                        scenarioName,
                        pathRunsCsv,
                        pathRunsJson,
                        perRunStream,
                        ref perRunRows,
                        ref perRunOk);
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
            _sweepCts?.Dispose();
            _sweepCts = null;
            Debug.Log("[PresetSweep] Finished");
            var formatter = new global::TmpSummaryFormatter();
            AppendPresetLogLine(formatter.Footer());
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        // Metrics トグルを適用
        ApplyMetricsToggles();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyMetricsToggles();
    }
#endif

    private void ApplyMetricsToggles()
    {
        bool enabled = (metricsSettings != null) ? metricsSettings.enableMetrics : false;
        bool span    = (metricsSettings != null) ? metricsSettings.enableSpan     : false;
        bool jitter  = (metricsSettings != null) ? metricsSettings.enableJitter   : false;
        global::MetricsHub.Enabled = enabled;
        global::MetricsHub.EnableSpan = span;
        global::MetricsHub.EnableJitter = jitter;
    }

    // ===== Kモード: パッシブ一覧表示（フェードイン） =====
    private BaseStates FindActorByUI(UIController ui)
    {
        var bm = Walking.Instance?.bm;
        var all = bm?.AllCharacters;
        if (ui == null || all == null) return null;
        foreach (var ch in all)
        {
            if (ch != null && ch.UI == ui) return ch;
        }
        return null;
    }

    private void SetKPassivesText(BaseStates actor)
    {
        if (kPassivesText == null) return;
        // TMP取得＆設定をヘルパで実施
        _kPassivesTMP = GetOrSetupTMPForBackground(kPassivesText, _kPassivesTMP, kPassivesUseRectMask);
        // 計測のためにオブジェクトを一時可視化（アルファ0で非表示）し、レイアウトを確定
        var go = kPassivesText.gameObject;
        var cg0 = go.GetComponent<CanvasGroup>();
        if (cg0 == null) cg0 = go.AddComponent<CanvasGroup>();
        go.SetActive(true);
        cg0.alpha = 0f;
        Canvas.ForceUpdateCanvases();
        string tokens = kPassivesDebugMode
            ? BuildDummyKPassivesTokens(kPassivesDebugCount, kPassivesDebugPrefix)
            : BuildKPassivesTokens(actor);
        _kPassivesTokensRaw = tokens ?? string.Empty;
        // RectTransform 内に収まるように末尾をカットして "••••" を付与
        var fitted = FitTextIntoRectWithEllipsis(
            _kPassivesTokensRaw,
            kPassivesText,
            Mathf.Max(1, kPassivesEllipsisDotCount),
            Mathf.Max(0f, kPassivesFitSafety),
            kPassivesAlwaysAppendEllipsis
        );
        // リッチテキスト無効なので、そのまま <> を表示
        kPassivesText.text = fitted;
        // 背景更新
        kPassivesText.RefreshBackground();
        // 表示はフェード側で行う。ここでは非表示に保つ。
        // alphaは0のままにしておく
    }

    private string BuildKPassivesTokens(BaseStates actor)
    {
        if (actor == null || actor.Passives == null || actor.Passives.Count == 0)
        {
            Debug.LogWarning("actor or actor.Passives is null or empty.");
            return string.Empty;
        }
        var list = actor.Passives;
        var sb = new StringBuilder();
        bool first = true;
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            if (p == null) continue;
            string raw = string.IsNullOrWhiteSpace(p.SmallPassiveName) ? p.ID.ToString() : p.SmallPassiveName;
            // <noparse> で包むため、エスケープ不要。トークン間は半角スペース1個。
            string token = $"<{raw}>";
            if (!first) sb.Append(' ');
            sb.Append(token);
            first = false;
        }
        return sb.ToString();
    }

    // デバッグ用のダミーパッシブトークンを生成（実データは変更しない）
    private string BuildDummyKPassivesTokens(int count, string prefix)
    {
        if (count <= 0) return string.Empty;
        var sb = new StringBuilder();
        bool first = true;
        for (int i = 1; i <= count; i++)
        {
            string raw = $"{prefix}{i}";
            string token = $"<{raw}>";
            if (!first) sb.Append(' ');
            sb.Append(token);
            first = false;
        }
        return sb.ToString();
    }

    private async UniTask FadeInKPassives(BaseStates actor, CancellationToken ct)
    {
        if (kPassivesText == null) return;
        var go = kPassivesText.gameObject;
        if (string.IsNullOrEmpty(kPassivesText.text))
        {
            go.SetActive(false);
            return;
        }
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        go.SetActive(true);
        // 背景更新（可視化直後）
        kPassivesText.RefreshBackground();

        // レイアウトが落ち着くのを待ってから最終フィット（初回サイズ未確定対策）
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        Canvas.ForceUpdateCanvases();
        string baseTokens = string.IsNullOrEmpty(_kPassivesTokensRaw) ? (kPassivesText.text ?? string.Empty) : _kPassivesTokensRaw;
        var finalFitted = FitTextIntoRectWithEllipsis(
            baseTokens,
            kPassivesText,
            Mathf.Max(1, kPassivesEllipsisDotCount),
            Mathf.Max(0f, kPassivesFitSafety),
            kPassivesAlwaysAppendEllipsis
        );
        if (!string.Equals(finalFitted, kPassivesText.text, StringComparison.Ordinal))
        {
            kPassivesText.text = finalFitted;
            kPassivesText.RefreshBackground();
        }
        var t = LMotion.Create(0f, 1f, kPassivesFadeDuration)
            .WithEase(Ease.OutCubic)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(a => cg.alpha = a)
            .ToUniTask(ct);
        try
        {
            await t;
        }
        catch (OperationCanceledException)
        {
            // 即時解除など
        }
    }

    // ========= Kパッシブテキストのフィット計算 =========
    private string FitTextIntoRectWithEllipsis(string src, TMPTextBackgroundImage textBg, int dotCount, float safety, bool alwaysAppendEllipsis)
    {
        if (string.IsNullOrEmpty(src) || textBg == null) return string.Empty;

        var tmp = _kPassivesTMP != null ? _kPassivesTMP : (textBg.rectTransform != null ? textBg.rectTransform.GetComponent<TMP_Text>() : null);
        if (tmp == null)
        {
            tmp = textBg.GetComponentInChildren<TMP_Text>(true);
        }
        if (tmp == null) return src;

        string ellipsis = new string('•', Mathf.Max(1, dotCount));

        var tmpRT = tmp.rectTransform;
        string original = tmp.text;

        bool Fits(string candidate)
        {
            // レイアウト最新化
            Canvas.ForceUpdateCanvases();
            var containerRT = textBg.transform as RectTransform; // 親コンテナ
            var containerRect = containerRT != null ? containerRT.rect : tmpRT.rect;
            // 一時的に子TMPを親サイズに合わせて計測（戻す）
            float ow = tmpRT.rect.width;
            float oh = tmpRT.rect.height;
            tmpRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, containerRect.width);
            tmpRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, containerRect.height);
            bool prevRich = tmp.richText;
            tmp.richText = false; // 表示と同条件
            tmp.text = candidate;
            tmp.ForceMeshUpdate();
            // 折り返しを考慮した推奨高さで判定
            float height = tmp.preferredHeight;
            bool ok = height <= containerRect.height - safety;
            // 元に戻す
            tmpRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ow);
            tmpRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, oh);
            tmp.richText = prevRich;
            return ok;
        }

        // まずフルで収まるか、収まらなくても末尾に••••を付けた場合の適合を確認
        bool srcFits = Fits(src);
        bool srcWithDotsFits = Fits(src + ellipsis);
        if (alwaysAppendEllipsis)
        {
            if (srcWithDotsFits)
            {
                tmp.text = original; // 元に戻す
                return src + ellipsis;
            }
        }
        else
        {
            if (srcFits)
            {
                tmp.text = original; // 元に戻す
                return src;
            }
            if (srcWithDotsFits)
            {
                tmp.text = original; // 元に戻す
                return src + ellipsis;
            }
        }

        // 2分探索で最大長を探索（末尾に省略記号を付けて測定）
        int lo = 0, hi = src.Length, bestLen = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            string cand = src.Substring(0, mid);
            cand = AvoidLoneOpeningBracket(cand);
            string composed = cand + ellipsis;
            if (Fits(composed))
            {
                bestLen = cand.Length;
                lo = mid + 1; // もっと伸ばせる
            }
            else
            {
                hi = mid - 1; // 短くする
            }
        }

        string best = src.Substring(0, Mathf.Min(bestLen, src.Length));
        best = AvoidLoneOpeningBracket(best);
        string result = best.Length > 0 ? best + ellipsis : string.Empty;

        // 念のための最終安全網：まだはみ出す場合は空白境界ごとにさらに詰める
        int guard = 0;
        while (!string.IsNullOrEmpty(result) && !Fits(result) && guard++ < 128)
        {
            // 末尾の省略記号を外して再カット
            string withoutDots = result.EndsWith(ellipsis) ? result.Substring(0, result.Length - ellipsis.Length) : result;
            int prevSpace = withoutDots.LastIndexOf(' ');
            if (prevSpace <= 0)
            {
                // これ以上切れない場合は1文字ずつ
                withoutDots = withoutDots.Length > 0 ? withoutDots.Substring(0, withoutDots.Length - 1) : string.Empty;
            }
            else
            {
                withoutDots = withoutDots.Substring(0, prevSpace);
            }
            withoutDots = AvoidLoneOpeningBracket(withoutDots);
            result = string.IsNullOrEmpty(withoutDots) ? string.Empty : withoutDots + ellipsis;
        }

        // バイナリサーチの結果が空（= 先頭すら確保できない）場合のフォールバック: トークン単位で詰める
        if (string.IsNullOrEmpty(result))
        {
            var tokens = src.Split(' ');
            var acc = new StringBuilder();
            for (int i = 0; i < tokens.Length; i++)
            {
                string next = tokens[i];
                string trial = acc.Length == 0 ? next + ellipsis : acc.ToString() + " " + next + ellipsis;
                if (Fits(trial))
                {
                    if (acc.Length > 0) acc.Append(' ');
                    acc.Append(next);
                }
                else
                {
                    break;
                }
            }
            result = acc.Length == 0 ? ellipsis : acc.ToString() + ellipsis;
        }

        tmp.text = original; // 元に戻す（呼出側で最終テキストを設定する）
        return result;
    }

    // ---- Helper: TMP取得＆設定（背景コンテナサイズに合わせる、必要ならRectMask2D付加） ----
    private TMP_Text GetOrSetupTMPForBackground(TMPTextBackgroundImage bg, TMP_Text cache, bool addRectMask)
    {
        if (bg == null) return null;
        if (cache == null)
        {
            cache = bg.rectTransform != null ? bg.rectTransform.GetComponent<TMP_Text>() : null;
            if (cache == null)
            {
                cache = bg.GetComponentInChildren<TMP_Text>(true);
            }
        }
        if (cache == null) return null;

        cache.enableWordWrapping = true;
        cache.overflowMode = TextOverflowModes.Overflow;
        cache.richText = false;
        cache.enableAutoSizing = false;
        cache.alignment = TextAlignmentOptions.TopLeft;

        var contRT = bg.transform as RectTransform;
        var childRT = cache.rectTransform;
        if (contRT != null && childRT != null)
        {
            childRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contRT.rect.width);
            childRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contRT.rect.height);
        }
        if (addRectMask && contRT != null)
        {
            var mask = contRT.GetComponent<RectMask2D>();
            if (mask == null) contRT.gameObject.AddComponent<RectMask2D>();
        }
        return cache;
    }

    // トークンの先頭 "<" のみが表示されるケース（<••••）を避ける
    // その場合は直前のトークン境界（スペース）まで戻す
    private string AvoidLoneOpeningBracket(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int lastOpen = s.LastIndexOf('<');
        int lastClose = s.LastIndexOf('>');
        if (lastOpen > lastClose)
        {
            // 直近の開き括弧以降に実文字が1文字もなければ境界へ戻す
            int i = lastOpen + 1; // "<" の直後
            bool hasVisible = i < s.Length; // 1文字でも続きがあれば可
            if (!hasVisible)
            {
                int prevSpace = s.LastIndexOf(' ', lastOpen - 1);
                if (prevSpace >= 0)
                    return s.Substring(0, prevSpace);
                else
                    return string.Empty;
            }
        }
        return s;
    }

    [SerializeField] private TextMeshProUGUI StagesString; //ステージとエリア名のテキスト
    [SerializeField] private TenmetuNowImage MapImg; //直接で現在位置表示する簡易マップ
    [SerializeField] private RectTransform bgRect; //背景のRectTransform

    //ズーム用変数
    [SerializeField] private AnimationCurve _firstZoomAnimationCurve;
    [SerializeField] private float _firstZoomSpeedTime;
    [SerializeField] private Vector2 _gotoPos;
    [SerializeField] private Vector2 _gotoScaleXY;
    
    [Header("ズームアニメ有効/無効")]
    [SerializeField] private bool enableZoomAnimation = true;      // 背景/敵ズームアニメを行うか

    GameObject[] TwoObjects;//サイドオブジェクトの配列
    SideObjectMove[] SideObjectMoves = new SideObjectMove[2];//サイドオブジェクトのスクリプトの配列
    List<SideObjectMove>[] LiveSideObjects = new List<SideObjectMove>[2] { new List<SideObjectMove>(), new List<SideObjectMove>() };//生きているサイドオブジェクトのリスト 間引き用

    // 戦闘画面レイヤー構成
    [Header("戦闘画面レイヤー構成")]
    [SerializeField] private Transform enemyBattleLayer;     // 敵配置レイヤー（背景と一緒にズーム）
    [SerializeField] private Transform allyBattleLayer;      // 味方アイコンレイヤー（独立アニメーション）

    // 導入アニメ最適化（準備→一斉起動）
    [Header(
        "導入アニメ最適化（準備→一斉起動）\n" +
        "[モバイル推奨] introYieldDuringPrepare=true, introYieldEveryN=3〜6, introPreAnimationDelaySec=0.01〜0.04, introSlideStaggerInterval=0.06〜0.08\n" +
        "[PC推奨]      introYieldDuringPrepare=false, introYieldEveryN=4, introPreAnimationDelaySec=0〜0.01, introSlideStaggerInterval=0.04〜0.06\n" +
        "備考: 準備フェーズで計算を分散し、アニメ生成は一斉に起動してスパイクを回避します。")]
    [SerializeField] private bool introYieldDuringPrepare = true; // 準備計算をフレーム分散（低端末はtrue推奨）
    [Header("準備計算で何件ごとにYieldするか（3〜6 推奨：端末次第）")]
    [SerializeField] private int  introYieldEveryN = 4;           // N件ごとにYield
    [Header("アニメ起動直前の小休止（秒）0.01〜0.04 推奨（PCは0〜0.01）")]
    [SerializeField] private float introPreAnimationDelaySec = 0.02f; // 一斉起動直前に待つ時間
    [Header("味方スライドの段差（秒）0.06〜0.08 推奨（PCは0.04〜0.06）")]
    [SerializeField] private float introSlideStaggerInterval = 0.075f; // 味方スライドの段差ディレイ
    [Header("敵UI配置パフォーマンス/ログ設定")]
    [Header("詳細: enableVerboseEnemyLogs\ntrue: 敵UI配置処理の詳細ログをConsoleに出力（開発/デバッグ向け）\nfalse: 最低限のみ出力\n注意: ログが多いとEditorでのフレーム落ち/GCを誘発する場合があります。ビルドではOFF推奨")]
    [SerializeField] private bool enableVerboseEnemyLogs = false; // 敵配置周りの詳細ログを出す

    [Header("詳細: throttleEnemySpawns\ntrue: 敵UI生成を複数フレームへ分散し、CPUスパイク/Canvas Rebuildの山を緩和\nfalse: 1フレームに一括生成（見た目は即時だがスパイクが出やすい）\n対象: 大量スポーン/低スペ端末ではtrue推奨")]
    [SerializeField] private bool throttleEnemySpawns = true;     // 敵UI生成をフレームに分散する

    [Header("詳細: enemySpawnBatchSize\n1フレームあたりに生成する敵UIの数\n小さいほど1フレームの負荷は下がるが、全員が出揃うまでの時間は伸びる\n目安: 1-3（モバイル/多数）、4-8（PC/少数）")]
    [SerializeField] private int enemySpawnBatchSize = 2;         // 何体ごとに小休止するか（最小1）

    [Header("詳細: enemySpawnInterBatchFrames\nバッチ間で待機するフレーム数\n0: 毎フレ連続で処理／1: 1フレ休む／2+: さらに分散（ポップインが目立つ可能性）\n目安: 0-2 推奨")]
    [SerializeField] private int enemySpawnInterBatchFrames = 1;  // バッチ間で待機するフレーム数


    // アクションマーク（行動順マーカー）
    [Header("ActionMark 設定")]
    [SerializeField] private ActionMarkUI actionMark;        // 行動対象のアイコン背面に移動させるマーカー
    [SerializeField] private RectTransform actionMarkSpawnPoint; // ActionMarkを最初に出す基準位置（中心）

    // HPバーサイズ設定
    [Header("敵HPバー設定")]
    [SerializeField] private Vector2 hpBarSizeRatio = new Vector2(1.0f, 0.15f); // x: バー幅/アイコン幅, y: バー高/アイコン幅
    
    // 敵UIプレハブ（UIController付き）
    [Header("敵UI Prefab")]
    [SerializeField] private UIController enemyUIPrefab;

    // 敵ランダム配置時の余白（ピクセル）
    [Header("敵ランダム配置 余白設定")]
    [SerializeField] private float enemyMargin = 10f;
    
    // 戦闘エリア設定（ズーム後座標系）
    [Header("戦闘エリア設定（ズーム後座標系）")]
    [SerializeField] private RectTransform enemySpawnArea;   // 敵ランダム配置エリア（単一・ズーム対象外）
    [SerializeField] private Transform[] allySpawnPositions; // 味方出現位置
    [SerializeField] private Vector2 allySlideStartOffset = new Vector2(0, -200); // 味方スライドイン開始オフセット

    // 複数階層ZoomContainer方式
    [Header("ズーム対象コンテナ")]
    [SerializeField] private Transform zoomBackContainer;  // 背景用ズームコンテナ
    [SerializeField] private Transform zoomFrontContainer; // 敵用ズームコンテナ

    [Header("K拡大ステータス(Kモード)")]
    [SerializeField] private RectTransform kZoomRoot;             // 画面全体をまとめるルート（Kズーム対象）
    [SerializeField] private RectTransform kTargetRect;           // ズーム後にアイコンが収まる枠（固定UI層などK非対象）
    [Range(0f,1f)]
    [SerializeField] private float kFitBlend01 = 0.5f;            // 0=高さ優先, 1=横幅優先 のブレンド
    [SerializeField] private float kZoomDuration = 0.6f;
    [SerializeField] private Ease kZoomEase = Ease.OutQuart;
    [Space(4)]
    [SerializeField] private TMPTextBackgroundImage kNameText;           // 名前TMP
    [SerializeField] private TMPTextBackgroundImage kPassivesText;       // パッシブ一覧TMP（K専用、フェード表示）
    [SerializeField] private float kTextSlideDuration = 0.35f;
    [SerializeField] private Ease kTextSlideEase = Ease.OutCubic;
    [SerializeField] private float kTextSlideOffsetX = 220f;      // 右からのオフセット量
    [SerializeField] private float kPassivesFadeDuration = 0.35f; // パッシブ用フェード時間（スライドなし）
    [Header("Kモードテキスト表示設定")]
    [SerializeField] private int kPassivesEllipsisDotCount = 4;    // 末尾に付与するドット数
    [SerializeField] private float kPassivesFitSafety = 1.0f;      // 高さ方向のセーフティ余白(px相当)
    [SerializeField] private bool kPassivesAlwaysAppendEllipsis = true; // 収まる場合でもドットを付ける
    [SerializeField] private bool kPassivesUseRectMask = true;     // 見切れ対策としてRectMask2Dを付与
    [Header("Kモードデバッグ")]
    [SerializeField] private bool kPassivesDebugMode = false;     // ダミーパッシブ表示を有効化
    [SerializeField] private int kPassivesDebugCount = 100;       // 生成するダミーパッシブの数
    [SerializeField] private string kPassivesDebugPrefix = "pas"; // ダミートークンの接頭辞
    [Space(4)]
    [SerializeField] private bool lockBattleZoomDuringK = true;   // K中は既存戦闘ズームを抑制
    [SerializeField] private bool disableIconClickWhileBattleZoom = true; // 既存ズーム中はアイコンクリック無効

    // Kモード内部状態
    private bool _isKActive = false;
    private bool _isKAnimating = false;
    private CancellationTokenSource _kCts;
    private Vector2 _kOriginalPos;
    private Vector3 _kOriginalScale;
    private static Vector3[] s_corners;
    // Kズーム前のトランスフォーム保存が有効か（EnterKで保存されたか）
    private bool _kSnapshotValid = false;
    // Kパッシブ表示: フィット用の生トークン文字列を保持（再フィット用）
    private string _kPassivesTokensRaw = string.Empty;
    // Kパッシブ表示: 子TMPキャッシュ
    private TMP_Text _kPassivesTMP;
    // K中: クリック元のUI（UIController）で、Icon以外の子を一時的にOFFにするための参照
    private UIController _kExclusiveUI;
    // K開始時のActionMark表示状態を退避
    private bool _actionMarkWasActiveBeforeK = false;
    // K開始時のSchizoLog表示状態を退避
    private bool _schizoWasVisibleBeforeK = false;
    // K中: 対象以外のUIControllerの有効状態を退避して一時的に非表示にする
    private List<(UIController ui, bool wasActive)> _kHiddenOtherUIs;

    [Header("前のめりUI設定")]
    [SerializeField] private Vector2 vanguardOffsetPxRange = new Vector2(8f, 16f);//前のめり時の移動量
    [SerializeField] private Vector2 vanguardDurationSecRange = new Vector2(0.12f, 0.2f);//前のめり時のアニメーション時間
    /// <summary>
    /// 前のめり時の移動量
    /// </summary>
    public float BeVanguardOffset { get=> RandomEx.Shared.NextFloat(vanguardOffsetPxRange.x, vanguardOffsetPxRange.y); }
    /// <summary>
    /// 前のめり時のアニメーション時間
    /// </summary>
    public float BaVanguardDurationSec { get=> RandomEx.Shared.NextFloat(vanguardDurationSecRange.x, vanguardDurationSecRange.y); }
    

    private void Start()
    {
        TwoObjects = new GameObject[2];//サイドオブジェクト二つ分の生成
        LiveSideObjects = new List<SideObjectMove>[2];//生きているサイドオブジェクトのリスト
        LiveSideObjects[0] = new List<SideObjectMove>();//左右二つ分
        LiveSideObjects[1] = new List<SideObjectMove>();

        // 起動時はアクションマークを非表示にしておく
        if (actionMark != null)
        {
            actionMark.gameObject.SetActive(false);
        }

        // KモードUI初期設定
        if (kNameText != null) kNameText.gameObject.SetActive(false);
        if (kPassivesText != null) kPassivesText.gameObject.SetActive(false);
    }

    RectTransform _rect;
    RectTransform Rect
    {
        get {
            if (_rect == null)
            {
                _rect = GetComponent<RectTransform>();
            }
            return _rect;
        }
    }

    /// <summary>
    ///     歩行時のEYEAREAのUI更新
    /// </summary>
    public void WalkUIUpdate(StageData sd, StageCut sc, PlayersStates pla)
    {
        StagesString.text = sd.StageName + "・\n" + sc.AreaName;
        NowImageCalc(sc, pla);
        SideObjectManage(sc, SideObject_Type.Normal, sd.StageThemeColorUI.FrameArtColor,sd.StageThemeColorUI.TwoColor);//サイドオブジェクト（ステージ色適用）
    }

    /// <summary>
    /// エンカウントしたら最初にズームする処理（改良版）
    /// </summary>
    public async UniTask FirstImpressionZoomImproved()
    {
        // 準備（計算）→ 一斉起動（アニメ）の二段階でスパイクを抑制
        var ctx = BuildMetricsContext();
        IntroMotionPlan plan;
        using (global::MetricsHub.Instance.BeginSpan("Intro.Prepare", ctx))
        {
            // Orchestrator 事前準備（I/F先行：現状はno-op）
            try
            {
                EnsureOrchestrator();
                var ictx = BuildIntroContextForOrchestrator();
                var token = _sweepCts != null ? _sweepCts.Token : System.Threading.CancellationToken.None;
                await _orchestrator.PrepareAsync(ictx, token);
            }
            catch { /* no-op */ }
            plan = await PrepareIntroMotions(introYieldDuringPrepare, introYieldEveryN);
        }

        // 理論所要時間（秒）を算出（ズーム/スライド段差/プリディレイを考慮）
        double plannedSec = ComputePlannedIntroDurationSeconds(plan, introPreAnimationDelaySec);

        // 実測：Playの開始から完了までを計測
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using (global::MetricsHub.Instance.BeginSpan("Intro.Play", ctx))
        {
            await PlayIntroMotions(plan, introPreAnimationDelaySec);
        }
        sw.Stop();

        // メトリクス保存
        _lastIntroMetrics.PlannedMs = plannedSec * 1000.0;
        _lastIntroMetrics.ActualMs  = sw.Elapsed.TotalMilliseconds;
        _lastIntroMetrics.DelayMs   = Math.Max(0.0, _lastIntroMetrics.ActualMs - _lastIntroMetrics.PlannedMs);
        _lastIntroMetrics.Timestamp = System.DateTime.Now;
        _lastIntroMetrics.AllyCount = Walking.Instance?.bm?.AllyGroup?.Ours?.Count ?? (allySpawnPositions?.Length ?? 0);
        _lastIntroMetrics.EnemyCount = Walking.Instance?.bm?.EnemyGroup?.Ours?.Count ?? 0;

        Debug.Log($"[Intro] Planned={_lastIntroMetrics.PlannedMs:F2}ms, Actual={_lastIntroMetrics.ActualMs:F2}ms, Delay={_lastIntroMetrics.DelayMs:F2}ms, Allies={_lastIntroMetrics.AllyCount}, Enemies={_lastIntroMetrics.EnemyCount}");
    }

    // ===== 準備→一斉起動 フェーズ実装 =====
    private struct ZoomPlan
    {
        public RectTransform target;
        public Vector2 fromScale;
        public Vector2 toScale;
        public Vector2 fromPos;
        public Vector2 toPos;
        public float duration;
        public AnimationCurve curve;
    }

    private struct SlidePlan
    {
        public RectTransform target;
        public Vector2 fromPos;
        public Vector2 toPos;
        public float duration;
        public float delay;
        public Ease  ease;
    }

    private sealed class IntroMotionPlan
    {
        public List<ZoomPlan> Zooms = new List<ZoomPlan>();
        public List<SlidePlan> Slides = new List<SlidePlan>();
    }

    private async UniTask<IntroMotionPlan> PrepareIntroMotions(bool yieldDuringPrepare, int yieldEveryN)
    {
        // NOTE: ProfilerMarker.Auto() は await を跨ぐとフレーム越境で警告/エラーになるため未使用
        var plan = new IntroMotionPlan();
        int workCount = 0;

            // ZoomPlan は廃止（ズームは常に Orchestrator 経由で実行）

            // Slide（味方アイコン）
            if (allySpawnPositions != null)
            {
                for (int i = 0; i < allySpawnPositions.Length; i++)
                {
                    var rect = allySpawnPositions[i] as RectTransform;
                    if (rect == null) continue;
                    var fromPos = rect.anchoredPosition + allySlideStartOffset;
                    var toPos   = rect.anchoredPosition;
                    plan.Slides.Add(new SlidePlan
                    {
                        target   = rect,
                        fromPos  = fromPos,
                        toPos    = toPos,
                        duration = 0.5f,
                        delay    = i * Mathf.Max(0f, introSlideStaggerInterval),
                        ease     = Ease.OutBack,
                    });
                    if (yieldDuringPrepare && (++workCount % Mathf.Max(1, yieldEveryN) == 0)) await UniTask.Yield();
                }
            }

        return plan;
    }

    // 準備した計画から理論所要時間（秒）を算出（ズームとスライド段差、起動前ディレイを考慮）
    private double ComputePlannedIntroDurationSeconds(IntroMotionPlan plan, float preAnimationDelaySec)
    {
        if (plan == null) return Mathf.Max(0f, preAnimationDelaySec);
        // ズームは常に Orchestrator 経由で実行されるため、所要はインスペクタ設定から取得
        float zoomMax = enableZoomAnimation ? Mathf.Max(0f, _firstZoomSpeedTime) : 0f;
        float slideMax = 0f;
        for (int i = 0; i < plan.Slides.Count; i++)
        {
            var s = plan.Slides[i];
            float end = Mathf.Max(0f, s.delay) + Mathf.Max(0f, s.duration);
            if (end > slideMax) slideMax = end;
        }
        float core = Mathf.Max(zoomMax, slideMax);
        return Mathf.Max(0f, preAnimationDelaySec) + core;
    }

    private async UniTask PlayIntroMotions(IntroMotionPlan plan, float preAnimationDelaySec)
    {
        // NOTE: ProfilerMarker.Auto() は await を跨ぐとフレーム越境で警告/エラーになるため未使用
        if (plan == null) plan = new IntroMotionPlan();

            // 一斉起動直前の小休止（低端末でのスパイク緩和）
            if (preAnimationDelaySec > 0f)
            {
                var token = _sweepCts != null ? _sweepCts.Token : System.Threading.CancellationToken.None;
                await UniTask.Delay(TimeSpan.FromSeconds(preAnimationDelaySec), cancellationToken: token);
            }

            var tasks = new List<UniTask>();
            // --- Jitter（アニメ滑らかさ）計測開始 ---
            var jitter = global::MetricsHub.Instance.StartJitter("Intro.Jitter", BuildMetricsContext());

            // 敵UI生成（従来はZoom開始時に並行起動していたものをここで起動）
            var currentBattleManager = Walking.Instance?.bm;
            if (currentBattleManager?.EnemyGroup != null)
            {
                var placeSw = System.Diagnostics.Stopwatch.StartNew();
                var placeTask = UniTask.Create(async () =>
                {
                    EnsureOrchestrator();
                    var ictx = BuildIntroContextForOrchestrator();
                    var pctx = BuildPlacementContext(currentBattleManager.EnemyGroup);
                    using (global::MetricsHub.Instance.BeginSpan("PlaceEnemies", BuildMetricsContext()))
                    {
                        var token = _sweepCts != null ? _sweepCts.Token : System.Threading.CancellationToken.None;
                        await _orchestrator.PlaceEnemiesAsync(ictx, pctx, token);
                    }
                    placeSw.Stop();
                    _lastIntroMetrics.EnemyPlacementMs = placeSw.Elapsed.TotalMilliseconds;
                });
                tasks.Add(placeTask);
            }
            else
            {
                _lastIntroMetrics.EnemyPlacementMs = 0;
            }

            // Zoom 同時起動（常に Orchestrator 経由）
            if (enableZoomAnimation)
            {
                try
                {
                    EnsureOrchestrator();
                    var ictx = BuildIntroContextForOrchestrator();
                    var token = _sweepCts != null ? _sweepCts.Token : System.Threading.CancellationToken.None;
                    _isZoomAnimating = true;
                    var zoomTask = _orchestrator.PlayAsync(ictx, token);
                    tasks.Add(zoomTask);
                }
                catch { /* no-op */ }
            }

            // Ally アイコンの可視化（必要分のみ）
            if (allyBattleLayer != null)
            {
                allyBattleLayer.gameObject.SetActive(true);
                try
                {
                    var ours = Walking.Instance?.bm?.AllyGroup?.Ours;
                    if (ours != null)
                    {
                        foreach (var ch in ours)
                        {
                            ch?.UI?.SetActive(true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Intro] Ally UI enable failed: {ex.Message}");
                }
            }

            // Slide 同時起動（開始前にfromPosへスナップ）
            if (plan.Slides.Count > 0)
            {
                _isAllySlideAnimating = true;
                foreach (var s in plan.Slides)
                {
                    if (s.target == null) continue;
                    s.target.anchoredPosition = s.fromPos; // 一瞬のチラツキ防止
                    var token = _sweepCts != null ? _sweepCts.Token : System.Threading.CancellationToken.None;
                    var slideTask = UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, s.delay)), cancellationToken: token)
                        .ContinueWith(() =>
                            LMotion.Create(s.fromPos, s.toPos, s.duration)
                                .WithEase(s.ease)
                                .BindToAnchoredPosition(s.target)
                                .ToUniTask()
                        );
                    tasks.Add(slideTask);
                }
            }

            if (tasks.Count > 0)
            {
                try
                {
                    await UniTask.WhenAll(tasks);
                }
                catch (System.OperationCanceledException)
                {
                    _isZoomAnimating = false;
                    _isAllySlideAnimating = false;
                    throw;
                }
            }

            // --- Jitter終了＆統計反映 ---
            var stats = await jitter.EndAsync();
            _lastIntroMetrics.IntroFrameAvgMs = stats.AvgMs;
            _lastIntroMetrics.IntroFrameP95Ms = stats.P95Ms;
            _lastIntroMetrics.IntroFrameMaxMs = stats.MaxMs;

            _isZoomAnimating = false;
            _isAllySlideAnimating = false;

            // MetricsHubへ最終結果を記録（Planned/Actual/Delay/配置/Jitter統計を含む）
            try
            {
                var ctx = BuildMetricsContext();
                global::MetricsHub.Instance.RecordIntro(new global::IntroMetricsEvent
                {
                    PlannedMs = _lastIntroMetrics.PlannedMs,
                    ActualMs = _lastIntroMetrics.ActualMs,
                    DelayMs = _lastIntroMetrics.DelayMs,
                    EnemyPlacementMs = _lastIntroMetrics.EnemyPlacementMs,
                    Timestamp = _lastIntroMetrics.Timestamp,
                    AllyCount = _lastIntroMetrics.AllyCount,
                    EnemyCount = _lastIntroMetrics.EnemyCount,
                    IntroFrameAvgMs = _lastIntroMetrics.IntroFrameAvgMs,
                    IntroFrameP95Ms = _lastIntroMetrics.IntroFrameP95Ms,
                    IntroFrameMaxMs = _lastIntroMetrics.IntroFrameMaxMs,
                    Context = ctx,
                });
            }
            catch { /* no-op */ }
    }

    // ===== MetricsHub 用コンテキスト生成 =====
    private global::MetricsContext BuildMetricsContext()
    {
        // 基本は現在のIntro設定からSummaryを作る
        string currentPresetSummary = $"{introYieldDuringPrepare.ToString().ToLower()} {introYieldEveryN} {introPreAnimationDelaySec} {introSlideStaggerInterval}";
        var baseScenario = IsBenchmarkRunning ? "Benchmark" : "Runtime";

        // BenchmarkRunner からの文脈があれば優先
        var outer = global::BenchmarkContext.Current;
        return new global::MetricsContext
        {
            ScenarioName = outer?.ScenarioName ?? baseScenario,
            PresetSummary = outer?.PresetSummary ?? currentPresetSummary,
            PresetIndex = outer?.PresetIndex ?? -1,
            RunIndex = outer?.RunIndex ?? -1,
            Tags = outer?.Tags,
        };
    }

    /// <summary>
    /// Play中のみ、毎フレームのフレーム時間（unscaledDeltaTime）をmsで収集する
    /// </summary>
    private async UniTask SampleIntroFrameTimes(List<float> dest, System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            dest.Add(Time.unscaledDeltaTime * 1000f);
            await UniTask.Yield();
        }
    }

    

    

    

    
    
    /// <summary>
    /// RectTransformのワールド座標を取得
    /// </summary>
    

    
    
    

    public void EraceEnemyUI()
    {
        var parent = enemyBattleLayer;
        int childCount = parent.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            // エディタ上かどうかで処理を分岐
            #if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(parent.GetChild(i).gameObject);
            else
            #endif
                Destroy(parent.GetChild(i).gameObject);
        }   
    }
    
    /// <summary>
    /// アクションマークを指定アイコンの中心へ移動（サイズは自動追従）
    /// </summary>
    /// <param name="targetIcon">対象アイコンのRectTransform</param>
    /// <param name="immediate">即時反映（アニメーションなし）</param>
    public void MoveActionMarkToIcon(RectTransform targetIcon, bool immediate = false)
    {
        if (actionMark == null)
        {
            Debug.LogWarning("ActionMarkUI が未設定です。WatchUIUpdate の Inspector で actionMark を割り当ててください。");
            return;
        }
        if (targetIcon == null)
        {
            Debug.LogWarning("MoveActionMarkToIcon: targetIcon が null です。");
            return;
        }

        actionMark.MoveToTarget(targetIcon, immediate);
    }

    /// <summary>
    /// スケール補正付きでアイコンへ移動（ズーム/スライドの見かけスケール差を補正）
    /// </summary>
    public void MoveActionMarkToIconScaled(RectTransform targetIcon, bool immediate = false)
    {
        if (actionMark == null || targetIcon == null)
        {
            Debug.LogWarning("MoveActionMarkToIconScaled: 必要参照が不足しています。");
            return;
        }
        var extraScale = ComputeScaleRatioForTarget(targetIcon);
        actionMark.MoveToTargetWithScale(targetIcon, extraScale, immediate);
    }

    /// <summary>
    /// アクションマークを指定アクター（BaseStates）のUIアイコンへ移動
    /// </summary>
    /// <param name="actor">BaseStates 派生のアクター</param>
    /// <param name="immediate">即時反映（アニメーションなし）</param>
    public void MoveActionMarkToActor(BaseStates actor, bool immediate = false)
    {
        if (actor == null)
        {
            Debug.LogWarning("MoveActionMarkToActor: actor が null です。");
            return;
        }

        var ui = actor.UI;
        if (ui == null)
        {
            Debug.LogWarning($"MoveActionMarkToActor: actor.UI が null です。actor={actor.GetType().Name}");
            return;
        }

        var img = ui.Icon;
        if (img == null)
        {
            Debug.LogWarning($"MoveActionMarkToActor: UI.Icon が null です。actor={actor.GetType().Name}");
            return;
        }

        var iconRT = img.transform as RectTransform;
        MoveActionMarkToIcon(iconRT, immediate);
    }

    /// <summary>
    /// ズーム/スライド完了を待ってから、スケール補正付きでアクションマークを移動
    /// </summary>
    public async UniTask MoveActionMarkToActorScaled(BaseStates actor, bool immediate = false, bool waitAnimations = true)
    {
        if (actor == null)
        {
            Debug.LogWarning("MoveActionMarkToActorScaled: actor が null です。");
            return;
        }
        var ui = actor.UI;
        if (ui?.Icon == null)
        {
            Debug.LogWarning($"MoveActionMarkToActorScaled: UI.Icon が null です。actor={actor.GetType().Name}");
            return;
        }
        if (waitAnimations)
        {
            await WaitBattleIntroAnimations();
        }
        var iconRT = ui.Icon.transform as RectTransform;
        MoveActionMarkToIconScaled(iconRT, immediate);
    }

    /// <summary>
    /// target(アイコン)の見かけスケールと、ActionMark親の見かけスケールの比率を返す
    /// </summary>
    private Vector2 ComputeScaleRatioForTarget(RectTransform target)
    {
        var parentRT = actionMark?.rectTransform?.parent as RectTransform;
        if (target == null)
            return Vector2.one;
        var sTarget = GetWorldScaleXY(target);
        var sParent = parentRT != null ? GetWorldScaleXY(parentRT) : Vector2.one;
        float sx = (Mathf.Abs(sParent.x) > 1e-5f) ? sTarget.x / sParent.x : 1f;
        float sy = (Mathf.Abs(sParent.y) > 1e-5f) ? sTarget.y / sParent.y : 1f;
        return new Vector2(sx, sy);
    }

    private static Vector2 GetWorldScaleXY(RectTransform rt)
    {
        if (rt == null) return Vector2.one;
        var s = rt.lossyScale;
        return new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.y));
    }

    /// <summary>
    /// バトル導入時のズーム/スライドが完了するまで待機
    /// </summary>
    public async UniTask WaitBattleIntroAnimations()
    {
        // 既に完了なら即return
        if (!_isZoomAnimating && !_isAllySlideAnimating) return;
        // 状態が落ち着くまでフレーム待機
        while (_isZoomAnimating || _isAllySlideAnimating)
        {
            await UniTask.Yield();
        }
    }

    // ===== K MODE (ステータス拡大) =====
    /// <summary>
    /// Kモードに入れるかどうか（戦闘導入ズームや味方スライドが走っている場合は抑制可能）
    /// </summary>
    public bool CanEnterK => !_isKActive && !_isKAnimating && !(disableIconClickWhileBattleZoom && (_isZoomAnimating || _isAllySlideAnimating));

    /// <summary>
    /// Kモードがアクティブか
    /// </summary>
    public bool IsKActive => _isKActive;
    /// <summary>
    /// Kモードのアニメーション中か
    /// </summary>
    public bool IsKAnimating => _isKAnimating;
    /// <summary>
    /// 現在のKズーム対象UIかどうか
    /// </summary>
    public bool IsCurrentKTarget(UIController ui) => _isKActive && (_kExclusiveUI == ui);

    /// <summary>
    /// 指定アイコンをkTargetRectにフィットさせるように、kZoomRootをスケール・移動させてKモード突入
    /// </summary>
    public async UniTask EnterK(RectTransform iconRT, string title)
    {
        if (!CanEnterK)
        {
            Debug.Log("[K] CanEnterK=false のためEnterKを無視");
            return;
        }
        if (iconRT == null || kZoomRoot == null || kTargetRect == null)
        {
            Debug.LogWarning("[K] 必要参照が不足しています(iconRT/kZoomRoot/kTargetRect)。");
            return;
        }

        // テキスト設定（まずは非表示）
        if (kNameText != null) { kNameText.text = title ?? string.Empty; kNameText.gameObject.SetActive(false); }
        
        _kCts?.Cancel();
        _kCts?.Dispose();
        _kCts = new CancellationTokenSource();

        var ct = _kCts.Token;
        _isKAnimating = true;

        // クリック元UIの参照のみ保持（復元用）。非表示化はUIController.TriggerKModeで行う。
        _kExclusiveUI = iconRT.GetComponentInParent<UIController>();

        // 非対象キャラのUIControllerをK中は丸ごと非表示にする（元の有効状態を退避）
        _kHiddenOtherUIs = new List<(UIController ui, bool wasActive)>();
        var bm = Walking.Instance?.bm;
        var allChars = bm?.AllCharacters;
        if (allChars != null)
        {
            foreach (var ch in allChars)
            {
                var ui = ch?.UI;
                if (ui == null || ui == _kExclusiveUI) continue;
                bool prev = ui.gameObject.activeSelf;
                _kHiddenOtherUIs.Add((ui, prev));
                if (prev)
                {
                    ui.SetActive(false);
                }
            }
        }

        // ActionMarkの表示状態を退避し、K中は非表示にする
        if (actionMark != null)
        {
            _actionMarkWasActiveBeforeK = actionMark.gameObject.activeSelf;
            if (_actionMarkWasActiveBeforeK)
            {
                HideActionMark();
            }
        }

        // SchizoLogの表示状態を退避し、K中は非表示にする（不要な参照を増やさずシングルトンを直接利用）
        if (SchizoLog.Instance != null)
        {
            _schizoWasVisibleBeforeK = SchizoLog.Instance.IsVisible();
            if (_schizoWasVisibleBeforeK)
            {
                SchizoLog.Instance.SetVisible(false);
            }
        }

        // もとの状態を保存
        _kOriginalPos = kZoomRoot.anchoredPosition;
        _kOriginalScale = kZoomRoot.localScale;
        _kSnapshotValid = true;

        // フィット計算
        ComputeKFit(iconRT, out float targetScale, out Vector2 targetAnchoredPos);
        // Kテキスト/ボタンの再マッピング計算は廃止（レイアウトのアンカー/位置決めに任せる）

        // ズームイン（位置＋スケール）
        var rootRT = kZoomRoot;
        var scaleTask = LMotion.Create((Vector3)_kOriginalScale, new Vector3(targetScale, targetScale, _kOriginalScale.z), kZoomDuration)
            .WithEase(kZoomEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToLocalScale(rootRT)
            .ToUniTask(ct);

        var posTask = LMotion.Create(_kOriginalPos, targetAnchoredPos, kZoomDuration)
            .WithEase(kZoomEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPosition(rootRT)
            .ToUniTask(ct);

        // Kパッシブテキストの準備（内容セット＆非表示→フェード表示準備）
        BaseStates actorForK = FindActorByUI(_kExclusiveUI);
        SetKPassivesText(actorForK);

        // テキストのスライドインもズームと同時に開始
        var slideTask = SlideInKTexts(title, ct);
        var fadePassivesTask = FadeInKPassives(actorForK, ct);

        try
        {
            await UniTask.WhenAll(scaleTask, posTask, slideTask, fadePassivesTask);
        }
        catch (OperationCanceledException)
        {
            // 即時終了などのキャンセル
            if (_kExclusiveUI != null)
            {
                _kExclusiveUI.SetExclusiveIconMode(false);
                _kExclusiveUI = null;
            }
            // 非対象UIを元の有効状態へ復帰
            if (_kHiddenOtherUIs != null)
            {
                foreach (var pair in _kHiddenOtherUIs)
                {
                    if (pair.ui != null) pair.ui.SetActive(pair.wasActive);
                }
                _kHiddenOtherUIs = null;
            }
            _isKAnimating = false;
            _kSnapshotValid = false;
            // キャンセル時はテキストを念のため非表示へ戻す
            if (kNameText != null) kNameText.gameObject.SetActive(false);
            if (kPassivesText != null) kPassivesText.gameObject.SetActive(false);
            // EnterK中断時はActionMarkを元状態に戻す
            if (actionMark != null && _actionMarkWasActiveBeforeK)
            {
                ShowActionMark();
            }
            _actionMarkWasActiveBeforeK = false;
            // EnterK中断時はSchizoLogも元状態に戻す
            if (SchizoLog.Instance != null && _schizoWasVisibleBeforeK)
            {
                SchizoLog.Instance.SetVisible(true);
            }
            _schizoWasVisibleBeforeK = false;
            return;
        }

        _isKActive = true;
        _isKAnimating = false;

        // 以降の処理（_isKActive の更新など）のみ。テキストのスライドはズームと同時に完了済み。
    }

    /// <summary>
    /// Kモード解除（アニメーションあり）
    /// </summary>
    public async UniTask ExitK()
    {
        if (!_isKActive && !_isKAnimating)
        {
            return;
        }

        // テキストは即時非表示
        if (kNameText != null) kNameText.gameObject.SetActive(false);
        if (kPassivesText != null) kPassivesText.gameObject.SetActive(false);

        _kCts?.Cancel();
        _kCts?.Dispose();
        _kCts = new CancellationTokenSource();
        var ct = _kCts.Token;

        _isKAnimating = true;

        // ここで即時にK中に非表示にしていたUIを復帰させる（ズームアウト中に表示して良い要件）
        // クリック元UIのIcon以外の可視状態を復元
        if (_kExclusiveUI != null)
        {
            _kExclusiveUI.SetExclusiveIconMode(false);
            _kExclusiveUI = null;
        }
        // 非対象UIControllerを元の有効状態へ復帰
        if (_kHiddenOtherUIs != null)
        {
            foreach (var pair in _kHiddenOtherUIs)
            {
                if (pair.ui != null) pair.ui.SetActive(pair.wasActive);
            }
            _kHiddenOtherUIs = null;
        }
        // ActionMarkの表示状態を復帰
        if (actionMark != null && _actionMarkWasActiveBeforeK)
        {
            ShowActionMark();
            _actionMarkWasActiveBeforeK = false;
        }
        // SchizoLogの表示状態を復帰
        if (SchizoLog.Instance != null && _schizoWasVisibleBeforeK)
        {
            SchizoLog.Instance.SetVisible(true);
            _schizoWasVisibleBeforeK = false;
        }

        var rootRT = kZoomRoot;
        if (rootRT == null)
        {
            _isKActive = false;
            _isKAnimating = false;
            return;
        }

        var scaleTask = LMotion.Create(rootRT.localScale, _kOriginalScale, kZoomDuration)
            .WithEase(kZoomEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToLocalScale(rootRT)
            .ToUniTask(ct);

        var posTask = LMotion.Create(rootRT.anchoredPosition, _kOriginalPos, kZoomDuration)
            .WithEase(kZoomEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPosition(rootRT)
            .ToUniTask(ct);

        try
        {
            await UniTask.WhenAll(scaleTask, posTask);
        }
        catch (OperationCanceledException)
        {
            // 即時終了など
        }

        _isKActive = false;
        _isKAnimating = false;
        _kSnapshotValid = false;
        // 復帰処理はズームアウト開始時に実施済み
    }

    /// <summary>
    /// Kモードを即時解除（キャンセルやNextWaitで使用）
    /// </summary>
    public void ForceExitKImmediate()
    {
        _kCts?.Cancel();
        _kCts?.Dispose();
        _kCts = null;

        // クリック元UIのIcon以外の可視状態を即時復元
        if (_kExclusiveUI != null)
        {
            _kExclusiveUI.SetExclusiveIconMode(false);
            _kExclusiveUI = null;
        }
        // 非対象UIControllerを元の有効状態に即時復帰
        if (_kHiddenOtherUIs != null)
        {
            foreach (var pair in _kHiddenOtherUIs)
            {
                if (pair.ui != null) pair.ui.SetActive(pair.wasActive);
            }
            _kHiddenOtherUIs = null;
        }

        if (kNameText != null) kNameText.gameObject.SetActive(false);

        if (kZoomRoot != null && _kSnapshotValid)
        {
            kZoomRoot.anchoredPosition = _kOriginalPos;
            kZoomRoot.localScale = _kOriginalScale;
        }

        _isKActive = false;
        _isKAnimating = false;
        // 即時解除時もActionMarkを元状態へ復帰
        if (actionMark != null && _actionMarkWasActiveBeforeK)
        {
            ShowActionMark();
        }
        _actionMarkWasActiveBeforeK = false;
        // 即時解除時もSchizoLogを元状態へ復帰
        if (SchizoLog.Instance != null && _schizoWasVisibleBeforeK)
        {
            SchizoLog.Instance.SetVisible(true);
        }
        _schizoWasVisibleBeforeK = false;
    }

    /// <summary>
    /// テキストの右→左スライドイン
    /// </summary>
    private async UniTask SlideInKTexts(string title, CancellationToken ct)
    {
        var tasks = new List<UniTask>(2);

        if (kNameText != null)
        {
            var nameRT = kNameText.rectTransform;
            // レイアウトで設定された anchoredPosition をそのまま目標とする
            var target = nameRT.anchoredPosition;
            var start = target + new Vector2(kTextSlideOffsetX, 0f);
            nameRT.anchoredPosition = start;
            kNameText.gameObject.SetActive(true);
            // 可視化直後に背景を開始位置へ更新
            kNameText.RefreshBackground();

            var t = LMotion.Create(start, target, kTextSlideDuration)
                .WithEase(kTextSlideEase)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAnchoredPosition(nameRT)
                .ToUniTask(ct);
            tasks.Add(t);
        }

        if (tasks.Count > 0)
        {
            try
            {
                await UniTask.WhenAll(tasks);
                // レイアウト確定後に最終位置へ背景を更新
                Canvas.ForceUpdateCanvases();
                if (kNameText != null) kNameText.RefreshBackground();
                // 念のため次フレーム終端でもう一度更新（ContentSizeFitter等の遅延対策）
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                Canvas.ForceUpdateCanvases();
                if (kNameText != null) kNameText.RefreshBackground();
            }
            catch (OperationCanceledException)
            {
                // 即時解除時など
            }
        }
    }

    /// <summary>
    /// Kモードのフィット計算：icon中心→target中心へ。スケールは幅/高さ比のブレンド。
    /// </summary>
    private void ComputeKFit(RectTransform iconRT, out float outScale, out Vector2 outAnchoredPos)
    {
        GetWorldRect(iconRT, out var iconCenter, out var iconSize);
        GetWorldRect(kTargetRect, out var targetCenter, out var targetSize);

        float sx = SafeDiv(targetSize.x, iconSize.x);
        float sy = SafeDiv(targetSize.y, iconSize.y);
        float s = Mathf.Lerp(sy, sx, Mathf.Clamp01(kFitBlend01));

        var parentPivotWorld = kZoomRoot.TransformPoint(Vector3.zero);
        // 親(kZoomRoot)を移動/拡大したときの子(icon)の新ワールド位置
        // newIconWorld = (iconCenter - parentPivotWorld) * s + parentPivotWorld + move
        // => move = targetCenter - ((iconCenter - parentPivotWorld) * s + parentPivotWorld)
        Vector2 moveWorld = targetCenter - ((iconCenter - (Vector2)parentPivotWorld) * s + (Vector2)parentPivotWorld);

        var parentRT = kZoomRoot.parent as RectTransform;
        Vector2 moveLocal = parentRT != null ? (Vector2)parentRT.InverseTransformVector(moveWorld) : moveWorld;

        outScale = s;
        outAnchoredPos = _kOriginalPos + moveLocal;
    }

    private static float SafeDiv(float a, float b)
    {
        return Mathf.Abs(b) < 1e-5f ? 1f : a / b;
    }

    private static void GetWorldRect(RectTransform rt, out Vector2 center, out Vector2 size)
    {
        var corners = s_corners ??= new Vector3[4];
        rt.GetWorldCorners(corners);
        var min = new Vector2(corners[0].x, corners[0].y);
        var max = new Vector2(corners[2].x, corners[2].y);
        center = (min + max) * 0.5f;
        size = max - min;
    }

    // ActionMark の表示/非表示ファサード
    public void ShowActionMark()
    {
        if (actionMark == null)
        {
            Debug.LogWarning("ShowActionMark: ActionMarkUI が未設定です。");
            return;
        }
        actionMark.gameObject.SetActive(true);
    }

    public void HideActionMark()
    {
        if (actionMark == null)
        {
            Debug.LogWarning("HideActionMark: ActionMarkUI が未設定です。");
            return;
        }
        actionMark.gameObject.SetActive(false);
    }

    /// <summary>
    /// 特別版: スポーン位置(actionMarkSpawnPoint)の中心に0サイズで出す
    /// 次の MoveActionMarkToActor/Icon 時に、ここから拡大・移動する演出になります。
    /// </summary>
    public void ShowActionMarkFromSpawn(bool zeroSize = true)
    {
        if (actionMark == null)
        {
            Debug.LogWarning("ShowActionMarkFromSpawn: ActionMarkUI が未設定です。");
            return;
        }
        if (actionMarkSpawnPoint == null)
        {
            Debug.LogWarning("ShowActionMarkFromSpawn: actionMarkSpawnPoint が未設定です。通常の ShowActionMark() を使用します。");
            ShowActionMark();
            return;
        }

        var markRT = actionMark.rectTransform;
        // 念のため中央基準
        markRT.pivot = new Vector2(0.5f, 0.5f);
        markRT.anchorMin = new Vector2(0.5f, 0.5f);
        markRT.anchorMax = new Vector2(0.5f, 0.5f);

        // スポーン位置(中心)のワールド座標 → ActionMark親のローカル(anchoredPosition)へ
        Vector2 worldCenter = actionMarkSpawnPoint.TransformPoint(actionMarkSpawnPoint.rect.center);
        Vector2 anchored = WorldToAnchoredPosition(markRT, worldCenter);

        actionMark.gameObject.SetActive(true);
        markRT.anchoredPosition = anchored;
        if (zeroSize)
        {
            actionMark.SetSize(0f, 0f);
        }
    }

    /// <summary>
    /// ワールド座標をRectTransformのanchoredPosition座標系へ変換
    /// </summary>
    private Vector2 WorldToAnchoredPosition(RectTransform rectTransform, Vector2 worldPos)
    {
        var parent = rectTransform.parent as RectTransform;
        if (parent == null)
        {
            return rectTransform.InverseTransformPoint(worldPos);
        }
        // Canvas/Camera を考慮した正確な変換
        var canvas = rectTransform.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            {
                cam = canvas.worldCamera;
            }
        }
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out var localPoint);
        return localPoint;
    }

    


    
    /// <summary>
    /// 敵をランダムエリア内に配置（旧ローカル/ワールド座標版）は廃止。
    /// 現行は GetRandomPreZoomLocalPosition と WorldToPreZoomLocal の組み合わせで実装。
    /// </summary>

    /// <summary>
    /// ズーム後のワールド座標をズーム前のenemyBattleLayerローカル座標に変換
    /// ズームパラメータ(_gotoPos, _gotoScaleXY)を考慮した逆算処理
    /// </summary>
    private Vector2 WorldToPreZoomLocal(Vector2 targetWorldPos)
    {
        if (enemyBattleLayer == null) return Vector2.zero;
        
        // ① まずワールド座標をenemyBattleLayerローカル座標に変換
        var local = ((RectTransform)enemyBattleLayer).InverseTransformPoint(targetWorldPos);
        
        // ② ズームで掛かるスケールと平行移動分を逆算
        // enemyBattleLayerは(_gotoScaleXY, _gotoPos)でズームする想定
        // pivot(0.5,0.5)なら「中心からの差分」にスケールが掛かる
        local = new Vector2(
            (local.x - _gotoPos.x) / _gotoScaleXY.x,
            (local.y - _gotoPos.y) / _gotoScaleXY.y
        );
        
        return local;
    }
    

    /// <summary>
    /// ズーム後にspawnArea内に収まるズーム前のローカル座標を取得
    /// 逆算ロジックでズーム後の目標位置を先に決めてからズーム前座標を算出
    /// </summary>
    private Vector2 GetRandomPreZoomLocalPosition(Vector2 enemySize, List<Vector2> existingWorldPositions, float marginSize, out Vector2 chosenWorldPos)
    {
        if (enemySpawnArea == null)
        {
            Debug.LogWarning("enemySpawnAreaが設定されていません。");
            chosenWorldPos = Vector2.zero;
            return Vector2.zero;
        }

        var rect = enemySpawnArea.rect;
        var halfEnemySize = enemySize / 2 + Vector2.one * marginSize;

        // デバッグ: 範囲とサイズを一度だけ出力
        Debug.Log($"SpawnArea rect: xMin={rect.xMin:F2}, xMax={rect.xMax:F2}, yMin={rect.yMin:F2}, yMax={rect.yMax:F2} | enemySize={enemySize}, half+margin={halfEnemySize}, existingCount={existingWorldPositions?.Count ?? 0}");

        var minX = rect.xMin + halfEnemySize.x;
        var maxX = rect.xMax - halfEnemySize.x;
        var minY = rect.yMin + halfEnemySize.y;
        var maxY = rect.yMax - halfEnemySize.y;

        const int maxAttempts = 50;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var randomX = UnityEngine.Random.Range(minX, maxX);
            var randomY = UnityEngine.Random.Range(minY, maxY);
            var spawnAreaLocal = new Vector2(randomX, randomY);
            var targetWorldPos = enemySpawnArea.TransformPoint(spawnAreaLocal);

            // デバッグ: 最初の数回のみ詳細ログ
            if (attempt < 3)
            {
                Debug.Log($"Attempt#{attempt}: local={spawnAreaLocal}, world={targetWorldPos}");

                // Overlap判定の内訳ログ（最大2件）
                Vector2 halfSize = enemySize / 2 + Vector2.one * marginSize;
                Rect candidateRect = new Rect(spawnAreaLocal - halfSize, enemySize + Vector2.one * marginSize * 2);
                int toShow = Mathf.Min(existingWorldPositions.Count, 2);
                for (int i = 0; i < toShow; i++)
                {
                    var existingWorld = existingWorldPositions[i];
                    var existingLocal = (Vector2)enemySpawnArea.InverseTransformPoint(new Vector3(existingWorld.x, existingWorld.y, 0));
                    Rect existingRect = new Rect(existingLocal - halfSize, enemySize + Vector2.one * marginSize * 2);
                    bool overlap = candidateRect.Overlaps(existingRect);
                    Debug.Log($"  vs existing[{i}]: localPos={existingLocal}, overlap={overlap}, candRect(local)={candidateRect}, existRect(local)={existingRect}");
                }
            }

            // ローカル座標系での重複判定
            bool validLocal = true;
            Vector2 half = enemySize / 2 + Vector2.one * marginSize;
            Rect cand = new Rect(spawnAreaLocal - half, enemySize + Vector2.one * marginSize * 2);
            for (int i = 0; i < existingWorldPositions.Count; i++)
            {
                var existWorld = existingWorldPositions[i];
                var existLocal = (Vector2)enemySpawnArea.InverseTransformPoint(new Vector3(existWorld.x, existWorld.y, 0));
                Rect exist = new Rect(existLocal - half, enemySize + Vector2.one * marginSize * 2);
                if (cand.Overlaps(exist)) { validLocal = false; break; }
            }

            if (validLocal)
            {
                chosenWorldPos = targetWorldPos;
                if (attempt > 0)
                {
                    Debug.Log($"Valid position found at attempt#{attempt}: local={spawnAreaLocal}, world={targetWorldPos}");
                }
                return WorldToPreZoomLocal(targetWorldPos);
            }
        }

        // フォールバック: スポーンエリア中央
        var fallbackWorld = enemySpawnArea.TransformPoint(rect.center);
        var fallbackLocal = rect.center;
        Debug.LogWarning($"GetRandomPreZoomLocalPosition: fallback to center. enemySize={enemySize}, margin={marginSize}, centerLocal={fallbackLocal}, centerWorld={fallbackWorld}");
        chosenWorldPos = fallbackWorld;
        return WorldToPreZoomLocal(fallbackWorld);
    }

    /// <summary>
    /// BattleGroupの敵リストに基づいて敵UIを配置（戦闘参加敵のみ）
    /// </summary>
    public async UniTask PlaceEnemiesFromBattleGroup(BattleGroup enemyGroup)
    {
        // NOTE: ProfilerMarker.Auto() は await を跨ぐとフレーム越境で警告/エラーになるため未使用
        if (enemyGroup?.Ours == null || enemyBattleLayer == null) return;
            if (enableVerboseEnemyLogs)
            {
                Debug.Log($"PlaceEnemiesFromBattleGroup開始: 敵数={enemyGroup.Ours.Count}");
            }

            var placedWorldPositions = new List<Vector2>();

            // スロットルあり: バッチ単位かつフレーム分散で逐次生成
            if (throttleEnemySpawns)
            {
                int batchCounter = 0;
                var batchCreated = new List<UIController>();
                foreach (var character in enemyGroup.Ours)
                {
                    if (character is NormalEnemy enemy)
                    {
                        Vector2 iconSize = (enemy.EnemyGraphicSprite != null)
                            ? enemy.EnemyGraphicSprite.rect.size
                            : new Vector2(100f, 100f);
                        float iconW = iconSize.x;
                        float barH  = iconW * hpBarSizeRatio.y;
                        float vSpace = iconW * hpBarSizeRatio.y * 0.5f;
                        float totalBarHeight = barH * 2f + vSpace;
                        Vector2 combinedSize = new Vector2(iconW, iconSize.y + vSpace + totalBarHeight);

                        var preZoomLocal = GetRandomPreZoomLocalPosition(
                            combinedSize,
                            placedWorldPositions,
                            enemyMargin,
                            out var chosenWorldPos);

                        placedWorldPositions.Add(chosenWorldPos);

                        var ui = await PlaceEnemyUI(enemy, preZoomLocal);
                        if (ui != null) batchCreated.Add(ui);

                        batchCounter++;
                        if (batchCounter >= Mathf.Max(1, enemySpawnBatchSize))
                        {
                            batchCounter = 0;
                            // バッチ分をまとめて有効化
                            using (global::MetricsHub.Instance.BeginSpan("PlaceEnemies.Activate", BuildMetricsContext()))
                            {
                                for (int i = 0; i < batchCreated.Count; i++)
                                {
                                    if (batchCreated[i] != null)
                                        batchCreated[i].gameObject.SetActive(true);
                                }
                            }
                            batchCreated.Clear();
                            for (int f = 0; f < Mathf.Max(0, enemySpawnInterBatchFrames); f++)
                            {
                                await UniTask.NextFrame();
                            }
                        }
                    }
                }
                // 余り分を最後に有効化
                if (batchCreated.Count > 0)
                {
                    using (global::MetricsHub.Instance.BeginSpan("PlaceEnemies.Activate", BuildMetricsContext()))
                    {
                        for (int i = 0; i < batchCreated.Count; i++)
                        {
                            if (batchCreated[i] != null)
                                batchCreated[i].gameObject.SetActive(true);
                        }
                    }
                }
            }
            else
            {
                // 旧挙動: 並列生成（スパイクが発生しやすい）
                var tasks = new List<UniTask<UIController>>();
                foreach (var character in enemyGroup.Ours)
                {
                    if (character is NormalEnemy enemy)
                    {
                        Vector2 iconSize = (enemy.EnemyGraphicSprite != null)
                            ? enemy.EnemyGraphicSprite.rect.size
                            : new Vector2(100f, 100f);
                        float iconW = iconSize.x;
                        float barH  = iconW * hpBarSizeRatio.y;
                        float vSpace = iconW * hpBarSizeRatio.y * 0.5f;
                        float totalBarHeight = barH * 2f + vSpace;
                        Vector2 combinedSize = new Vector2(iconW, iconSize.y + vSpace + totalBarHeight);

                        var preZoomLocal = GetRandomPreZoomLocalPosition(
                            combinedSize,
                            placedWorldPositions,
                            enemyMargin,
                            out var chosenWorldPos);
                        placedWorldPositions.Add(chosenWorldPos);

                        tasks.Add(PlaceEnemyUI(enemy, preZoomLocal));
                    }
                }
                var results = await UniTask.WhenAll(tasks);
                // まとめて有効化
                using (global::MetricsHub.Instance.BeginSpan("PlaceEnemies.Activate", BuildMetricsContext()))
                {
                    foreach (var ui in results)
                    {
                        if (ui != null) ui.gameObject.SetActive(true);
                    }
                }
            }
    }

    /// <summary>
    /// 個別の敵UIを配置（ズーム前座標で即座に配置）
    /// </summary>
    private UniTask<UIController> PlaceEnemyUI(NormalEnemy enemy, Vector2 preZoomLocalPosition)
    {
        if (enemyUIPrefab == null)
        {
            Debug.LogWarning("enemyUIPrefab が設定されていません。敵UIを生成できません。");
            return UniTask.FromResult<UIController>(null);
        }

        if (enemyBattleLayer == null)
        {
            Debug.LogWarning("enemyBattleLayerが設定されていません。");
            return UniTask.FromResult<UIController>(null);
        }
            UIController uiInstance = null;
            if (enableVerboseEnemyLogs)
            {
                Debug.Log($"[Prefab ref] enemyUIPrefab.activeSelf={enemyUIPrefab.gameObject.activeSelf}", enemyUIPrefab);
                Debug.Log($"[Parent] enemyBattleLayer activeSelf={enemyBattleLayer.gameObject.activeSelf}, inHierarchy={enemyBattleLayer.gameObject.activeInHierarchy}", enemyBattleLayer);
            }
#if UNITY_EDITOR
            if (enableVerboseEnemyLogs)
            {
                Debug.Log($"[Prefab path] {AssetDatabase.GetAssetPath(enemyUIPrefab)}", enemyUIPrefab);
                if (!enemyUIPrefab.gameObject.activeSelf)
                {
                    Debug.LogWarning($"[Detect] Prefab asset inactive at call. path={AssetDatabase.GetAssetPath(enemyUIPrefab)}\n{new System.Diagnostics.StackTrace(true)}", enemyUIPrefab);
                }
            }
#endif
            // 敵UIプレハブを生成（enemyBattleLayer直下）
            using (global::MetricsHub.Instance.BeginSpan("PlaceEnemies.Spawn", BuildMetricsContext()))
            {
                var uiInstanceSpawn = Instantiate(enemyUIPrefab, enemyBattleLayer, false);
                // 設定中は非アクティブにしてCanvas再構築を抑制
                uiInstanceSpawn.gameObject.SetActive(false);
                uiInstance = uiInstanceSpawn;
            }
            if (enableVerboseEnemyLogs)
            {
                Debug.Log($"[Instantiated] {uiInstance.name} activeSelf={uiInstance.gameObject.activeSelf}, inHierarchy={uiInstance.gameObject.activeInHierarchy}", uiInstance);
            }
            var rectTransform = (RectTransform)uiInstance.transform;
            using (global::MetricsHub.Instance.BeginSpan("PlaceEnemies.Layout", BuildMetricsContext()))
            {
                // ズーム前のローカル座標で配置（ズーム後に正しい位置に収まる）
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = preZoomLocalPosition;

                // アイコン設定（スプライトとサイズ）
                if (uiInstance.Icon != null)
                {
                    uiInstance.Icon.preserveAspect = true;
                    if (enemy.EnemyGraphicSprite != null)
                    {
                        uiInstance.Icon.sprite = enemy.EnemyGraphicSprite;
                        // 念のための安全初期化
                        uiInstance.Icon.type = UnityEngine.UI.Image.Type.Simple;
                        uiInstance.Icon.useSpriteMesh = true;
                        uiInstance.Icon.color = Color.white;
                        uiInstance.Icon.material = null;

                        if (enableVerboseEnemyLogs)
                        {
                            var spr = uiInstance.Icon.sprite;
                            Debug.Log($"Enemy Icon sprite assigned: name={spr.name}, tex={(spr.texture != null ? spr.texture.name : "<no texture>")}, rect={spr.rect}, ppu={spr.pixelsPerUnit}");
                        }
                    }
                    else
                    {
                        // スプライトが無い場合は白矩形が出ないように一旦非表示
                        uiInstance.Icon.enabled = false;
                        Debug.LogWarning($"Enemy Icon sprite is NULL for enemy: {enemy.CharacterName}. Icon will be hidden to avoid white box.\nCheck: NormalEnemy.EnemyGraphicSprite assignment and Texture import type (Sprite [2D and UI]).");
                    }

                    // サイズ決定（EnemySize優先、未設定ならSpriteサイズ）
                    var iconRT = (RectTransform)uiInstance.Icon.transform;
                    if (uiInstance.Icon.sprite != null)
                    {
                        iconRT.sizeDelta = uiInstance.Icon.sprite.rect.size;
                        uiInstance.Icon.SetNativeSize();
                        uiInstance.Icon.enabled = true;
                    }
                    else
                    {
                        iconRT.sizeDelta = new Vector2(100f, 100f);
                    }
                }

                // UIの初期化
                uiInstance.Init();

                // HPバー設定（プレハブ内のCombinedStatesBarを利用）
                if (uiInstance.HPBar != null)
                {
                    float iconW = (uiInstance.Icon != null)
                        ? ((RectTransform)uiInstance.Icon.transform).sizeDelta.x
                        : rectTransform.sizeDelta.x;

                    float barW = iconW * hpBarSizeRatio.x;
                    float barH = iconW * hpBarSizeRatio.y;
                    uiInstance.HPBar.SetSize(barW, barH);

                    float verticalSpacing = iconW * hpBarSizeRatio.y * 0.5f;
                    uiInstance.HPBar.VerticalSpacing = verticalSpacing;

                    var barRT = (RectTransform)uiInstance.HPBar.transform;
                    barRT.pivot = new Vector2(0.5f, 1f);
                    barRT.anchorMin = barRT.anchorMax = new Vector2(0.5f, 0f);
                    barRT.anchoredPosition = new Vector2(0f, -verticalSpacing);

                    uiInstance.HPBar.SetBothBarsImmediate(
                        enemy.HP / enemy.MaxHP,
                        enemy.MentalHP / enemy.MaxHP,
                        enemy.GetMentalDivergenceThreshold());

                    float totalBarHeight = barH * 2f + uiInstance.HPBar.VerticalSpacing;
                    float iconHeight = (uiInstance.Icon != null) ? ((RectTransform)uiInstance.Icon.transform).sizeDelta.y : 0f;
                    float totalHeight = iconHeight + verticalSpacing + totalBarHeight;
                    rectTransform.sizeDelta = new Vector2(Mathf.Max(rectTransform.sizeDelta.x, iconW), totalHeight);

                    if (enableVerboseEnemyLogs)
                    {
                        Debug.Log($"敵UI配置完了: {enemy.GetHashCode()} at preZoomLocal={preZoomLocalPosition}, IconW: {iconW}, Bar: {barW}x{barH}");
                    }
                }
            }

            // BaseStatesへバインド
            enemy.BindUIController(uiInstance);
            
            // ここでは有効化せず、呼び出し側でバッチ一括有効化する
            return UniTask.FromResult(uiInstance);
    }

    
/// <summary>
    /// 歩行の度に更新されるSideObjectの管理
    /// </summary>
    private void SideObjectManage(StageCut nowStageCut, SideObject_Type type, Color themeColor,Color twoColor)
    {

        var GetObjects = nowStageCut.GetRandomSideObject();//サイドオブジェクトLEFTとRIGHTを取得

        //サイドオブジェクト二つ分の生成
        for(int i =0; i < 2; i++)
        {
            if (TwoObjects[i] != null)
            {
                SideObjectMoves[i].FadeOut().Forget();//フェードアウトは待たずに処理をする。
            }

            TwoObjects[i] = Instantiate(GetObjects[i], bgRect);//サイドオブジェクトを生成、配列に代入
            var LineObject = TwoObjects[i].GetComponent<UILineRenderer>();
            LineObject.sideObject_Type = type;//引数のタイプを渡す。
            // ステージテーマ色を適用（フェードイン初期値に反映されるようStart前にセット）
            if (LineObject != null)
            {
                LineObject.lineColor = themeColor;
                LineObject.two = twoColor;
                LineObject.SetVerticesDirty();
            }
            SideObjectMoves[i] = TwoObjects[i].GetComponent<SideObjectMove>();//スクリプトを取得
            SideObjectMoves[i].boostSpeed=3.0f;//スピードを初期化
            LiveSideObjects[i].Add(SideObjectMoves[i]);//生きているリスト(左右どちらか)に追加
            //Debug.Log("サイドオブジェクト生成[" + i +"]");

            //数が多くなりだしたら
            /*if (LiveSideObjects[i].Count > 2) {
                SideObjectMoves[i].boostSpeed = 3.0f;//スピードをブースト

            }*/

        }
    }

    /// <summary>
    ///     簡易マップ現在地のUI更新とその処理
    /// </summary>
    private void NowImageCalc(StageCut sc, PlayersStates player)
    {
        //進行度自体の割合を計算
        var Ratio = (float)player.NowProgress / (sc.AreaDates.Count - 1);
        //進行度÷エリア数(countだから-1) 片方キャストしないと整数同士として小数点以下切り捨てられる。
        //Debug.Log("現在進行度のエリア数に対する割合"+Ratio);

        //lerpがベクトルを設定してくれる、調整された位置を渡す
        MapImg.LocationSet(Vector2.Lerp(sc.MapLineS, sc.MapLineE, Ratio));
    }

    private bool _isZoomAnimating;
    private bool _isAllySlideAnimating;
}