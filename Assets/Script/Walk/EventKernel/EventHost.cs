using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class EventHost
{
    private readonly EventRunner runner;
    private GameContext context;
    private IEventUI ui;

    public EventHost(EventRunner runner, GameContext context, IEventUI ui)
    {
        this.runner = runner;
        this.context = context;
        this.ui = ui;
    }

    /// <summary>
    /// EventRunnerを公開（外部からRunAsyncを呼び出す用）。
    /// </summary>
    public IEventRunner Runner => runner;

    public void SetContext(GameContext nextContext)
    {
        context = nextContext;
    }

    public void SetUI(IEventUI nextUi)
    {
        ui = nextUi;
    }

    /// <summary>
    /// 旧API互換。EventDefinitionSOを実行し、Effectを即座に適用する。
    /// </summary>
    public UniTask Trigger(EventDefinitionSO definition)
    {
        if (runner == null)
        {
            Debug.LogWarning("EventHost.Trigger: runner is null.");
            return UniTask.CompletedTask;
        }
        if (context == null)
        {
            Debug.LogWarning("EventHost.Trigger: context is null.");
            return UniTask.CompletedTask;
        }
        if (ui == null)
        {
            Debug.LogWarning("EventHost.Trigger: ui is null.");
            return UniTask.CompletedTask;
        }

        return runner.Run(definition, context, ui);
    }

    /// <summary>
    /// 新API。EventContextを指定してEventDefinitionSOを実行し、Effectを返す。
    /// </summary>
    public async UniTask<EffectSO[]> TriggerWithContext(EventDefinitionSO definition, EventContext eventContext)
    {
        if (runner == null)
        {
            Debug.LogWarning("EventHost.TriggerWithContext: runner is null.");
            return Array.Empty<EffectSO>();
        }
        if (eventContext == null)
        {
            Debug.LogWarning("EventHost.TriggerWithContext: eventContext is null.");
            return Array.Empty<EffectSO>();
        }

        return await runner.RunAsync(definition, eventContext);
    }

    /// <summary>
    /// 基本的なEventContextを生成する。
    /// CentralObjectRT等はオプションで設定可能。
    /// </summary>
    public EventContext CreateEventContext(RectTransform centralObjectRT = null)
    {
        return new EventContext
        {
            GameContext = context,
            EventUI = ui,
            NovelUI = ui as INovelEventUI,
            DialogueRunner = context?.DialogueRunner,
            BattleRunner = context?.BattleRunner,
            EventRunner = runner,
            CentralObjectRT = centralObjectRT
        };
    }
}