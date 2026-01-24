using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;
using static TenDayAbilityPosition;

public abstract partial class BaseStates    
{
    //  ==============================================================================================================================
    //                                              スキルパワー計算など
    //  ==============================================================================================================================
    
    
    /// <summary>
    /// スキル威力（本体/精神）を計算する共通ヘルパ（spreadを掛ける）。
    /// ReactionSkillOnBattle と同一の補正規則（TLOA=40%, それ以外=20%, 刃物で精神補正無効）。
    /// </summary>
    protected void ComputeSkillPowers(BaseStates attacker, BaseSkill skill, float spread, out float skillPower, out float skillPowerForMental)
    {
        var modifier = GetSkillVsCharaSpiritualModifier(skill.SkillSpiritual, attacker);
        var SpiritualModifierPercentage = 0.2f; // デフォは20%
        if (skill.IsTLOA) SpiritualModifierPercentage = 0.4f; // TLOAは40%

        var modifierForSkillPower = modifier.GetValue(SpiritualModifierPercentage);//精神補正値
        if (attacker.NowUseWeapon.IsBlade) modifierForSkillPower = 1.0f; // 刃物武器なら精神補正なし

        skillPower = skill.SkillPowerCalc(skill.IsTLOA) * modifierForSkillPower * spread;
        skillPowerForMental = skill.SkillPowerForMentalCalc(skill.IsTLOA) * modifier.GetValue() * spread; // 精神は100%
    }


    //  ==============================================================================================================================
    //                                              命中回避計算
    //  ==============================================================================================================================
    
    
    /* ---------------------------------
     * メイン関数
     * --------------------------------- 
     */
    /// <summary>
    /// 攻撃者と防御者とスキルを利用してヒットするかの計算
    /// </summary>
    private HitResult IsReactHIT(BaseStates Attacker)
    {
        AddBattleLog("IsReactHITが呼ばれた", true);
        var skill = Attacker.NowUseSkill;
        var minusMyChance = 0f;
        var minimumHitChancePer = CalcMinimumHitChancePer(Attacker,this);//ミニマムヒットチャンスの発生確率
        if(skill.IsMagic)minimumHitChancePer = 2f;

        //vanguardじゃなければ攻撃者の命中減少
        if (!manager.IsVanguard(Attacker))
        {
            //スキルのその場DontMove性の担保のため、前のめりの選択がないスキルは後衛でも命中低下しない
            if(skill.CanSelectAggressiveCommit)
            {//だから前のめり選べるスキルの場合のみ命中低下する。
                minusMyChance += AGI().Total * 0.2f;//チャンス計算だけだからTotal    
            }
        }

        if (minusMyChance > Attacker.EYE().Total)//マイナス対策
        {
            minusMyChance = Attacker.EYE().Total;
        }

        var minimumHitChanceResult= HitResult.CompleteEvade;//命中回避計算外のミニマムヒットチャンス
        if(rollper(minimumHitChancePer))//ミニマムヒットチャンス  ケレン行動パーセントの確率でかすりとクリティカルの計算
        {
            //三分の一で二分の一計算、三分の二ならステータス計算に入ります
            //三分の1でかすりとクリティカルは完全二分の一計算
            if(RandomEx.Shared.NextFloat(3) < 1)
            {
                if(RandomEx.Shared.NextFloat(2) < 1)
                {
                    minimumHitChanceResult = HitResult.Critical;
                    AddBattleLog("ミニマムヒットチャンスの確率計算-クリティカル", true);
                }
                else
                {
                    minimumHitChanceResult = HitResult.Graze;
                    AddBattleLog("ミニマムヒットチャンスの確率計算-かすり", true);
                }
            }else
            {//残り三分の二で、ステータス比較の計算
                var atkerCalcEYEAGI = Attacker.EYE().Total + Attacker.AGI().Total *0.6f;//minusMychanceは瀬戸際の攻防計算なので使用しない
                var defCalcEYEAGI = EYE().Total * 0.8f + AGI().Total;

                // 確率計算に使用する実効値を準備       多い方の値を1.7倍することで、より強さの絶対性を高める。
                float effectiveAtkerCalc = atkerCalcEYEAGI;
                float effectiveDefCalc = defCalcEYEAGI;
                // 多い方の値を1.7倍する
                if (atkerCalcEYEAGI > defCalcEYEAGI)
                {
                    effectiveAtkerCalc = atkerCalcEYEAGI * 1.7f;
                }
                else if (defCalcEYEAGI > atkerCalcEYEAGI)
                {
                    effectiveDefCalc = defCalcEYEAGI * 1.7f;
                }
                // atkerCalcEYEAGI == defCalcEYEAGI の場合は、どちらも変更しない（元の確率計算と同じ）

                
                if(RandomEx.Shared.NextFloat(effectiveAtkerCalc + effectiveDefCalc) < effectiveAtkerCalc)
                {
                    minimumHitChanceResult = HitResult.Critical;//攻撃者側のステータスが乱数で出たなら、クリティカル
                    AddBattleLog("ミニマムヒットチャンスのステータス比較計算-クリティカル", true);
                }
                else
                {
                    minimumHitChanceResult = HitResult.Graze;//そうでなければかすり
                    AddBattleLog("ミニマムヒットチャンスのステータス比較計算-かすり", true);
                }
            }
        }

        //術者の命中+被害者の自分の回避率　をMAXに　ランダム値が術者の命中に収まったら　命中。
        AddBattleLog(Attacker.CharacterName + "の命中率:" + Attacker.EYE().Total + CharacterName + "の回避率:" + EvasionRate(AGI().Total,Attacker), true);
        if (RandomEx.Shared.NextFloat(Attacker.EYE().Total + EvasionRate(AGI().Total,Attacker)) < Attacker.EYE().Total - minusMyChance || minimumHitChanceResult != HitResult.CompleteEvade)
        {
            var hitResult = minimumHitChanceResult;//ミニマムヒット前提でヒット結果変数に代入
            //ミニマムヒットがなく、かつ、通常の命中率が満たされた場合
            if(minimumHitChanceResult == HitResult.CompleteEvade)
            {
                hitResult = HitResult.Hit;//スキル命中に渡すヒット結果に通常のHitを代入
                AddBattleLog("IsReactHit-Hit", true);
            }
            AddBattleLog("通常Hitにより更なるスキル命中計算を実行", true);
            //スキルそのものの命中率 スキル命中率は基本独立させて、スキル自体の熟練度系ステータスで補正する？
            return skill.SkillHitCalc(this,AccuracySupremacy(Attacker.EYE().Total, AGI().Total), hitResult);
        }
        //回避されたので、まずは魔法スキルなら魔法かすりする　三分の一で
        //事前魔法かすり判定である。(攻撃性質スキル以外はスキル命中のみで魔法かすり判定をするという違いがある為。)
        if(skill.IsMagic && RandomEx.Shared.NextFloat(3) < 1)
        {
            //スキルそのものの命中率 スキル命中率は基本独立させて、スキル自体の熟練度系ステータスで補正する？
            return skill.SkillHitCalc(this,AccuracySupremacy(Attacker.EYE().Total, AGI().Total), HitResult.Graze, true);
        }


        //スキルが爆破型で、なおかつ被害者の自分が前のめりなら完全回避のはずがかすりになる
        if(skill.DistributionType == AttackDistributionType.Explosion && manager.IsVanguard(this))
        {
            var hitResult = HitResult.Graze;
            //が、AGI比較て勝ってたらそれを免除し本来の完全回避へ

            //三倍以上越してると84%で避けられる
            if(Attacker.AGI().Total * 3 < AGI().Total)
            {
                if(rollper(84))
                {
                    hitResult = HitResult.CompleteEvade;
                }
            }else
            {
                //攻撃者のAGIを1.6倍以上越していると、二分の一で避けられる。
                if(Attacker.AGI().Total * 1.6 < AGI().Total)
                {
                    if(RandomEx.Shared.NextFloat(2) < 1)
                    {
                        hitResult = HitResult.CompleteEvade;
                    }
                }
            }

            //爆破型なのでかすりだが、そもそものスキル命中の計算をする介する
            return skill.SkillHitCalc(this,AccuracySupremacy(Attacker.EYE().Total, AGI().Total), hitResult);
        }


        return HitResult.CompleteEvade;
    }

