using System.Collections.Generic;
using System.Linq;
using RandomExtensions;

/// <summary>
/// 友情コンビ登録の判定ロジック。
/// 4シグナルの収集とカウント式確率テーブルによる登録判定を行う。
/// </summary>
public static class FriendshipComboJudge
{
    /// <summary>カウント式確率テーブル（%）: index = 該当シグナル数</summary>
    private static readonly int[] ProbabilityTable = { 0, 3, 12, 30, 60 };

    /// <summary>腐れ縁パスの無条件登録確率（%）</summary>
    private const int KusareEnProbability = 4;

    /// <summary>ダメージ効率の閾値（総与ダメージ ÷ 総MaxHP）</summary>
    private const float DamageEfficiencyThreshold = 1.0f;

    /// <summary>
    /// 友情コンビ登録の4シグナル
    /// </summary>
    public struct ComboSignals
    {
        /// <summary>戦果: 味方を1人でも倒したか</summary>
        public bool HasBattleResult;
        /// <summary>仲間の死: 敵メンバーが戦闘中に死亡したか</summary>
        public bool HasComradeDeath;
        /// <summary>同情効果: 結成時にHP≤50%のメンバーがいたか</summary>
        public bool HasSympathy;
        /// <summary>ダメージ効率: 総与ダメージ÷総MaxHP ≥ 1.0</summary>
        public bool HasDamageEfficiency;

        public int Count
        {
            get
            {
                int c = 0;
                if (HasBattleResult) c++;
                if (HasComradeDeath) c++;
                if (HasSympathy) c++;
                if (HasDamageEfficiency) c++;
                return c;
            }
        }
    }

    /// <summary>
    /// 戦闘終了時に4シグナルを収集する。
    /// damageEvalTargets を指定すると、ダメージ効率の計算対象をそのメンバーに限定する
    /// （グループ逃走時に逃走者だけで評価するために使用）。
    /// </summary>
    public static ComboSignals CollectSignals(
        BattleGroup enemyGroup,
        BattleGroup allyGroup,
        IReadOnlyList<NormalEnemy> damageEvalTargets = null)
    {
        var signals = new ComboSignals();

        // 1. 戦果: 味方に死亡者がいるか
        for (var i = 0; i < allyGroup.Ours.Count; i++)
        {
            if (allyGroup.Ours[i].Death())
            {
                signals.HasBattleResult = true;
                break;
            }
        }

        // 2. 仲間の死: 敵メンバーに死亡者がいるか
        for (var i = 0; i < enemyGroup.Ours.Count; i++)
        {
            if (enemyGroup.Ours[i].Death())
            {
                signals.HasComradeDeath = true;
                break;
            }
        }

        // 3. 同情効果: 結成時フラグ
        signals.HasSympathy = enemyGroup.HasFormationSympathy;

        // 4. ダメージ効率: 評価対象が明示されていればそのメンバーのみ、なければOurs全体
        if (damageEvalTargets != null)
        {
            var totalDamage = CalculateTotalEnemyDamageFromList(damageEvalTargets, allyGroup);
            var targetMaxHP = SumMaxHP(damageEvalTargets);
            signals.HasDamageEfficiency = targetMaxHP > 0 && (totalDamage / targetMaxHP) >= DamageEfficiencyThreshold;
        }
        else
        {
            var totalDamage = CalculateTotalEnemyDamageFromGroup(enemyGroup, allyGroup);
            var groupMaxHP = enemyGroup.GroupTotalMaxHP;
            signals.HasDamageEfficiency = groupMaxHP > 0 && (totalDamage / groupMaxHP) >= DamageEfficiencyThreshold;
        }

        return signals;
    }

    /// <summary>
    /// 味方のdamageDatasを走査し、enemyGroup.Oursに属する敵が与えた総ダメージを算出する。
    /// 通常経路（戦闘終了時）用。
    /// </summary>
    private static float CalculateTotalEnemyDamageFromGroup(BattleGroup enemyGroup, BattleGroup allyGroup)
    {
        var enemySet = new HashSet<BaseStates>(enemyGroup.Ours);
        return SumDamageFromSet(enemySet, allyGroup);
    }

    /// <summary>
    /// 味方のdamageDatasを走査し、指定リストに属する敵が与えた総ダメージを算出する。
    /// グループ逃走経路（逃走者のみで評価）用。
    /// </summary>
    private static float CalculateTotalEnemyDamageFromList(IReadOnlyList<NormalEnemy> targets, BattleGroup allyGroup)
    {
        var enemySet = new HashSet<BaseStates>();
        for (var i = 0; i < targets.Count; i++)
            enemySet.Add(targets[i]);
        return SumDamageFromSet(enemySet, allyGroup);
    }

    private static float SumDamageFromSet(HashSet<BaseStates> enemySet, BattleGroup allyGroup)
    {
        var total = 0f;
        for (var i = 0; i < allyGroup.Ours.Count; i++)
        {
            var ally = allyGroup.Ours[i];
            if (ally.damageDatas == null) continue;
            for (var j = 0; j < ally.damageDatas.Count; j++)
            {
                var dd = ally.damageDatas[j];
                if (dd.Attacker != null && enemySet.Contains(dd.Attacker) && !dd.IsHeal)
                    total += dd.Damage;
            }
        }
        return total;
    }

    private static float SumMaxHP(IReadOnlyList<NormalEnemy> targets)
    {
        var total = 0f;
        for (var i = 0; i < targets.Count; i++)
            total += targets[i].MaxHP;
        return total;
    }

    /// <summary>
    /// 登録判定を行う。正規パス → 腐れ縁パスの順に判定。
    /// </summary>
    /// <returns>登録する場合true</returns>
    public static bool ShouldRegister(ComboSignals signals, out bool isKusareEn, float outcomeMultiplier = 1.0f)
    {
        isKusareEn = false;

        // 正規パス: カウント式確率テーブル × 結果倍率
        var count = System.Math.Min(signals.Count, ProbabilityTable.Length - 1);
        var probability = (int)(ProbabilityTable[count] * outcomeMultiplier);
        if (probability > 0 && RandomEx.Shared.NextInt(100) < probability)
            return true;

        // 腐れ縁パス: 無条件4%
        if (RandomEx.Shared.NextInt(100) < KusareEnProbability)
        {
            isKusareEn = true;
            return true;
        }

        return false;
    }
}
