using System.Collections.Generic;
using Effects.Integration;
using UnityEngine;

/// <summary>
/// イラつきシステムの全ロジックを集約する静的サービス。
/// データは各BaseStatesのフィールド（IrritationCounters等）に保持される。
/// 補正テーブル（構想書セクション8）はstatic辞書で保持。
/// </summary>
public static class IrritationService
{
    // ─── 8.1 精神属性別 減衰量テーブル（3ターンごとに適用、デフォルト1） ───
    // 配列で順序保証（複合精神属性のビットフラグマッチで先頭優先）
    private static readonly (SpiritualProperty flag, int value)[] DecayTable =
    {
        (SpiritualProperty.Doremis,          1),
        (SpiritualProperty.Pillar,           3),
        (SpiritualProperty.Kindergarten,     6),
        (SpiritualProperty.LiminalWhiteTile, 1),
        (SpiritualProperty.Sacrifaith,       1),
        (SpiritualProperty.Cquiest,          2),
        (SpiritualProperty.Psycho,           1),
        (SpiritualProperty.GodTier,          1),
        (SpiritualProperty.BaleDrival,       0),
        (SpiritualProperty.Devil,            1),
    };

    // ─── 8.2 精神属性別 付与量補正テーブル（百分率、デフォルト100%） ───
    // 配列で順序保証（複合精神属性のビットフラグマッチで先頭優先）
    private static readonly (SpiritualProperty flag, float value)[] ApplicationTable =
    {
        (SpiritualProperty.Doremis,           0.90f),
        (SpiritualProperty.Pillar,            0.30f),
        (SpiritualProperty.Kindergarten,      1.20f),
        (SpiritualProperty.LiminalWhiteTile,  1.00f),
        (SpiritualProperty.Sacrifaith,        1.10f),
        (SpiritualProperty.Cquiest,           1.00f),
        (SpiritualProperty.Psycho,            1.00f),
        (SpiritualProperty.GodTier,           0.90f),
        (SpiritualProperty.BaleDrival,        1.00f),
        (SpiritualProperty.Devil,             1.09f),
    };

    // ─── 8.3 Demeanor別 発動確率係数テーブル（デフォルト6%） ───
    private static readonly Dictionary<Demeanor, float> TriggerCoefficientTable = new()
    {
        { Demeanor.Painful,    0.02f },
        { Demeanor.Optimistic, 0.06f },
        { Demeanor.Elated,     0.03f },
        { Demeanor.Resolved,   0.06f },
        { Demeanor.Angry,      0.06f },
        { Demeanor.Doubtful,   0.07f },
        { Demeanor.Confused,   0.06f },
        { Demeanor.Normal,     0.06f },
    };

    // ─── 3.3 暴走火力倍率テーブル（超過量別） ───
    private static readonly (int maxExcess, float multiplier)[] RageMultiplierTable =
    {
        (0, 1.25f),   // ちょうど閾値
        (2, 1.40f),   // 超過1〜2
        (4, 1.55f),   // 超過3〜4
    };
    private const float RageMultiplierMax = 1.75f; // 超過5以上

    // =================================================================
    //  付与
    // =================================================================

    /// <summary>
    /// targetにsource由来のイラつきを付与する。
    /// Machine種別チェック・精神属性補正（8.2）を内部で適用。
    /// </summary>
    public static void Add(BaseStates target, BaseStates source, int rawAmount)
    {
        if (target == null || source == null || rawAmount <= 0) return;
        if (target == source) return; // 自分自身にはイラつかない

        // Machine種別は付与ブロック
        if ((target.MyType & CharacterType.Machine) != 0) return;

        // 辞書未初期化ガード
        if (target.IrritationCounters == null) return;

        // 精神属性別 付与量補正
        float modifier = GetApplicationModifier(target.MyImpression);
        int finalAmount = Mathf.Max(1, Mathf.RoundToInt(rawAmount * modifier));

        // やり場のないイラつきの再挑発解消（条件②: 同じ対象から再挑発→全加算）
        if (target.UnresolvedIrritation != null && target.UnresolvedIrritation.TryGetValue(source, out int unresolvedAmount))
        {
            finalAmount += unresolvedAmount;
            target.UnresolvedIrritation.Remove(source);
        }

        // 加算
        if (target.IrritationCounters.ContainsKey(source))
            target.IrritationCounters[source] += finalAmount;
        else
            target.IrritationCounters[source] = finalAmount;

        UpdateEffects(target);
    }

    // =================================================================
    //  減衰
    // =================================================================

