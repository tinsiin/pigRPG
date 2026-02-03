using System.Linq;
using Cysharp.Threading.Tasks;
using RandomExtensions;
using UnityEngine;
using static CommonCalc;

/// <summary>
/// リベンジボーナス効果。
/// ダメージを受けたキャラクターの仲間が攻撃者に対してボーナスを得る。
/// </summary>
public sealed class RevengeBonusEffect : ISkillEffect
{
    public int Priority => 300;

    public bool ShouldApply(SkillEffectContext context)
    {
        return context.QueryService != null && context.Targets != null && context.Targets.Count > 0;
    }

    public UniTask Apply(SkillEffectContext context)
    {
        var queryService = context.QueryService;

        for (var i = 0; i < context.Targets.Count; i++)
        {
            var chara = context.Targets.GetAtCharacter(i);
            var helpGroup = queryService.GetGroupForCharacter(chara);
            if (helpGroup == null) continue;

            var liveAllyGroupList = queryService.GetOtherAlliesAlive(chara);
            if (liveAllyGroupList.Count < 1) continue;

            liveAllyGroupList = liveAllyGroupList
                .Where(ally => helpGroup.CharaCompatibility.ContainsKey((ally, chara)) && helpGroup.CharaCompatibility[(ally, chara)] >= 86)
                .ToList();
            if (liveAllyGroupList.Count < 1) continue;

            var data = chara.RecentDamageData;
            var damageRate = data.Damage / chara.MaxHP;

            foreach (var ally in liveAllyGroupList)
            {
                if (ally.NowPower < ThePower.medium) continue;

                float compatibility = helpGroup.CharaCompatibility[(ally, chara)];
                float compatibilityFactor = Mathf.Clamp01((compatibility - 86f) / (130f - 86f));
                compatibilityFactor = Mathf.Clamp01(compatibilityFactor) * 0.7f;

                float powerFactor = 0.5f;
                if (ally.NowPower > ThePower.medium) powerFactor = 1f;

                float k = 1.5f;
                float occurrenceProbability = compatibilityFactor * (1f + k * damageRate) * powerFactor;
                occurrenceProbability = Mathf.Clamp01(occurrenceProbability);

                if (rollper(occurrenceProbability * 100))
                {
                    float expectedDuration = occurrenceProbability * 12f;
                    int baseDuration = Mathf.FloorToInt(expectedDuration);
                    float extraChance = expectedDuration - baseDuration;
                    int duration = baseDuration;
                    if (RandomEx.Shared.NextFloat(1f) < extraChance / 2.3f)
                    {
                        duration++;
                    }

                    float bonusMultiplier = 1f + occurrenceProbability * 0.4f;
                    ally.TargetBonusDatas.Add(duration + 1, bonusMultiplier, data.Attacker);
                }
            }
        }

        return UniTask.CompletedTask;
    }
}
