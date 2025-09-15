using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;

//戦闘のイベント系統
public abstract partial class BaseStates
{
    
    //  ==============================================================================================================================
    //                                              互角一撃
    //
    //
    //  ==============================================================================================================================



    /// <summary>
    /// 互角一撃の生存判定
    /// </summary>
    void CalculateMutualKillSurvivalChance(float LiveHP,float dmg,BaseStates atker)
    {
        //deathの判定が入る前に、互角一撃の生存判定を行い、HP再代入
        //ダメージの大きさからして絶対に死んでるからDeath判定は要らず、だからDeath辺りでの判定がいらない。(DeathCallBackが起こらない)
        if(LiveHP >= _maxhp*0.2f)//HPが二割以上の時に、
        {
            if(atker.TenDayValuesSum(true) <= TenDayValuesSum(false) * 1.6f)//自分の十日能力の総量の1.6倍以下なら
            {
                if (dmg >= _maxhp * 0.34f && dmg <= _maxhp * 0.66f )//大体半分くらいの攻撃なら  
                {
                    //生存判定が入る
                    if(rollper(GetMutualKillSurvivalChance()))
                    {
                        HP = _maxhp * 0.07f;
                    }
                }
            }
        }
    }


    /* ------------------------------------------------------------------------------------------------------------------------------------------
     *                                          処理関数群
     * ------------------------------------------------------------------------------------------------------------------------------------------
     */




    /// <summary>
    /// 互角一撃の状況で「即死しかけたが奇跡的に生き残る」確率(%)を返す。
    ///
    /// ◆大まかな流れ：
    ///  1) 精神属性 × パワー条件 を満たしているかどうか
    ///      - 満たしていなければ 0%
    ///  2) 人間状況ごとの基本値をベースにする
    ///      - 怒り/高揚/辛い/混乱 → 0%
    ///      - 覚悟 → 7%
    ///      - 楽観的 → 2%
    ///      - 普調 → 4%
    ///      - 疑念 → 1%
    ///  3) 特定の「精神属性 × 人間状況」組み合わせでさらに上書き
    ///      - 例: ゴッドティア × 楽観的 = 12% など
    /// </summary>
    public int GetMutualKillSurvivalChance()
    {
        var property = MyImpression;
        var power = NowPower;
        var condition = NowCondition;
        // (A) まず "パワー条件" をチェックして、
        //     クリアしていなければ0%を返す
        //     （属性ごとに分岐。ゴッドティアなど「パワー条件なし」はスルー）
        if (!CheckPowerCondition(property, power))
        {
            return 0; 
        }

        // (B) 次に "人間状況" ごとの基本値を設定
        int baseChance = GetBaseChanceByCondition(condition);

        // (C) 最後に「特定の属性×状況」で上書き（例: デビル×楽観的=0% など）
        baseChance = OverrideByPropertyAndCondition(property, condition, baseChance);

        // 返却値を 0～100 にクランプ（負になったり100超えたりしないように）
        if (baseChance < 0) baseChance = 0;
        if (baseChance > 100) baseChance = 100;

        return baseChance;
    }


    /// <summary>
    /// 属性ごとの「パワー条件」をチェックし、満たしていればtrue、ダメならfalseを返す。
    /// </summary>
    private bool CheckPowerCondition(SpiritualProperty property, ThePower power)
    {
        switch (property)
        {
            case SpiritualProperty.liminalwhitetile:
                // パワーが普通以上 (>= medium)
                return (power >= ThePower.medium);

            case SpiritualProperty.kindergarden:
                // パワーが高い (== high)
                return (power == ThePower.high);

            case SpiritualProperty.sacrifaith:
                // パワーが普通以上 (>= medium)
                return (power >= ThePower.medium);

            case SpiritualProperty.cquiest:
                // 「低い以上」と書かれていたため (>= low)
                // 低い(low), 普通(medium), 高い(high) はOK。 たるい(lowlow)はNG
                return (power >= ThePower.low);

            case SpiritualProperty.devil:
                // 本文に「パワーが高いと」としか書かれていない→ここでは「高いでないとダメ」と仮定
                return (power == ThePower.high);

            case SpiritualProperty.doremis:
                // パワーが普通以上
                return (power >= ThePower.medium);

            case SpiritualProperty.pillar:
                // パワーが普通以上
                return (power >= ThePower.medium);

            case SpiritualProperty.godtier:
                // 「パワー条件なし」
                return true;

            case SpiritualProperty.baledrival:
                // 「パワーが低い以上」→ ここでは (power >= ThePower.low) と解釈
                return (power >= ThePower.low);

            case SpiritualProperty.pysco:
                // パワーが普通以上
                return (power >= ThePower.medium);

            default:
                // それ以外( none など) は特に定義されていない場合、0%扱い
                return false;
        }
    }


