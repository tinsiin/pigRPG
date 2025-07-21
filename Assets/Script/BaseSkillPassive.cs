using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEditor.Rendering;
using Unity.VisualScripting;
using static CommonCalc;
using RandomExtensions;

/// <summary>
/// スキルパッシブはどうやって付与されるスキルを選ぶのかの形式
/// </summary>
public enum SkillPassiveTargetSelection
{
    /// <summary> 
    /// 直接選択式(UI,標準ロジックで渡される値)
    /// </summary>
    Select,
    /// <summary> 
    /// 付与するスキルをランダムで選ぶ、完全ランダム
    ///  </summary>
    Random,
    /// <summary> 
    /// 特定のキャラとスキルに対応するタイプ
    ///  </summary>
    Reaction
}
/// <summary>
/// スキルパッシブが反応するキャラとスキルを保持するクラス
/// </summary>
public class SkillPassiveReactionCharaAndSkill
{
    public string CharaName;
    public string SkillName;
}


/// <summary>
///     スキルのパッシブ
/// </summary>
[Serializable]
public class BaseSkillPassive
{
    public string Name;
    BaseSkill OwnerSkill;
    /// <summary>
    /// 持続ターン数 -1なら消えない
    /// </summary>
    public int DurationTurn;
    /// <summary>
    /// 持続歩数ターン　-1なら消えない
    /// </summary>
    public int DurationWalkTurn;
    /// <summary>
    /// ターン数が0なら消える
    /// この関数はBaseStates、スキルの所属するユーザーから呼ぶ
    /// ただ消すのは個々に登録されているスキルの関数を呼び出してパッシブリストを消す形
    /// [パッシブから消される際、パッシブ側から操作するので、ここはその処理に関係ない]
    /// </summary>
    public void Update()
    {
        //0以上ならターン経過で消える。
        if(DurationTurn > 0)
        {
            DurationTurn--;
            if(DurationTurn <= 0)
            {
                //userの全スキルパッシブ からこのパッシブをRemove
                OwnerSkill.RemoveSkillPassive(this);
            }
        }
    }
    /// <summary>
    /// 歩行でターン経過しパッシブが消えるなら
    /// </summary>
    public void UpdateWalk()
    {
        DurationWalkTurn--;
        if(DurationWalkTurn <= 0)
        {
            //userの全スキルパッシブ からこのパッシブをRemove
            OwnerSkill.RemoveSkillPassive(this);
        }
    }


    /// <summary>
    /// スキルパワーを百分率で増減させる
    /// </summary>
    public float SkillPowerRate = 1.0f;

    //スキルレベルが操作されると何か絡み合いすぎるし、ゆりかごシステムとも嚙み合って変になるし、
    // パッシブでレベルが動くのはおかしいからパワーにダイレクト影響が普通だと思う

    /// <summary>
    /// スキルロック
    /// </summary>
    public bool IsLock;

    /// <summary>
    /// 悪いパッシブかどうか
    /// </summary>
    public bool IsBad;


    public BaseSkillPassive DeepCopy()
    {
        return new BaseSkillPassive()
        {
            DurationTurn = DurationTurn,
            DurationWalkTurn = DurationWalkTurn,
            SkillPowerRate = SkillPowerRate,
            IsLock = IsLock,
            OwnerSkill = OwnerSkill
        };
    }

}
