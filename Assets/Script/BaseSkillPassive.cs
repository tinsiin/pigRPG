using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEditor.Rendering;
using Unity.VisualScripting;
using static CommonCalc;

/// <summary>
///     スキルのパッシブ
/// </summary>
public class BaseSkillPassive
{
    public BaseSkill OwnerSkill;
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
    public float SkillPowerRate;

    //スキルレベルが操作されると何か絡み合いすぎるし、ゆりかごシステムとも嚙み合って変になるし、
    // パッシブでレベルが動くのはおかしいからパワーにダイレクト影響が普通だと思う

    /// <summary>
    /// スキルロック
    /// </summary>
    public bool IsLock;

    /// <summary>
    /// 熟練度上下レート
    /// TLOAの思い入れでのメリットなど
    /// </summary>
    public float ProficiencyRate;

    /// <summary>
    /// HP固定加算率
    /// 主にTLOAの思い入れでのメリット用
    /// </summary>
    public float HPFixedValueEffect;

}
