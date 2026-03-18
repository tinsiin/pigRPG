using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using UnityEngine.Serialization;

/// <summary>
/// EnemyClass等基礎クラスで関数を共通して扱う 付け替え可能なAI用の抽象クラス
/// </summary>
public abstract partial class BattleAIBrain : ScriptableObject
{
    [Header("基本の最大ダメージスキルを決めるためのシミュレーション用設定\n必ずしも最大ダメージスキルを思考しないキャラならば、設定する必要はない")]
    /// <summary>
    /// ダメージシミュレートのシミュレート内容
    /// </summary>
    [SerializeField]SkillAnalysisPolicy _damageSimulatePolicy;

    [Header("AI思考ログ")]
    [Tooltip("0=最終結果のみ, 1=候補一覧, 2=スコア詳細, 3=全試算")]
    [SerializeField, Range(0, 3)] private int _thinkLogLevel = 0;

    protected IBattleContext manager;
    private IBattleContext _battleContext;
    private IBattleRandom _random = new SystemBattleRandom();
    protected IBattleRandom RandomSource => manager?.Random ?? _battleContext?.Random ?? _random;
    protected BaseStates user;
    protected List<BaseSkill> availableSkills;

#if UNITY_EDITOR
    // SO共有汚染検出用: 前回Run()を呼んだキャラを記録
    [NonSerialized] private BaseStates _lastRunActer;
#endif

    // --- 決定バッファ（各AIは最終結果だけをここに書く）--------------------
    protected class AIDecision
    {
        public BaseSkill Skill;
        public SkillZoneTrait? RangeWill;
        public DirectedWill? TargetWill;
        public bool IsEscape;
        public bool IsStock; // trueならSkillフィールドをストック対象として扱う

        public bool HasSkill => Skill != null;
        public bool HasRangeWill => RangeWill.HasValue;
        public bool HasTargetWill => TargetWill.HasValue;
        // HasAnyにIsStockは含めない（IsStock=trueなら必ずSkillもセットされるためHasSkillで十分）
        public bool HasAny => HasSkill || HasRangeWill || HasTargetWill || IsEscape;
    }

    // Inspector 変更時にポリシーの不正値を検証・矯正
    private void OnValidate()
    {
        _damageSimulatePolicy.ValidateAndClamp("BattleAIBrain.OnValidate");
    }


    // =============================================================================================================================
    // スキルAI部分
    //PlanでAIDecisionに明示的に派生クラスで答えを渡し　それが実行される
    //それ以外にもいろいろあるから、obsidian の 敵思考AI.md見といて
    // =============================================================================================================================


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

        // ストック行動（SKillUseCallは使わない。ポイント消費・CashMoveSet・ForgetStock二重実行の副作用があるため）
        if (decision.IsStock && decision.HasSkill)
        {
            if (decision.Skill.IsFullStock())
            {
                manager.DoNothing = true; // 満杯なら何もしない（ターン浪費を防ぐ）
                return;
            }
            user.NowUseSkill = decision.Skill; // 直接代入
            manager.SkillStock = true;
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

        if (!decision.HasSkill)
        {
            // スキル未設定で到達した異常パス（RangeWill/TargetWillのみ、IsStockのみ等）
            manager.DoNothing = true;
            Debug.LogError("BattleAIBrain.CommitDecision: 通常パスですが Skill が設定されていません");
            return;
        }

        user.SKillUseCall(decision.Skill);
        // AI決定の範囲意志を正規化してから適用（競合解消）
        // ※プレイヤー側はUI段階的選択のためAdd(OR)だが、AIは一括決定のため置換で正しい
        //   ターン開始時にRangeWill=0にリセット済みなので実質的な差異はない
        if (decision.HasRangeWill) user.RangeWill = SkillZoneTraitNormalizer.Normalize(decision.RangeWill.Value);
        if (decision.HasTargetWill) manager.Acter.Target = decision.TargetWill.Value;
        // d.Targets(直接的な対象者) の具体的な積み先は既存BMフロー依存のためここでは未コミット
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
        var hasSingleReservation = manager != null
            && manager.Acts.TryPeek(out var entry)
            && entry.SingleTarget != null;

        var filtered = availableSkills
            .Where(skill =>
                skill != null &&
                SkillResourceFlow.CanCastSkill(acter, skill) &&
                (
                    !hasSingleReservation
                    || (
                        // 単体先約がある場合の範囲・タイプ制限
                        skill.IsEligibleForSingleTargetReservation()
                    )
                )
            )
            .ToList();

        LogThink(2, $"MustSkillSelect: {availableSkills.Count}件 → {filtered.Count}件");
        if (filtered.Count < availableSkills.Count)
        {
            LogThink(3, () =>
            {
                var removed = availableSkills.Where(s => s != null && !filtered.Contains(s)).Select(s => s.SkillName);
                return $"MustSkillSelect 除外: [{string.Join(", ", removed)}]";
            });
        }

        return filtered;
    }

