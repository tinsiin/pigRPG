using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;

//ダメージシステム統合ファイル
public abstract partial class BaseStates
{


    
    // =====================
    // 戦闘外適用/ダメージ用ポリシー
    // =====================
    public class DamageOptions
    {
        public bool AimStyleClamp = false;
        public bool BaseRandomVariance = false; // 基礎山形補正
        public bool Frenzy = false;
        public bool Adaptation = false;
        public bool BladeInstantDeath = false;
        public bool Resonance = false;
        public bool PhysicalResistance = true;
        // 戦闘外Damage()でダメージ直前のパッシブ適用を行うか（BM依存パッシブの抑止用）
        public bool BeforeDamagePassives = true;
        public bool PassivesReduction = true; // パッシブによる減衰率
        public bool TLOReduction = true;
        public bool BarrierLayers = true;
        public bool CantKillClamp = true;
        public bool DontDamageClamp = true;
        public bool MentalDamage = true;
        public bool UseHitMultiplier = true; // 命中段階による補正

        public DamageOptions Clone()
        {
            return (DamageOptions)MemberwiseClone();
        }
    }

    public class SkillApplyPolicy
    {
        // 戦闘外で命中/回避を使うか
        public bool UseHitEvade = false;
        public bool UseAllyEvade = false;
        // 友好系も命中でゲートするか
        public bool GateFriendlyByHit = false;
        // バッファ即時コミット
        public bool CommitBuffersImmediately = true;
        // 復活時のパーティ連鎖を使うか
        public bool UsePartyAngelChain = false;
        // ダメージ詳細
        public DamageOptions Damage = new DamageOptions();

        public SkillApplyPolicy Clone()
        {
            return new SkillApplyPolicy
            {
                UseHitEvade = UseHitEvade,
                UseAllyEvade = UseAllyEvade,
                GateFriendlyByHit = GateFriendlyByHit,
                CommitBuffersImmediately = CommitBuffersImmediately,
                UsePartyAngelChain = UsePartyAngelChain,
                Damage = Damage?.Clone() ?? new DamageOptions()
            };
        }

        // プリセット
        static SkillApplyPolicy _outOfBattleDefault;
        public static SkillApplyPolicy OutOfBattleDefault
        {
            get
            {
                if (_outOfBattleDefault == null)
                {
                    _outOfBattleDefault = new SkillApplyPolicy
                    {
                        UseHitEvade = false,
                        UseAllyEvade = false,
                        GateFriendlyByHit = false,
                        CommitBuffersImmediately = true,
                        UsePartyAngelChain = false,
                        Damage = new DamageOptions
                        {
                            AimStyleClamp = false,
                            BaseRandomVariance = false,
                            Frenzy = false,
                            Adaptation = false,
                            BladeInstantDeath = false,
                            Resonance = false,
                            PhysicalResistance = true,
                            BeforeDamagePassives = false,
                            PassivesReduction = true,
                            TLOReduction = true,
                            BarrierLayers = true,
                            CantKillClamp = true,
                            DontDamageClamp = true,
                            MentalDamage = true,
                            UseHitMultiplier = true,
                        }
                    };
                }
                return _outOfBattleDefault.Clone();
            }
        }

        static SkillApplyPolicy _battleLike;
        public static SkillApplyPolicy BattleLike
        {
            get
            {
                if (_battleLike == null)
                {
                    _battleLike = new SkillApplyPolicy
                    {
                        UseHitEvade = true,
                        UseAllyEvade = true,
                        GateFriendlyByHit = true,
                        CommitBuffersImmediately = true, // 戦闘外なので即コミット
                        UsePartyAngelChain = true,
                        Damage = new DamageOptions
                        {
                            AimStyleClamp = true,
                            BaseRandomVariance = true,
                            Frenzy = true,
                            Adaptation = true,
                            BladeInstantDeath = true,
                            Resonance = true,
                            PhysicalResistance = true,
                            BeforeDamagePassives = true,
                            PassivesReduction = true,
                            TLOReduction = true,
                            BarrierLayers = true,
                            CantKillClamp = true,
                            DontDamageClamp = true,
                            MentalDamage = true,
                            UseHitMultiplier = true,
                        }
                    };
                }
                return _battleLike.Clone();
            }
        }
    }


    //  ==============================================================================================================================
    //                                              ダメージ記録
    //
    //
    //  ==============================================================================================================================

    /// <summary>
    /// キャラクターの被害記録
    /// </summary>
    public List<DamageData> damageDatas;
    /// <summary>
    /// キャラクターの対ひとりごとの行動記録
    /// </summary>
    public List<ACTSkillDataForOneTarget> ActDoOneSkillDatas;
    /// <summary>
    /// 直近の行動記録
    /// </summary>
    public ACTSkillDataForOneTarget RecentACTSkillData => ActDoOneSkillDatas[ActDoOneSkillDatas.Count - 1];
    /// <summary>
    /// アクション事のスキルデータ AttackChara単位で記録　= スキル一回に対して
    /// </summary>
    public List<ActionSkillData> DidActionSkillDatas = new();
    /// <summary>
    /// bm内にスキル実行した回数。
    /// </summary>
    protected int AllSkillDoCountInBattle => DidActionSkillDatas.Count;

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
            case SpiritualProperty.liminalwhitetile:
                // リーミナルホワイトタイル 0%
                return; // 常に発生しない
                
            case SpiritualProperty.kindergarden:
                // キンダーガーデン 95%
                if(!rollper(95f)) return;
                break;
                
            case SpiritualProperty.sacrifaith:
            case SpiritualProperty.cquiest:
            case SpiritualProperty.devil:
            case SpiritualProperty.doremis:
            case SpiritualProperty.pillar:
                // 自己犠牲・シークイエスト・デビル・ドレミス・支柱は全て100%
                // 100%発生するので何もしない（returnしない）
                break;
                
            case SpiritualProperty.godtier:
                // ゴッドティア 50%
                if(!rollper(50f)) return;
                break;
                
            case SpiritualProperty.baledrival:
                // ベールドライヴァル 70%
                if(!rollper(70f)) return;
                break;
                
            case SpiritualProperty.pysco:
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
    /// 現在持ってる対象者のボーナスデータ
    /// </summary>
    public TargetBonusDatas TargetBonusDatas = new();

