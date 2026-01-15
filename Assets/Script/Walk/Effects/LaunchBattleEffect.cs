using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/LaunchBattle")]
public sealed class LaunchBattleEffect : EffectSO
{
    [SerializeField] private EncounterSO encounter;

    public override async UniTask Apply(GameContext context)
    {
        if (context == null || encounter == null) return;
        var runner = context.BattleRunner;
        if (runner == null) return;

        await runner.RunBattleAsync(new EncounterContext(encounter, context));
    }
}
