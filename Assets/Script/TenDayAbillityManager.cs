using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 十日能力の種類
/// </summary>
[Serializable]
public enum TenDayAbility
{
    /// <summary>テント空洞</summary>
    TentVoid,
    /// <summary>夜暗黒</summary>
    NightDarkness,
    /// <summary>泉水</summary>
    SpringWater,
    /// <summary>星テルシ</summary>
    StarTersi,
    /// <summary>レインコート</summary>
    Raincoat,
    /// <summary>水雷神経</summary>
    WaterThunderNerve,
    /// <summary>冷酷冷静</summary>
    ColdHeartedCalm,
    /// <summary>nightinknight</summary>
    NightInkKnight,
    /// <summary>春仮眠</summary>
    SpringNap,
    /// <summary>ミザ</summary>
    Miza,
    /// <summary>馬鹿</summary>
    Baka,
    /// <summary>スマイラー</summary>
    Smiler,
    /// <summary>顔から手</summary>
    FaceToHand,
    /// <summary>未消光の道</summary>
    UnextinguishedPath,
    /// <summary>ソート</summary>
    Sort,
    /// <summary>陽炎</summary>
    HeatHaze,
    /// <summary>ベイル</summary>
    Vail,
    /// <summary>烈火</summary>
    BlazingFire,
    /// <summary>燃え火吹きウィフ</summary>
    FlameBreathingWife,
    /// <summary>威光</summary>
    Glory,
    /// <summary>雨</summary>
    Rain,
    /// <summary>余裕</summary>
    Leisure,
    /// <summary>元素信仰力</summary>
    ElementFaithPower,
    /// <summary>クリオネ質</summary>
    CryoniteQuality,
    /// <summary>ドクマムシ</summary>
    dokumamusi,
    /// <summary>ジョー歯</summary>
    JoeTeeth,
    /// <summary>ピルマグレイトフル</summary>
    Pilmagreatifull,
    /// <summary>刃物</summary>
    Blades,
    /// <summary>ペルソナ乖離</summary>
    PersonaDivergence,
    /// <summary>サイレント練度</summary>
    SilentTraining,
    /// <summary>人殺し人</summary>
    HumanKiller,
    /// <summary>盥豚</summary>
    Taraiton,
    /// <summary>天と終戦</summary>
    HeavenAndEndWar,
    /// <summary>エノクナギ</summary>
    Enokunagi,
    /// <summary>ヴォンド</summary>
    Vond,
    /// <summary>ケレケレ</summary>
    KereKere,
    /// <summary>ラッキー</summary>
    Lucky


}

/// <summary>
/// 十日能力の座標管理
/// </summary>
public static class TenDayAbilityPosition
{
    /// <summary>
    /// 十日能力ごとの座標データ
    /// </summary>
    private static readonly Dictionary<TenDayAbility, Vector2> positions =
        new Dictionary<TenDayAbility, Vector2>
        {
        { TenDayAbility.TentVoid,            new Vector2(7,  0) },
        { TenDayAbility.NightDarkness,       new Vector2(6,  1) },
        { TenDayAbility.SpringWater,         new Vector2(10, 1) },
        { TenDayAbility.StarTersi,           new Vector2(5,  2) },
        { TenDayAbility.Raincoat,            new Vector2(6,  3) },
        { TenDayAbility.WaterThunderNerve,   new Vector2(7,  3) },
        { TenDayAbility.ColdHeartedCalm,     new Vector2(9,  3) },
        { TenDayAbility.NightInkKnight,      new Vector2(10, 3) },
        { TenDayAbility.SpringNap,           new Vector2(5,  4) },
        { TenDayAbility.Miza,                new Vector2(6,  4) },
        { TenDayAbility.Baka,                new Vector2(8,  4) },
        { TenDayAbility.Smiler,              new Vector2(10, 4) },
        { TenDayAbility.FaceToHand,          new Vector2(11, 4) },
        { TenDayAbility.UnextinguishedPath,  new Vector2(14, 4) },
        { TenDayAbility.Sort,                new Vector2(3,  5) },
        { TenDayAbility.HeatHaze,            new Vector2(6,  5) },
        { TenDayAbility.Vail,                new Vector2(7,  5) },
        { TenDayAbility.BlazingFire,         new Vector2(8,  5) },
        { TenDayAbility.FlameBreathingWife,  new Vector2(9,  5) },
        { TenDayAbility.Glory,               new Vector2(10, 5) },
        { TenDayAbility.Rain,                new Vector2(13, 5) },
        { TenDayAbility.Leisure,             new Vector2(5,  6) },
        { TenDayAbility.ElementFaithPower,   new Vector2(11, 6) },
        { TenDayAbility.CryoniteQuality,     new Vector2(13, 6) },
        { TenDayAbility.dokumamusi,          new Vector2(4,  7) },
        { TenDayAbility.JoeTeeth,            new Vector2(7,  7) },
        { TenDayAbility.Pilmagreatifull,     new Vector2(8,  7) },
        { TenDayAbility.Blades,              new Vector2(9,  7) },
        { TenDayAbility.PersonaDivergence,   new Vector2(5,  8) },
        { TenDayAbility.SilentTraining,      new Vector2(9,  8) },
        { TenDayAbility.HumanKiller,         new Vector2(12, 8) },
        { TenDayAbility.Taraiton,            new Vector2(7,  10) },
        { TenDayAbility.HeavenAndEndWar,     new Vector2(11, 11) },
        { TenDayAbility.Enokunagi,           new Vector2(1,  12) },
        { TenDayAbility.Vond,                new Vector2(6,  12) }
        };//距離は最大15辺り