    /// <summary>
    /// 直近の行動記録
    /// </summary>
    public ACTSkillDataForOneTarget RecentSkillData => ActDoOneSkillDatas[ActDoOneSkillDatas.Count - 1];
    /// <summary>
    /// 直近の被害記録
    /// </summary>
    public DamageData RecentDamageData => damageDatas[damageDatas.Count - 1];

    /// <summary>
    /// そのキャラクターを殺すまでに与えたダメージ
    /// </summary>
    Dictionary<BaseStates, float> DamageDealtToEnemyUntilKill = new Dictionary<BaseStates, float>();
    /// <summary>
    /// キャラクターを殺すまでに与えるダメージを記録する辞書に記録する
    /// </summary>
    /// <param name="dmg"></param>
    /// <param name="target"></param>
    void RecordDamageDealtToEnemyUntilKill(float dmg,BaseStates target)//戦闘開始時にそのキャラクターを殺すまでに与えたダメージを記録する辞書に記録する
    {
        if (DamageDealtToEnemyUntilKill.ContainsKey(target))
        {
            DamageDealtToEnemyUntilKill[target] += dmg;
        }
        else
        {
            DamageDealtToEnemyUntilKill[target] = dmg;
        }
    }

    //  ==============================================================================================================================
    //                                              ダメージ計算-補正関数群
    //
    //                                                  単独で完結するもの
    //  ==============================================================================================================================
    
