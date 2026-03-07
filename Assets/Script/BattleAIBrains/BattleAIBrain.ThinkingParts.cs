using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BattleAIBrain partial: 思考部品パーツ（パッシブキャンセル・ストック/トリガー情報・Freeze情報・カウンターリスク推定・回復/付与スコアリング）
/// </summary>
public abstract partial class BattleAIBrain
{
    // ── 定数（カウンターリスク推定用） ──
    private const float CounterVondBonusRate = 0.01f;
    private const float CounterAbilityWeight = 0.9f;
    private const float CounterFixedGate = 0.5f;
    private const float DefaultExplosionVoid = 10f;
    private const float CounterDivisionEpsilon = 0.01f;

    // ---------- パッシブキャンセル関連----------------------------------

    /// <summary>
    /// パッシブキャンセル
    /// </summary>
    protected void CancelPassive(BasePassive passive)
    {
        if(passive != null)
        {
            // パッシブのキャンセル処理
            user.RemovePassive(passive);
            manager.PassiveCancel = true;//ACTBranchingでパッシブキャンセルするboolをtrueに。
        }else
        {
            manager.DoNothing = true;//ACTBranchingで何もしないようにするboolをtrueに。
            //nullなら直前の思考で消せるパッシブがなかったはずだから。
        }
    }
    /// <summary>
    /// 「IsCantACT かつ CanCancel」のパッシブのリストを返す。
    /// </summary>
    protected List<BasePassive> SelectCancelableCantActPassives()
    {
        // 同居パッシブの候補を抽出
        return user.Passives.Where(p => p != null && p.IsCantACT && p.CanCancel).ToList();
    }


    /// <summary>
    /// 消せるパッシブリストとして渡されたリストから、ランダムにisbad = trueのパッシブから
    /// 一つだけ選んで返す。　　なかった場合はnull
    /// </summary>
    /// <param name="CanCancelPassives">消せるパッシブリストのみを渡してください。</param>
    protected BasePassive RandomSelectCanCancelPassiveOnlyBadPassives(List<BasePassive> CanCancelPassives)
    {
        var badPassives = CanCancelPassives.Where(p => p.IsBad).ToList();//悪いパッシブのみ選別
        if(badPassives.Count == 0)
        {
            Debug.Log("消せるパッシブリストに悪いパッシブが存在しません(skillAI)");
            return null;
        }
        return RandomSource.GetItem(badPassives);//ランダムに一つ入手
    }


    // ---------- ストック・トリガー情報取得ユーティリティ----------------------------------

    protected struct StockInfo
    {
        public int Current;      // 現在のストック数
        public int Max;          // 最大値（DefaultAtkCount）
        public int Default;      // デフォルト値（DefaultStockCount）
        public bool IsFull;      // 満杯か
        public int StockPower;   // 1回のストックで増える量
        public int TurnsToFull;  // 満杯まであと何回ストックが必要か
        public float FillRate;   // 充填率（Current / Max）
    }

    protected struct TriggerInfo
    {
        public int CurrentCount;    // 現在のカウント
        public int MaxCount;        // 最大カウント
        public bool IsTriggering;   // カウント中か
        public int RemainingTurns;  // 発動まであと何ターンか
        public int RollBackCount;   // 巻き戻し量
        public bool CanCancel;      // キャンセル可能か
    }

    /// <summary>
    /// Stockpileフラグを持つスキルを列挙する
    /// </summary>
    protected IEnumerable<BaseSkill> GetStockpileSkills(IEnumerable<BaseSkill> skills)
    {
        if (skills == null) yield break;
        foreach (var s in skills)
        {
            if (s != null && s.HasConsecutiveType(SkillConsecutiveType.Stockpile))
                yield return s;
        }
    }

    /// <summary>
    /// 指定スキルのストック状態を一括取得する
    /// </summary>
    protected StockInfo GetStockInfo(BaseSkill skill)
    {
        if (skill == null) return default;
        int current = skill.NowStockCount;
        int max = skill.MaxStockCount;
        int power = skill.StockPower;
        int remaining = power > 0 ? Mathf.Max(0, Mathf.CeilToInt((float)(max - current) / power)) : 0;
        return new StockInfo
        {
            Current = current,
            Max = max,
            Default = skill.StockDefault,
            IsFull = skill.IsFullStock(),
            StockPower = power,
            TurnsToFull = remaining,
            FillRate = max > 0 ? (float)current / max : 0f,
        };
    }

