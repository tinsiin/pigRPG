public enum SkillType
{
    Attack,Heal
}

public class BaseSkill
{
    /// <summary>
    ///     スキルの精神属性
    /// </summary>
    public SpiritualProperty SkillSpiritual { get; }

    /// <summary>
    ///     スキルの物理属性
    /// </summary>
    public PhysicalProperty SkillPhysical { get; }


    /// <summary>
    /// TLOAかどうか
    /// </summary>
    public bool IsTLOA;

    /// <summary>
    /// スキルのパワー
    /// </summary>
    public int SkillPower;

    //防御無視率
    public float DEFATK;

    public SkillType WhatSkill;
}