    /// <summary>
    /// 即死刃物クリティカル
    /// </summary>
    bool BladeCriticalCalculation(ref StatesPowerBreakdown dmg, ref StatesPowerBreakdown resonanceDmg, BaseStates Atker, BaseSkill skill)
    {
        var LiveHP = HP - dmg.Total;//もし即死が発生したときに、ダメージに加算される即死に足りないfloat
        var atkerBlade = Atker.TenDayValues(true).GetValueOrZero(TenDayAbility.Blades);
        var UnderBlade = TenDayValues(false).GetValueOrZero(TenDayAbility.Blades);
        var UnderPower = TenDayValuesSum(false);

        //まずしきい値発生から
        var CriticalHPThreshold = Mathf.Min(atkerBlade/150f,1f) * (5/12) *100;
        if(rollper(CriticalHPThreshold))
        {
            //攻撃者と被害者の刃物能力を比較してクリティカル発生率の計算
            var threshold = atkerBlade / UnderPower * 100;
            if(rollper(threshold))
            {
                //クリティカル発生
                dmg.TenDayAdd(TenDayAbility.Blades,LiveHP);//刃物に差分ダメージを追加 = 即死
                resonanceDmg.TenDayAdd(TenDayAbility.Blades, LiveHP);//思えダメージ用にも
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 基礎山型分布によるダメージ補正
    /// 返り値で攻撃が乱れたかどうか -15%以下なら乱れたのでTrueが返ります。
    /// </summary>
    bool GetBaseCalcDamageWithPlusMinus22Percent(ref StatesPowerBreakdown baseDamage)
    {
        // 1) 8d5501 を振る（8回ランダム）
        int diceSum = 0;
        for (int i = 0; i < 8; i++)
        {
            // Range(1, 5502) => [1..5501] の整数
            diceSum += RandomEx.Shared.NextInt(1, 5502);
        }

        // 2) 平均(22008)を引いて、0.00001f を掛ける
        //    → -0.22 ～ +0.22 (±22%)
        float offset = (diceSum - 22008) * 0.00001f;

        // 3) baseDamage に対して (1 + offset) 倍する
        //    → (1 - 0.22)～(1 + 0.22) = 0.78～1.22 倍
        StatesPowerBreakdown finalDamage = baseDamage * (1f + offset);

        // 5) float で返す（丸めたくないのでそのまま）
        baseDamage = finalDamage;//ダメージに代入。

        return offset <= -0.15f;//-15%以下なら乱れた
    }
    /// <summary>
    /// ダメージを渡し、がむしゃらの補正をかけて返す
    /// </summary>
    StatesPowerBreakdown GetFrenzyBoost(BaseStates atker,StatesPowerBreakdown dmg)
    {
        var boost =1.0f;
        var skill = atker.NowUseSkill;
        if(skill.NowConsecutiveATKFromTheSecondTimeOnward())//2回目以降の連続攻撃なら
        {
            var StrongFootEye = (EYE() + AGI()) /2f;
            var WeekEye = atker.EYE();
            var boostCoef = 0f;//ブースト係数

            if(StrongFootEye > WeekEye)//ちゃんと被害者側の命中回避平均値が攻撃者の命中より高い場合に限定する
            {
                boostCoef = Mathf.Floor((StrongFootEye.Total - WeekEye.Total) / 5);//十日能力の記録の必要がないので、totalを使う。
                boost += boostCoef * 0.01f;
                for(int i =0;i<skill.ATKCountUP-1;i++)//初回=単回攻撃の恐れがある場合は、がむしゃらは発動しないので、二回目から一回ずつ乗算されるようにしたいから-1
                {
                    dmg *= boost;
                }
            }
        }
        return dmg;//連続攻撃でないなら、そのまま返す
    }

    /// <summary>
    /// ダメージを物理耐性で減衰
    /// </summary>
    StatesPowerBreakdown ApplyPhysicalResistance(StatesPowerBreakdown dmg, BaseSkill skill)
    {
        switch(skill.SkillPhysical)
        {
            case PhysicalProperty.dishSmack:
                dmg *= DishSmackRsistance;
                break;
            case PhysicalProperty.heavy:
                dmg *= HeavyResistance;
                break;
            case PhysicalProperty.volten:
                dmg *= voltenResistance;
                break;
            //noneは物理耐性の計算無し
        }        
        return dmg;
    }
    /// <summary>
    /// 殺せないスキルの場合のクランプ
    /// </summary>
    void CantKillSkillClamp(BaseStates Atker, BaseSkill skill)
    {
        //もし攻撃者がTLOAスキルなら
        if(skill.IsTLOA)
        {
            if(Atker.HasPassive(5))//攻撃者が「TLOAではとどめがさせない」パッシブを持ってたら1割まで
            {
                HP = Mathf.Max(HP,MaxHP * 0.1f);//10%までしか減らせない
            }
            else//それ以外は3.4%まで
            {
                HP = Mathf.Max(HP,MaxHP * 0.034f);//3.4%までしか減らせない
            }
        }

        //もし攻撃者が殺せないスキルなら
        if(skill.Cantkill)
        {
            HP = Mathf.Max(HP,1);//1までしか減らせない
        }
    }
    
    /// <summary>
    /// TLOAスキルの威力減衰
    /// 呼び出し側のダメージを受ける自分のHPの割合が条件　詳しくはTLOAスキル　を参照
    /// </summary>
    public void ApplyTLOADamageReduction(ref StatesPowerBreakdown damage,ref StatesPowerBreakdown resDamage)
    {
        //HPが38%以下ならTLOAは0.7倍まで減衰する。
        if(this.HP / this.MaxHP < 0.38f)
        {
            damage *= 0.7f;
            resDamage *= 0.7f;
        }

        return;
    }

    //  ==============================================================================================================================
    //                                              防ぎ方、狙い流れ　AImStyle
    //
    //
    //  ==============================================================================================================================

    /* ---------------------------------
     * 不一致によるクランプ処理
     * --------------------------------- 
     */

    /// <summary>
    /// 防ぎ方(AimStyle)の不一致がある場合、クランプする
    /// </summary>
    private StatesPowerBreakdown ClampDefenseByAimStyle(BaseSkill skill,StatesPowerBreakdown def)
    {
        if(skill.NowAimStyle() != NowDeffenceStyle)
        {
            var MatchedMaxClampDef = DEF(skill.DEFATK, skill.NowAimStyle())*0.7f;//適切な防御力の0.7倍がクランプ最大値

            if(NowPower>ThePower.medium)//パワーが高い場合は 「適切な防御力をこしてた場合のみ」適切防御力の0.7倍にクランプ
            {
                //まず比較する、超していた場合にクランプ
                if(DEF()>DEF(0,skill.NowAimStyle()))//今回の防御力が適切な防御力を超してた場合、
                {
                    return MatchedMaxClampDef;//クランプされる。
                }
            }else//そうでない場合は、「適切な防御力を超してる越してない関係なく」適切防御力の0.7倍にクランプ(その最大値を絶対に超えない。)
            {
                
                if(def > MatchedMaxClampDef)
                {
                    return MatchedMaxClampDef;//最大値を超えたら最大値にクランプ
                }
            }
        }
        return def;//そのまま返す。
    }

    /* ---------------------------------
     * 防ぎ方の切り替え
     * --------------------------------- 
     */

    /// <summary>防ぎ方の切り替え </summary>
    private void SwitchDefenceStyle(BaseStates atker)
    {
        if(atker.NowBattleProtocol == BattleProtocol.none)
        {
            NowDeffenceStyle = AimStyle.none;//戦闘規格がない(フリーハンドスキル)なら、防ぎ方もnone(防御排他ステがない)
            return;
        } 
        var skill = atker.NowUseSkill;
        var pattern = DefaultDefensePatternPerProtocol[atker.NowBattleProtocol];

        if(!skill.NowConsecutiveATKFromTheSecondTimeOnward()){//単回攻撃または初回攻撃なら  (戦闘規格noneが入ることを想定)

            var per = 1f;
            if(GetTightenMindCorrectionStage()>=2)per=0.75f;//補正段階が2以上になるまで75%の確率で切り替えます、それ以降は100%で完全対応

           if(RandomEx.Shared.NextFloat(1) < pattern.a)//パターンAなら 
           {
            skill.DecideNowMoveSet_A0_B1(0);
            skill.SetSingleAimStyle(pattern.aStyle);//攻撃者側スキルにデフォルトの狙い流れを設定

            if(RandomEx.Shared.NextFloat(1)<per){
                NowDeffenceStyle =  pattern.aStyle;
                //攻撃者のAimStyle = 被害者のAimStyle　となるので狙い流れを対応できている。
            }else{
                NowDeffenceStyle = GetRandomAimStyleExcept(pattern.aStyle);//aStyle以外のAimStyleをランダムに選びます
            }
           }
           else                                         //パターンBなら
           {
            skill.DecideNowMoveSet_A0_B1(1);
            skill.SetSingleAimStyle(pattern.bStyle);//攻撃者側スキルにデフォルトの狙い流れを設定

            if(RandomEx.Shared.NextFloat(1)<per){
                NowDeffenceStyle =  pattern.bStyle;
                
            }else{
                NowDeffenceStyle = GetRandomAimStyleExcept(pattern.bStyle);//bStyle以外のAimStyleをランダムに選びます
            }
           }

           
        }else{                                              //連続攻撃中なら　　(戦闘規格noneを連続攻撃のmovesetに入れないこと前提)
            var AtkAimStyle = skill.NowAimStyle();//攻撃者の現在のAimStyleを取得
            
            if (AtkAimStyle == NowDeffenceStyle) return;// 既に同じAimStyleなら何もしない

            var TightenMind = GetTightenMindCorrectionStage();//現在の自分の引き締め値を入手

            if(UpdateAimStyleMemory(AtkAimStyle, TightenMind))//まず短期記憶を更新または新生する処理
            {
                if(atker.NowUseSkill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))
                {
                    if(RandomEx.Shared.NextFloat(1)<0.3f)return;
                }
                NowDeffenceStyle = AtkAimStyle;
            }//カウントアップ完了したなら、nowDeffenceStyleに記録されたAimStyleを適用するだけ
            
        }



    }
    /* ---------------------------------
     * 関連関数群
     * --------------------------------- 
     */


    /// <summary>
    /// nightinknightの値に応じて現在の「引き締める」補正段階を返す関数 </summary>
    /// <returns>補正段階 は増えていく。/returns>
    int GetTightenMindCorrectionStage()
    {
        float nightinknightValue = TenDayValues(false).GetValueOrZero(TenDayAbility.NightInkKnight);

        nightinknightValue /= 10;
        nightinknightValue = Mathf.Floor(nightinknightValue);
        if(NowPower == ThePower.high && RandomEx.Shared.NextFloat(1) < 0.5f)  nightinknightValue += 1;//パワーが高く、二分の一の確率を当てると、補正段階が1増える

        return (int)nightinknightValue;
    }

    /// <summary>
    /// 今回攻撃された際のAimStyle で短期記憶(TransformCount など)を更新する
    /// </summary>
    private bool UpdateAimStyleMemory(AimStyle newAimStyle, int tightenStage)
    {
        // 現在の短期記憶
        var mem = _aimStyleMemory;

        // 1) まだ何も対応していない or 前回の TargetAimStyle と違う ならリセット
        if (mem.TargetAimStyle == null || mem.TargetAimStyle.Value != newAimStyle)
        {
            // 新しく対応を始める
            mem.TargetAimStyle      = newAimStyle;
            mem.TransformCount      = 0;

            // TightenStage を加味して「対応に必要なカウントMax」を求める
            mem.TransformCountMax   = CalcTransformCountMax(tightenStage, newAimStyle);

        }
        
            // 変革カウントを進める
            int increment = CalcTransformCountIncrement(tightenStage);

            mem.TransformCount += increment;

            // 更新を反映
            _aimStyleMemory = mem;

            if(mem.TransformCount >= mem.TransformCountMax)//カウント上限を超えたらリセットし変更成功の項を返す
            {
                mem.TransformCount = 0;
                mem.TargetAimStyle = null;
                mem.TransformCountMax = 0;
                // 更新を反映
                _aimStyleMemory = mem;
            return true;
            }
        return false;
        
    }
    /// <summary>
    /// AimStyleを食らった時、何カウント増やすかを決める
    /// ※ tightenStageが高いほど変革スピードが速い、など
    /// </summary>
    private int CalcTransformCountIncrement(int tightenStage)
    {
        var rndmin = 0;
        var rndmax = tightenStage;
        if(NowPower< ThePower.medium)rndmax -= 1;
        if(tightenStage <2)return 1;//1以下なら基本値のみ
        if(tightenStage>5) rndmin = tightenStage/6;//6以上なら、補正段階の1/6が最小値
        return 1 + RandomEx.Shared.NextInt(rndmin, rndmax);//2以降なら補正段階分乱数の最大値が増える
    }
    /// <summary>
    /// 引き締め段階(tightenStage)と、新AimStyle に応じて必要な最大カウントを算出
    /// </summary>
    private int CalcTransformCountMax(int tightenStage, AimStyle AttackerStyle)
    {
        //AIMSTYLEの組み合わせ辞書により、必要な最大カウントを計算する
        var count = DefenseTransformationThresholds[(AttackerStyle, NowDeffenceStyle)];
        if(tightenStage>=2)
        {
            if(RandomEx.Shared.NextFloat(1)<0.31f + TenDayValues(false).GetValueOrZero(TenDayAbility.NightInkKnight)*0.01f)
        {
                count -= 1;

        }
        }
        
        if(tightenStage >= 5){
            if(RandomEx.Shared.NextFloat(1)<0.8f)
                {
                    count-=1;
                }
        }

    return  count;
    }






    //  ==============================================================================================================================
    //                                              メインダメージ関数
    //
    //
    //  ==============================================================================================================================

    /// <summary>
    ///オーバライド可能なダメージ関数
    /// </summary>
    /// <param name="atkPoint"></param>
    public virtual StatesPowerBreakdown DamageOnBattle(BaseStates Atker, float SkillPower,float SkillPowerForMental,HitResult hitResult,ref bool isdisturbed)
    {
        var skill = Atker.NowUseSkill;

        //ダメージ直前のパッシブ効果
        PassivesOnBeforeDamage(Atker);


        //もしカウンター用の防御無視率が攻撃者が持ってたら(本来の防御無視率より多ければ)
        var defatk = skill.DEFATK;
        if (Atker._exCounterDEFATK > defatk) defatk = Atker._exCounterDEFATK;

        var def = DEF(defatk);//防御力

        def = ClampDefenseByAimStyle(skill,def);//防ぎ方(AimStyle)の不一致がある場合、クランプする

        StatesPowerBreakdown dmg, mentalDmg;
        var mentalATKBoost = Mathf.Max(Atker.TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure) - TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure),0)
        * Atker.MentalHP * 0.2f;//相手との余裕の差と精神HPの0.2倍を掛ける 

        //下の魔法スキル以外の計算式を基本計算式と考えましょう
        if(skill.IsMagic)//魔法スキルのダメージ計算
        {
            dmg = MagicDamageCalculation(Atker, SkillPower, def);
            mentalDmg = MagicMentalDamageCalculation(Atker, mentalATKBoost, SkillPowerForMental);
        }
        else//それ以外のスキルのダメージ計算
        {
            dmg = NonMagicDamageCalculation(Atker, SkillPower, def);
            mentalDmg = NonMagicMentalDamageCalculation(Atker, mentalATKBoost, SkillPowerForMental);
        }
        
        isdisturbed = false;//攻撃が乱れたかどうか　　受けた攻撃としての視点から乱れていたかどうか
        if(NowPower > ThePower.lowlow)//たるくなければ基礎山形補正がある。
        {
            isdisturbed = GetBaseCalcDamageWithPlusMinus22Percent(ref dmg);//基礎山型補正
        }

        //物理耐性による減衰
        dmg = ApplyPhysicalResistance(dmg,skill);

        //パッシブによるダメージの減衰率による絶対削減
        PassivesDamageReductionEffect(ref dmg);
        //がむしゃらな補正
        dmg = GetFrenzyBoost(Atker,dmg);

        //慣れ補正
        dmg *= AdaptToSkill(Atker, skill, dmg);

        //ここまでで被害者に向かう純正ダメージです。

        //思えのダメージ保存　追加HPは通らない
        //ただクリティカルで増幅してほしいので、各クリティカル処理で本来のdmg同様にダメージを計算するべく引数に加える必要がある。
        var ResonanceDmg = dmg;

        //vitalLayerを通る処理
        BarrierLayers(ref dmg,ref mentalDmg, Atker);

        //刃物スキルであり、ダメージがまだ残っていて、自分の体力がダメージより多いのなら、刃物即死クリティカル
        bool BladeCriticalDeath = false;
        if(skill.IsBlade && dmg.Total > 0 && HP > dmg.Total)BladeCriticalDeath = BladeCriticalCalculation(ref dmg,ref ResonanceDmg,Atker,skill);

        //命中段階による最終ダメージ計算
        HitDmgCalculation(ref dmg,ref ResonanceDmg, hitResult,Atker);

        //TLOAスキルの威力減衰 本体HPの割合に対するダメージの削り切れる限界というもの。
        ApplyTLOADamageReduction(ref dmg,ref ResonanceDmg);

        if(isdisturbed)
        {//もし乱れ攻撃なら、味方(自分も含む)のスレームパッシブのイースターノジール効果を発動を判定
            manager.MyGroup(this).PartySlaimsEasterNoshiirEffectOnEnemyDisturbedAttack(Atker,ref dmg,ref ResonanceDmg);
        }
        
        //思えのダメージ発生  各クリティカルのダメージを考慮するためクリティカル後に
        ResonanceDamage(ResonanceDmg, skill, Atker);

        var totalDmg = dmg.Total;//直接引くように変数に代入
        if(totalDmg < 0)totalDmg = 0;//0未満は0にする　逆に回復してしまうのを防止
        var tempHP = HP;//計算用にダメージ受ける前のHPを記録

        
        HP -= totalDmg;
        CantKillSkillClamp(Atker,skill);//殺せない系再代入クランプ処理（戦闘版は常時適用）
        Debug.Log("攻撃が実行された");
        schizoLog.AddLog(Atker.CharacterName + "が" + this.CharacterName + "を攻撃した-「" + totalDmg + "」ダメージを与えた");

        //攻撃者がダメージを殺すまでに与えたダメージ辞書に記録する
        Atker.RecordDamageDealtToEnemyUntilKill(dmg.Total,this);

        var totalMentalDmg = mentalDmg.Total;//直接引くように変数に代入
        if(totalMentalDmg < 0)totalMentalDmg = 0;//0未満は0にする
        //パッシブによる絶対的なダメージ食らわないクランプ処理  下回ると代入するダメージの防ぎ
        DontDamagePassiveEffect(Atker);

        MentalHP -= totalMentalDmg;//実ダメージで精神HPの最大値がクランプされた後に、精神攻撃が行われる。

        

        if(!skill.IsBlade)//刃物スキルでなければ発生
        {
            CalculateMutualKillSurvivalChance(tempHP,totalDmg,Atker);//互角一撃の生存によるHP再代入の可能性
        }
        if(BladeCriticalDeath)//刃物即死発生したのなら
        {
           CalculateBladeDeathCriticalSurvivalChance(Atker);//生存チャンス
        }
        

        //余剰ダメージを計算
        var OverKillOverFlow = totalDmg - tempHP;//余剰ダメージ

        //死んだら攻撃者のOnKillを発生
        if(Death())
        {
            Atker.OnKill(this);//攻撃者のOnkill発生

            //overKillの処理
            OverKilledBrokenCalc(Atker,OverKillOverFlow);//攻撃者、引かれる前のHP,ダメージを渡す。
        }

        //もし"攻撃者が"割り込みカウンターパッシブだったら
        var CounterPower = Atker.GetPassiveByID(1) as InterruptCounterPassive;
        if (CounterPower != null)
        {
            //攻撃者の割り込みカウンターパッシブの威力が下がる
            ///とりあえずOnAfterAttackに入れた

            //割り込みカウンターをされた = さっき「自分は連続攻撃」をしていた
            //その連続攻撃の追加硬直値分だけ、「食らわせ」というパッシブを食らう。

            //ただし範囲攻撃で巻き添えの場合もあるから追加で判定　
            if(!RecentACTSkillData.IsDone && RecentACTSkillData.Target == Atker)//直近の攻撃行動で割り込みされてたか And 割り込みしてきた(攻撃対象)のが今の割り込みパッシブ攻撃者か
            {
                var DurationTurn = RecentACTSkillData.Skill.SKillDidWaitCount;//食らうターン
                if(DurationTurn > 0)//持続ターンが存在すれば、
                {
                    ApplyPassiveBufferInBattleByID(2,Atker);//パッシブ、食らわせを入手する。
                    var hurt = GetBufferPassiveByID(2);
                    if(CanApplyPassive(hurt))//適合したなら(適合条件がある)
                    {
                        hurt.DurationTurn = DurationTurn;//持続ターンを入れる
                    }
                }
            }



            

        }

        //攻撃者のキャラ単位への攻撃後のパッシブ効果
        Atker.PassivesOnAfterAttackToTargetWithHitresult(this,hitResult);
        //攻撃者の完全単体選択なら。
        if(Atker.Target == DirectedWill.One)
        Atker.PassivesOnAfterPerfectSingleAttack(this);//攻撃者のパッシブの完全単体選択発動効果

        return dmg;
    }
    /// <summary>
    /// 戦闘外やシンプル用途向けのダメージ適用（戦闘専用の連鎖・記録・割込み・ログ等は行わない）
    /// イベント用
    /// </summary>
    /// <param name="Atker"></param>
    /// <param name="SkillPower"></param>
    /// <param name="SkillPowerForMental"></param>
    /// <param name="hitResult"></param>
    /// <param name="isdisturbed"></param>
    /// <returns>最終的に本体HPへ適用されたダメージ内訳（VitalLayer通過後）</returns>
    public virtual StatesPowerBreakdown Damage(BaseStates Atker, float SkillPower,float SkillPowerForMental,HitResult hitResult,ref bool isdisturbed, DamageOptions opts = null)
    {
        var o = opts ?? SkillApplyPolicy.OutOfBattleDefault.Damage;
        var skill = Atker.NowUseSkill;

        // ダメージ直前のパッシブ効果
        // 戦闘外でBM依存のパッシブが混ざるのを避けるため、ポリシーから切替可能
        if (o.BeforeDamagePassives)
            PassivesOnBeforeDamage(Atker);

        // もしカウンター用の防御無視率が攻撃者が持ってたら(本来の防御無視率より多ければ)
        var defatk = skill.DEFATK;
        if (Atker._exCounterDEFATK > defatk) defatk = Atker._exCounterDEFATK;

        var def = DEF(defatk);//防御力

        // 防ぎ方(AimStyle)の不一致クランプ
        if (o.AimStyleClamp)
            def = ClampDefenseByAimStyle(skill,def);

        StatesPowerBreakdown dmg, mentalDmg;
        var mentalATKBoost = Mathf.Max(Atker.TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure) - TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure),0)
        * Atker.MentalHP * 0.2f;//相手との余裕の差と精神HPの0.2倍を掛ける 

        // 下の魔法スキル以外の計算式を基本計算式と考えましょう
        if(skill.IsMagic)//魔法スキルのダメージ計算
        {
            dmg = MagicDamageCalculation(Atker, SkillPower, def);
            mentalDmg = MagicMentalDamageCalculation(Atker, mentalATKBoost, SkillPowerForMental);
        }
        else//それ以外のスキルのダメージ計算
        {
            dmg = NonMagicDamageCalculation(Atker, SkillPower, def);
            mentalDmg = NonMagicMentalDamageCalculation(Atker, mentalATKBoost, SkillPowerForMental);
        }

        isdisturbed = false;//攻撃が乱れたかどうか　　受けた攻撃としての視点から乱れていたかどうか
        if(o.BaseRandomVariance && NowPower > ThePower.lowlow)//たるくなければ基礎山形補正がある。
        {
            isdisturbed = GetBaseCalcDamageWithPlusMinus22Percent(ref dmg);//基礎山型補正
        }

        // 物理耐性による減衰
        if(o.PhysicalResistance)
            dmg = ApplyPhysicalResistance(dmg,skill);

        // パッシブによるダメージの減衰率による絶対削減
        if(o.PassivesReduction)
            PassivesDamageReductionEffect(ref dmg);
        // がむしゃらな補正
        if(o.Frenzy)
            dmg = GetFrenzyBoost(Atker,dmg);

        // 慣れ補正
        if(o.Adaptation)
            dmg *= AdaptToSkill(Atker, skill, dmg);

        // ここまでで被害者に向かう純正ダメージです。

        // 思えのダメージ保存　追加HPは通らない
        var ResonanceDmg = dmg;

        // vitalLayerを通る処理
        if(o.BarrierLayers)
            BarrierLayers(ref dmg,ref mentalDmg, Atker);

        // 刃物スキルであり、ダメージがまだ残っていて、自分の体力がダメージより多いのなら、刃物即死クリティカル
        bool BladeCriticalDeath = false;
        if(o.BladeInstantDeath && skill.IsBlade && dmg.Total > 0 && HP > dmg.Total)
            BladeCriticalDeath = BladeCriticalCalculation(ref dmg,ref ResonanceDmg,Atker,skill);

        // 命中段階による最終ダメージ計算
        if(o.UseHitMultiplier)
            HitDmgCalculation(ref dmg,ref ResonanceDmg, hitResult,Atker);

        // TLOAスキルの威力減衰 本体HPの割合に対するダメージの削り切れる限界というもの。
        if(o.TLOReduction)
            ApplyTLOADamageReduction(ref dmg,ref ResonanceDmg);

        // 思えのダメージ発生（戦闘外でも基本効果として適用可能）
        if(o.Resonance)
            ResonanceDamage(ResonanceDmg, skill, Atker);

        var totalDmg = dmg.Total;//直接引くように変数に代入
        if(totalDmg < 0)totalDmg = 0;//0未満は0にする　逆に回復してしまうのを防止
        var tempHP = HP;//計算用にダメージ受ける前のHPを記録

        HP -= totalDmg;
        if(o.CantKillClamp)
            CantKillSkillClamp(Atker,skill);//殺せない系再代入クランプ処理

        var totalMentalDmg = mentalDmg.Total;//直接引くように変数に代入
        if(totalMentalDmg < 0)totalMentalDmg = 0;//0未満は0にする
        // パッシブによる絶対的なダメージ食らわないクランプ処理  下回ると代入するダメージの防ぎ
        if(o.DontDamageClamp)
            DontDamagePassiveEffect(Atker);

        if(o.MentalDamage)
            MentalHP -= totalMentalDmg;//実ダメージで精神HPの最大値がクランプされた後に、精神攻撃が行われる。

        // 以下は戦闘専用処理のため省略（連鎖・記録・割込み・OnKill/OverKill・生存チャンス・攻撃後パッシブ等）

        return dmg;
    }
    /// <summary>
    /// パッシブの毒ダメや、リンク等の「数値を渡すだけ」の単純ダメージ用
    /// - スキルに依存しない（NowUseSkill 参照なし）
    /// - 物理耐性/命中/各種補正/思え等は適用しない
    /// - VitalLayer だけを通すかどうかを選べる
    /// </summary>
    /// <param name="damage">ダメージ</param>
    /// <param name="LayerDamage">VitalLayerを通すかどうか</param>
    public void RatherDamage(StatesPowerBreakdown damage,bool LayerDamage,float DamageRatio)
    {
        StatesPowerBreakdown notUseDamage = damage;//使わないが、引数に渡す必要がある
        damage *= DamageRatio;//ダメージの倍率を掛ける

        //vitalLayerを通る処理
        if(LayerDamage)
        {
            // スキル非依存のため、物理属性やスキル連動効果を持たない簡易版を使用
            BarrierLayersForPassiveDamage(ref damage,ref notUseDamage);
        }

        // 純粋に本体HPを減算（スキル依存のクランプ等は適用しない）
        HP -= damage.Total;
    }

    //  ==============================================================================================================================
    //                                              メインダメージ処理の副関数
    //
    //
    //  ==============================================================================================================================
    
    /* ------------------------------------------------------------------------------------------------------------------------------------------
     * メインダメージ計算式
     * ------------------------------------------------------------------------------------------------------------------------------------------
     */


    //                 [[[[[[[[[[[[[[[                            ーーーー
    //                                                          通常ダメージ計算
    //                                                          ーーーー                                ]]]]]]]]]]]]]]


    /// <summary>
    /// 魔法スキル以外のダメージ計算
    /// </summary>
    public StatesPowerBreakdown NonMagicDamageCalculation(BaseStates Atker, float SkillPower,StatesPowerBreakdown def)
    {
        return ((Atker.ATK(Atker.SkillAttackModifier) - def) * SkillPower) + SkillPower;//(攻撃-対象者の防御) にスキルパワー加算と乗算
    }
    /// <summary>
    /// 魔法スキルのダメージ計算
    /// </summary>
    public StatesPowerBreakdown MagicDamageCalculation(BaseStates Atker, float SkillPower,StatesPowerBreakdown def)
    {
        return (MagicBlendVSCalc(Atker.ATK(Atker.SkillAttackModifier),def) * (SkillPower * 0.5f)) + SkillPower * Atker.ATK() * 0.09f;//(攻撃-対象者の防御) にスキルパワー加算と乗算
    }

    //                 [[[[[[[[[[[[[[[                            ーーーー
    //                                                          精神ダメージ計算
    //                                                          ーーーー                                ]]]]]]]]]]]]]]
    
    /// <summary>
    /// 魔法スキル以外の精神ダメージ計算
    /// </summary>
    public StatesPowerBreakdown NonMagicMentalDamageCalculation(BaseStates Atker,float mentalATKBoost,float SkillPowerForMental)
    {
        return  ((Atker.ATK() * mentalATKBoost - MentalDEF()) * SkillPowerForMental) + SkillPowerForMental ;//精神攻撃
    }


    /// <summary>
    /// 魔法スキルの精神ダメージ計算
    /// </summary>
    public StatesPowerBreakdown MagicMentalDamageCalculation(BaseStates Atker,float mentalATKBoost,float SkillPowerForMental)
    {
        return  (Atker.ATK() * mentalATKBoost / MentalDEF() * (SkillPowerForMental * 0.6f)) + SkillPowerForMental * 0.7f ;//精神攻撃
    }


    //                 [[[[[[[[[[[[[[[                            ーーーー
    //                                                          魔法ダメージ特殊計算
    //                                                          ーーーー                                ]]]]]]]]]]]]]]


    /// <summary>
    /// 魔法スキルの計算式は、基本の-計算と÷計算がブレンドするからそれ用の関数
    /// -計算から÷計算へtの値を元にシフトしていく(÷計算の最低保証性が攻撃力が防御力に負けるにつれて高まるって感じ)
    /// </summary>
    /// <param name="atkPoint"></param>
    /// <param name="defPoint"></param>
    /// <returns></returns>
    public StatesPowerBreakdown MagicBlendVSCalc(StatesPowerBreakdown atkPoint, StatesPowerBreakdown defPoint)
    {
        float k = 0.02f;      // 調整可能な傾きパラメータ
        float t = 140f;      // 調整可能な閾値シフト
        float epsilon = 0.0001f;

        // ロジスティック関数を用いて重みを計算
        // atk - def が大きければ weight は 1 に近づき、差分計算（通常スキル的な挙動）をして、
        // atk - def が小さい（またはマイナス）なら weight は 0 に近づき、比率計算（最低ダメージ保証的な挙動）の比率が高まる
        float weight = 1.0f / (1.0f + Mathf.Exp(-k * (atkPoint.Total - defPoint.Total - t)));//重みなのでtotal

        // 差分に基づくダメージ（通常スキルの挙動に近い）       
        StatesPowerBreakdown damage_diff = atkPoint - defPoint;
        
        
        // 除算に基づくダメージ（魔法特有の挙動、最低ダメージ保証の要素を持たせる）  （思った以上に効果ないから+10とかしとくわ）
        StatesPowerBreakdown damage_ratio = atkPoint / (defPoint + epsilon) + 8 + damage_diff / 1.6f;


        if(damage_diff.Total <= 0)//攻撃と防御の差が0以下なら　除算計算オンリー
        {
            return damage_ratio;
        }
        // 両者をブレンドする
        // weight が 1 に近いときは damage_diff が支配的（通常スキル的挙動）、
        // weight が 0 に近いときは damage_ratio が支配的に
        StatesPowerBreakdown baseDamage = (weight * damage_diff) + ((1.0f - weight) * damage_ratio);
        return baseDamage;
    }

    /* ------------------------------------------------------------------------------------------------------------------------------------------
     * 思えダメージ
     * ------------------------------------------------------------------------------------------------------------------------------------------
     */

    /// <summary>
    /// 思えダメージの精神属性合致補正
    /// </summary>
    const float SPIRITUAL_MODIFIER = 1.29f;
    /// <summary>
    /// 思えダメージが発生する強さの比率のしきい値
    /// </summary>
    const float RESONANCE_POWER_THRESHOLD = 1.4f;
    /// <summary>
    /// 思えのダメージの基礎値
    /// </summary>
    const float BASEDAMGE_TENDAYS = 0.06f;
    /// <summary>
    /// 思えダメージの精神属性の種類数、精神ポテンシャルによる除算DEFの数に掛ける係数
    /// </summary>
    const float SPRITUAL_POTENTIAL_DEF_COED =  0.06f;
    /// <summary>
    /// 思えダメージをランダマイズ出来るスキル数のしきい値
    /// </summary>
    const int SKILL_COUNT_THRESHOLD_DAMAGE_RANDOMIZE = 6;
    /// <summary>
    /// 思えダメージをスキルの数でランダマイズする
    /// </summary>
    float ResonanceDamageRandomizeBySkillCount(float dmg)
    {
        //スキル数がしきい値以下ならランダマイズしない
        if(SkillList.Count <= SKILL_COUNT_THRESHOLD_DAMAGE_RANDOMIZE) return dmg;
        //ランダマイズする数
        var RandomizeCalcCount = SkillList.Count - SKILL_COUNT_THRESHOLD_DAMAGE_RANDOMIZE;
        
        var maxFactor = 1.0f;
        var minFactor = 1.0f;
        for(int i = 0; i < RandomizeCalcCount; i++)
        {
            var RandomizeUpper = RandomEx.Shared.NextFloat(0.01f,0.015f);//ランダマイズの上振れ
            var RandomizeLower = RandomEx.Shared.NextFloat(0.01f,0.03f);//ランダマイズの下振れ

            maxFactor += RandomizeUpper;
            minFactor -= RandomizeLower;
        }
        if(minFactor < 0) minFactor = 0;
        if(minFactor > maxFactor) minFactor = maxFactor;//念のため
        return dmg * RandomEx.Shared.NextFloat(minFactor,maxFactor);
    }
    /// <summary>
    /// 思えのダメージ処理
    /// ダメージと精神ダメージを食らう前に判定され、追加HPで防がれない
    /// </summary>
    public void ResonanceDamage(StatesPowerBreakdown dmg, BaseSkill skill, BaseStates Atker)
    {   
        //攻撃者がこちらに対してどれだけ強いか
        var powerRatio = Atker.TenDayValuesSum(false) / TenDayValuesSum(false);//思えダメージはスキルの十日能力補正なし
        //相手が自分より定数倍強いとダメージが発生する
        if(powerRatio < RESONANCE_POWER_THRESHOLD) return;

        //被害者の精神HP現在値とHPの平均値と最大HPの割合
        var myBodyAndMentalAverageRatio = (HP + MentalHP) / 2 / MaxHP;
        //思えの食らう割合。
        var ResonanceDangerRatio = 1.0f - myBodyAndMentalAverageRatio;
        //最大0.8　8割分食らってる所までダメージが伸びきることを想定する
        ResonanceDangerRatio = Mathf.Min(0.8f,ResonanceDangerRatio);

        //攻撃者と攻撃スキルの精神属性が一致してた場合にかかる補正は各case文で計算
        
        //十日能力による基礎思えダメージ
        var BaseDmg =dmg.TenDayValuesSum * BASEDAMGE_TENDAYS;//十日能力のダメージ分定数分

        //相手がどのくらい強いかの倍率
        var DamageMultipilerByPowerRatio = powerRatio - (RESONANCE_POWER_THRESHOLD - 1.0f);
        
        //人間状況による分岐
        switch(NowCondition)
        {
            case HumanConditionCircumstances.Painful:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.UnextinguishedPath);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.FlameBreathingWife);

                if(skill.SkillSpiritual == SpiritualProperty.devil || skill.SkillSpiritual == SpiritualProperty.liminalwhitetile)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.devil || Atker.MyImpression == SpiritualProperty.liminalwhitetile)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
            case HumanConditionCircumstances.Optimistic:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.NightDarkness);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.StarTersi);

                if(skill.SkillSpiritual == SpiritualProperty.doremis)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.doremis)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
            case HumanConditionCircumstances.Elated:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.dokumamusi);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.SpringWater);
                //どの精神属性も効かない
                break;
            case HumanConditionCircumstances.Resolved:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.TentVoid);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Vond);
                if(skill.SkillSpiritual == SpiritualProperty.pysco)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.pysco)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
            case HumanConditionCircumstances.Angry:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.HeatHaze);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Rain);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.ColdHeartedCalm);
                if(skill.SkillSpiritual == SpiritualProperty.liminalwhitetile)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.liminalwhitetile)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
            case HumanConditionCircumstances.Doubtful:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.HumanKiller);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.PersonaDivergence);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Enokunagi);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Blades);
                if(skill.SkillSpiritual == SpiritualProperty.doremis || skill.SkillSpiritual == SpiritualProperty.pysco)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.doremis || Atker.MyImpression == SpiritualProperty.pysco)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
            case HumanConditionCircumstances.Confused:
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.SilentTraining);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Miza);
                BaseDmg += dmg.GetTenDayValue(TenDayAbility.Raincoat);
                if(skill.SkillSpiritual == SpiritualProperty.doremis || skill.SkillSpiritual == SpiritualProperty.sacrifaith)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                if(Atker.MyImpression == SpiritualProperty.doremis || Atker.MyImpression == SpiritualProperty.sacrifaith)
                {
                    BaseDmg *= SPIRITUAL_MODIFIER;
                }
                break;
        }

        var finalDamage = BaseDmg * ResonanceDangerRatio * DamageMultipilerByPowerRatio;

        //被害者側の精神属性の種類　= 精神ポテンシャルで除算をする。
        var potential = GetMySpiritualPotential();
        finalDamage *= 1 - (potential * SPRITUAL_POTENTIAL_DEF_COED);

        //スキルの数による除算
        finalDamage = ResonanceDamageRandomizeBySkillCount(finalDamage);
        //ダメージを反映
        NowResonanceValue -= finalDamage;
    }
    /// <summary>
    /// 人間状況が普調なら　行動を起こすたびに思えの値が回復する。
    /// AttackCharaで
    /// </summary>
    public void ResonanceHealingOnBattle()
    {
        if(NowCondition == HumanConditionCircumstances.Normal)
        {
            //とりあえず最大値 3~11%ランダム回復ってことで。
            var HealAmount = ResonanceValue * RandomEx.Shared.NextFloat(0.03f,0.11f);
            ResonanceHeal(HealAmount);
        }
    }



    /* ------------------------------------------------------------------------------------------------------------------------------------------
     * VitalLayerのダメージ処理関数
     * ------------------------------------------------------------------------------------------------------------------------------------------
     */

    /// <summary>
    /// パッシブ由来（スキル非依存）のDoT/リンク等の数値ダメージをバリア層に通すための簡易処理
    /// - 物理属性耐性（heavy/volten/dishSmack）や破壊慣れ/破壊負け等のスキル依存効果は適用しない
    /// - レイヤーの MentalPenetrateRatio は考慮する
    /// - レイヤー破壊時の処理は ResistMode に準拠（resistRate=1想定の簡易版）
    /// </summary>
    public void BarrierLayersForPassiveDamage(ref StatesPowerBreakdown dmg, ref StatesPowerBreakdown mentalDmg)
    {
        for (int i = 0; i < _vitalLayerList.Count;)
        {
            var layer = _vitalLayerList[i];

            // 耐性による軽減は行わない（resistRate=1.0想定）
            StatesPowerBreakdown dmgAfter = dmg;
            // レイヤーHPを削る
            StatesPowerBreakdown leftover = layer.LayerHP - dmgAfter; // マイナスなら破壊

            // 精神ダメージの通過率
            mentalDmg -= layer.LayerHP * (1 - layer.MentalPenetrateRatio);

            if (leftover <= 0f)
            {
                // 破壊された
                StatesPowerBreakdown overkill = -leftover; // -negative => positive
                var tmpHP = layer.LayerHP;// 仕組みC用に今回受ける時のLayerHPを保存
                var maxHP = layer.MaxLayerHP;
                layer.LayerHP = 0f;

                // ResistMode に準拠（resistRate=1 相当）
                switch (layer.ResistMode)
                {
                    case BarrierResistanceMode.A_SimpleNoReturn:
                        // 軽減の復活なし => overkill をそのまま次へ
                        dmg = overkill;
                        break;
                    case BarrierResistanceMode.B_RestoreWhenBreak:
                        // ここでは resistRate=1 とみなす => Aと同様
                        dmg = overkill;
                        break;
                    case BarrierResistanceMode.C_IgnoreWhenBreak:
                        // 元攻撃 - 現在のLayerHP
                        dmg = dmg - tmpHP;
                        break;
                    case BarrierResistanceMode.C_IgnoreWhenBreak_MaxHP:
                        // 元攻撃 - MaxHP
                        dmg = dmg - maxHP;
                        break;
                }

                _vitalLayerList.RemoveAt(i);
                // リスト削除したので i はインクリメントしない
            }
            else
            {
                // バリアで耐えた
                layer.LayerHP = leftover.Total;
                dmg = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0f);
                i++;
            }

            if (dmg.Total <= 0f)
            {
                break;
            }
        }
    }






}
