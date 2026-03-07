using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BattleAIBrain partial: ダメージ分析・命中率推定・期待ダメージ評価
/// </summary>
public abstract partial class BattleAIBrain
{
    // ---------- ベストダメージ分析関連----------------------------------

    /// <summary>
    /// 利用可能なスキルの中から最もダメージを与えられるスキルとターゲットを分析する
    ///  敵グループに対してどのスキルが最もダメージを与えられるかを分析する
    /// </summary>
    protected BruteForceResult AnalyzeBestDamage(List<BaseSkill> availableSkills, List<BaseStates> potentialTargets)
    {
        if(potentialTargets.Count == 0)
        {
            Debug.LogError("有効スキル分析の関数に渡されたターゲットが存在しません");
            return null;
        }
        if(availableSkills.Count == 0)
        {
            Debug.LogError("有効スキル分析の関数に渡されたスキルが存在しません");
            return null;
        }
        if(availableSkills.Count == 1)
        {
            Debug.LogWarning("有効スキル分析の関数に渡されたスキルが1つしかないため、最適なスキルを分析する必要はありません");
            return null;
        }
        BaseStates ResultTarget = null;
        BaseSkill ResultSkill = null;

        if(potentialTargets.Count == 1)//ターゲットが一人ならそのまま単体スキルの探索
        {
            ResultTarget = potentialTargets[0];
            ResultSkill = SingleBestDamageAnalyzer(availableSkills, ResultTarget);
            return new BruteForceResult { Skill = ResultSkill, Target = ResultTarget };
        }

        //敵グループのHPに対して有効なスキルを分析する
        if(_damageSimulatePolicy.groupType == TargetGroupType.Group)
        {
            return MultiBestDamageAndTargetAnalyzer(availableSkills, potentialTargets);
        }
        //敵単体に対して有効なスキルを分析する
        if(_damageSimulatePolicy.groupType == TargetGroupType.Single)
        {
            if(_damageSimulatePolicy.hpType == TargetHPType.Highest)//グループの中で最もHPが高い
            {
                ResultTarget = potentialTargets.OrderByDescending(x => x.HP).First();
            }
            if(_damageSimulatePolicy.hpType == TargetHPType.Lowest)//グループの中で最もHPが低い
            {
                ResultTarget = potentialTargets.OrderBy(x => x.HP).First();
            }
            if(_damageSimulatePolicy.hpType == TargetHPType.Random)//グループから一人をランダム
            {
                ResultTarget = RandomSource.GetItem(potentialTargets);
            }
            ResultSkill = SingleBestDamageAnalyzer(availableSkills, ResultTarget);
        }

        return new BruteForceResult
        {
            Skill = ResultSkill,
            Target = ResultTarget
        };
    }

    /// <summary>
    /// 単体用有効スキル分析ダメージ由来を選考する関数
    /// </summary>
    BaseSkill SingleBestDamageAnalyzer(List<BaseSkill> availableSkills, BaseStates target)
    {
        if(availableSkills.Count == 0)
        {
            Debug.LogError("有効スキル単体ターゲット分析の関数に渡されたスキルが存在しません");
            return null;
        }

        var result = DamageStepAnalysisHelper.SelectWithStepAndVariation(
            availableSkills,
            skill =>
            {
                var damage = EvaluateDamage(target, skill);
                LogThink(3, () => $"  Single試算: {skill.SkillName} → {target.CharacterName} = {damage:F1}");
                return damage;
            },
            _damageSimulatePolicy,
            RandomSource
        );
        LogThink(2, $"Single最適: {result?.SkillName ?? "none"} → {target.CharacterName}");
        return result;
    }

    /// <summary>
    /// グループに対して最大ダメージを与えるスキルとターゲットの組み合わせを分析する
    /// </summary>
    BruteForceResult MultiBestDamageAndTargetAnalyzer(List<BaseSkill> availableSkills, List<BaseStates> potentialTargets)
    {
        if(availableSkills.Count == 0)
        {
            Debug.LogError("グループ分析の関数に渡されたスキルが存在しません");
            return null;
        }
        if(potentialTargets.Count == 0)
        {
            Debug.LogError("グループ分析の関数に渡されたターゲットが存在しません");
            return null;
        }

        var combinations = new List<BruteForceResult>();
        foreach(var skill in availableSkills)
        {
            foreach(var target in potentialTargets)
            {
                var damage = EvaluateDamage(target, skill);
                LogThink(3, () => $"  Group試算: {skill.SkillName} → {target.CharacterName} = {damage:F1}");
                combinations.Add(new BruteForceResult
                {
                    Skill = skill,
                    Target = target,
                    Damage = damage
                });
            }
        }

        var selectedCombination = DamageStepAnalysisHelper.SelectWithStepAndVariation(
            combinations,
            combination => combination.Damage,
            _damageSimulatePolicy,
            RandomSource
        );

        var groupResult = selectedCombination ?? combinations.First();
        LogThink(2, $"Group最適: {groupResult.Skill?.SkillName ?? "none"} → {groupResult.Target?.CharacterName ?? "none"}");
        return groupResult;
    }


