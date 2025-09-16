using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;
using NRandom.Linq;


//BMでの行動管理系
public abstract partial class BaseStates    
{
    /// <summary>
    /// bm初回の先制攻撃者かどうか
    /// </summary>
    [NonSerialized]
    public bool BattleFirstSurpriseAttacker = false;

    /// <summary>
    /// 前回ターンが前のめりかの記録
    /// </summary>
    [NonSerialized]
    public bool _tempVanguard;
    /// <summary>
    /// 前回ターンに生きてたかどうかの比較のため
    /// </summary>
    [NonSerialized]
    public bool _tempLive;


    /// <summary>
    /// パッシブの中に一つでもIsCantACTがtrueのものがあればtrue
    /// 行動できません。　が、CanCancelのパッシブがあるのならCanCancel限定のスキル行動画面へ移動する。
    /// </summary>
    public bool IsFreezeByPassives => _passiveList.Any(p => p.IsCantACT);
    /// <summary>
    /// 動けなくなるが、中断可能なパッシブを一つでも持っているのなら
    /// </summary>
    public bool HasCanCancelCantACTPassive => _passiveList.Any(p => p.CanCancel && p.IsCantACT);
    
    /// <summary>
    /// SkillACT内(damage関数やReactionSkill)などで行動をキャンセルされたかどうか。
    /// TODO:もしかしたら非同期的なあれでこういうめんどくさいbool消せるかも
    /// </summary>
    [NonSerialized]
    public bool IsActiveCancelInSkillACT;

    /// <summary>
    /// 逃げる選択肢を押したかどうか
    /// </summary>
    [NonSerialized]
    public bool SelectedEscape;
    /// <summary>
    /// 実行するスキルがRandomRangeの計算が用いられたかどうか。
    /// </summary>
    [NonSerialized]
    public bool SkillCalculatedRandomRange;

    //  ==============================================================================================================================
    //                                             リカバリーターン
    //  ==============================================================================================================================

    /// <summary>
    /// リカバリターン/再行動クールタイムの「基礎」設定値。
    /// </summary>
    [SerializeField]
    private int maxRecoveryTurn;
    /// <summary>
    /// パッシブ由来のリカバリターン/再行動クールタイムの設定値。
    /// </summary>
    int PassivesMaxRecoveryTurn()
    {
        var result = 0;
        foreach (var passive in _passiveList)
        {
            result += passive.MaxRecoveryTurnModifier();//全て加算する。
        }
        return result;
    }
    /// <summary>
    ///     リカバリターン/再行動クールタイムの設定値。
    /// </summary>
    public int MaxRecoveryTurn
    {
        get
        {
            return maxRecoveryTurn + PassivesMaxRecoveryTurn();//パッシブによる補正値を加算
        }
    }

    /// <summary>
    ///     recovelyTurnの基礎バッキングフィールド
    /// </summary>
    private int recoveryTurn;

    /// <summary>
    /// skillDidWaitCountなどで一時的に通常recovelyTurnに追加される一時的に再行動クールタイム/追加硬直値
    /// </summary>
    private int _tmpTurnsToAdd;
    /// <summary>
    /// 一時的に必要ターン数から引く短縮ターン
    /// </summary>
    private int _tmpTurnsToMinus;
    /// <summary>
    /// 一時保存用のリカバリターン判別用の前ターン変数
    /// </summary>
    private int _tmp_EncountTurn;
    /// <summary>
    /// recovelyTurnTmpMinusという行動クールタイムが一時的に短縮
    /// </summary>
    public void RecovelyTurnTmpMinus(int MinusTurn)
    {
        _tmpTurnsToMinus += MinusTurn;
    }
    /// <summary>
    /// recovelyCountという行動クールタイムに一時的に値を加える
    /// </summary>
    public void RecovelyCountTmpAdd(int addTurn)
    {
        if(!IsActiveCancelInSkillACT)//行動がキャンセルされていないなら
        {
            _tmpTurnsToAdd += addTurn;
        }
    }
    /// <summary>
    /// このキャラが戦場にて再行動を取れるかどうかと時間を唱える関数
    /// </summary>
    public bool RecovelyBattleField(int nowTurn)
    {
        var difference = Math.Abs(nowTurn - _tmp_EncountTurn);//前ターンと今回のターンの差異から経過ターン
        //もし前のめりならば、二倍で進む
        if(manager.IsVanguard(this))
        {
            difference *= 2;
        }

        _tmp_EncountTurn = nowTurn;//今回のターンを次回の差異計算のために一時保存
        if ((recoveryTurn += difference) >= MaxRecoveryTurn + _tmpTurnsToAdd -_tmpTurnsToMinus)//累計ターン経過が最大値を超えたら
        {
            //ここでrecovelyTurnを初期化すると　リストで一括処理した時にカウントアップだけじゃなくて、
            //選ばれたことになっちゃうから、0に初期化する部分はBattleManagerで選ばれた時に処理する。
            return true;
        }
        return false;
    }
    /// <summary>
    /// 戦場へ参戦回復出来るまでのカウントスタート
    /// </summary>
    public void RecovelyWaitStart()
    {
        recoveryTurn = 0;
        RemoveRecovelyTmpAddTurn();//一時追加ターンをリセット
        RemoveRecovelyTmpMinusTurn();//一時短縮ターンをリセット
    }
    /// <summary>
    /// キャラに設定された追加硬直値をリセットする
    /// </summary>
    public void RemoveRecovelyTmpAddTurn()
    {
        _tmpTurnsToAdd = 0;
    }
    /// <summary>
    /// キャラに設定された再行動短縮ターンをリセットする
    /// </summary>
    public void RemoveRecovelyTmpMinusTurn()
    {
        _tmpTurnsToMinus = 0;
    }
    /// <summary>
    /// 戦場へ参戦回復が出来るようにする
    /// </summary>
    public void RecovelyOK()
    {
        recoveryTurn = MaxRecoveryTurn;
    }

}