using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IEventUI
{
    void ShowMessage(string message);
    UniTask<int> ShowChoices(string[] labels, string[] ids);
}

/// <summary>
/// EventDefinitionSOを実行するランナー。
/// IEventRunnerインターフェースを実装。
/// </summary>
public sealed class EventRunner : IEventRunner
{
    /// <summary>
    /// EventDefinitionSOを実行し、収集したEffectを返す。
    /// 新しいIEventStep対応API。
    /// </summary>
    public async UniTask<EffectSO[]> RunAsync(EventDefinitionSO definition, EventContext context)
    {
        if (definition == null)
        {
            Debug.LogWarning("EventRunner.RunAsync: definition is null.");
            return Array.Empty<EffectSO>();
        }

        var steps = definition.Steps;
        if (steps == null || steps.Length == 0)
        {
            // TerminalEffectsのみ返す
            return definition.TerminalEffects ?? Array.Empty<EffectSO>();
        }

        var collectedEffects = new List<EffectSO>();

        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            if (step == null) continue;

            var effects = await step.ExecuteAsync(context);
            if (effects != null && effects.Length > 0)
            {
                collectedEffects.AddRange(effects);
            }
        }

        // TerminalEffectsを追加
        if (definition.TerminalEffects != null && definition.TerminalEffects.Length > 0)
        {
            collectedEffects.AddRange(definition.TerminalEffects);
        }

        return collectedEffects.ToArray();
    }

    /// <summary>
    /// EventDefinitionSOを実行し、収集したEffectを即座に適用する。
    /// 旧API互換（GameContext + IEventUI）。
    /// </summary>
    public async UniTask Run(EventDefinitionSO definition, GameContext gameContext, IEventUI ui)
    {
        if (definition == null)
        {
            Debug.LogWarning("EventRunner.Run: definition is null.");
            return;
        }

        // EventContextを構築
        var context = new EventContext
        {
            GameContext = gameContext,
            EventUI = ui,
            NovelUI = ui as INovelEventUI,
            EventRunner = this,
            DialogueRunner = gameContext?.DialogueRunner,
            BattleRunner = gameContext?.BattleRunner
        };

        // 実行してEffectを収集
        var effects = await RunAsync(definition, context);

        // Effectを適用
        await ApplyEffects(effects, gameContext);
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