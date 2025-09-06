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

/// <summary>
/// WatchUIUpdateクラスに戦闘画面用のレイヤー分離システムを追加しました。
/// 背景と敵は一緒にズームし、味方アイコンは独立してスライドインします。
/// 戦闘エリアはズーム後の座標系で直接デザイン可能です。
/// </summary>
public class WatchUIUpdate : MonoBehaviour
{
    // シングルトン参照
    public static WatchUIUpdate Instance { get; private set; }

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
    // ズーム前の初期状態を自動保存（実行時に設定）
    private Vector2 _originalBackPos;
    private Vector2 _originalBackScale;
    private Vector2 _originalFrontPos;
    private Vector2 _originalFrontScale;
    
    [Header("ズームアニメ有効/無効")]
    [SerializeField] private bool enableZoomAnimation = true;      // 背景/敵ズームアニメを行うか
    [SerializeField] private bool enableRestoreAnimation = true;   // 復元時にアニメを行うか（falseなら即時復元）

    GameObject[] TwoObjects;//サイドオブジェクトの配列
    SideObjectMove[] SideObjectMoves = new SideObjectMove[2];//サイドオブジェクトのスクリプトの配列
    List<SideObjectMove>[] LiveSideObjects = new List<SideObjectMove>[2] { new List<SideObjectMove>(), new List<SideObjectMove>() };//生きているサイドオブジェクトのリスト 間引き用

    // 戦闘画面レイヤー構成
    [Header("戦闘画面レイヤー構成")]
    [SerializeField] private Transform enemyBattleLayer;     // 敵配置レイヤー（背景と一緒にズーム）
    [SerializeField] private Transform allyBattleLayer;      // 味方アイコンレイヤー（独立アニメーション）

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
    
    // パフォーマンス/ログ設定
    [Header("パフォーマンス/ログ設定")]
    [Header("詳細: enableVerboseEnemyLogs\ntrue: 敵UI配置処理の詳細ログをConsoleに出力（開発/デバッグ向け）\nfalse: 最低限のみ出力\n注意: ログが多いとEditorでのフレーム落ち/GCを誘発する場合があります。ビルドではOFF推奨")]
    [SerializeField] private bool enableVerboseEnemyLogs = false; // 敵配置周りの詳細ログを出す

    [Header("詳細: throttleEnemySpawns\ntrue: 敵UI生成を複数フレームへ分散し、CPUスパイク/Canvas Rebuildの山を緩和\nfalse: 1フレームに一括生成（見た目は即時だがスパイクが出やすい）\n対象: 大量スポーン/低スペ端末ではtrue推奨")]
    [SerializeField] private bool throttleEnemySpawns = true;     // 敵UI生成をフレームに分散する

    [Header("詳細: enemySpawnBatchSize\n1フレームあたりに生成する敵UIの数\n小さいほど1フレームの負荷は下がるが、全員が出揃うまでの時間は伸びる\n目安: 1-3（モバイル/多数）、4-8（PC/少数）")]
    [SerializeField] private int enemySpawnBatchSize = 2;         // 何体ごとに小休止するか（最小1）

    [Header("詳細: enemySpawnInterBatchFrames\nバッチ間で待機するフレーム数\n0: 毎フレ連続で処理／1: 1フレ休む／2+: さらに分散（ポップインが目立つ可能性）\n目安: 0-2 推奨")]
    [SerializeField] private int enemySpawnInterBatchFrames = 1;  // バッチ間で待機するフレーム数


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
        // 背景+敵レイヤーのズーム、味方アイコンのスライドイン、敵UI生成を同時実行
        var zoomTask = ZoomBackgroundAndEnemies();
        var slideTask = SlideInAllyIcons();
        
        // 敵UI生成も同時に開始（ズーム開始と同タイミング）
        // 注意: 敵UI生成は別途PlaceEnemiesFromBattleGroupで呼び出されることを前提
        
        // 両方のアニメーションが完了するまで待機
        await UniTask.WhenAll(zoomTask, slideTask);
        
