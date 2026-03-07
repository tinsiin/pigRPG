using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BattleAIBrain partial: 3軸パーソナリティ・逃走・トラウマ・記憶参照・パッシブ読み
/// </summary>
public abstract partial class BattleAIBrain
{
    // ── 定数 ──
    private const float EscapeHpDropBonus = 0.2f;
    private const float PerceptionRateHigh = 0.9f;
    private const float PerceptionRateLow = 0.7f;

    // ── フィールド ──

    [Header("逃走設定")]
    [SerializeField, Range(0f, 1f)] private float _escapeChance = 0f;
    [SerializeField] private bool _canEscape = false;

    [Header("思慮推測レベル（3軸パーソナリティ: 知性軸）")]
    [Tooltip("0=情報読みなし, 1=HP・基本判断, 2=パッシブ読み(不完全)+カウンターリスク推定, 3=高精度パッシブ+ポイント推測")]
    [SerializeField, Range(0, 3)] private int _deliberationLevel = 0;

    [Header("精神レベル（3軸パーソナリティ: EQ軸）")]
    [Tooltip("0=精神判断なし(物理のみ), 1=精神HP削れ認識, 2=精神属性相性活用, 3=物理vs精神の最適判断")]
    [SerializeField, Range(0, 3)] private int _spiritualLevel = 0;

    [Header("トラウマ設定")]
    [Tooltip("カウンター1回あたりのトラウマ率上昇量")]
    [SerializeField, Range(0f, 0.3f)] private float _traumaPerCounter = 0.15f;
    [Tooltip("HP急減時のトラウマ率ボーナス倍率（HpDropRate × この値がトラウマに加算）")]
    [SerializeField, Range(0f, 1f)] private float _traumaHpDropWeight = 0.3f;
    [Tooltip("味方死亡1回あたりのトラウマ率上昇量")]
    [SerializeField, Range(0f, 0.3f)] private float _traumaPerAllyDeath = 0.1f;
    [Tooltip("トラウマ率の上限（これ以上は上がらない）")]
    [SerializeField, Range(0f, 1f)] private float _traumaCap = 0.8f;

    // ── 逃走判断 ──

    /// <summary>
    /// 逃走判断。HP急減・トラウマが逃走確率を押し上げる。
    /// </summary>
    protected virtual bool ShouldEscape()
    {
        if (!_canEscape) return false;
        // 基本逃走確率 + HP急減ボーナス
        float chance = _escapeChance + HpDropRate * EscapeHpDropBonus;
        chance = Mathf.Clamp01(chance);
        bool result = Roll(chance);
        if (result) LogThink(2, $"逃走判定: 成功 (base={_escapeChance:P0} hpDrop={HpDropRate:F2} effective={chance:P0})");
        return result;
    }

    // ── トラウマシステム ──

    /// <summary>
    /// 現在のトラウマ率を計算する (0.0~_traumaCap)。
    /// カウンター被弾・HP急減・味方死亡で上昇する感情軸パラメータ。
    /// </summary>
    protected float TraumaRate
    {
        get
        {
            var mem = Memory;
            if (mem == null) return 0f;

            float rate = 0f;
            // カウンター被弾
            rate += mem.CounterCount * _traumaPerCounter;
            // HP急減
            rate += HpDropRate * _traumaHpDropWeight;
            // 味方死亡（相性値で重み付け: 70未満=0, 70~79=×1.0, 80~90=×1.5, 91+=×2.5）
            rate += mem.AllyDeathTraumaWeight * _traumaPerAllyDeath;

            return Mathf.Clamp(rate, 0f, _traumaCap);
        }
    }

    /// <summary>
    /// 指定スキルに対してトラウマ回避が発動するか判定する。
    /// そのスキルでカウンターされた経験がある場合のみトラウマ判定の対象になる。
    /// </summary>
    protected bool IsTraumaAvoided(BaseSkill skill)
    {
        if (skill == null) return false;
        int countered = CounterCountBySkill(skill);
        if (countered <= 0) return false;

        // スキル固有トラウマ = 全体トラウマ率 × (そのスキルでのカウンター回数 / 全カウンター回数)
        float totalTrauma = TraumaRate;
        int totalCounters = CounterCount;
        float skillTrauma = totalCounters > 0
            ? totalTrauma * ((float)countered / totalCounters)
            : 0f;

        bool avoided = Roll(skillTrauma);
        if (avoided)
        {
            LogThink(1, $"トラウマ回避: {skill.SkillName} (trauma={skillTrauma:P0}, countered={countered}回)");
        }
        return avoided;
    }