    /* ---------------------------------
     * ミニマムヒットチャンス
     * --------------------------------- 
     */

    /// <summary>
    /// ミニマムヒットチャンスの発生確率を計算。
    /// </summary>
    float CalcMinimumHitChancePer(BaseStates Attacker,BaseStates Defender)
    {
        const float synergy_threshold = 7f;//高め合いボーナスの発生しきい値　　0~100で
        const float  synergy_bonus_per = 0.2f;//高め合いボーナスの係数　ボーナスの大小を調整するのならここで。
        const float KereKereModifier_per = 0.09f;//ケレケレによる追加補正時にケレケレに掛ける係数

        var AtkerKerenRate = Attacker.PassivesAttackACTKerenACTRate();//攻撃側のケレン行動パーセント
        var DefenderKerenRate = Defender.PassivesDefenceACTKerenACTRate();//防御側のケレン行動パーセント

        //基本、攻撃側か防御側のどちらか大きい方が使われます
        var MinimumHitChanceRate = Math.Max(AtkerKerenRate, DefenderKerenRate);

        //もし両方とも高め合いボーナスの発生しきい値を上回っていたら発生
        if(AtkerKerenRate > synergy_threshold && DefenderKerenRate > synergy_threshold)
        {
            //どちらもデフォ値を引いて、加算する。
            var synergyPotential = AtkerKerenRate - KerenACTRateDefault + (DefenderKerenRate - KerenACTRateDefault);
            //調整用の係数を掛ける
            synergyPotential *= synergy_bonus_per;
            //基本確立に加算
            MinimumHitChanceRate += synergyPotential;
        }

        //大きい方の値が偶然の定数を上回っていた場合、その大きい方の十日能力ケレケレにより加算される。(高め合いボーナス計算後に)
        if(AtkerKerenRate>=DefenderKerenRate)//攻撃者側が大きいなら  攻撃者側のがバトルの主導は握りがちだと思うので、同値の場合も含める。
        {
            if(AtkerKerenRate > KerenACTRateDefault)//偶然の定数を上回っていたら
            {
                var AtkerKereKere = Attacker.TenDayValues(true).GetValueOrZero(TenDayAbility.KereKere);
                MinimumHitChanceRate += AtkerKereKere * KereKereModifier_per;
            }
        }
        else
        {
            if(DefenderKerenRate > KerenACTRateDefault)//偶然の定数を上回っていたら
            {
                var DefenderKereKere = Defender.TenDayValues(true).GetValueOrZero(TenDayAbility.KereKere);
                MinimumHitChanceRate += DefenderKereKere * KereKereModifier_per;
            }
        }
        
        
        return MinimumHitChanceRate;
    }

    //  ==============================================================================================================================
    //                                              SkillTypeの処理関数群
    //  ==============================================================================================================================

    /// <summary>
    /// 復活する際の関数
    /// </summary>
    public virtual void Angel()
    {
        if(!broken)//brokenしてないなら
        {
            m_BattleDeathProcessed = false;
            HP = float.Epsilon;//生きてるか生きてないか=Angel
            if(NowPower == ThePower.high)
            {
                HP = 30;//気力が高いと多少回復
            }
        }
    }
    /// <summary>
    /// ヒール
    /// </summary>
    /// <param name="HealPoint"></param>
    public virtual float Heal(float HealPoint)
    {
        if(!Death())
        {
            HP += HealPoint * (PassivesHealEffectRate() / 100f);//パッシブ由来の補正がかかる
            Debug.Log("ヒールが実行された");
            return HealPoint;
        }

        return 0f;
    }
    /// <summary>
    /// 精神HPのヒール処理
    /// </summary>
    /// <param name="HealPoint"></param>
    public virtual void MentalHeal(float HealPoint)
    {
        if(!Death())
        {
            MentalHP += HealPoint;
            Debug.Log("精神ヒールが実行された");
        }
    }


