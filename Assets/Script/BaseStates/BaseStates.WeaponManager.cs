using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;
using static TenDayAbilityPosition;

//武器管理
public abstract partial class BaseStates    
{
    /// <summary>
    /// 装備中の武器
    /// </summary>
    [NonSerialized]
    public BaseWeapon NowUseWeapon;
    /// <summary>
    /// 初期所持してる武器のID
    /// </summary>
    public int InitWeaponID;
    /// <summary>
    /// 武器装備、武器から移る戦闘規格の変化
    /// </summary>
    public void ApplyWeapon(int ID)
    {
        if (WeaponManager.Instance == null)
        {
            Debug.LogError("WeaponManager.Instance is null");
            return;
        }
        NowUseWeapon = WeaponManager.Instance.GetAtID(ID);//武器を変更'
        NowBattleProtocol = NowUseWeapon.protocol;//戦闘規格の変更
    }
    /// <summary>
    /// 今のキャラの戦闘規格
    /// </summary>
    [NonSerialized]
    public BattleProtocol NowBattleProtocol;
   
}
/// <summary>
/// 武器依存の戦闘規格
/// </summary>
public enum BattleProtocol
{
    /// <summary>地味</summary>
    LowKey,
    /// <summary>トライキー</summary>
    Tricky,
    /// <summary>派手</summary>
    Showey,
    /// <summary>
    /// この戦闘規格には狙い流れ(AimStyle)がないため、には防ぎ方(AimStyleごとに対応される防御排他ステ)もなく、追加攻撃力(戦闘規格による排他ステ)もない
    /// </summary>
    none
}