    /// <summary>
    /// スキル思考部分
    /// 非virtualの統一入口。ここで共通初期化と強制キャンセル判定を行い、
    /// 強制時はキャンセル行動のみを実行し、そうでなければ既存のThink()へ委譲する。
    /// 呼び出し側は今後 Run() を利用すること。
    /// </summary>
    public void SkillActRun(IBattleContext context = null)
    {
        // 共通初期化
        manager = context ?? _battleContext ?? BattleContextHub.Current;
        if (manager == null || manager.Acter == null)
        {
            Debug.LogError("BattleAIBrain.Run: manager または Acter が未設定のため実行を中断します。");
            return;
        }
        _random = manager.Random ?? _random;

#if UNITY_EDITOR
        // SO共有汚染検出: 前回の実行主体と異なる場合に警告（同一SOを複数敵が共有している兆候）
        if (_lastRunActer != null && _lastRunActer != manager.Acter)
            Debug.LogWarning($"[AI汚染検出] 同一BrainSOが複数キャラで共有されています 前回={_lastRunActer.CharacterName} 今回={manager.Acter.CharacterName}");
        _lastRunActer = manager.Acter;
#endif

        user = manager.Acter;

        // ポリシーの不正値を実行前に検証・矯正（0以下はエラーを出して1に補正）
        _damageSimulatePolicy.ValidateAndClamp("BattleAIBrain.Run");

        // 1) 連続実行（Freeze）は最優先で処理し、通常思考へ進まない（強制力）
        if (user.IsFreeze)
        {
            LogThink(0, "Freeze継続");
            HandleFreezeContinuation();
            SnapshotHPAfterDecision();
            return;
        }

        // 2) 強制キャンセル行動限定条件がtrueなら（強制力）
        if (user.HasCanCancelCantACTPassive)
        {
            LogThink(0, "キャンセル行動（CantACTパッシブ）");
            OnCancelPassiveThink();
            SnapshotHPAfterDecision();
            return;
        }


        // 3) 使用可能スキル選別（Freezeでない通常ターンのみ）
        availableSkills = MustSkillSelect(user.SkillList.ToList());

        // 3b) イラつき攻撃時: 攻撃系スキルのみに制限
        if (user.IsIrritationAttack && user.IrritationForcedTarget != null)
        {
            availableSkills = availableSkills.Where(s => s.HasType(SkillType.Attack)).ToList();
            LogThink(0, $"イラつき攻撃: 攻撃系スキルのみに制限（残り{availableSkills.Count}）");
        }

        if(availableSkills.Count == 0)
        {
            LogThink(0, "使用可能スキルなし → DoNothing");
            manager.DoNothing = true;
            SnapshotHPAfterDecision();
            return;
        }


        // 4) Plan結果のコミットは単体先約の有無で自動分岐（単体時はSkillのみコミット）
        var reserved = manager.Acts.TryPeek(out var entry) ? entry.SingleTarget : null;

        // 5) 新スタイル：Planで結果だけを記述 → Commitで一括反映（単体先約ならSkillのみ）
        {
            LogThink(1, () => $"候補スキル数={availableSkills.Count}: [{string.Join(", ", availableSkills.Select(s => s.SkillName))}]");

            var decision = new AIDecision();
            Plan(decision);
            if (decision.HasAny)
            {
                LogThink(0, $"決定: {(decision.IsEscape ? "逃走" : decision.IsStock ? $"ストック({decision.Skill?.SkillName})" : decision.Skill?.SkillName ?? "???")}");
                CommitDecision(decision, reserved);
                // AI戦闘記憶: 行動記録
                user.AIMemory?.RecordAction(new ActionRecord
                {
                    Skill = decision.Skill,
                    Targets = null, // ターゲットはBM側で最終決定されるためここでは未確定（PatchLastActionTargetsで補填）
                    Turn = TurnCount,
                    WasEscape = decision.IsEscape,
                });
                SnapshotHPAfterDecision();
                return;
            }
        }

        LogThink(0, "Plan結果なし → DoNothing");
        Debug.LogError("BattleAIBrain.Run: Plan結果のコミットが行われませんでしたPlanに値が設定されてない");
        manager.DoNothing = true;
        SnapshotHPAfterDecision();
    }