    /// <summary>
    /// 悪いパッシブ付与処理
    /// </summary>
    bool BadPassiveHit(BaseSkill skill,BaseStates grantor)
    {
        var hit = false;
        // Phase 3b: DI注入を優先、フォールバックでPassiveManager.Instance
        var provider = PassiveProvider ?? PassiveManager.Instance;
        foreach (var id in skill.SubEffects.Where(id => provider.GetAtID(id).IsBad))
        {//付与する瞬間はインスタンス作成のディープコピーがまだないので、passivemanagerで調査する
            ApplyPassiveBufferInBattleByID(id,grantor);
            hit = true;//goodpassiveHitに説明
        }
        return hit;
    }
    /// <summary>
    /// 悪いパッシブ解除処理
    /// </summary>
    bool BadPassiveRemove(BaseSkill skill)
    {
        var done = false;

        // skill.canEraceEffectIDs のIDを順にチェックして、
        // _passiveList の中で同じIDを持つパッシブを検索
        // そのパッシブが IsBad == true なら Remove する
        var rndList = skill.canEraceEffectIDs.ToArray();
        RandomEx.Shared.Shuffle(rndList);
        var decrement = 0;
        for(var i = 0; i < skill.Now_CanEraceEffectCount; i++)
        {
            // ID が一致するパッシブを検索 (存在しない場合は null)
            var found = _passiveList.FirstOrDefault(p => p.ID == rndList[i]);//解除するときはちゃんとインスタンスがあるので、passiveList内のインスタンスを調査する
            if (found != null && found.IsBad)
            {
                RemovePassiveByID(rndList[i]);
                done = true;
                decrement++;
            }
        }
        skill.Now_CanEraceEffectCount -= decrement;//使った分現在の消せる数を減らす
        return done;
    }
    /// <summary>
    /// 悪い追加HP付与処理
    /// </summary>
    bool BadVitalLayerHit(BaseSkill skill)
    {
        var done = false;
        foreach (var id in skill.subVitalLayers.Where(id => VitalLayerManager.Instance.GetAtID(id).IsBad))
        {
            ApplyVitalLayer(id);
            done = true;
        }
        return done;
    }
    /// <summary>
    /// 悪い追加HP解除処理
    /// </summary>
    bool BadVitalLayerRemove(BaseSkill skill)
    {
        var done = false;
        var rndList = skill.canEraceVitalLayerIDs.ToArray();
        RandomEx.Shared.Shuffle(rndList);
        var decrement = 0;
        for(var i=0; i<skill.Now_CanEraceVitalLayerCount; i++)
        {
            var found = _vitalLayerList.FirstOrDefault(v => v.id == rndList[i]);
            if (found != null && found.IsBad)
            {
                RemoveVitalLayerByID(rndList[i]);
                done = true;
                decrement++;
            }
        }
        skill.Now_CanEraceVitalLayerCount -= decrement;//使った分現在の消せる数を減らす
        return done;
    }
    /// <summary>
    /// 良いパッシブ付与処理
    /// </summary>
    bool GoodPassiveHit(BaseSkill skill,BaseStates grantor)
    {
        var hit = false;
        // Phase 3b: DI注入を優先、フォールバックでPassiveManager.Instance
        var provider = PassiveProvider ?? PassiveManager.Instance;
        foreach (var id in skill.SubEffects.Where(id => !provider.GetAtID(id).IsBad))
        {
            ApplyPassiveBufferInBattleByID(id,grantor);
            hit = true;//スキル命中率を介してるのだから、適合したかどうかはヒットしたかしないかに関係ない。 = ApplyPassiveで元々適合したかどうかをhitに代入してた
            //。。。バッファーリストの関係で一々シミュレイト用関数作るのがめんどくさかったけど、この考えが割と合理的だったからそうしたけどね。
            //それに、このhitしたのに敵になかったら、プレイヤーはこのパッシブの適合条件で適合しなかったことを察せれるし。

        }
        return hit;
    }
    /// <summary>
    /// 良いパッシブ解除処理
    /// </summary>
    bool GoodPassiveRemove(BaseSkill skill)
    {
        var done = false;
        var rndList = skill.canEraceEffectIDs.ToArray();
        RandomEx.Shared.Shuffle(rndList);
        var decrement = 0;
        for(var i=0; i<skill.Now_CanEraceEffectCount; i++)
        {
            var found = _passiveList.FirstOrDefault(p => p.ID == rndList[i]);
            if(found != null && !found.IsBad)
            {
                RemovePassiveByID(rndList[i]);
                done = true;
                decrement++;
            }
        }
        skill.Now_CanEraceEffectCount -= decrement;//使った分現在の消せる数を減らす
        return done;
    }
    /// <summary>
    /// 良いスキルパッシブ付与処理
    /// </summary>
    async UniTask<bool> GoodSkillPassiveHit(BaseSkill skill)
    {
        var hit = false;
        foreach (var pas in skill.AggressiveSkillPassiveList.Where(pas => !pas.IsBad))//スキルパッシブスキルに装弾されたスキルパッシブ
        {
            //スキルパッシブ付与対象のスキル
            var targetedSkill =  await skill.SelectSkillPassiveAddTarget(this);
            if(targetedSkill == null)continue;//付与対象のスキルがなかったら、次のループへ
            //付与対象のスキルで回す
            foreach(var targetSkill in targetedSkill)
            {
                targetSkill.ApplySkillPassiveBufferInBattle(pas);//スキルパッシブ追加用バッファに追加
            }

            //ヒットしたことを返すフラグ
            hit = true;//スキル命中率を介してるのだから。。。。[適合条件はスキルパッシブにはない]
        }
        return hit;
    }
    /// <summary>
    /// 悪いスキルパッシブ付与処理
    /// </summary>
    async UniTask<bool> BadSkillPassiveHit(BaseSkill skill)
    {
        var hit = false;
        foreach (var pas in skill.AggressiveSkillPassiveList.Where(pas => pas.IsBad))//スキルパッシブスキルに装弾されたスキルパッシブ
        {
            //スキルパッシブ付与対象のスキル
            var targetedSkill = await skill.SelectSkillPassiveAddTarget(this);
            if(targetedSkill == null)continue;//付与対象のスキルがなかったら、次のループへ
            //付与対象のスキルで回す
            foreach(var targetSkill in targetedSkill)
            {
                targetSkill.ApplySkillPassiveBufferInBattle(pas);//追加用バッファに追加
            }

            //ヒットしたことを返すフラグ
            hit = true;//スキル命中率を介してるのだから。。。。[適合条件はスキルパッシブにはない]
        }
        return hit;
    }
    /// <summary>
    /// 良いスキルパッシブ解除処理
    /// 各スキルパッシブのisbadで場合分けせずに、スキルそのもので場合分けするため、
    /// スキル攻撃性質でgoodかbad指定での友好的、敵対的の命中計算の場合分けをしているため、
    /// ここでgoodかbadを区別して処理する必要はないのでパッシブ除去と違いgood,badの冠詞は付かない
    /// </summary>
    bool SkillPassiveRemove(BaseSkill skill)
    {
        var done = false;
        //個別指定の反応式
        foreach(var hold in skill.ReactionCharaAndSkillList)//反応するキャラとスキルのリスト
        {
            //そもそもキャラ名が違っていたら、飛ばす
            if(CharacterName != hold.CharaName) continue;

            foreach(var targetSkill in SkillList)//対象者である自分の有効スキルすべてで回す。
            {
                if(targetSkill.SkillName == hold.SkillName)//スキル名まで一致したら
                {
                    targetSkill.SkillRemoveSkillPassive();//スキル効果ですべて抹消
                    done = true;
                    break;//スキルが一致して、他のスキルネームで検証する必要がなくなったので、次の対象スキルへ
                }
            }
        }

        return done;
    }
    /// <summary>
    /// 良い追加HP付与処理
    /// </summary>
    /// <param name="skill"></param>
    bool GoodVitalLayerHit(BaseSkill skill)
    {
        var done = false;
        foreach (var id in skill.subVitalLayers.Where(id => !VitalLayerManager.Instance.GetAtID(id).IsBad))
        {
            ApplyVitalLayer(id);
            done = true;
        }
        return done;
    }
    /// <summary>
    /// 良い追加HP解除処理
    /// </summary>
    /// <param name="skill"></param>
    bool GoodVitalLayerRemove(BaseSkill skill)
    {
        var done = false;
        var rndList = skill.canEraceVitalLayerIDs.ToArray();
        RandomEx.Shared.Shuffle(rndList);
        var decrement = 0;
        for(var i=0; i<skill.Now_CanEraceVitalLayerCount; i++)
        {
            var found = _vitalLayerList.FirstOrDefault(v => v.id == rndList[i]);
            if (found != null && !found.IsBad)
            {
                RemoveVitalLayerByID(rndList[i]);
                done = true;
                decrement++;
            }
        }
        skill.Now_CanEraceVitalLayerCount -= decrement;//使った分現在の消せる数を減らす
        return done;
    }
    /* ---------------------------------
     * 友好系処理
     * --------------------------------- 
     */
    
    /// <summary>
    /// 友好系: 良いパッシブ付与のコア（命中結果に従って適用）
    /// </summary>
    bool ExecuteAddPassiveFriendlyCore(BaseStates attacker, BaseSkill skill, HitResult hitResult)
    {
        if (hitResult != HitResult.Hit) return false;
        return this.GoodPassiveHit(skill, attacker);
    }
    /// <summary>
    /// 友好系: 良い追加HP付与のコア（命中結果に従って適用）
    /// </summary>
    bool ExecuteAddVitalLayerFriendlyCore(BaseStates attacker, BaseSkill skill, HitResult hitResult)
    {
        if (hitResult != HitResult.Hit) return false;
        this.GoodVitalLayerHit(skill);
        return true;
    }
    /// <summary>
    /// 友好系: 良いスキルパッシブ付与のコア（命中結果に従って適用）
    /// </summary>
    async UniTask<bool> ExecuteAddSkillPassiveFriendlyCore(BaseStates attacker, BaseSkill skill, HitResult hitResult)
    {
        if (hitResult != HitResult.Hit) return false;
        return await this.GoodSkillPassiveHit(skill);
    }
    /// <summary>
    /// 友好系: 良いパッシブ除去のコア（命中結果に従って適用）
    /// </summary>
    bool ExecuteRemovePassiveFriendlyCore(BaseStates attacker, BaseSkill skill, HitResult hitResult)
    {
        if (hitResult != HitResult.Hit) return false;
        // 既存実装にならい良いパッシブ除去は BadPassiveRemove() を使用
        return this.BadPassiveRemove(skill);
    }
    /// <summary>
    /// 友好系: 良い追加HP除去のコア（命中結果に従って適用）
    /// </summary>
    bool ExecuteRemoveVitalLayerFriendlyCore(BaseStates attacker, BaseSkill skill, HitResult hitResult)
    {
        if (hitResult != HitResult.Hit) return false;
        // 既存実装にならい良い追加HP除去は BadVitalLayerRemove() を使用
        return this.BadVitalLayerRemove(skill);
    }