    // --- 命中率シミュレート + 期待ダメージ ---

    /// <summary>
    /// ターゲットに対するスキルの命中率を推定する（派生クラス向け公開ヘルパー）
    /// </summary>
    protected float EstimateHitRate(BaseStates target, BaseSkill skill)
    {
        if (target == null || skill == null || user == null) return 0f;
        var policy = _damageSimulatePolicy.considerVanguardForHit && manager != null
            ? HitSimulatePolicy.FromBattleState(manager, user, target)
            : HitSimulatePolicy.Minimal;
        return target.SimulateHitRate(user, skill, policy);
    }

    /// <summary>
    /// ダメージ × 命中率の期待ダメージを返す（派生クラス向け公開ヘルパー）
    /// </summary>
    protected float EstimateExpectedDamage(BaseStates target, BaseSkill skill)
    {
        float damage = target.SimulateDamage(user, skill, _damageSimulatePolicy);
        float hitRate = EstimateHitRate(target, skill);
        return damage * hitRate;
    }

    /// <summary>
    /// ポリシーに応じてダメージまたは期待ダメージを返す（分析関数内部用）。
    /// 精神レベルに応じてdamageType/spiritualModifierを動的に調整する。
    /// </summary>
    private float EvaluateDamage(BaseStates target, BaseSkill skill)
    {
        var policy = GetSpirituallyAwarePolicy(target);
        float damage = EvaluateDamageWithPolicy(target, skill, policy);

        // Lv3: 物理と精神の両方を評価し、高い方を採用
        if (_spiritualLevel >= 3)
        {
            var altPolicy = policy;
            altPolicy.damageType = policy.damageType == SimulateDamageType.dmg
                ? SimulateDamageType.mentalDmg
                : SimulateDamageType.dmg;
            altPolicy.spiritualModifier = true;
            float altDamage = EvaluateDamageWithPolicy(target, skill, altPolicy);
            if (altDamage > damage)
            {
                LogThink(3, () => $"  精神Lv3切替: {altPolicy.damageType}の方が有効 ({altDamage:F1} > {damage:F1})");
                damage = altDamage;
            }
        }

        return damage;
    }

    /// <summary>
    /// 指定ポリシーでダメージ（or期待ダメージ）を評価する内部ヘルパー
    /// </summary>
    private float EvaluateDamageWithPolicy(BaseStates target, BaseSkill skill, SkillAnalysisPolicy policy)
    {
        float damage = target.SimulateDamage(user, skill, policy);
        if (policy.useExpectedDamage)
        {
            var hitPolicy = policy.considerVanguardForHit && manager != null
                ? HitSimulatePolicy.FromBattleState(manager, user, target)
                : HitSimulatePolicy.Minimal;
            float hitRate = target.SimulateHitRate(user, skill, hitPolicy);
            LogThink(3, () => $"  期待ダメージ補正: hitRate={hitRate:F3} dmg={damage:F1} → {damage * hitRate:F1}");
            damage *= hitRate;
        }
        return damage;
    }

    /// <summary>
    /// 精神レベルに応じてSkillAnalysisPolicyを動的に調整する。
    /// Lv0: 変更なし（Inspector設定のまま）
    /// Lv1: ターゲットの精神HPが物理HPより削れているなら精神ダメージを狙う
    /// Lv2: さらに精神属性相性を考慮
    /// Lv3: GetSpirituallyAwarePolicyではLv2と同じ。EvaluateDamage側で両方比較
    /// </summary>
    private SkillAnalysisPolicy GetSpirituallyAwarePolicy(BaseStates target)
    {
        var policy = _damageSimulatePolicy;
        if (_spiritualLevel <= 0 || target == null) return policy;

        // Lv1+: ターゲットの精神HPが物理HPより削れていたら精神ダメージモードに切り替え
        float targetHpRatio = target.MaxHP > 0 ? target.HP / target.MaxHP : 1f;
        float targetMentalRatio = target.MentalMaxHP > 0 ? target.MentalHP / target.MentalMaxHP : 1f;
        if (targetMentalRatio < targetHpRatio)
        {
            policy.damageType = SimulateDamageType.mentalDmg;
            LogThink(3, () => $"  精神Lv{_spiritualLevel}: 精神HP({targetMentalRatio:P0})が物理HP({targetHpRatio:P0})より削れている → mentalDmg");
        }

        // Lv2+: 精神属性相性を考慮
        if (_spiritualLevel >= 2)
        {
            policy.spiritualModifier = true;
        }

        return policy;
    }
}
