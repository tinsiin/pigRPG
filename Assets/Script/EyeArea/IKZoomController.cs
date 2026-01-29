using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// KZoom（アイコンタップ詳細表示）の制御インターフェース。
/// バトル中にキャラアイコンをタップした時のズーム表示を管理する。
/// </summary>
public interface IKZoomController
{
    /// <summary>KZoomに入れる状態か（アニメーション中でない等）</summary>
    bool CanEnterK { get; }

    /// <summary>KZoomがアクティブか</summary>
    bool IsKActive { get; }

    /// <summary>KZoomアニメーション中か</summary>
    bool IsKAnimating { get; }

    /// <summary>指定UIが現在のKZoomターゲットか</summary>
    bool IsCurrentKTarget(BattleIconUI ui);

    /// <summary>KZoomに入る（指定アイコンにズーム）</summary>
    UniTask EnterK(RectTransform iconRT, string title);

    /// <summary>KZoomから抜ける（アニメーションあり）</summary>
    UniTask ExitK();

    /// <summary>KZoomを即座に解除</summary>
    void ForceExitKImmediate();
}