    /// <summary>
    /// 発動カウント付きスキルを列挙する
    /// </summary>
    protected IEnumerable<BaseSkill> GetTriggerSkills(IEnumerable<BaseSkill> skills)
    {
        if (skills == null) yield break;
        foreach (var s in skills)
        {
            if (s != null && s.TriggerMax > 0)
                yield return s;
        }
    }

    /// <summary>
    /// 指定スキルのトリガー状態を一括取得する
    /// </summary>
    protected TriggerInfo GetTriggerInfo(BaseSkill skill)
    {
        if (skill == null) return default;
        int current = skill.CurrentTriggerCount;
        int max = skill.TriggerMax;
        return new TriggerInfo
        {
            CurrentCount = current,
            MaxCount = max,
            IsTriggering = skill.IsTriggering,
            RemainingTurns = current + 1, // -1到達で発動のため。カウント未開始時はMaxCount+1を返す
            RollBackCount = skill.TriggerRollBack,
            CanCancel = skill.CanCancelTrigger,
        };
    }


    // ---------- Freeze情報・カウンターリスク推定 ----------------------------------

    protected struct FreezeInfo
    {
        public BaseSkill Skill;           // 凍結中のスキル
        public bool IsFreeze;             // 凍結中か
        public SkillZoneTrait RangeWill;  // 凍結中の範囲
        public bool CanOperate;           // 操作可能（範囲/対象を変更できる）か
        public bool WillBeDeleted;        // 次ターンで打ち切り予約があるか
    }

    /// <summary>
    /// 自キャラのFreeze状態を一括取得する
    /// </summary>
    protected FreezeInfo GetFreezeInfo()
    {
        if (user == null) return default;
        var skill = user.FreezeUseSkill;
        return new FreezeInfo
        {
            Skill = skill,
            IsFreeze = user.IsFreeze,
            RangeWill = user.FreezeRangeWill,
            CanOperate = skill != null
                && skill.NowConsecutiveATKFromTheSecondTimeOnward()
                && skill.HasConsecutiveType(SkillConsecutiveType.CanOprate),
            WillBeDeleted = user.IsDeleteMyFreezeConsecutive,
        };
    }

    /// <summary>
    /// ターゲットにカウンターされるリスクを推定する (0.0~1.0)。
    /// TryInterruptCounterのロジックを確率論的に簡易近似。乱数は使わない。
    /// </summary>
    protected float EstimateCounterRisk(BaseStates target, BaseSkill skill)
    {
        if (target == null || skill == null || user == null) return 0f;
        if (!target.IsInterruptCounterActive) return 0f;
        // カウンター側のPowerLevelがMedium未満なら発動不可
        if (target.NowPower < PowerLevel.Medium) return 0f;

        // Gate 1: DEFATK/3 + Vond差補正
        var userVond = user.TenDayValuesForSkill().GetValueOrZero(TenDayAbility.Vond);
        var targetVond = target.TenDayValuesBase().GetValueOrZero(TenDayAbility.Vond);
        float vondBonus = targetVond > userVond ? (targetVond - userVond) * CounterVondBonusRate : 0f;
        float gate1 = Mathf.Clamp01(skill.DEFATK / 3f + vondBonus);

        // Gate 2: カウンター側 vs 攻撃側の能力値比較
        var targetPD = target.TenDayValuesBase().GetValueOrZero(TenDayAbility.PersonaDivergence);
        var targetTV = target.TenDayValuesBase().GetValueOrZero(TenDayAbility.TentVoid);
        var userSort = user.TenDayValuesForSkill().GetValueOrZero(TenDayAbility.Sort);
        var userRain = user.TenDayValuesForSkill().GetValueOrZero(TenDayAbility.Rain);
        var userCold = user.TenDayValuesForSkill().GetValueOrZero(TenDayAbility.ColdHeartedCalm);

        float denom = targetTV - DefaultExplosionVoid;
        // counterValueは負になりうる（targetTV < DefaultExplosionVoidの場合）が、その場合gate2 ≤ 0 → Clamp01で0になり実害なし
        float counterValue = (targetVond + (Mathf.Abs(denom) > CounterDivisionEpsilon ? targetPD / denom : 0f)) * CounterAbilityWeight;
        float attackerValue = Mathf.Max(userSort - userRain / 3f, 0f) + userCold;

        float total = counterValue + attackerValue;
        float gate2 = total > CounterDivisionEpsilon ? counterValue / total : 0f;

        // Gate 3: 50%固定判定
        return Mathf.Clamp01(gate1 * gate2 * CounterFixedGate);
    }


