using System.Linq;
using Cysharp.Threading.Tasks;
using RandomExtensions;
using UnityEngine;
using static CommonCalc;

/// <summary>
/// 相性による回復ターン短縮効果。
/// ダメージを受けたキャラクターの仲間が回復ターンを短縮する。
/// </summary>
public sealed class HelpRecoveryEffect : ISkillEffect
{
    public int Priority => 200;

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
                .Where(ally => helpGroup.CharaCompatibility.ContainsKey((ally, chara)) && helpGroup.CharaCompatibility[(ally, chara)] >= 60)
                .ToList();
            if (liveAllyGroupList.Count < 1) continue;

            var data = chara.RecentDamageData;
            var damageRate = data.Damage / chara.MaxHP;

            foreach (var ally in liveAllyGroupList)
            {
                var compatibility = helpGroup.CharaCompatibility[(ally, chara)];
                var occurrenceProbability = 0f;
                var baseChance = 0f;

                if (compatibility > 130)
                {
                    baseChance = 0.47f;
                }
                else if (compatibility < 60)
                {
                    baseChance = 0f;
                }
                else
                {
                    float compOffset = compatibility - 60f;
                    float x = 0.2f * compOffset - 4.0f;
                    float sig = 1f / (1f + Mathf.Exp(-x));
                    baseChance = 0.34f * sig;
                }

                var helpRate = damageRate;
                if (data.IsBadPassiveHit || data.IsBadVitalLayerHit || data.IsGoodPassiveRemove || data.IsGoodVitalLayerRemove)
                {
                    helpRate += RandomEx.Shared.NextFloat(0.07f, 0.15f);
                }
                helpRate = Mathf.Clamp01(helpRate);

                float k = 2f;
                occurrenceProbability = baseChance * (1f + k * helpRate);
                occurrenceProbability = Mathf.Min(occurrenceProbability, 1f);

                if (rollper(occurrenceProbability * 100))
                {
                    float expectedShorten = occurrenceProbability * 4f;
                    var baseShorten = Mathf.Floor(expectedShorten);
                    var ratio = expectedShorten - baseShorten;
                    var upChance = ratio / 3;
                    float finalShorten = baseShorten;
                    if (RandomEx.Shared.NextFloat(1) < upChance)
                    {
                        finalShorten = baseShorten + 1;
                    }

                    ally.RecovelyTurnTmpMinus((int)finalShorten);
                }
            }
        }

        return UniTask.CompletedTask;
    }
}
