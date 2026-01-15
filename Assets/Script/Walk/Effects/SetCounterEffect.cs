using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/Set Counter")]
public sealed class SetCounterEffect : EffectSO
{
    [SerializeField] private string key;
    [SerializeField] private int value;
    [SerializeField] private bool add;

    public override UniTask Apply(GameContext context)
    {
        if (context == null || string.IsNullOrEmpty(key)) return UniTask.CompletedTask;
        if (add)
        {
            var current = context.GetCounter(key);
            context.SetCounter(key, current + value);
        }
        else
        {
            context.SetCounter(key, value);
        }

        return UniTask.CompletedTask;
    }
}