    /// <summary>
    /// charaの全イラつきカウンターに精神属性別の減衰を適用する。
    /// 3ターンごとの判定タイミングで呼ぶ。
    /// </summary>
    public static void Decay(BaseStates chara)
    {
        if (chara?.IrritationCounters == null || chara.IrritationCounters.Count == 0) return;

        int decayAmount = GetDecayAmount(chara.MyImpression);
        if (decayAmount <= 0) return;

        // Keys列挙中に変更するためリスト化（foreachで統一）
        var keys = new List<BaseStates>(chara.IrritationCounters.Keys);
        for (int i = keys.Count - 1; i >= 0; i--)
        {
            var key = keys[i];
            chara.IrritationCounters[key] -= decayAmount;
            if (chara.IrritationCounters[key] <= 0)
                chara.IrritationCounters.Remove(key);
        }

        UpdateEffects(chara);
    }

    // =================================================================
    //  発動確率判定
    // =================================================================

    /// <summary>
    /// charaのイラつき攻撃の発動確率判定を行う。
    /// 戻り値: (ヒットしたか, 強制ターゲット)。ミス時はtargetがnull。
    /// </summary>
    public static (bool hit, BaseStates forcedTarget) JudgeTrigger(BaseStates chara)
    {
        if (chara?.IrritationCounters == null || chara.IrritationCounters.Count == 0)
            return (false, null);

        // 生存対象の最大値と合計をforeachで一括計算（アロケーション回避）
        BaseStates maxTarget = null;
        int maxCount = 0;
        int totalAlive = 0;

        foreach (var kv in chara.IrritationCounters)
        {
            if (kv.Key == null || kv.Key.Death() || kv.Value <= 0) continue;
            totalAlive += kv.Value;
            if (kv.Value > maxCount)
            {
                maxCount = kv.Value;
                maxTarget = kv.Key;
            }
        }

        if (maxTarget == null)
            return (false, null);

        // 残りの合計
        int rest = totalAlive - maxCount;

        // 発動値 = 最大相手のカウント + (合計 - 最大相手のカウント) / 4
        float triggerValue = maxCount + rest / 4f;

        // Demeanor別係数
        float coefficient = GetTriggerCoefficient(chara.CurrentDemeanor);

        // 発動確率
        float probability = triggerValue * coefficient;

        // 乱数判定
        var random = BattleContextHub.IsInBattle ? BattleContextHub.Current?.Random : null;
        float roll = random != null ? random.NextFloat() : Random.Range(0f, 1f);

        if (roll < probability)
            return (true, maxTarget);

        return (false, null);
    }

    // =================================================================
    //  暴走判定
    // =================================================================

    /// <summary>
    /// charaが暴走中（合計イラつき ≧ 暴走閾値）かどうか。
    /// </summary>
    public static bool IsRaging(BaseStates chara)
    {
        return GetTotalIrritation(chara) >= chara.RageThreshold;
    }

    /// <summary>
    /// 暴走時の火力倍率を返す。暴走していなければ1.0。
    /// </summary>
    public static float GetRageMultiplier(BaseStates chara)
    {
        int total = GetTotalIrritation(chara);
        int excess = total - chara.RageThreshold;
        if (excess < 0) return 1.0f;

        foreach (var (maxExcess, multiplier) in RageMultiplierTable)
        {
            if (excess <= maxExcess)
                return multiplier;
        }
        return RageMultiplierMax;
    }

    // =================================================================
    //  死亡時処理
    // =================================================================

    /// <summary>
    /// キャラクター死亡時に全キャラのイラつきから死者を横断的に処理する。
    /// やり場のないイラつきへの変換条件も判定。
    /// </summary>
    public static void OnCharacterDeath(BaseStates deadChara, BaseStates killer, bool wasPassiveKill)
    {
        if (deadChara == null) return;

        var context = BattleContextHub.IsInBattle ? BattleContextHub.Current : null;
        if (context == null) return;

        foreach (var chara in context.AllCharacters)
        {
            if (chara == null || chara == deadChara) continue;
            if (chara.IrritationCounters == null) continue;

            if (chara.IrritationCounters.TryGetValue(deadChara, out int amount))
            {
                // やり場のないイラつきへの変換条件判定
                // 条件: 対象単体のイラつき ≧ 暴走閾値 AND B自身が倒していない
                bool selfKilled = (killer == chara);
                bool exceedsThreshold = amount >= chara.RageThreshold;

                if (exceedsThreshold && !selfKilled)
                {
                    // やり場のないイラつきに変換
                    if (chara.UnresolvedIrritation == null) continue; // 未初期化ガード

                    if (chara.UnresolvedIrritation.ContainsKey(deadChara))
                        chara.UnresolvedIrritation[deadChara] += amount;
                    else
                        chara.UnresolvedIrritation[deadChara] = amount;
                }

                // 通常イラつきから削除
                chara.IrritationCounters.Remove(deadChara);
            }
        }

        // 死者自身のイラつきもクリア
        deadChara.IrritationCounters?.Clear();

        // 死者自身のエフェクトを確実に停止（AllCharactersから既に除外されている場合に備える）
        UpdateEffects(deadChara);

        // 全キャラのエフェクトを更新（死亡により複数キャラの暴走状態が変わりうる）
        UpdateAllEffects();
    }

