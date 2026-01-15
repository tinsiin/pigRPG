using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/Apply Stage Bonus")]
public sealed class ApplyStageBonusEffect : EffectSO
{
    [SerializeField] private AllyId target;
    [SerializeField] private StageBonus bonus;
    [SerializeField] private bool additive = true;

    public override UniTask Apply(GameContext context)
    {
        if (context == null) return UniTask.CompletedTask;
        if (additive)
        {
            context.AddStageBonus(target, bonus);
        }
        else
        {
            context.SetStageBonus(target, bonus);
        }
        return UniTask.CompletedTask;
    }
}
