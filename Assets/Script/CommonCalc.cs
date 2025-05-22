using RandomExtensions;
using System.Collections.Generic;
using System.Linq;
using System;
public static class CommonCalc
{
    /// <summary>
    /// ケレン行動パーセントのデフォルト値(偶然の定数)
    /// </summary>
    public const float KerenACTRateDefault = 4.4f;
    /// <summary>
    /// RandomExtensionsを利用して、確率を判定する
    /// 百分率の数字で
    /// </summary>
    public static bool rollper(float percentage)
    {
        if (percentage < 0)percentage = 0;
        return RandomEx.Shared.NextFloat(100) < percentage;
    }
    /// <summary>
    /// 二つの値のどちらが選ばれるかを確率的に決定する
    /// </summary>
    /// <param name="a">最初の値</param>
    /// <param name="b">二番目の値</param>
    /// <returns>aが選ばれたらtrue、bが選ばれたらfalse</returns>
    public static bool rollComparison(float a, float b)
    {
        float totalValue = a + b;
        float percentage = a / totalValue * 100f;
        return rollper(percentage);
    }
    /// <summary>
    /// BaseStatesを継承したキャラクターのListから死亡者を省いたリストに変換する
    /// </summary>
    public static List<BaseStates> RemoveDeathCharacters(List<BaseStates> Charas)
    {
        return Charas.Where(chara => !chara.Death()).ToList();
    }
    /// <summary>
    /// 死亡者のみのリストに変換する
    /// </summary>
    public static List<BaseStates> OnlyDeathCharacters(List<BaseStates> Charas)
    {
        return Charas.Where(chara => chara.Death()).ToList();
    }

    /// <summary>
    /// ランダム性があるかどうか。
    /// 範囲意志と範囲誠意どっちと比べるかを引数で指定する。
    /// </summary>
    public static bool IsRandomTrait(SkillZoneTrait trait)
    {
        return HasZoneTraitAny(trait, SkillZoneTrait.RandomSingleTarget, 
        SkillZoneTrait.RandomSelectMultiTarget, SkillZoneTrait.RandomMultiTarget, SkillZoneTrait.RandomRange);
    }
    /// <summary>
    /// 渡したスキル範囲の内、いずれかの性質を持っているかどうか
    /// 複数指定した場合はどれか一つでも当てはまればtrueを返す
    /// </summary>
    /// <param name="ZoneTrait">対象範囲性質</param>
    /// <param name="skills">対象範囲性質の配列</param>
    /// <returns>対象範囲性質のいずれかを持っているかどうか</returns>
    public static bool HasZoneTraitAny(SkillZoneTrait ZoneTrait,params SkillZoneTrait[] skills)
    {
        return skills.Any(skill => (ZoneTrait & skill) != 0);
    }


    /// <summary>
    /// 十日能力辞書間の距離を計算するメソッド
    /// マンハッタン距離らしい(AI全任せwｗ)
    /// </summary>
    public static float CalculateTenDaysDistance(TenDayAbilityDictionary skillValues1, TenDayAbilityDictionary skillValues2)
    {
        float totalDistance = 0f;
        
        // すべての十日能力を列挙
        foreach (TenDayAbility ability in Enum.GetValues(typeof(TenDayAbility)))
        {
            // 1つ目の値（存在しない場合は0）
            float value1 = skillValues1.GetValueOrZero(ability);
            
            // 2つ目の値（存在しない場合は0）
            float value2 = skillValues2.GetValueOrZero(ability);
            
            // 距離を加算（値の差の絶対値）
            totalDistance += Math.Abs(value1 - value2);
        }
        
        return totalDistance;
    }
    /// <summary>
    /// 単体系のスキル範囲性質
    /// </summary>
    public static SkillZoneTrait SingleZoneTrait = SkillZoneTrait.CanPerfectSelectSingleTarget | SkillZoneTrait.CanSelectSingleTarget | 
                                SkillZoneTrait.RandomSingleTarget | SkillZoneTrait.ControlByThisSituation;

}