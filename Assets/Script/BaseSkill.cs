using R3;
using RandomExtensions;
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
    Attack = 1 << 0,
    Heal = 1 << 1,
    addPassive = 1 << 2,
    RemovePassive = 1 << 3,
    DeathHeal = 1 << 4,
    AddVitalLayer =1 << 5,
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
    /// この際唐突性があるためテラーズヒットは発生しない
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
    RandomTargetALLSituation = 1 << 7,
    /// <summary>
    /// 範囲ランダム   前のめり,後衛単位or単体ランダム
    /// </summary>
    RandomTargetMultiOrSingle = 1 << 8,
    /// <summary>
    /// 範囲ランダム　全体or単体ランダム
    /// </summary>
    RandomTargetALLorSingle = 1 << 9,
    /// <summary>
    /// 範囲ランダム　全体or前のめり,後衛単位
    /// </summary>
    RandomTargetALLorMulti = 1 << 10,

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
    /// <summary>
    /// 範囲ランダム判定用
    /// </summary>
    RandomRange = 1 << 15,

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
/// <summary>
/// 対象を選別する意思状態の列挙体 各手順で選ばれた末の結果なので、単一の結果のみだからビット演算とかでない
/// この列挙体は**状況内で選別するシチュエーション的な変数**という側面が強い。
/// 要はbattleManagerでそれぞれの状況に応じてキャラを選別するって感じ
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
}
/// <summary>
/// "範囲"攻撃の分散性質を表す列挙体 予め設定された3～６つの割合をどう扱うかの指定
/// powerSpreadの配列のサイズで分散するかどうかを判定する。(つまりNoneみたいな値はない。)
/// </summary>
public enum AttackDistributionType
{
    /// <summary>
    /// 完全ランダムでいる分だけ割り当てる
    /// </summary>
    Random,
    /// <summary>
    /// 前のめり状態(敵味方問わず)のキャラが最初に回されるランダム分散　
    /// 放射系統のビーム的な
    /// </summary>
    Beam,
    /// <summary>
    /// 2までの値だけを利用して、前衛と後衛への割合。　
    /// 前衛が以内なら後衛単位　おそらく2が使われる
    /// </summary>
    Explosion,
    /// <summary>
    /// 投げる。　つまり敵味方問わず前のめり状態のが一番後ろに回される
    /// </summary>
    Throw,
}
[Serializable]
public class BaseSkill
{
    /// <summary>
    ///     スキルの精神属性
    /// </summary>
    public SpiritualProperty SkillSpiritual;



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
    private int _recordDoCount;//スキルを実行した回数（記録する
    private int _hitCount;    // スキルがヒットした回数
    private int _hitConsecutiveCount;//スキルが連続ヒットした回数
    private int _triggerCount;//発動への−カウント　このカウント分連続でやらないと発動しなかったりする　重要なのは連続でやらなくても　一気にまたゼロからになるかはスキル次第
    private int _triggerCountMax;//発動への−カウント　の指標
    private int _atkCountUP;//連続攻撃中のインデックス的回数
    private float _RandomConsecutivePer;//連続実行の確率判定のパーセント

    //stockpile用　最大値はランダム確率の連続攻撃と同じように、ATKCountを参照する。
    private int _nowStockCount;//現在のストック数

