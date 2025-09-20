using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;

//パッシブコールバック全般はHasPassives.csにあり
//ここはBaseStatesの主だったコールバック全般
public abstract partial class BaseStates    
{
    //  ==============================================================================================================================
    //                                              死亡処理（戦闘/非戦闘）
    //  ==============================================================================================================================

    /// <summary>
    /// 今バトルで死亡処理（OnBattleDeathCallBack）を既に実行済みか。
    /// 従来の hasDied の代替。Angel() で復活したら false に戻す。
    /// </summary>
    private bool m_BattleDeathProcessed = false;

    /// <summary>
    /// 戦闘時に一度だけ死亡処理を実行する。HP<=0 かつ 未処理のときに OnBattleDeathCallBack を呼ぶ。
    /// </summary>
    public bool ProcessBattleDeathIfNeeded()
    {
        if (Death() && !m_BattleDeathProcessed)
        {
            m_BattleDeathProcessed = true;
            OnBattleDeathCallBack();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 戦闘時の死亡時コールバック。既定では共通 DeathCallBack を呼ぶ。
    /// 必要に応じて派生クラスで上書き可能。
    /// </summary>
    protected virtual void OnBattleDeathCallBack()
    {
        DeleteConsecutiveATK();//連続攻撃の消去
        ApplyConditionChangeOnDeath();//人間状況の変化

        //あるかわからないが続行中のスキルを消し、
        //以外のそれ以外のスキルの連続攻撃回数消去(基本的に一個しか増えないはずだが)は以下のforeachループで行う
        foreach (var skill in SkillList)
        {
            skill.OnBattleDeath();
        }

        //対象者ボーナス全削除
        TargetBonusDatas.AllClear();

        //パッシブの死亡時処理
        UpdateDeathAllPassiveSurvival();

        //思えの値リセット
        ResetResonanceValue();

        //精神HPの死亡時分岐
        MentalHPOnDeath();

        //落ち着きをリセット　死んだらスキルの持続力無くなるしね
        CalmDown();

    }
    //  ==============================================================================================================================
    //                                              ReactionSkill用
    //  ==============================================================================================================================

    /// <summary>
    /// 一人に対するスキル実行が終わった時のコールバック
    /// </summary>
    void OnAttackerOneSkillActEnd()
    {
        //バッファをクリア
        NowUseSkill.EraseBufferSkillType();//攻撃性質のバッファ
        NowUseSkill.EraseBufferSubEffects();//スキルの追加パッシブ付与リスト
        
    }
    /// <summary>
    /// 一人に対するスキル実行が始まった時のコールバック
    /// </summary>
    void OnAttackerOneSkillActStart(BaseStates UnderAtker)
    {
        ApplyExtraPassivesToSkill(UnderAtker);//攻撃者にスキルの追加パッシブ性質を適用
        NowUseSkill.CalcCradleSkillLevel(UnderAtker);//「攻撃者の」スキルのゆりかご計算
        NowUseSkill.RefilCanEraceCount();//除去スキル用の消せるカウント回数の補充
    }

    /// <summary>
    /// 攻撃や友好効果など「何らかがヒットした後」の薄い拡張フック
    /// 既定では何もしません。必要に応じて派生クラスでオーバーライドしてください。
    /// </summary>
    protected virtual void OnBattleAnyEffectHit(BaseStates attacker, BaseSkill skill, bool isAttack, HitResult bestHitOutcome)
    {
    }
    /// <summary>
    /// 戦闘中に次のターンに進む際のコールバック
    /// </summary>
    public void OnNextTurnNoArgument()
    {
        ProcessBattleDeathIfNeeded();//バトル内での死亡コールバック実装

        UpdateTurnAllPassiveSurvival();
        UpdateAllSkillPassiveSurvival();
        UpdateNotVanguardAllPassiveSurvival();
        PassivesOnNextTurn();//パッシブのターン進効果

        //パッシブで死亡した時のため
        ProcessBattleDeathIfNeeded();//バトル内での死亡コールバック実装


        //生きている場合にのみする処理
        if(!Death())
        {
            ConditionInNextTurn();//人間状況ターン変化
            TryMentalPointRecovery();//精神HPが自動回復される前に精神HPによるポイント自然回復の判定

            if(IsMentalDiverGenceRefilCountDown() == false)//再充填とそのカウントダウンが終わってるのなら
            {
                MentalDiverGence();
                if(_mentalDivergenceRefilCount > 0)//乖離が発生した直後に回復が起こらないようにするif 再充填カウントダウンがセットされたら始まってるから
                {
                    MentalHPHealOnTurn();//精神HP自動回復
                }
               
            }

            CalmDownCountDec();//落ち着きカウントダウン
        }
       
        ApplyBufferApplyingPassive();//パッシブをここで付与。 =>詳細は豚のパッシブみとけ
        ApplySkillsBufferApplyingSkillPassive();//スキルパッシブも付与

        //記録系
        _tempLive = !Death();//死んでない = 生きてるからtrue
        BattleFirstSurpriseAttacker = false;//絶対にoff bm初回先手攻撃フラグは
        
        //フラグ系
        SelectedEscape = false;//選択を解除
        SkillCalculatedRandomRange = false;//ランダム範囲計算フラグを解除
    }
    //  ==============================================================================================================================
    //                                              BM
    //  ==============================================================================================================================

    /// <summary>
    ///bm生成時に初期化される関数
    /// </summary>
    public virtual void OnBattleStartNoArgument()
    {
        m_BattleDeathProcessed = false; // バトル開始時に未処理へ
        TempDamageTurn = 0;
        SelectedEscape = false;//選択を解除
        SkillCalculatedRandomRange = false;//ランダム範囲計算フラグを解除
        CalmDownSet(AGI().Total * 1.3f,1f);//落ち着きカウントの初回生成　
        //最初のSkillACT前までは回避率補正は1.3倍 攻撃補正はなし。=100%\

        _tempVanguard = false;
        _tempLive = true;
        Rivahal = 0;//ライバハル値を初期化
        Target = 0;//どの辺りを狙うかの初期値
        RangeWill = 0;//範囲意志の初期値
        DecisionKinderAdaptToSkillGrouping();//慣れ補正の優先順位のグルーピング形式を決定するような関数とか
        DecisionSacriFaithAdaptToSkillGrouping();
        ActDoOneSkillDatas = new List<ACTSkillDataForOneTarget>();//スキルの行動記録はbm単位で記録する。
        DidActionSkillDatas = new();//スキルのアクション事の記録データを初期化
        damageDatas = new();
        FocusSkillImpressionList = new();//慣れ補正用スキル印象リストを初期化
        TargetBonusDatas = new();
        ConditionTransition();
        RecovelyWaitStart();//リカバリーターンのリセット
        _mentalDivergenceRefilCount = 0;//精神HP乖離の再充填カウントをゼロに戻す
        _mentalDivergenceCount = 0;//精神HP乖離のカウントをゼロに戻す
        _mentalPointRecoveryCountUp = 0;//精神HP自然回復のカウントをゼロに戻す
        DamageDealtToEnemyUntilKill = new();//戦闘開始時にキャラクターを殺すまでに与えたダメージを記録する辞書を初期化する
        battleGain = new();//バトルが開始するたびに勝利ブースト用の値を初期化
        PassivesReSetDurationTurnOnBattleStart();//パッシブの持続戦闘ターン数を場合により計算して再代入する。

        InitPByNowPower();//Pの初期値設定

        //初期精神HPは常に戦闘開始時に最大値
        MentalHP = MentalMaxHP;

        //スキルの戦闘開始時コールバック
        OnBattleStartSkills();
        
    }
    public virtual void OnBattleEndNoArgument()
    {
        m_BattleDeathProcessed = false; // 終了時にクリア（保険）
        DeleteConsecutiveATK();//連続攻撃を消す
        NowUseSkill = null;//現在何のスキルも使っていない。
        TempDamageTurn = 0;
        DeleteConsecutiveATK();
        DecayOfPersistentAdaptation();//恒常的な慣れ補正の減衰　　持ち越しの前に行われる　じゃないと記憶された瞬間に忘れてしまうし
        AdaptCarryOver();//慣れ補正持ち越しの処理
        battleGain.Clear();//勝利ブーストの値をクリアしてメモリをよくする
        foreach(var layer in _vitalLayerList.Where(lay => lay.IsBattleEndRemove))
        {
            RemoveVitalLayerByID(layer.id);//戦闘の終了で消える追加HPを持ってる追加HPリストから全部消す
        }
        foreach(var passive in _passiveList.Where(pas => pas.DurationWalkCounter < 0))
        {
            RemovePassive(passive);//歩行残存ターンが-1の場合戦闘終了時に消える。
        }
        foreach (var pas in _passiveList)
        {
            pas.DurationTurnCounter = -1;//歩行ターンがあれば残存するが、それには関わらず戦闘ターンは意味がなくなるので全て-1 = 戦闘ターンによる消えるのをなくす。
        }
        //スキルパッシブの戦闘終了時の歩行ターンによる処理は下のスキルコールバックでやってます。
    
        //スキルの戦闘終了時コールバック
        OnBattleEndSkills();

        CalmDown();//落ち着きカウント無くす
        BattleFirstSurpriseAttacker = false;//bm初回先手攻撃フラグ
        Rivahal = 0;//ライバハル値を初期化

    }

    /// <summary>
    /// 攻撃した相手が死んだ場合のコールバック
    /// </summary>
    void OnKill(BaseStates target)
    {
        //まず殺すまでのダメージを取得する。
        var AllKillDmg = DamageDealtToEnemyUntilKill[target];
        DamageDealtToEnemyUntilKill.Remove(target);//殺したので消す(angelしたらもう一回最初から記録する)

        HighNessChance(target);//ハイネスチャンス(ThePowerの増加判定)
        ApplyConditionChangeOnKillEnemy(target);//人間状況の変化

        RecordConfidenceBoost(target,AllKillDmg);

        //ここの殺した瞬間のはみんな精神属性の分岐では　=> スキルの精神属性　を使えば、　実行した瞬間にそのスキルの印象に染まってその状態の精神属性で分岐するってのを表現できる
        //(スキル属性のキャラ代入のタイミングについて　を参照)
        
    }
    /// <summary>
    /// BM終了時に全スキルの一時保存系プロパティをリセットする
    /// </summary>
    public void OnBattleEndSkills()
    {
        foreach (var skill in SkillList)
        {
            skill.OnBattleEnd();//プロパティをリセットする
        }
    }
    public void OnBattleStartSkills()
    {
        foreach (var skill in SkillList)
        {
            skill.OnBattleStart();
        }
    }

    //  ==============================================================================================================================
    //                                              汎用
    //  ==============================================================================================================================

    /// <summary>
    /// 持ってるスキルリストを初期化する
    /// 立場により持ってる実体スキルの扱い方が異なるので各派生クラスで実装する。
    /// </summary>
    public abstract void OnInitializeSkillsAndChara();



}
