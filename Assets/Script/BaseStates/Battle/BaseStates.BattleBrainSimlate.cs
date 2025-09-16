using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;


//敵スキルAIに対する全キャラシミュレート用
//シミュレートなので　厳密に実際のBM上での実行計算を再現する必要がない。
public abstract partial class BaseStates    
{

    //  ==============================================================================================================================
    //                                              メインのシミュレート関数とか
    //  ==============================================================================================================================

    /// <summary>
    /// AIのブルートフォースダメージシミュレイト用関数
    /// </summary>
    public float SimulateDamage(BaseStates attacker, BaseSkill skill, SkillAnalysisPolicy policy)
    {
        //スキルパワー取得
        //damage関数と違う内容　spread,敵による分散値が乗算されない
        var simulateHP = HP;//計算用HP
        var simulateMentalHP = MentalHP;//計算用精神HP

        // 統一: 戦闘内外の威力計算規則に揃える（spread=1.0固定）
        ComputeSkillPowers(attacker, skill, 1.0f, out var skillPower, out var skillPowerForMental);
        // AIポリシーで精神補正を無効化したい場合は、素の威力へリセット（完全に精神補正を排除）
        if(!policy.spiritualModifier)
        {
            skillPower = skill.SkillPowerCalc(skill.IsTLOA);
            skillPowerForMental = skill.SkillPowerForMentalCalc(skill.IsTLOA);
        }

        //防御力（ポリシーに応じた簡易/完全シミュレーション）
        StatesPowerBreakdown def;
        if (policy.SimlatePerfectEnemyDEF)
        {
            // 従来通りの完全なDEF計算を使用（パッシブ補正やAimStyle排他などすべて含む）
            def = DEF();
        }
        else if (policy.SimlateEnemyDEF)
        {
            // 基本防御力のみ：b_b_def + 共通TenDay係数（AimStyle排他・パッシブ補正などは含めない）
            var basic = new StatesPowerBreakdown(new TenDayAbilityDictionary(), b_b_def);
            foreach (var kv in DefensePowerConfig.CommonDEF)
            {
                float td = TenDayValues(false).GetValueOrZero(kv.Key);
                if (td != 0f && kv.Value != 0f)
                {
                    basic.TenDayAdd(kv.Key, td * kv.Value);
                }
            }
            def = basic;
        }
        else
        {
            // DEFを考慮しない
            def = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);
        }


        //精神攻撃ブーストはシミュレートでは考慮しない
        var mentalATKBoost = 1.0f;

        //ダメージを計算
        StatesPowerBreakdown dmg, mentalDmg;
        if(skill.IsMagic)//魔法スキルのダメージ計算
        {
            dmg = MagicDamageCalculation(attacker, skillPower, def);
            mentalDmg = MagicMentalDamageCalculation(attacker, mentalATKBoost, skillPowerForMental);
        }
        else//それ以外のスキルのダメージ計算
        {
            dmg = NonMagicDamageCalculation(attacker, skillPower, def);
            mentalDmg = NonMagicMentalDamageCalculation(attacker, mentalATKBoost, skillPowerForMental);
        }

        //物理耐性による減衰(オプション)
        if(policy.physicalResistance)
        {
            dmg = ApplyPhysicalResistance(dmg,skill);
        }

        //追加HP（バリア層）のシミュレート処理（オプション）
        if(policy.SimlateVitalLayerPenetration)
        {
            SimulateBarrierLayers(ref dmg, ref mentalDmg, attacker);
        }