    /// <summary>
    /// 二つの十日能力間の距離を計算
    /// </summary>
    /// <param name="a">十日能力A</param>
    /// <param name="b">十日能力B</param>
    /// <returns>二点間の距離</returns>
    public static float GetDistance(TenDayAbility a, TenDayAbility b)
    {
        return Vector2.Distance(positions[a], positions[b]);
    }
    /// <summary>
    /// 十日能力のスキルと精神属性の距離による成長値の算出用
    /// 距離 d をもとに、単純な線形減衰係数(1 → 0)を返す。
    /// maxDistanceで閾値を決めて、「ある一定以上の距離が離れてると伸びない」
    /// </summary>
    /// <param name="distance">ユークリッド距離などで計算された値</param>
    /// <param name="maxDistance">この距離に達したら係数が0になる上限</param>
    /// <returns>減衰係数(0～1)</returns>
    public static float GetLinearAttenuation(float distance, float maxDistance)
    {
        if (distance >= maxDistance)
        {
            // 距離が maxDistance を超えたら 0
            return 0f;
        }
        else
        {
            // それ以外は 1 - (distance / maxDistance)
            return 1f - (distance / maxDistance);
        }
    }

    /// <summary>
    /// 十日能力の値を取得。存在しない場合は0を返す
    /// </summary>
    public static float GetValueOrZero(this SerializableDictionary<TenDayAbility, float> dict,
    TenDayAbility ability)
    {
        return dict.TryGetValue(ability, out float value) ? value : 0f;
    }

    /// <summary>
    /// 十日能力の値を取得。存在しない場合は0を返す(通常辞書用)
    /// </summary>
    public static float GetValueOrZero(this Dictionary<TenDayAbility, float> dict,
    TenDayAbility ability)
    {
        return dict.TryGetValue(ability, out float value) ? value : 0f;
    }

}
/// <summary>
/// TenDayAbility用の乗算演算子をサポートする辞書
/// </summary>
[Serializable]
public class TenDayAbilityDictionary : SerializableDictionary<TenDayAbility, float>
{
    /// <summary>
    /// 既存のTenDayAbilityDictionaryからコピーを作成するコンストラクタ
    /// </summary>
    public TenDayAbilityDictionary(TenDayAbilityDictionary dictionary) : base()
    {
        // 既存のTenDayAbilityDictionaryの内容をこのインスタンスにコピー
        foreach (var pair in dictionary)
        {
            this[pair.Key] = pair.Value;
        }
    }
    /// <summary>
    /// デフォルトコンストラクタ
    /// </summary>
    public TenDayAbilityDictionary() : base()
    {
        // 空のコンストラクタ
    }

    /// <summary>
    /// Dictionary<TenDayAbility, float>からコピーを作成するコンストラクタ
    /// </summary>
    public TenDayAbilityDictionary(Dictionary<TenDayAbility, float> dictionary) : base()
    {
        // 既存のDictionaryの内容をこのインスタンスにコピー
        foreach (var pair in dictionary)
        {
            this[pair.Key] = pair.Value;
        }
    }


    /// <summary>
    /// 加算演算子 - 2つの辞書の値を加算する
    /// </summary>
    public static TenDayAbilityDictionary operator +(TenDayAbilityDictionary left, TenDayAbilityDictionary right)
    {
        TenDayAbilityDictionary result = new TenDayAbilityDictionary(left);
        
        foreach (var pair in right)
        {
            if (result.ContainsKey(pair.Key))
            {
                result[pair.Key] += pair.Value;
            }
            else
            {
                result[pair.Key] = pair.Value;
            }
        }
        
        return result;
    }

    /// <summary>
    /// 加算演算子 - TenDayAbilityDictionaryにfloat値を加算する
    /// 全ての値にfloat値を加算します
    /// </summary>
    public static TenDayAbilityDictionary operator +(TenDayAbilityDictionary dict, float value)
    {
        // 安全案1: 元の辞書(dict)を列挙し、新しい辞書に書き込む
        TenDayAbilityDictionary result = new TenDayAbilityDictionary();

        foreach (var pair in dict)
        {
            result[pair.Key] = pair.Value + value;
        }

        return result;
    }

    /// <summary>
    /// 加算演算子 - float値にTenDayAbilityDictionaryを加算する（交換法則対応）
    /// </summary>
    public static TenDayAbilityDictionary operator +(float value, TenDayAbilityDictionary dict)
    {
        return dict + value;
    }

