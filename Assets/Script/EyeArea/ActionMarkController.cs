using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ActionMark（行動対象マーカー）を制御するコントローラー。
/// WatchUIUpdateから分離されたPhase 3コンポーネント。
/// </summary>
public sealed class ActionMarkController : IActionMarkController
{
    private readonly ActionMarkUI _actionMark;
    private readonly RectTransform _spawnPoint;
    private readonly Func<UniTask> _waitAnimations;

    /// <summary>
    /// ActionMarkControllerを構築する。
    /// </summary>
    /// <param name="actionMark">ActionMarkUIへの参照</param>
    /// <param name="spawnPoint">スポーン位置（ShowFromSpawn用）</param>
    /// <param name="waitAnimations">アニメーション待機用のデリゲート（MoveToActorScaled用）</param>
    public ActionMarkController(
        ActionMarkUI actionMark,
        RectTransform spawnPoint,
        Func<UniTask> waitAnimations = null)
    {
        _actionMark = actionMark;
        _spawnPoint = spawnPoint;
        _waitAnimations = waitAnimations;
    }

    /// <summary>ActionMarkUIへの直接アクセス（高度な操作用）</summary>
    public ActionMarkUI ActionMark => _actionMark;

    /// <summary>マーカーが現在表示中かどうか</summary>
    public bool IsVisible => _actionMark != null && _actionMark.gameObject.activeSelf;

    public void MoveToIcon(RectTransform targetIcon, bool immediate = false)
    {
        if (_actionMark == null)
        {
            Debug.LogWarning("[ActionMarkController] ActionMarkUI が未設定です。");
            return;
        }
        if (targetIcon == null)
        {
            Debug.LogWarning("[ActionMarkController] MoveToIcon: targetIcon が null です。");
            return;
        }
        _actionMark.MoveToTarget(targetIcon, immediate);
    }

    public void MoveToIconScaled(RectTransform targetIcon, bool immediate = false)
    {
        if (_actionMark == null || targetIcon == null)
        {
            Debug.LogWarning("[ActionMarkController] MoveToIconScaled: 必要参照が不足しています。");
            return;
        }
        var extraScale = ComputeScaleRatioForTarget(targetIcon);
        _actionMark.MoveToTargetWithScale(targetIcon, extraScale, immediate);
    }

    public void MoveToActor(BaseStates actor, bool immediate = false)
    {
        if (actor == null)
        {
            Debug.LogWarning("[ActionMarkController] MoveToActor: actor が null です。");
            return;
        }

        var ui = actor.UI;
        if (ui == null)
        {
            Debug.LogWarning($"[ActionMarkController] MoveToActor: actor.UI が null です。actor={actor.GetType().Name}");
            return;
        }

        var img = ui.Icon;
        if (img == null)
        {
            Debug.LogWarning($"[ActionMarkController] MoveToActor: UI.Icon が null です。actor={actor.GetType().Name}");
            return;
        }

        var iconRT = img.transform as RectTransform;
        MoveToIcon(iconRT, immediate);
    }

    public async UniTask MoveToActorScaled(BaseStates actor, bool immediate = false, bool waitAnimations = true)
    {
        if (actor == null)
        {
            Debug.LogWarning("[ActionMarkController] MoveToActorScaled: actor が null です。");
            return;
        }
        var ui = actor.UI;
        if (ui?.Icon == null)
        {
            Debug.LogWarning($"[ActionMarkController] MoveToActorScaled: UI.Icon が null です。actor={actor.GetType().Name}");
            return;
        }

        if (waitAnimations && _waitAnimations != null)
        {
            await _waitAnimations();
        }

        var iconRT = ui.Icon.transform as RectTransform;
        MoveToIconScaled(iconRT, immediate);
    }

    public void Show()
    {
        if (_actionMark == null)
        {
            Debug.LogWarning("[ActionMarkController] Show: ActionMarkUI が未設定です。");
            return;
        }
        _actionMark.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (_actionMark == null)
        {
            Debug.LogWarning("[ActionMarkController] Hide: ActionMarkUI が未設定です。");
            return;
        }
        _actionMark.gameObject.SetActive(false);
    }

    public void ShowFromSpawn(bool zeroSize = true)
    {
        if (_actionMark == null)
        {
            Debug.LogWarning("[ActionMarkController] ShowFromSpawn: ActionMarkUI が未設定です。");
            return;
        }
        if (_spawnPoint == null)
        {
            Debug.LogWarning("[ActionMarkController] ShowFromSpawn: spawnPoint が未設定です。通常の Show() を使用します。");
            Show();
            return;
        }

        var markRT = _actionMark.rectTransform;
        // 中央基準に設定
        markRT.pivot = new Vector2(0.5f, 0.5f);
        markRT.anchorMin = new Vector2(0.5f, 0.5f);
        markRT.anchorMax = new Vector2(0.5f, 0.5f);

        // スポーン位置(中心)のワールド座標 → ActionMark親のローカル(anchoredPosition)へ
        Vector2 worldCenter = _spawnPoint.TransformPoint(_spawnPoint.rect.center);
        Vector2 anchored = WorldToAnchoredPosition(markRT, worldCenter);

        _actionMark.gameObject.SetActive(true);
        markRT.anchoredPosition = anchored;
        if (zeroSize)
        {
            _actionMark.SetSize(0f, 0f);
        }
    }

    /// <summary>
    /// ステージテーマカラーを設定する
    /// </summary>
    public void SetStageThemeColor(Color color)
    {
        if (_actionMark != null)
        {
            _actionMark.SetStageThemeColor(color);
        }
    }

    #region ヘルパーメソッド

    /// <summary>
    /// target(アイコン)の見かけスケールと、ActionMark親の見かけスケールの比率を返す
    /// </summary>
    private Vector2 ComputeScaleRatioForTarget(RectTransform target)
    {
        var parentRT = _actionMark?.rectTransform?.parent as RectTransform;
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
    /// ワールド座標をRectTransformのanchoredPosition座標系へ変換
    /// Canvas/Cameraを考慮して正確に変換する
    /// </summary>
    private static Vector2 WorldToAnchoredPosition(RectTransform rectTransform, Vector2 worldPos)
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

    #endregion
}
