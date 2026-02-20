using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using static TenDayAbilityPosition;
using System.Linq;
//戦闘のイベント系統
public abstract partial class BaseStates
{
    //  ==============================================================================================================================
    //                                             乖離スキル使用による十日能力値減少
    //                                      スキルの印象構造(精神属性)　と　キャラクターのデフォルト精神属性が一定以上離れている場合　
    //                      　　　　　　　キャラクターの十日能力が下がる可能性のある処理。　　　　Attack.cs
    //  ==============================================================================================================================

    /// <summary>
    /// 乖離したスキルを利用したことによる苦悩での十日能力値下降処理
    /// </summary>
    protected void ResolveDivergentSkillOutcome()
    {
        //まずバトル内でのスキル実行回数が一定以上で発生
        if(AllSkillDoCountInBattle < 7) return;

        //終了時の精神属性による発生確率の分岐
        switch(MyImpression)
        {
            case SpiritualProperty.LiminalWhiteTile:
                // リーミナルホワイトタイル 0%
                return; // 常に発生しない
                
            case SpiritualProperty.Kindergarten:
                // キンダーガーデン 95%
                if(!rollper(95f)) return;
                break;
                
            case SpiritualProperty.Sacrifaith:
            case SpiritualProperty.Cquiest:
            case SpiritualProperty.Devil:
            case SpiritualProperty.Doremis:
            case SpiritualProperty.Pillar:
                // 自己犠牲・シークイエスト・デビル・ドレミス・支柱は全て100%
                // 100%発生するので何もしない（returnしない）
                break;
                
            case SpiritualProperty.GodTier:
                // ゴッドティア 50%
                if(!rollper(50f)) return;
                break;
                
            case SpiritualProperty.BaleDrival:
                // ベールドライヴァル 70%
                if(!rollper(70f)) return;
                break;
                
            case SpiritualProperty.Psycho:
                // サイコパス 20%
                if(!rollper(20f)) return;
                break;
                
            default:
                // その他の精神属性の場合はデフォルト処理
                break;
        }
    
        //乖離したスキルが一定％以上全体実行に対して使用されていたら
        var DivergenceCount = 0;
        foreach(var skill in DidActionSkillDatas)
        {
            if(skill.IsDivergence)
            {
                DivergenceCount++;
            }
        }
        //特定％以上乖離スキルがあったら発生する。
        if(AllSkillDoCountInBattle * 0.71 > DivergenceCount) return;

        //減少する十日能力値の計算☆☆☆

        //最後に使った乖離スキル
        BaseSkill lastDivergenceSkill = null;
        for(var i = DidActionSkillDatas.Count - 1; i >= 0; i--)//最後に使った乖離スキルに辿り着くまでループ
        {
            if(DidActionSkillDatas[i].IsDivergence)
            {
                lastDivergenceSkill = DidActionSkillDatas[i].Skill;
                break;
            }
        }

        //乖離スキルの全種類の印象構造の平均
        
        //まず全乖離スキルを取得する　同じのは重複しないようにhashset
        var DivergenceSkills = new HashSet<BaseSkill>();
        foreach(var skill in DidActionSkillDatas)
        {
            if(skill.IsDivergence)
            {
                DivergenceSkills.Add(skill.Skill);
            }
        }
        //全乖離スキルの印象構造の平均値
        var averageImpression = TenDayAbilityDictionary.CalculateAverageTenDayValues(DivergenceSkills);

        //「最後に使った乖離スキル」と「乖離スキル全体の平均値」の平均値を求める
        var DecreaseTenDayValue = TenDayAbilityDictionary.CalculateAverageTenDayDictionary(new[] { lastDivergenceSkill.TenDayValues(), averageImpression });
        DecreaseTenDayValue *= 1.2f;//定数で微増

        //自分の十日能力から減らす
        _baseTenDayValues -= DecreaseTenDayValue;
        Debug.Log($"乖離スキルの影響で、{CharacterName}の十日能力が減少しました。- {DecreaseTenDayValue}:現在値は{_baseTenDayValues}");
    }
    /// <summary>
    /// 現在のスキルが乖離してるかどうかを返す
    /// </summary>
    public bool GetIsSkillDivergence()
    {
        if(DefaultImpression == SpiritualProperty.None) 
        {
            Debug.Log($"{CharacterName}の{NowUseSkill.SkillName}-"
            +"「DefaultImpressionがnoneなら乖離判定は行われません。」none精神属性互換の十日能力とかないからね");
            return false;
        }

        //判定するスキル印象構造の種類数を取得  1クランプする。
        var NeedJudgementSkillTenDayCount = Mathf.Max(1, (int)(NowUseSkill.TenDayValues().Count * 0.8 -1));

        //判定するスキル印象構造を取得して
        var SuggestionJudgementSkillTenDay =new TenDayAbilityDictionary(NowUseSkill.TenDayValues());
        //キーリストを取得
        var SuggestionJudgementSkillTenDayKeys = SuggestionJudgementSkillTenDay.Keys.ToArray();
        Debug.Log($"(使用スキルの乖離判定)判定するスキル印象構造の種類数 : {SuggestionJudgementSkillTenDayKeys.Length}");    
        if(SuggestionJudgementSkillTenDayKeys.Length <= 0)
        {
            Debug.Log("(使用スキルの乖離判定)判定するスキル印象構造の種類数が0以下、つまりスキルに印象構造がセットされてないので、GetIsSkillDivergenceはfalseを返し終了します。");
            return false;
        }
        //キーリストをシャッフルする
        RandomSource.Shuffle(SuggestionJudgementSkillTenDayKeys);

        //判定する種類分判定リストに代入
        var JudgementSkillTenDays =new HashSet<TenDayAbility>();
        for(var i = 0; i < NeedJudgementSkillTenDayCount; i++)
        {
            Debug.Log($"{i} : {SuggestionJudgementSkillTenDayKeys[i]} スキルが乖離してるかどうかを判定するリストに代入");
            var key = SuggestionJudgementSkillTenDayKeys[i];
            JudgementSkillTenDays.Add(key);
        }

        //判定印象構造とデフォルト精神属性互換の精神属性全て同士の距離の平均を出す。
        var AllAverageBetweenSkillTenAndDefaultImpressionDistance = 0f;//判定印象構造とデフォルト精神属性互換の精神属性全て同士の距離の平均の印象構造分全て
        foreach(var skillTen in JudgementSkillTenDays)//スキルの判定印象構造で回す
        {
            var AllBetweenSkillTenAndDefaultImpressionDistance = 0f;
            if(!SpritualTenDayAbilitysMap.ContainsKey(DefaultImpression)) return false;
            foreach(var defaultImpTen in SpritualTenDayAbilitysMap[DefaultImpression])//デフォルト精神属性互換の精神属性全てで回す
            {
                //距離を足す
                AllBetweenSkillTenAndDefaultImpressionDistance += GetDistance(skillTen, defaultImpTen);
            }
            //デフォルト精神属性互換の十日能力の数で割って、平均を出す
            AllAverageBetweenSkillTenAndDefaultImpressionDistance//その平均距離を総距離として加算する
             += AllBetweenSkillTenAndDefaultImpressionDistance / SpritualTenDayAbilitysMap[DefaultImpression].Count;
        }
        //平均の平均を出す　判定印象構造とデフォルト精神属性互換の精神属性全て同士の距離の平均の平均
        var AvarageAllAverageBetweenSkillTenAndDefaultImpressionDistance 
        = AllAverageBetweenSkillTenAndDefaultImpressionDistance / JudgementSkillTenDays.Count;

        //この平均の平均が特定の定数より多い、　離れているのなら、乖離したスキルとみなす。
        return AvarageAllAverageBetweenSkillTenAndDefaultImpressionDistance >= 8;
    }