    /// <summary>
    /// 思考完了後にHPスナップショットを記録する。
    /// 次ターンのHpDropRate計算で「前ターン思考時のHP」として参照される。
    /// </summary>
    private void SnapshotHPAfterDecision()
    {
        user?.AIMemory?.SnapshotHP(user.HP, user.MentalHP);
    }


    /// <summary>
    /// 連続実行（Freeze）中のターンをAI側で処理する。
    /// スキル復元は BaseStates.ResumeFreezeSkill() に統一。
    /// 操作可能（CanOprate）な場合のみAIが範囲/対象を決めるフックを呼ぶ。
    /// </summary>
    protected void HandleFreezeContinuation()
    {
        // Freeze中断判断フック（派生AIでoverride可能）
        if (ShouldAbortFreeze())
        {
            LogThink(0, "Freeze中断を選択");
            user.DeleteConsecutiveATK();
            manager.DoNothing = true;
            return;
        }

        var result = user.ResumeFreezeSkill();

        if (result == FreezeResumeResult.Cancelled)
        {
            manager.DoNothing = true;
            return;
        }

        if (result == FreezeResumeResult.ResumedCanOperate)
        {
            OnFreezeOperate(user.NowUseSkill);
        }
        // Resumed: 操作不可の場合は何もせずBMの後段フロー（SelectTargetFromWill→SkillACT）に委譲
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
    /// Freeze継続中に中断するかどうかの判断フック。
    /// ResumeFreezeSkillの前に呼ばれる。派生AIでoverrideして条件を設定できる。
    /// trueを返すとFreeze強制解除 → DoNothing。
    /// </summary>
    protected virtual bool ShouldAbortFreeze() => false;



    /// <summary>
    /// 強制キャンセル行動しかとれない場合の思考、特殊な敵はここをoverrideして振る舞いを変更可能。
    /// </summary>
    protected virtual void OnCancelPassiveThink()
    {
        //デフォルトでは悪いパッシブのみを消す
        CancelPassive(RandomSelectCanCancelPassiveOnlyBadPassives(SelectCancelableCantActPassives()));
    }


    // =============================================================================
    // 共通ユーティリティ
    // =============================================================================

    public void BindBattleContext(IBattleContext context)
    {
        _battleContext = context;
        _random = context?.Random ?? _random;
    }

    protected bool Roll(float p01)
    {
        var p = Mathf.Clamp01(p01);
        return RandomSource.NextFloat() < p;
    }

    // ── AI思考ログ ─────────────────────────────────────────────
    // level: 0=最終結果, 1=候補一覧, 2=スコア詳細, 3=全試算
    protected void LogThink(int level, string message)
    {
        if (level > _thinkLogLevel) return;
        var charName = user?.CharacterName ?? "???";
        var turn = manager?.BattleTurnCount.ToString() ?? "?";
        Debug.Log($"[AI:{charName}][T{turn}] {message}");
    }

    /// <summary>
    /// 高コスト文字列生成を遅延評価するオーバーロード。
    /// レベルチェック通過時のみ messageFactory を呼び出す。
    /// ループ内や LINQ/string.Join を含む呼び出しで使用すること。
    /// </summary>
    protected void LogThink(int level, Func<string> messageFactory)
    {
        if (level > _thinkLogLevel) return;
        var charName = user?.CharacterName ?? "???";
        var turn = manager?.BattleTurnCount.ToString() ?? "?";
        Debug.Log($"[AI:{charName}][T{turn}] {messageFactory()}");
    }

    // グループ味方列挙ユーティリティ（self は除外）
    protected IEnumerable<BaseStates> EnumerateGroupAllies(BaseStates self, bool includeDead)
    {
        var group = manager?.MyGroup(self);
        if (group == null || group.Ours == null) yield break;
        foreach (var m in group.Ours)
        {
            if (m == null || m == self) continue;
            if (!includeDead && m.Death()) continue;
            yield return m;
        }
    }

    // ── 自キャラ状態ヘルパー ──

    protected float HpRatio => user != null && user.MaxHP > 0 ? user.HP / user.MaxHP : 0f;
    protected float MentalHpRatio => user != null && user.MentalMaxHP > 0 ? user.MentalHP / user.MentalMaxHP : 0f;
    protected bool IsLowHP(float threshold = 0.25f) => HpRatio < threshold;

    protected int TurnCount => manager?.BattleTurnCount ?? 0;

    /// <summary>
    /// 敵グループ列挙（AIから見た攻撃対象）
    /// </summary>
    protected List<BaseStates> GetPotentialTargets(bool includeDeadTargets = false)
    {
        if (manager == null || user == null) return new List<BaseStates>();
        var myFaction = manager.GetCharacterFaction(user);
        var opponentFaction = myFaction == Faction.Ally ? Faction.Enemy : Faction.Ally;
        var opponentGroup = manager.FactionToGroup(opponentFaction);
        if (opponentGroup?.Ours == null) return new List<BaseStates>();
        return opponentGroup.Ours
            .Where(t => t != null && (includeDeadTargets || !t.Death()))
            .ToList();
    }

    /// <summary>
    /// スキル名検索ヘルパー
    /// </summary>
    protected BaseSkill FindSkill(string name)
        => availableSkills?.FirstOrDefault(s => s != null && s.SkillName == name);
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
    [FormerlySerializedAs("SimlateVitalLayerPenetration")]
    public bool SimulateVitalLayerPenetration;//追加HP
    [FormerlySerializedAs("SimlateEnemyDEF")]
    public bool SimulateEnemyDEF;//敵のDEF
    [Header("完全DEFシミュレーション SimulateEnemyDEFがオンの場合の追加オプション。falseならb_b_defと共通十日能力による防御力のみによるシミュレート")]
    [FormerlySerializedAs("SimlatePerfectEnemyDEF")]
    public bool SimulatePerfectEnemyDEF;//完全DEFシミュレート（パッシブ・AimStyle排他含む）

    [Header("命中率シミュレーション")]
    [Tooltip("trueの場合、期待ダメージ(ダメージ x 命中率)で評価する")]
    public bool useExpectedDamage;
    [Tooltip("前のめり状態を命中率計算に反映するか")]
    public bool considerVanguardForHit;

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
    public static T SelectWithStepAndVariation<T>(List<T> candidates, System.Func<T, float> getDamage, SkillAnalysisPolicy policy, IBattleRandom random)
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
            return SelectWithWeights(validCandidates, validSteppedValues, maxSteppedDamage, policy.damageStep, random);
        }
        else
        {
            // 均等抽選
            return random.GetItem(validCandidates).Original;
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
    /// 重み付き抽選による候補選択
    /// </summary>
    private static T SelectWithWeights<T>(List<CandidateInfo<T>> candidates, List<float> validSteppedValues, float maxSteppedDamage, int step, IBattleRandom random)
    {
        // 刻み値ごとにグループ化
        var groups = candidates
            .GroupBy(x => x.SteppedDamage)
            .OrderByDescending(g => g.Key)
            .ToList();

        var weightedGroups = new List<List<CandidateInfo<T>>>();
        var weights = new List<float>();

        foreach (var group in groups)
        {
            int stepsFromMax = Mathf.RoundToInt((maxSteppedDamage - group.Key) / step);
            float weight = Mathf.Max(0.1f, 1.0f - (stepsFromMax * 0.3f)); // 上位ほど高い重み

            weightedGroups.Add(group.ToList());
            weights.Add(weight);
        }

        var selectedGroup = PickWeightedGroup(weightedGroups, weights, random);

        // 選択されたグループ内からランダムで1つ選択
        return random.GetItem(selectedGroup).Original;
    }

    private static List<CandidateInfo<T>> PickWeightedGroup<T>(IReadOnlyList<List<CandidateInfo<T>>> groups, IReadOnlyList<float> weights, IBattleRandom random)
    {
        if (groups == null || groups.Count == 0) return new List<CandidateInfo<T>>();
        var total = 0f;
        for (var i = 0; i < weights.Count; i++)
        {
            var w = weights[i];
            if (w > 0) total += w;
        }
        if (total <= 0f)
        {
            return groups[0];
        }

        var roll = random.NextFloat(total);
        var cumulative = 0f;
        for (var i = 0; i < groups.Count; i++)
        {
            var w = i < weights.Count ? weights[i] : 1f;
            if (w <= 0) continue;
            cumulative += w;
            if (roll <= cumulative)
            {
                return groups[i];
            }
        }
        return groups[groups.Count - 1];
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
