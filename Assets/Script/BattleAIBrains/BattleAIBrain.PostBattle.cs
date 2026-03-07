using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// BattleAIBrain partial: 戦闘後自己行動（Out-of-Battle）
/// </summary>
public abstract partial class BattleAIBrain
{
    // =============================================================================================================================
    // 戦闘後自己行動（Out-of-Battle）：デフォルトは空実装だが、派生AIで明示的に計画・実行できる基盤
    // - BattleManager 側から戦闘終了時に PostBattleActRun(self) を呼ぶ想定（呼び元はユーザーが実装）
    // - 実行は AI クラス内で完結し、Acter/manager へのコミットは行わない
    // - 実効果の適用には BaseStates.ApplySkillCoreOutOfBattle を使用
    // =============================================================================================================================

    protected struct PostBattleAction
    {
        public BaseStates Target;              // 実行対象（null不可）
        public List<BaseSkill> Skills;         // 複数スキルのシーケンス指定（最大5）
        public bool IsSelf;                    // 自分かどうか（派生でロジックを分けたい時の補助）
        public PostBattleActionOptions Options;// アクション単位のオプション
    }

    // アクション単位のオプション（非公開扱いの内部設定）
    protected struct PostBattleActionOptions
    {
        // Angel(DeathHeal) の再試行確率（0..1）
        // 仕様: DeathHeal実行後も target.Death()==true だった場合、毎回この確率で「もう一度試すか」を判定。
        // true: ポイントが続き、かつ死亡中なら再施行。false: 以後のHeal/MentalHealをスキップして、非回復系は実行。
        public float AngelRepeatChance;
    }

    protected class PostBattleDecision
    {
        public readonly List<PostBattleAction> Actions = new List<PostBattleAction>();
        public bool HasAny => Actions != null && Actions.Count > 0;

        // ヘルパ（任意）: 単一スキルのアクション追加
        public void AddAction(BaseStates target, BaseSkill skill, bool isSelf = false, PostBattleActionOptions? options = null)
        {
            if (target == null || skill == null) return;
            Actions.Add(new PostBattleAction { Target = target, Skills = new List<BaseSkill> { skill }, IsSelf = isSelf, Options = options.GetValueOrDefault() });
        }

        // ヘルパ（任意）: 複数スキルのアクション追加（最大5に丸め）
        public void AddActionSequence(BaseStates target, IEnumerable<BaseSkill> skills, bool isSelf = false, PostBattleActionOptions? options = null)
        {
            if (target == null || skills == null) return;
            var list = skills.Where(s => s != null).Take(5).ToList();
            if (list.Count == 0) return;
            Actions.Add(new PostBattleAction { Target = target, Skills = list, IsSelf = isSelf, Options = options.GetValueOrDefault() });
        }
    }

    // アクションから実行スキル列を解決（最大5）。Skillのみ指定でも動作。
    protected List<BaseSkill> ResolveActionSkills(in PostBattleAction act)
    {
        if (act.Skills != null && act.Skills.Count > 0)
        {
            // 最大5に制限
            if (act.Skills.Count > 5)
            {
                Debug.LogWarning($"PostBattle: Skills が {act.Skills.Count} 件指定されています。最大5件までに丸めます。");
            }
            return act.Skills.Where(s => s != null).Take(5).ToList();
        }
        return new List<BaseSkill>();
    }

