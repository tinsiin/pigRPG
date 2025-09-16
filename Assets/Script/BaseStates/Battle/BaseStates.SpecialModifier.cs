using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;


//特別補正　BattleManagerでターン一回分を用いられる
public abstract partial class BaseStates    
{
    /// <summary>
    /// 特別補正用保持リスト
    /// </summary>
    private List<ModifierPart> _specialModifiers = new();
    /// <summary>
    /// 特別補正などをすべて消す
    /// </summary>
    public void RemoveUseThings()
    {
        _exCounterDEFATK = -1;
        _specialModifiers = new List<ModifierPart>();
        _charaConditionalMods = new List<CharacterConditionalModifier>();
    }

    /// <summary>
    /// 特別補正をセットする。
    /// オプション変数部分が固定値
    /// </summary>
    public void SetSpecialModifier(string memo,whatModify whatstate,float value = 1, StatesPowerBreakdown fixedModifier = null, bool isFixed = false)
    {
        if (_specialModifiers == null) _specialModifiers = new List<ModifierPart>();//nullチェック、処理
        _specialModifiers.Add(new ModifierPart(memo, whatstate, value, fixedModifier, isFixed));
    }
    /// <summary>
    /// 既にある特別補正をコピーする。
    /// </summary>
    public void CopySpecialModifier(ModifierPart mod)
    {
        if (_specialModifiers == null) _specialModifiers = new List<ModifierPart>();//nullチェック、処理
        _specialModifiers.Add(mod);
    }
    /// <summary>
    /// 特別な補正を利用  パーセンテージ補正用  戦闘の状況で要所要所で傾くイメージなので平均化　
    /// パッシブの乗算補正の積とブレンドとは違う(CalculateBlendedPercentageModifier)
    /// </summary>
    public float GetSpecialPercentModifier(whatModify mod)
    {
        return _specialModifiers.Where(m => m.IsFixedOrPercent == false && m.whatStates == mod)
            .Aggregate(1.0f, (total, m) => total * m.Modifier);//指定したステータスとパーセンテージ補正のリスト内全ての値を乗算
    }
    /// <summary>
    /// 特別な補正を利用  固定値補正用
    /// </summary>
    public StatesPowerBreakdown GetSpecialFixedModifier(whatModify mod)
    {
        var calculateList =_specialModifiers.Where(m => m.IsFixedOrPercent == true && m.whatStates == mod).ToList();
        return CalculateFixedModifierTotal(calculateList);
            
    }
    /// <summary>
    /// 特別補正の内訳リストを集約した一つの内訳にして返す処理
    /// 特別補正リストの固定値集約用
    /// </summary>
    private StatesPowerBreakdown CalculateFixedModifierTotal(List<ModifierPart> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
        {
            return new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);
        }
        
        StatesPowerBreakdown result = new StatesPowerBreakdown(new TenDayAbilityDictionary(), 0);
        
        for (int i = 0; i < modifiers.Count; i++)
        {
            result = result + modifiers[i].FixedModifier;
        }
        
        return result;
    }

    /// <summary>
    /// 特別な補正の保持リストをただ返す。　主にフレーバー要素用。_conditionalMods;
    /// </summary>
    public List<ModifierPart> UseSpecialModifiers
    {
        get => _specialModifiers;
    }

    /// <summary>
    /// カウンター用の一時的な防御無視率 )特別補正_
    /// 比較する際にこちらの方が本来の無視率より多ければ、こちらの値が使用される。
    /// -1は使用されていない。というか直接比較されるから-以下の数字にしとけば絶対参照されない。
    /// </summary>
    float _exCounterDEFATK =-1;
    /// <summary>
    /// カウンター用防御無視率をセット
    /// </summary>
    public void SetExCounterDEFATK(float value)
    {
        _exCounterDEFATK = value;
    }

    //  ==============================================================================================================================
    //                                              キャラ限定補正
    //
    //
    //  ==============================================================================================================================
    public List<CharacterConditionalModifier> _charaConditionalMods;
    /// <summary>
    /// キャラ限定補正を追加
    /// </summary>
    public void SetCharaConditionalModifierList(BaseStates target, string memo, 
    whatModify whatstate,float value, StatesPowerBreakdown fixedModifier = null, bool isFixed = false)
    {
        if (_charaConditionalMods == null)
            _charaConditionalMods = new List<CharacterConditionalModifier>();

        // ModifierPart を生成して CharacterConditionalModifier に渡す
        var part = new ModifierPart(memo, whatstate, value, fixedModifier, isFixed);
        _charaConditionalMods.Add(new CharacterConditionalModifier(target, part));
    }
   /// <summary>
    /// キャラ限定補正を、その敵と一致しているものだけ通常の特別補正リストに追加する
    /// </summary>
    public void ApplyCharaConditionalToSpecial(BaseStates target)
    {
        if (_charaConditionalMods == null || _charaConditionalMods.Count < 1) return;
        foreach (var cond in _charaConditionalMods.Where(x => x.Target == target))
        {
            // SetSpecialModifier(memo, whatstate, value, fixedModifier, isFixed)
            SetSpecialModifier(
                cond.Part.memo,
                cond.Part.whatStates,
                cond.Part.Modifier,
                cond.Part.FixedModifier,
                cond.Part.IsFixedOrPercent
            );
        }
    }
    
}
/// <summary>
/// あるキャラクターにのみ効く一時補正
/// </summary>
public class CharacterConditionalModifier
{
    public BaseStates Target { get; }
    public ModifierPart Part  { get; }

    public CharacterConditionalModifier(BaseStates target, ModifierPart part)
    {
        Target = target;
        Part   = part;
    }
}
/// <summary>
/// 命中率、攻撃力、回避力、防御力への補正
/// </summary>
public class ModifierPart
{
    /// <summary>
    /// どういう補正かを保存する　攻撃時にunderに出てくる
    /// </summary>
    public string memo;

    public whatModify whatStates;

    /// <summary>
    /// trueならfixed、falseならpercent
    /// </summary>
    public bool IsFixedOrPercent;

    /// <summary>
    /// 補正率 
    /// </summary>
    public float Modifier;

    /// <summary>
    /// 固定値補正値　十日能力の内訳を含む
    /// </summary>
    public StatesPowerBreakdown FixedModifier;


    public ModifierPart(string memo, whatModify whatStates, float value, StatesPowerBreakdown fixedModifier = null, bool isFixedOrPercent = false)
    {
        this.memo = memo;
        Modifier = value;
        this.whatStates = whatStates;
        IsFixedOrPercent = isFixedOrPercent;
        FixedModifier = fixedModifier;
    }
}
