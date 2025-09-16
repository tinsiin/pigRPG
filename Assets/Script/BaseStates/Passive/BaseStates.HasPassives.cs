using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using NRandom.Linq;
using Cysharp.Threading.Tasks;
using System.Linq;


//キャラクターの持ってるパッシブの関数
public abstract partial class BaseStates    
{

    //  ==============================================================================================================================
    //                                              パッシブコールバック
    //  ==============================================================================================================================

    /// <summary>
    /// 全パッシブの歩行時効果を呼ぶ
    /// </summary>
    protected void AllPassiveWalkEffect()
    {
        foreach (var pas in _passiveList)
        {
            if(pas.DurationWalkCounter > 0)
            {
                pas.WalkEffect();//歩行残存ターンが1以上でないと動作しない。
            }
        }
    }

    /// <summary>
    /// 割り込みカウンター発生時のパッシブ効果
    /// </summary>
    public bool PassivesOnInterruptCounter()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnInterruptCounter();
        }
        return true;
    }
    /// <summary>
    ///ダメージ直前のパッシブ効果
    /// </summary>
    public void PassivesOnBeforeDamage(BaseStates Atker)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnBeforeDamage(Atker);
        }
    }
    /// <summary>
    ///被害者がダメージを食らうその発動前
    /// 持ってるパッシブ中で一つでも　false(スキルは発動しない)があるなら、falseを返す
    /// </summary>
    public bool PassivesOnBeforeDamageActivate(BaseStates attacker)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            //一つでもfalseがあればfalseを返す
            if(!pas.OnBeforeDamageActivate(attacker)) return false;
        }
        return true;
    }
    /// <summary>
    ///ダメージ食らった後のパッシブ効果
    /// </summary>
    public void PassivesOnAfterDamage(BaseStates Atker, StatesPowerBreakdown damage)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnAfterDamage(Atker, damage);
        }
    }

    /// <summary>
    /// 攻撃後のパッシブ効果
    /// </summary>
    public void PassivesOnAfterAttack()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnAfterAttack();
        }
    }
    /// <summary>
    /// キャラ単位への攻撃後のパッシブ効果　　命中段階を伴った処理
    /// </summary>
    public void PassivesOnAfterAttackToTargetWithHitresult(BaseStates UnderAttacker,HitResult hitResult)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnAfterAttackToTargetWitheHitresult(UnderAttacker,hitResult);
        }
    }

    /// <summary>
    /// 完全単体選択系の攻撃の直後の全パッシブ効果　一人に対する攻撃ごとに使用
    /// </summary>
    public void PassivesOnAfterPerfectSingleAttack(BaseStates UnderAttacker)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnAfterPerfectSingleAttack(UnderAttacker);
        }
    }

    /// <summary>
    /// 味方や自分がダメージを食らった後のパッシブ効果
    /// </summary>
    public void PassivesOnAfterAlliesDamage(BaseStates Atker)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnAfterAlliesDamage(Atker);
        }
    }
    /// <summary>
    /// 対全体攻撃限定で、攻撃が食らう前のパッシブ効果
    /// </summary>
    public void PassivesOnBeforeAllAlliesDamage(BaseStates Atker,ref UnderActersEntryList underActers)
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnBeforeAllAlliesDamage(Atker,ref underActers);
        }
    }
    /// <summary>
    /// 次のターンに進むときのパッシブ効果
    /// </summary>
    public void PassivesOnNextTurn()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.OnNextTurn();
        }
    }

    //  ==============================================================================================================================
    //                                              パッシブ補正効果
    //  ==============================================================================================================================
    /// <summary>
    /// 戦闘開始時に「パッシブ」の持続歩行ターンの場合により、持続戦闘ターン数をリセットする
    /// </summary>
    public void PassivesReSetDurationTurnOnBattleStart()
    {
        foreach (var pas in Passives)//全ての所持パッシブ
        {
            //歩行ターンのif文戦闘ターンの処理分け
            if(pas.DurationWalk - 2 >= pas.DurationWalkCounter)//現在値が設定値-2　以下なら
            {
                //歩行ターンがどの程度減っているかを割合で入手
                var rate = pas.DurationWalkCounter / pas.DurationWalk;
                //その割合を戦闘ターン設定値に掛けて、再代入する。
                pas.DurationTurnCounter = pas.DurationTurn * rate;
                Debug.Log("戦闘パッシブのターンが歩行ターンが2以下になったので再変更の機会	→変化: " + pas.DurationTurnCounter + " / " + pas.DurationTurn 
                + "× " + rate);
            }
        }
    }
    /// <summary>
    /// ダメージに対して実行されるパッシブの減衰率
    /// 平均計減衰率が使われ、-1ならそもそも平均計算に使われない...
    /// </summary>
    void PassivesDamageReductionEffect(ref StatesPowerBreakdown damage)
    {
        // -1 を除外して有効な減衰率を収集
        var rates = _passiveList
            .Select(p => p.DamageReductionRateOnDamage)
            .Where(r => r >= 0f)
            .ToList();
        if (rates.Count == 0) return;

        // 平均減衰率を計算
        var avgRate = rates.Average();

        // ダメージに乗算
        damage *= avgRate;
    }
    
    /// <summary>
    /// 持ってるパッシブによるターゲットされる確率
    /// 平均化と実効化が行われる　1~100
    /// </summary>
    public float PassivesTargetProbability()
    {
        // -1 を除外して有効な確率を収集
        var rates = _passiveList
            .Select(p => p.TargetProbability)
            .Where(r => r >= 0f)
            .ToList();
        if (rates.Count == 0) return 0f;

        // 平均確率
        var avgRate = rates.Average();// -100～100 想定
        var sign = Mathf.Sign(avgRate);         // +1 or -1
        var absNorm = Mathf.Abs(avgRate) / 100f; // 0～1 正規化

        // FE式を適用（0.76閾値は 1.0 基準 → 0.76）
        float eff = absNorm <= 0.76f
            ? 2f * absNorm * absNorm
            : 1f - 2f * (1f - absNorm) * (1f - absNorm);

        // 符号を戻し、0～±100 にスケール
        return sign * eff * 100f;
    }
    /// <summary>
    /// 持ってるパッシブ全てのスキル発動率を平均化して返す
    /// このキャラクターのパッシブ由来のスキル発動率
    /// 全ての値が100% の場合　平均化しても100% その場合100として呼び出す元に返った時計算が省かれる
    /// </summary>
    /// <returns></returns>
    public float PassivesSkillActivationRate()
    {
        // -1 を除外して有効な確率を収集
        var rates = _passiveList
            .Select(p => p.SkillActivationRate())
            .Where(r => r >= 0f)//念のため0以上のみが入るようにする
            .ToList();
        if (rates.Count == 0) return 100f;//もし要素がなければ100を返す

        // 平均確率
        var avgRate = rates.Average();
        return avgRate;
    }
    /// <summary>
    /// パッシブによる回復補正率、下の方に補正される。
    /// </summary>
    public float PassivesHealEffectRate()
    {
        // -1 を除外して有効な確率を収集
        var rates = _passiveList
            .Select(p => p.HealEffectRate())
            .Where(r => r >= 0f) //念のため0以上のみが入るようにする
            .ToList();

        if (rates.Count == 0) return 100f; //もし要素がなければ100を返す

        // 要素を昇順に並べ替え
        var sortedRates = rates.OrderBy(r => r).ToList();
        
        // 考慮する要素数を計算 
        // 例:(小さい方から75%の個数、小数点以下切り上げ)
        // rates.Count = 1 => countToConsider = Ceiling(0.75) = 1
        // rates.Count = 2 => countToConsider = Ceiling(1.5)  = 2
        // rates.Count = 3 => countToConsider = Ceiling(2.25) = 3
        // rates.Count = 4 => countToConsider = Ceiling(3.0)  = 3
        int countToConsider = (int)Math.Ceiling(sortedRates.Count * 0.65f);

        // 上記の計算により、countToConsider は rates.Count > 0 なら必ず1以上になります。
        
        // 計算対象の要素を取得
        var takenRates = sortedRates.Take(countToConsider).ToList();
        
        // takenRatesが空になることは上記のロジックでは通常ありませんが、
        // リストが空でないことを保証してからAverageを呼び出すのがより安全です。
        if (!takenRates.Any()) 
        {
            // この状況は rates.Count > 0 の場合は発生しない想定です。
            // 万が一のためのフォールバック処理（例：元のリスト全体の平均や100fなど）
            // ここでは元のリスト全体の平均を返します。
            return rates.Average(); 
        }

        return takenRates.Average();
    }
    /// <summary>
    /// 攻撃行動時のパッシブ由来のケレン行動パーセント
    /// </summary>
    public float PassivesAttackACTKerenACTRate()
    {
        // デフォルト値以外の値を収集する。
        var rates = _passiveList
            .Select(p => p.AttackACTKerenACTRate())
            .Where(r => r != KerenACTRateDefault)
            .ToList();
        if (rates.Count == 0) return KerenACTRateDefault;//もし要素がなければデフォルト値を返す

        // 集めたデフォ値以外の値の中から、ランダムで一つ返す
        return rates.ToArray().RandomElement();
    }
    public float PassivesDefenceACTKerenACTRate()
    {
        // デフォルト値以外の値を収集する。
        var rates = _passiveList
            .Select(p => p.DefenceACTKerenACTRate())
            .Where(r => r != KerenACTRateDefault)
            .ToList();
        if (rates.Count == 0) return KerenACTRateDefault;//もし要素がなければデフォルト値を返す

        // 集めたデフォ値以外の値の中から、ランダムで一つ返す
        return rates.ToArray().RandomElement();
    }
        /// <summary>
    /// パッシブのパーセンテージ補正を返す  特別補正と違い一個一個掛ける
    ///  特別補正と違い、積と平均の中間を取る（ブレンド方式）　CalculateBlendedModifierのconstで操作
    /// </summary>
    public void PassivesPercentageModifier(whatModify mod,ref StatesPowerBreakdown value)
    {
        // 1) モディファイアを収集
        var factors = new List<float>();
        switch (mod)
        {
            case whatModify.atk:
                foreach (var pas in _passiveList) factors.Add(pas.ATKPercentageModifier());
                break;
            case whatModify.def:
                foreach (var pas in _passiveList) factors.Add(pas.DEFPercentageModifier());
                break;
            case whatModify.eye:
                foreach (var pas in _passiveList) factors.Add(pas.EYEPercentageModifier());
                break;
            case whatModify.agi:
                foreach (var pas in _passiveList) factors.Add(pas.AGIPercentageModifier());
                break;
            default:
                return;
        }
        if (factors.Count == 0) return;

        // 3) ブレンド乗算
        float blend = CalculateBlendedPercentageModifier(factors);
        value *= blend;
    }
    /// <summary>
    /// 持ってる全てのパッシブによる防御力パーセンテージ補正
    /// </summary>
    public float PassivesDefencePercentageModifierByAttacker()
    {
        // 各パッシブから「この攻撃者に対する防御倍率」を取得
        var factors = _passiveList
            .Select(p => p.CheckDefencePercentageModifierByAttacker(manager.Acter))
            .Where(mod => mod >= 0f);

        // ブレンドして返却　　
        return CalculateBlendedPercentageModifier(factors);
    }
    /// <summary>
    /// 持ってる全てのパッシブによる回避パーセンテージ補正
    /// </summary>
    public float PassivesEvasionPercentageModifierByAttacker()
    {
        // 各パッシブから「この攻撃者に対する回避倍率」を取得
        var factors = _passiveList
            .Select(p => p.CheckEvasionPercentageModifierByAttacker(manager.Acter))
            .Where(mod => mod >= 0f);

        // ブレンドして返却　　
        return CalculateBlendedPercentageModifier(factors);
    }

    /// <summary>
    /// 持ってるパッシブの中で一番大きいDontDamageRatioを探し、その割合を用いてHPをクランプする
    /// レイザーダメージは貫通する。(この補正がない。)
    /// </summary>
    void DontDamagePassiveEffect(BaseStates attacker = null)
    {
        float maxDontDamageHpMinRatio = 0;//使われない値は-1としてデフォ値が入ってるので、0ならば、比較時に0以上の値が入らない。
        foreach (var pas in _passiveList)
        {
            float ratio = attacker != null 
                ? pas.CheckDontDamageHpMinRatioNormalOrByAttacker(attacker)
                : pas.NormalDontDamageHpMinRatio;
                
            if (ratio > maxDontDamageHpMinRatio)
            {
                maxDontDamageHpMinRatio = ratio;
            }
        }
        
        // 「HPが下回らない最大HPの割合の最大値」より下回ってたらクランプ処理
        if (maxDontDamageHpMinRatio > 0)
        {
            int minHp = (int)(MaxHP * maxDontDamageHpMinRatio);
            HP = Math.Clamp(_hp, minHp, MaxHP);
        }
    }

    //  ==============================================================================================================================
    //                                              パッシブ生存関数
    //  ==============================================================================================================================

    /// <summary>
    /// 全スキルの、パッシブリストを全てが消えるかどうか
    /// </summary>
    void UpdateAllSkillPassiveSurvival()
    {
        //スキルのリスト
        foreach (var skill in SkillList)
        {
            //スキルが持つパッシブリストで回す
            foreach (var pas in skill.ReactiveSkillPassiveList)
            {
                pas.Update();
            }
        }
    }
    /// <summary>
    /// 全スキルの、パッシブリストを全てが歩行で消えるかどうか
    /// </summary>
    protected void UpdateAllSkillPassiveWalkSurvival()
    {
        //スキルのリスト
        foreach (var skill in SkillList)
        {
            //スキルが持つパッシブリストで回す
            foreach (var pas in skill.ReactiveSkillPassiveList)
            {
                pas.UpdateWalk();
            }
        }
    }

    /// <summary>
    /// 全パッシブのUpdateTurnSurvivalを呼ぶ ターン経過時パッシブが生存するかどうか
    /// NextTurnで呼び出す
    /// </summary>
    void UpdateTurnAllPassiveSurvival()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.UpdateTurnSurvival(this);
        }
    }
    /// <summary>
    /// 全パッシブが前のめり出ないときに消えるかどうかの判定をする
    /// RemoveOnNotVanguard = true 前のめり出ないなら消える
    /// </summary>
    void UpdateNotVanguardAllPassiveSurvival()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.UpdateNotVanguardSurvival(this);
        }
    }
    
    /// <summary>
    /// 全パッシブのUpdateWalkSurvivalを呼ぶ 歩行時パッシブが生存するかどうか
    /// </summary>
    protected void UpdateWalkAllPassiveSurvival()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.UpdateWalkSurvival(this);
        }
    }
    void UpdateDeathAllPassiveSurvival()
    {
        // 途中でRemoveされる可能性があるのでコピーを取ってから回す
        var copy = _passiveList.ToArray();
        foreach (var pas in copy)
        {
            pas.UpdateDeathSurvival(this);
        }
    }

    //  ==============================================================================================================================
    //                                              パッシブ特殊所持確認
    //  ==============================================================================================================================

    /// <summary>
    /// 自分が前のめりの時に味方を交代阻止するパッシブを一個でも持っているかどうか
    /// </summary>
    public bool HasBlockVanguardByAlly_IfImVanguard()
    {
        return _passiveList.Any(pas => pas.BlockVanguardByAlly_IfImVanguard);
    }

    

    
}