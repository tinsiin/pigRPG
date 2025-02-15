using RandomExtensions;
using System.Collections.Generic;
using System.Linq;
public static class CommonCalc
{
    /// <summary>
    /// RandomExtensionsを利用して、確率を判定する
    /// </summary>
    public static bool rollper(float percentage)
    {
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
}