    /// <summary>
    /// 戦闘後の自己行動の実行エントリ。AI内部で完結してActionsを順に適用する。
    /// 呼び元（BattleManager等）は対象キャラ(self)を渡して await するだけ。
    /// </summary>
    public async UniTask<bool> PostBattleActRun(BaseStates self, IBattleContext context = null)
    {
        if (self == null)
        {
            Debug.LogError("PostBattleActRun: self が null です");
            return false;
        }

        // 実行主体を AI 側にも保持（PostBattlePlanで派生が参照するため）
        user = self;
        // BM は原則取得可能想定。取得失敗時はログのみ（利他部品側で自分のみへフォールバック）
        manager = context ?? _battleContext ?? BattleContextHub.Current;

        // デフォルトは空プラン。派生で PostBattlePlan(self, decision) を実装して Actions を詰める。
        var decision = new PostBattleDecision();
        PostBattlePlan(self, decision);
        if (!decision.HasAny)
        {
            return false; // 何もしない
        }

        // SO共有汚染メモ: await以降はSOフィールド(user/manager)が他敵のRun()で上書きされうる。
        // 現状selfはメソッドパラメータ（安全）、manager参照はRoll()のみで影響軽微。
        // 根本対策（user/managerをメソッドパラメータ化）は中期リファクタとして別途対応。

        try
        {
            var policy = GetPostBattlePolicy();
            bool any = false;
            foreach (var act in decision.Actions)
            {
                if (act.Target == null)
                {
                    Debug.LogWarning("PostBattleActRun: Action に null Target が含まれています");
                    continue;
                }

                var skills = ResolveActionSkills(in act);
                if (skills.Count == 0) continue;

                bool skipHealsAfterAngelGiveUp = false;

                foreach (var skill in skills)
                {
                    if (skill == null) continue;

                    // Angel諦め後はHeal/MentalHealのみスキップ（付与等は通す）
                    if (skipHealsAfterAngelGiveUp && (skill.HasType(SkillType.Heal) || skill.HasType(SkillType.MentalHeal)))
                    {
                        continue;
                    }

                    // リソース不足なら全体早期終了
                    if (!SkillResourceFlow.CanConsumeOnCast(self, skill))
                    {
                        Debug.Log($"PostBattleActRun: リソース不足のため早期終了 skill={skill.SkillName}");
                        return any;
                    }

                    // DeathHeal(Angel) の特別扱い
                    if (skill.HasType(SkillType.DeathHeal) && act.Target.Death())
                    {
                        // 1回施行
                        await act.Target.ApplySkillCoreOutOfBattle(self, skill, policy);
                        any = true;

                        // 失敗（まだ死亡）なら、毎回確率判定しながら再試行
                        while (act.Target.Death())
                        {
                            var p = Mathf.Clamp01(act.Options.AngelRepeatChance);
                            if (!Roll(p))
                            {
                                // 諦め: 以後のHeal/MentalHealはスキップし、非回復系は続行
                                skipHealsAfterAngelGiveUp = true;
                                break;
                            }

                            if (!SkillResourceFlow.CanConsumeOnCast(self, skill))
                            {
                                Debug.Log($"PostBattleActRun: リソース不足（Angel再試行中）で早期終了 skill={skill.SkillName}");
                                return any;
                            }

                            await act.Target.ApplySkillCoreOutOfBattle(self, skill, policy);
                            any = true;
                        }

                        // DeathHealブロックはここで次のskillへ
                        continue;
                    }

                    // 通常適用
                    await act.Target.ApplySkillCoreOutOfBattle(self, skill, policy);
                    any = true;
                }
            }
            return any;
        }
        catch (Exception e)
        {
            Debug.LogError($"PostBattleActRun: 適用中に例外: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 戦闘後の自己行動用プラン。デフォルトは空（明示的に設定する派生AIのみ動作）。
    /// 例）ヒーラー系AIでは、SelectPostBattleCandidateSkills→SelectMostEffectiveHealSkill で選定し decision.Skill に設定。
    /// </summary>
    protected virtual void PostBattlePlan(BaseStates self, PostBattleDecision decision)
    {
        // 何もしない（デフォルト空実装）
    }

    /// <summary>
    /// 戦闘後自己行動で使用可能な候補（消費資源や武器適合を満たした「回復/付与」系のみ）を抽出。
    /// </summary>
    protected IEnumerable<BaseSkill> SelectPostBattleCandidateSkills(BaseStates self)
    {
        if (self == null) yield break;
        bool isBladeWielder = self.NowUseWeapon != null && self.NowUseWeapon.IsBlade;

        foreach (var s in self.SkillList)
        {
            if (s == null) continue;
            if (!SkillResourceFlow.CanConsumeOnCast(self, s)) continue;
            if (!isBladeWielder && s.IsBlade) continue;

            // 回復・自己強化（付与）系のみ候補にする
            if (s.HasType(SkillType.Heal) || s.HasType(SkillType.MentalHeal) || s.HasType(SkillType.DeathHeal)
                || s.HasType(SkillType.addPassive) || s.HasType(SkillType.AddVitalLayer) || s.HasType(SkillType.addSkillPassive))
            {
                yield return s;
            }
        }
    }

    /// <summary>
    /// 戦闘後適用時のポリシー（デフォルトは OutOfBattleDefault）。派生でゲート/命中等を変更可能。
    /// </summary>
    protected virtual SkillApplyPolicy GetPostBattlePolicy()
    {
        return SkillApplyPolicy.OutOfBattleDefault;
    }
}