    /// <summary>
    /// 友好系: 復活（DeathHeal）のコア（OnBattle版：相性連鎖含む）
    /// </summary>
    void ExecuteDeathHealFriendlyOnBattle(BaseStates attacker, HitResult hitResult, ref bool isHeal)
    {
        if (hitResult != HitResult.Hit) return;
        Angel();
        isHeal = true;
        manager.MyGroup(this).PartyApplyConditionChangeOnCloseAllyAngel(this);
    }
    /// <summary>
    /// 友好系: ヒールのコア（命中結果に従って適用）
    /// 適用量を返す
    /// </summary>
    float ExecuteHealFriendlyCore(float skillPower, HitResult hitResult, ref bool isHeal)
    {
        if (hitResult != HitResult.Hit) return 0f;
        isHeal = true;
        return Heal(skillPower);
    }
    /// <summary>
    /// 友好系: 精神ヒールのコア（命中結果に従って適用）
    /// </summary>
    void ExecuteMentalHealFriendlyCore(float skillPowerForMental, HitResult hitResult, ref bool isHeal)
    {
        if (hitResult != HitResult.Hit) return;
        isHeal = true;
        MentalHeal(skillPowerForMental);
    }

    /* ---------------------------------
     * 処理まとめ系
     * --------------------------------- 
     */

    /// <summary>
    /// 直接攻撃じゃない敵対行動系
    /// </summary>
    async UniTask<HostileEffectResult>
    ApplyNonDamageHostileEffects(BaseStates Atker,BaseSkill skill,HitResult hitResult)
    {
        var badPassiveHit = false;
        var badVitalLayerHit = false;
        var goodPassiveRemove = false;
        var goodVitalLayerRemove = false;
        var badSkillPassiveHit = false;
        var goodSkillPassiveRemove = false;

        var rndFrequency = 90;//ランダム発動率　基本的に100%発動する　　　　　　　　　　　　　　　　　　　　　　　　　　　微妙に攻撃タイプ以外の敵対行動を発動しにくくして、攻撃することの優位性を高める　ドキュメント記述なし
        if(hitResult == HitResult.Graze)
        {
            rndFrequency = 50;//かすりHitなので、二分の一で発動
        }        

        if (skill.HasType(SkillType.addPassive))
        {
            if (rollper(rndFrequency))
            {
                //悪いパッシブを付与しようとしてるのなら、命中回避計算
                badPassiveHit = BadPassiveHit(skill,Atker);
            }else
            {
                Debug.Log("悪いパッシブを付与が上手く発動しなかった。");
            }
        }

        if (skill.HasType(SkillType.AddVitalLayer))
        {
            if (rollper(rndFrequency))
            {
                //悪い追加HPを付与しようとしてるのなら、命中回避計算
                badVitalLayerHit = BadVitalLayerHit(skill);
            }else
            {
                Debug.Log("悪い追加HPを付与が上手く発動しなかった。");
            }
        }
        if(skill.HasType(SkillType.RemovePassive))
        {
            if (rollper(rndFrequency))
            {
                //良いパッシブを取り除こうとしてるのなら、命中回避計算
                goodPassiveRemove = GoodPassiveRemove(skill);
            }else
            {
                Debug.Log("良いパッシブを取り除くのが上手く発動しなかった。");
            }
        }
        if (skill.HasType(SkillType.RemoveVitalLayer))
        {
            if (rollper(rndFrequency))
            {
                //良い追加HPを取り除こうとしてるのなら、命中回避計算
                goodVitalLayerRemove = GoodVitalLayerRemove(skill);
            }else
            {
                Debug.Log("良い追加HPを取り除くのが上手く発動しなかった。");
            }
        }
        if(skill.HasType(SkillType.removeGoodSkillPassive))
        {
            if (rollper(rndFrequency))
            {
                //良いスキルパッシブを取り除こうとしてるのなら、命中回避計算
                goodSkillPassiveRemove = SkillPassiveRemove(skill);
            }else
            {
                Debug.Log("良い「スキル」パッシブを取り除くのが上手く発動しなかった。");
            }
        }
        if(skill.HasType(SkillType.addSkillPassive))
        {
            if (rollper(rndFrequency))
            {
                //悪いスキルパッシブを付与しようとしてるのなら、命中回避計算
                badSkillPassiveHit = await BadSkillPassiveHit(skill);
            }else
            {
                Debug.Log("悪い「スキル」パッシブを付与が上手く発動しなかった。");
            }
        }

        return new HostileEffectResult(badPassiveHit,
            badVitalLayerHit,
            goodPassiveRemove,
            goodVitalLayerRemove,
            badSkillPassiveHit,
            goodSkillPassiveRemove);
    }//async化すると非同期処理ではoutで引数渡す形にするとfalseで返ってだめらしいから戻り値構造体で返す


    //  ==============================================================================================================================
    //                                              バトル用メイン関数
    //  ==============================================================================================================================

    /// <summary>
    /// スキルに対するリアクション ここでスキルの解釈をする。
    /// BattleManager内での戦闘として呼び出すもの。
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="UnderIndex">攻撃される人の順番　スキルのPowerSpreadの順番に同期している</param>
    public virtual async UniTask<string> ReactionSkillOnBattle(BaseStates attacker, float spread)
    {
        // ポイント精算用: 最も強いヒット結果を保持
        HitResult bestHitOutcome = HitResult.none;
        int Rank(HitResult hr)
        {
            switch (hr)
            {
                case HitResult.Critical: return 4;
                case HitResult.Hit: return 3;
                case HitResult.Graze: return 2;
                case HitResult.CompleteEvade: return 1;
                default: return 0;
            }
        }
        void AccumulateHitResult(HitResult hr)
        {
            if (Rank(hr) > Rank(bestHitOutcome)) bestHitOutcome = hr;
        }
        
        //Debug.Log($"{attacker.CharacterName}の{attacker.NowUseSkill.SkillName}に対する{CharacterName}のReactionSkillが始まった");
        //Debug.Log($"スキルを受ける{CharacterName}の精神属性 = {MyImpression}:(ReactionSkill)");
        attacker.OnAttackerOneSkillActStart(this);//攻撃者の一人へのスキル実行開始時のコールバック

        var skill = attacker.NowUseSkill;

        // スキルパワーの精神属性による計算（共通ヘルパ使用で戦闘内外の整合を担保）
        ComputeSkillPowers(attacker, skill, spread, out var skillPower, out var skillPowerForMental);
        //Debug.Log($"{attacker.CharacterName}の{skill.SkillName}のスキルパワー = {skillPower} ,精神用スキルパワー = {skillPowerForMental} (ComputeSkillPowers)\n-スキルパワーの準備終了(ReactionSkill)");

        //メッセージテキスト用
        var txt = "";

        //発動するかどうか
        var thisAtkTurn = true;
        //攻撃が乱れたかどうか
        var isdisturbed = false;

        //被害記録用の一時保存boolなど
        var BadPassiveHit = false;
        var BadPassiveRemove = false;
        var GoodPassiveRemove = false;
        var GoodPassiveHit = false;
        var BadVitalLayerHit = false;
        var BadVitalLayerRemove = false;
        var GoodVitalLayerHit = false;
        var GoodVitalLayerRemove = false;
        var BadSkillPassiveHit = false;
        var GoodSkillPassiveHit = false;
        var BadSkillPassiveRemove = false;
        var GoodSkillPassiveRemove = false;
        var isHeal = false;
        var isAtkHit = false;
        var healAmount = 0f;
        var damageAmount = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);

        

