using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[Flags]
public enum SkillType
{
    Attack =1 << 0,
    Heal = 1 << 1,
    addPassive = 1 << 2,
    RemovePassive = 1 << 3,
        //攻撃、回復、状態異常付与
}
[Serializable]
public class BaseSkill
{
    /// <summary>
    ///     スキルの精神属性
    /// </summary>
    public SpiritualProperty SkillSpiritual { get; }

    /// <summary>
    /// スキル性質を持ってるかどうか
    /// </summary>
    public bool HasType(SkillType skill)
    {
        return (WhatSkill & skill) == skill;
    }


    /// <summary>
    ///     スキルの物理属性
    /// </summary>
    public PhysicalProperty SkillPhysical { get; }

    private BaseStates Doer;//行使者

    [SerializeField]
    private string _name;
 
    private int _doConsecutiveCount;//BattleManager単位で"連続"で使われた回数。　
    private int _doCount;//BattleManager単位で行使した回数

    private int _triggerCount;//発動への－カウント　このカウント分連続でやらないと発動しなかったりする　重要なのは連続でやらなくても　一気にまたゼロからになるかはスキル次第
    private int _triggerCountMax;//発動への－カウント　の指標
    private int _atkCountMax;//攻撃回数
    private int _atkCountDown;//実際にカウントダウンする攻撃回数用変数
    private int _deltaTurn;//前回のスキル行使から経った戦闘ターン

    public bool CanCancel = true;//triggerCountが0以上の複数ターン実行が必要なスキルの場合、複数ターンに跨る実行中に中断出来るかどうか。

    public int SKillDidWaitCount;//スキルを行使した後の硬直時間。 Doer、行使者のRecovelyTurnに一時的に加算される？
    //複数実行するとどう扱われる？


    public string SkillName
    {
        get { return _name; }
    }


    /// <summary>
    /// 攻撃した回数
    /// </summary>
    public  int DoCount
    {
        get { return _doCount; }
        set { _doCount = value; }
    }

    /// <summary>
    /// スキル実行に必要なカウント　-1で実行される。
    /// </summary>
    public int TrigerCount()
    {
        if (_triggerCountMax > 0)//1回以上設定されてたら
        {
            _triggerCount--;
            return _triggerCount;
        }

        //発動カウントが0に設定されている場合、そのまま実行される。
        return -1;
    }

    /// <summary>
    /// 選ばれなかった時の発動カウントが戻っちゃう処理
    /// </summary>
    public virtual void ReturnTrigger()
    {
        _triggerCount = _triggerCountMax;//基本的に一回でもやんなかったらすぐ戻る感じ
    }

    /// <summary>
    /// オーバライド可能な攻撃回数
    /// </summary>
    public virtual int ATKCountMax
    {
        get { return _atkCountMax; }
        set { _atkCountMax = value; }

    }
    /// <summary>
    /// オーバーライド可能な攻撃回数のカウントダウンする変数
    /// </summary>
    public virtual int ATKCountDown
    {
        get => _atkCountDown;
    }

    /// <summary>
    /// オーバライド可能な連続で使われた回数
    /// </summary>
    public virtual int DoConsecutiveCount
    {
        get { return _doConsecutiveCount; }
        set { _doConsecutiveCount = value; }

    }

    /// <summary>
    /// TLOAかどうか
    /// </summary>
    public bool IsTLOA;

    /// <summary>
    /// スキルのパワー
    /// </summary>
    public float SkillPower;

    /// <summary>
    /// スキルの命中補正
    /// </summary>
    public float SkillHitPer;

    /// <summary>
    /// 基本的にスキルのレベルは恒常的に上がらないが、戦闘内では一時的に上がったりするのかもしれない。
    /// </summary>
    public float SkillLevel;
    /// <summary>
    /// スキルの現在のライバハルの値 特定の範囲でレベルが上がる？
    /// </summary>
    public float SkillRivahal;

    /// <summary>
    /// TLOAと対決した際のライバハルの増え方の関数
    /// </summary>
    public void RivahalDream()
    {

    }

    //スキルパワーの計算
    public virtual float SkillPowerCalc()
    {
        return SkillPower;
    }

    /// <summary>
    /// スキル実行時に付与する状態異常とか
    /// </summary>
    public List<BasePassive> subEffects;

    //防御無視率
    public float DEFATK;

    public SkillType WhatSkill;
}