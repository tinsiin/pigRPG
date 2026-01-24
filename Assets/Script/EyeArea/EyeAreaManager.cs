using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// EyeArea（バトルUI/歩行UI）の統合管理クラス。
/// Phase 6: 分離したコントローラーへの統一アクセスを提供するファサード。
///
/// 各コントローラーはWatchUIUpdate.Instanceから初期化され、
/// このクラスを通じて一元的にアクセス可能。
/// </summary>
public sealed class EyeAreaManager
{
    private static EyeAreaManager _instance;

    /// <summary>
    /// シングルトンインスタンス。WatchUIUpdate.Instance経由で自動初期化。
    /// </summary>
    public static EyeAreaManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var wui = WatchUIUpdate.Instance;
                if (wui != null)
                {
                    _instance = new EyeAreaManager(wui);
                }
            }
            return _instance;
        }
    }

    private readonly WatchUIUpdate _source;

    private EyeAreaManager(WatchUIUpdate source)
    {
        _source = source;
    }

    /// <summary>
    /// インスタンスをリセット（シーン切替時など）
    /// </summary>
    public static void Reset()
    {
        _instance = null;
    }

    // ========================================================================
    // コントローラーアクセス
    // ========================================================================

    /// <summary>
    /// ビューポートコントローラー（ズーム、レイヤー管理）
    /// </summary>
    public IViewportController Viewport => _source?.Viewport;

    /// <summary>
    /// ActionMarkコントローラー（行動順マーカー）
    /// </summary>
    public IActionMarkController ActionMark => _source?.ActionMarkCtrl;

    /// <summary>
    /// KZoomコントローラー（アイコン詳細表示）
    /// </summary>
    public IKZoomController KZoom => _source?.KZoomCtrl;

    /// <summary>
    /// 敵配置コントローラー
    /// </summary>
    public IEnemyPlacementController EnemyPlacement => _source?.EnemyPlacementCtrl;

    /// <summary>
    /// Intro Orchestrator ファサード（ズーム/配置/復帰）
    /// </summary>
    public IIntroOrchestratorFacade IntroOrchestrator => _source?.IntroOrchestrator;

    /// <summary>
    /// 歩行UIコントローラー
    /// </summary>
    public IWalkingUIController WalkingUI => _source?.WalkingUICtrl;

    // ========================================================================
    // 便利プロパティ
    // ========================================================================

    /// <summary>
    /// KZoomが現在アクティブか
    /// </summary>
    public bool IsKZoomActive => KZoom?.IsKActive ?? false;

    /// <summary>
    /// KZoomがアニメーション中か
    /// </summary>
    public bool IsKZoomAnimating => KZoom?.IsKAnimating ?? false;

    /// <summary>
    /// KZoomに入れる状態か
    /// </summary>
    public bool CanEnterKZoom => KZoom?.CanEnterK ?? false;

    /// <summary>
    /// 敵配置エリア
    /// </summary>
    public RectTransform EnemySpawnArea => EnemyPlacement?.SpawnArea;

    /// <summary>
    /// サイドオブジェクトルート
    /// </summary>
    public RectTransform SideObjectRoot => WalkingUI?.SideObjectRoot;

    // ========================================================================
    // ズーム演出ラッパー（共通入口）
    // ========================================================================
    //
    // これらのメソッドは IntroOrchestrator Facade への簡易ラッパー。
    // バトル/ノベル/歩行など、どこからでも同じ入口でズーム演出を呼べる。
    //
    // 注意: KZoom（アイコンタップ詳細）は別系統。KZoomCtrl を使用すること。
    // ========================================================================

    /// <summary>
    /// ズーム演出の準備（原状キャプチャ）。
    /// PlayZoomAsync の前に呼ぶ必要がある。
    /// </summary>
    public UniTask PrepareZoomAsync(CancellationToken ct = default)
        => IntroOrchestrator?.PrepareAsync(ct) ?? UniTask.CompletedTask;

    /// <summary>
    /// ズームイン演出を再生。
    /// 事前に PrepareZoomAsync を呼んでおくこと。
    /// </summary>
    public UniTask PlayZoomAsync(CancellationToken ct = default)
        => IntroOrchestrator?.PlayAsync(ct) ?? UniTask.CompletedTask;

    /// <summary>
    /// ズームアウト（原状復帰）演出を再生。
    /// </summary>
    /// <param name="animated">アニメーション付きで復帰するか</param>
    /// <param name="duration">アニメーション時間（秒）</param>
    public UniTask RestoreZoomAsync(bool animated = false, float duration = 0f, CancellationToken ct = default)
        => IntroOrchestrator?.RestoreAsync(animated, duration, ct) ?? UniTask.CompletedTask;
}
