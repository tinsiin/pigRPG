using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;
using NRandom.Linq;


//数字管理系ステータス
public abstract partial class BaseStates    
{

    //  ==============================================================================================================================
    //                                              HP
    //  ==============================================================================================================================
    CombinedStatesBar HPBar => UI?.HPBar;//UI
    [Header("HP")]
    [Tooltip("現在のHP（実数）。MaxHPを超えないようにクランプされる")]
    [SerializeField]
    private float _hp;
    public float HP
    {
        get { return _hp; }
        set
        {
            Debug.Log($"HP:{value}");
            // HP をクランプ
            if (value > MaxHP)//最大値を超えないようにする
            {
                _hp = MaxHP;
            }
            else _hp = value;

            // 精神HPのチェック（HP変化で最大値が下がる可能性があるためクランプ）
            if (_mentalHP > MentalMaxHP)//最大値超えてたらカットする。
            {
                _mentalHP = MentalMaxHP;
            }

            // UI 更新（クランプ後の値で反映）
            if (HPBar != null)
            {
                float denom = MaxHP;
                float hpPercent = denom > 0f ? (_hp / denom) : 0f;
                float mentalRatio = denom > 0f ? (_mentalHP / denom) : 0f;
                HPBar.HPPercent = hpPercent;
                HPBar.MentalRatio = mentalRatio; // 精神HPも最新のクランプ値で同期
                HPBar.DivergenceMultiplier = GetMentalDivergenceThreshold(); // 乖離指標の更新
            }
        }
    }
    [Tooltip("最大HP（実数）。HPはこの値でクランプされる")]
    [SerializeField]
    private float _maxhp;
    public float MaxHP => _maxhp;
    //  ==============================================================================================================================
    //                                              精神HP
    //  ==============================================================================================================================
    [Space]
    [Header("精神HP")]
    [Tooltip("現在の精神HP。最大値は動的に計算される(MentalMaxHP)")]
    [SerializeField]
    float _mentalHP;
    /// <summary>
    /// 精神HP
    /// </summary>
    public float MentalHP 
    {
        get 
        {
            if(_mentalHP > MentalMaxHP)//最大値超えてたらカットする。
            {
                _mentalHP = MentalMaxHP;
            }
            return _mentalHP;
        }
        set
        {
            if(value > MentalMaxHP)//最大値を超えないようにする。
            {
                _mentalHP = MentalMaxHP;
            }
            else _mentalHP = value;
            if(HPBar != null)
            {
                // クランプ後の実値で UI を更新する（未クランプの value を使うと過大表示になる）
                HPBar.MentalRatio = MaxHP > 0f ? (_mentalHP / MaxHP) : 0f;//精神HPを設定
                HPBar.DivergenceMultiplier = GetMentalDivergenceThreshold();//UIの乖離指標の幅を設定
            }
        }
    }
    /// <summary>
    /// 精神HP最大値
    /// </summary>
    public float MentalMaxHP => CalcMentalMaxHP();

