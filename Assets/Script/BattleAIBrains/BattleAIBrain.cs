using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using RandomExtensions;
using NRandom.Collections;
/// <summary>
/// EnemyClass等基礎クラスで関数を共通して扱う 付け替え可能なAI用の抽象クラス
/// </summary>
public abstract class BattleAIBrain : ScriptableObject
{
    [Header("基本の最大ダメージスキルを決めるためのシミュレーション用設定\n必ずしも最大ダメージスキルを思考しないキャラならば、設定する必要はない")]
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
        public bool IsEscape;

        public bool HasSkill => Skill != null;
        public bool HasRangeWill => RangeWill.HasValue;
        public bool HasTargetWill => TargetWill.HasValue;
        public bool HasTargets => Targets != null && Targets.Count > 0;
        public bool HasAny => HasSkill || HasRangeWill || HasTargetWill || HasTargets;
    }

    // Inspector 変更時にポリシーの不正値を検証・矯正
    private void OnValidate()
    {
        _damageSimulatePolicy.ValidateAndClamp("BattleAIBrain.OnValidate");
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

        if(decision.IsEscape)//逃走するならスキル使用の理由がないので
        {
            user.SelectedEscape = true;
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
            user.SKillUseCall(decision.Skill);//スキルのみを反映
            return;
        }

        if (decision.HasSkill) user.SKillUseCall(decision.Skill);
        if (decision.HasRangeWill) user.RangeWill = decision.RangeWill.Value;
        if (decision.HasTargetWill) manager.Acter.Target = decision.TargetWill.Value;
        // d.Targets(直接的な対象者) の具体的な積み先は既存BMフロー依存のためここでは未コミット
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
        // 1) SkillResourceFlow.CanConsumeOnCast(acter, skill)　　ポインントによる消費可能可否判定
        // 2) (acter.NowUseWeapon.IsBlade || !skill.IsBlade)　　刃物武器と刃物スキルの嚙み合わせ
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

    /// <summary>
    /// スキル思考部分
    /// 非virtualの統一入口。ここで共通初期化と強制キャンセル判定を行い、
    /// 強制時はキャンセル行動のみを実行し、そうでなければ既存のThink()へ委譲する。
    /// 呼び出し側は今後 Run() を利用すること。
    /// </summary>
    public void SkillActRun()
    {
        // 共通初期化
        manager = Walking.Instance?.bm;
        if (manager == null || manager.Acter == null)
        {
            Debug.LogError("BattleAIBrain.Run: manager または Acter が未設定のため実行を中断します。");
            return;
        }
        user = manager.Acter;

        // ポリシーの不正値を実行前に検証・矯正（0以下はエラーを出して1に補正）
        _damageSimulatePolicy.ValidateAndClamp("BattleAIBrain.Run");

        // 1) 連続実行（Freeze）は最優先で処理し、通常思考へ進まない（強制力）
        if (user.IsFreeze)
        {
            HandleFreezeContinuation();
            return;
        }

        // 2) 強制キャンセル行動限定条件がtrueなら（強制力）
        if (user.HasCanCancelCantACTPassive)
        {
            // キャンセル専用思考（デフォルトは当該パッシブを消して DoNothing）
            OnCancelPassiveThink();
            return;
        }


        // 3) 使用可能スキル選別（Freezeでない通常ターンのみ）
        availableSkills = MustSkillSelect(user.SkillList.ToList());
        if(availableSkills.Count == 0)
        {
            Debug.Log("BattleAIBrain.Run: availableSkills が空です");
            manager.DoNothing = true;
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


    // =============================================================================
    // コールバック　行動者の状態により取れる行動が異なる違いをコールバックで表現。
    // 思考部品をこれらのコールバック内で組み合わせ、キャラごとのAIを作る。
    // =============================================================================



    // 各AIはPlanに「結果」だけを書く（NowUseSkill等へは直接書かない想定）
    protected virtual void Plan(AIDecision decision)
    {
        // デフォルト実装は何もしない（後方互換：未実装ならThink()へフォールバック）
    }


    /// <summary>
    /// 凍結中スキルが操作可能な場合に、AIが範囲/対象を決め直すためのフック。
    /// デフォルト実装は存在しない  「CanOparete」な連続スキルを持っている場合、このフックが呼ばれる可能性は常にあるので、
    /// これを実装する必要がある。
    /// CanOprateなスキルで呼ばれるので、
    /// 範囲選択出来たり、対象者選択だけをここでする。(Skillの選択はしない)
    /// 主人公キャラでいうPlayersStates.DetermineNextUIStateの部分なのでこれを参考にして。
    /// </summary>
    /// <param name="skill">凍結中の再実行スキル</param>
    protected virtual void OnFreezeOperate(BaseSkill skill)
    {
        //デフォルトでは何もしないがabstractにするとわざわざ必ず派生で実装する必要があるので、
        //virtualにしておく。
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
        return RandomEx.Shared.GetItem(badPassives.ToArray());//ランダムに一つ入手
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

        // 刻み・ブレ段階が無効な場合は従来のロジック
        if(_damageSimulatePolicy.damageStep <= 1 && _damageSimulatePolicy.variationStages <= 1)
        {
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

        // 新しいロジック：刻み・ブレ段階システムを使用
        return DamageStepAnalysisHelper.SelectWithStepAndVariation(
            availableSkills, 
            skill => target.SimulateDamage(manager.Acter, skill, _damageSimulatePolicy), 
            _damageSimulatePolicy
        );
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

        // 刻み・ブレ段階が無効な場合は従来のロジック
        if(_damageSimulatePolicy.damageStep <= 1 && _damageSimulatePolicy.variationStages <= 1)
        {
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

        // 新しいロジック：全組み合わせを生成してから刻み・ブレ段階システムで選択
        var combinations = new List<BruteForceResult>();
        foreach(var skill in availableSkills)
        {
            foreach(var target in potentialTargets)
            {
                var damage = target.SimulateDamage(manager.Acter, skill, _damageSimulatePolicy);
                combinations.Add(new BruteForceResult
                {
                    Skill = skill,
                    Target = target,
                    Damage = damage
                });
            }
        }

        // 刻み・ブレ段階システムで最適な組み合わせを選択
        var selectedCombination = DamageStepAnalysisHelper.SelectWithStepAndVariation(
            combinations, 
            combination => combination.Damage, 
            _damageSimulatePolicy
        );

        return selectedCombination ?? combinations.First();
    }

}

public class BruteForceResult
{
    public BaseSkill Skill;
    public BaseStates Target;
    public float Damage; // 刻み・ブレ段階システム用に追加
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
    [Header("基本設定")]
    public TargetGroupType groupType; // 単体 or グループ
    public TargetHPType hpType;       // HP最大/最小/任意
    public SimulateDamageType damageType;
    [Header("ダメージシミュレーションの追加考慮要素")]
    public bool spiritualModifier;//精神補正
    public bool physicalResistance;//物理耐性
    public bool SimlateVitalLayerPenetration;//追加HP
    public bool SimlateEnemyDEF ;//敵のDEF
    [Header("完全DEFシミュレーションbool SimlateNemeyDEFがオンの場合の追加オプション。falseならb_b_defと共通十日能力による防御力のみによるシミュレート")]
    public bool SimlatePerfectEnemyDEF;//完全DEFシミュレート（パッシブ・AimStyle排他含む）
    
    [Header("ダメージ刻み・ブレ段階設定")]
    [Tooltip("ダメージを指定の刻み幅で下方向に丸めて評価します。\n例) step=10, 生ダメージ=237 → 230 として扱う。\n1 を指定すると刻み無し(従来通りの生ダメージ評価)。\n推奨: ダメージスケールに応じて 5〜50 程度。小さすぎると従来に近く、\n大きすぎると候補が粗くなります。")]
    [Min(1)]
    public int damageStep;              // ダメージ刻み（1=刻み無効、10=10刻み）
    [Tooltip("最大の刻みダメージから何段下までを候補に含めるか(=揺らぎの幅)。\n例) max=230, step=10, stages=3 → {230,220,210} の刻み値を候補に含める。\n1 を指定すると最大段のみ(従来の最大一本釣りに近い)。\n理論上は上限なしですが、実運用では 2〜5 程度が推奨。\n大きくするほど上位以外も混ざりやすくなり、行動のバリエーションが増えます。")]
    [Min(1)]
    public int variationStages;         // ブレ段階（1=最大のみ、4=上位4段階など）
    [Header("最大優先オプション ブレ段階で含まれる序列候補を重み付けして選択するか")]
    public bool useWeightedSelection;   // 重み付き抽選を使用するか

    /// <summary>
    /// 不正な設定値（0以下）を検知してエラーログを出し、1にクランプする
    /// </summary>
    public void ValidateAndClamp(string context = null)
    {
        if (damageStep <= 0)
        {
            Debug.LogError($"[{context}] SkillAnalysisPolicy.damageStep は1以上が必要です (現在値={damageStep})。1に補正します。");
            damageStep = 1;
        }
        if (variationStages <= 0)
        {
            Debug.LogError($"[{context}] SkillAnalysisPolicy.variationStages は1以上が必要です (現在値={variationStages})。1に補正します。");
            variationStages = 1;
        }
    }
}

//各キャラ用AIはScriptableObejctとして扱うため、UnityのSOを継承するpublicクラスは、
//ファイル名 = クラス名でなくてはならないというUnityの仕様により、別ファイルに分ける。

/// <summary>
/// ダメージ刻み・ブレ段階システムのヘルパークラス
/// </summary>
public static class DamageStepAnalysisHelper
{
    /// <summary>
    /// スキル候補から刻み・ブレ段階システムに基づいて最適なスキルを選択
    /// </summary>
    public static T SelectWithStepAndVariation<T>(List<T> candidates, System.Func<T, float> getDamage, SkillAnalysisPolicy policy)
    {
        if (candidates == null || candidates.Count == 0) return default(T);
        if (candidates.Count == 1) return candidates[0];

        // 1. 各候補のダメージを刻み値でまとめる
        var steppedCandidates = candidates.Select(candidate => new CandidateInfo<T>
        {
            Original = candidate,
            Damage = getDamage(candidate),
            SteppedDamage = StepDamage(getDamage(candidate), policy.damageStep)
        }).ToList();

        // 2. 最大刻みダメージを取得
        float maxSteppedDamage = steppedCandidates.Max(x => x.SteppedDamage);

        // 3. ブレ段階に基づいて候補範囲を決定
        var validSteppedValues = new List<float>();
        for (int i = 0; i < policy.variationStages; i++)
        {
            float targetValue = maxSteppedDamage - (i * policy.damageStep);
            if (targetValue >= 0) // 負の値は除外
            {
                validSteppedValues.Add(targetValue);
            }
        }

        // 4. 有効な刻み値に該当する候補を抽出
        var validCandidates = steppedCandidates
            .Where(x => validSteppedValues.Contains(x.SteppedDamage))
            .ToList();

        if (validCandidates.Count == 0) return candidates[0]; // フォールバック

        // 5. 重み付き抽選または均等抽選で選択
        if (policy.useWeightedSelection && validSteppedValues.Count > 1)
        {
            return SelectWithWeights(validCandidates, validSteppedValues, maxSteppedDamage, policy.damageStep);
        }
        else
        {
            // 均等抽選
            return RandomEx.Shared.GetItem(validCandidates.ToArray()).Original;
        }
    }

    /// <summary>
    /// ダメージを指定の刻み値で丸める（下方向）
    /// </summary>
    private static float StepDamage(float damage, int step)
    {
        if (step <= 1) return damage; // step=1なら刻まない
        return Mathf.Floor(damage / step) * step;
    }

    /// <summary>
    /// 重み付き抽選による候補選択（NRandomのWeightedListを使用）
    /// </summary>
    private static T SelectWithWeights<T>(List<CandidateInfo<T>> candidates, List<float> validSteppedValues, float maxSteppedDamage, int step)
    {
        // 刻み値ごとにグループ化
        var groups = candidates.GroupBy(x => x.SteppedDamage).ToList();
        
        // WeightedListを作成して各グループに重みを設定
        var weightedGroups = new WeightedList<List<CandidateInfo<T>>>();
        
        foreach (var group in groups.OrderByDescending(g => g.Key)) // 降順でソート
        {
            int stepsFromMax = Mathf.RoundToInt((maxSteppedDamage - group.Key) / step);
            float weight = Mathf.Max(0.1f, 1.0f - (stepsFromMax * 0.3f)); // 上位ほど高い重み
            
            weightedGroups.Add(group.ToList(), weight);
        }
        
        // NRandomのWeightedListで重み付き抽選
        var selectedGroup = weightedGroups.GetRandom();
        
        // 選択されたグループ内からランダムで1つ選択
        return RandomEx.Shared.GetItem(selectedGroup.ToArray()).Original;
    }
    
    /// <summary>
    /// 候補情報を格納する構造体
    /// </summary>
    private struct CandidateInfo<T>
    {
        public T Original;
        public float Damage;
        public float SteppedDamage;
    }

}

