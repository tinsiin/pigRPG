using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BaseSkill
{
    /// <summary>
    /// スキルの精神属性
    /// </summary>
    public SpiritualProperty SkillSpiritual {  get; private set; }
    /// <summary>
    /// スキルの物理属性
    /// </summary>
    public PhysicalProperty SkillPhysical { get; private set; }

}
