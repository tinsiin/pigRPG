using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IEventUI
{
    void ShowMessage(string message);
    UniTask<int> ShowChoices(string[] labels, string[] ids);
}

public sealed class EventRunner
{
    public async UniTask Run(EventDefinitionSO definition, GameContext context, IEventUI ui)
    {
        if (definition == null)
        {
            Debug.LogWarning("EventRunner.Run: definition is null.");
            return;
        }

        var steps = definition.Steps;
        if (steps == null || steps.Length == 0) return;

        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            if (step == null) continue;

            if (!string.IsNullOrEmpty(step.Message))
            {
                ui?.ShowMessage(step.Message);
            }

            var choices = step.Choices;
            if (choices != null && choices.Length > 0)
            {
                if (ui == null)
                {
                    Debug.LogWarning("EventRunner.Run: UI is null, cannot show choices.");
                    return;
                }

                var labels = new string[choices.Length];
                var ids = new string[choices.Length];
                for (var c = 0; c < choices.Length; c++)
                {
                    var choice = choices[c];
                    labels[c] = choice != null && !string.IsNullOrEmpty(choice.Label) ? choice.Label : $"Choice {c}";
                    ids[c] = c.ToString();
                }

                var selected = await ui.ShowChoices(labels, ids);
                if (selected >= 0 && selected < choices.Length)
                {
                    await ApplyEffects(choices[selected]?.Effects, context);
                }

                continue;
            }

            await ApplyEffects(step.Effects, context);
        }
    }

    private static async UniTask ApplyEffects(EffectSO[] effects, GameContext context)
    {
        if (effects == null || effects.Length == 0) return;
        for (var i = 0; i < effects.Length; i++)
        {
            var effect = effects[i];
            if (effect == null) continue;
            await effect.Apply(context);
        }
    }
}