    /// <summary>
    /// 人間状況ごとの「基本値」を返す。
    /// </summary>
    private int GetBaseChanceByCondition(HumanConditionCircumstances condition)
    {
        switch (condition)
        {
            case HumanConditionCircumstances.Angry:
            case HumanConditionCircumstances.Elated:
            case HumanConditionCircumstances.Painful:
            case HumanConditionCircumstances.Confused:
                return 0;

            case HumanConditionCircumstances.Resolved:
                return 7;
            case HumanConditionCircumstances.Optimistic:
                return 2;
            case HumanConditionCircumstances.Normal:
                return 4;
            case HumanConditionCircumstances.Doubtful:
                return 1;

            default:
                // ここに来ることはあまり想定外だが、念のため0%
                return 0;
        }
    }

    /// <summary>
    /// 属性 × 状況 の特別な組み合わせで「上書き」する。
    /// 例：ゴッドティア × 楽観的 => 12% など
    /// </summary>
    private int OverrideByPropertyAndCondition(
        SpiritualProperty property,
        HumanConditionCircumstances condition,
        int baseChance
    )
    {
        switch (property)
        {
            //=======================================
            // ■ゴッドティア (godtier)
            //=======================================
            case SpiritualProperty.godtier:
                // 楽観的なら 12% (通常2%を上書き)
                if (condition == HumanConditionCircumstances.Optimistic)
                {
                    return 12;
                }
                break;

            //=======================================
            // ■デビル (devil)
            //=======================================
            case SpiritualProperty.devil:
                // 楽観的なら 0% (通常2% => 0% 上書き)
                if (condition == HumanConditionCircumstances.Optimistic)
                {
                    return 0;
                }
                break;

            //=======================================
            // ■自己犠牲 (sacrifaith)
            //=======================================
            case SpiritualProperty.sacrifaith:
                // 怒り => 6% (通常 怒りは0% => 6%で上書き)
                if (condition == HumanConditionCircumstances.Angry)
                {
                    return 6;
                }
                break;

            //=======================================
            // ■ドレミス (doremis)
            //=======================================
            case SpiritualProperty.doremis:
                // 疑念 => 14% (通常1% => 14%)
                if (condition == HumanConditionCircumstances.Doubtful)
                {
                    return 14;
                }
                break;

            //=======================================
            // ■支柱 (pillar)
            //=======================================
            case SpiritualProperty.pillar:
                // 辛い => 6% (通常0% => 6%)
                if (condition == HumanConditionCircumstances.Painful)
                {
                    return 6;
                }
                break;

            //=======================================
            // ■ベールドライヴァル (baledrival)
            //=======================================
            case SpiritualProperty.baledrival:
                // 高揚 => 11% (通常0% => 11%)
                if (condition == HumanConditionCircumstances.Elated)
                {
                    return 11;
                }
                break;

            //=======================================
            // ■その他のケース
            //   (サイコパスやキンダーガーデン、リーミナルホワイトタイルなど)
            //   特に指定がなければ、 baseChance のまま
            //=======================================
            default:
                break;
        }

        // 上記で特に上書きされなければ baseChance のまま
        return baseChance;
    }

    //  ==============================================================================================================================
    //                                              刃物即死-復活
    //
    //
    //  ==============================================================================================================================

