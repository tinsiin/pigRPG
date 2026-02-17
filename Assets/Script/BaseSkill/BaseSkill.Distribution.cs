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
    /// スキルの分散性質
    /// </summary>
    public AttackDistributionType DistributionType => FixedSkillLevelData[_levelIndex].DistributionType;

    /// <summary>
    /// 威力の範囲が複数に選択または分岐可能時の割合差分
    /// </summary>
    public SerializableDictionary<SkillZoneTrait, float> PowerRangePercentageDictionary
        => FixedSkillLevelData[_levelIndex].PowerRangePercentageDictionary;
    /// <summary>
    /// 命中率のステータスに直接かかるスキルの範囲意志による威力補正
    /// </summary>
    public SerializableDictionary<SkillZoneTrait, float> HitRangePercentageDictionary
        => FixedSkillLevelData[_levelIndex].HitRangePercentageDictionary;

}
/// <summary>
/// "範囲"攻撃の分散性質を表す列挙体 予め設定された3～６つの割合をどう扱うかの指定
/// powerSpreadの配列のサイズで分散するかどうかを判定する。(つまりNoneみたいな値はない。)
/// </summary>
public enum AttackDistributionType
{
    /// <summary>
    /// 完全ランダムでいる分だけ割り当てる
    /// </summary>
    Random,
    /// <summary>
    /// 前のめり状態(敵味方問わず)のキャラが最初に回されるランダム分散
    /// 放射系統のビーム的な
    /// </summary>
    Beam,
    /// <summary>
    /// 2までの値だけを利用して、前衛と後衛への割合。
    /// 前衛が以内なら後衛単位　おそらく2が使われる
    /// </summary>
    Explosion,
    /// <summary>
    /// 投げる。　つまり敵味方問わず前のめり状態のが一番後ろに回される
    /// </summary>
    Throw,
}
