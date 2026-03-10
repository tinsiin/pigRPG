using UnityEngine;

/// <summary>
/// 乖離スキル精神消耗に関する定数・計算メソッドを一元管理する設定。
/// 乖離したスキルを戦闘中に連発すると精神HPが加速的に消耗し、
/// ダウナー乖離を自然に誘発するメカニクス。
/// 詳細: doc/スキル/乖離スキル精神消耗仕様書.md
/// </summary>
public static class MentalExhaustionConfig
{
    // ── 乖離判定ゲート（苦悩システムと共通） ──

    /// <summary> 乖離とみなす最低距離（主効果のゲート） </summary>
    public const float DivergenceThreshold = 8f;

    /// <summary> 副次効果（精神DEF低下）が発生する乖離度のゲート </summary>
    public const float HighDivergenceThreshold = 12f;

    // ── 主効果: 精神HP消耗 ──

    /// <summary>
    /// 基礎消耗のスケール係数。
    /// mastery=0, N=1 で精神MaxHPの4%を消耗する。
    /// </summary>
    public const float BaseDrainCoefficient = 0.04f;

    /// <summary>
    /// 主効果の使いこなし度しきい値K。
    /// mastery が K 以上で基礎消耗が0になる。
    /// K=2.0 なので、masteryが2.0（＝スキル要求の倍）でようやく完全無消耗。
    /// </summary>
    public const float MasteryThresholdK = 2.0f;

    // ── 副次効果: 精神DEF低下 ──

    /// <summary>
    /// 副次効果の使いこなし度しきい値K_def。
    /// 主効果より高い = 副次効果の方が0にしづらい。
    /// </summary>
    public const float MasteryThresholdKDef = 3.0f;

    /// <summary> 蓄積値がこれ以上で精神DEF×0.5 </summary>
    public const float DefReductionStage1Threshold = 3.0f;

    /// <summary> 蓄積値がこれ以上で精神DEF×0.25 </summary>
    public const float DefReductionStage2Threshold = 6.0f;

    // ── 計算メソッド ──

    /// <summary>
    /// 主効果の基礎消耗率を計算する。
    /// max(0, 1 - mastery/K) × BaseDrainCoefficient
    /// </summary>
    /// <param name="mastery">使いこなし度 (0～∞)</param>
    /// <returns>精神MaxHPに対する消耗率（N=1時）</returns>
    public static float CalcBaseDrain(float mastery)
    {
        return Mathf.Max(0f, 1f - mastery / MasteryThresholdK) * BaseDrainCoefficient;
    }

    /// <summary>
    /// 加速関数 f(N)。線形。
    /// N回目の乖離スキル使用でN倍の消耗。
    /// </summary>
    /// <param name="n">乖離スキル連発カウント</param>
    public static float AccelerationF(int n)
    {
        return n;
    }

    /// <summary>
    /// 副次効果の蓄積単位を計算する。
    /// max(0, 1 - mastery/K_def)
    /// </summary>
    /// <param name="mastery">使いこなし度 (0～∞)</param>
    public static float CalcDefAccumulationUnit(float mastery)
    {
        return Mathf.Max(0f, 1f - mastery / MasteryThresholdKDef);
    }

    /// <summary>
    /// 蓄積値から精神DEF倍率を返す。
    /// 段階的に低下: 1.0 → 0.5 → 0.25
    /// </summary>
    /// <param name="accumulation">副次効果の蓄積値</param>
    public static float GetDefMultiplier(float accumulation)
    {
        if (accumulation >= DefReductionStage2Threshold) return 0.25f;
        if (accumulation >= DefReductionStage1Threshold) return 0.5f;
        return 1.0f;
    }
}