        //シミュレートするダメージの種類で分岐
        if(policy.damageType == SimulateDamageType.dmg)
        {
            return dmg.Total;
        }
        if(policy.damageType == SimulateDamageType.mentalDmg)
        {
            return mentalDmg.Total;
        }
        Debug.LogError("BaseStatesのダメージシミュレート関数に渡されたダメージタイプが正しくありません");
        return 0;
    }

    /// <summary>
    /// バリア層のシミュレート処理（実際のバリア層を変更せずにダメージ計算のみ行う）
    /// PenetrateLayerのロジックを再現しつつ、実体を変更しない
    /// </summary>
    private void SimulateBarrierLayers(ref StatesPowerBreakdown dmg, ref StatesPowerBreakdown mentalDmg, BaseStates atker)
    {
        // バリア層のシミュレート用データを作成（実体は変更しない）
        var simulateVitalLayers = new List<(float layerHP, float maxLayerHP, BaseVitalLayer originalLayer)>();
        
        // 元のバリア層リストから必要な情報をコピー
        foreach(var layer in _vitalLayerList)
        {
            simulateVitalLayers.Add((layer.LayerHP, layer.MaxLayerHP, layer));
        }

        for (int i = 0; i < simulateVitalLayers.Count;)
        {
            var (layerHP, maxLayerHP, originalLayer) = simulateVitalLayers[i];
            var skillPhy = atker.NowUseSkill.SkillPhysical;
            
            // PenetrateLayerのロジックを再現（実体を変更せずに）
            var (newDmg, newMentalDmg, newLayerHP, isDestroyed) = SimulatePenetrateLayer(
                dmg, mentalDmg, layerHP, originalLayer, skillPhy);
            
            dmg = newDmg;
            mentalDmg = newMentalDmg;

            if (isDestroyed)
            {
                // このレイヤーは破壊された
                simulateVitalLayers.RemoveAt(i);
                // リストを削除したので、 i はインクリメントしない
                
                //破壊慣れまたは破壊負け
                var kerekere = atker.TenDayValues(true).GetValueOrZero(TenDayAbility.KereKere);
                if (skillPhy == PhysicalProperty.heavy)//暴断なら破壊慣れ
                {
                    dmg += dmg * 0.015f * kerekere;
                }
                if (skillPhy == PhysicalProperty.volten)//vol天なら破壊負け
                {
                    dmg -= dmg * 0.022f * (atker.b_ATK.Total - kerekere);
                    //b_atk < kerekereになった際、減らずに逆に威力が増えるので、そういう場合の演出差分が必要
                }
            }
            else
            {
                // レイヤーが残ったら i を進める
                simulateVitalLayers[i] = (newLayerHP, maxLayerHP, originalLayer);
                i++;
            }

            // dmg が 0 以下になったら、もうこれ以上削る必要ない
            if (dmg.Total <= 0f)
            {
                break;
            }
        }
    }

    /// <summary>
    /// PenetrateLayerのロジックをシミュレート（実体を変更しない）
    /// </summary>
    /// <returns>(新しいdmg, 新しいmentalDmg, 新しいlayerHP, 破壊されたかどうか)</returns>
    private (StatesPowerBreakdown dmg, StatesPowerBreakdown mentalDmg, float layerHP, bool isDestroyed) 
        SimulatePenetrateLayer(StatesPowerBreakdown dmg, StatesPowerBreakdown mentalDmg, float layerHP, 
                              BaseVitalLayer originalLayer, PhysicalProperty impactProperty)
    {
        // 1) 物理属性に応じた耐性率を取得
        float resistRate = 1.0f;
        switch (impactProperty)
        {
            case PhysicalProperty.heavy:
                resistRate = originalLayer.HeavyResistance;
                break;
            case PhysicalProperty.volten:
                resistRate = originalLayer.voltenResistance;
                break;
            case PhysicalProperty.dishSmack:
                resistRate = originalLayer.DishSmackRsistance;
                break;
        }

        // 2) 軽減後の実ダメージ
        StatesPowerBreakdown dmgAfter = dmg * resistRate;

        // 3) レイヤーHPを削る（シミュレート）
        StatesPowerBreakdown leftover = layerHP - dmgAfter; // leftover "HP" => もしマイナスなら破壊

        //精神dmgが現存する攻撃に削られる前のLayerを通る
        mentalDmg -= layerHP * (1 - originalLayer.MentalPenetrateRatio);//精神HPの通過率の分だけ通るので、つまり100%ならmentalDMgの低減はないということ。

        if (leftover <= 0f)
        {
            // 破壊された
            StatesPowerBreakdown overkill = -leftover; // -negative => positive
            var tmpHP = layerHP;//仕組みC用に今回受ける時のLayerHPを保存。
            
            // 仕組みの違い
            switch (originalLayer.ResistMode)
            {
                case BarrierResistanceMode.A_SimpleNoReturn:
                    // Aは一度軽減した分は戻さない: overkill をそのまま次へ
                    return (overkill, mentalDmg, 0f, true);

                case BarrierResistanceMode.B_RestoreWhenBreak:
                    // Bは「軽減後ダメージ」分を元に戻す => leftover を "÷ resistRate" で拡大
                    StatesPowerBreakdown restored = overkill / resistRate;
                    return (restored, mentalDmg, 0f, true);

                case BarrierResistanceMode.C_IgnoreWhenBreak:
                    // Cは元攻撃 - 現在のLayerHP
                    StatesPowerBreakdown cValue = dmg - tmpHP;
                    return (cValue, mentalDmg, 0f, true);
                    
                case BarrierResistanceMode.C_IgnoreWhenBreak_MaxHP:
                    // Cは元攻撃 - 最大LayerHP
                    StatesPowerBreakdown cmValue = dmg - originalLayer.MaxLayerHP;
                    return (cmValue, mentalDmg, 0f, true);
            }
        }
        else
        {
            // バリアで耐えた（破壊されなかった）
            float newLayerHP = leftover.Total;//レイヤーHPに戻すのでtotal
            StatesPowerBreakdown zeroDmg = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0f); // 余剰ダメージなし
            return (zeroDmg, mentalDmg, newLayerHP, false);
        }
        
        // デフォルト（通常ここには来ない）
        return (dmg, mentalDmg, layerHP, false);
    }
    

}
