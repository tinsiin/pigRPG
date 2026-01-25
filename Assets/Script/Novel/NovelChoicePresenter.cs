using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ノベルパートの選択肢ボタンを動的生成するプレゼンター。
/// 縦並びでボタンを配置する。
/// </summary>
public sealed class NovelChoicePresenter : DynamicButtonPresenterBase
{
    private Action<int> onChoiceSelected;
    private NovelInputHub inputHub;

    /// <summary>
    /// 入力ハブを設定する。
    /// </summary>
    public void SetInputHub(NovelInputHub hub)
    {
        inputHub = hub;
    }

    /// <summary>
    /// 選択肢ボタンを生成する。
    /// </summary>
    /// <param name="labels">選択肢のラベル配列</param>
    /// <param name="callback">選択時コールバック（インデックスを渡す）</param>
    public void ShowChoices(string[] labels, Action<int> callback = null)
    {
        ClearAllButtons();
        onChoiceSelected = callback;

        if (labels == null || labels.Length == 0) return;

        ResetVerticalLayout(out var currentY);

        for (var i = 0; i < labels.Length; i++)
        {
            var button = CreateButtonVertical(labels[i], ref currentY);

            // クリックイベント設定
            var capturedIndex = i;
            button.onClick.AddListener(() => OnButtonClicked(capturedIndex));
        }
    }

    /// <summary>
    /// 選択肢ボタンを生成する（DialogueChoice配列から）。
    /// </summary>
    public void ShowChoices(DialogueChoice[] choices, Action<int> callback = null)
    {
        if (choices == null || choices.Length == 0)
        {
            ClearAllButtons();
            return;
        }

        var labels = new string[choices.Length];
        for (var i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            var label = choice?.Text ?? $"Choice {i}";

            // 精神属性があれば表示
            if (!string.IsNullOrEmpty(choice?.SpiritProperty))
            {
                label += $" [{choice.SpiritProperty}]";
            }

            labels[i] = label;
        }

        ShowChoices(labels, callback);
    }

    public override void ClearAllButtons()
    {
        base.ClearAllButtons();
        onChoiceSelected = null;
    }

    private void OnButtonClicked(int index)
    {
        // コールバックを呼ぶ
        onChoiceSelected?.Invoke(index);

        // 入力ハブに通知
        inputHub?.NotifyChoice(index);

        // ボタンをクリア
        ClearAllButtons();
    }
}