    /// <summary>
    /// 刃物即死クリティカルで生存するチャンス
    /// </summary>
    void CalculateBladeDeathCriticalSurvivalChance(BaseStates Atker)
    {
        if(NowPower < ThePower.high)return;//パワーが高くないなら発生しないって感じに

        var underBlade = TenDayValues(false).GetValueOrZero(TenDayAbility.Blades);
        var AtkerBlade = Atker.TenDayValues(true).GetValueOrZero(TenDayAbility.Blades);

        //刃物能力を乱数比較して被害者の方のが出たなら、
        if(rollComparison(underBlade,AtkerBlade))
        {   
            var AtkerBladeRate = AtkerBlade;
            if(Atker.NowPower == ThePower.high) AtkerBladeRate *= 1.5f;
            //生き残りHP
            var survivalHP = RandomEx.Shared.NextFloat(1,2.8f) + Mathf.Max(0,underBlade - AtkerBladeRate) * RandomEx.Shared.NextFloat(4,5);

            HP = survivalHP;//HPに生き残ったHP分を代入
        }
    }


    //  ==============================================================================================================================
    //                                              オーバーキル-brokenシステム
    //
    //
    //  ==============================================================================================================================

    const float FINAL_BROKEN_RATE_MACHINE = 33;//機械がオーバーキルされてbrokenする最終判定率
    const float FINAL_BROKEN_RATE_LIFE = 93;//生物がオーバーキルされてbrokenする最終判定率
    /// <summary>
    /// オーバーキルされてbrokenするからの判断
    /// 殺された側がbrokenがtrueになるかの判断です。(だから殺された奴から呼び出そうよ)
    /// </summary>
    void OverKilledBrokenCalc(BaseStates Atker,float OverkillOverflow)
    {
        if(OverkillOverflow <= 0)return;//余剰ダメージが0以下なら終わり

        if(!(MyType == CharacterType.Machine || MyType == CharacterType.Life))
        {
            //被害者の自分が機械でも生物でもないなら発生せずに終わり
            return;
        }
        var OverkillOverflowMax = Atker.GetOverkillOverflowMax();//余剰ダメージの最大値を取得
        var OverkillOverflowPassRate = Atker.GetOverkillOverflowPassRate();//余剰ダメージの通過率を取得

        

        //通過した余剰ダメージ(最大値クランプ
        var OverkillOverflowPass = Mathf.Min(OverkillOverflow * OverkillOverflowPassRate,OverkillOverflowMax);
        var overkillBreakThreshold = _maxhp * OverKillBrokenRate;//オーバーキルされてbrokenする閾値

        if(OverkillOverflowPass <= overkillBreakThreshold) return;//通過した余剰ダメージが閾値を超えなかったら終わり

        //ここまで到達したら発生したが　被害者の種別による判定の発生の計算と　生命なら攻撃者の性質による発生の判定

        //機械なら　33%で完全破壊
        if(MyType == CharacterType.Machine)
        {
            if(rollper(FINAL_BROKEN_RATE_MACHINE))
            {
                broken = true;
            }
        }

        //人間なら攻撃者の性質による発生の判定
        if(MyType == CharacterType.Life)
        {
            //まず攻撃者の種別と、分岐では彼らの性質によりそもそも発生するかの判定

            if(Atker.MyType == CharacterType.Life)//攻撃者が生命なら
            {
                if(Atker.MyImpression != SpiritualProperty.pysco) return;//サイコパスでないなら終わり
            }
            if(Atker.MyType == CharacterType.Machine)//攻撃者が機械なら
            {
                var im = Atker.MyImpression;
                var hc = Atker.NowCondition;
                // サイコパスならOK
                bool isPsyco = im == SpiritualProperty.pysco;

                // ベイルの怒り ⇒ baledrival + Angry 状態
                bool isBaleRivalAngry = im == SpiritualProperty.baledrival && hc == HumanConditionCircumstances.Angry;

                // キンダーの高揚 ⇒ kindergarden + Elated 状態
                bool isKindergardenElated = im == SpiritualProperty.kindergarden && hc == HumanConditionCircumstances.Elated;

                // 上記3パターンのいずれにも当てはまらない場合は発生しない
                if (!(isPsyco || isBaleRivalAngry || isKindergardenElated))
                {
                    return; 
                }
            }
            //それ以外の種別なら生命に対して

            if(rollper(FINAL_BROKEN_RATE_LIFE))
            {
                broken = true;
            }
        }
    }

