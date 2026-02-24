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

/// <summary>
/// WatchUIUpdateクラスに戦闘画面用のレイヤー分離システムを追加しました。
/// 背景と敵は一緒にズームし、味方アイコンは独立してスライドインします。
/// 戦闘エリアはズーム後の座標系で直接デザイン可能です。
///
/// Phase 1リファクタリング: IViewportController, IActionMarkController, IKZoomController実装
/// </summary>
public partial class WatchUIUpdate : MonoBehaviour,
    IViewportController,
    IActionMarkController,
    IKZoomController,
    IIntroContextProvider,
    IEnemyPlacementContextProvider
{
    // シングルトン参照
    public static WatchUIUpdate Instance { get; private set; }

    // プロファイラーマーカー（HUDでも参照できる固定名）
    private static readonly ProfilerMarker kPrepareIntro = new ProfilerMarker("WUI.PrepareIntro");
    private static readonly ProfilerMarker kPlayIntro    = new ProfilerMarker("WUI.PlayIntro");
    private static readonly ProfilerMarker kPlaceEnemies = new ProfilerMarker("WUI.PlaceEnemies");

    /// <summary>
    /// Orchestrator 経由でズーム原状復帰を行う任意呼び出しの導線。
    /// トグルOFF時は従来の RestoreOriginalTransforms にフォールバックします。
    /// </summary>
    public async UniTask RestoreZoomViaOrchestrator(bool animated = false, float duration = 0f)
    {
        try
        {
            var token = _sweepCts != null ? _sweepCts.Token : System.Threading.CancellationToken.None;
            var intro = IntroOrchestrator;
            Debug.Log($"[WatchUIUpdate] RestoreZoomViaOrchestrator animated={animated} duration={duration}");
            if (intro != null)
            {
                await intro.RestoreAsync(animated, duration, token);
            }
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
            var zoom = Viewport?.Zoom;
            _orchestrator = new DefaultIntroOrchestrator(new WuiEnemyPlacerAdapter(), zoom);
        }
    }

    private IIntroContext BuildIntroContextForOrchestrator()
    {
        var frontRect = zoomFrontContainer as RectTransform;
        var backRect  = zoomBackContainer  as RectTransform;
        // Diagnostics: ズームスキップの原因特定用
        Debug.Log($"[WUI] IntroContext: front={(frontRect!=null)} back={(backRect!=null)} enableZoom={enableZoomAnimation} gotoScale={_gotoScaleXY} gotoPos={_gotoPos} dur={_firstZoomSpeedTime}");
        if (frontRect == null && backRect == null)
        {
            Debug.LogWarning("[WUI] Zoom containers are both null. zoomFrontContainer/zoomBackContainer の割当を確認してください。");
        }
        return new IntroContext(
            "Runtime",
            -1,
            "-",
            null,
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

    IIntroContext IIntroContextProvider.BuildIntroContext()
        => BuildIntroContextForOrchestrator();

    IEnemyPlacementContext IEnemyPlacementContextProvider.BuildPlacementContext(BattleGroup enemyGroup)
        => BuildPlacementContext(enemyGroup);

    // キャンセル制御用
    private CancellationTokenSource _sweepCts;

    // Orchestrator（I/F注入）。当面は内部でデフォルト生成し、段階的に移行する
    private global::IIntroOrchestrator _orchestrator;
    private IIntroOrchestratorFacade _introFacade;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    // ===== Kモード: パッシブ一覧表示（フェードイン） =====
    private BaseStates FindActorByUI(BattleIconUI ui)
    {
        var battle = BattleUIBridge.Active?.BattleContext;
        var all = battle?.AllCharacters;
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
    public RectTransform SideObjectRoot => bgRect;

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
    //敵UI最適化
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
    
    // 敵UIプレハブ（BattleIconUI付き）
    [Header("敵UI Prefab")]
    [SerializeField] private BattleIconUI enemyUIPrefab;

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

    // Phase 2: ViewportController への委譲
    private ViewportController _viewportController;

    /// <summary>
    /// ビューポートコントローラーへのアクセス（外部からの直接参照用）。
    /// Phase 2で追加: WatchUIUpdate.Instance.Viewport として利用可能。
    /// </summary>
    public ViewportController Viewport
    {
        get
        {
            if (_viewportController == null)
            {
                _viewportController = new ViewportController(
                    zoomBackContainer as RectTransform,
                    zoomFrontContainer as RectTransform
                );
            }
            return _viewportController;
        }
    }

    /// <summary>
    /// Orchestratorファサードへのアクセス（Intro/Restoreなどを文脈込みで実行）。
    /// </summary>
    public IIntroOrchestratorFacade IntroOrchestrator
    {
        get
        {
            EnsureOrchestrator();
            if (_introFacade == null)
            {
                _introFacade = new IntroOrchestratorFacade(
                    _orchestrator,
                    this,
                    this,
                    () => _sweepCts != null ? _sweepCts.Token : System.Threading.CancellationToken.None);
            }
            return _introFacade;
        }
    }

    #region IViewportController実装（ViewportControllerへの委譲）

    /// <summary>ズーム制御（IZoomController）- ViewportControllerへ委譲</summary>
    public IZoomController Zoom => Viewport.Zoom;

    /// <summary>ズームする背景レイヤー - ViewportControllerへ委譲</summary>
    public RectTransform ZoomBackContainer => Viewport.ZoomBackContainer;

    /// <summary>ズームする前景レイヤー - ViewportControllerへ委譲</summary>
    public RectTransform ZoomFrontContainer => Viewport.ZoomFrontContainer;

    /// <summary>共通背景への参照 - ViewportControllerへ委譲</summary>
    public Transform Background => Viewport.Background;

    #endregion

    // Phase 3: ActionMarkController への委譲
    private ActionMarkController _actionMarkController;

    /// <summary>
    /// ActionMarkコントローラーへのアクセス（外部からの直接参照用）。
    /// Phase 3で追加: WatchUIUpdate.Instance.ActionMarkCtrl として利用可能。
    /// </summary>
    public ActionMarkController ActionMarkCtrl
    {
        get
        {
            if (_actionMarkController == null)
            {
                _actionMarkController = new ActionMarkController(
                    actionMark,
                    actionMarkSpawnPoint,
                    WaitBattleIntroAnimations  // アニメーション待機用デリゲート
                );
            }
            return _actionMarkController;
        }
    }

    // Phase 3b: KZoomController への委譲
    private KZoomController _kZoomController;
    private KZoomConfig _kZoomConfig;
    private KZoomState _kZoomState;

    /// <summary>
    /// KZoomコントローラーへのアクセス（外部からの直接参照用）。
    /// Phase 3bで追加: WatchUIUpdate.Instance.KZoomCtrl として利用可能。
    /// </summary>
    public KZoomController KZoomCtrl
    {
        get
        {
            if (_kZoomController == null)
            {
                // Config作成
                _kZoomConfig = new KZoomConfig
                {
                    ZoomRoot = kZoomRoot,
                    TargetRect = kTargetRect,
                    FitBlend = kFitBlend01,
                    ZoomDuration = kZoomDuration,
                    ZoomEase = kZoomEase,
                    NameText = kNameText,
                    PassivesText = kPassivesText,
                    TextSlideDuration = kTextSlideDuration,
                    TextSlideEase = kTextSlideEase,
                    TextSlideOffsetX = kTextSlideOffsetX,
                    PassivesFadeDuration = kPassivesFadeDuration,
                    PassivesEllipsisDotCount = kPassivesEllipsisDotCount,
                    PassivesFitSafety = kPassivesFitSafety,
                    PassivesAlwaysAppendEllipsis = kPassivesAlwaysAppendEllipsis,
                    PassivesUseRectMask = kPassivesUseRectMask,
                    PassivesDebugMode = kPassivesDebugMode,
                    PassivesDebugCount = kPassivesDebugCount,
                    PassivesDebugPrefix = kPassivesDebugPrefix,
                    DisableIconClickWhileBattleZoom = disableIconClickWhileBattleZoom
                };

                // State作成
                _kZoomState = new KZoomState();

                // Controller作成
                _kZoomController = new KZoomController(
                    _kZoomConfig,
                    _kZoomState,
                    ActionMarkCtrl,
                    () => BattleUIBridge.Active?.BattleContext?.AllCharacters,
                    () => _isZoomAnimating,
                    () => _isAllySlideAnimating,
                    visible => SchizoLog.Instance?.SetVisible(visible),
                    () => SchizoLog.Instance?.IsVisible() ?? false,
                    GetOrSetupTMPForBackground,
                    FitTextIntoRectWithEllipsis
                );
            }
            return _kZoomController;
        }
    }

    // Phase 3c: EnemyPlacementController への委譲
    private EnemyPlacementController _enemyPlacementController;
    private EnemyPlacementConfig _enemyPlacementConfig;

    /// <summary>
    /// EnemyPlacementコントローラーへのアクセス（外部からの直接参照用）。
    /// Phase 3cで追加: WatchUIUpdate.Instance.EnemyPlacementCtrl として利用可能。
    /// </summary>
    public EnemyPlacementController EnemyPlacementCtrl
    {
        get
        {
            if (_enemyPlacementController == null)
            {
                // Config作成
                _enemyPlacementConfig = new EnemyPlacementConfig
                {
                    BattleLayer = enemyBattleLayer,
                    EnemyUIPrefab = enemyUIPrefab,
                    SpawnArea = enemySpawnArea,
                    HpBarSizeRatio = hpBarSizeRatio,
                    Margin = enemyMargin,
                    ThrottleSpawns = throttleEnemySpawns,
                    SpawnBatchSize = enemySpawnBatchSize,
                    SpawnInterBatchFrames = enemySpawnInterBatchFrames,
                    EnableVerboseLogs = enableVerboseEnemyLogs
                };

                // Controller作成
                _enemyPlacementController = new EnemyPlacementController(
                    _enemyPlacementConfig,
                    () => _gotoPos,
                    () => _gotoScaleXY
                );
            }
            return _enemyPlacementController;
        }
    }

    // Phase 4: WalkingUIController への委譲
    private WalkingUIController _walkingBattleIconUI;

    /// <summary>
    /// WalkingUIコントローラーへのアクセス（外部からの直接参照用）。
    /// Phase 4で追加: WatchUIUpdate.Instance.WalkingUICtrl として利用可能。
    /// </summary>
    public WalkingUIController WalkingUICtrl
    {
        get
        {
            if (_walkingBattleIconUI == null)
            {
                _walkingBattleIconUI = new WalkingUIController(
                    StagesString,
                    bgRect,
                    ActionMarkCtrl
                );
            }
            return _walkingBattleIconUI;
        }
    }

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
    [SerializeField] private bool disableIconClickWhileBattleZoom = true; // 既存ズーム中はアイコンクリック無効

    // Kモード内部状態
    private bool _isKActive = false;
    private bool _isKAnimating = false;
    private CancellationTokenSource _kCts;
    private Vector2 _kOriginalPos;
    private Vector3 _kOriginalScale;
    // Kズーム前のトランスフォーム保存が有効か（EnterKで保存されたか）
    private bool _kSnapshotValid = false;
    // Kパッシブ表示: フィット用の生トークン文字列を保持（再フィット用）
    private string _kPassivesTokensRaw = string.Empty;
    // Kパッシブ表示: 子TMPキャッシュ
    private TMP_Text _kPassivesTMP;
    // K中: クリック元のUI（BattleIconUI）で、Icon以外の子を一時的にOFFにするための参照
    private BattleIconUI _kExclusiveUI;
    // K開始時のActionMark表示状態を退避
    private bool _actionMarkWasActiveBeforeK = false;
    // K開始時のSchizoLog表示状態を退避
    private bool _schizoWasVisibleBeforeK = false;
    // K中: 対象以外のBattleIconUIの有効状態を退避して一時的に非表示にする
    private List<(BattleIconUI ui, bool wasActive)> _kHiddenOtherUIs;

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
    
    [Header("ウォームアップ（初回カクつき低減）")]
    [Tooltip("起動時に各種システムの初期化を先に済ませ、初回ズーム/スライドのカクつきを抑えます。")]
    [SerializeField] private bool warmupOnStart = true;
    [Tooltip("UniTaskのPlayerLoop初期化など、最小限の非同期初期化を先に行います。")]
    [SerializeField] private bool warmupUniTask = true;
    [Tooltip("LitMotionのバインディング/拡張(AnchoredPosition等)の初期化を先に行います。")]
    [SerializeField] private bool warmupLitMotion = false;
    [Tooltip("TMP背景計測やテキストバウンディングの初回確定を先に行います（kPassivesTextが設定されている場合）。")]
    [SerializeField] private bool warmupTMPBackground = true;
    [Tooltip("Canvas/レイアウトの強制更新を行い、初回のレイアウト確定コストを分散します。")]
    [SerializeField] private bool warmupCanvasRebuild = true;
    [Tooltip("時間はかかりますが、必要に応じて全シェーダのウォームアップを行います（ビルド設定に依存）。")]
    [SerializeField] private bool warmupShaders = false;


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

        // 初回カクつき低減のためのウォームアップを起動時に実行（任意）
        if (warmupOnStart)
        {
            WarmupIntroSystemsAsync().Forget();
        }
    }

    /// <summary>
    /// 初回ズーム/スライド/配置で発生しがちな初期化コストを、起動直後に先に済ませるウォームアップ処理。
    /// 実際のアニメーションや可視状態は変えずに、内部のJIT/初回バインド/レイアウト確定等のみを誘発します。
    /// </summary>
    private async UniTask WarmupIntroSystemsAsync()
    {
        try
        {
            // Orchestratorの遅延生成を事前に済ませる
            EnsureOrchestrator();

            // 1) UniTaskのPlayerLoop初期化（最小コスト）
            if (warmupUniTask)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // 2) LitMotionの初期化（AnchoredPositionバインド等）
            if (warmupLitMotion)
            {
                var dummyGo = new GameObject("WUI_Warmup_DummyRT");
                var rt = dummyGo.AddComponent<RectTransform>();
                rt.SetParent(this.transform, false);
                rt.anchoredPosition = Vector2.zero;
                try
                {
                    // 0秒モーションを起動し、内部のディスパッチャ/バインディング初期化のみを誘発（awaitしない）
                    _ = LMotion.Create(Vector2.zero, new Vector2(1, 1), 0f)
                        .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                        .BindToAnchoredPosition(rt);
                }
                catch { /* no-op（起動直後で親Canvas未確定でも問題なし）*/ }
                finally
                {
                    if (dummyGo != null) Destroy(dummyGo);
                }
            }

            // 3) TMP背景（kPassives）関連のレイアウト確定と背景生成（不可視のまま）
            if (warmupTMPBackground && kPassivesText != null)
            {
                var go = kPassivesText.gameObject;
                var cg = go.GetComponent<CanvasGroup>();
                if (cg == null) cg = go.AddComponent<CanvasGroup>();
                float prevAlpha = cg.alpha;
                bool prevActive = go.activeSelf;
                try
                {
                    cg.alpha = 0f; // 不可視
                    go.SetActive(true);
                    // TMP参照の取得/生成と背景生成を先に済ませる
                    _kPassivesTMP = GetOrSetupTMPForBackground(kPassivesText, _kPassivesTMP, kPassivesUseRectMask);
                    kPassivesText.RefreshBackground();
                    Canvas.ForceUpdateCanvases();
                }
                catch { /* no-op */ }
                finally
                {
                    // 状態を元に戻す
                    cg.alpha = prevAlpha;
                    go.SetActive(prevActive);
                }
            }

            // 4) Canvas/レイアウトの強制確定
            if (warmupCanvasRebuild)
            {
                try { Canvas.ForceUpdateCanvases(); } catch { /* no-op */ }
            }

            // 5) シェーダウォームアップ（必要時のみ。処理時間が長い可能性あり）
            if (warmupShaders)
            {
                try { Shader.WarmupAllShaders(); } catch { /* no-op */ }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WUI.Warmup] Failed: {ex.Message}");
        }
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
    ///     新歩行システム向けのUI更新
    ///     Phase 4: WalkingUIControllerに委譲
    /// </summary>
    public void ApplyNodeUI(string displayName, NodeUIHints hints)
        => WalkingUICtrl.ApplyNodeUI(displayName, hints);

    /// <summary>
    /// エンカウントしたら最初にズームする処理（改良版）
    /// </summary>
    public async UniTask FirstImpressionZoomImproved()
    {
        // 準備（計算）→ 一斉起動（アニメ）の二段階でスパイクを抑制
        IntroMotionPlan plan;
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

        // アニメーション実行
        await PlayIntroMotions(plan, introPreAnimationDelaySec);
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

            // 敵UI生成（従来はZoom開始時に並行起動していたものをここで起動）
            var currentBattle = BattleUIBridge.Active?.BattleContext;
            if (currentBattle?.EnemyGroup != null)
            {
                var placeTask = UniTask.Create(async () =>
                {
                    EnsureOrchestrator();
                    var ictx = BuildIntroContextForOrchestrator();
                    var pctx = BuildPlacementContext(currentBattle.EnemyGroup);
                    var token = _sweepCts != null ? _sweepCts.Token : System.Threading.CancellationToken.None;
                    await _orchestrator.PlaceEnemiesAsync(ictx, pctx, token);
                });
                tasks.Add(placeTask);
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
                    Debug.Log("[Intro] Queue Zoom task via Orchestrator.PlayAsync()");
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
                    var ours = BattleUIBridge.Active?.BattleContext?.AllyGroup?.Ours;
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

            _isZoomAnimating = false;
            _isAllySlideAnimating = false;
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
    /// Phase 3: ActionMarkControllerへ委譲
    /// </summary>
    public void MoveActionMarkToIcon(RectTransform targetIcon, bool immediate = false)
        => ActionMarkCtrl.MoveToIcon(targetIcon, immediate);

    /// <summary>
    /// スケール補正付きでアイコンへ移動（ズーム/スライドの見かけスケール差を補正）
    /// Phase 3: ActionMarkControllerへ委譲
    /// </summary>
    public void MoveActionMarkToIconScaled(RectTransform targetIcon, bool immediate = false)
        => ActionMarkCtrl.MoveToIconScaled(targetIcon, immediate);

    /// <summary>
    /// アクションマークを指定アクター（BaseStates）のUIアイコンへ移動
    /// Phase 3: ActionMarkControllerへ委譲
    /// </summary>
    public void MoveActionMarkToActor(BaseStates actor, bool immediate = false)
        => ActionMarkCtrl.MoveToActor(actor, immediate);

    /// <summary>
    /// ズーム/スライド完了を待ってから、スケール補正付きでアクションマークを移動
    /// Phase 3: ActionMarkControllerへ委譲
    /// </summary>
    public UniTask MoveActionMarkToActorScaled(BaseStates actor, bool immediate = false, bool waitAnimations = true)
        => ActionMarkCtrl.MoveToActorScaled(actor, immediate, waitAnimations);

    // ComputeScaleRatioForTarget と GetWorldScaleXY は ActionMarkController に移動済み
    // 以下は後方互換のため残す（他で使用されている可能性）
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

    // ===== K MODE (ステータス拡大) - Phase 3b: KZoomControllerへ委譲 =====
    /// <summary>
    /// Kモードに入れるかどうか（戦闘導入ズームや味方スライドが走っている場合は抑制可能）
    /// Phase 3b: KZoomControllerへ委譲
    /// </summary>
    public bool CanEnterK => KZoomCtrl.CanEnterK;

    /// <summary>
    /// Kモードがアクティブか
    /// Phase 3b: KZoomControllerへ委譲
    /// </summary>
    public bool IsKActive => KZoomCtrl.IsKActive;

    /// <summary>
    /// Kモードのアニメーション中か
    /// Phase 3b: KZoomControllerへ委譲
    /// </summary>
    public bool IsKAnimating => KZoomCtrl.IsKAnimating;

    /// <summary>
    /// 現在のKズーム対象UIかどうか
    /// Phase 3b: KZoomControllerへ委譲
    /// </summary>
    public bool IsCurrentKTarget(BattleIconUI ui) => KZoomCtrl.IsCurrentKTarget(ui);

    /// <summary>
    /// 指定アイコンをkTargetRectにフィットさせるように、kZoomRootをスケール・移動させてKモード突入
    /// Phase 3b: KZoomControllerへ委譲
    /// </summary>
    public UniTask EnterK(RectTransform iconRT, string title) => KZoomCtrl.EnterK(iconRT, title);

    /// <summary>
    /// Kモード解除（アニメーションあり）
    /// Phase 3b: KZoomControllerへ委譲
    /// </summary>
    public UniTask ExitK() => KZoomCtrl.ExitK();

    /// <summary>
    /// Kモードを即時解除（キャンセルやNextWaitで使用）
    /// Phase 3b: KZoomControllerへ委譲
    /// </summary>
    public void ForceExitKImmediate() => KZoomCtrl.ForceExitKImmediate();

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
        RectTransformUtil.GetWorldRect(iconRT, out var iconCenter, out var iconSize);
        RectTransformUtil.GetWorldRect(kTargetRect, out var targetCenter, out var targetSize);

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


    // ActionMark の表示/非表示ファサード（Phase 3: ActionMarkControllerへ委譲）
    public void ShowActionMark() => ActionMarkCtrl.Show();

    public void HideActionMark() => ActionMarkCtrl.Hide();

    /// <summary>
    /// 特別版: スポーン位置(actionMarkSpawnPoint)の中心に0サイズで出す
    /// Phase 3: ActionMarkControllerへ委譲
    /// </summary>
    public void ShowActionMarkFromSpawn(bool zeroSize = true) => ActionMarkCtrl.ShowFromSpawn(zeroSize);

    #region IActionMarkController実装（ActionMarkControllerへの委譲）

    void IActionMarkController.MoveToIcon(RectTransform targetIcon, bool immediate)
        => ActionMarkCtrl.MoveToIcon(targetIcon, immediate);

    void IActionMarkController.MoveToIconScaled(RectTransform targetIcon, bool immediate)
        => ActionMarkCtrl.MoveToIconScaled(targetIcon, immediate);

    void IActionMarkController.MoveToActor(BaseStates actor, bool immediate)
        => ActionMarkCtrl.MoveToActor(actor, immediate);

    UniTask IActionMarkController.MoveToActorScaled(BaseStates actor, bool immediate, bool waitAnimations)
        => ActionMarkCtrl.MoveToActorScaled(actor, immediate, waitAnimations);

    void IActionMarkController.Show() => ActionMarkCtrl.Show();

    void IActionMarkController.Hide() => ActionMarkCtrl.Hide();

    void IActionMarkController.ShowFromSpawn(bool zeroSize) => ActionMarkCtrl.ShowFromSpawn(zeroSize);

    void IActionMarkController.SetStageThemeColor(Color color) => ActionMarkCtrl.SetStageThemeColor(color);

    bool IActionMarkController.IsVisible => ActionMarkCtrl.IsVisible;

    #endregion

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
    /// Phase 3c: EnemyPlacementControllerに委譲
    /// </summary>
    public UniTask PlaceEnemiesFromBattleGroup(BattleGroup enemyGroup)
        => EnemyPlacementCtrl.PlaceEnemiesAsync(enemyGroup);

    // ========================================================================
    // 以下のメソッドはPhase 3cでEnemyPlacementControllerへ移動済み
    // 後方互換のため削除せず残しているがコントローラー経由で呼び出されるようになった
    // ========================================================================

#if false
    // NOTE: 以下は EnemyPlacementController に移動済みのため無効化
    private async UniTask PlaceEnemiesFromBattleGroup_Legacy(BattleGroup enemyGroup)
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
                var batchCreated = new List<BattleIconUI>();
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
                            for (int i = 0; i < batchCreated.Count; i++)
                            {
                                if (batchCreated[i] != null)
                                    batchCreated[i].gameObject.SetActive(true);
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
                    for (int i = 0; i < batchCreated.Count; i++)
                    {
                        if (batchCreated[i] != null)
                            batchCreated[i].gameObject.SetActive(true);
                    }
                }
            }
            else
            {
                // 旧挙動: 並列生成（スパイクが発生しやすい）
                var tasks = new List<UniTask<BattleIconUI>>();
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
                foreach (var ui in results)
                {
                    if (ui != null) ui.gameObject.SetActive(true);
                }
            }
    }

    /// <summary>
    /// 個別の敵UIを配置（ズーム前座標で即座に配置）
    /// </summary>
    private UniTask<BattleIconUI> PlaceEnemyUI(NormalEnemy enemy, Vector2 preZoomLocalPosition)
    {
        if (enemyUIPrefab == null)
        {
            Debug.LogWarning("enemyUIPrefab が設定されていません。敵UIを生成できません。");
            return UniTask.FromResult<BattleIconUI>(null);
        }

        if (enemyBattleLayer == null)
        {
            Debug.LogWarning("enemyBattleLayerが設定されていません。");
            return UniTask.FromResult<BattleIconUI>(null);
        }
            BattleIconUI uiInstance = null;
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
            var uiInstanceSpawn = Instantiate(enemyUIPrefab, enemyBattleLayer, false);
            // 設定中は非アクティブにしてCanvas再構築を抑制
            uiInstanceSpawn.gameObject.SetActive(false);
            uiInstance = uiInstanceSpawn;
            if (enableVerboseEnemyLogs)
            {
                Debug.Log($"[Instantiated] {uiInstance.name} activeSelf={uiInstance.gameObject.activeSelf}, inHierarchy={uiInstance.gameObject.activeInHierarchy}", uiInstance);
            }
            var rectTransform = (RectTransform)uiInstance.transform;
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
            enemy.BindBattleIconUI(uiInstance);
            
            // ここでは有効化せず、呼び出し側でバッチ一括有効化する
            return UniTask.FromResult(uiInstance);
    }
#endif

    private bool _isZoomAnimating;
    private bool _isAllySlideAnimating;
}
