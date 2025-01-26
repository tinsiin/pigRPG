using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 十日能力の種類
/// </summary>
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
        };

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
    /// 十日能力の値を取得。存在しない場合は0を返す
    /// </summary>
    public static float GetValueOrZero(this SerializableDictionary<TenDayAbility, float> dict,
    TenDayAbility ability)
    {
        return dict.TryGetValue(ability, out float value) ? value : 0f;
    }
}

/// <summary>

