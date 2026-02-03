using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RandomExtensions;
using UnityEngine;

/// <summary>
/// 淡々としたロゼ効果。
/// 前のめり状態で攻撃スキルを使用した際に追加行動を予約する。
/// </summary>
public sealed class FlatRozeEffect : ISkillEffect
{
    public int Priority => 100;

    public bool ShouldApply(SkillEffectContext context)
    {
        var acter = context.Acter;
        if (acter == null || acter.NowUseSkill == null) return false;
        if (!acter.HasPassive(0)) return false;
        if (!acter.NowUseSkill.HasType(SkillType.Attack)) return false;
        if (acter._tempVanguard) return false;
        if (context.QueryService == null || !context.QueryService.IsVanguard(acter)) return false;
        if (acter.NowUseSkill.RecordDoCount <= 20) return false;
        if (acter.NowUseSkill.NowConsecutiveATKFromTheSecondTimeOnward()) return false;

        return true;
    }

    public UniTask Apply(SkillEffectContext context)
    {
        var acter = context.Acter;
        var chance = Ideal50or60Easing(acter, acter.NowUseSkill.SkillHitPer);

        if (RandomEx.Shared.NextInt(100) >= chance)
        {
            return UniTask.CompletedTask;
        }

        context.Acts.Add(acter, context.ActerFaction, "淡々としたロゼ", new List<ModifierPart>
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

        return UniTask.CompletedTask;
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
