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
    /// スキルのパワー　
    /// ゆりかご効果によるスキルレベルを参照するか、そうでないか。
    /// </summary>
    protected virtual float _skillPower(bool IsCradle)
    {
        var Level = _nowSkillLevel;
        var powerMultiplier = SkillPassiveSkillPowerRate();//スキルパワーに乗算する値
        if(IsCradle)
        {
            Level = _cradleSkillLevel;//ゆりかごならゆりかご用計算されたスキルレベルが
        }
        // スキルレベルが有限範囲ならそれを返す
        if (FixedSkillLevelData.Count > Level)
        {
            return FixedSkillLevelData[Level].SkillPower * powerMultiplier;
        }
        else
        {// そうでないなら有限最終以降と無限単位の加算
            // 有限リストの最終値と無限単位に以降のスキルレベルを乗算した物を加算
            // 有限リストの最終値を基礎値にする
            var baseSkillPower = FixedSkillLevelData[FixedSkillLevelData.Count - 1].SkillPower;

            // 有限リストの超過分、無限単位にどの程度かけるかの数
            var infiniteLevelMultiplier = Level - (FixedSkillLevelData.Count - 1);

            // 基礎値に無限単位に超過分を掛けたものを加算して返す
            return (baseSkillPower + _infiniteSkillPowerUnit * infiniteLevelMultiplier) * powerMultiplier;

            // 有限リストがないってことはない。必ず一つは設定されてるはずだしね。
        }
    }
    /// <summary>
    /// スキルのパワー
    /// </summary>
    public float GetSkillPower(bool IsCradle = false) => _skillPower(IsCradle) * (1.0f - MentalDamageRatio);
    /// <summary>
    /// 精神HPへのスキルのパワー
    /// </summary>
    public float GetSkillPowerForMental(bool IsCradle = false) => _skillPower(IsCradle) * MentalDamageRatio;
    /// <summary>
    /// スキルパッシブ由来のスキルパワー百分率
    /// </summary>
    public float SkillPassiveSkillPowerRate()
    {
        //初期値を1にして、すべてのかかってるスキルパッシブのSkillPowerRateを掛ける
        var rate = ReactiveSkillPassiveList.Aggregate(1.0f, (acc, pas) => acc * (1.0f + pas.SkillPowerRate));
        return rate;
    }

    //  ==============================================================================================================================
    //                                              スキルパワーの計算
    //  ==============================================================================================================================

    /// <summary>
    /// スキルパワーの計算
    /// </summary>
    public virtual float SkillPowerCalc(bool IsCradle = false)
    {
        var pwr = GetSkillPower(IsCradle);//基礎パワー




        return pwr;
    }
    public virtual float SkillPowerForMentalCalc(bool IsCradle = false)
    {
        var pwr = GetSkillPowerForMental(IsCradle);//基礎パワー


        return pwr;
    }


}
