using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 別のEventDefinitionSOを呼び出すEventStep。
/// イベントの再利用や条件分岐後の共通処理に使用。
/// </summary>
[Serializable]
public sealed class EmitEventStep : IEventStep
{
    [Header("呼び出すイベント")]
    [SerializeField] private EventDefinitionSO eventRef;

    public EventDefinitionSO EventRef => eventRef;

    public EmitEventStep() { }

    public EmitEventStep(EventDefinitionSO eventRef)
    {
        this.eventRef = eventRef;
    }

    public async UniTask<EffectSO[]> ExecuteAsync(EventContext context)
    {
        if (eventRef == null)
        {
            return Array.Empty<EffectSO>();
        }

        if (context.EventRunner == null)
        {
            Debug.LogWarning("EmitEventStep: EventRunner is null.");
            return Array.Empty<EffectSO>();
        }

        // 別のEventDefinitionSOを実行
        return await context.EventRunner.RunAsync(eventRef, context);
    }
}