        //スキルの持ってる性質を全て処理として実行
        Debug.Log($"{attacker.CharacterName}の{skill.SkillName}のスキル性質 = {skill.SkillType}(ReactionSkill)");

        //Manual1
        if(skill.HasType(SkillType.Manual1_GoodHitCalc))//良い攻撃
        {
            var hitResult = skill.SkillHitCalc(this);//良い攻撃なのでスキル命中のみ
            hitResult = MixAllyEvade(hitResult,attacker);//味方別口回避の発生と回避判定
            AccumulateHitResult(hitResult);

            skill.ManualSkillEffect(this,hitResult);//効果
        }
        if(skill.HasType(SkillType.Manual1_BadHitCalc))//悪い攻撃
        {
            var hitResult = IsReactHIT(attacker);//攻撃タイプでないので直接IsReactHitね
            hitResult = MixAllyEvade(hitResult,attacker);//味方別口回避の発生と回避判定
            AccumulateHitResult(hitResult);

            skill.ManualSkillEffect(this,hitResult);//効果
        }

        if (skill.HasType(SkillType.Attack))
        {
            Debug.Log($"{attacker.CharacterName}の{skill.SkillName}は攻撃タイプスキルで{CharacterName}はそれに対する個別反応を開始(ReactionSkill)");
            var hitResult = ATKTypeSkillReactHitCalc(attacker, skill);
            //味方別口回避の発生と回避判定
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult != HitResult.CompleteEvade)//完全回避以外なら = HITしてるなら
            {
                Debug.Log($"HITした{attacker.CharacterName}の{CharacterName}に対して{skill.SkillName}がかすり以上");
                //割り込みカウンターの判定
                if (skill.NowConsecutiveATKFromTheSecondTimeOnward())//連続攻撃されてる途中なら
                {
                    if(!skill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))//ターンをまたいだ物じゃないなら
                    {
                        Debug.Log($"割り込みカウンターの判定{attacker.CharacterName}の{skill.SkillName}に対するもので");
                        thisAtkTurn = !TryInterruptCounter(attacker);//割り込みカウンターの判定
                        if(!thisAtkTurn)
                        {
                            PassivesOnInterruptCounter();//割り込みカウンター発生時の効果
                        }
                    }
                }

                //攻撃される側からのパッシブ由来のスキル発動の可否を判定
                thisAtkTurn = PassivesOnBeforeDamageActivate(attacker);

