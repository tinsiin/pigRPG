using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// メッセージ表示と選択肢を扱うEventStep。
/// 旧EventStepと互換性を持つ。
/// </summary>
[Serializable]
public sealed class MessageStep : IEventStep
{
    [TextArea(2, 6)]
    [SerializeField] private string message;
    [SerializeField] private EventChoice[] choices;
    [SerializeField] private EffectSO[] effects;

    public string Message => message;
    public EventChoice[] Choices => choices;
    public EffectSO[] Effects => effects;

    public MessageStep() { }

    public MessageStep(string message, EventChoice[] choices, EffectSO[] effects)
    {
        this.message = message;
        this.choices = choices;
        this.effects = effects;
    }

    public async UniTask<EffectSO[]> ExecuteAsync(EventContext context)
    {
        var ui = context.EventUI;

        // メッセージ表示
        if (!string.IsNullOrEmpty(message))
        {
            ui?.ShowMessage(message);
        }

        // 選択肢がある場合
        if (choices != null && choices.Length > 0)
        {
            if (ui == null)
            {
                Debug.LogWarning("MessageStep: UI is null, cannot show choices.");
                return Array.Empty<EffectSO>();
            }

            var labels = new string[choices.Length];
            var ids = new string[choices.Length];
            for (var i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                labels[i] = choice != null && !string.IsNullOrEmpty(choice.Label) ? choice.Label : $"Choice {i}";
                ids[i] = i.ToString();
            }

            var selected = await ui.ShowChoices(labels, ids);
            if (selected >= 0 && selected < choices.Length)
            {
                // 選択肢のEffectを返す
                return choices[selected]?.Effects ?? Array.Empty<EffectSO>();
            }

            return Array.Empty<EffectSO>();
        }

        // 選択肢がない場合はステップのEffectを返す
        return effects ?? Array.Empty<EffectSO>();
    }
}
