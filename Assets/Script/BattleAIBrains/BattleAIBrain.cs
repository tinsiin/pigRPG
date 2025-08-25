using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using RandomExtensions;
/// <summary>
/// EnemyClass等基礎クラスで関数を共通して扱う 付け替え可能なAI用の抽象クラス
/// </summary>
public abstract class BattleAIBrain : ScriptableObject
{
    /// <summary>
    /// ダメージシミュレートのシミュレート内容
    /// </summary>
    [SerializeField]SkillAnalysisPolicy _damageSimulatePolicy;

    protected BattleManager manager;
    protected BaseStates user;
    protected List<BaseSkill> availableSkills;

    // --- 決定バッファ（各AIは最終結果だけをここに書く）--------------------
    protected class AIDecision
    {
        public BaseSkill Skill;
        public SkillZoneTrait? RangeWill;
        public DirectedWill? TargetWill;
        public List<BaseStates> Targets;

        public bool HasSkill => Skill != null;
        public bool HasRangeWill => RangeWill.HasValue;
        public bool HasTargetWill => TargetWill.HasValue;
        public bool HasTargets => Targets != null && Targets.Count > 0;
        public bool HasAny => HasSkill || HasRangeWill || HasTargetWill || HasTargets;
    }

    // 新スタイル：各AIはPlanに「結果」だけを書く（NowUseSkill等へは直接書かない想定）
    protected virtual void Plan(AIDecision decision)
    {
        // デフォルト実装は何もしない（後方互換：未実装ならThink()へフォールバック）
    }

    // 共通コミット層：単体先約時はSkillのみをコミットし、範囲/対象はBMに委譲
    protected void CommitDecision(AIDecision decision, BaseStates reservedSingleTarget)
    {
        if (decision == null)//結果がnullなら
        {
            manager.DoNothing = true;
            Debug.LogError("BattleAIBrain.CommitDecision: decision が null です");
            return;
        }

        if (reservedSingleTarget != null)//単体先約時
        {
            if (!decision.HasSkill)//結果にスキルがないなら
            {
                manager.DoNothing = true;
                Debug.LogError("BattleAIBrain.CommitDecision: 単体先約時ですが、\ndecision.Skill が null です");
                return;
            }
            user.NowUseSkill = decision.Skill;//スキルのみを反映
            return;
        }

        if (decision.HasSkill) user.NowUseSkill = decision.Skill;
        if (decision.HasRangeWill) user.RangeWill = decision.RangeWill.Value;
        if (decision.HasTargetWill) manager.Acter.Target = decision.TargetWill.Value;
        // d.Targets の具体的な積み先は既存BMフロー依存のためここでは未コミット
    }

    // デフォルトのスキル選定（派生AIでoverride可）
    protected virtual BaseSkill SelectSkill(List<BaseSkill> candidates, List<BaseStates> potentialTargets)
    {
        if (candidates == null || candidates.Count == 0) return null;

        if (potentialTargets != null && potentialTargets.Count > 0)
        {
            var r = AnalyzeBestDamage(candidates, potentialTargets);
            if (r != null && r.Skill != null) return r.Skill;
        }

        return candidates[0];
    }
    /// <summary>
    /// 非virtualの統一入口。ここで共通初期化と強制キャンセル判定を行い、
    /// 強制時はキャンセル行動のみを実行し、そうでなければ既存のThink()へ委譲する。
    /// 呼び出し側は今後 Run() を利用すること。
    /// </summary>
    public void Run()
    {
        // 共通初期化
        manager = Walking.Instance?.bm;
        if (manager == null || manager.Acter == null)
        {
            Debug.LogError("BattleAIBrain.Run: manager または Acter が未設定のため実行を中断します。");
            return;
        }
        user = manager.Acter;

        // 1) 連続実行（Freeze）は最優先で処理し、通常思考へ進まない（強制力）
        if (user.IsFreeze)
        {
            HandleFreezeContinuation();
            return;
        }

        // 2) 使用可能スキル選別（Freezeでない通常ターンのみ）
        availableSkills = MustSkillSelect(user.SkillList.ToList());
        if(availableSkills.Count == 0)
        {
            Debug.Log("BattleAIBrain.Run: availableSkills が空です");
            manager.DoNothing = true;
            return;
        }

        // 3) 強制キャンセル行動限定条件がtrueなら（強制力）
        if (user.HasCanCancelCantACTPassive)
        {
            // キャンセル専用思考（デフォルトは当該パッシブを消して DoNothing）
            OnCancelPassiveThink();
            return;
        }

        // 4) Plan結果のコミットは単体先約の有無で自動分岐（単体時はSkillのみコミット）
        var reserved = manager.Acts.GetAtSingleTarget(0);

        // 5) 新スタイル：Planで結果だけを記述 → Commitで一括反映（単体先約ならSkillのみ）
        {
            var decision = new AIDecision();
            Plan(decision);
            if (decision.HasAny)
            {
                CommitDecision(decision, reserved);
                return;
            }
        }

        Debug.LogError("BattleAIBrain.Run: Plan結果のコミットが行われませんでしたPlanに値が設定されてない");

    }


