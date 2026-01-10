using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using RandomExtensions;
using UnityEngine;
using static CommonCalc;

public sealed class EffectResolver
{
    public void ApplyRatherDamage(List<BaseStates> targets, float damageAmount)
    {
        var damage = new StatesPowerBreakdown(new TenDayAbilityDictionary(), damageAmount);
        foreach (var target in targets)
        {
            target.RatherDamage(damage, false, 1);
        }
    }

    public async UniTask ResolveSkillEffectsAsync(
        BaseStates acter,
        allyOrEnemy acterFaction,
        UnderActersEntryList targets,
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        ActionQueue acts,
        int battleTurnCount,
        Action<string> messageCallback)
    {
        if (acter == null || targets == null) return;

        var skill = acter.NowUseSkill;
        if (skill == null) return;

        skill.SetDeltaTurn(battleTurnCount);
        var message = await acter.AttackChara(targets);
        messageCallback?.Invoke(message);

        TryAddFlatRoze(acter, acterFaction, allyGroup, enemyGroup, acts);
        TryHelpMinusRecovelyTurnByCompatibility(targets, allyGroup, enemyGroup);
        TryAddRevengeBonus(targets, allyGroup, enemyGroup);
    }

    private static bool TryAddFlatRoze(
        BaseStates acter,
        allyOrEnemy acterFaction,
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        ActionQueue acts)
    {
        if (acter == null || acter.NowUseSkill == null) return false;
        if (!acter.HasPassive(0)) return false;
        if (!acter.NowUseSkill.HasType(SkillType.Attack)) return false;
        if (acter._tempVanguard) return false;
        if (!IsVanguard(acter, allyGroup, enemyGroup)) return false;
        if (acter.NowUseSkill.RecordDoCount <= 20) return false;
        if (acter.NowUseSkill.NowConsecutiveATKFromTheSecondTimeOnward()) return false;

        if (RandomEx.Shared.NextInt(100) >= Ideal50or60Easing(acter, acter.NowUseSkill.SkillHitPer)) return false;

        acts.Add(acter, acterFaction, "淡々としたロゼ", new List<ModifierPart>
        {
            new(
                "ロゼ瞳",
                whatModify.atk,
                1.6f + GetCoolnesFlatRozePower(acter),
                null,
                false
            ),
            new(
                "ロゼ威力半減",
                whatModify.atk,
                0.5f,
                null,
                false
            )
        }, true);

        return true;
    }

    private static void TryHelpMinusRecovelyTurnByCompatibility(
        UnderActersEntryList targets,
        BattleGroup allyGroup,
        BattleGroup enemyGroup)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            var chara = targets.GetAtCharacter(i);
            var helpGroup = GetGroupForChara(chara, allyGroup, enemyGroup);
            if (helpGroup == null) continue;
            var liveAllyGroupList = GetOtherAlliesAlive(chara, allyGroup, enemyGroup);
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
    }

    private static void TryAddRevengeBonus(
        UnderActersEntryList targets,
        BattleGroup allyGroup,
        BattleGroup enemyGroup)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            var chara = targets.GetAtCharacter(i);
            var helpGroup = GetGroupForChara(chara, allyGroup, enemyGroup);
            if (helpGroup == null) continue;
            var liveAllyGroupList = GetOtherAlliesAlive(chara, allyGroup, enemyGroup);
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
    }

    private static BattleGroup GetGroupForChara(BaseStates chara, BattleGroup allyGroup, BattleGroup enemyGroup)
    {
        if (allyGroup != null && allyGroup.Ours.Contains(chara)) return allyGroup;
        if (enemyGroup != null && enemyGroup.Ours.Contains(chara)) return enemyGroup;
        return null;
    }

    private static List<BaseStates> GetOtherAlliesAlive(BaseStates chara, BattleGroup allyGroup, BattleGroup enemyGroup)
    {
        var group = GetGroupForChara(chara, allyGroup, enemyGroup);
        if (group == null) return new List<BaseStates>();
        return RemoveDeathCharacters(group.Ours).Where(x => x != chara).ToList();
    }

    private static bool IsVanguard(BaseStates chara, BattleGroup allyGroup, BattleGroup enemyGroup)
    {
        if (allyGroup != null && chara == allyGroup.InstantVanguard) return true;
        if (enemyGroup != null && chara == enemyGroup.InstantVanguard) return true;
        return false;
    }

    private static float GetCoolnessFlatRozeChance(BaseStates acter)
    {
        var coolPower = acter.TenDayValues(false).GetValueOrZero(TenDayAbility.SpringWater);
        return Mathf.Floor(coolPower / 16.7f) * 0.01f;
    }

    private static float GetCoolnesFlatRozePower(BaseStates acter)
    {
        var coolPower = acter.TenDayValues(true).GetValueOrZero(TenDayAbility.SpringWater);
        return coolPower * 0.005f;
    }

    private static float Ideal50or60Easing(BaseStates acter, float x, float alpha = 4.3f)
    {
        float coolChance = GetCoolnessFlatRozeChance(acter);
        x = Mathf.Clamp(x, 0f, 100f);

        float dist50 = Mathf.Abs(x - 50f);
        float dist60 = Mathf.Abs(x - 60f);
        float d = Mathf.Min(dist50, dist60);

        float ab = 12f;
        float bc = 25f;

        if (d >= bc)
        {
            return 0f + coolChance;
        }
        else if (d >= ab)
        {
            float t = (d - ab) / (bc - ab);
            return Mathf.Lerp(4.44f, 0f, t) + coolChance;
        }
        else
        {
            float baseline = 4.44f;
            float peak = 27f;

            float t = (ab - d) / ab;
            float easePortion = Mathf.Pow(t, alpha);

            float val = baseline + (peak - baseline) * easePortion;
            return val + coolChance;
        }
    }
}
