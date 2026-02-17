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
    /// 精神攻撃率　100だとSkillPower全てが精神HPの方に行くよ。
    /// </summary>
    public float MentalDamageRatio => FixedSkillLevelData[_levelIndex].MentalDamageRatio;
}
