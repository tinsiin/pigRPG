using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// AI用の戦闘記憶。個体（NormalEnemy）ごとに保持し、被害・行動の履歴を記録する。
/// SOに持たせず個体に持たせることでSO共有汚染を回避する。
/// </summary>
public class BattleMemory
{
    // ── 被害記録 ──────────────────────────────────────────────
    private readonly List<DamageRecord> _damageRecords = new();
    public IReadOnlyList<DamageRecord> DamageRecords => _damageRecords;

    // ── 行動記録 ──────────────────────────────────────────────
    private readonly List<ActionRecord> _actionRecords = new();
    public IReadOnlyList<ActionRecord> ActionRecords => _actionRecords;

    // ── カウンター被弾記録 ──────────────────────────────────────
    private readonly List<CounterRecord> _counterRecords = new();
    public IReadOnlyList<CounterRecord> CounterRecords => _counterRecords;

    // ── 死亡記録 ──────────────────────────────────────────────
    private readonly List<DeathRecord> _deathRecords = new();
    public IReadOnlyList<DeathRecord> DeathRecords => _deathRecords;

    // ── 前ターンHP（HP急減検出用）─────────────────────────────
    public float LastTurnHP { get; set; }
    public float LastTurnMentalHP { get; set; }

    // =============================================================================
    // 記録メソッド
    // =============================================================================

    public void RecordDamage(DamageRecord record)
    {
        _damageRecords.Add(record);
    }

    public void RecordAction(ActionRecord record)
    {
        _actionRecords.Add(record);
    }

    public void RecordCounter(CounterRecord record)
    {
        _counterRecords.Add(record);
    }

    public void RecordDeath(DeathRecord record)
    {
        _deathRecords.Add(record);
    }

    /// <summary>
    /// ターン開始時にHP/精神HPのスナップショットを保存する
    /// </summary>
    public void SnapshotHP(float hp, float mentalHP)
    {
        LastTurnHP = hp;
        LastTurnMentalHP = mentalHP;
    }

    // =============================================================================
    // 参照ユーティリティ
    // =============================================================================

    /// <summary>
    /// 指定スキルで受けた被害記録を返す
    /// </summary>
    public IEnumerable<DamageRecord> GetDamageBySkill(BaseSkill skill)
        => _damageRecords.Where(r => r.Skill == skill);

    /// <summary>
    /// 指定攻撃者からの被害記録を返す
    /// </summary>
    public IEnumerable<DamageRecord> GetDamageFrom(BaseStates attacker)
        => _damageRecords.Where(r => r.Attacker == attacker);

    /// <summary>
    /// 直近N件の被害記録を返す
    /// </summary>
    public IEnumerable<DamageRecord> RecentDamage(int count)
        => _damageRecords.Skip(Math.Max(0, _damageRecords.Count - count));

    /// <summary>
    /// このターンに受けた合計ダメージ
    /// </summary>
    public float TotalDamageThisTurn(int currentTurn)
        => _damageRecords.Where(r => r.Turn == currentTurn).Sum(r => r.Damage);

    /// <summary>
    /// HP急減率（前ターンからのHP減少割合）
    /// </summary>
    public float HpDropRate(float currentHP)
    {
        if (LastTurnHP <= 0f) return 0f;
        return Mathf.Clamp01((LastTurnHP - currentHP) / LastTurnHP);
    }

    /// <summary>
    /// カウンターされた回数
    /// </summary>
    public int CounterCount => _counterRecords.Count;

    /// <summary>
    /// 指定スキル使用時にカウンターされた回数
    /// </summary>
    public int CounterCountBySkill(BaseSkill skill)
        => _counterRecords.Count(r => r.SkillUsed == skill);

    /// <summary>
    /// 自分が使ったスキルの使用回数
    /// </summary>
    public int SkillUseCount(BaseSkill skill)
        => _actionRecords.Count(r => r.Skill == skill);

    /// <summary>
    /// 直近N件の自分の行動記録
    /// </summary>
    public IEnumerable<ActionRecord> RecentActions(int count)
        => _actionRecords.Skip(Math.Max(0, _actionRecords.Count - count));

    /// <summary>
    /// 味方の死亡数（自分を含まない）
    /// </summary>
    public int AllyDeathCount => _deathRecords.Count(d => d.IsAlly && !d.IsSelf);

    /// <summary>
    /// 味方死亡の相性値重み付き合計。
    /// 70未満=0, 70~79=×1.0, 80~90=×1.5, 91+=×2.5
    /// </summary>
    public float AllyDeathTraumaWeight
    {
        get
        {
            float total = 0f;
            foreach (var d in _deathRecords)
            {
                if (!d.IsAlly || d.IsSelf) continue;
                total += AffinityToTraumaWeight(d.Affinity);
            }
            return total;
        }
    }

    /// <summary>
    /// 相性値からトラウマ重みを返す
    /// </summary>
    private static float AffinityToTraumaWeight(int affinity)
    {
        if (affinity < 70) return 0f;
        if (affinity <= 79) return 1.0f;  // 気にかける
        if (affinity <= 90) return 1.5f;  // 親しい
        return 2.5f; // 91+ 親友
    }

    /// <summary>
    /// 戦闘開始時にリセット
    /// </summary>
    public void Clear()
    {
        _damageRecords.Clear();
        _actionRecords.Clear();
        _counterRecords.Clear();
        _deathRecords.Clear();
        LastTurnHP = 0f;
        LastTurnMentalHP = 0f;
    }
}

// =============================================================================
// Record構造体
// =============================================================================

/// <summary>
/// 被害記録（1回の被ダメージにつき1レコード）
/// </summary>
public struct DamageRecord
{
    public BaseStates Attacker;
    public BaseSkill Skill;
    public float Damage;
    public float MentalDamage;
    public HitResult HitResult;
    public int Turn;
    public bool ResultedInDeath;
}

/// <summary>
/// 行動記録（自分が行動したときに1レコード）
/// </summary>
public struct ActionRecord
{
    public BaseSkill Skill;
    public BaseStates Target;
    public int Turn;
    public bool WasEscape;
}

/// <summary>
/// カウンター被弾記録
/// </summary>
public struct CounterRecord
{
    public BaseStates CounteredBy;
    public BaseSkill SkillUsed;    // カウンターされた時に自分が使っていたスキル
    public int Turn;
}

/// <summary>
/// 死亡記録（味方の死亡も含む）
/// </summary>
public struct DeathRecord
{
    public BaseStates Victim;
    public BaseStates Killer;
    public BaseSkill KillerSkill;
    public int Turn;
    public bool IsSelf;
    public bool IsAlly;
    /// <summary>
    /// 死亡した味方に対する自分の相性値（0〜100+）。IsSelf=trueの場合は0。
    /// </summary>
    public int Affinity;
}
