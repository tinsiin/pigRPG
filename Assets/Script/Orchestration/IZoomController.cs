using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ズーム制御の抽象。既存の zoomFrontContainer 制御を外出しし、実装差し替えを容易にします。
/// </summary>
public interface IZoomController
{
    /// <summary>
    /// 指定時間でズームイン（拡大）します。具体的なターゲット倍率は実装側で定義して構いません。
    /// </summary>
    UniTask ZoomInAsync(float duration, CancellationToken ct);

    /// <summary>
    /// 指定時間でズームアウト（縮小）します。
    /// </summary>
    UniTask ZoomOutAsync(float duration, CancellationToken ct);

    /// <summary>
    /// 任意の倍率に指定時間で遷移します。
    /// </summary>
    UniTask SetZoomAsync(float target, float duration, CancellationToken ct);

    /// <summary>
    /// 即時既定状態へ戻します（フェイルセーフ用）。
    /// </summary>
    void Reset();

    /// <summary>
    /// ズーム前の原状（front/back の pos/scale）をキャプチャします。
    /// </summary>
    void CaptureOriginal(RectTransform front, RectTransform back);

    /// <summary>
    /// 原状へ即時復元します。
    /// </summary>
    void RestoreImmediate();

    /// <summary>
    /// 原状へアニメ付きで復元します。
    /// </summary>
    UniTask RestoreAsync(float duration, AnimationCurve curve, CancellationToken ct);
}
