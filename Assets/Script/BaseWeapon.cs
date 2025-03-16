using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BaseWeapon
{
    public int id;
    public string name;
    /// <summary>
    /// この武器で決まる戦闘規格
    /// </summary>
    public BattleProtocol protocol;
    /// <summary>
    /// 武器専用スキル
    /// </summary>
    public BaseSkill  WeaponSkill;

    /// <summary>
    /// 武器の装備に必要な能力値　十日能力値
    /// </summary>
    public SerializableDictionary<TenDayAbility,float> TenDayValues = new SerializableDictionary<TenDayAbility,float>();


    /// <summary>
    /// 刃物武器かどうか
    /// </summary>
    public bool IsBlade;


}
