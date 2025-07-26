using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using RandomExtensions;
/// <summary>
/// EnemyClass等基礎クラスで関数を共通して扱う 付け替え可能なAI用の抽象クラス
/// </summary>
public abstract class BattleAIBrain
{
    BattleManager manager;
    /// <summary>
    /// 思考部分のメイン関数
    /// 呼び出し側に値を返すのではなくmanagerを操作する形、managerが存在するときしか使わないし
    /// </summary>
    public virtual void Think()
    {
        manager = Walking.bm;
    }

    /// <summary>
    /// ダメージシミュレートのシミュレート内容
    /// </summary>
    [SerializeField]SkillAnalysisPolicy policy;
    /// <summary>
    /// 利用可能なスキルの中から最もダメージを与えられるスキルとターゲットを分析する
    ///  敵グループに対してどのスキルが最もダメージを与えられるかを分析する
    /// </summary>
    public BruteForceResult AnalyzeBestDamage(List<BaseSkill> availableSkills, List<BaseStates> potentialTargets)
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
        }

        //敵グループのHPに対して有効なスキルを分析する
        if(policy.groupType == TargetGroupType.Group)
        {
            return MultiBestDamageAndTargetAnalyzer(availableSkills, potentialTargets);
        }
        //敵単体に対して有効なスキルを分析する
        if(policy.groupType == TargetGroupType.Single)
        {
            if(policy.hpType == TargetHPType.Highest)//グループの中で最もHPが高い
            {
                ResultTarget = potentialTargets.OrderByDescending(x => x.HP).First();
            }
            if(policy.hpType == TargetHPType.Lowest)//グループの中で最もHPが低い
            {
                ResultTarget = potentialTargets.OrderBy(x => x.HP).First();
            }
            if(policy.hpType == TargetHPType.Random)//グループから一人をランダム
            {
                ResultTarget = RandomEx.Shared.GetItem(potentialTargets.ToArray());
            }
            ResultSkill = SingleBestDamageAnalyzer(availableSkills, ResultTarget);
        }

        foreach(var skill in availableSkills)
        {
            foreach(var target in potentialTargets)
            {
                var damage = target.SimulateDamage(manager.Acter, skill, policy);
            }
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
    /// <param name="availableSkills"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    BaseSkill SingleBestDamageAnalyzer(List<BaseSkill> availableSkills, BaseStates target)
    {
        if(availableSkills.Count == 0)
        {
            Debug.LogError("有効スキル単体ターゲット分析の関数に渡されたスキルが存在しません");
            return null;
        }

        var potential = -3f;
        BaseSkill ResultSkill = null;
        foreach(var skill in availableSkills)
        {
            var damage = target.SimulateDamage(manager.Acter, skill, policy);
            if(damage > potential)//与えたダメージが多ければ
            {
                potential = damage;//基準を更新
                ResultSkill = skill;//結果を更新
            }
        }
        return ResultSkill;
    }   
    /// <summary>
    /// グループに対して最大ダメージを与えるスキルとターゲットの組み合わせを分析する
    /// </summary>
    /// <param name="availableSkills">使用可能なスキルリスト</param>
    /// <param name="potentialTargets">攻撃対象候補リスト</param>
    /// <returns>最大ダメージを与えるスキルとターゲットの組み合わせ</returns>
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

        var maxDamage = -3f;
        BaseSkill bestSkill = null;
        BaseStates bestTarget = null;

        // 全スキル × 全ターゲットの組み合わせを総当たりで分析
        foreach(var skill in availableSkills)
        {
            foreach(var target in potentialTargets)
            {
                var damage = target.SimulateDamage(manager.Acter, skill, policy);
                if(damage > maxDamage)
                {
                    maxDamage = damage;
                    bestSkill = skill;
                    bestTarget = target;
                }
            }
        }

        return new BruteForceResult
        {
            Skill = bestSkill,
            Target = bestTarget
        };
    }
}

public class BruteForceResult
{
    public BaseSkill Skill;
    public BaseStates Target;
}
public enum TargetGroupType
{
    Single,
    Group
}
public enum SimulateDamageType
{
    dmg,
    mentalDmg,
}

public enum TargetHPType
{
    Highest,
    Lowest,
    Random
}


[Serializable]
public struct SkillAnalysisPolicy
{
    public TargetGroupType groupType; // 単体 or グループ
    public TargetHPType hpType;       // HP最大/最小/任意
    public SimulateDamageType damageType;
    public bool spiritualModifier;//精神補正
    public bool physicalResistance;//物理耐性
    public bool SimlateVitalLayerPenetration;//追加HP
    public bool SimlateEnemyDEF;//敵のDEF
}

//各キャラ用AI

public class TestAI1 : BattleAIBrain
{

    public override void Think()
    {
        base.Think();

    }


}