    /// <summary>
    /// 乗算演算子 - 辞書の全ての値に乗数を掛ける
    /// </summary>
    public static TenDayAbilityDictionary operator *(TenDayAbilityDictionary dict, float multiplier)
    {
        TenDayAbilityDictionary result = new TenDayAbilityDictionary();
        
        foreach (var pair in dict)
        {
            result[pair.Key] = pair.Value * multiplier;
        }
        
        return result;
    }

    /// <summary>
    /// 乗算演算子（交換法則対応）
    /// </summary>
    public static TenDayAbilityDictionary operator *(float multiplier, TenDayAbilityDictionary dict)
    {
        return dict * multiplier;
    }
    /// <summary>
    /// 減算演算子 - 2つの辞書の値を減算する
    /// </summary>
    public static TenDayAbilityDictionary operator -(TenDayAbilityDictionary left, TenDayAbilityDictionary right)
    {
        TenDayAbilityDictionary result = new TenDayAbilityDictionary(left);
        
        foreach (var pair in right)
        {
            if (result.ContainsKey(pair.Key))
            {
                result[pair.Key] -= pair.Value;
            }
            else
            {
                result[pair.Key] = -pair.Value;
            }

            if(result[pair.Key] < 0)//0クランプ
            {
                result[pair.Key] = 0;
            }
        }
        
        return result;
    }

    //存在するもののみで掛け合わせる平均値の算出==============================================================================================================
    //つまり一つの辞書に存在した十日能力の値は他の辞書に存在してなくても0で平均されず存在する値のみでしか割られない　詳しくはcascadeに聞こう
    

    /// <summary>
    /// 複数のBaseSkillからそれぞれの十日能力の平均値を計算してTenDayAbilityDictionaryを返す
    /// </summary>
    /// <param name="skills">平均を計算するスキルのコレクション</param>
    /// <returns>平均値を持つTenDayAbilityDictionary</returns>
    public static TenDayAbilityDictionary CalculateAverageTenDayValues(IEnumerable<BaseSkill> skills)
    {
        if (skills == null || !skills.Any())
            return new TenDayAbilityDictionary();

        // すべてのスキルの十日能力値を集める
        var allValues = new Dictionary<TenDayAbility, List<float>>();

        foreach (var skill in skills)
        {
            foreach (var pair in skill.TenDayValues())
            {
                if (!allValues.ContainsKey(pair.Key))
                    allValues[pair.Key] = new List<float>();

                allValues[pair.Key].Add(pair.Value);
            }
        }

        // 平均を計算する
        var result = new TenDayAbilityDictionary();
        foreach (var pair in allValues)
        {
            result[pair.Key] = pair.Value.Average();
        }

        return result;
    }

    /// <summary>
    /// 複数のTenDayAbilityDictionaryから各十日能力の平均値を計算する
    /// </summary>
    /// <param name="dictionaries">平均を計算するTenDayAbilityDictionaryのコレクション</param>
    /// <returns>平均値を持つTenDayAbilityDictionary</returns>
    public static TenDayAbilityDictionary CalculateAverageTenDayDictionary(IEnumerable<TenDayAbilityDictionary> dictionaries)
    {
        if (dictionaries == null || !dictionaries.Any())
            return new TenDayAbilityDictionary();

        // すべての辞書から十日能力値を集める
        var allValues = new Dictionary<TenDayAbility, List<float>>();

        foreach (var dict in dictionaries)
        {
            foreach (var pair in dict)
            {
                if (!allValues.ContainsKey(pair.Key))
                    allValues[pair.Key] = new List<float>();

                allValues[pair.Key].Add(pair.Value);
            }
        }

        // 平均を計算する
        var result = new TenDayAbilityDictionary();
        foreach (var pair in allValues)
        {
            result[pair.Key] = pair.Value.Average();
        }

        return result;
    }
    //存在するもののみで掛け合わせる平均値の算出======================================================================終わり＝＝＝＝＝＝＝＝＝＝＝＝========================================
}

/// <summary>
/// 読み取り専用インデクサーを持つTenDayAbilityDictionaryラッパー
/// 読み取り専用の十日能力辞書データに入れる。　代入を禁止する。
/// </summary>
[Serializable]
public class ReadOnlyIndexTenDayAbilityDictionary : TenDayAbilityDictionary
{
    private readonly TenDayAbilityDictionary _source;

    public ReadOnlyIndexTenDayAbilityDictionary(TenDayAbilityDictionary source) : base(source)
    {
        _source = source;
    }

    // インデクサーをオーバーライド - get のみ実装
    public new float this[TenDayAbility key]
    {
        get { return _source[key]; }
        set { throw new InvalidOperationException("TenDayValues()の結果に直接値を代入することはできません。BaseTenDayValuesを使用してください。"); }
    }

    // その他のTenDayAbilityDictionaryのメソッドや演算子は継承されたまま
}