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
    /// スキルの印象構造（素振り + HIT の合算）。
    /// 乖離判定・使いこなし度・TenDayValuesSum 等、成長以外の全用途で使用。
    /// </summary>
    public TenDayAbilityDictionary TenDayValues(bool IsCradle = false, BaseStates actor = null)
    {
        return TenDayValuesSwing(IsCradle, actor) + TenDayValuesHit(IsCradle, actor);
    }

    /// <summary>
    /// 素振り成長用の十日能力値
    /// </summary>
    public TenDayAbilityDictionary TenDayValuesSwing(bool IsCradle = false, BaseStates actor = null)
    {
        return GetTenDayValuesInternal(IsCradle, actor, swing: true);
    }

    /// <summary>
    /// HIT時成長用の十日能力値
    /// </summary>
    public TenDayAbilityDictionary TenDayValuesHit(bool IsCradle = false, BaseStates actor = null)
    {
        return GetTenDayValuesInternal(IsCradle, actor, swing: false);
    }

    /// <summary>
    /// 内部共通: swing=true なら素振り側、false ならHIT側を返す
    /// </summary>
    TenDayAbilityDictionary GetTenDayValuesInternal(bool IsCradle, BaseStates actor, bool swing)
    {
        if (FixedSkillLevelData == null || FixedSkillLevelData.Count == 0)
        {
            Debug.LogError($"スキル「{SkillName}」のFixedSkillLevelDataが空です。Inspectorで最低1つのスキルレベルデータを設定してください");
            return new TenDayAbilityDictionary();
        }
        var Level = _nowSkillLevel;
        if(IsCradle)
        {
            Level = _cradleSkillLevel;
        }

        if(FixedSkillLevelData.Count > Level)
        {
            var data = FixedSkillLevelData[Level];
            return swing ? (data.TenDayValuesSwing ?? new TenDayAbilityDictionary())
                         : (data.TenDayValuesHit ?? new TenDayAbilityDictionary());
        }
        else
        {
            var lastData = FixedSkillLevelData[FixedSkillLevelData.Count - 1];
            var InfiniteLevelMultiplier = Level - (FixedSkillLevelData.Count - 1);

            var baseValues = swing ? (lastData.TenDayValuesSwing ?? new TenDayAbilityDictionary())
                                   : (lastData.TenDayValuesHit ?? new TenDayAbilityDictionary());
            var infUnit = swing ? _infiniteSkillTenDaysSwingUnit
                                : _infiniteSkillTenDaysHitUnit;

            return baseValues + infUnit * InfiniteLevelMultiplier;
        }
    }

    /// <summary>
    /// スキルの印象構造の十日能力値の合計（素振り+HIT合算）
    /// </summary>
    public float TenDayValuesSum => TenDayValues().Sum(kvp => kvp.Value);
}