    //  ==============================================================================================================================
    //                                             精神乖離
    //                                      精神HPがHPと乖離した場合　パッシブやHP変化などが起こる
    //
    //  ==============================================================================================================================

    /// <summary>
    /// 実HPに比べて何倍離れているのだろうか。
    /// </summary>
    /// <returns></returns>
    public float GetMentalDivergenceThreshold()
    {
        var ExtraValue = (TenDayValues(false).GetValueOrZero(TenDayAbility.NightDarkness) -
         TenDayValues(false).GetValueOrZero(TenDayAbility.KereKere)) * 0.01f;//0クランプいらない
        var EnokunagiValue = TenDayValues(false).GetValueOrZero(TenDayAbility.Enokunagi) * 0.005f;
        switch (NowCondition)
        {
            case Demeanor.Angry:
                return 0.47f + ExtraValue;
            case Demeanor.Elated:
                return 2.6f+ ExtraValue;
            case Demeanor.Painful:
                return 0.6f+ ExtraValue;
            case Demeanor.Confused:
                return 0.3f+ ExtraValue;
            case Demeanor.Resolved:
                return 1.2f+ ExtraValue;
            case Demeanor.Optimistic:
                return 1.4f+ ExtraValue;
            case Demeanor.Normal:
                return 0.9f+ ExtraValue;
            case Demeanor.Doubtful:
                return 0.7f+ ExtraValue - EnokunagiValue;//疑念だとエノクナギの影響で乖離しやすくなっちゃうよ
            default:
                return 0f;
        }
    }
    /// <summary>
    /// 精神HPの乖離が起こるまでの発動持続ターン最大値を取得
    /// </summary>
    int GetMentalDivergenceMaxCount()
    {
        if(TenDayValues(false).GetValueOrZero(TenDayAbility.NightDarkness)> 0)//ゼロ除算対策
        {
            var maxCount = (int)((TenDayValues(false).GetValueOrZero(TenDayAbility.SpringNap) - TenDayValues(false).GetValueOrZero(TenDayAbility.TentVoid ) / 2) / TenDayValues(false).GetValueOrZero(TenDayAbility.NightDarkness));
            if(maxCount > 0)return maxCount;//0より大きければ返す
        }
        return 0 ;

    }
    /// <summary>
    /// 精神HPと実HPの乖離発生処理全般
    /// </summary>
    void MentalDiverGence()
    {
        // 乖離率は 実HPに対する精神HPの割合で決まる。
        float divergenceRatio = Mathf.Abs(MentalHP - HP) / HP;

        if(divergenceRatio > GetMentalDivergenceThreshold())//乖離してるなら
        {
            if(_mentalDivergenceCount >= GetMentalDivergenceMaxCount())//カウントが最大値を超えたら
            {
                _mentalDivergenceRefilCount = GetMentalDivergenceRefulMaxCount();//再度行われないようにカウント開始
                //精神HPが現在HPより上に乖離してるなら アッパー系の乖離メゾット
                if(MentalHP > HP)
                {
                    MentalUpperDiverGenceEffect();
                }else
                {//精神HPが現在HPより下に乖離してるなら ダウナ系の乖離メゾット
                    MentalDownerDiverGenceEffect();
                }
            }

            if(_mentalDivergenceRefilCount <= 0)//再充填カウントがセットされてないので、乖離が発生していないなら持続カウントをプラス
            {
                _mentalDivergenceCount++;//持続カウントをプラス
            }
        }else
        {
            _mentalDivergenceCount = 0;//乖離から外れたらカウントをリセット
        }

    }
    /// <summary>
    /// 精神HPの乖離の再充填までのターン数を取得
    /// </summary>
    int GetMentalDivergenceRefulMaxCount()
    {
        var refil = TenDayValues(false).GetValueOrZero(TenDayAbility.TentVoid) * 3 - TenDayValues(false).GetValueOrZero(TenDayAbility.Miza) / 4 * TenDayValues(false).GetValueOrZero(TenDayAbility.Smiler);
        if(refil < 0)return 0;
        return (int)refil;
    }
    /// <summary>
    /// 再充填カウントがゼロより多いならばカウントダウンし、そうでなければtrue、つまり再充填されている。
    /// </summary>
    /// <returns></returns>
    bool IsMentalDiverGenceRefilCountDown()
    {
        if(_mentalDivergenceRefilCount > 0)
        {
            _mentalDivergenceRefilCount--;
            return true;
        }
        return false;//カウントは終わっている。
    }
    int _mentalDivergenceRefilCount = 0;
    int _mentalDivergenceCount = 0;

