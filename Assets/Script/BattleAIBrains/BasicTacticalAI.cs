using UnityEngine;
using System.Linq;

/// <summary>
/// 基本戦術AI：SkillAnalysisPolicyベースのダメージ分析 + 逃走判断。
/// Inspector設定の組み合わせ（SkillAnalysisPolicy / 逃走確率 / variationStages）で
/// 1つのSOクラスから多様な個性を生み出せる。
///
/// SimpleRandomTestAI（ランダム選択）からの進化点:
/// - ターゲット選択の合理化（HP低い味方を狙う等、SkillAnalysisPolicy.hpTypeで制御）
/// - ダメージ最大スキルの選択（期待ダメージ対応）
/// - 逃走の導入（_canEscape + _escapeChance）
/// </summary>
[CreateAssetMenu(fileName = "BasicTacticalAI", menuName = "BattleAIBrain/BasicTacticalAI")]
public class BasicTacticalAI : BattleAIBrain
{
    protected override void Plan(AIDecision decision)
    {
        if (availableSkills == null || availableSkills.Count == 0) return;

        // 逃走判断（最優先）
        if (ShouldEscape())
        {
            LogThink(0, "逃走を選択");
            decision.IsEscape = true;
            return;
        }

        // トラウマフィルタ（カウンターされたスキルを回避）
        var candidates = FilterByTrauma(availableSkills);

        // 攻撃対象の列挙
        var targets = GetPotentialTargets();

        // ダメージ分析でベストスキル＋ターゲットを選定
        if (targets.Count > 0 && candidates.Count >= 2)
        {
            var result = AnalyzeBestDamage(candidates, targets);
            if (result?.Skill != null)
            {
                decision.Skill = result.Skill;
                // 完全単体選択スキルなら単体意思を設定
                if (result.Skill.HasZoneTraitAny(SkillZoneTrait.CanPerfectSelectSingleTarget))
                {
                    decision.TargetWill = DirectedWill.One;
                }
                return;
            }
        }

        // フォールバック: スキルが1つだけ、またはダメージ分析が使えない場合
        var skill = candidates.Count == 1
            ? candidates[0]
            : RandomSource.GetItem(candidates);

        decision.Skill = skill;
        if (skill.HasZoneTraitAny(SkillZoneTrait.CanPerfectSelectSingleTarget))
        {
            decision.TargetWill = DirectedWill.One;
        }
    }
}