        Debug.Log("ズームアニメーション完了。敵UIが正しい位置に配置されているはずです。");
    }

    /// <summary>
    /// 背景と敵コンテナを同時にズーム（新方式）
    /// </summary>
    private async UniTask ZoomBackgroundAndEnemies()
    {
        // Kモード中は既存の戦闘ズームを抑制
        if (lockBattleZoomDuringK && (_isKActive || _isKAnimating))
        {
            Debug.Log("[WatchUIUpdate] Kモード中のためZoomBackgroundAndEnemiesをスキップ");
            return;
        }
        _isZoomAnimating = true;
        var tasks = new List<UniTask>();
        
        Debug.Log($"複数コンテナズーム開始: 目標スケール={_gotoScaleXY}, 目標位置={_gotoPos}");
        
        // ズーム前の初期状態を自動保存
        SaveOriginalTransforms();
        
        // ★ ズームと同時に敵UIを生成（並行実行でレスポンシブに）
        var currentBattleManager = Walking.Instance.bm;
        if (currentBattleManager?.EnemyGroup != null)
        {
            Debug.Log("ズームと同時に敵UI生成を開始");
            PlaceEnemiesFromBattleGroup(currentBattleManager.EnemyGroup).Forget();
        }
        
        // インスペクタでズーム無効の場合は、ここでスキップ（敵UI生成は実行済み）
        if (!enableZoomAnimation)
        {
            Debug.Log("[WatchUIUpdate] enableZoomAnimation=false のためズーム処理をスキップします。");
            _isZoomAnimating = false;
            return;
        }
        
        // ZoomBackContainer（背景）のズーム
        if (zoomBackContainer != null)
        {
            var backRect = zoomBackContainer as RectTransform;
            if (backRect != null)
            {
                var nowScale = new Vector2(backRect.localScale.x, backRect.localScale.y);
                var nowPos = new Vector2(backRect.anchoredPosition.x, backRect.anchoredPosition.y);
                
                Debug.Log($"ZoomBackContainer: 現在スケール={nowScale}, 現在位置={nowPos}");
                
                var scaleTask = LMotion.Create(nowScale, _gotoScaleXY, _firstZoomSpeedTime)
                    .WithEase(_firstZoomAnimationCurve)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .BindToLocalScaleXY(backRect)
                    .ToUniTask();
                    
                var posTask = LMotion.Create(nowPos, _gotoPos, _firstZoomSpeedTime)
                    .WithEase(_firstZoomAnimationCurve)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .BindToAnchoredPosition(backRect)
                    .ToUniTask();
                    
                tasks.Add(scaleTask);
                tasks.Add(posTask);
            }
        }
        
        // ZoomFrontContainer（敵）のズーム（同じパラメータ）
        if (zoomFrontContainer != null)
        {
            var frontRect = zoomFrontContainer as RectTransform;
            if (frontRect != null)
            {
                var nowScale = new Vector2(frontRect.localScale.x, frontRect.localScale.y);
                var nowPos = new Vector2(frontRect.anchoredPosition.x, frontRect.anchoredPosition.y);
                
                Debug.Log($"ZoomFrontContainer: 現在スケール={nowScale}, 現在位置={nowPos}");
                
                var scaleTask = LMotion.Create(nowScale, _gotoScaleXY, _firstZoomSpeedTime)
                    .WithEase(_firstZoomAnimationCurve)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .BindToLocalScaleXY(frontRect)
                    .ToUniTask();
                    
                var posTask = LMotion.Create(nowPos, _gotoPos, _firstZoomSpeedTime)
                    .WithEase(_firstZoomAnimationCurve)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .BindToAnchoredPosition(frontRect)
                    .ToUniTask();
                    
                tasks.Add(scaleTask);
                tasks.Add(posTask);
            }
        }
        
        Debug.Log($"MiddleFixedLayer, FixedUILayer, EnemySpawnAreaはズーム対象外です。");
        
        if (tasks.Count > 0)
        {
            await UniTask.WhenAll(tasks);
            Debug.Log("複数コンテナズーム完了");
        }
        else
        {
            Debug.LogWarning("ズーム対象コンテナが設定されていません。zoomBackContainer/zoomFrontContainerを設定してください。");
        }
        _isZoomAnimating = false;
    }
    
    /// <summary>
    /// RectTransformのワールド座標を取得
    /// </summary>
    

    /// <summary>
    /// ズーム前の初期状態を保存
    /// </summary>
    private void SaveOriginalTransforms()
    {
        if (zoomBackContainer != null)
        {
            var backRect = zoomBackContainer as RectTransform;
            if (backRect != null)
            {
                _originalBackPos = backRect.anchoredPosition;
                _originalBackScale = backRect.localScale;
                Debug.Log($"ZoomBackContainer初期状態保存: pos={_originalBackPos}, scale={_originalBackScale}");
            }
        }
        
        if (zoomFrontContainer != null)
        {
            var frontRect = zoomFrontContainer as RectTransform;
            if (frontRect != null)
            {
                _originalFrontPos = frontRect.anchoredPosition;
                _originalFrontScale = frontRect.localScale;
                Debug.Log($"ZoomFrontContainer初期状態保存: pos={_originalFrontPos}, scale={_originalFrontScale}");
            }
        }
    }
    
    /// <summary>
    /// ズーム前の状態に戻す 引数で速度指定
    /// </summary>
    public async UniTask RestoreOriginalTransforms(float duration = 1.0f)
    {
        var tasks = new List<UniTask>();
        
        Debug.Log("ズーム状態を初期状態に戻します");

        // インスペクタで復元アニメ無効の場合は即時復元
        if (!enableRestoreAnimation)
        {
            var backRect = zoomBackContainer as RectTransform;
            if (backRect != null)
            {
                backRect.anchoredPosition = _originalBackPos;
                backRect.localScale = _originalBackScale;
            }
            var frontRect = zoomFrontContainer as RectTransform;
            if (frontRect != null)
            {
                frontRect.anchoredPosition = _originalFrontPos;
                frontRect.localScale = _originalFrontScale;
            }
            Debug.Log("[WatchUIUpdate] enableRestoreAnimation=false のため即時復元しました。");
            return;
        }
        
        if (zoomBackContainer != null)
        {
            var backRect = zoomBackContainer as RectTransform;
            if (backRect != null)
            {
                var currentPos = backRect.anchoredPosition;
                var currentScale = backRect.localScale;
                
                var posTask = LMotion.Create(currentPos, _originalBackPos, duration)
                    .WithEase(Ease.OutQuart)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .BindToAnchoredPosition(backRect)
                    .ToUniTask();
                    
                var scaleTask = LMotion.Create((Vector3)currentScale, (Vector3)_originalBackScale, duration)
                    .WithEase(Ease.OutQuart)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .BindToLocalScale(backRect)
                    .ToUniTask();
                    
                tasks.Add(posTask);
                tasks.Add(scaleTask);
            }
        }
        
        if (zoomFrontContainer != null)
        {
            var frontRect = zoomFrontContainer as RectTransform;
            if (frontRect != null)
            {
                var currentPos = frontRect.anchoredPosition;
                var currentScale = frontRect.localScale;
                
                var posTask = LMotion.Create(currentPos, _originalFrontPos, duration)
                    .WithEase(Ease.OutQuart)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .BindToAnchoredPosition(frontRect)
                    .ToUniTask();
                    
                var scaleTask = LMotion.Create((Vector3)currentScale, (Vector3)_originalFrontScale, duration)
                    .WithEase(Ease.OutQuart)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .BindToLocalScale(frontRect)
                    .ToUniTask();
                    
                tasks.Add(posTask);
                tasks.Add(scaleTask);
            }
        }
        
        if (tasks.Count > 0)
        {
            await UniTask.WhenAll(tasks);
            Debug.Log("ズーム状態の復元完了");
        }
        else
        {
            Debug.LogWarning("復元対象のコンテナが見つかりません");
        }
    }

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
    /// 味方アイコンを下からスライドイン
    /// </summary>
    private async UniTask SlideInAllyIcons()
    {
        _isAllySlideAnimating = true;
        Debug.Log($"SlideInAllyIcons: allyBattleLayer={allyBattleLayer?.name}, allySpawnPositions={allySpawnPositions?.Length}");
        
        if (allyBattleLayer == null)
        {
            Debug.LogWarning("allyBattleLayerがnullです。設定してください。");
            return;
        }
        
        if (allySpawnPositions == null)
        {
            Debug.LogWarning("allySpawnPositionsがnullです。設定してください。");
            return;
        }
        //味方アイコンレイヤーを表示する
        allyBattleLayer.gameObject.SetActive(true);
        
        var tasks = new List<UniTask>();
        
        for (int i = 0; i < allySpawnPositions.Length; i++)
        {
            var allyIcon = allySpawnPositions[i];
            if (allyIcon != null)
            {
                var rect = allyIcon as RectTransform;
                if (rect != null)
                {
                    // 開始位置を下にオフセット
                    var startPos = rect.anchoredPosition + allySlideStartOffset;
                    var endPos = rect.anchoredPosition;
                    
                    rect.anchoredPosition = startPos;
                    
                    // スライドインアニメーション（少しずつ遅延）
                    var delay = i * 0.1f;
                    var slideTask = UniTask.Delay(TimeSpan.FromSeconds(delay))
                        .ContinueWith(() => 
                            LMotion.Create(startPos, endPos, 0.5f)
                                .WithEase(Ease.OutBack)
                                .BindToAnchoredPosition(rect)
                                .ToUniTask()
                        );
                    
                    tasks.Add(slideTask);
                }
            }
        }
        
        await UniTask.WhenAll(tasks);
        _isAllySlideAnimating = false;
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
                foreach (var ui in results)
                {
                    if (ui != null) ui.gameObject.SetActive(true);
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
            var uiInstance = Instantiate(enemyUIPrefab, enemyBattleLayer, false);
            // 設定中は非アクティブにしてCanvas再構築を抑制
            uiInstance.gameObject.SetActive(false);
            if (enableVerboseEnemyLogs)
            {
                Debug.Log($"[Instantiated] {uiInstance.name} activeSelf={uiInstance.gameObject.activeSelf}, inHierarchy={uiInstance.gameObject.activeInHierarchy}", uiInstance);
            }
            var rectTransform = (RectTransform)uiInstance.transform;

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