    /// <summary>
    /// 精神HPの最大値を設定する　パワーでの分岐やHP最大値に影響される
    /// </summary>
    float CalcMentalMaxHP()
    {
        if(NowPower == PowerLevel.High)
        {
            return _hp * 1.3f + _maxhp *0.08f;
        }else
        {
            return _hp;
        }
    }
    /* ------------------------------------------------------------------------------------------------------------------------------------------
     * イベント変化
     * ------------------------------------------------------------------------------------------------------------------------------------------
     */
    /// <summary>
    /// 精神HPは攻撃時にスキル設定の百分率分だけ変動する
    /// </summary>
    /// <param name="percentOverride">任意指定の百分率。指定しない場合は使用スキルを参照</param>
    void MentalHealOnAttack(float percentOverride = float.NaN)
    {
        float percent = percentOverride;

        if (float.IsNaN(percent))
        {
            percent = NowUseSkill != null ? NowUseSkill.AttackMentalHealPercent : 80f;
        }

        var healAmount = b_ATK.Total * (percent / 100f);
        MentalHP += healAmount;
    }
    void MentalHPHealOnTurn()
    {
        MentalHP += TenDayValues(false).GetValueOrZero(TenDayAbility.Rain);
    }
    /// <summary>
    /// 死亡時の精神HP
    /// </summary>
    void MentalHPOnDeath()
    {
            switch (MyImpression)
        {
            case SpiritualProperty.LiminalWhiteTile:
                // そのまま（変化なし）
                break;
            case SpiritualProperty.Kindergarten:
                // 10割回復
                MentalHP = MentalMaxHP;
                break;
            case SpiritualProperty.Sacrifaith:
                // 10割回復　犠牲になって満足した
                MentalHP = MentalMaxHP;
                break;
            case SpiritualProperty.Cquiest:
                // 10%加算 + 元素信仰力
                MentalHP += MentalMaxHP * 0.1f + TenDayValues(false).GetValueOrZero(TenDayAbility.ElementFaithPower) / 3;
                break;
            case SpiritualProperty.Devil:
                // 10%減る
                MentalHP -= MentalMaxHP * 0.1f;
                break;
            case SpiritualProperty.Doremis:
                // 春仮眠の夜暗黒に対する多さ 割
                var darkNight = TenDayValues(false).GetValueOrZero(TenDayAbility.NightDarkness);
                var springNap = TenDayValues(false).GetValueOrZero(TenDayAbility.SpringNap);
                if (springNap > 0)
                {
                    MentalHP = MentalMaxHP * (springNap / darkNight);
                }
                break;
            case SpiritualProperty.Pillar:
                // 8割固定
                MentalHP = MentalMaxHP * 0.8f;
                break;
            case SpiritualProperty.GodTier:
                // 35%加算
                MentalHP += MentalMaxHP * 0.35f;
                break;
            case SpiritualProperty.BaleDrival:
                // 7割回復
                MentalHP = MentalMaxHP * 0.7f;
                break;
            case SpiritualProperty.Psycho:
                // 20%加算
                MentalHP += MentalMaxHP * 0.2f;
                break;
        }
    
        // 最大値を超えないように調整
        if (MentalHP > MentalMaxHP)
        {
            MentalHP = MentalMaxHP;
        }
        
        // 負の値にならないように調整
        if (MentalHP < 0)
        {
            MentalHP = 0;
        }
    }

    
    //  ==============================================================================================================================
    //                                              物理耐性
    //  ==============================================================================================================================
    [Space]
    [Header("物理耐性")]
    /// <summary>
    /// 暴断耐性
    /// </summary>
    [Tooltip("暴断(heavy)に対するダメージ倍率。1.0=基準(無補正)")]
    public float HeavyResistance = 1.0f;
    /// <summary>
    /// ヴォル転耐性
    /// </summary>
    [Tooltip("ヴォル転(volten)に対するダメージ倍率。1.0=基準(無補正)")]
    public float voltenResistance = 1.0f;
    /// <summary>
    /// 床ずれ耐性
    /// </summary>
    [Tooltip("床ずれ(dish smack)に対するダメージ倍率。1.0=基準(無補正)")]
    public float DishSmackRsistance = 1.0f;
    //  ==============================================================================================================================
    //                                              4大ステータス
    //  ==============================================================================================================================
    /* ------------------------------------------------------------------------------------------------------------------------------------------
     * 基礎ステータス
     * ------------------------------------------------------------------------------------------------------------------------------------------
     */
    public StatesPowerBreakdown b_AGI
    {
        get
        {
            // StatesPowerBreakdownのインスタンスを作成
            var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_agi);
            // 共通係数の適用（AgiPowerConfig）
            foreach (var kv in global::AgiPowerConfig.CommonAGI)
            {
                float td = TenDayValues(false).GetValueOrZero(kv.Key);
                if (td != 0f && kv.Value != 0f)
                {
                    breakdown.TenDayAdd(kv.Key, td * kv.Value);
                }
            }

            return breakdown;
        }
    }
    /// <summary>
    /// 攻撃力を十日能力とb_b_atkから計算した値
    /// 分解用にStatesPowerBreakdownとして返す
    /// </summary>
    public StatesPowerBreakdown b_ATK
    {
        get 
        {
            // StatesPowerBreakdownのインスタンスを作成
            var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_atk);
            
            // 共通係数の適用（AttackPowerConfig）
            foreach (var kv in AttackPowerConfig.CommonATK)
            {
                float td = TenDayValues(false).GetValueOrZero(kv.Key);
                if (td != 0f && kv.Value != 0f)
                {
                    breakdown.TenDayAdd(kv.Key, td * kv.Value);
                }
            }

            // プロトコル排他係数の適用（適応ラグによる適応率を乗算）
            var excl = AttackPowerConfig.GetExclusiveATK(NowBattleProtocol);
            foreach (var kv in excl)
            {
                float td = TenDayValues(false).GetValueOrZero(kv.Key);
                if (td != 0f && kv.Value != 0f)
                {
                    breakdown.TenDayAdd(kv.Key, td * kv.Value * _adaptationRate);
                }
            }
            
            return breakdown;
        }   
    }
  
    /// <summary>
    /// 基礎攻撃防御　(大事なのは、基本的にこの辺りは超スキル依存なの)
    /// オプションのAimStyleに値を入れるとそのAimStyleでシミュレート
    /// </summary>
    /// <param name="SimulateAimStyle"></param>
    /// <returns></returns>
    public StatesPowerBreakdown b_DEF(AimStyle? SimulateAimStyle = null)
    {
       // StatesPowerBreakdownのインスタンスを作成
        var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_def);
        
        StatesPowerBreakdown styleBreakdown;
        
        if(SimulateAimStyle == null)
        {
            styleBreakdown = CalcBaseDefenseForAimStyle(NowDeffenceStyle);
        }
        else
        {
            styleBreakdown = CalcBaseDefenseForAimStyle(SimulateAimStyle.Value);
        }
    
        // スタイルによる防御力内訳を追加
        return breakdown + styleBreakdown;
    }
    public StatesPowerBreakdown b_EYE
    {
        get
        {
            // StatesPowerBreakdownのインスタンスを作成
            var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_eye);
            
            // 共通係数の適用（EyePowerConfig）
            foreach (var kv in global::EyePowerConfig.CommonEYE)
            {
                float td = TenDayValues(false).GetValueOrZero(kv.Key);
                if (td != 0f && kv.Value != 0f)
                {
                    breakdown.TenDayAdd(kv.Key, td * kv.Value);
                }
            }
            
            return breakdown;
        }
    }
    /* ------------------------------------------------------------------------------------------------------------------------------------------
     * 基礎ステータス特殊計算用
     * ------------------------------------------------------------------------------------------------------------------------------------------
     */

    public StatesPowerBreakdown b_ATKSimulate(BattleProtocol simulateProtocol)
    {
        // StatesPowerBreakdownのインスタンスを作成
        var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_atk);

        // 共通係数の適用（AttackPowerConfig）
        foreach (var kv in AttackPowerConfig.CommonATK)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }

        // 指定プロトコルの排他係数の適用
        var excl = AttackPowerConfig.GetExclusiveATK(simulateProtocol);
        foreach (var kv in excl)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }

        return breakdown;
    }
    /// <summary>
    /// 戦闘規格ごとの「排他（プロトコル固有）加算」だけを返す攻撃内訳
    /// 基礎値や共通TenDay加算は含めない
    /// </summary>
    public StatesPowerBreakdown b_ATKProtocolExclusive(BattleProtocol protocol)
    {
        var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);
        var excl = AttackPowerConfig.GetExclusiveATK(protocol);
        foreach (var kv in excl)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }
        return breakdown;
    }

    /// <summary>
    /// 攻撃の排他（プロトコル固有）加算の合計値
    /// </summary>
    public float ATKProtocolExclusiveTotal(BattleProtocol protocol)
    {
        return b_ATKProtocolExclusive(protocol).Total;
    }
    /// <summary>
    /// 指定したAimStyleでの基礎防御力を計算する
    /// </summary>
    private StatesPowerBreakdown CalcBaseDefenseForAimStyle(AimStyle style)
    {
        // StatesPowerBreakdownのインスタンスを作成
        var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);

        // 共通係数の適用（DefensePowerConfig）
        foreach (var kv in DefensePowerConfig.CommonDEF)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }

        // AimStyle排他係数の適用
        var excl = DefensePowerConfig.GetExclusiveDEF(style);
        foreach (var kv in excl)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }
    
        return breakdown;
    }

    /// <summary>
    /// 防御の排他（AimStyle固有）加算のみを返す防御内訳
    /// 基礎値や共通TenDay加算は含めない
    /// </summary>
    public StatesPowerBreakdown b_DEFProtocolExclusive(AimStyle style)
    {
        var breakdown = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);
        var excl = global::DefensePowerConfig.GetExclusiveDEF(style);
        foreach (var kv in excl)
        {
            float td = TenDayValues(false).GetValueOrZero(kv.Key);
            if (td != 0f && kv.Value != 0f)
            {
                breakdown.TenDayAdd(kv.Key, td * kv.Value);
            }
        }
        return breakdown;
    }
    /// <summary>
    /// 防御の排他（AimStyle固有）加算の合計値
    /// </summary>
    public float DEFProtocolExclusiveTotal(AimStyle style)
    {
        return b_DEFProtocolExclusive(style).Total;
    }

    /* ------------------------------------------------------------------------------------------------------------------------------------------
     * 最終ステータス
     * ------------------------------------------------------------------------------------------------------------------------------------------
     */

    /// <summary>
    /// 命中率計算
    /// </summary>
    /// <returns></returns>
    public virtual StatesPowerBreakdown EYE()
    {
        StatesPowerBreakdown eye = b_EYE;//基礎命中率

        eye *= GetSpecialPercentModifier(StatModifier.Eye);//命中率補正。リスト内がゼロならちゃんと1.0fが返る。
        PassivesPercentageModifier(StatModifier.Eye, ref eye);//パッシブの乗算補正
        eye += GetSpecialFixedModifier(StatModifier.Eye);//命中率固定値補正

        if(NowUseSkill != null)//スキルがある、攻撃時限定処理
        {
            //範囲意志によるボーナス
            var dict = NowUseSkill.HitRangePercentageDictionary;
            if(dict == null || dict.Count <= 0)
            {
                Debug.Log($"{CharacterName}の{NowUseSkill.SkillName}-"
                +"範囲意志によるボーナスがスキルに設定されていないため計算されませんでした。"
                +"「範囲意志ボーナスが設定されていないスキルなら正常です。」");
            }else{
                foreach (KeyValuePair<SkillZoneTrait, float> entry
                    in NowUseSkill.HitRangePercentageDictionary)//辞書に存在する物全てをループ
                {
                    if (HasRangeWill(entry.Key))//キーの内容が範囲意志と合致した場合
                    {
                        eye += entry.Value;//範囲意志による補正は非十日能力値

                        //基本的に範囲は一つだけのはずなので無用なループは避けてここで終了
                        break;
                    }
                }
            }

                    //単体攻撃による命中補正
            //複数性質を持っていない、完全なる単体の攻撃なら
            if (IsPerfectSingleATK())
            //ControlBySituationでの事故性質でも複数性質で複数事故が起こるかもしれないので、それも加味してる。
            {
                var agiPer = 6;//攻撃者のAgiの命中補正用 割る数
                if (NowUseSkill.SkillPhysical == PhysicalProperty.heavy)//暴断攻撃なら
                {
                    agiPer *= 2;//割る数が二倍に
                }
                eye += AGI() / agiPer;
            }

        }


        //パッシブの補正　固定値
        eye += _passiveList.Sum(p => p.EYEFixedValueEffect());

        //eye.ClampToZero();//クランプ　ここでやらない
        return eye;
    }

    /// <summary>
    /// 回避率計算
    /// </summary>
    public virtual StatesPowerBreakdown AGI()
    {
        StatesPowerBreakdown agi = b_AGI;//基礎回避率

        agi *= GetSpecialPercentModifier(StatModifier.Agi);//回避率補正。リスト内がゼロならちゃんと1.0fが返る。
        PassivesPercentageModifier(StatModifier.Agi, ref agi);//パッシブの乗算補正
        agi += GetSpecialFixedModifier(StatModifier.Agi);//回避率固定値補正

        if(manager == null)
        {
            Debug.Log("BattleManagerがnullです、恐らく戦闘開始前のステータス計算をしている可能性があります。回避率の前のめり補正をスキップします。");
        }
        else if (manager.IsVanguard(this))//自分が前のめりなら
        {
            agi /= 2;//回避率半減
        }
        //パッシブの補正　固定値
        agi += _passiveList.Sum(p => p.AGIFixedValueEffect());

        //agi.ClampToZero();//クランプ　ここでやらない
        return agi;
    }

    /// <summary>
    /// 攻撃力のステータス
    /// 引数の補正はいわゆるステータスに関係ないものとして、攻撃補正なので、
    /// 「実際に敵のHPを減らす際」のみに指定する。  = 精神HPに対するダメージにも使われない。
    /// </summary>
    /// <param name="AttackModifier"></param>
    /// <returns></returns>
    public virtual StatesPowerBreakdown ATK(float AttackModifier =1f)
    {
        StatesPowerBreakdown atk = b_ATK;//基礎攻撃力

        atk *= GetSpecialPercentModifier(StatModifier.Atk);//攻撃力補正
        PassivesPercentageModifier(StatModifier.Atk, ref atk);
        atk += GetSpecialFixedModifier(StatModifier.Atk);//攻撃力固定値補正

        atk *= AttackModifier;//攻撃補正  実際の攻撃時のみに参照される。

        //範囲意志によるボーナス
        foreach (KeyValuePair<SkillZoneTrait, float> entry
            in NowUseSkill.PowerRangePercentageDictionary)//辞書に存在する物全てをループ
        {
            if (HasRangeWill(entry.Key))//キーの内容が範囲意志と合致した場合
            {
                atk += entry.Value;//範囲意志による補正が掛かる

                //基本的に範囲は一つだけのはずなので無用なループは避けてここで終了
                break;
            }
        }

        //単体攻撃で暴断物理攻撃の場合のAgi攻撃補正
        if (IsPerfectSingleATK())
        {
            if (NowUseSkill.SkillPhysical == PhysicalProperty.heavy)
            {
                atk += AGI() / 6;
            }
        }
        //パッシブの補正　固定値を加算する
        atk += _passiveList.Sum(p => p.ATKFixedValueEffect());

        //atk.ClampToZero();//クランプ　ここでやらない
        return atk;
    }

    /// <summary>
    ///     防御力計算 シミュレートも含む(AimStyle不一致によるクランプのため)
    ///     オプションのAimStyleに値を入れるとそのAimStyleでシミュレート
    /// </summary>
    /// <returns></returns>
    public virtual StatesPowerBreakdown DEF(float minusPer=0f, AimStyle? SimulateAimStyle = null)
    {
        var def = b_DEF(); //基礎防御力が基本。

        if(SimulateAimStyle != null)//シミュレートするなら
        {
            def = b_DEF(SimulateAimStyle);//b_defをシミュレート
        }

        def *= GetSpecialPercentModifier(StatModifier.Def);//防御力補正
        PassivesPercentageModifier(StatModifier.Def, ref def);//パッシブの乗算補正
        def += GetSpecialFixedModifier(StatModifier.Def);//防御力固定値補正

        def *= PassivesDefencePercentageModifierByAttacker();//パッシブ由来の攻撃者を限定する補正

        var minusAmount = def * minusPer;//防御低減率

        //パッシブの補正　固定値
        def += _passiveList.Sum(p => p.DEFFixedValueEffect());

        def -= minusAmount;//低減

        //def.ClampToZero();//クランプ　ここでやらない
        return def;
    }
    //  ==============================================================================================================================
    //                                             精神HP用4大ステータス
    //  ==============================================================================================================================

    /// <summary>
    /// 精神HP用の防御力
    /// </summary>
    public virtual StatesPowerBreakdown MentalDEF()
    {
        return b_DEF() * 0.7f * NowPower switch
        {
            PowerLevel.High => 1.4f,
            PowerLevel.Medium => 1f,
            PowerLevel.Low => 0.7f,
            PowerLevel.VeryLow => 0.4f,
            _ => -4444444,//エラーだ
        };
    }

    //  ==============================================================================================================================
    //                                              基礎ステータス
    //  ==============================================================================================================================
    [Header("4大ステの基礎基礎値")]
    public float b_b_atk = 4f;
    public float b_b_def = 4f;
    public float b_b_eye = 4f;
    public float b_b_agi = 4f;

    //  ==============================================================================================================================
    //                                              十日能力ステータス管理
    //  ==============================================================================================================================

    [Header("基本十日能力値テンプレ\n"+"初期設定用のテンプレ値です。実行時はこのテンプレからランタイム用辞書にコピーされます。(設定用クラスからランタイム用クラスにコピーされますする際に)\n"
    +"ランタイム用の辞書は非シリアライズのため、プレイ中のインスペクタには表示・反映されません、\nつまりい、「ランタイム用クラスにはこのtenDayTempleteは何も表示されないのが正常です。」")]
    /// <summary>
    /// 基本の十日能力値、インスペクタで設定する。
    /// </summary>
    [SerializeField]
    TenDayAbilityDictionary _tenDayTemplate = new();
    /// <summary>
    /// ランタイムで使用する基本十日能力値（インスペクタ非対象）。
    /// </summary>
    [NonSerialized]
    TenDayAbilityDictionary _baseTenDayValues = new();
    /// <summary>
    /// 基本十日能力値データ構造への参照を返すメソッド
    /// 要は十日能力値の値をいじる用途
    /// </summary>
    public TenDayAbilityDictionary BaseTenDayValues
    {
        get { return _baseTenDayValues; }
    }

    protected void EnsureBaseTenDayValues()
    {
        if (_baseTenDayValues == null)
        {
            _baseTenDayValues = new TenDayAbilityDictionary();
        }
        if (_baseTenDayValues.Count > 0) return;
        if (_tenDayTemplate == null) return;

        foreach (var entry in _tenDayTemplate)
        {
            _baseTenDayValues[entry.Key] = entry.Value;
        }
    }
    /// <summary>
    /// 読み取り専用の十日能力値、直接代入しないで
    /// スキル専属十日値を参照するかは引数で指定する
    /// </summary>
    public ReadOnlyIndexTenDayAbilityDictionary TenDayValues(bool IsSkillEffect)
    {
        //武器ボーナスを参照する。
        var IsBladeSkill = false;
        var IsMagicSkill = false;
        var IsTLOASkill = false;
        if(NowUseSkill != null && IsSkillEffect)
        {
            IsBladeSkill = NowUseSkill.IsBlade;
            IsMagicSkill = NowUseSkill.IsMagic;
            IsTLOASkill = NowUseSkill.IsTLOA;
        }
        var weaponBonus = (NowUseWeapon != null)
            ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(IsBladeSkill, IsMagicSkill, IsTLOASkill)
            : new TenDayAbilityDictionary();

        if(NowUseWeapon == null)
        {
            Debug.LogError($"NowUseWeapon is null 武器にデフォルトで設定されるはずのIDが設定されてない。{CharacterName}");
        }
        // 素の十日能力（武器ボーナスなし）の合計値を出力
        //Debug.Log($"{CharacterName}の素の十日能力の合計値:{_baseTenDayValues.Values.Sum()}");
        var result = _baseTenDayValues + weaponBonus;
        //Debug.Log($"{CharacterName}の武器ボーナスを加えた十日能力の合計値:{result.Values.Sum()}");
        return new ReadOnlyIndexTenDayAbilityDictionary(result);
    }
    /// <summary>
    /// 十日能力の総量
    /// </summary>
    public float TenDayValuesSum(bool IsSkillEffect) => TenDayValues(IsSkillEffect).Values.Sum();
    /// <summary>
    /// 所持してる十日能力の中から、ランダムに一つ選ぶ
    /// </summary>
    public TenDayAbility GetRandomTenDayAbility()
    {
        return TenDayValues(true).Keys.ToArray().RandomElement();
    }
    /* ---------------------------------
     * 成長システム
     * --------------------------------- 
     */
    /// <summary>
    /// 十日能力の成長する、能力値を加算する関数
    /// </summary>
    void TenDayGrow(TenDayAbility ability, float growthAmount)
    {
        if (BaseTenDayValues.ContainsKey(ability))
        {
            BaseTenDayValues[ability] += growthAmount;
        }
        else
        {
            BaseTenDayValues[ability] = growthAmount;
        }

        if (battleGain.ContainsKey(ability))
        {
            battleGain[ability] += growthAmount;//勝利用ブーストのために記録する。
        }
        else
        {
            // 存在しない場合は新しく追加
            battleGain[ability] = growthAmount;
        }
    }
    /// <summary>
    /// 十日能力が割合で成長する関数。既存の値に対して指定された割合分を加算する。
    /// BaseTenDayValues と battleGain の両方に適用される。
    /// </summary>
    /// <param name="ability">成長させる能力の種類</param>
    /// <param name="percent">成長させる割合（例: 0.1f で10%増加）</param>
    public void TenDayGrowByPercentOfCurrent(TenDayAbility ability, float percent)
    {
        if (BaseTenDayValues.ContainsKey(ability))
        {
            // 成長量を計算（現在の値 * 割合）
            float growthAmount = BaseTenDayValues[ability] * percent;

            if(growthAmount <= 0)//成長量が0以下なら何もしない
            {
                return;
            }

            // BaseTenDayValues に成長量を加算
            BaseTenDayValues[ability] += growthAmount;

            // battleGain にも成長量を加算または新規追加
            if (battleGain.ContainsKey(ability))
            {
                battleGain[ability] += growthAmount;
            }
            else
            {
                battleGain[ability] = growthAmount;
            }
        }
        // BaseTenDayValuesにキーが存在しない場合は何もしない（成長の基となる値がないため）
    }    
    /// <summary>
    /// 十日能力の下降する、能力値を減算する関数（0未満にはならない）
    /// </summary>
    void TenDayDecrease(TenDayAbility ability, float decreaseAmount)
    {
        if (BaseTenDayValues.ContainsKey(ability))
        {
            BaseTenDayValues[ability] = Mathf.Max(0, BaseTenDayValues[ability] - decreaseAmount);
        }
        // 存在しない場合は何もしない

        // 勝利時ブーストは成長値を増やす奴なので、ここでは使わない。
    }    
    /// <summary>
    /// 十日能力の下降する、能力値を減算する関数（0未満にはならない）
    /// 割合を指定する際、ちゃんと0~1のRatioで指定する。
    /// </summary>
    public void TenDayDecreaseByPercent(TenDayAbility ability, float percent)
    {
        TenDayDecrease(ability, BaseTenDayValues[ability] * percent);
    }    

    /// <summary>
    /// ヒット分で伸びる十日能力の倍率と使用する印象構造を記録する。
    /// </summary>
    List<(float Factor, TenDayAbilityDictionary growTenDay)> TenDayGrowthListByHIT=new();
    /// <summary>
    /// スキル成長 引数で渡す倍率と十日能力の辞書から直接増加値を調整する。
    /// スキルを直接渡すのではなく、柔軟性のため成長する十日能力の辞書を渡す形式にした。
    /// </summary>
    void GrowTenDayAbilityBySkill(float Factor,TenDayAbilityDictionary growTenDay)
    {
        const float topValueThresholdRate = 0.6f;//トップ能力のしきい値　どのくらいの大きいのと同じような能力値をスキルの十日能力として比較するかの指標
        //精神属性を実際に構成している十日能力が実際にスキルの十日能力値と比較されるイメージ

        const float distanceAttenuationLimit = 15f;//距離をグラデーション係数に変える際の、一定以上の距離から0にカットオフし成長しないようにする値

        //現在の精神属性を構成する十日能力の中で最も大きいものを算出
        float topTenDayValue = 0f;
        Debug.Log($"(スキル成長)精神属性のチェック : {MyImpression},キャラ:{CharacterName}");
        if(MyImpression == SpiritualProperty.None || !SpritualTenDayAbilitysMap.ContainsKey(MyImpression))
        {
            Debug.Log($"キャラクター{CharacterName}の精神属性({MyImpression})が未設定またはnoneなので成長できません。");
            return;
        }
        foreach(var ten in SpritualTenDayAbilitysMap[MyImpression])
        {
            topTenDayValue = TenDayValues(true).GetValueOrZero(ten) > topTenDayValue ? TenDayValues(true).GetValueOrZero(ten) : topTenDayValue;
        }

        //トップ能力の60%以内の「十日能力の列挙体と値」を該当スキルの該当能力値との距離比較用にピックアップ
        List<(TenDayAbility,float)> pickupSpiritualTenDays = new List<(TenDayAbility,float)>();
        foreach(var ten in SpritualTenDayAbilitysMap[MyImpression])
        {
            var value = TenDayValues(true).GetValueOrZero(ten);
            if(value > topTenDayValue * topValueThresholdRate)
            {
                pickupSpiritualTenDays.Add((ten,value));
            }
        }

        
        
        foreach(var GrowSkillTenDayValue in growTenDay)//渡された十日能力の辞書に含まれてる全ての印象構造の十日能力分処理する。
        {
            // 加重平均用の変数
            float totalWeight = 0f;
            float weightedDistanceSum = 0f;
            
            // ピックアップした十日能力値全ての距離を加重平均する。
            foreach(var (myImpTen, value) in pickupSpiritualTenDays)
            {
                // 十日能力間の距離を計算
                float dist = TenDayAbilityPosition.GetDistance(myImpTen, GrowSkillTenDayValue.Key);
                
                // 能力値を重みとして使用
                totalWeight += value;
                weightedDistanceSum += dist * value;
            }
            
            // 加重平均距離を計算（totalWeightが0の場合は0とする）
            float averageDistance = totalWeight > 0 ? weightedDistanceSum / totalWeight : 0f;//全ての能力値がゼロだった場合の対策
            
            // 距離からグラデーション係数を計算（距離が遠いほど成長しにくくなる）
            float growthFactor = TenDayAbilityPosition.GetLinearAttenuation(averageDistance, distanceAttenuationLimit); // 15は最大距離の目安

            //グラデーション係数のデフォルト精神属性による救済処理
            if(growthFactor < 0.3f && SpritualTenDayAbilitysMap.ContainsKey(DefaultImpression))
            {
                var isHelp = true;
                foreach(var ten in SpritualTenDayAbilitysMap[DefaultImpression])//デフォルト精神属性の構成する十日能力で回す.
                {
                    //スキルの回してる十日能力とデフォルト精神属性の回してる十日能力間の距離が10より多いなら
                    if(TenDayAbilityPosition.GetDistance(ten, GrowSkillTenDayValue.Key) > 10)
                    {
                        isHelp = false;//foreachで一回でも当てはまってしまうと、falseとなり、救済は発生しません、
                        break;
                    }
                }
                if(isHelp) growthFactor = 0.35f;
            }

            //ある程度の自信ブーストを適用する
            var confidenceBoost = 1.0f;
            if(ConfidenceBoosts.ContainsKey(GrowSkillTenDayValue.Key))//自信ブーストの辞書に今回の能力値が含まれていたら
            {
                confidenceBoost = 1.3f + TenDayValues(true).GetValueOrZero(TenDayAbility.Baka) * 0.01f;
            }
            
            // 成長量を計算（スキルの該当能力値と減衰係数から）
            float growthAmount = growthFactor * GrowSkillTenDayValue.Value * Factor * confidenceBoost; 
            // グラデーション係数 × スキルの該当能力値 × 引数から渡された倍率　× 自信ブースト
            
            // 十日能力値を更新
            TenDayGrow(GrowSkillTenDayValue.Key, growthAmount);
        }
    }
    //                 [[[[[[[[[[[[[[[                            ーーーー
    //                                                          ある程度の自信ブースト
    //                                                          ーーーー                                ]]]]]]]]]]]]]]

    /// <summary>
    /// ある程度の自信ブーストを記録する辞書
    /// </summary>
    protected Dictionary<TenDayAbility, int> ConfidenceBoosts = new Dictionary<TenDayAbility, int>();

    /// <summary>
    /// ある程度の自信ブーストの相手の強さによって持続ターンを返す関数
    /// </summary>
    /// <returns></returns>
    int GetConfidenceBoostDuration(float ratio)
    {
        return ratio switch
        {
            < 1.77f => 2,
            < 1.85f => 3,
            < 2.0f => 4,
            < 2.2f => 5,
            < 2.4f => 6,
            < 2.7f => 9,
            < 3.0f => 12,
            < 3.3f => 14,
            < 3.4f => 16,
            < 3.6f => 17,
            < 3.8f => 20,
            < 3.9f => 21,
            < 4.0f => 23,
            _ => 23 + ((int)Math.Floor(ratio - 4.0f) * 2)  // 4以降は1増えるごとに2歩増える
        };
    }
    /// <summary>
    /// ある程度の自信ブーストを記録する。
    /// </summary>
    void RecordConfidenceBoost(BaseStates target,float allkilldmg)
    {
        // 相手との強さの比率を計算
        float opponentStrengthRatio = target.TenDayValuesSum(false) / TenDayValuesSum(true);
        //自分より1.7倍以上強い敵かどうか そうじゃないならreturn
        if(opponentStrengthRatio < 1.7f)return;
        //与えたダメージが敵の最大HPの半分以上与えてるかどうか、そうじゃないならreturn
        if(allkilldmg < target.MaxHP * 0.5f)return;

        //ブーストする十日能力を敵のデフォルト精神属性を構成する一番大きいの達から取得

        //デフォルト精神属性の十日能力たちを候補リストにする
        if(!SpritualTenDayAbilitysMap.ContainsKey(target.DefaultImpression)) return;
        var candidateAbilitiesList = SpritualTenDayAbilitysMap[target.DefaultImpression];
        //倒したキャラの十日能力値と合わせたリストにする。
        var candidateAbilitiyValuesList = new List<(TenDayAbility ability , float value)>();
        foreach(var ability in candidateAbilitiesList)
        {
            candidateAbilitiyValuesList.Add((ability, TenDayValues(false).GetValueOrZero(ability)));//列挙体と能力値を持つタプルのリストに変換
        }

        //複数ある場合は最大から降順で　何個ブーストされるかはパッシブや何かしらで補正されます。
        var boostCount = 1;//基本
        var walkturn = GetConfidenceBoostDuration(opponentStrengthRatio);
        for(var i = 0; i< boostCount; i++)
        {
            var MaxTenDayValue = candidateAbilitiyValuesList.Max(x => x.value);//リスト内で一番大きい値
            var MaxTenDayAbilities = candidateAbilitiyValuesList.Where(x => x.value == MaxTenDayValue).ToList();//最大キーと等しい値の能力値を全て取得

            //最大の値を持つデフォルト精神属性を構成する十日能力の内、同じ最大の値でダブってるからランダムで
            var boostAbility = MaxTenDayAbilities.ToArray().RandomElement();
            //ブーストを記録する
            ConfidenceBoosts.Add(boostAbility.ability,walkturn);//ブースト倍率は固定なので列挙体のみ記録すればok

            candidateAbilitiyValuesList.Remove(boostAbility);//今回取得した能力値と列挙体の候補セットリストを削除
        }
    }
    /// <summary>
    /// 歩行によって自信ブーストがフェードアウトする、やってることはただのデクリメント
    /// </summary>
    protected void FadeConfidenceBoostByWalking()
    {
        //辞書のキーをリストにしておく (そのまま foreach で書き換えるとエラーになる可能性がある)
        var keys = ConfidenceBoosts.Keys.ToList();

        //キーを回して、値を取り出し -1 して戻す
        foreach (var key in keys)
        {
            ConfidenceBoosts[key]--;
            
            //もし歩行ターンが0以下になったら削除する
            if (ConfidenceBoosts[key] <= 0) { ConfidenceBoosts.Remove(key); }
        }
    }


    //                 [[[[[[[[[[[[[[[                            ーーーー
    //                                                          勝利時ブースト
    //                                                          ーーーー                                ]]]]]]]]]]]]]]
    /// <summary>
    /// 十日能力成長値を勝利ブースト用に記録する
    /// </summary>
    protected TenDayAbilityDictionary battleGain = new();
    /// <summary>
    /// 勝利時の強さの比率から成長倍率を計算する
    /// </summary>
    protected float CalculateVictoryBoostMultiplier(float ratio)
    {
        // (最大比率, 対応する倍) を小さい順に並べる
        (float maxRatio, float multiplier)[] boostTable = new[]
        {
            // 6割以下
            (0.6f, 2.6f),
            // 6割 < 7割
            (0.7f, 5f),
            // 7割 < 8割
            (0.8f, 6.6f),
            // 8割 < 9割
            (0.9f, 7f),
            // 9割 < 12割
            (1.2f, 10f),
            // 12割 < 15割
            (1.5f, 12f),
            // 15割 < 17割
            (1.7f, 13f),
            // 17割 < 20割
            (2.0f, 16f),
            // 20割 < 24割
            (2.4f, 18f),
            // 24割 < 26割 (特別ゾーン)
            (2.6f, 20f),
            // 26割 < 29割 (少し下がる設定)
            (2.9f, 19f),
            // 29割 < 34割
            (3.4f, 24f),
            // 34割 < 38割
            (3.8f, 30f),
            // 38割 < 40割
            (4.0f, 31f),
            // 40割 < 42割
            (4.2f, 34f),
            // 42割 < 48割
            (4.8f, 36f),
        };

        // テーブルを順に判定して返す
        foreach (var (maxVal, multi) in boostTable)
        {
            if (ratio <= maxVal)
            {
                return multi;
            }
        }

        // 最後: 48割を超える場合は (ratio - 7) 倍
        // ただし (ratio - 7) が 1 未満になる場合の扱いは要相談。
        // ここでは最低1倍を保証する例を示す:
        float result = ratio - 7f;
        if (result < 1f) result = 1f;
        return result;
    }

    /// <summary>
    /// 勝利時の十日能力ブースト倍化処理
    /// </summary>
    public void VictoryBoost(float ratio)
    {
        // 勝利時の強さの比率から成長倍率を計算する
        var multiplier = CalculateVictoryBoostMultiplier(ratio);

        // 一時的なリストにキーをコピーしてから処理
        var abilities = battleGain.Keys.ToList();

        foreach (var ability in abilities)
        {
            float totalGained = battleGain[ability]; // 戦闘中に合計で上がった量
            float extra = totalGained * (multiplier - 1f);//リアルタイムで加算済みなので倍率から1減らす
            
            // 追加で足す
            BaseTenDayValues[ability] += extra;

            // battleGainに今回のバトルで上がった分をすべて代入する。
            battleGain[ability] = totalGained * multiplier;
        }

    }


    /* ---------------------------------
     * UI表示用
     * --------------------------------- 
     */

    /// <summary>
    /// UI表示用: 基本値と各スキル特判の「追加分のみ」を行データとして返す。
    /// - 基本値: TenDayValues(false) の値 = 素の値 + Normal武器補正
    /// - Normal武器補正: GetTenDayAbilityDictionary(false,false,false) から該当キーの値
    /// - 各スキル特判補正: (Normal+該当特判) - Normal の差分のみ
    /// 返却順は TenDayValues(false) の列挙順を維持する。
    /// </summary>
    public struct TenDayDisplayRow
    {
        public string Name;          // 能力名（ToString）
        public float BaseValue;      // 基本表示値（= 素 + Normal）
        public float NormalBonus;    // Normal のみの武器補正
        public float TloaBonus;      // TLOA の追加分のみ
        public float BladeBonus;     // 刃物 の追加分のみ
        public float MagicBonus;     // 魔法 の追加分のみ
    }

    public List<TenDayDisplayRow> GetTenDayDisplayRows()
    {
        var rows = new List<TenDayDisplayRow>();

        // 表示順は TenDayValues(false) に従う
        var baseWithNormal = TenDayValues(false);

        // Normal と各特判のフル(=Normal+特判)を用意
        var normalDict = (NowUseWeapon != null)
            ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(false, false, false)
            : new TenDayAbilityDictionary();
        var tloaFull = (NowUseWeapon != null)
            ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(false, false, true)
            : new TenDayAbilityDictionary();
        var bladeFull = (NowUseWeapon != null)
            ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(true, false, false)
            : new TenDayAbilityDictionary();
        var magicFull = (NowUseWeapon != null)
            ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(false, true, false)
            : new TenDayAbilityDictionary();

        // 差分 = (Normal+特判) - Normal
        // Dictionary的アクセスのため TryGetValue で個別キーを参照
        foreach (var kv in baseWithNormal)
        {
            var key = kv.Key;
            var name = key.ToDisplayText();

            float baseValue = kv.Value;
            float normalB = 0f;
            float tloaB = 0f;
            float bladeB = 0f;
            float magicB = 0f;

            if (normalDict != null && normalDict.TryGetValue(key, out var n)) normalB = n;
            if (tloaFull != null && tloaFull.TryGetValue(key, out var tf)) tloaB = tf - normalB;
            if (bladeFull != null && bladeFull.TryGetValue(key, out var bf)) bladeB = bf - normalB;
            if (magicFull != null && magicFull.TryGetValue(key, out var mf)) magicB = mf - normalB;

            rows.Add(new TenDayDisplayRow
            {
                Name = name,
                BaseValue = baseValue,
                NormalBonus = normalB,
                TloaBonus = tloaB,
                BladeBonus = bladeB,
                MagicBonus = magicB,
            });
        }

        return rows;
    }

    //  ==============================================================================================================================
    //                                              OverKillでのbroken確率
    //  ==============================================================================================================================
    [Header("OverKillでのbroken確率(このキャラが機械なら)")]
    [SerializeField]
    float _machineBrokenRate = 0.3f;//インスペクタで設定する際の初期デフォルト値
    const float _lifeBrokenRate = 0.1f;//生命の壊れる確率は共通の定数
    /// <summary>
    /// OverKillが発生した場合、壊れる確率
    /// </summary>
    float OverKillBrokenRate
    {
        get
        {
            if(MyType == CharacterType.Machine)
            {
                return _machineBrokenRate;
            }
            if(MyType == CharacterType.Life)
            {
                return _lifeBrokenRate;
            }
            // そのほかのタイプに対応していない場合は例外をスロー
            throw new NotImplementedException(
            $"OverKillBrokenRate is not implemented for CharacterType: {MyType}"
        );
        }
    }



    //  ==============================================================================================================================
    //                                              ライバハル
    //  ==============================================================================================================================

    /// <summary>
    /// ライバハル値
    /// </summary>
    [NonSerialized]
    public float Rivahal;
    /// <summary>
    /// 馴化定数: Rivahalが蓄積するほど新しい刺激の加算割合が減る速度を制御
    /// </summary>
    const float RIVAHAL_HABITUATION_K = 1.0f;
    /// <summary>
    /// TLOAスキルからのダメージ時、ライバハルの増える処理
    /// 総量方式 + 馴化式
    /// </summary>
    public void RivahalDream(BaseStates Atker,BaseSkill skill)
    {
        // 総量方式: スキル印象構造に一致する攻撃者十日能力の合計 / スキル印象構造の合計
        var attackerMatchSum = 0f;
        var skillSum = 0f;
        foreach(var tenDay in skill.TenDayValues(actor: Atker))
        {
            attackerMatchSum += Atker.TenDayValues(true).GetValueOrZero(tenDay.Key);
            skillSum += tenDay.Value;
        }
        var baseValue = attackerMatchSum / Mathf.Max(1f, skillSum);

        // 精神補正100%を適用
        var raw = baseValue * GetSkillVsCharaSpiritualModifier(skill.SkillSpiritual,Atker).GetValue();

        // 馴化式: raw² / (raw + Rivahal × K)
        // 蓄積が多いほど新しい刺激の加算割合が減る
        if(raw > 0f)
        {
            var habituation = raw * raw / (raw + Rivahal * RIVAHAL_HABITUATION_K);
            Rivahal += habituation;
        }
    }
    //  ==============================================================================================================================
    //                                             思えの値
    //  ==============================================================================================================================
    
    /* ------------------------------------------------------------------------------------------------------------------------------------------
    * 基本フィールド
    * ------------------------------------------------------------------------------------------------------------------------------------------
    */
   
    /// <summary>
    /// 思えの値　設定値
    /// 知能が低いほど高い(馬鹿は思えの鳥になりにくい)
    /// 思慮係数で知能の大体を決め　スキルの数での頭のほぐされ具合は微小に知能の高さとして影響する。
    /// </summary>
    public float ResonanceValue
    {
        get 
        { 
            //基本値
            var baseValue = TenDayValuesSum(false) * 0.56f;
            //スキル数による微小スケーリング
            baseValue = CalculateResonanceSkillCountMicroScaling(SkillList.Count, baseValue);
            //思慮係数によるスケーリング
            baseValue = CalculateResonanceThinkingScaling(baseValue, 11f);

            return baseValue + TenDayValues(false).GetValueOrZero(TenDayAbility.Baka) * 1.3f;//馬鹿を加算する 
        }
    }
    /// <summary>
    /// 思えの値用の各キャラクターに設定するユニークな思慮係数(知能？)
    /// 1~100でキャラの思慮深さを定義
    /// </summary>
    [Header("思念係数\n 1~100でキャラの思慮深さを定義")]
    [SerializeField][Range(1,100)] float _thinkingFactor;
    /// <summary>
    /// 思えの値の現在の値
    /// </summary>
    float _nowResonanceValue;
    /// <summary>
    /// 現在の思えの値
    /// </summary>
    public float NowResonanceValue
    {
        get
        {
            return _nowResonanceValue;
        }
        set
        {
            _nowResonanceValue = value;
            //最小値未満なら最小値にする。
            if(_nowResonanceValue < 0) _nowResonanceValue = 0;
            //最大値超えたら最大値にする。
            if(_nowResonanceValue > ResonanceValue) _nowResonanceValue = ResonanceValue;
        }
    }

    /* ------------------------------------------------------------------------------------------------------------------------------------------
    * ユーティリティ関数
    * ------------------------------------------------------------------------------------------------------------------------------------------
    */

    /// <summary>
    /// 思えの値をフルリセットする
    /// </summary>
    public void ResetResonanceValue() { NowResonanceValue = ResonanceValue; }
    /// <summary>
    /// 思えの値を回復する
    /// </summary>
    public void ResonanceHeal(float heal)
    {
        NowResonanceValue += heal;
        //最大値超えたら最大値にする。
        if(NowResonanceValue > ResonanceValue) NowResonanceValue = ResonanceValue;
    }
    /// <summary>
    /// 思えの値現在値をランダム化する
    /// </summary>
    public void InitializeNowResonanceValue() { NowResonanceValue = RandomSource.NextFloat(ResonanceValue * 0.6f, ResonanceValue); }
    const float _resonanceHealingOnWalkingFactor = 1f;
    /// <summary>
    /// 歩行時の思えの値回復
    /// </summary>
    public void ResonanceHealingOnWalking() 
    { 
        ResonanceHeal(_resonanceHealingOnWalkingFactor + TenDayValues(false).GetValueOrZero(TenDayAbility.SpringNap) * 1.5f);
    }

    //                 [[[[[[[[[[[[[[[                            ーーーー
    //                                                         スケーリング
    //                                                          ーーーー                                ]]]]]]]]]]]]]]

    /// <summary>
    /// スキルの数による微小スケーリング定数
    /// </summary>
    const float skillCountMicroScaling = 0.04f;
    /// <summary>
    /// スキル数による思えの値最大値の微小スケーリング
    /// </summary>
    public float CalculateResonanceSkillCountMicroScaling(int skillCount ,float resonanceValue)
    {
        return resonanceValue * (1 - (skillCount - 1 * skillCountMicroScaling));
        //スキルが二つ以上なら数に応じて引かれる
    }
    /// <summary>
    /// 思慮係数による思えの値最大値のスケーリング
    /// scalingMax の値を増やすほど、思慮係数 = 大元の知能が極端に低いキャラの思え最大値がより膨れ上がる設計
    /// </summary>
    /// <returns></returns>
    public float CalculateResonanceThinkingScaling(float ResonanceValue,float scaleMax)
    {
        float scale = Mathf.Lerp(scaleMax,1f,_thinkingFactor/100.0f);
        return ResonanceValue * scale;
    }


}
/// <summary>
///物理属性、スキルに依存し、キャラクター達の種別や個人との相性で攻撃の通りが変わる
/// </summary>
public enum PhysicalProperty
{
    heavy,
    volten,
    dishSmack //床ずれ、ヴぉ流転、暴断
    ,none
}
/// <summary>
/// 分解に対応した十日能力と非十日能力の内訳を持つ四大ステ保持クラス
/// </summary>
public class StatesPowerBreakdown
{
    /// <summary>
    /// 「TenDayAbilityからくる四大ステの内訳」
    /// </summary>
    public TenDayAbilityDictionary TenDayBreakdown { get; set; }

