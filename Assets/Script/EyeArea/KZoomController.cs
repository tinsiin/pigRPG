using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using TMPro;
using UnityEngine;

/// <summary>
/// KZoom（アイコンタップ詳細表示）を制御するコントローラー。
/// WatchUIUpdateから分離されたPhase 3bコンポーネント。
/// </summary>
public sealed class KZoomController : IKZoomController
{
    private readonly KZoomConfig _config;
    private readonly KZoomState _state;
    private readonly IActionMarkController _actionMark;

    // 外部依存のデリゲート
    private readonly Func<IEnumerable<BaseStates>> _getAllCharacters;
    private readonly Func<bool> _isZoomAnimating;
    private readonly Func<bool> _isAllySlideAnimating;
    private readonly Action<bool> _setSchizoVisible;
    private readonly Func<bool> _getSchizoVisible;

    // ヘルパーメソッドのデリゲート（WatchUIUpdateから注入）
    private readonly Func<TMPTextBackgroundImage, TMP_Text, bool, TMP_Text> _getOrSetupTMP;
    private readonly Func<string, TMPTextBackgroundImage, int, float, bool, string> _fitTextIntoRect;


    public KZoomController(
        KZoomConfig config,
        KZoomState state,
        IActionMarkController actionMark,
        Func<IEnumerable<BaseStates>> getAllCharacters,
        Func<bool> isZoomAnimating,
        Func<bool> isAllySlideAnimating,
        Action<bool> setSchizoVisible,
        Func<bool> getSchizoVisible,
        Func<TMPTextBackgroundImage, TMP_Text, bool, TMP_Text> getOrSetupTMP = null,
        Func<string, TMPTextBackgroundImage, int, float, bool, string> fitTextIntoRect = null)
    {
        _config = config;
        _state = state;
        _actionMark = actionMark;
        _getAllCharacters = getAllCharacters;
        _isZoomAnimating = isZoomAnimating;
        _isAllySlideAnimating = isAllySlideAnimating;
        _setSchizoVisible = setSchizoVisible;
        _getSchizoVisible = getSchizoVisible;
        _getOrSetupTMP = getOrSetupTMP;
        _fitTextIntoRect = fitTextIntoRect;
    }

    #region IKZoomController実装

    public bool CanEnterK => !_state.IsActive && !_state.IsAnimating
        && !(_config.DisableIconClickWhileBattleZoom && (_isZoomAnimating?.Invoke() == true || _isAllySlideAnimating?.Invoke() == true));

    public bool IsKActive => _state.IsActive;

    public bool IsKAnimating => _state.IsAnimating;

    public bool IsCurrentKTarget(BattleIconUI ui) => _state.IsActive && (_state.ExclusiveUI == ui);

