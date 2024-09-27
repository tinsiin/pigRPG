using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using R3;

/// <summary>
/// 基礎状態の抽象クラス
/// </summary>
public　abstract class BasePassive 
{
    /// <summary>
    /// PassivePowerの設定値
    /// </summary>
    public int MaxPassivePower;

    /// <summary>
    /// この値はパッシブの"重ね掛け" →&lt;思え鳥4&gt;
    /// </summary>
    public int PassivePower { get; private set; }

    /// <summary>
    /// パッシブを重ね掛けする。
    /// </summary>
    /// <param name="addpoint"></param>
    public void AddPassivePower(int addpoint)
    {
        PassivePower += addpoint;
        if(PassivePower > MaxPassivePower)PassivePower = MaxPassivePower;//設定値を超えたら設定値にする
    }

    /// <summary>
    /// 適合する種別のリスト。　種別は一人一つなので、判断基準はこれだけでOK
    /// </summary>
    public List<CharacterType> TypeOkList;

    //適合するキャラ属性(精神属性)のリスト　
    public List<SpiritualProperty> CharaPropertyOKList;

    /// <summary>
    /// 歩行時効果　basestatesでapplypassiveで購読する
    /// </summary>
    public abstract void WalkEffect();
    
    /// <summary>
    /// 戦闘時効果　basestatesでapplypassiveで購読する
    /// </summary>
    public abstract void BattleEffect();
}
