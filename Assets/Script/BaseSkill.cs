using R3;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

/// <summary>
/// スキルの実行性質
/// </summary>
[Flags]
public enum SkillType
{
    Attack =1 << 0,
    Heal = 1 << 1,
    addPassive = 1 << 2,
    RemovePassive = 1 << 3,
    DeathHeal = 1 << 4,
        //攻撃、回復、状態異常付与、死回復
}
/// <summary>
/// スキル範囲の性質
/// </summary>
[Flags]
public enum SkillZoneTrait
{
    /// <summary>
    /// 選択可能な単体対象
    /// </summary>
    CanPerfectSelectSingleTarget = 1 << 0,
    /// <summary>
    /// 前のめりか後衛(内ランダム)で選択可能な単体対象
    /// </summary>
    CanSelectSingleTarget = 1 << 1,
    /// <summary>
    /// ランダムの単体対象
    /// </summary>
    RandomSingleTarget = 1 << 2,

    /// <summary>
    /// 前のめりか後衛で選択可能な範囲対象
    /// </summary>
    CanSelectMultiTarget = 1 << 3,
    /// <summary>
    /// ランダムで選ばれる前のめりか後衛の範囲対象
    /// </summary>
    RandomSelectMultiTarget = 1 << 4,
    /// <summary>
    /// ランダムな範囲、つまり一人か二人か三人がランダム
    /// </summary>
    RandomMultiTarget = 1 << 5,
    /// <summary>
    /// 全範囲攻撃
    /// </summary>
    AllTarget = 1 << 6,

    /// <summary>
    /// 範囲ランダム　全シチュエーション
    /// </summary>
    RandomRangeRandomTargetALLSituation = 1 << 7,
    /// <summary>
    /// 範囲ランダム   前のめり,後衛単位or単体ランダム
    /// </summary>
    RandomRangeRandomTargetMultiOrSingle = 1 << 8,
    /// <summary>
    /// 範囲ランダム　全体or単体ランダム
    /// </summary>
    RandomRangeRandomTargetALLorSingle = 1 << 9,
    /// <summary>
    /// 範囲ランダム　全体or前のめり,後衛単位
    /// </summary>
    RandomRangeRandomTargetALLorMulti = 1 << 10,

    /// <summary>
    /// RandomRangeを省いた要素のうちが選択可能かどうか
    /// </summary>
    CanSelectRange = 1 << 11,
    /// <summary>
    /// 死を選べるまたは対象選別の範囲に入るかどうか
    /// </summary>
    CanSelectDeath = 1 << 12,
    /// <summary>
    /// 対立関係のグループ相手だけではなく、自陣をも選べるかどうか
    /// </summary>
    CanSelectAlly = 1 << 13,
    /// <summary>
    /// 基本的には前のめりしか選べない。 前のめりがいない場合
    ///ランダムな事故が起きる感じで、
    ///その事故は意志により選択は不可能
    ///canSelectAllyは無効
    /// </summary>
    ControlByThisSituation = 1 << 14,

}
/// <summary>
/// スキルの実行順序的性質
/// </summary>
[Flags]
public enum SkillConsecutiveType
{
    /// <summary>
    /// 最初に選ばれた敵を攻撃し続ける
    /// </summary>
    AttackTargetContinuously = 1 >> 0,

    /// <summary>
    /// 毎コマンドZoneTraitに従って対象者の選択可能
    /// </summary>
    CanOprate = 1 >> 1 ,

    /// <summary>
    /// 毎コマンドZoneTraitに従ったランダムな対象者の選別をする。
    /// </summary>
    RandomOprate = 1 >> 2,
    /// <summary>
    /// ターンをまたいだ連続的攻撃　連続攻撃回数分だけ単体攻撃が無理やり進む感じ
    /// </summary>
    FreezeConsecutive = 1 >> 3 ,
    /// <summary>
    /// ランダムな百分率でスキル実行が連続されるかどうか
    /// </summary>
    RandomPercentConsecutive = 1 >> 4 ,
    /// <summary>
    /// _atkCountの値に応じて連続攻撃が行われるかどうか
    /// </summary>
    FixedConsecutive =1 >> 5 ,
    /// <summary>
    /// スキル保存性質　
    /// **意図的に実行とは別に攻撃保存を選べて**、
    ///その攻撃保存を**選んだ分だけ連続攻撃回数として発動**
    ///Randomな場合はパーセント補正が変わる？
    /// </summary>
    Stockpile = 1 >> 6 ,

}
/// <summary>
/// 対象を選別する意思状態の列挙体 各手順で選ばれた末の結果なので、単一の結果のみだからビット演算とかでない
/// </summary>
public enum DirectedWill
{
    /// <summary>
    /// 前のめり
    /// </summary>
    InstantVanguard,
    /// <summary>
    /// 後衛または前のめりいない集団
    /// </summary>
    BacklineOrAny,
    /// <summary>
    /// 単一ユニット
    /// </summary>
    One,
    /// <summary>
    /// 全て
    /// </summary>
    All,
}
/// <summary>
/// "範囲"攻撃の割合的な意志を表す列挙体 予め設定された3つの割合をどう扱うかの指定
/// </summary>
public enum AttackDistributionType
{
    /// <summary>
    /// 完全ランダムでいる分だけ割り当てる
    /// </summary>
    Random,
    /// <summary>
    /// 一人一人指定する　味方なら選んだのが、敵ならSkillAIを通じて指定
    /// </summary>
    OneToOne,
    /// <summary>
    /// 2までの値だけを利用して、前衛と後衛への割合。　前衛が以内なら後衛単位　おそらく2が使われる
    /// </summary>
    vanguardOrBackers
}
[Serializable]
public class BaseSkill
{
    /// <summary>
    ///     スキルの精神属性
    /// </summary>
    public SpiritualProperty SkillSpiritual { get; }


