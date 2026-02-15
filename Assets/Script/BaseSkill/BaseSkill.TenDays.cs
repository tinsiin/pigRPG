using R3;
using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
public partial class BaseSkill
{
    /// <summary>
    /// スキルの印象構造　十日能力値
    /// ゆりかごするかどうかは引数で
    /// </summary>
    public TenDayAbilityDictionary TenDayValues(bool IsCradle = false, BaseStates actor = null)
    {
        if (FixedSkillLevelData == null || FixedSkillLevelData.Count == 0)
        {
            Debug.LogError($"スキル「{SkillName}」のFixedSkillLevelDataが空です。Inspectorで最低1つのスキルレベルデータを設定してください");
            return new TenDayAbilityDictionary();
        }
        Debug.Log($"スキル印象構造の取得 : スキル有限レベルリストの数:{FixedSkillLevelData.Count}" + (actor != null ? $",キャラ:{actor.CharacterName}" : ""));
        var Level = _nowSkillLevel;
        if(IsCradle)
        {
            Level = _cradleSkillLevel;//ゆりかごならゆりかご用計算されたスキルレベルが
        }

        //skillLecelが有限範囲ならそれを返す
        if(FixedSkillLevelData.Count > Level)
        {
            return FixedSkillLevelData[Level].TenDayValues;
        }else
        {//そうでないなら有限最終以降と無限単位の加算
            //有限リストの最終値と無限単位に以降のスキルレベルを乗算した物を加算
            //有限リストの最終値を基礎値にする
            var BaseTenDayValues = FixedSkillLevelData[FixedSkillLevelData.Count - 1].TenDayValues;

            //有限リストの超過分、無限単位にどの程度かけるかの数
            var InfiniteLevelMultiplier =  Level - (FixedSkillLevelData.Count - 1);

            //基礎値に無限単位に超過分を掛けたものを加算して返す。
            return BaseTenDayValues + _infiniteSkillTenDaysUnit * InfiniteLevelMultiplier;
        
        }
    }

    /// <summary>
    /// スキルの印象構造の十日能力値の合計
    /// </summary>
    public float TenDayValuesSum => TenDayValues().Sum(kvp => kvp.Value);
}
