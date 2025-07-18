using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 動作的雰囲気　スキル印象の裏バージョン
/// </summary>
[CreateAssetMenu(menuName = "MotionFlavorTag")]
public class MotionFlavorTag : ScriptableObject
{
    public string displayName;   // 雰囲気名
    [SerializeField] string memo;
}
/// <summary>
/// 動作的雰囲気のユーティリティ
/// </summary>
public static class MotionFlavorUtil
{
    /// <summary>
    /// キャラクターの現在有効なスキルの動作的雰囲気に応じた辞書化
    /// </summary>
    /// <param name="Character"></param>
    /// <returns></returns>
    public static Dictionary<string, List<BaseSkill>> CharacterMotionFlavorDict(BaseStates Character)
        => BuildDict(Character.SkillList);

    /// <summary>
    /// スキルリストの動作的雰囲気による区切りをした辞書化ロジック
    /// </summary>
    /// <param name="skills"></param>
    /// <returns></returns>
    static Dictionary<string,List<BaseSkill>> BuildDict(IEnumerable<BaseSkill> skills)
    {
        var dict = new Dictionary<string, List<BaseSkill>>();

        foreach (var s in skills)
        {
            string key = s.MotionFlavor ? s.MotionFlavor.displayName
                                        : s.SkillName;           // null → ユニーク

            if (!dict.TryGetValue(key, out var list))//辞書にキーが存在しない場合はlistにnullが入るので
                dict[key] = list = new List<BaseSkill>();//新しいリストを作成

            list.Add(s);//既存のか新しいものどちらでもリストにスキルを追加
        }
        return dict;
    }
}