    public async UniTask EnterK(RectTransform iconRT, string title)
    {
        if (!CanEnterK)
        {
            Debug.Log("[KZoomController] CanEnterK=false のためEnterKを無視");
            return;
        }
        if (iconRT == null || _config.ZoomRoot == null || _config.TargetRect == null)
        {
            Debug.LogWarning("[KZoomController] 必要参照が不足しています(iconRT/ZoomRoot/TargetRect)。");
            return;
        }

        // テキスト設定（まずは非表示）
        if (_config.NameText != null)
        {
            _config.NameText.text = title ?? string.Empty;
            _config.NameText.gameObject.SetActive(false);
        }

        _state.Cts?.Cancel();
        _state.Cts?.Dispose();
        _state.Cts = new CancellationTokenSource();

        var ct = _state.Cts.Token;
        _state.IsAnimating = true;

        // クリック元UIの参照を保持
        _state.ExclusiveUI = iconRT.GetComponentInParent<BattleIconUI>();

        // 非対象キャラのBattleIconUIをK中は非表示にする
        _state.HiddenOtherUIs = new List<(BattleIconUI ui, bool wasActive)>();
        var allChars = _getAllCharacters?.Invoke();
        if (allChars != null)
        {
            foreach (var ch in allChars)
            {
                var ui = ch?.UI;
                if (ui == null || ui == _state.ExclusiveUI) continue;
                bool prev = ui.gameObject.activeSelf;
                _state.HiddenOtherUIs.Add((ui, prev));
                if (prev)
                {
                    ui.SetActive(false);
                }
            }
        }

        // ActionMarkの表示状態を退避し、K中は非表示にする
        if (_actionMark != null)
        {
            _state.ActionMarkWasActive = _actionMark.IsVisible;
            if (_state.ActionMarkWasActive)
            {
                _actionMark.Hide();
            }
        }

        // SchizoLogの表示状態を退避し、K中は非表示にする
        _state.SchizoWasVisible = _getSchizoVisible?.Invoke() ?? false;
        if (_state.SchizoWasVisible)
        {
            _setSchizoVisible?.Invoke(false);
        }

        // もとの状態を保存
        _state.OriginalPos = _config.ZoomRoot.anchoredPosition;
        _state.OriginalScale = _config.ZoomRoot.localScale;
        _state.SnapshotValid = true;

        // フィット計算
        ComputeKFit(iconRT, out float targetScale, out Vector2 targetAnchoredPos);

        // ズームイン（位置＋スケール）
        var rootRT = _config.ZoomRoot;
        var scaleTask = LMotion.Create((Vector3)_state.OriginalScale, new Vector3(targetScale, targetScale, _state.OriginalScale.z), _config.ZoomDuration)
            .WithEase(_config.ZoomEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToLocalScale(rootRT)
            .ToUniTask(ct);

        var posTask = LMotion.Create(_state.OriginalPos, targetAnchoredPos, _config.ZoomDuration)
            .WithEase(_config.ZoomEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPosition(rootRT)
            .ToUniTask(ct);

        // パッシブテキストの準備
        BaseStates actorForK = FindActorByUI(_state.ExclusiveUI);
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
            // 中断時の復帰処理
            RestoreOnCancel();
            return;
        }

        _state.IsActive = true;
        _state.IsAnimating = false;
    }

    public async UniTask ExitK()
    {
        if (!_state.IsActive && !_state.IsAnimating)
        {
            return;
        }

        // テキストは即時非表示
        if (_config.NameText != null) _config.NameText.gameObject.SetActive(false);
        if (_config.PassivesText != null) _config.PassivesText.gameObject.SetActive(false);

        _state.Cts?.Cancel();
        _state.Cts?.Dispose();
        _state.Cts = new CancellationTokenSource();
        var ct = _state.Cts.Token;

        _state.IsAnimating = true;

        // 即時にK中に非表示にしていたUIを復帰させる
        RestoreHiddenUIs();

        var rootRT = _config.ZoomRoot;
        if (rootRT == null)
        {
            _state.IsActive = false;
            _state.IsAnimating = false;
            return;
        }

        var scaleTask = LMotion.Create(rootRT.localScale, _state.OriginalScale, _config.ZoomDuration)
            .WithEase(_config.ZoomEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToLocalScale(rootRT)
            .ToUniTask(ct);

        var posTask = LMotion.Create(rootRT.anchoredPosition, _state.OriginalPos, _config.ZoomDuration)
            .WithEase(_config.ZoomEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPosition(rootRT)
            .ToUniTask(ct);

        try
        {
            await UniTask.WhenAll(scaleTask, posTask);
        }
        catch (OperationCanceledException)
        {
            // キャンセル時も状態を確定
        }

        _state.IsActive = false;
        _state.IsAnimating = false;
    }

    public void ForceExitKImmediate()
    {
        _state.Cts?.Cancel();
        _state.Cts?.Dispose();
        _state.Cts = null;

        // テキスト非表示
        if (_config.NameText != null) _config.NameText.gameObject.SetActive(false);
        if (_config.PassivesText != null) _config.PassivesText.gameObject.SetActive(false);

        // 位置/スケール即時復元
        if (_config.ZoomRoot != null && _state.SnapshotValid)
        {
            _config.ZoomRoot.anchoredPosition = _state.OriginalPos;
            _config.ZoomRoot.localScale = _state.OriginalScale;
        }

        _state.IsActive = false;
        _state.IsAnimating = false;
        _state.SnapshotValid = false;

        // UI復帰
        RestoreHiddenUIs();
    }

    #endregion

    #region ヘルパーメソッド

    private void RestoreOnCancel()
    {
        if (_state.ExclusiveUI != null)
        {
            _state.ExclusiveUI.SetExclusiveIconMode(false);
            _state.ExclusiveUI = null;
        }
        RestoreHiddenUIs();
        _state.IsAnimating = false;
        _state.SnapshotValid = false;

        if (_config.NameText != null) _config.NameText.gameObject.SetActive(false);
        if (_config.PassivesText != null) _config.PassivesText.gameObject.SetActive(false);

        if (_state.ActionMarkWasActive)
        {
            _actionMark?.Show();
        }
        _state.ActionMarkWasActive = false;

        if (_state.SchizoWasVisible)
        {
            _setSchizoVisible?.Invoke(true);
        }
        _state.SchizoWasVisible = false;
    }

    private void RestoreHiddenUIs()
    {
        if (_state.ExclusiveUI != null)
        {
            _state.ExclusiveUI.SetExclusiveIconMode(false);
            _state.ExclusiveUI = null;
        }

        if (_state.HiddenOtherUIs != null)
        {
            foreach (var pair in _state.HiddenOtherUIs)
            {
                if (pair.ui != null) pair.ui.SetActive(pair.wasActive);
            }
            _state.HiddenOtherUIs = null;
        }

        if (_state.ActionMarkWasActive)
        {
            _actionMark?.Show();
            _state.ActionMarkWasActive = false;
        }

        if (_state.SchizoWasVisible)
        {
            _setSchizoVisible?.Invoke(true);
            _state.SchizoWasVisible = false;
        }
    }

    private BaseStates FindActorByUI(BattleIconUI ui)
    {
        var all = _getAllCharacters?.Invoke();
        if (ui == null || all == null) return null;
        foreach (var ch in all)
        {
            if (ch != null && ch.UI == ui) return ch;
        }
        return null;
    }

    private void ComputeKFit(RectTransform iconRT, out float outScale, out Vector2 outAnchoredPos)
    {
        RectTransformUtil.GetWorldRect(iconRT, out Vector2 iconCenter, out Vector2 iconSize);
        RectTransformUtil.GetWorldRect(_config.TargetRect, out Vector2 targetCenter, out Vector2 targetSize);

        float sH = SafeDiv(targetSize.y, iconSize.y);
        float sW = SafeDiv(targetSize.x, iconSize.x);
        float s = Mathf.Lerp(sH, sW, _config.FitBlend);

        var parentPivotWorld = _config.ZoomRoot.TransformPoint(Vector3.zero);
        Vector2 moveWorld = targetCenter - ((iconCenter - (Vector2)parentPivotWorld) * s + (Vector2)parentPivotWorld);

        var parentRT = _config.ZoomRoot.parent as RectTransform;
        Vector2 moveLocal = parentRT != null ? (Vector2)parentRT.InverseTransformVector(moveWorld) : moveWorld;

        outScale = s;
        outAnchoredPos = _state.OriginalPos + moveLocal;
    }

    private static float SafeDiv(float a, float b)
    {
        return Mathf.Abs(b) < 1e-5f ? 1f : a / b;
    }


    private void SetKPassivesText(BaseStates actor)
    {
        if (_config.PassivesText == null) return;

        if (_getOrSetupTMP != null)
        {
            _state.PassivesTMP = _getOrSetupTMP(_config.PassivesText, _state.PassivesTMP, _config.PassivesUseRectMask);
        }

        var go = _config.PassivesText.gameObject;
        var cg0 = go.GetComponent<CanvasGroup>();
        if (cg0 == null) cg0 = go.AddComponent<CanvasGroup>();
        go.SetActive(true);
        cg0.alpha = 0f;
        Canvas.ForceUpdateCanvases();

        string tokens = _config.PassivesDebugMode
            ? BuildDummyKPassivesTokens(_config.PassivesDebugCount, _config.PassivesDebugPrefix)
            : BuildKPassivesTokens(actor);
        _state.PassivesTokensRaw = tokens ?? string.Empty;

        if (_fitTextIntoRect != null)
        {
            var fitted = _fitTextIntoRect(
                _state.PassivesTokensRaw,
                _config.PassivesText,
                Mathf.Max(1, _config.PassivesEllipsisDotCount),
                Mathf.Max(0f, _config.PassivesFitSafety),
                _config.PassivesAlwaysAppendEllipsis
            );
            _config.PassivesText.text = fitted;
        }
        else
        {
            _config.PassivesText.text = _state.PassivesTokensRaw;
        }

        _config.PassivesText.RefreshBackground();
    }

    private string BuildKPassivesTokens(BaseStates actor)
    {
        if (actor == null || actor.Passives == null || actor.Passives.Count == 0)
        {
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
            string token = $"<{raw}>";
            if (!first) sb.Append(' ');
            sb.Append(token);
            first = false;
        }
        return sb.ToString();
    }

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

    private async UniTask SlideInKTexts(string title, CancellationToken ct)
    {
        if (_config.NameText == null) return;

        _config.NameText.text = title ?? string.Empty;
        var rt = _config.NameText.rectTransform;
        var startPos = rt.anchoredPosition;
        var endPos = startPos;
        startPos.x += _config.TextSlideOffsetX;

        rt.anchoredPosition = startPos;
        _config.NameText.gameObject.SetActive(true);
        _config.NameText.RefreshBackground();

        await LMotion.Create(startPos, endPos, _config.TextSlideDuration)
            .WithEase(_config.TextSlideEase)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .BindToAnchoredPosition(rt)
            .ToUniTask(ct);
    }

    private async UniTask FadeInKPassives(BaseStates actor, CancellationToken ct)
    {
        if (_config.PassivesText == null) return;
        var go = _config.PassivesText.gameObject;
        if (string.IsNullOrEmpty(_config.PassivesText.text))
        {
            go.SetActive(false);
            return;
        }
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        go.SetActive(true);
        _config.PassivesText.RefreshBackground();

        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        Canvas.ForceUpdateCanvases();

        if (_fitTextIntoRect != null)
        {
            string baseTokens = string.IsNullOrEmpty(_state.PassivesTokensRaw)
                ? (_config.PassivesText.text ?? string.Empty)
                : _state.PassivesTokensRaw;
            var finalFitted = _fitTextIntoRect(
                baseTokens,
                _config.PassivesText,
                Mathf.Max(1, _config.PassivesEllipsisDotCount),
                Mathf.Max(0f, _config.PassivesFitSafety),
                _config.PassivesAlwaysAppendEllipsis
            );
            if (!string.Equals(finalFitted, _config.PassivesText.text, StringComparison.Ordinal))
            {
                _config.PassivesText.text = finalFitted;
                _config.PassivesText.RefreshBackground();
            }
        }

        await LMotion.Create(0f, 1f, _config.PassivesFadeDuration)
            .WithEase(Ease.Linear)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .Bind(a => cg.alpha = a)
            .ToUniTask(ct);
    }

    #endregion
}
