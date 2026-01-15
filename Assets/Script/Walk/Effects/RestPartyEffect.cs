using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Effects/Rest Party")]
public sealed class RestPartyEffect : EffectSO
{
    [SerializeField] private bool restoreHp = true;
    [SerializeField] private bool restoreMental = true;
    [SerializeField] private bool restoreP = true;

    public override UniTask Apply(GameContext context)
    {
        var roster = context?.Players?.Roster;
        if (roster == null) return UniTask.CompletedTask;

        var count = roster.AllyCount;
        for (var i = 0; i < count; i++)
        {
            var ally = roster.GetAllyById((AllyId)i);
            if (ally == null) continue;

            if (restoreHp)
            {
                ally.HP = ally.MaxHP;
            }
            if (restoreMental)
            {
                ally.MentalHP = ally.MentalMaxHP;
            }
            if (restoreP)
            {
                ally.P = ally.MAXP;
            }
        }

        return UniTask.CompletedTask;
    }
}
