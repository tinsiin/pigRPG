using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/Set Flag")]
public sealed class SetFlagEffect : EffectSO
{
    [SerializeField] private string key;
    [SerializeField] private bool value = true;

    public override UniTask Apply(GameContext context)
    {
        context?.SetFlag(key, value);
        return UniTask.CompletedTask;
    }
}