    /// <summary>
    /// 連続実行（Freeze）中のターンをAI側で強制的に処理する。
    /// 味方側のACTPopに相当する分岐（NowUseSkill/RangeWillの復元）を再現し、
    /// 操作可能（CanOprate）な場合のみAIが範囲/対象を決めるフックを呼ぶ。
    /// </summary>
    protected void HandleFreezeContinuation()
    {
        // プレイヤー側ACTPop相当: 連続実行(Freeze)の打ち切り予約がある場合は即時中止し、このターンは何もしない
        if (user.IsDeleteMyFreezeConsecutive)
        {
            user.DeleteConsecutiveATK();      // 連続実行を破棄
            manager.DoNothing = true;         // このターンは何もしない
            Debug.Log(user.CharacterName + "（AI）は連続実行を中止し、何もしない");
            return;                            // Freeze継続処理には進まない
        }

        var skill = user.FreezeUseSkill;
        if (skill == null)
        {
            Debug.LogError("HandleFreezeContinuation: FreezeUseSkill が null です");
            manager.DoNothing = true;
            return;
        }

        // 強制続行スキルの状態を復元
        user.NowUseSkill = skill;
        user.RangeWill   = user.FreezeRangeWill;

        // 2回目以降かつ操作可能なら、AIで範囲/対象を決め直す
        if (skill.NowConsecutiveATKFromTheSecondTimeOnward()
            && skill.HasConsecutiveType(SkillConsecutiveType.CanOprate))
        {
            OnFreezeOperate(skill);
        }
        // 操作不可の場合は何もせずBMの後段フロー（SelectTargetFromWill→SkillACT）に委譲
    }

    /// <summary>
    /// 凍結中スキルが操作可能な場合に、AIが範囲/対象を決め直すためのフック。
    /// デフォルト実装は「完全単体」のみ簡易的にランダム単体を事前予約する。
    /// 必要に応じて各キャラAIでoverrideして、前のめり/後衛や群選択などを実装する。
    /// </summary>
    /// <param name="skill">凍結中の再実行スキル</param>
    protected virtual void OnFreezeOperate(BaseSkill skill)
    {
        //CanOprateなスキルで呼ばれるので、
        //範囲選択出来たり、対象者選択だけをここでする。(Skillの選択はしない)
        // 主人公キャラでいうPlayersStates.DetermineNextUIStateの部分なのでこれを参考にして。
    }

    /// <summary>
    /// 強制キャンセル行動しかとれない場合の思考、特殊な敵はここをoverrideして振る舞いを変更可能。
    /// </summary>
    protected virtual void OnCancelPassiveThink()
    {
        //デフォルトでは悪いパッシブのみを消す
        CancelPassive(RandomSelectCanCancelPassiveOnlyBadPassives(SelectCancelableCantActPassives()));
    }


