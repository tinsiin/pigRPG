using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
///     基礎状態の抽象クラス
/// </summary>
[Serializable]
public class BasePassive
{
    /// <summary>
    /// 適合するキャラ属性(精神属性)　
    /// </summary>
    public SpiritualProperty OkImpression;

    /// <summary>
    ///     適合する種別　
    /// </summary>
    public CharacterType OkType;


    /// <summary>
    ///     PassivePowerの設定値
    /// </summary>
    public int MaxPassivePower = 1;
    /// <summary>
    /// パッシブの名前
    /// </summary>
    public string PassiveName;
    public int ID;


    /// <summary>
    ///     この値はパッシブの"重ね掛け" →&lt;思え鳥4&gt;
    /// </summary>
    public int PassivePower { get; private set; }

    /// <summary>
    ///     パッシブを重ね掛けする。
    /// </summary>
    /// <param name="addpoint"></param>
    public void AddPassivePower(int addpoint)
    {
        PassivePower += addpoint;
        if (PassivePower > MaxPassivePower) PassivePower = MaxPassivePower; //設定値を超えたら設定値にする
    }

    /// <summary>
    ///     歩行時効果　basestatesでapplypassiveで購読する
    /// </summary>
    public virtual void WalkEffect()
    {

    }

    /// <summary>
    ///     戦闘時効果　basestatesでapplypassiveで購読する
    /// </summary>
    public virtual void BattleEffect()
    {

    }
    /// <summary>
    ///行動時効果
    /// </summary>
    public virtual void ACTEffect()
    {

    }

    //↑三つで操り切れない部分は、直接baseStatesでのforeachでpassiveListから探す関数でゴリ押しすればいい。
}