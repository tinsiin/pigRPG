using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 歩行システム関連のUI制御を担当するコントローラー。
/// Phase 4: WatchUIUpdateから歩行UI機能を分離。
/// </summary>
public sealed class WalkingUIController : IWalkingUIController
{
    private readonly TextMeshProUGUI _stagesText;
    private readonly RectTransform _sideObjectRoot;
    private readonly IActionMarkController _actionMark;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="stagesText">ステージ名表示用テキスト</param>
    /// <param name="sideObjectRoot">サイドオブジェクトの配置ルート</param>
    /// <param name="actionMark">ActionMarkコントローラー（テーマカラー設定用）</param>
    public WalkingUIController(
        TextMeshProUGUI stagesText,
        RectTransform sideObjectRoot,
        IActionMarkController actionMark)
    {
        _stagesText = stagesText;
        _sideObjectRoot = sideObjectRoot;
        _actionMark = actionMark;
    }

    /// <inheritdoc/>
    public RectTransform SideObjectRoot => _sideObjectRoot;

    /// <inheritdoc/>
    public void ApplyNodeUI(string displayName, NodeUIHints hints)
    {
        if (_stagesText != null && !string.IsNullOrEmpty(displayName))
        {
            _stagesText.text = displayName;
        }

        if (hints.UseActionMarkColor && _actionMark != null)
        {
            _actionMark.SetStageThemeColor(hints.ActionMarkColor);
        }
    }

    /// <inheritdoc/>
    public void SetStageText(string text)
    {
        if (_stagesText != null)
        {
            _stagesText.text = text;
        }
    }
}