    [SerializeField]
    private int _defaultStockCount = 1;//ストックデフォルト
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
        if (_tmpSkillUseTurn >= 0)//前回のターンが記録されていたら
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
    public int DoCount
    {
        get { return _doCount; }
        set { _doCount = value; }
    }
    /// <summary>
    /// 行使した回数 永続的にカウントされる
    /// </summary>    
    public int RecordDoCount
    {
        get { return _recordDoCount; }
        set { _recordDoCount = value; }
    }
    /// <summary>
    /// 永続的なものと一時的な物両方のスキル使用回数をカウントアップ
    /// </summary>
    public void DoSkillCountUp()
    {
        _doCount++;
        _recordDoCount++;
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
    /// 実行に成功した際の発動カウントのリセット 0ばら
    /// </summary>
    public virtual void DoneTrigger()
    {
        _triggerCount = _triggerCountMax;//基本的にもう一回最初から
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
    /// 現在の連続攻撃回数のindex
    /// </summary>
    public virtual int ATKCountUP => _atkCountUP;

    /// <summary>
    /// 攻撃回数カウントをリセットする
    /// </summary>
    public void ResetAtkCountUp()
    {
        _atkCountUP = 0;
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

            if(RandomEx.Shared.NextFloat(1)<_RandomConsecutivePer)//確率があったら、
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
    public float[] PowerSpread;

    /// <summary>
    /// スキルのパワー
    /// </summary>
    public float SkillPower;

    /// <summary>
    /// スキルの命中補正 int 百分率
    /// </summary>
    public int SkillHitPer;

    /// <summary>
    /// 基本的にスキルのレベルは恒常的に上がらないが、戦闘内では一時的に上がったりするのかもしれない。
    /// </summary>
    public float SkillLevel;
    

    /// <summary>
    /// 初期化コールバック関数 初期化なので起動時の最初の一回しか使わないような処理しか書かないようにして
    /// </summary>
    public void OnInitialize(BaseStates owner)
    {
        Doer = owner;//管理者を記録
        ResetStock();//_nowstockは最初は0になってるので、初期化でdefaultstockと同じ数にする。
    }
    /// <summary>
    /// 行使者 doerが死亡したときdoer側で呼ばれるコールバック
    /// </summary>
    public void OnDeath()
    {
        ResetStock();
        ResetAtkCountUp();
    }

    /// <summary>
    /// スキルの"一時保存"系プロパティをリセットする　BattleManager終了時など
    /// </summary>
    public void OnBattleEnd()
    {
        _doCount = 0;
        _doConsecutiveCount = 0;
        _hitCount = 0;
        _hitConsecutiveCount = 0;
        ResetAtkCountUp();
        _triggerCount = _triggerCountMax;//発動カウントはカウントダウンするから最初っから
        _tmpSkillUseTurn = -1;//前回とのターン比較用の変数をnullに
        ResetStock();

        SkillLevel = 0;
    }
    /// <summary>
    /// TLOAと対決した際のライバハルの増え方の関数
    /// </summary>
    public void RivahalDream()
    {

    }

    //スキルパワーの計算
    public virtual float SkillPowerCalc(float spread)
    {
        var pwr = SkillPower;//基礎パワー



        pwr *= spread;//分散値を掛ける

        return pwr;
    }

    /// <summary>
    /// スキルにより補正された最終命中率
    /// </summary>
    /// <param name="supremacyBonus">命中ボーナス　主に命中凌駕用途</param>>
    public virtual bool SkillHitCalc(float supremacyBonus)
    {
        var rndMin = RandomEx.Shared.NextInt(3);//ボーナスがある場合ランダムで三パーセント~0パーセント引かれる
        if(supremacyBonus>rndMin)supremacyBonus -= rndMin;

        return RandomEx.Shared.NextInt(100) < supremacyBonus + SkillHitPer;
    }

    /// <summary>
    /// スキル実行時に付与する状態異常とか ID指定
    /// </summary>
    public List<int> subEffects;

    /// <summary>
    /// スキル実行時に付与する追加HP(Passive由来でない)　ID指定
    /// </summary>
    public List<int> subVitalLayers;

    /// <summary>
    /// スキルごとのムーブセット 戦闘規格ごとのaに対応するもの。
    /// </summary>
    [SerializeField]
    List<MoveSet> A_MoveSet;
    /// <summary>
    /// スキルごとのムーブセット 戦闘規格ごとのbに対応するもの。
    /// </summary>
    [SerializeField]
    List<MoveSet> B_MoveSet;
    /// <summary>
    /// 現在のムーブセット
    /// </summary>
    [NonSerialized]
    MoveSet NowMoveSetState=new();

    /// <summary>
    /// A-MoveSetのList<MoveSet>から現在のMoveSetをランダムに取得する
    /// なにもない場合はreturnで終わる。つまり単体攻撃前提ならmovesetが決まらない。
    /// aOrB 0:A 1:B
    /// </summary>
    public void DecideNowMoveSet_A0_B1(int aOrB)
    {
        if(aOrB == 0)
        {
            if(A_MoveSet.Count == 0)
            {
                NowMoveSetState = new();
                return;
            }
            NowMoveSetState = A_MoveSet[RandomEx.Shared.NextInt(A_MoveSet.Count)];
        }
        else if(aOrB == 1)
        {
            if(B_MoveSet.Count == 0)
            {
                NowMoveSetState = new();
                return;
            }
            NowMoveSetState = B_MoveSet[RandomEx.Shared.NextInt(B_MoveSet.Count)];
        }
    }
    /// <summary>
    /// 単体攻撃時のAimStyle保存用
    /// </summary>
    AimStyle _nowSingleAimStyle;
    /// <summary>
    /// 単体攻撃時のAimStyle設定
    /// </summary>
    public void SetSingleAimStyle(AimStyle style)
    {
        _nowSingleAimStyle = style;
    }
    /// <summary>
    /// 現在のムーブセットでのAimStyleを、現在の攻撃回数から取得する
    /// </summary>
    /// <returns></returns>
    public AimStyle NowAimStyle()
    {
        if(!NowConsecutiveATKFromTheSecondTimeOnward())return _nowSingleAimStyle;//初回攻撃なら単体保存した変数を返す

        return NowMoveSetState.GetAtState(_atkCountUP - 1); 
    }
    /// <summary>
    /// 現在のムーブセットでのDEFATKを、現在の攻撃回数から取得する
    /// 初回攻撃を指定したらエラー出るます
    /// </summary>
    float NowAimDefATK()
    {
        var nowCountUp = _atkCountUP;//今何回目の攻撃か。
        return NowMoveSetState.GetAtDEFATK(_atkCountUP - 1); 
    }

    [SerializeField]
    float _defAtk;
    /// <summary>
    /// 連続攻撃時にはそれ用の、それ以外はスキル自体の防御無視率が返ります。
    /// </summary>
    public float DEFATK{
        get{
            if(NowConsecutiveATKFromTheSecondTimeOnward())//連続攻撃中ならば、
            return NowAimDefATK();//連続攻撃に設定されているDEFATKを乗算する

            return _defAtk;
        }
    }

    /// <summary>
    /// スキルの攻撃性質
    /// </summary>
    public SkillType WhatSkill;
    /// <summary>
    /// スキルの連撃性質
    /// </summary>
    public SkillConsecutiveType ConsecutiveType;
    /// <summary>
    /// スキルの範囲性質
    /// </summary>
    public SkillZoneTrait ZoneTrait;
    /// <summary>
    /// スキルの分散性質
    /// </summary>
    public AttackDistributionType DistributionType;

    /// <summary>
    /// 威力の範囲が複数に選択または分岐可能時の割合差分
    /// インスペクタで追加し、
    /// 基本的にskillPowerCalcと味方の範囲選択ボタンでの表記で用い、
    /// canselectRangeで範囲が複数選択できる、
    /// またはrandomRangeで範囲が複数分岐した際に使う感じ。
    /// </summary>
    public SerializableDictionary<SkillZoneTrait, float>
        PowerRangePercentageDictionary;
    /// <summary>
    /// 命中率のステータスに直接かかるスキルの範囲意志による威力補正
    /// </summary>
    public SerializableDictionary<SkillZoneTrait, float>
        HitRangePercentageDictionary;

}
/// <summary>
/// インスペクタで表示可能なAimStyleのリストの一つのステータスとDEFATKのペア
/// </summary>
[Serializable]
public class MoveSet: ISerializationCallbackReceiver
{
    public List<AimStyle> States = new List<AimStyle>();
    public List<float> DEFATKList = new List<float>();
    public AimStyle GetAtState(int index)
    {
        return States[index];
    }
    public float GetAtDEFATK(int index)
    {
        return DEFATKList[index];
    }
    public MoveSet()
    {
        States.Clear();
        DEFATKList.Clear();
    }
    //Unityインスペクタ上で新しく防御無視率を設定した際に、デフォルトで何も起こらない(=1.0f)値が入るようにするための処理。

    // 旧サイズを保持しておくための変数
    [NonSerialized]
    private int oldSizeDEFATK = 0;

// シリアライズ直前に呼ばれる
    public void OnBeforeSerialize()
    {
        // 新規追加があった場合、そのぶんだけ1.0fを代入
        if (DEFATKList.Count > oldSizeDEFATK)
        {
            for (int i = oldSizeDEFATK; i < DEFATKList.Count; i++)
            {
                // 新しく挿入された分
                DEFATKList[i] = 1.0f;
            }
        }

        // 今回のリストサイズを保存
        oldSizeDEFATK = DEFATKList.Count;
    }

    // デシリアライズ後に呼ばれる
    public void OnAfterDeserialize()
    {
        // 特に何もしない場合は空でOK
    }



}