    /// <summary>
    /// 「非TenDayAbility要素 (固定値 等)」
    /// </summary>
    public float NonTenDayPart { get; set; }

    /// <summary>
    /// コンストラクタ
    /// 十日能力と非十日能力の初期値
    /// </summary>
    public StatesPowerBreakdown(TenDayAbilityDictionary tenDayBreakdown, float nonTenDayPart)
    {
        TenDayBreakdown = tenDayBreakdown;
        NonTenDayPart = nonTenDayPart;
    }

    /// <summary>
    /// 合計値を出すプロパティ
    /// </summary>
    public float Total
        => TenDayBreakdown.Values.Sum() + NonTenDayPart;
    public float TenDayValuesSum => TenDayBreakdown.Values.Sum();
    /// <summary>
    /// 十日能力追加
    /// </summary>
    public void TenDayAdd(TenDayAbility tenDayAbility, float value)
    {
        if (!TenDayBreakdown.ContainsKey(tenDayAbility))
        {
            TenDayBreakdown.Add(tenDayAbility, 0);
        }
        TenDayBreakdown[tenDayAbility] += value;
    }
    /// <summary>
    /// 非十日能力追加
    /// </summary>
    public void NonTenDayAdd(float value)
    {
        NonTenDayPart += value;
    }
    /// <summary>
    /// 該当の十日能力ステータス値を入手
    /// </summary>
    public float GetTenDayValue(TenDayAbility tenDayAbility)
    {
        return TenDayBreakdown.GetValueOrZero(tenDayAbility);
    }

