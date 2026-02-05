using R3;
using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public partial class BaseSkill
{
    //  ==============================================================================================================================
    //                                             連続攻撃用関数、フィールド
    //  ==============================================================================================================================

    [Header("SkillConsecutiveTypeがRandomPercentなら、ここで設定した確率で、連続攻撃が続く。")]
    [SerializeField]
    private float _RandomConsecutivePer;//連続実行の確率判定のパーセント
    /// <summary>
    /// オーバライド可能な攻撃回数
    /// </summary>
    public virtual int ATKCount
    {
        get { 
            if(HasConsecutiveType(SkillConsecutiveType.Stockpile))
            {
                return _nowStockCount;//stockpileの場合は現在ストック数を参照する
            }
            return DefaultAtkCount; 
            }

    }


    /// <summary>
    /// 現在の連続攻撃回数を参照して次回の連続攻撃があるかどうか
    /// </summary>
    public bool NextConsecutiveATK()
    {
        if(HasConsecutiveType(SkillConsecutiveType.FixedConsecutive))//回数による連続性質の場合
        {
            if (_atkCountUP >= ATKCount)//もし設定した値にカウントアップ値が達成してたら。
            {
                _atkCountUP = 0;//値初期化
                return false;//終わり
            }
            return true;//まだ達成してないから次の攻撃がある。

        }else if(HasConsecutiveType(SkillConsecutiveType.RandomPercentConsecutive))//確率によるなら
        {
            if (_atkCountUP >= ATKCount)//もし設定した値にカウントアップ値が達成してたら。
            {
                _atkCountUP = 0;//値初期化
                return false;//終わり
            }

            if(RandomSource.NextFloat(1)<_RandomConsecutivePer)//確率があったら、
            {
                
                return true;
            }
            return false;
        }

        return false;
    }
    /// <summary>
    /// 現在が連続攻撃中(二回目以降)かどうか
    /// </summary>
    public bool NowConsecutiveATKFromTheSecondTimeOnward()
    {
        if (_atkCountUP > 0)
        {
            return true;
        }
        return false;
    }
    /// <summary>
    /// 単回攻撃かどうか(連続攻撃性がないわけじゃない。(連続攻撃のメモ参照))
    /// </summary>
    bool IsSingleHitAtk
    {
        get
        {
            return _a_moveset.Count <= 0;//movesetのリストが空なら単体攻撃(二回目以降が設定されていないので)
        }
    }
    //  ==============================================================================================================================
    //                                             連続攻撃用カウント管理
    //  ==============================================================================================================================

    private int _atkCountUP;//連続攻撃中のインデックス的回数

    /// <summary>
    /// 現在の連続攻撃回数のindex
    /// </summary>
    public int ATKCountUP => _atkCountUP;


    /// <summary>
    /// 攻撃回数カウントをリセットする
    /// </summary>
    public void ResetAtkCountUp()
    {
        _atkCountUP = 0;
    }

    /// <summary>
    /// 連続攻撃の値を増やす
    /// </summary>
    /// <returns></returns>
    public virtual int ConsecutiveFixedATKCountUP()
    {
        _atkCountUP++;
        Debug.Log(SkillName +"_atkCountUP++" + _atkCountUP);

        return _atkCountUP;
    }
    //  ==============================================================================================================================
    //                                             ストック
    //                  stockpile用
    //                  最大値はランダム確率の連続攻撃と同じように、ATKCountを参照する。
    //  ==============================================================================================================================
    private int _nowStockCount;//現在のストック数
    /// <summary>
    /// デフォルトのストック数
    /// </summary>
    [SerializeField]
    private int _defaultStockCount = 1;
    ///<summary> ストックデフォルト値。DefaultAtkCount を超えないように調整された値を返す</summary> ///
    int DefaultStockCount => _defaultStockCount > DefaultAtkCount ? DefaultAtkCount : _defaultStockCount;

    [SerializeField]
    private int _stockPower = 1;//ストック単位
    /// <summary>
    /// ストック単位を手に入れる
    /// </summary>
    protected virtual int GetStcokPower()
    {
        return _stockPower;
    }
    [SerializeField]
    private int _stockForgetPower = 1;//ストック忘れ単位
    /// <summary>
    /// ストック忘れ単位を手に入れる
    /// </summary>
    protected virtual int GetStcokForgetPower()
    {
        return _stockForgetPower;
    }

 
    /// <summary>
    /// ストック数をデフォルトにリセット
    /// </summary>
    public void ResetStock()
    {
        _nowStockCount = DefaultStockCount;
    }
    /// <summary>
    /// 攻撃回数をストック
    /// </summary>
    public void ATKCountStock()
    {

        _nowStockCount += GetStcokPower();
        if(_nowStockCount > DefaultAtkCount)_nowStockCount = DefaultAtkCount;//想定される最大値を超えないようにする
        
    }
    /// <summary>
    /// ストックを忘れる
    /// </summary>
    public void ForgetStock()
    {
        _nowStockCount -= GetStcokForgetPower();
        if(_nowStockCount < DefaultStockCount)_nowStockCount = DefaultStockCount;//ストック数はデフォルト値を下回らないようにする
    }
    /// <summary>
    /// ストックが満杯かどうか
    /// </summary>
    public bool IsFullStock()
    {
        return _nowStockCount >= DefaultAtkCount;//最大値以上ならばストックが満杯とする
    }

    /// <summary>
    /// 通常の攻撃回数
    /// </summary>
    int DefaultAtkCount =>  1 + NowMoveSetState.States.Count;


}

/// <summary>
/// スキルの実行順序的性質
/// </summary>
[Flags]
public enum SkillConsecutiveType
{
    /// <summary>
    /// 毎コマンドZoneTraitに従って対象者の選択可能　ランダムだったらそれに従う感じ
    /// </summary>
    CanOprate = 1 << 0,

    /// <summary>CanOprateの対局的なもの、つまり最初しかZoneTraitでできることを選べない
    /// 要は普通の連続攻撃の挙動です。
    CantOprate = 1 << 1, 

    /// <summary>
    /// ターンをまたいだ連続的攻撃　連続攻撃回数分だけ単体攻撃が無理やり進む感じ
    /// </summary>
    FreezeConsecutive = 1 << 2,

    /// <summary>
    /// 同一ターンで連続攻撃が行われるかどうか
    /// </summary>
    SameTurnConsecutive = 1 << 3,

    /// <summary>
    /// ランダムな百分率でスキル実行が連続されるかどうか
    /// </summary>
    RandomPercentConsecutive = 1 << 4, 
    /// <summary>
    /// _atkCountの値に応じて連続攻撃が行われるかどうか
    /// </summary>
    FixedConsecutive = 1 << 5,

    /// <summary>
    /// スキル保存性質　
    /// **意図的に実行とは別に攻撃保存を選べて**、
    ///その攻撃保存を**選んだ分だけ連続攻撃回数として発動**
    ///Randomな場合はパーセント補正が変わる？
    /// </summary>
    Stockpile = 1 << 6,

}