    /// <summary>
    /// 精神HPのアッパー乖離で起こる変化
    /// </summary>
    protected virtual void MentalUpperDiverGenceEffect()
    {//ここに書かれるのは基本効果
        ApplyPassiveBufferInBattleByID(4);//アッパーのパッシブを付与
    }
    /// <summary>
    /// 精神HPのダウナー乖離で起こる変化
    /// </summary>
    protected virtual void MentalDownerDiverGenceEffect()
    {//ここに書かれるのは基本効果
        
        if(MyType == CharacterType.TLOA)
        {
            HP = _hp * 0.76f;
        }else
        {//TLOA以外の種別なら
            ApplyPassiveBufferInBattleByID(3);//強制ダウナーのパッシブを付与
            if(rollper(50))
            {
                Power1Down();//二分の一でパワーが下がる。
            }
        }
    }
    
    
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
    private bool CheckPowerCondition(SpiritualProperty property, PowerLevel power)
    {
        switch (property)
        {
            case SpiritualProperty.LiminalWhiteTile:
                // パワーが普通以上 (>= medium)
                return (power >= PowerLevel.Medium);

            case SpiritualProperty.Kindergarten:
                // パワーが高い (== high)
                return (power == PowerLevel.High);

            case SpiritualProperty.Sacrifaith:
                // パワーが普通以上 (>= medium)
                return (power >= PowerLevel.Medium);

            case SpiritualProperty.Cquiest:
                // 「低い以上」と書かれていたため (>= low)
                // 低い(low), 普通(medium), 高い(high) はOK。 たるい(VeryLow)はNG
                return (power >= PowerLevel.Low);

            case SpiritualProperty.Devil:
                // 本文に「パワーが高いと」としか書かれていない→ここでは「高いでないとダメ」と仮定
                return (power == PowerLevel.High);

            case SpiritualProperty.Doremis:
                // パワーが普通以上
                return (power >= PowerLevel.Medium);

            case SpiritualProperty.Pillar:
                // パワーが普通以上
                return (power >= PowerLevel.Medium);

            case SpiritualProperty.GodTier:
                // 「パワー条件なし」
                return true;

            case SpiritualProperty.BaleDrival:
                // 「パワーが低い以上」→ ここでは (power >= PowerLevel.Low) と解釈
                return (power >= PowerLevel.Low);

            case SpiritualProperty.Psycho:
                // パワーが普通以上
                return (power >= PowerLevel.Medium);

            default:
                // それ以外( none など) は特に定義されていない場合、0%扱い
                return false;
        }
    }