    /// <summary>
    /// スキルリストからトラウマ回避されたスキルを除外する。
    /// 全スキルが除外された場合はフォールバックとして元のリストを返す。
    /// </summary>
    protected List<BaseSkill> FilterByTrauma(List<BaseSkill> skills)
    {
        if (skills == null || skills.Count <= 1) return skills;

        var filtered = skills.Where(s => !IsTraumaAvoided(s)).ToList();
        if (filtered.Count == 0)
        {
            LogThink(1, "トラウマ: 全スキル回避 → フォールバック（元リスト使用）");
            return skills;
        }
        if (filtered.Count < skills.Count)
        {
            LogThink(2, $"トラウマフィルタ: {skills.Count}件 → {filtered.Count}件");
        }
        return filtered;
    }

    // ── 記憶参照ユーティリティ ──

    /// <summary>
    /// 自キャラのAI戦闘記憶（なければnull）
    /// </summary>
    protected BattleMemory Memory => user?.AIMemory;

    /// <summary>
    /// 前ターンからのHP急減率 (0.0~1.0)
    /// </summary>
    protected float HpDropRate => Memory?.HpDropRate(user?.HP ?? 0f) ?? 0f;

    /// <summary>
    /// このターンに受けた合計ダメージ
    /// </summary>
    protected float DamageThisTurn => Memory?.TotalDamageThisTurn(TurnCount) ?? 0f;

    /// <summary>
    /// カウンターされた総回数
    /// </summary>
    protected int CounterCount => Memory?.CounterCount ?? 0;

    /// <summary>
    /// 指定スキルでカウンターされた回数
    /// </summary>
    protected int CounterCountBySkill(BaseSkill skill) => Memory?.CounterCountBySkill(skill) ?? 0;

    /// <summary>
    /// 指定スキルの使用回数
    /// </summary>
    protected int SkillUseCount(BaseSkill skill) => Memory?.SkillUseCount(skill) ?? 0;

    /// <summary>
    /// 味方の死亡数（自分を含まない）
    /// </summary>
    protected int AllyDeathCount => Memory?.AllyDeathCount ?? 0;

    // ── 思慮推測レベル・パッシブ読み ──

    /// <summary>
    /// 思慮推測レベル（0-3）。派生AIでの条件分岐用プロパティ。
    /// </summary>
    protected int DeliberationLevel => _deliberationLevel;

    /// <summary>
    /// 精神レベル（0-3）。派生AIでの条件分岐用プロパティ。
    /// </summary>
    protected int SpiritualLevel => _spiritualLevel;

    /// <summary>
    /// 思慮レベルに応じたパッシブ認識率（Lv3=90%, Lv2=70%）
    /// </summary>
    private float PerceptionRate => _deliberationLevel >= 3 ? PerceptionRateHigh : PerceptionRateLow;

    /// <summary>
    /// ターゲットのパッシブリストを思慮レベルに応じてフィルタしたコピーを返す。
    /// 直接参照を返さないことで戦闘システムへの副作用を防止する。
    /// Lv0-1: 空リスト（パッシブ読み不可）
    /// Lv2: 各パッシブに30%の見落とし確率
    /// Lv3: 各パッシブに10%の見落とし確率（100%にはしない）
    /// </summary>
    protected List<BasePassive> ReadTargetPassives(BaseStates target)
    {
        if (target == null || _deliberationLevel <= 1) return new List<BasePassive>();

        var passives = target.Passives;
        if (passives == null || passives.Count == 0) return new List<BasePassive>();

        // コピーを作成（直接参照で戦闘システム破壊を防ぐ）
        var result = new List<BasePassive>(passives.Count);
        foreach (var p in passives)
        {
            if (p != null && Roll(PerceptionRate))
                result.Add(p);
        }

        LogThink(2, $"パッシブ読み(Lv{_deliberationLevel}): {target.CharacterName} → {result.Count}/{passives.Count}件認識");
        return result;
    }

    /// <summary>
    /// ターゲットが特定IDのパッシブを持っているか推定する（思慮レベル依存）。
    /// Lv0-1: 常にfalse。Lv2: 70%の認識率。Lv3: 90%の認識率。
    /// </summary>
    protected bool CanSeePassive(BaseStates target, int passiveId)
    {
        if (target == null || _deliberationLevel <= 1) return false;
        if (!target.HasPassive(passiveId)) return false;
        return Roll(PerceptionRate);
    }
}