                if(thisAtkTurn)
                {
                    Debug.Log($"{attacker.CharacterName}の{skill.SkillName}が{CharacterName}を攻撃した(発動成功)");
                    
                    //成功されるとダメージを受ける（戦闘版）
                    damageAmount = DamageOnBattle(attacker, skillPower,skillPowerForMental,hitResult,ref isdisturbed);
                    isAtkHit = true;//攻撃をしたからtrue

                    var result = await ApplyNonDamageHostileEffects(attacker,skill,hitResult);
                    BadPassiveHit = result.BadPassiveHit;
                    BadVitalLayerHit = result.BadVitalLayerHit;
                    GoodPassiveRemove = result.GoodPassiveRemove;
                    GoodVitalLayerRemove = result.GoodVitalLayerRemove;
                    BadSkillPassiveHit = result.BadSkillPassiveHit;
                    GoodSkillPassiveRemove = result.GoodSkillPassiveRemove;
                }
                else
                {
                    Debug.Log($"命中はしたが発動しなかった。{attacker.CharacterName}の{CharacterName}に対して{skill.SkillName}が");
                }
            }else   
            {
                AddBattleLog(attacker.CharacterName + "は外した");
            }
        }
        else//atktypeがないと各自で判定
        {
            var hitResult = IsReactHIT(attacker);
            //味方別口回避の発生と回避判定
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult != HitResult.CompleteEvade)
            {
                var result = await ApplyNonDamageHostileEffects(attacker,skill,hitResult);     
                BadPassiveHit = result.BadPassiveHit;
                BadVitalLayerHit = result.BadVitalLayerHit;
                GoodPassiveRemove = result.GoodPassiveRemove;
                GoodVitalLayerRemove = result.GoodVitalLayerRemove;
                BadSkillPassiveHit = result.BadSkillPassiveHit;
                GoodSkillPassiveRemove = result.GoodSkillPassiveRemove;   
            }
        }

        //回復系は常に独立
        if(skill.HasType(SkillType.DeathHeal))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            ExecuteDeathHealFriendlyOnBattle(attacker, hitResult, ref isHeal);
        }

        if (skill.HasType(SkillType.Heal))
        {
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);//味方別口回避の発生と回避判定
            AccumulateHitResult(hitResult);
            healAmount += ExecuteHealFriendlyCore(skillPower, hitResult, ref isHeal);
        }

        if (skill.HasType(SkillType.MentalHeal))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            ExecuteMentalHealFriendlyCore(skillPower, hitResult, ref isHeal);
        }

        //付与や除去系の友好的スキル(敵対的な物はApplyNonDamageHostileEffectsで処理)
        //攻撃要素あるにかかわらず、回復系と同じで独立している。

        if (skill.HasType(SkillType.addPassive))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            GoodPassiveHit = ExecuteAddPassiveFriendlyCore(attacker, skill, hitResult);
        }
        if (skill.HasType(SkillType.AddVitalLayer))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            GoodVitalLayerHit = ExecuteAddVitalLayerFriendlyCore(attacker, skill, hitResult);
        }
        if (skill.HasType(SkillType.addSkillPassive))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            GoodSkillPassiveHit = await ExecuteAddSkillPassiveFriendlyCore(attacker, skill, hitResult);
        }
        if (skill.HasType(SkillType.removeBadSkillPassive))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            if (hitResult == HitResult.Hit)//スキル命中率の計算だけ行う
            {
                //悪いパッシブを取り除くのなら、スキル命中のみ
                BadSkillPassiveRemove = SkillPassiveRemove(skill);
            }
        }


        if(skill.HasType(SkillType.RemovePassive))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            BadPassiveRemove = ExecuteRemovePassiveFriendlyCore(attacker, skill, hitResult);
        }
        if (skill.HasType(SkillType.RemoveVitalLayer))
        {
            //味方別口回避の発生と回避判定
            var hitResult = skill.SkillHitCalc(this);
            hitResult = MixAllyEvade(hitResult,attacker);
            AccumulateHitResult(hitResult);
            BadVitalLayerRemove = ExecuteRemoveVitalLayerFriendlyCore(attacker, skill, hitResult);
        }

        
        var arrowThicknessDamagePercent = 0.05f;//ダメージを表す矢印の太さ用　デフォは10%
        



        Debug.Log("ReactionSkillの反応部分終了、最後の処理の記録を開始");
        //攻撃者がヒットしたかどうかをタイプにより記録
        bool isAttackerHit;
        if (skill.HasType(SkillType.Attack))
        {
            isAttackerHit = thisAtkTurn;
        }else{
                isAttackerHit = BadPassiveHit || BadPassiveRemove || GoodPassiveHit || GoodPassiveRemove || 
                GoodVitalLayerHit || GoodVitalLayerRemove || BadVitalLayerHit || BadVitalLayerRemove ||
                BadSkillPassiveHit || GoodSkillPassiveHit || BadSkillPassiveRemove || GoodSkillPassiveRemove || isHeal;
        }
        //攻撃がいわゆるヒットをしたならば、
        if (isAttackerHit)
        {
            ImposedImpressionFromSkill(skill.SkillSpiritual,attacker);//特殊なスキル属性に影響されるか
            RivahalDream(attacker,skill);//ライバハルの上昇

            //攻撃者の成長処理 HIT分のスキルの印象構造の十日能力が上昇する。
            float growRate;
            if (skill.HasType(SkillType.Attack))
            {
                growRate = 0.7f;
            }
            else
            {
                growRate = 0.9f;
            }
            // 攻撃者とと攻撃相手の総量の比率を使用して比率を計算
            float clampedRatio = attacker.CalculateClampedStrengthRatio(TenDayValuesSum(false));

            //攻撃者のHIT分の成長を記録
            attacker.TenDayGrowthListByHIT.Add((growRate * clampedRatio, skill.TenDayValues(skill.IsTLOA)));//成長量にTLOAならゆりかごを考慮

            arrowThicknessDamagePercent = 0.2f;//ヒットしたら矢印の太さちょっと増やしとく

            // 薄い拡張フック（将来の拡張用）
            OnBattleAnyEffectHit(attacker, skill, skill.HasType(SkillType.Attack), bestHitOutcome);

        }


        //ここで攻撃者の攻撃記録を記録する
        attacker.ActDoOneSkillDatas.Add(new ACTSkillDataForOneTarget(thisAtkTurn,isdisturbed,skill,this,isAttackerHit));//発動したのか、何のスキルなのかを記録
        attacker.OnAttackerOneSkillActEnd();//攻撃者の一人へのスキル実行終了時のコールバック
        //被害の記録
        damageDatas.Add(new DamageData//クソ長い
        (isAtkHit,BadPassiveHit,BadPassiveRemove,GoodPassiveHit,GoodPassiveRemove,GoodVitalLayerHit,GoodVitalLayerRemove,
        BadVitalLayerHit,BadVitalLayerRemove,GoodSkillPassiveHit,GoodSkillPassiveRemove,BadSkillPassiveHit,BadSkillPassiveRemove,
        isHeal,skill,damageAmount.Total,healAmount,attacker));

        if(isAtkHit)//このboolは「攻撃性質」のスキルを食らったかどうかの判定になる。
        {
            //グループ全員分の「味方と自分」がダメージを食らった際のコールバックを呼び出す
            manager.MyGroup(this).PartyPassivesOnAfterAlliesDamage(attacker);
            //パッシブのダメージ食らった後のコールバックを呼び出す
            PassivesOnAfterDamage(attacker,damageAmount);
            //自分のグループの全体現在HPに対してくらったダメージの割合が線の太さに反映される
            var totalHP = manager.MyGroup(this).OurNowHP;
            var groupeHP = totalHP > 0f ? damageAmount.Total / totalHP : 1f;//もしグループHPが0以下なら　1fで100%の太さを指定(死体蹴りだから)
            arrowThicknessDamagePercent = groupeHP;
        }

        // 集約: 攻撃者へ最強ヒット結果を渡してキャスト単位で後で精算
        attacker.AggregateSkillHit(bestHitOutcome);

        //今回の攻撃結果を矢印の描画キューに
        // Phase 3d: DI注入を優先、フォールバックでBattleSystemArrowManager.Instance
        var arrowMgr = ArrowManager ?? BattleSystemArrowManager.Instance;
        arrowMgr?.Enqueue(attacker,this,arrowThicknessDamagePercent);

        return txt;
    }
    //  ==============================================================================================================================
    //                                              バトル用処理関数
    //  ==============================================================================================================================

    /// <summary>
    ///スキルの精神属性が特殊な場合、自分の精神属性が変化をしてしまう。
    /// </summary>
    void ImposedImpressionFromSkill(SpiritualProperty skillImp,BaseStates attacker)
    {
        switch(skillImp)
        {
            case SpiritualProperty.mvoid:
                MyImpression = attacker.MyImpression;
                break;
            case SpiritualProperty.Galvanize:
                MyImpression = attacker.MyImpression;
                break;
            case SpiritualProperty.air:
                //noneでも変化なし
                break;
            case SpiritualProperty.memento:
                //被害者には変化なし
                break;
            default:
                if(MyImpression == SpiritualProperty.none)
                {
                    MyImpression = skillImp;
                }
                break;
        }
    }

    /// <summary>
    /// 精神補正用にスキルの属性を精神属性用に特殊分岐した後に渡す関数
    /// </summary>
    SpiritualProperty GetCastImpressionToModifier(SpiritualProperty skillSpiritual,BaseStates attacker)
    {
        SpiritualProperty imp;
        switch (skillSpiritual)
        {
            case SpiritualProperty.mvoid:
            case SpiritualProperty.air:
                //精神補正無し
                imp = SpiritualProperty.none;//noneなら補正無しなので
                break;
            case SpiritualProperty.Galvanize:
                imp = attacker.MyImpression;
                break;
            case SpiritualProperty.memento:
                imp = attacker.DefaultImpression;
                break;
            default://基本的に精神属性をそのまま補正に渡す
                imp = skillSpiritual;
                break;
        }
        Debug.Log($"スキルの属性解釈 : {skillSpiritual},キャラ:{attacker.CharacterName}");
        return imp;
    }
    /// <summary>
    /// スキル攻撃とその被害者の場合の精神属性の補正を取り出す。
    /// 被害者側から呼び出す。 引数に攻撃スキルと攻撃者を
    /// </summary>
    FixedOrRandomValue GetSkillVsCharaSpiritualModifier(SpiritualProperty skillImp,BaseStates attacker)
    {
        var castSkillImp = GetCastImpressionToModifier(skillImp,attacker);//補正に投げる特殊スキル属性を純正な精神属性に変換

        if(castSkillImp == SpiritualProperty.none) return new FixedOrRandomValue(100);//noneなら補正なし(100%なので無変動)

        var key = (castSkillImp, MyImpression);
        if (!SpiritualModifier.ContainsKey(key))
        {
            Debug.LogWarning($"SpiritualModifier辞書にキー {key} が存在しません。攻撃スキルcastSkillImp={castSkillImp}({(int)castSkillImp}), スキルを受ける側MyImpression={MyImpression}。"
            +"デフォルト値100%を返します。\n(ReactionSkillの攻撃スキルと被害者の精神補正計算中)");
            return new FixedOrRandomValue(100);
        }

        var resultModifier = SpiritualModifier[key];//スキルの精神属性と自分の精神属性による補正
        
        if(attacker.DefaultImpression == skillImp)
        {
            resultModifier.RandomMaxPlus(12);//一致してたら12%程乱数上昇
        }
        Debug.Log($"スキルの精神属性補正 : {resultModifier}({skillImp}, {CharacterName})");
        return resultModifier;
    }

    /// <summary>
    /// 攻撃対象の十日能力総量と被害者の十日能力総量の比率を計算し返す
    /// 攻撃者から呼び出す
    /// </summary>
    private float CalculateClampedStrengthRatio(float targetSum)
        {
        // 自分のTenDayValuesSumとの比率を計算（自分の値が0の場合は1とする）
        float strengthRatio = TenDayValuesSum(true) > 0 ? targetSum / TenDayValuesSum(true) : 1f;
        
        return strengthRatio;
    }

    //  ==============================================================================================================================
    //                                              命中計算
    //  ==============================================================================================================================

    /// <summary>
    /// 直接攻撃スキルのを「食らう側のヒット判定のラッパー
    /// 命中回避を用いるかどうかなどをここで計算する。
    /// </summary>
    /// <returns></returns>
    HitResult ATKTypeSkillReactHitCalc(BaseStates attacker,BaseSkill atkSkill)
    {
        HitResult hitResult = HitResult.CompleteEvade;//念のため初期値を
        var HitResultSet = false;

        //善意攻撃であるのなら、スキル命中率のみ

        //攻撃者と被害者(自分)が味方同士で、かつ、
        if(manager.IsFriend(attacker, this))
        {
            //自分の持ってるパッシブに一つでも「行動不能」と「現存してる追加HPが生存条件である」プロパティの二つが同時に含まれていれば
            //または、「行動不能」と「RemoveOnDamage」の二つが同時に含まれていれば
            foreach (var pas in _passiveList)//自分の持ってるパッシブで回す
            {
                if(pas.IsCantACT)//行動不能のパッシブなら
                {
                    if(pas.RemoveOnDamage)//RemoveOnDamageが有効なら
                    {
                        hitResult = atkSkill.SkillHitCalc(this);//一個でも条件を満たせば善意攻撃なのでループを抜けていい
                        HitResultSet = true;
                        break;
                    }
                    if(pas.VitalLayers == null) continue;//パッシブに追加HPが無ければ飛ばす

                    if(pas.HasRemainingSurvivalVitalLayer(this))//生存条件としてのVitalLayerを今持っているかどうか
                    {
                        hitResult = atkSkill.SkillHitCalc(this);//一個でも条件を満たせば善意攻撃なのでループを抜けていい
                        HitResultSet = true;
                        break;
                    }
                }
            }
        }
        if(!HitResultSet)
        {
            AddBattleLog("攻撃スキルヒット判定でisreactHitが呼ばれた。", true);
            hitResult = IsReactHIT(attacker);//善意ヒット判定が未代入なら通常のヒット判定
        }

        Debug.Log($"攻撃スキルヒット判定結果 : {hitResult}({attacker.CharacterName}の{atkSkill.SkillName}の{CharacterName}に対する判定)");
        return hitResult;
    }
    /// <summary>
    /// AllyEvade 計算  ➜  既存 HitResult と合成して返すショートハンド
    /// 味方別口回避
    /// </summary>
    HitResult MixAllyEvade(HitResult existingHit, BaseStates attacker)
    {
        var allyEvade = AllyEvadeCalculation(attacker);
        var hitresult = AllyEvade_HitMixDown(existingHit, allyEvade);
        Debug.Log($"味方別口回避結果 : {existingHit} → {hitresult}");
        return hitresult;
    }
    /// <summary>
    /// 命中結果の合算
    /// 主に味方別口回避と既存の計算結果を混ぜるため
    /// MIXテーブルの通りにAIに実装してもらたｗ
    /// </summary>
    HitResult AllyEvade_HitMixDown(HitResult existingHit, HitResult allyEvadeHit)
    {
        if(allyEvadeHit == HitResult.none)
        {//味方別口回避がなければ、既存のをそのまま返す
            return existingHit;
        }

        // 完全回避 + 完全回避 または かすり + 完全回避
        if ((existingHit == HitResult.CompleteEvade && allyEvadeHit == HitResult.CompleteEvade) ||
            (existingHit == HitResult.CompleteEvade && allyEvadeHit == HitResult.Graze) ||
            (existingHit == HitResult.Graze && allyEvadeHit == HitResult.CompleteEvade))
        {
            return HitResult.CompleteEvade;
        }
        
        // HIT + 完全回避 = かすり
        if ((existingHit == HitResult.Hit && allyEvadeHit == HitResult.CompleteEvade) ||
            (existingHit == HitResult.CompleteEvade && allyEvadeHit == HitResult.Hit))
        {
            return HitResult.Graze;
        }
        
        // クリティカル + 完全回避 = HITまたは1/2の確率でかすり
        if ((existingHit == HitResult.Critical && allyEvadeHit == HitResult.CompleteEvade) ||
            (existingHit == HitResult.CompleteEvade && allyEvadeHit == HitResult.Critical))
        {
            // 50%の確率でかすり、それ以外はHIT
            return rollper(50) ? HitResult.Graze : HitResult.Hit;
        }
        
        // かすり + HIT = かすり
        if ((existingHit == HitResult.Graze && allyEvadeHit == HitResult.Hit) ||
            (existingHit == HitResult.Hit && allyEvadeHit == HitResult.Graze))
        {
            return HitResult.Graze;
        }
        
        // クリティカル + HIT = HIT
        if ((existingHit == HitResult.Critical && allyEvadeHit == HitResult.Hit) ||
            (existingHit == HitResult.Hit && allyEvadeHit == HitResult.Critical))
        {
            return HitResult.Hit;
        }
        
        // クリティカル + かすり = HIT
        if ((existingHit == HitResult.Critical && allyEvadeHit == HitResult.Graze) ||
            (existingHit == HitResult.Graze && allyEvadeHit == HitResult.Critical))
        {
            return HitResult.Hit;
        }
        
        // それ以外の場合は、同じ値ならそのまま返す
        if (existingHit == allyEvadeHit)
        {
            return existingHit;
        }
        
        // 上記以外のケースは、ここには来ないはずだが、安全のため
        // 値が小さい方（より回避側）を選択
        return ((int)existingHit < (int)allyEvadeHit) ? existingHit : allyEvadeHit;


    }

    //  ==============================================================================================================================
    //                                              イベント用メイン関数
    //                  命中計算等を行わない、BattleManager外で使われるスキル確定実行用関数
    //  ==============================================================================================================================
    
    
    /// <summary>
    /// 戦闘外のシンプルなスキル適用（純粋効果のみ）。
    /// - 命中/回避・割り込み・記録・矢印・成長などの戦闘専用処理は行わない。
    /// - パッシブ/スキルパッシブのバッファは即時コミットする。
    /// - ダメージは Hit 扱いで簡易 Damage() を適用する。
    /// </summary>
    public async UniTask<SimpleSkillOutcomeOutOfBattle> ApplySkillCoreOutOfBattle(BaseStates attacker, BaseSkill skill)
    {
        return await ApplySkillCoreOutOfBattle(attacker, skill, SkillApplyPolicy.OutOfBattleDefault);
    }

    public async UniTask<SimpleSkillOutcomeOutOfBattle> ApplySkillCoreOutOfBattle(BaseStates attacker, BaseSkill skill, SkillApplyPolicy policy)
    {
        policy ??= SkillApplyPolicy.OutOfBattleDefault;
        ComputeSkillPowers(attacker, skill, 1.0f, out var skillPower, out var skillPowerForMental);

        var outcome = new SimpleSkillOutcomeOutOfBattle();
        bool any = false;
        bool isdisturbed = false;

        // 戦闘外でも最小限の事前準備（戦闘時の OnAttackerOneSkillActStart 相当の一部）
        // ・除去回数の補充（Remove/Erase 系の動作を有効化）
        // ・ゆりかご計算（威力計算の整合）
        skill.RefilCanEraceCount();
        skill.CalcCradleSkillLevel(attacker);

        // 攻撃
        if (skill.HasType(SkillType.Attack))
        {
            var hr = HitResult.Hit;
            if (policy.UseHitEvade)
            {
                hr = ATKTypeSkillReactHitCalc(attacker, skill);
                if (policy.UseAllyEvade)
                {
                    hr = MixAllyEvade(hr, attacker);
                }
            }

            if (hr != HitResult.CompleteEvade)
            {
                var dmg = Damage(attacker, skill, skillPower, skillPowerForMental, hr, ref isdisturbed, policy.Damage);
                outcome.DamageDealt = dmg.Total;
                any = any || dmg.Total > 0f;
            }
        }

            // 回復
            if (skill.HasType(SkillType.Heal))
            {
                if (!policy.GateFriendlyByHit || SkillHitPassed(attacker, skill, policy))
                {
                    outcome.HealAmount += Heal(skillPower);
                    any = true;
                }
            }
            if (skill.HasType(SkillType.MentalHeal))
            {
                if (!policy.GateFriendlyByHit || SkillHitPassed(attacker, skill, policy))
                {
                    MentalHeal(skillPowerForMental);
                    outcome.DidMentalHeal = true;
                    any = true;
                }
            }

            // 付与・除去（友好系）
            if (skill.HasType(SkillType.addPassive))
            {
                var ok = (!policy.GateFriendlyByHit || SkillHitPassed(attacker, skill, policy)) && GoodPassiveHit(skill, attacker);
                outcome.DidAddPassive = ok;
                any = any || ok;
            }
            if (skill.HasType(SkillType.AddVitalLayer))
            {
                var ok = (!policy.GateFriendlyByHit || SkillHitPassed(attacker, skill, policy)) && GoodVitalLayerHit(skill);
                outcome.DidAddVitalLayer = ok;
                any = any || ok;
            }
            if (skill.HasType(SkillType.addSkillPassive))
            {
                var ok = false;
                if (!policy.GateFriendlyByHit || SkillHitPassed(attacker, skill, policy))
                {
                    ok = await GoodSkillPassiveHit(skill);
                }
                outcome.DidAddSkillPassive = ok;
                any = any || ok;
            }
            if (skill.HasType(SkillType.removeBadSkillPassive))
            {
                var ok = false;
                if (!policy.GateFriendlyByHit || SkillHitPassed(attacker, skill, policy))
                {
                    ok = SkillPassiveRemove(skill);
                }
                outcome.DidRemoveSkillPassive = ok;
                any = any || ok;
            }
            if (skill.HasType(SkillType.RemovePassive))
            {
                var ok = false;
                if (!policy.GateFriendlyByHit || SkillHitPassed(attacker, skill, policy))
                {
                    ok = BadPassiveRemove(skill);
                }
                outcome.DidRemovePassive = ok;
                any = any || ok;
            }
            if (skill.HasType(SkillType.RemoveVitalLayer))
            {
                var ok = false;
                if (!policy.GateFriendlyByHit || SkillHitPassed(attacker, skill, policy))
                {
                    ok = BadVitalLayerRemove(skill);
                }
                outcome.DidRemoveVitalLayer = ok;
                any = any || ok;
            }

            // 復活系（戦闘連鎖は policy で制御）
            if (skill.HasType(SkillType.DeathHeal))
            {
                if (!policy.GateFriendlyByHit || SkillHitPassed(attacker, skill, policy))
                {
                    Angel();
                    if (policy.UsePartyAngelChain && manager != null)
                    {
                        manager.MyGroup(this)?.PartyApplyConditionChangeOnCloseAllyAngel(this);
                    }
                    outcome.DidAngel = true;
                    any = true;
                }
            }

            // バッファは直ちにコミット（戦闘外）
            if (policy.CommitBuffersImmediately)
            {
                ApplyBufferApplyingPassive();
                ApplySkillsBufferApplyingSkillPassive();
            }

            // 戦闘内と同じロジック：何らかの効果がヒットした場合、スキルの特殊属性による精神変化を適用
            if (policy.ApplyImposedImpression && any)
            {
                ImposedImpressionFromSkill(skill.SkillSpiritual, attacker);
            }

            outcome.AnyEffect = any;
            return outcome;
        }

    /* ---------------------------------
     * 関連関数
     * --------------------------------- 
     */

    /// <summary>
    /// 友好系ポリシーの命中可否（SkillHitCalc と AllyEvade を使用）
    /// </summary>
    private bool SkillHitPassed(BaseStates attacker, BaseSkill skill, SkillApplyPolicy policy)
    {
        if (!policy.UseHitEvade) return true;
        var hr = skill.SkillHitCalc(this);
        if (policy.UseAllyEvade)
        {
            hr = MixAllyEvade(hr, attacker);
        }
        return hr == HitResult.Hit;
    }

    /// <summary>
    /// 戦闘外の純粋効果結果
    /// </summary>
    public struct SimpleSkillOutcomeOutOfBattle
    {
        public float DamageDealt;
        public float HealAmount;
        public bool DidMentalHeal;
        public bool DidAddPassive;
        public bool DidRemovePassive;
        public bool DidAddVitalLayer;
        public bool DidRemoveVitalLayer;
        public bool DidAddSkillPassive;
        public bool DidRemoveSkillPassive;
        public bool DidAngel;
        public bool AnyEffect;
    }
    




}
/// <summary>
/// 命中結果の種類を表す列挙型
/// </summary>
public enum HitResult
{
    /// <summary>完全回避（ノーダメージ）</summary>
    CompleteEvade,
    