    /// <summary>
    /// 人間状況ごとの「基本値」を返す。
    /// </summary>
    private int GetBaseChanceByCondition(Demeanor condition)
    {
        switch (condition)
        {
            case Demeanor.Angry:
            case Demeanor.Elated:
            case Demeanor.Painful:
            case Demeanor.Confused:
                return 0;

            case Demeanor.Resolved:
                return 7;
            case Demeanor.Optimistic:
                return 2;
            case Demeanor.Normal:
                return 4;
            case Demeanor.Doubtful:
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
        Demeanor condition,
        int baseChance
    )
    {
        switch (property)
        {
            //=======================================
            // ■ゴッドティア (GodTier)
            //=======================================
            case SpiritualProperty.GodTier:
                // 楽観的なら 12% (通常2%を上書き)
                if (condition == Demeanor.Optimistic)
                {
                    return 12;
                }
                break;

            //=======================================
            // ■デビル (Devil)
            //=======================================
            case SpiritualProperty.Devil:
                // 楽観的なら 0% (通常2% => 0% 上書き)
                if (condition == Demeanor.Optimistic)
                {
                    return 0;
                }
                break;

            //=======================================
            // ■自己犠牲 (Sacrifaith)
            //=======================================
            case SpiritualProperty.Sacrifaith:
                // 怒り => 6% (通常 怒りは0% => 6%で上書き)
                if (condition == Demeanor.Angry)
                {
                    return 6;
                }
                break;

            //=======================================
            // ■ドレミス (Doremis)
            //=======================================
            case SpiritualProperty.Doremis:
                // 疑念 => 14% (通常1% => 14%)
                if (condition == Demeanor.Doubtful)
                {
                    return 14;
                }
                break;

            //=======================================
            // ■支柱 (Pillar)
            //=======================================
            case SpiritualProperty.Pillar:
                // 辛い => 6% (通常0% => 6%)
                if (condition == Demeanor.Painful)
                {
                    return 6;
                }
                break;

            //=======================================
            // ■ベールドライヴァル (BaleDrival)
            //=======================================
            case SpiritualProperty.BaleDrival:
                // 高揚 => 11% (通常0% => 11%)
                if (condition == Demeanor.Elated)
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
        if(NowPower < PowerLevel.High)return;//パワーが高くないなら発生しないって感じに

        var underBlade = TenDayValues(false).GetValueOrZero(TenDayAbility.Blades);
        var AtkerBlade = Atker.TenDayValues(true).GetValueOrZero(TenDayAbility.Blades);

        //刃物能力を乱数比較して被害者の方のが出たなら、
        if(rollComparison(underBlade,AtkerBlade))
        {   
            var AtkerBladeRate = AtkerBlade;
            if(Atker.NowPower == PowerLevel.High) AtkerBladeRate *= 1.5f;
            //生き残りHP
            var survivalHP = RandomSource.NextFloat(1,2.8f) + Mathf.Max(0,underBlade - AtkerBladeRate) * RandomSource.NextFloat(4,5);

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
                if(Atker.MyImpression != SpiritualProperty.Psycho) return;//サイコパスでないなら終わり
            }
            if(Atker.MyType == CharacterType.Machine)//攻撃者が機械なら
            {
                var im = Atker.MyImpression;
                var hc = Atker.NowCondition;
                // サイコパスならOK
                bool isPsyco = im == SpiritualProperty.Psycho;

                // ベイルの怒り ⇒ BaleDrival + Angry 状態
                bool isBaleRivalAngry = im == SpiritualProperty.BaleDrival && hc == Demeanor.Angry;

                // キンダーの高揚 ⇒ Kindergarten + Elated 状態
                bool isKindergardenElated = im == SpiritualProperty.Kindergarten && hc == Demeanor.Elated;

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
        flowmax = TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller) * 2 + TenDayValues(true).GetValueOrZero(TenDayAbility.Dokumamusi) * 0.4f;

        switch(MyImpression)//精神属性で分岐　
        {
            case SpiritualProperty.LiminalWhiteTile:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.FlameBreathingWife) * 0.8f;
                break;
            case SpiritualProperty.Kindergarten:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) * 2;
                break;
                
            case SpiritualProperty.Sacrifaith:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) * 0.5f + TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight);
                break;
                
