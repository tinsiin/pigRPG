using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 戦闘終了時にBattleMemoryの記録を振り返り、敵同士の相性値変動量を算出する。
/// BattleManager.OnBattleEnd() から EnemiesBattleEndSkillAI() の前に呼ばれる。
/// </summary>
public static class PostBattleBondCalculator
{
    // 変動量定数（仮値、プレイテストで調整）
    private const int BondHealReceived = 7;       // 味方から回復を受けた
    private const int BondShielded = 10;          // 味方がかばってくれた（テラーズヒット1回あたり）
    private const int BondEnemyKilled = 4;        // 敵を殺した（グループ全員）
    private const int BondAllyEscaped = -15;      // 味方が逃走した

    /// <summary>
    /// 敵グループの全メンバーについて相性値変動を計算し、
    /// BattleGroup.CharaCompatibility と NormalEnemy._bondDeltas の両方を更新する。
    /// </summary>
    public static void Apply(BattleGroup enemyGroup)
    {
        if (enemyGroup == null) return;

        var members = enemyGroup.Ours.OfType<NormalEnemy>().ToList();

        // 逃走者と残留者の間のボンド変動
        ApplyEscapeBonds(enemyGroup, members);

        if (members.Count < 2) return;

        // 残留メンバー間のペアごと変動量を計算
        var deltas = CalcDeltas(members);

        // CharaCompatibility に即反映（PostBattle行動用）+ 個体に永続化
        ApplyDeltas(enemyGroup, deltas);
    }

    private static void ApplyDeltas(BattleGroup group, List<(NormalEnemy A, NormalEnemy B, int Delta)> deltas)
    {
        foreach (var (a, b, delta) in deltas)
        {
            if (delta == 0) continue;

            if (group.CharaCompatibility.TryGetValue((a, b), out var currentAB))
            {
                group.CharaCompatibility[(a, b)] = Mathf.Clamp(currentAB + delta, 0, 160);
            }
            else
            {
                Debug.LogWarning($"PostBattleBondCalculator: CharaCompatibility にキーがありません ({a.CharacterName} -> {b.CharacterName})");
            }

            if (group.CharaCompatibility.TryGetValue((b, a), out var currentBA))
            {
                group.CharaCompatibility[(b, a)] = Mathf.Clamp(currentBA + delta, 0, 160);
            }

            a.RecordBondDelta(b.EnemyGuid, delta);
            b.RecordBondDelta(a.EnemyGuid, delta);
        }
    }

    private static List<(NormalEnemy A, NormalEnemy B, int Delta)> CalcDeltas(List<NormalEnemy> members)
    {
        var result = new List<(NormalEnemy, NormalEnemy, int)>();

        for (var i = 0; i < members.Count; i++)
        {
            for (var j = i + 1; j < members.Count; j++)
            {
                var a = members[i];
                var b = members[j];
                int delta = 0;

                delta += CalcHealBond(a, b);
                delta += CalcHealBond(b, a);
                delta += CalcShieldBond(a, b);

                result.Add((a, b, delta));
            }
        }

        int killBonus = CalcKillBonus(members);
        if (killBonus > 0)
        {
            for (var i = 0; i < result.Count; i++)
            {
                var (a, b, d) = result[i];
                result[i] = (a, b, d + killBonus);
            }
        }

        return result;
    }

    /// <summary>
    /// AがBから回復を受けた → A-B間の相性値が上がる。
    /// healerのActionRecordからTargetsにreceiverが含まれるHeal系スキルを検出する。
    /// ※TargetsはSkillExecutor.SkillACT内でPatchLastActionTargetsにより補填される。
    /// </summary>
    private static int CalcHealBond(NormalEnemy receiver, NormalEnemy healer)
    {
        var healerMemory = healer.AIMemory;
        if (healerMemory == null) return 0;

        int delta = 0;
        foreach (var act in healerMemory.ActionRecords)
        {
            if (act.Targets == null || !act.Targets.Contains(receiver)) continue;
            if (act.Skill == null) continue;
            if (act.Skill.HasType(SkillType.Heal) || act.Skill.HasType(SkillType.MentalHeal) || act.Skill.HasType(SkillType.DeathHeal))
            {
                delta += BondHealReceived;
            }
        }

        return delta;
    }

    /// <summary>
    /// 前のめり者がテラーズヒットで味方をかばった → 全味方との相性値が上がる。
    /// ShieldRecordは前のめり者のBattleMemoryに記録される。
    /// </summary>
    private static int CalcShieldBond(NormalEnemy a, NormalEnemy b)
    {
        int delta = 0;
        if (a.AIMemory != null) delta += a.AIMemory.ShieldCount * BondShielded;
        if (b.AIMemory != null) delta += b.AIMemory.ShieldCount * BondShielded;
        return delta;
    }

    /// <summary>
    /// 逃走者と残留者の間の相性値ペナルティ。
    /// 逃走者はEscapeAndRemoveでOursから除去済みなので、BattleGroup.EscapedMembersから取得する。
    /// </summary>
    private static void ApplyEscapeBonds(BattleGroup group, List<NormalEnemy> remainingMembers)
    {
        var escapers = group.EscapedMembers;
        if (escapers == null || escapers.Count == 0) return;
        if (remainingMembers.Count == 0) return;

        foreach (var escaper in escapers)
        {
            foreach (var stayer in remainingMembers)
            {
                stayer.RecordBondDelta(escaper.EnemyGuid, BondAllyEscaped);
                escaper.RecordBondDelta(stayer.EnemyGuid, BondAllyEscaped);
            }
        }
    }

    /// <summary>
    /// グループが敵を1体以上殺した場合のボーナス（全員の連帯感）
    /// </summary>
    private static int CalcKillBonus(List<NormalEnemy> members)
    {
        foreach (var member in members)
        {
            var memory = member.AIMemory;
            if (memory == null) continue;

            foreach (var death in memory.DeathRecords)
            {
                if (!death.IsAlly && !death.IsSelf)
                {
                    return BondEnemyKilled;
                }
            }
        }

        return 0;
    }
}