    /* ------------------------------------------------------------------------------------------------------------------------------------------
     * 処理関数群
     * ------------------------------------------------------------------------------------------------------------------------------------------
     */

    /// <summary>
    /// 呼び出し側の攻撃時の最大余剰ダメージを取得する
    /// </summary>
    float GetOverkillOverflowMax()
    {
        var flowmax = 0f;

        //基本値
        flowmax = TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller) * 2 + TenDayValues(true).GetValueOrZero(TenDayAbility.dokumamusi) * 0.4f;

        switch(MyImpression)//精神属性で分岐　
        {
            case SpiritualProperty.liminalwhitetile:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.FlameBreathingWife) * 0.8f;
                break;
            case SpiritualProperty.kindergarden:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) * 2;
                break;
                
            case SpiritualProperty.sacrifaith:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) * 0.5f + TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight);
                break;
                
            case SpiritualProperty.pysco:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.Raincoat) * 6 * TenDayValues(true).GetValueOrZero(TenDayAbility.UnextinguishedPath);
                break;
                
            case SpiritualProperty.baledrival:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure) * 3;
                break;
                
            case SpiritualProperty.devil:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.NightDarkness) + TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm);
                break;
                
            case SpiritualProperty.cquiest:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.JoeTeeth) * 1.7f - TenDayValues(true).GetValueOrZero(TenDayAbility.ElementFaithPower) * 0.11f;
                break;
                
            case SpiritualProperty.godtier:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.CryoniteQuality);
                break;
                
            case SpiritualProperty.pillar:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.PersonaDivergence) - TenDayValues(true).GetValueOrZero(TenDayAbility.Pilmagreatifull);
                break;
                
            case SpiritualProperty.doremis:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.SpringNap) - TenDayValues(true).GetValueOrZero(TenDayAbility.ElementFaithPower); 
                break;
            case SpiritualProperty.none:
                //noneならそもそも最大余剰ダメージ発生せず
                break;
            default:
                //他の未実装の精神属性を追加し忘れた場合に気づける
                throw new NotImplementedException($"SpiritualProperty {MyImpression} is not handled.");
        }

        return flowmax;//これ自体がクランプ要素だから0クランプいらん
    }
    /// <summary>
    /// 呼び出し側の攻撃時の余剰ダメージの通過率
    /// </summary>
    /// <returns></returns>
    float GetOverkillOverflowPassRate()
    {
        var passRate = 0f;
        switch(NowCondition)
        {
            case HumanConditionCircumstances.Painful:
                if(MyImpression == SpiritualProperty.devil)
                {
                    passRate = 2;
                }else{
                    passRate = 0.43f;
                }
                break;
            case HumanConditionCircumstances.Optimistic:
                if(MyImpression == SpiritualProperty.cquiest)
                {
                    passRate = 1.1f;
                }else{
                    passRate = 1.01f;
                }
                break;
            case HumanConditionCircumstances.Elated:
                passRate = 1.2f;
                break;
            case HumanConditionCircumstances.Resolved:
                passRate = 1.0f;
                break;
            case HumanConditionCircumstances.Angry:
                if(MyImpression == SpiritualProperty.sacrifaith)
                {
                    passRate = 1.5f;
                }else if(MyImpression == SpiritualProperty.devil)
                {
                    passRate = 1.0f;
                }else
                {
                    passRate = 1.3f;
                }
                break;
            case HumanConditionCircumstances.Doubtful:
                if(MyImpression == SpiritualProperty.doremis)
                {
                    passRate = 0.93f;
                }else{
                    passRate = 0.77f;
                }
                break;
            case HumanConditionCircumstances.Confused:
                passRate = 0.7f;
                break;
            case HumanConditionCircumstances.Normal:
                passRate = 0.8f;
                break;
                
        }

        //+-20%入れ替わる
        passRate += RandomEx.Shared.NextFloat(-0.2f,0.2f);

        return passRate;
    }

    //  ==============================================================================================================================
    //                                              割り込みカウンター
    //
    //
    //  ==============================================================================================================================

    /// <summary>
    /// 連続攻撃中の割り込みカウンターが可能かどうかを判定する
    /// </summary>
    private bool TryInterruptCounter(BaseStates attacker)//attacker = 割り込みカウンターの被害者ね
    {
        if(!IsInterruptCounterActive)return false;//割り込みカウンターActiveがfalseなら発動しない

        var skill = attacker.NowUseSkill;
        if(NowPower >= ThePower.medium)//普通のパワー以上で
        {//割り込みカウンターは
            var eneVond = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.Vond);
            var myVond =  TenDayValues(false).GetValueOrZero(TenDayAbility.Vond);
            var plusAtkChance = myVond> eneVond ? myVond - eneVond : 0f;//ヴォンドの差による微加算値
            if(RandomEx.Shared.NextFloat(1) < skill.DEFATK/3 + plusAtkChance*0.01f)
            {
                var mypersonDiver = TenDayValues(false).GetValueOrZero(TenDayAbility.PersonaDivergence);
                var myTentvoid = TenDayValues(false).GetValueOrZero(TenDayAbility.TentVoid);
                var eneSort = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.Sort);
                var eneRain = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.Rain);
                var eneCold = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm);
                var ExVoid = PlayersStates.Instance.ExplosionVoid;
                var counterValue = (myVond + mypersonDiver/(myTentvoid-ExVoid)) * 0.9f;//カウンターする側の特定能力値
                var attackerValue = Mathf.Max(eneSort - eneRain/3,0)+eneCold;//攻撃者の特定能力値


                if(RandomEx.Shared.NextFloat(counterValue+attackerValue) < counterValue &&
                 RandomEx.Shared.NextFloat(1)<0.5f)
                {
                    //まず連続攻撃の無効化
                    attacker.DeleteConsecutiveATK();
                    attacker.IsActiveCancelInSkillACT = true;//スキルの行動を無効化された。
                    
                    //無効化のみ、次のターンで攻撃可能、それに加えて割り込みカウンターのパッシブが加わる。
                    //その三パターンで分かれる。　　最後のパッシブ条件のみ直接割り込みカウンターPassiveの方で設定している。

                    //割り込みカウンターのパッシブ付与しますが、適合するかどうかはそのpassiveの条件次第です。
                    var counterID = 1;
                    ApplyPassiveBufferInBattleByID(counterID);
                    var CounterPower = GetBufferPassiveByID(counterID);
                    if (CanApplyPassive(CounterPower))//適合したら
                    {
                        var attackerCounterPower = attacker.GetPassiveByID(counterID);
                        if(attackerCounterPower != null) //もし攻撃者が割り込みカウンターパッシブなら、
                        {
                            //攻撃者の割り込みカウンターパッシブのパワー+1で生成
                            CounterPower.SetPassivePower(attackerCounterPower.PassivePower +1);
                        }
                    }

                    //次のターンで攻撃、つまり先約リストの予約を判定する。　
                    if(HasCharacterType(CharacterType.Life))
                    {//生命なら、必ず反撃可能
                        
                        //割り込みカウンターの反撃は割り込んだ際の、敵の攻撃の防御無視率の方が、反撃スキルの防御無視率より多ければ、
                        // 食らいそうになった敵スキルの防御無視率をそのまま利用する。
                        var CounterDEFATK = skill.DEFATK;
                        

                        //攻撃を食らった際、中断不可能なカウンターまたはfreezeConecutiveの場合、武器スキルでしか返せない。
                        var isfreeze = false;
                        if(NowUseSkill.NowConsecutiveATKFromTheSecondTimeOnward() && NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive) ||
                        NowUseSkill.IsTriggering) 
                        {
                            NowUseSkill = NowUseWeapon.WeaponSkill;
                            isfreeze = true;
                        }
                        manager.Acts.Add(this,manager.GetCharacterFaction(this),"割り込みカウンター",null,isfreeze,null,CounterDEFATK);//通常の行動予約 
                    }

                    //無効化は誰でも可能です　以下のtrueを返して、呼び出し側で今回の攻撃の無効化は行います。
                    return true;
                }
            }
        }
        return false;
    }


}
