using R3.Collections;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// スレーム　　aiの考えたつづり => https://apps.abacus.ai/chatllm/?convoId=136be9a4fa&appId=1036bbd7c6
/// </summary>
public class Slaim : BasePassive
{

    public override void OnApply(BaseStates user)
    {
        //サイレント練度により、ターン数加算
        var silentTrain = user.TenDayValues(false).GetValueOrZero(TenDayAbility.SilentTraining);
        var AddTurn = Mathf.Min(4,silentTrain / 11); //加算限界は　4ターン　　サイレント練度÷ 11分ね
        DurationTurn += (int)AddTurn;//int変換して小数省いて加算

        base.OnApply(user);
    }
    public override void OnBeforeDamage(BaseStates Atker)
    {
        //自分に雲隠れを付与
        _owner.ApplyPassiveBufferInBattleByID(14);
        //雲隠れの2ターンと十日能力の比較分のリーディングステップを味方に付与
        foreach(var live in Walking.bm.GetOtherAlliesAlive(_owner))
        {
            live.ApplyPassiveBufferInBattleByID(13);
            var pas = live.GetBufferPassiveByID(13);
            pas.SetPercentageModifier(whatModify.agi, 1.04f);//4%上げる
            pas.SetFixedValue(whatModify.agi,_owner.TenDayValues(false).GetValueOrZero(TenDayAbility.Taraiton)/3 *
            live.TenDayValues(false).GetValueOrZero(TenDayAbility.SilentTraining));//支援対象の味方のサイレント練度　×　付与者の盥豚÷3

            ApplyTaktbruch(Atker);//タクトブルフの攻撃者への付与
        }
        
        //派生元の処理を最後に呼び出す。
        base.OnBeforeDamage(Atker);
    }

    /// <summary>
    /// タクトブルフ付与関数 紛らわしいけど攻撃者が付与対象者ね
    /// </summary>
    void ApplyTaktbruch(BaseStates AtkerButPassiveDefender)
    {
        //計算要素の十日能力
        var AtkerTraiton = AtkerButPassiveDefender.TenDayValues(false).GetValueOrZero(TenDayAbility.Taraiton);//術者の盥豚
        var defenderCool = _owner.TenDayValues(false).GetValueOrZero(TenDayAbility.ColdHeartedCalm);//食らい者の冷酷冷静
        var AtkerVoid = AtkerButPassiveDefender.TenDayValues(false).GetValueOrZero(TenDayAbility.TentVoid);//術者のテント空虚
        var AtkerSmiler = AtkerButPassiveDefender.TenDayValues(false).GetValueOrZero(TenDayAbility.Smiler);//術者のスマイラー
        var defenderSmiler = _owner.TenDayValues(false).GetValueOrZero(TenDayAbility.Smiler);//食らい者のスマイラー
        var AtkerFaceToHand = AtkerButPassiveDefender.TenDayValues(false).GetValueOrZero(TenDayAbility.FaceToHand);//術者の顔から手
        var AtkerSilentTrain = AtkerButPassiveDefender.TenDayValues(false).GetValueOrZero(TenDayAbility.SilentTraining);//術者のサイレント練度
        var defenderSilentTrain = _owner.TenDayValues(false).GetValueOrZero(TenDayAbility.SilentTraining);//食らい者のサイレント練度


        //攻撃者にタクトブルフを付与
        AtkerButPassiveDefender.ApplyPassiveBufferInBattleByID(16);
        var pas = AtkerButPassiveDefender.GetBufferPassiveByID(16);

        //ここ以降AtkerButPassiveDefender  => defender 狩られる側として変数で扱ってるよ

        //攻撃者のタクトブルフの発動率を補正操作　　この場合、付与攻撃者が対象者に大して十日能力が凌駕してる倍率分発動率が　失敗しやすくなる = 減少するので、
        //付与者(自分)の十日能力 ÷　対象者の十日能力　で　攻撃者がどの程度対象者の十日能力を超えてるかの倍率が出る。
        var AtkerValue = AtkerTraiton * 2.2f + AtkerSmiler *1.4f + AtkerFaceToHand * 1.3f;
        var defenderValue = defenderSmiler - Mathf.Max(0,AtkerVoid * 0.3f - defenderCool * 0.17f) + Mathf.Max(0,defenderCool * 2.61f - AtkerVoid * 0.35f);
        var SilentTrainigRatio = AtkerSilentTrain / defenderSilentTrain;//サイレント練度の比
        var TaktbruchRatio = AtkerValue / defenderValue;//攻撃者と防衛者の十日能力比率
        var FinalAvarageRatio = (TaktbruchRatio + SilentTrainigRatio) / 2;//サイレント練度と十日能力比率の平均

        
        float percent = FinalAvarageRatio * 100f;// 100掛けて小数からパーセンテージに
        float diff    = percent - 100f;                // 100引いて凌駕差分　　例　130% => 30%
        float rate    = Mathf.Clamp(100f - diff, 0f, 100f);  // 0~100の範囲で留まるようにクランプをし、発動率に変換する
        //例　凌駕パーセンテージ　30% => 100-30 = 70%    30%分能力値を凌駕してるなら　敵の発動率を70%にできる。
        //逆に言うと　防衛者(付与対象者)の方が付与者より能力値が高いほど　付与対象者は100%の発動率を維持する。　つまり「ノーダメージ」

        //パワーによる調整
        if(AtkerButPassiveDefender.NowPower == ThePower.lowlow)//たるいなら　あえて調子の破断もくそもないので、むしろ効果が減る(発動率上昇)
        {
            rate *= 3;
            rate = Mathf.Clamp(rate, 0f, 100f);//100超えそうだしクランプ　　発動率の計算とrollperで対応されてるから別に無理にしなくていいけどね
        }
        if(AtkerButPassiveDefender.NowPower == ThePower.high)//高いなら　出鼻をくじかれるイメージで　もっと発動率が減る
        {
            rate /= 1.9f;
            rate = Mathf.Clamp(rate, 0f, 100f);
        }

        pas.SetSkillActivationRate(rate);//タクトブルフの発動率を設定
    }

    /// <summary>
    /// 味方や自分への、「敵の乱れ攻撃」に反応するイースターノジール用コールバック
    /// 味方や自分ごと処理しやすいBattleGroupから呼び出す。
    /// </summary>
    public virtual void OnAlliesEnemyDisturbedAttack()
    {
        //ジーノのスレーム側でパワー条件が変わる
        //通常スレームでは高いと使用できる
        if(_owner.NowPower == ThePower.high)
        {
            EasterNoshiir();
        }
    }
    /// <summary>
    /// イースターノジールの効果の関数
    /// </summary>
    protected void EasterNoshiir()
    {
        
    }
}