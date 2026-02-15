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
    /// この武器で使用可能な戦闘規格（複数設定可能）
    /// 通常武器: 1つ、複数規格武器: 任意の数、フリーハンド: 全規格
    /// </summary>
    public List<BattleProtocol> protocols = new();

    /// <summary>
    /// 後方互換: 先頭の規格を返す（既存コード用）
    /// </summary>
    public BattleProtocol protocol => protocols.Count > 0 ? protocols[0] : BattleProtocol.none;

    /// <summary>
    /// 複数の戦闘規格を持つ武器かどうか
    /// </summary>
    public bool HasMultipleProtocols => protocols.Count > 1;
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

    /// <summary>
    /// 武器の物理属性（必ずいずれかの属性を持つ）
    /// </summary>
    public WeaponPhysicalProperty WeaponPhysical;

    /// <summary>
    /// 武器の物理属性をスキル用のPhysicalPropertyに変換
    /// </summary>
    public PhysicalProperty ToPhysicalProperty()
    {
        return WeaponPhysical switch
        {
            WeaponPhysicalProperty.heavy => PhysicalProperty.heavy,
            WeaponPhysicalProperty.volten => PhysicalProperty.volten,
            WeaponPhysicalProperty.dishSmack => PhysicalProperty.dishSmack,
            _ => PhysicalProperty.heavy
        };
    }

    /// <summary>
    /// 掛け合わせエントリ（非武器スキル×この武器 → 掛け合わせスキル）
    /// </summary>
    public List<WeaponCombinationEntry> CombinationEntries = new();

    /// <summary>
    /// 非武器スキルIDから掛け合わせスキルを取得。定義がなければnull。
    /// </summary>
    public BaseSkill GetCombinedSkill(int nonWeaponSkillId)
    {
        if (CombinationEntries == null || CombinationEntries.Count == 0)
            return null;
        var entry = CombinationEntries.Find(e => e.sourceSkillId == nonWeaponSkillId);
        return entry?.combinedSkill;
    }
}

/// <summary>
/// 掛け合わせエントリ: 非武器スキルとの組み合わせで発動するスキルを定義
/// </summary>
[Serializable]
public class WeaponCombinationEntry
{
    /// <summary>
    /// 掛け合わせ元の非武器スキルのID（AllySkill.ID）
    /// </summary>
    public int sourceSkillId;
    /// <summary>
    /// 掛け合わせ後に発動するスキル
    /// </summary>
    public BaseSkill combinedSkill;
}
/// <summary>
/// 武器専用の物理属性（noneなし、武器は必ずいずれかの属性を持つ）
/// </summary>
public enum WeaponPhysicalProperty
{
    /// <summary>暴断</summary>
    heavy,
    /// <summary>ヴォ流転</summary>
    volten,
    /// <summary>床ずれ</summary>
    dishSmack
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
