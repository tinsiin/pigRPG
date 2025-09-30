using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキルのポイント可否判定を提供するユーティリティ（副作用なし）。
/// - UI/AI/実行層から共通利用するための純粋関数群。
/// - 消費や変換・返金などの「清算」は別層で扱う（本クラスは事前チェックのみ）。
/// </summary>
public static class SkillResourceFlow
{
    /// <summary>
    /// 発動時に要求されるポイントを <paramref name="doer"/> が支払えるかを判定（副作用なし）。
    /// </summary>
    public static bool CanConsumeOnCast(BaseStates doer, BaseSkill skill)
    {
        if (doer == null || skill == null)
        {
            //Debug.LogError("[CanConsumeOnCast] doer==null or skill==null. doerNull=" + (doer == null) + " skillNull=" + (skill == null));
            return false;
        }

        int reqNormal = Mathf.Max(0, skill.RequiredNormalP);
        int haveNormal = Mathf.Max(0, doer.P);
        if (haveNormal < reqNormal)
        {
            //Debug.LogError("[CanConsumeOnCast] NormalP insufficient. have=" + haveNormal + " need=" + reqNormal);
            return false;
        }

        var reqAttr = skill.RequiredAttrP;
        if (reqAttr != null)
        {
            foreach (var kv in reqAttr)
            {
                int need = Mathf.Max(0, kv.Value);
                if (need == 0) continue;
                int have = doer.GetAttrP(kv.Key);
                if (have < need)
                {
                    //Debug.LogError("[CanConsumeOnCast] AttrP insufficient. key=" + kv.Key + " have=" + have + " need=" + need);
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// スキルに必要なリソースと武器適合の両方を満たしているかの基本判定。
    /// </summary>
    public static bool CanCastSkill(BaseStates actor, BaseSkill skill)
    {
        if (actor == null || skill == null)
        {
            return false;
        }

        if (!CanConsumeOnCast(actor, skill))
        {
            return false;
        }

        float requiredHpPercent = Mathf.Clamp(skill.RequiredRemainingHPPercent, 0f, 100f);
        if (requiredHpPercent > 0f)
        {
            float maxHP = actor.MaxHP;
            if (maxHP <= 0f)
            {
                return false;
            }

            float currentHpPercent = actor.HP / maxHP * 100f;
            if (currentHpPercent + 0.0001f < requiredHpPercent)
            {
                return false;
            }
        }

        bool hasBladeWeapon = actor.NowUseWeapon != null && actor.NowUseWeapon.IsBlade;
        return hasBladeWeapon || !skill.IsBlade;
    }

    /// <summary>
    /// 可否判定の詳細（不足内訳）を返す。副作用なし。
    /// </summary>
    public static AffordanceResult GetAffordanceOnCast(BaseStates doer, BaseSkill skill)
    {
        var result = new AffordanceResult
        {
            CanUse = false,
            MissingNormalP = 0,
            MissingAttrP = new Dictionary<SpiritualProperty, int>()
        };

        if (doer == null || skill == null)
        {
            return result;
        }

        int reqNormal = Mathf.Max(0, skill.RequiredNormalP);
        int haveNormal = Mathf.Max(0, doer.P);
        result.MissingNormalP = Mathf.Max(0, reqNormal - haveNormal);

        var reqAttr = skill.RequiredAttrP;
        if (reqAttr != null)
        {
            foreach (var kv in reqAttr)
            {
                int need = Mathf.Max(0, kv.Value);
                if (need == 0) continue;
                int have = doer.GetAttrP(kv.Key);
                int missing = Mathf.Max(0, need - have);
                if (missing > 0)
                {
                    result.MissingAttrP[kv.Key] = missing;
                }
            }
        }

        result.CanUse = result.MissingNormalP == 0 && result.MissingAttrP.Count == 0;
        return result;
    }

    /// <summary>
    /// 可否判定の詳細結果。
    /// </summary>
    public struct AffordanceResult
    {
        public bool CanUse;
        public int MissingNormalP; // 0以上
        public Dictionary<SpiritualProperty, int> MissingAttrP; // 属性ごとの不足（0は含めない）
    }
}
