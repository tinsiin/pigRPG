using RandomExtensions;
using System.Collections.Generic;
using System.Linq;
using System;
public static class CommonCalc
{
    /// <summary>
    /// RandomExtensionsを利用して、確率を判定する
    /// </summary>
    public static bool rollper(float percentage)
    {
        if (percentage < 0)percentage = 0;
        return RandomEx.Shared.NextFloat(100) < percentage;
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
    /// 十日能力辞書間の距離を計算するメソッド
    /// マンハッタン距離らしい(AI全任せwｗ)
    /// </summary>
    public static float CalculateTenDaysDistance(SerializableDictionary<TenDayAbility, float> skillValues1, Dictionary<TenDayAbility, float> skiillValues2)
    {
        float totalDistance = 0f;
        
        // すべての十日能力を列挙
        foreach (TenDayAbility ability in Enum.GetValues(typeof(TenDayAbility)))
        {
            // 1つ目の値（存在しない場合は0）
            float value1 = skillValues1.GetValueOrZero(ability);
            
            // 2つ目の値（存在しない場合は0）
            float value2 = skiillValues2.GetValueOrZero(ability);
            
            // 距離を加算（値の差の絶対値）
            totalDistance += Math.Abs(value1 - value2);
        }
        
        return totalDistance;
    }
}