    // =============================================================================
    // 思考部品のパーツ：回復・付与スキルスコアリング
    // =============================================================================

    /// <summary>
    /// 候補群から「回復系」で最も効果的なスキルを選ぶ（簡易スコア）。
    /// 派生で ScoreHealSkill を override すれば評価基準を差し替え可能。
    /// </summary>
    protected BaseSkill SelectMostEffectiveHealSkill(BaseStates self, IEnumerable<BaseSkill> candidates)
    {
        if (self == null || candidates == null) return null;
        var list = candidates.Where(s => s.HasType(SkillType.Heal) || s.HasType(SkillType.MentalHeal) || s.HasType(SkillType.DeathHeal)).ToList();
        if (list.Count == 0) return null;

        BaseSkill best = null;
        float bestScore = float.MinValue;
        foreach (var s in list)
        {
            float score = ScoreHealSkill(self, s);
            if (score > bestScore)
            {
                bestScore = score;
                best = s;
            }
        }
        return best;
    }

    /// <summary>
    /// 回復スキルの効果スコア（デフォルト簡易版）。
    /// - 本体HP回復量（概算）を重視、精神回復は0.5係数で加点。
    /// - DeathHeal は死亡時に大きく優先。
    /// </summary>
    protected virtual float ScoreHealSkill(BaseStates self, BaseSkill skill)
    {
        if (self == null || skill == null) return 0f;

        float hpScore = 0f;
        if (skill.HasType(SkillType.Heal))
        {
            // 概算: SkillPowerCalc をそのまま加点（詳細補正は派生で）
            hpScore = Mathf.Max(0f, skill.SkillPowerCalc(skill.IsTLOA, self));
        }

        float mentalScore = 0f;
        if (skill.HasType(SkillType.MentalHeal))
        {
            mentalScore = Mathf.Max(0f, skill.SkillPowerForMentalCalc(skill.IsTLOA, self)) * 0.5f;
        }

        float deathBonus = (skill.HasType(SkillType.DeathHeal) && self.Death()) ? 100000f : 0f;
        return hpScore + mentalScore + deathBonus;
    }

    /// <summary>
    /// 候補群から「付与系」で最も効果的なスキルを選ぶ（簡易スコア）。
    /// 派生で ScoreAddPassiveSkill を override すれば評価基準を差し替え可能。
    /// </summary>
    protected BaseSkill SelectBestPassiveGrantSkill(BaseStates self, IEnumerable<BaseSkill> candidates)
    {
        if (self == null || candidates == null) return null;
        var list = candidates.Where(s => s.HasType(SkillType.addPassive) || s.HasType(SkillType.AddVitalLayer) || s.HasType(SkillType.addSkillPassive)).ToList();
        if (list.Count == 0) return null;

        BaseSkill best = null;
        float bestScore = float.MinValue;
        foreach (var s in list)
        {
            float score = ScoreAddPassiveSkill(self, s);
            if (score > bestScore)
            {
                bestScore = score;
                best = s;
            }
        }
        return best;
    }

    /// <summary>
    /// 付与スキルの効果スコア（デフォルト簡易版）。
    /// - 良いパッシブ/追加HP/スキルパッシブの付与数でスコアリング。
    /// </summary>
    protected virtual float ScoreAddPassiveSkill(BaseStates self, BaseSkill skill)
    {
        if (self == null || skill == null) return 0f;
        int goodPassiveCount = 0;
        try
        {
            // Phase 3b: DI注入を優先、フォールバックでPassiveManager.Instance
            var provider = self.PassiveProvider ?? PassiveManager.Instance;
            // PassiveManager/VitalLayerManager が利用可能なら良性のみカウント
            if (skill.SubEffects != null)
            {
                goodPassiveCount = provider != null
                    ? skill.SubEffects.Count(id => !provider.GetAtID(id).IsBad)
                    : skill.SubEffects.Count;  // providerがない場合は総数で代替
            }
        }
        catch { goodPassiveCount = skill.SubEffects?.Count ?? 0; }

        int vitalCount = skill.SubVitalLayers?.Count ?? 0;
        int skillPassiveCount = skill.AggressiveSkillPassiveList?.Count ?? 0;
        return goodPassiveCount * 1.0f + vitalCount * 0.8f + skillPassiveCount * 0.6f;
    }
}
