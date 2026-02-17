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
    /// スキルの範囲効果における各割合　最大で6の長さまで使うと思う
    /// </summary>
    public float[] PowerSpread => FixedSkillLevelData[_levelIndex].PowerSpread;

}
