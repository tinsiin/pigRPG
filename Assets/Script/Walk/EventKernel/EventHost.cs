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

    public void SetContext(GameContext nextContext)
    {
        context = nextContext;
    }

    public void SetUI(IEventUI nextUi)
    {
        ui = nextUi;
    }

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
}