    /// <summary>
    /// StatesPowerBreakdown同士の除算演算子
    /// 結果の合計値が left.Total / right.Total と同じになるように各値をスケーリングします
    /// </summary>
    public static StatesPowerBreakdown operator /(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        // ゼロ除算対策
        if (right.Total == 0)
        {
            // 右辺の合計が0の場合は左辺をそのまま返す
            return new StatesPowerBreakdown(
                new TenDayAbilityDictionary(left.TenDayBreakdown),
                left.NonTenDayPart);
        }
        
        // スケーリング係数を計算 (left.Total / right.Total)
        float scaleFactor = left.Total / right.Total;
        
        // 新しいDictionaryを作成
        var newTenDayBreakdown = new TenDayAbilityDictionary(left.TenDayBreakdown);
        
        // 全ての値をスケーリング係数で乗算
        foreach (var key in newTenDayBreakdown.Keys.ToList())
        {
            newTenDayBreakdown[key] *= scaleFactor;
        }
        
        // 非十日能力部分も同様にスケーリング
        float newNonTenDayPart = left.NonTenDayPart * scaleFactor;
        
        // 新しいStatesPowerBreakdownを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }

    /// <summary>
    /// 乗算演算子のオーバーロード - スカラー値による乗算
    /// 乗算補正で十日能力などが使われてたとしてもすべてに対するブーストなので、
    /// どんな十日能力に補正され乗算補正されたかの情報は持たないし、全ての値に乗算する。
    /// </summary>
    public static StatesPowerBreakdown operator *(StatesPowerBreakdown breakdown, float multiplier)
    {
        // 新しいDictionaryを作成（元のを変更しないため）
        var newTenDayBreakdown = new TenDayAbilityDictionary();
        
        // すべての十日能力の寄与値に乗算を適用
        foreach (var entry in breakdown.TenDayBreakdown)
        {
            newTenDayBreakdown[entry.Key] = entry.Value * multiplier;
        }
        
        // 非十日能力部分にも乗算を適用
        float newNonTenDayPart = breakdown.NonTenDayPart * multiplier;
        
        // 新しいStatesPowerBreakdownオブジェクトを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }
    /// <summary>
    /// float値とStatesPowerBreakdownの乗算演算子（交換法則対応）
    /// 各十日能力値と非十日能力値に同じfloat値を乗算します
    /// </summary>
    public static StatesPowerBreakdown operator *(float multiplier, StatesPowerBreakdown breakdown)
    {
        // 既存の StatesPowerBreakdown * float 演算子を利用（交換法則）
        return breakdown * multiplier;
    }
    /// <summary>
    /// 除算補正で十日能力などが使われてたとしてもすべてに対するブーストなので、
    /// どんな十日能力に補正され除算補正されたかの情報は持たないし、全ての値に除算する。
    /// </summary>
    public static StatesPowerBreakdown operator /(StatesPowerBreakdown breakdown, float divisor)
    {
        if (divisor == 0)
        {
            // ゼロ除算の場合はそのまま返す
            return breakdown;
        }
        
        // 新しい十日能力値の内訳を作成
        var newTenDayBreakdown = new TenDayAbilityDictionary();
        
        // 各十日能力値を割る
        foreach (var entry in breakdown.TenDayBreakdown)
        {
            newTenDayBreakdown[entry.Key] = entry.Value / divisor;
        }
        
        // 非十日能力値も割る
        float newNonTenDayPart = breakdown.NonTenDayPart / divisor;
        
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }

    /// <summary>
    /// float値でStatesPowerBreakdownを除算する演算子
    /// 結果の合計値が float / breakdown.Total と同じになるように各値をスケーリングします
    /// </summary>
    public static StatesPowerBreakdown operator /(float left, StatesPowerBreakdown right)
    {
        // ゼロ除算対策
        if (right.Total == 0)
        {
            // 右辺の合計が0の場合は、ゼロに近い小さな値を使用して除算を続行
            return new StatesPowerBreakdown(new TenDayAbilityDictionary(),left);
        }
        
        // スケーリング係数を計算 (left / right.Total)
        float scaleFactor = left / right.Total;
        
        // 新しいDictionaryを作成
        var newTenDayBreakdown = new TenDayAbilityDictionary();
        
        // 右辺の全ての十日能力値に対して、その逆数にスケーリング係数を掛ける
        foreach (var entry in right.TenDayBreakdown)
        {
            // 元の値との比率を反転させつつスケーリング
            newTenDayBreakdown[entry.Key] = entry.Value / right.Total * left;
        }
        
        // 非十日能力部分も同様にスケーリング
        float newNonTenDayPart = right.NonTenDayPart / right.Total * left;
        
        // 新しいStatesPowerBreakdownを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }





    /// <summary>
    /// StatesPowerBreakdown同士の加算演算子
    /// 十日能力ごとに対応する値を加算します
    /// </summary>
    public static StatesPowerBreakdown operator +(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        // 新しいDictionaryを作成
        var newTenDayBreakdown = new TenDayAbilityDictionary(left.TenDayBreakdown);
        
        // 右辺の十日能力値を加算
        foreach (var entry in right.TenDayBreakdown)
        {
            if (newTenDayBreakdown.ContainsKey(entry.Key))
            {
                newTenDayBreakdown[entry.Key] += entry.Value;
            }
            else
            {
                newTenDayBreakdown[entry.Key] = entry.Value;
            }
        }
        
        // 非十日能力部分も加算
        float newNonTenDayPart = left.NonTenDayPart + right.NonTenDayPart;
        
        // 新しいStatesPowerBreakdownを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }
    /// <summary>
    /// StatesPowerBreakdown同士の減算演算子
    /// 十日能力ごとに対応する値を減算します
    /// </summary>
    public static StatesPowerBreakdown operator -(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        // 新しいDictionaryを作成
        var newTenDayBreakdown = new TenDayAbilityDictionary(left.TenDayBreakdown);
        
        // 右辺の十日能力値を減算
        foreach (var entry in right.TenDayBreakdown)
        {
            if (newTenDayBreakdown.ContainsKey(entry.Key))
            {
                newTenDayBreakdown[entry.Key] -= entry.Value;
            }
            else
            {
                newTenDayBreakdown[entry.Key] = -entry.Value;
            }
        }
        
        // 非十日能力部分も減算
        float newNonTenDayPart = left.NonTenDayPart - right.NonTenDayPart;
        
        // 新しいStatesPowerBreakdownを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }

    /// <summary>
    /// スカラー値による加算演算子
    /// 非十日能力部分に加算されます
    /// </summary>
    public static StatesPowerBreakdown operator +(StatesPowerBreakdown breakdown, float value)
    {
        // 新しいオブジェクトを作成（十日能力部分はコピー）
        var newBreakdown = new StatesPowerBreakdown(
            new TenDayAbilityDictionary(breakdown.TenDayBreakdown), 
            breakdown.NonTenDayPart);
        
        // 加算は非十日能力部分に適用
        newBreakdown.NonTenDayPart += value;
        
        return newBreakdown;
    }
    /// <summary>
    /// スカラー値による減算演算子
    /// 非十日能力部分に減算されます
    /// </summary>
    public static StatesPowerBreakdown operator -(StatesPowerBreakdown breakdown, float value)
    {
        // 新しいオブジェクトを作成（十日能力部分はコピー）
        var newBreakdown = new StatesPowerBreakdown(
            new TenDayAbilityDictionary(breakdown.TenDayBreakdown), 
            breakdown.NonTenDayPart);
        
        // 減算は非十日能力部分に適用
        newBreakdown.NonTenDayPart -= value;
        
        return newBreakdown;
    }
    /// <summary>
    /// float値からStatesPowerBreakdownを引くための演算子
    /// 非十日能力部分に適用されます
    /// </summary>
    public static StatesPowerBreakdown operator -(float left, StatesPowerBreakdown right)
    {
        // 新しいStatesPowerBreakdownを作成
        // 十日能力の内訳は反転して保持（マイナス値にする）
        var newTenDayBreakdown = new TenDayAbilityDictionary();
        
        foreach (var entry in right.TenDayBreakdown)
        {
            newTenDayBreakdown[entry.Key] = -entry.Value;
        }
        
        // 非十日能力部分はfloat値から引く
        float newNonTenDayPart = left - right.NonTenDayPart;
        
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }
    /// <summary>
    /// 合計値が0未満になる場合は、非十日能力値を調整して0以上になるようにする
    /// 十日能力値は変更しない
    /// </summary>
    public void ClampToZero()
    {
        // 合計値を計算
        float total = Total;
        
        if (total < 0)
        {
            // 十日能力値の合計を計算
            float tenDayTotal = TenDayBreakdown.Values.Sum();
            
            // 非十日能力値を調整して、全体が0になるようにする
            // 非十日能力値が負の場合はそのまま0にせず、全体の合計が0になるよう調整
            if (tenDayTotal > 0)
            {
                // 十日能力値が正なら、合計が0になるよう非十日能力値を調整
                NonTenDayPart = -tenDayTotal;
            }
            else
            {
                // 十日能力値も負または0なら、非十日能力値を0にする
                NonTenDayPart = 0;
            }
        }
    }
    /// <summary>
    /// 大なり演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator >(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        return left.Total > right.Total;
    }

    /// <summary>
    /// 小なり演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator <(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        return left.Total < right.Total;
    }

    /// <summary>
    /// 以上演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator >=(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        return left.Total >= right.Total;
    }

    /// <summary>
    /// 以下演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator <=(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        return left.Total <= right.Total;
    }

    /// <summary>
    /// 等価演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator ==(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        if (ReferenceEquals(left, null))
            return ReferenceEquals(right, null);
        return left.Equals(right);
    }

