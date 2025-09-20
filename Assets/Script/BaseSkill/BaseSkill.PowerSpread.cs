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
    [Header("通常分散割合の設定値")]
    /// <summary>
    /// 通常の分散割合
    /// </summary>
    [SerializeField]
    float[] _powerSpread;
    /// <summary>
    /// スキルの範囲効果における各割合　最大で6の長さまで使うと思う
    /// 有限リストのオプション値で指定されてるのならそれを返す
    /// </summary>
    public float[] PowerSpread
    {
        get
        {
            //スキルレベルが有限範囲なら
            if(FixedSkillLevelData.Count > _nowSkillLevel)
            {
                //nullでないならあるので返す
                // 現在のレベルから逆向きに検索して最初に有効な値を見つける
                for(int i = _nowSkillLevel; i >= 0; i--) 
                {
                    if(FixedSkillLevelData[i].OptionPowerSpread != null && 
                    FixedSkillLevelData[i].OptionPowerSpread.Length > 0)
                    {
                        return FixedSkillLevelData[i].OptionPowerSpread;
                    }
                }
            }
            //当然有限リストは絶対に存在するので、
            //有限範囲以降なら、その最終値でオプションで指定されてるならそれを返す
            //有限リスト外の場合、最後の要素から逆向きに検索
            for(int i = FixedSkillLevelData.Count - 1; i >= 0; i--) 
            {
                if(FixedSkillLevelData[i].OptionPowerSpread != null && 
                FixedSkillLevelData[i].OptionPowerSpread.Length > 0)
                {
                    return FixedSkillLevelData[i].OptionPowerSpread;
                }
            }

            //そうでないなら設定値を返す
            return _powerSpread;
        }
    }
  
}
