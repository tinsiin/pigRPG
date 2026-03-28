using System.Linq;
using UnityEngine;

/// <summary>
/// スキル吸引の静的サービス。
/// 機械専用: スキルヒット時に対象へ吸引パッシブを付与し、
/// 全スキル系統のターゲットを吸引元に強制する。
/// </summary>
public static class AttractionService
{
    /// <summary>ステータス差の閾値（挑発属性と同じ0.33）</summary>
    private const float StrengthThreshold = 0.33f;

    /// <summary>PassiveManagerに登録された吸引パッシブのID</summary>
    private const int AttractionPassiveID = 26;

    // =================================================================
    //  付与
    // =================================================================

    /// <summary>
    /// targetにsource由来の吸引パッシブを付与する。
    /// ステータス差判定（対Machine無条件）を内部で行い、不成立なら何もしない。
    /// </summary>
    public static void TryApply(BaseStates target, BaseStates source, int durationTurns)
    {
        if (target == null || source == null || durationTurns <= 0) return;
        if (target == source) return;

        // 使用者がMachineでなければ吸引は発動しない（機械専用）
        if ((source.MyType & CharacterType.Machine) == 0) return;

        // ステータス差判定: 対象がMachineなら無条件、それ以外は強さチェック
        if ((target.MyType & CharacterType.Machine) == 0)
        {
            if (!PassesStrengthCheck(source, target)) return;
        }

        // 既存の吸引パッシブを除去（上書き）
        var existing = target.Passives.FirstOrDefault(p => p is AttractionPassive);
        if (existing != null)
            target.RemovePassive(existing);

        // PassiveManagerからテンプレート取得 → DeepCopy → 持続ターン上書き → 付与
        var template = PassiveManager.Instance.GetAtID(AttractionPassiveID);
        if (template == null)
        {
            Debug.LogError($"[Attraction] PassiveManager に ID={AttractionPassiveID} の吸引パッシブが未登録です");
            return;
        }
        var passive = template.DeepCopy();
        passive.DurationTurn = durationTurns;
        passive.DurationTurnCounter = durationTurns;
        target.ApplyPassive(passive, source);
    }

    // =================================================================
    //  クエリ
    // =================================================================

    /// <summary>
    /// キャラに付いている吸引パッシブの吸引元を返す。吸引なしならnull。
    /// </summary>
    public static BaseStates GetAttractor(BaseStates chara)
    {
        if (chara == null) return null;
        var passive = chara.Passives.FirstOrDefault(p => p is AttractionPassive) as AttractionPassive;
        if (passive == null) return null;
        return passive._grantor;
    }

    /// <summary>
    /// キャラが吸引状態かどうか。
    /// </summary>
    public static bool IsAttracted(BaseStates chara)
    {
        return GetAttractor(chara) != null;
    }

    // =================================================================
    //  死亡時処理
    // =================================================================

    /// <summary>
    /// 吸引元が死亡した際に、全キャラから該当する吸引パッシブを除去する。
    /// OnBattleDeathCallBack()から呼ぶ。
    /// </summary>
    public static void OnAttractorDeath(BaseStates deadAttractor)
    {
        if (deadAttractor == null) return;

        var context = BattleContextHub.IsInBattle ? BattleContextHub.Current : null;
        if (context == null) return;

        foreach (var chara in context.AllCharacters)
        {
            if (chara == null || chara == deadAttractor) continue;

            var passive = chara.Passives.FirstOrDefault(p => p is AttractionPassive) as AttractionPassive;
            if (passive != null && passive._grantor == deadAttractor)
            {
                chara.RemovePassive(passive);
            }
        }
    }

    // =================================================================
    //  内部
    // =================================================================

    /// <summary>
    /// ステータス差チェック。sourceがtargetの1/3以上の強さなら通過。
    /// 挑発属性と同じパターンだが、吸引は二値判定（救済なし）。
    /// </summary>
    private static bool PassesStrengthCheck(BaseStates source, BaseStates target)
    {
        float targetSum = target.TenDayValuesSumBase();
        if (targetSum <= 0f) return true; // ゼロ除算防止

        float ratio = source.TenDayValuesSumForSkill() / targetSum;
        return ratio > StrengthThreshold;
    }
}