    /// <summary>
    /// 非等価演算子のオーバーロード - Total値で比較する
    /// </summary>
    public static bool operator !=(StatesPowerBreakdown left, StatesPowerBreakdown right)
    {
        return !(left == right);
    }
    /// <summary>
    /// 単項マイナス演算子 - 全ての値の符号を反転
    /// </summary>
    public static StatesPowerBreakdown operator -(StatesPowerBreakdown value)
    {
        // 新しいDictionaryを作成
        var newTenDayBreakdown = new TenDayAbilityDictionary();
        
        // 各十日能力値の符号を反転
        foreach (var entry in value.TenDayBreakdown)
        {
            newTenDayBreakdown[entry.Key] = -entry.Value;
        }
        
        // 非十日能力部分も符号を反転
        float newNonTenDayPart = -value.NonTenDayPart;
        
        // 新しいStatesPowerBreakdownを返す
        return new StatesPowerBreakdown(newTenDayBreakdown, newNonTenDayPart);
    }

    /// <summary>
    /// オブジェクトの等価性を判定する - 全ての十日能力値と非十日能力値を比較
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is StatesPowerBreakdown other)
        {
            // 1. 非十日能力値の比較
            if (this.NonTenDayPart != other.NonTenDayPart)
                return false;
                
            // 2. 十日能力値の数が同じか確認
            if (this.TenDayBreakdown.Count != other.TenDayBreakdown.Count)
                return false;
                
            // 3. すべての十日能力値を比較
            foreach (var entry in this.TenDayBreakdown)
            {
                // キーが存在するか確認
                if (!other.TenDayBreakdown.TryGetValue(entry.Key, out float otherValue))
                    return false;
                    
                // 値が一致するか確認
                if (entry.Value != otherValue)
                    return false;
            }
            
            // すべての比較に合格したら等価
            return true;
        }
        return false;
    }

    /// <summary>
    /// ハッシュコードを取得する - 全ての値を考慮
    /// </summary>
    public override int GetHashCode()
    {
        unchecked // オーバーフローを許可
        {
            int hash = 17;
            hash = hash * 23 + NonTenDayPart.GetHashCode();
            
            foreach (var entry in TenDayBreakdown)
            {
                hash = hash * 23 + entry.Key.GetHashCode();
                hash = hash * 23 + entry.Value.GetHashCode();
            }
            
            return hash;
        }
    }
    /// <summary>
    /// StatesPowerBreakdownとfloatの大小比較（小なり）
    /// </summary>
    public static bool operator <=(StatesPowerBreakdown left, float right)
    {
        return left.Total <= right;
    }

    /// <summary>
    /// StatesPowerBreakdownとfloatの大小比較（大なり）
    /// </summary>
    public static bool operator >=(StatesPowerBreakdown left, float right)
    {
        return left.Total >= right;
    }

    /// <summary>
    /// StatesPowerBreakdownとfloatの大小比較（小なり）
    /// </summary>
    public static bool operator <(StatesPowerBreakdown left, float right)
    {
        return left.Total < right;
    }

    /// <summary>
    /// StatesPowerBreakdownとfloatの大小比較（大なり）
    /// </summary>
    public static bool operator >(StatesPowerBreakdown left, float right)
    {
        return left.Total > right;
    }
}