            case SpiritualProperty.Psycho:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.Raincoat) * 6 * TenDayValues(true).GetValueOrZero(TenDayAbility.UnextinguishedPath);
                break;
                
            case SpiritualProperty.BaleDrival:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure) * 3;
                break;
                
            case SpiritualProperty.Devil:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.NightDarkness) + TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm);
                break;
                
            case SpiritualProperty.Cquiest:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.JoeTeeth) * 1.7f - TenDayValues(true).GetValueOrZero(TenDayAbility.ElementFaithPower) * 0.11f;
                break;
                
            case SpiritualProperty.GodTier:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.CryoniteQuality);
                break;
                
            case SpiritualProperty.Pillar:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.PersonaDivergence) - TenDayValues(true).GetValueOrZero(TenDayAbility.Pilmagreatifull);
                break;
                
            case SpiritualProperty.Doremis:
                flowmax += TenDayValues(true).GetValueOrZero(TenDayAbility.SpringNap) - TenDayValues(true).GetValueOrZero(TenDayAbility.ElementFaithPower); 
                break;
            case SpiritualProperty.None:
            case (SpiritualProperty)0: // 未設定のデフォルト値
                //noneや未設定ならそもそも最大余剰ダメージ発生せず
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
            case Demeanor.Painful:
                if(MyImpression == SpiritualProperty.Devil)
                {
                    passRate = 2;
                }else{
                    passRate = 0.43f;
                }
                break;
            case Demeanor.Optimistic:
                if(MyImpression == SpiritualProperty.Cquiest)
                {
                    passRate = 1.1f;
                }else{
                    passRate = 1.01f;
                }
                break;
            case Demeanor.Elated:
                passRate = 1.2f;
                break;
            case Demeanor.Resolved:
                passRate = 1.0f;
                break;
            case Demeanor.Angry:
                if(MyImpression == SpiritualProperty.Sacrifaith)
                {
                    passRate = 1.5f;
                }else if(MyImpression == SpiritualProperty.Devil)
                {
                    passRate = 1.0f;
                }else
                {
                    passRate = 1.3f;
                }
                break;
            case Demeanor.Doubtful:
                if(MyImpression == SpiritualProperty.Doremis)
                {
                    passRate = 0.93f;
                }else{
                    passRate = 0.77f;
                }
                break;
            case Demeanor.Confused:
                passRate = 0.7f;
                break;
            case Demeanor.Normal:
                passRate = 0.8f;
                break;
                
        }

        //+-20%入れ替わる
        passRate += RandomSource.NextFloat(-0.2f,0.2f);

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
        if(NowPower >= PowerLevel.Medium)//普通のパワー以上で
        {//割り込みカウンターは
            var eneVond = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.Vond);
            var myVond =  TenDayValues(false).GetValueOrZero(TenDayAbility.Vond);
            var plusAtkChance = myVond> eneVond ? myVond - eneVond : 0f;//ヴォンドの差による微加算値
            if(RandomSource.NextFloat(1) < skill.DEFATK/3 + plusAtkChance*0.01f)
            {
                var mypersonDiver = TenDayValues(false).GetValueOrZero(TenDayAbility.PersonaDivergence);
                var myTentvoid = TenDayValues(false).GetValueOrZero(TenDayAbility.TentVoid);
                var eneSort = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.Sort);
                var eneRain = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.Rain);
                var eneCold = attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm);
                var ExVoid = Tuning?.ExplosionVoidValue ?? 10f;
                var counterValue = (myVond + mypersonDiver/(myTentvoid-ExVoid)) * 0.9f;//カウンターする側の特定能力値
                var attackerValue = Mathf.Max(eneSort - eneRain/3,0)+eneCold;//攻撃者の特定能力値


                if(RandomSource.NextFloat(counterValue+attackerValue) < counterValue &&
                 RandomSource.NextFloat(1)<0.5f)
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