    // =============================================================================
    // 思考部品のパーツ　
    // 以下の関数は各コールバック内で組み合わせることで　キャラごとの仕様を再現します(主に派生クラスで)
    // =============================================================================


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
        return user.PassiveList.Where(p => p != null && p.IsCantACT && p.CanCancel).ToList();
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
        return RandomEx.Shared.GetItem(badPassives.ToArray());//ランダムに一つ入手
    }


    /// <summary>
    /// 使用可能スキルの選別
    /// 主人公キャラ(PlayersStates)でいうCanCastNow/ZoneTraitAndTypeSkillMatchesUIFilterと同じ部分
    /// </summary>
    /// <param name="availableSkills"></param>
    /// <returns></returns>
    protected List<BaseSkill> MustSkillSelect(List<BaseSkill> availableSkills)
    {
        // 入力が空ならエラーログを出してそのまま返す
        if (availableSkills == null || availableSkills.Count == 0)
        {
            Debug.LogError("MustSkillSelect: availableSkills が null または空です");
            return availableSkills;
        }

        // manager/Acterが未設定ならフィルタ不能のためそのまま返す
        var acter = (manager != null) ? manager.Acter : null;
        if (acter == null)
        {
            Debug.LogError("MustSkillSelect: manager または Acter が未設定のためフィルタ処理を実行できません");
            return availableSkills;
        }

        // 刃物武器の所持判定（NowUseWeaponがnullの場合も考慮）
        bool isBladeWielder = acter.NowUseWeapon != null && acter.NowUseWeapon.IsBlade;

        // プレイヤー側と同一の条件でフィルタ
        // 1) SkillResourceFlow.CanConsumeOnCast(acter, skill)
        // 2) (acter.NowUseWeapon.IsBlade || !skill.IsBlade)
        // 3) Actsに単体指定がある場合は単体系ZoneTrait + 攻撃タイプに限定
        bool hasSingleReservation = false;
        try
        {
            hasSingleReservation = manager?.Acts?.GetAtSingleTarget(0) != null;
        }
        catch (Exception e)
        {
            Debug.LogError($"MustSkillSelect: Acts.GetAtSingleTarget(0) 参照時に例外: {e.Message}");
        }

        var filtered = availableSkills
            .Where(skill =>
                skill != null &&
                SkillResourceFlow.CanConsumeOnCast(acter, skill) &&
                (isBladeWielder || !skill.IsBlade) &&
                (
                    !hasSingleReservation
                    || (
                        // 単体先約がある場合の範囲・タイプ制限
                        skill.IsEligibleForSingleTargetReservation()
                    )
                )
            )
            .ToList();

        return filtered;
    }


    // ---------- ベストダメージ分析関連----------------------------------

    /// <summary>
    /// 利用可能なスキルの中から最もダメージを与えられるスキルとターゲットを分析する
    ///  敵グループに対してどのスキルが最もダメージを与えられるかを分析する
    /// </summary>
    protected BruteForceResult AnalyzeBestDamage(List<BaseSkill> availableSkills, List<BaseStates> potentialTargets)
    {
        if(potentialTargets.Count == 0) 
        {
            Debug.LogError("有効スキル分析の関数に渡されたターゲットが存在しません");
            return null;
        }
        if(availableSkills.Count == 0)
        {
            Debug.LogError("有効スキル分析の関数に渡されたスキルが存在しません");
            return null;
        }
        if(availableSkills.Count == 1)
        {
            Debug.LogWarning("有効スキル分析の関数に渡されたスキルが1つしかないため、最適なスキルを分析する必要はありません");
            return null;
        }
        BaseStates ResultTarget = null;
        BaseSkill ResultSkill = null;

        if(potentialTargets.Count == 1)//ターゲットが一人ならそのまま単体スキルの探索
        {
            ResultTarget = potentialTargets[0];
            ResultSkill = SingleBestDamageAnalyzer(availableSkills, ResultTarget);
        }

        //敵グループのHPに対して有効なスキルを分析する
        if(_damageSimulatePolicy.groupType == TargetGroupType.Group)
        {
            return MultiBestDamageAndTargetAnalyzer(availableSkills, potentialTargets);
        }
        //敵単体に対して有効なスキルを分析する
        if(_damageSimulatePolicy.groupType == TargetGroupType.Single)
        {
            if(_damageSimulatePolicy.hpType == TargetHPType.Highest)//グループの中で最もHPが高い
            {
                ResultTarget = potentialTargets.OrderByDescending(x => x.HP).First();
            }
            if(_damageSimulatePolicy.hpType == TargetHPType.Lowest)//グループの中で最もHPが低い
            {
                ResultTarget = potentialTargets.OrderBy(x => x.HP).First();
            }
            if(_damageSimulatePolicy.hpType == TargetHPType.Random)//グループから一人をランダム
            {
                ResultTarget = RandomEx.Shared.GetItem(potentialTargets.ToArray());
            }
            ResultSkill = SingleBestDamageAnalyzer(availableSkills, ResultTarget);
        }

        foreach(var skill in availableSkills)
        {
            foreach(var target in potentialTargets)
            {
                var damage = target.SimulateDamage(manager.Acter, skill, _damageSimulatePolicy);
            }
        }
        return new BruteForceResult
        {
            Skill = ResultSkill,
            Target = ResultTarget
        };
    }
    /// <summary>
    /// 単体用有効スキル分析ダメージ由来を選考する関数
    /// </summary>
    /// <param name="availableSkills"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    BaseSkill SingleBestDamageAnalyzer(List<BaseSkill> availableSkills, BaseStates target)
    {
        if(availableSkills.Count == 0)
        {
            Debug.LogError("有効スキル単体ターゲット分析の関数に渡されたスキルが存在しません");
            return null;
        }

        var potential = -3f;
        BaseSkill ResultSkill = null;
        foreach(var skill in availableSkills)
        {
            var damage = target.SimulateDamage(manager.Acter, skill, _damageSimulatePolicy);
            if(damage > potential)//与えたダメージが多ければ
            {
                potential = damage;//基準を更新
                ResultSkill = skill;//結果を更新
            }
        }
        return ResultSkill;
    }   
    /// <summary>
    /// グループに対して最大ダメージを与えるスキルとターゲットの組み合わせを分析する
    /// </summary>
    /// <param name="availableSkills">使用可能なスキルリスト</param>
    /// <param name="potentialTargets">攻撃対象候補リスト</param>
    /// <returns>最大ダメージを与えるスキルとターゲットの組み合わせ</returns>
    BruteForceResult MultiBestDamageAndTargetAnalyzer(List<BaseSkill> availableSkills, List<BaseStates> potentialTargets)
    {
        if(availableSkills.Count == 0)
        {
            Debug.LogError("グループ分析の関数に渡されたスキルが存在しません");
            return null;
        }
        if(potentialTargets.Count == 0)
        {
            Debug.LogError("グループ分析の関数に渡されたターゲットが存在しません");
            return null;
        }

        var maxDamage = -3f;
        BaseSkill bestSkill = null;
        BaseStates bestTarget = null;

        // 全スキル × 全ターゲットの組み合わせを総当たりで分析
        foreach(var skill in availableSkills)
        {
            foreach(var target in potentialTargets)
            {
                var damage = target.SimulateDamage(manager.Acter, skill, _damageSimulatePolicy);
                if(damage > maxDamage)
                {
                    maxDamage = damage;
                    bestSkill = skill;
                    bestTarget = target;
                }
            }
        }

        return new BruteForceResult
        {
            Skill = bestSkill,
            Target = bestTarget
        };
    }

}

public class BruteForceResult
{
    public BaseSkill Skill;
    public BaseStates Target;
}
public enum TargetGroupType
{
    Single,
    Group
}
public enum SimulateDamageType
{
    dmg,
    mentalDmg,
}

public enum TargetHPType
{
    Highest,
    Lowest,
    Random
}


[Serializable]
public struct SkillAnalysisPolicy
{
    public TargetGroupType groupType; // 単体 or グループ
    public TargetHPType hpType;       // HP最大/最小/任意
    public SimulateDamageType damageType;
    public bool spiritualModifier;//精神補正
    public bool physicalResistance;//物理耐性
    public bool SimlateVitalLayerPenetration;//追加HP
    public bool SimlateEnemyDEF;//敵のDEF
}

//各キャラ用AIはScriptableObejctとして扱うため、UnityのSOを継承するpublicクラスは、
//ファイル名 = クラス名でなくてはならないというUnityの仕様により、別ファイルに分ける。

