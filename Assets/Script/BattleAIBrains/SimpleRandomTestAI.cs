using System;
using UnityEngine;
using Unity.VisualScripting;
using R3;
using Cysharp.Threading.Tasks;
using static CommonCalc;
using System.Threading.Tasks;
using System.Linq;

/// <summary>
/// シンプルにランダムにスキルを選び、スキルが完全単体選択だった場合のみ、単体選択する
/// 複雑な範囲性質はキャラに合わせて考えるため、テスト用では利用しない。　　
/// 完全単体選択のみ実装しているが、基本は「戦闘全体フロー用」のテスト敵ControlByThisSituationで
/// </summary>
[CreateAssetMenu(fileName = "SimpleRandomTestAI", menuName = "BattleAIBrain/SimpleRandomTestAI")]
public class SimpleRandomTestAI : BattleAIBrain
{
    protected override void Plan(AIDecision decision)
    {
        // availableSkills は Run() 内で抽出済み（基底が用意）
        if (availableSkills == null || availableSkills.Count == 0) return;

        // 一個適当に有効スキルから選ぶだけ
        var rndSkill = RandomSource.GetItem(availableSkills);
        if (rndSkill == null) return;

        // 結果としてスキルのみ設定（単体先約時はCommitでスキルだけ反映される）
        decision.Skill = rndSkill;

        // 完全単体選択スキルなら、単体意思を提示（先約時はCommitが無視）
        if (rndSkill.HasZoneTraitAny(SkillZoneTrait.CanPerfectSelectSingleTarget))
        {
            decision.TargetWill = DirectedWill.One;
        }
    }
}
