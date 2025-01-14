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
    public float Regen = 0f;
    //実際のリジェネ処理はBaseStates?

    //追加HP自体の物理属性への耐性
    public float HeavyResistance = 1.0f;
    public float voltenResistance = 1.0f;
    public float DishSmackRsistance = 1.0f;

    /// <summary>
    /// バリアの耐性をどう扱うか（A/B/Cを切り替え）
    /// </summary>
    public BarrierResistanceMode ResistMode ;

    /// <summary>
    /// ダメージが層を通過する  与えられたダメージは通過して軽減され返る
    /// </summary>
    public float PenetrateLayer(float dmg, PhysicalProperty impactProperty)
    {
        // 1) 物理属性に応じた耐性率を取得
        float resistRate = 1.0f;
        switch (impactProperty)
        {
            case PhysicalProperty.heavy:
                resistRate = HeavyResistance;
                break;
            case PhysicalProperty.volten:
                resistRate = voltenResistance;
                break;
            case PhysicalProperty.dishSmack:
                resistRate = DishSmackRsistance;
                break;
        }

        // 2) 軽減後の実ダメージ
        float dmgAfter = dmg * resistRate;

        // 3) レイヤーHPを削る
        float leftover = LayerHP - dmgAfter; // leftover "HP" => もしマイナスなら破壊
        if (leftover <= 0f)
        {
            // 破壊された
            float overkill = -(leftover); // -negative => positive
            var tmpHP = LayerHP;//仕組みC用に今回受ける時のLayerHPを保存。
            LayerHP = 0f; // 自分のHPはゼロ

            // 仕組みの違い
            switch (ResistMode)
            {
                case BarrierResistanceMode.A_SimpleNoReturn:
                    // Aは一度軽減した分は戻さない: overkill をそのまま次へ
                    return overkill;

                case BarrierResistanceMode.B_RestoreWhenBreak:
                    // Bは「軽減後ダメージ」分を元に戻す => leftover を "÷ resistRate" で拡大
                    // ここで overkill は "dmgAfter - LayerHP" の結果
                    // → 仕組みB: leftoverDamage = overkill / resistRate
                    float restored = overkill / resistRate;
                    return restored;

                case BarrierResistanceMode.C_IgnoreWhenBreak:
                    // Cは元攻撃 - 現在のLayerHP
                    // leftover(= overkill)を無視し、
                    // "dmg - tmpHP(LayerHP)" などの再計算
                    float cValue = dmg - tmpHP;
                    if (cValue < 0) cValue = 0;
                    return cValue;
            }
        }
        else
        {
            // バリアで耐えた（破壊されなかった）
            LayerHP = leftover;
            return 0f; // 余剰ダメージなし
        }

        // fallback
        return 0f;
    }

}
public enum BarrierResistanceMode
{
    /// <summary>仕組みA: 一度軽減した分は復活させない（今のままの通過）</summary>
    A_SimpleNoReturn,

    /// <summary>仕組みB: バリア破壊時に耐性を"÷耐性率"で撤回して、残ダメージを復活</summary>
    B_RestoreWhenBreak,

    /// <summary>仕組みC: バリア破壊時に"耐性自体なかった"として計算(例: (元攻撃-HP) など)</summary>
    C_IgnoreWhenBreak,
}

