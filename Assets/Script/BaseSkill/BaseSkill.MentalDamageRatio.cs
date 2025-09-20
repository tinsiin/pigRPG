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
    [Header("通常の精神攻撃率")]
    /// <summary>
    /// 通常の精神攻撃率
    /// </summary>
    [SerializeField]
    float _mentalDamageRatio;
    /// <summary>
    /// 精神攻撃率　100だとSkillPower全てが精神HPの方に行くよ。
    /// 有限リストのオプション値で指定されてるのならそれを返す
    /// </summary>
    public float MentalDamageRatio
    {
        get
        {
            //スキルレベルが有限範囲なら
            if(FixedSkillLevelData.Count > _nowSkillLevel)
            {
                //-1でないならあるので返す
                // 現在のレベルから逆向きに検索して最初に有効な値を見つける
                for(int i = _nowSkillLevel; i >= 0; i--) 
                {
                    if(FixedSkillLevelData[i].OptionMentalDamageRatio != -1)
                    {
                        return FixedSkillLevelData[i].OptionMentalDamageRatio;
                    }
                }
            }
            //当然有限リストは絶対に存在するので、
            //有限範囲以降なら、その最終値でオプションで指定されてるならそれを返す
            //有限リスト外の場合、最後の要素から逆向きに検索
            for(int i = FixedSkillLevelData.Count - 1; i >= 0; i--) 
            {
                if(FixedSkillLevelData[i].OptionMentalDamageRatio != -1)
                {
                    return FixedSkillLevelData[i].OptionMentalDamageRatio;
                }
            }

            //そうでないなら設定値を返す
            return _mentalDamageRatio;
        }
    }
}