    /// <summary>かすり（割合少ないダメージ）</summary>
    Graze,
    
    /// <summary>通常ヒット</summary>
    Hit,
    
    /// <summary>クリティカルヒット（大ダメージ）</summary>
    Critical,
    /// <summary>結果ナシ</summary>
    none
}
/// <summary>
/// 敵対的行動のヒット結果を返す構造体
/// </summary>
public struct HostileEffectResult
{
    public bool BadPassiveHit;
    public bool BadVitalLayerHit;
    public bool GoodPassiveRemove;
    public bool GoodVitalLayerRemove;
    public bool BadSkillPassiveHit;
    public bool GoodSkillPassiveRemove;

    public HostileEffectResult(
        bool badPassiveHit,
        bool badVitalLayerHit,
        bool goodPassiveRemove,
        bool goodVitalLayerRemove,
        bool badSkillPassiveHit,
        bool goodSkillPassiveRemove)
    {
        BadPassiveHit        = badPassiveHit;
        BadVitalLayerHit     = badVitalLayerHit;
        GoodPassiveRemove    = goodPassiveRemove;
        GoodVitalLayerRemove = goodVitalLayerRemove;
        BadSkillPassiveHit   = badSkillPassiveHit;
        GoodSkillPassiveRemove = goodSkillPassiveRemove;
    }
}