    /// <summary>
    /// スキル性質を持ってるかどうか
    /// 複数指定した場合は全て当てはまってるかどうかで判断
    /// </summary>
    public bool HasType(params SkillType[] skills)
    {
        SkillType combinedSkills = 0;
        foreach (SkillType skill in skills)
        {
            combinedSkills |= skill;
        }
        return (WhatSkill & combinedSkills) == combinedSkills;
    }
    /// <summary>
    /// そのスキル連続性質を持ってるかどうか
    /// </summary>
    public bool HasConsecutiveType(SkillConsecutiveType skill)
    {
        return (ConsecutiveType & skill) == skill;
    }
    /// <summary>
    /// スキル範囲性質を持ってるかどうか
    /// 複数指定した場合は全て当てはまってるかどうかで判断
    /// </summary>
    public bool HasZoneTrait(params SkillZoneTrait[] skills)
    {
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }
        return (ZoneTrait & combinedSkills) == combinedSkills;
    }


    /// <summary>
    ///     スキルの物理属性
    /// </summary>
    public PhysicalProperty SkillPhysical { get; }

    public BaseStates Doer;//行使者

    [SerializeField]
    private string _name;

    private int _doConsecutiveCount;//スキルを連続実行した回数
    private int _doCount;//スキルを実行した回数
    private int _hitCount;    // スキルがヒットした回数
    private int _hitConsecutiveCount;//スキルが連続ヒットした回数
    private int _triggerCount;//発動への－カウント　このカウント分連続でやらないと発動しなかったりする　重要なのは連続でやらなくても　一気にまたゼロからになるかはスキル次第
    private int _triggerCountMax;//発動への－カウント　の指標
    private int _atkCount;//攻撃回数
    private int _atkCountUP;//攻撃回数

    /// <summary>
    /// 前回のスキル行使の戦闘ターン ない状態は-1
    /// </summary>
    private int _tmpSkillUseTurn = -1;
    /// <summary>
    /// 前回スキル実行した時からの経過ターン 
    /// </summary>
    public int DeltaTurn { get; private set; } = -1;

    /// <summary>
    /// 前回からのターン差を記録するDeltaTurnを更新する関数  battleManagerで利用する。
    /// </summary>
    public void SetDeltaTurn(int nowTurn)
    {
        if(_tmpSkillUseTurn >= 0)//前回のターンが記録されていたら
        {
            DeltaTurn = Math.Abs(_tmpSkillUseTurn - nowTurn);//前回との差をdeltaTurn保持変数に記録する
        }
        //記録されていない場合、リセットされて-1になっているので何もしない。


        _tmpSkillUseTurn = nowTurn;//今回のターン数を一時記録

    }

    /// <summary>
    /// triggerCountが0以上の複数ターン実行が必要なスキルの場合、複数ターンに跨る実行中に中断出来るかどうか。
    /// </summary>
    public bool CanCancel = true;

    /// <summary>
    /// このスキルを利用すると前のめり状態になるかどうか
    /// </summary>
    public bool IsAggressiveCommit = true;
    /// <summary>
    /// スキルが前のめりになるからならないかを選べるかどうか
    /// </summary>
    public bool CanSelectAggressiveCommit = false;

  

    /// <summary>
    /// ランダムにスキル実行が継続されるかどうかの割合　
    /// </summary>
    public float ConsecutivePercentage = 0;

    /// <summary>
    /// 実行したキャラに付与される追加硬直値
    /// </summary>
    public int SKillDidWaitCount;//スキルを行使した後の硬直時間。 Doer、行使者のRecovelyTurnに一時的に加算される？


    public string SkillName
    {
        get { return _name; }
    }


    /// <summary>
    /// BattleManager単位で行使した回数
    /// 実行対象のreactionSkill内でインクリメント
    /// </summary>    
    public  int DoCount
    {
        get { return _doCount; }
        set { _doCount = value; }
    }

    /// <summary>
    /// BattleManager単位で"連続"で使われた回数。　
    /// 実行する際にdoerのSkillUseConsecutiveCountUpからAttackChara内で使用
    /// </summary>
    public virtual int DoConsecutiveCount
    {
        get { return _doConsecutiveCount; }
        set { _doConsecutiveCount = value; }

    }

    /// <summary>
    /// スキルがヒットしたときの回数カウント
    /// </summary>
    public int HitCount
    {
        get => _hitCount;
        set { _hitCount = value; }
    }
    /// <summary>
    /// スキルが連続ヒットしたときの回数カウント
    /// </summary>
    public int HitConsecutiveCount
    {
        get => _hitConsecutiveCount;
        set { _hitConsecutiveCount = value; }
    }

    /// <summary>
    /// スキルのヒットした回数、またその連続回数をカウントアップする
    /// </summary>
    public void SkillHitCount()
    {
        HitCount++;//単純にヒット回数を増やす
    }

    /// <summary>
    /// スキル実行に必要なカウント　-1で実行される。
    /// </summary>
    public virtual int TrigerCount()
    {
        if (_triggerCountMax > 0)//1回以上設定されてたら
        {
            _triggerCount--;
            return _triggerCount;
        }

        //発動カウントが0に設定されている場合、そのまま実行される。
        return -1;
    }

    /// <summary>
    /// 選ばれなかった時の発動カウントが戻っちゃう処理
    /// </summary>
    public virtual void ReturnTrigger()
    {
        _triggerCount = _triggerCountMax;//基本的に一回でもやんなかったらすぐ戻る感じ
    }
    /// <summary>
    /// 実行に成功した際の発動カウントのリセット
    /// </summary>
    public virtual void DoneTrigger()
    {
        _triggerCount = _triggerCountMax;//基本的にもう一回最初から
    }

    /// <summary>
    /// オーバライド可能な攻撃回数
    /// </summary>
    public virtual int ATKCount
    {
        get { return _atkCount; }
        set { _atkCount = value; }

    }
    public virtual int ATKCountUP => _atkCountUP;

    /// <summary>
    /// 現在の連続攻撃回数を参照して次回の連続攻撃があるかどうか
    /// </summary>
    public bool NextConsecutiveATK()
    {
        if (_atkCountUP >= _atkCount)//もし設定した値にカウントアップ値が達成してたら。
        {
            _atkCountUP = 0;//値初期化
            return false;//終わり
        }
        return true;//まだ達成してないから次の攻撃がある。
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
    /// 連続攻撃の値を増やす
    /// </summary>
    /// <returns></returns>
    public virtual int ConsecutiveFixedATKCountUP()
    {
        _atkCountUP++;
        return _atkCountUP;
    }

    /// <summary>
    /// TLOAかどうか
    /// </summary>
    public bool IsTLOA;

    /// <summary>
    /// スキルの範囲効果における各割合　最大で6の長さまで使うと思う
    /// </summary>
    public int[] PowerSpread;

    /// <summary>
    /// スキルのパワー
    /// </summary>
    public float SkillPower;

    /// <summary>
    /// スキルの命中補正
    /// </summary>
    public float SkillHitPer;

    /// <summary>
    /// 基本的にスキルのレベルは恒常的に上がらないが、戦闘内では一時的に上がったりするのかもしれない。
    /// </summary>
    public float SkillLevel;
    /// <summary>
    /// スキルの現在のライバハルの値 特定の範囲でレベルが上がる？
    /// </summary>
    public float SkillRivahal;

    /// <summary>
    /// 初期化コールバック関数 初期化なので起動時の最初の一回しか使わないような処理しか書かないようにして
    /// </summary>
    public void OnInitialize(BaseStates owner)
    {
        Doer = owner;//管理者を記録
    }
    /// <summary>
    /// スキルの"一時保存"系プロパティをリセットする(主にbattleManager系で)
    /// </summary>
    public void ResetTmpProperty()
    {
        _doCount = 0;
        _doConsecutiveCount = 0;
        _hitCount = 0;
        _hitConsecutiveCount = 0;
        _atkCountUP = 0;
        _triggerCount = _triggerCountMax;//発動カウントはカウントダウンするから最初っから
        _tmpSkillUseTurn = -1;//前回とのターン比較用の変数をnullに

        SkillRivahal = 0;
        SkillLevel = 0;
    }
    /// <summary>
    /// TLOAと対決した際のライバハルの増え方の関数
    /// </summary>
    public void RivahalDream()
    {

    }

    //スキルパワーの計算
    public virtual float SkillPowerCalc(int underIndex)
    {
        //範囲割合を含める
        return SkillPower* PowerSpread[underIndex];
    }

    /// <summary>
    /// スキルにより補正された最終命中率
    /// </summary>
    public virtual float SkillHitCalc()
    {
        return Doer.HIT() * SkillHitPer;//術者の命中×スキルの命中率
    }

    /// <summary>
    /// スキル実行時に付与する状態異常とか
    /// </summary>
    public List<BasePassive> subEffects;

    //防御無視率
    public float DEFATK;

    public SkillType WhatSkill;

    public SkillConsecutiveType ConsecutiveType;
    public SkillZoneTrait ZoneTrait;

}