using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 追加されるHPの層　消費されると消える。
/// このクラス実体のイメージとしては、パッシブと生存条件等の意味合いで強く密接しているが、
/// あくまでパッシブの中の一つの機能の形に過ぎず、また、パッシブがキャラに与える影響はこの追加HP層には与えられないというイメージ。
/// </summary>
public class BaseVitalLayer
{
    /// <summary>
    /// マスターリストから抜き出すための判別用ID
    /// </summary>
    public int id;

    /// <summary>
    /// 積み重なる際の優先順位
    /// 数が低い方が上　同じなら先着順に積み重なる。
    /// </summary>
    public int Priority;

    [SerializeField]
    private float _layhp;
    /// <summary>
    /// レイヤーHP
    /// </summary>
    public float LayerHP
    {
        get { return _layhp; }
        set
        {
            if (value > MaxLayerHP)//最大値を超えないようにする
            {
                _layhp = MaxLayerHP;
            }
            else _layhp = value;
        }
    }
    [SerializeField]
    private float _maxLayhp;
    public float MaxLayerHP => _maxLayhp;

    /// <summary>
    /// HPを最大値まで再補充
    /// </summary>
    public void ReplenishHP()
    {
        LayerHP = 999999;
    }


    /// <summary>
    /// 戦闘の中断によって消えるかどうか
    /// </summary>
    public bool IsBattleEndRemove;

    /// <summary>
    /// この追加HPが勝手にリジェネするかどうか。
    /// 追加HPはキャラクターのパッシブとは独立した物である。
    /// だから、基本HPに影響は与えないしキャラ自体に掛かっているパッシブも追加HPに影響は与えない。
    /// </summary>
    public float Regen=0f;
    //実際のリジェネ処理はBaseStates?

    //追加HP自体の物理属性への耐性
    public float HeavyResistance;
    public float voltenResistance;
    public float DishSmackRsistance;

}