    // =================================================================
    //  やり場のないイラつき解消
    // =================================================================

    /// <summary>
    /// 攻撃ダメージによるやり場のないイラつきの解消判定。
    /// B自身の攻撃（パッシブ対象外）で元の対象にmaxHPの3%以上のダメージを与えたら全消滅。
    /// </summary>
    public static void TryResolveUnresolved(BaseStates attacker, BaseStates target, float damageAmount)
    {
        if (attacker?.UnresolvedIrritation == null || target == null) return;
        if (!attacker.UnresolvedIrritation.ContainsKey(target)) return;

        float threshold = target.MaxHP * 0.03f;
        if (damageAmount >= threshold)
        {
            attacker.UnresolvedIrritation.Remove(target);
            UpdateEffects(attacker);
        }
    }

    // =================================================================
    //  ユーティリティ
    // =================================================================

    /// <summary>最大イラつき対象を返す。該当なしならnull。</summary>
    public static BaseStates GetMaxIrritationTarget(BaseStates chara)
    {
        if (chara?.IrritationCounters == null || chara.IrritationCounters.Count == 0)
            return null;

        BaseStates maxTarget = null;
        int maxCount = 0;
        foreach (var kv in chara.IrritationCounters)
        {
            if (kv.Key != null && !kv.Key.Death() && kv.Value > maxCount)
            {
                maxCount = kv.Value;
                maxTarget = kv.Key;
            }
        }
        return maxTarget;
    }

    /// <summary>合計イラつき（やり場のないイラつき含む）を返す。</summary>
    public static int GetTotalIrritation(BaseStates chara)
    {
        if (chara == null) return 0;

        int total = 0;
        if (chara.IrritationCounters != null)
        {
            foreach (var kv in chara.IrritationCounters)
                total += kv.Value;
        }
        if (chara.UnresolvedIrritation != null)
        {
            foreach (var kv in chara.UnresolvedIrritation)
                total += kv.Value;
        }
        return total;
    }

    /// <summary>charaがイラつきを持っているか。</summary>
    public static bool HasAnyIrritation(BaseStates chara)
    {
        return chara?.IrritationCounters != null && chara.IrritationCounters.Count > 0;
    }

    // =================================================================
    //  エフェクト制御（6a）
    // =================================================================

    private const string IrritationAuraEffect = "irritation_aura";
    private const string RageAuraEffect = "rage_aura";

    /// <summary>
    /// キャラのイラつきエフェクト状態を現在のイラつき状況に合わせて更新する。
    /// イラつき変動後（Add/Decay/OnCharacterDeath）に呼ぶ。
    /// </summary>
    public static void UpdateEffects(BaseStates chara)
    {
        if (chara?.BattleIcon == null) return;
        var icon = chara.BattleIcon;

        bool hasIrritation = HasAnyIrritation(chara);
        bool isRaging = hasIrritation && IsRaging(chara);

        if (hasIrritation)
            EffectManager.Play(IrritationAuraEffect, icon, loop: true);
        else
            EffectManager.Stop(icon, IrritationAuraEffect);

        if (isRaging)
            EffectManager.Play(RageAuraEffect, icon, loop: true);
        else
            EffectManager.Stop(icon, RageAuraEffect);
    }

    /// <summary>
    /// 全キャラのイラつきエフェクトを更新する（死亡時など複数キャラに影響がある場合）。
    /// </summary>
    public static void UpdateAllEffects()
    {
        var context = BattleContextHub.IsInBattle ? BattleContextHub.Current : null;
        if (context == null) return;

        foreach (var chara in context.AllCharacters)
        {
            if (chara != null)
                UpdateEffects(chara);
        }
    }

    // =================================================================
    //  テーブル参照（内部用）
    // =================================================================

    /// <summary>精神属性に応じた減衰量を返す（UI表示用に公開）。</summary>
    public static int GetDecayAmount(SpiritualProperty impression)
    {
        // 配列順で先頭マッチ（複合精神属性でも決定的）
        foreach (var (flag, value) in DecayTable)
        {
            if ((impression & flag) != 0)
                return value;
        }
        return 1; // デフォルト
    }

    private static float GetApplicationModifier(SpiritualProperty impression)
    {
        foreach (var (flag, value) in ApplicationTable)
        {
            if ((impression & flag) != 0)
                return value;
        }
        return 1.0f; // デフォルト
    }

    private static float GetTriggerCoefficient(Demeanor condition)
    {
        if (TriggerCoefficientTable.TryGetValue(condition, out float coeff))
            return coeff;
        return 0.06f; // デフォルト
    }
}
