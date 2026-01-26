using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Effectだけを実行するEventStep。
/// ステップ間での分岐や即座のEffect適用に使用。
/// </summary>
[Serializable]
public sealed class EffectStep : IEventStep
{
    [Header("適用するEffect")]
    [SerializeField] private EffectSO[] effects;

    public EffectSO[] Effects => effects;

    public EffectStep() { }

    public EffectStep(EffectSO[] effects)
    {
        this.effects = effects;
    }

    public UniTask<EffectSO[]> ExecuteAsync(EventContext context)
    {
        // Effectを返す（EventRunnerが適用する）
        return UniTask.FromResult(effects ?? Array.Empty<EffectSO>());
    }
}
