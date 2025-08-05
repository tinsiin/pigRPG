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
    public BaseSkill WeaponSkill;

    /// <summary>
    /// 武器の十日能力ボーナス
    /// </summary>
    public WeaponTenDayAbilityBonusData TenDayBonusData = new();

    /// <summary>
    /// 武器の装備に必要な能力値　十日能力値
    /// </summary>
    public SerializableDictionary<TenDayAbility,float> TenDayValues = new SerializableDictionary<TenDayAbility,float>();


    /// <summary>
    /// 刃物武器かどうか
    /// </summary>
    public bool IsBlade;


}
/// <summary>
/// 武器の十日能力ボーナス用クラス
/// </summary>
[Serializable]
public class WeaponTenDayAbilityBonusData
{
    [SerializeField]
    TenDayAbilityDictionary TLOAData = new();
    [SerializeField]
    TenDayAbilityDictionary MagicData = new();
    [SerializeField]
    TenDayAbilityDictionary BladeData = new();
    [SerializeField]
    TenDayAbilityDictionary NormalData = new();

    /// <summary>
    /// 各スキル限定ボーナスを取得するかを引数で指定しつつ、
    /// 武器の十日能力ボーナスの辞書を返す。
    /// </summary>
    public TenDayAbilityDictionary GetTenDayAbilityDictionary(bool isBlade = false, bool isMagic = false, bool isTLOA = false)
    {
        var result = NormalData;
        if (isBlade)
        {
            result += BladeData;
        }
        if (isMagic)
        {
            result += MagicData;
        }
        if (isTLOA)
        {
            result += TLOAData;
        }
        return result;
    }




}
