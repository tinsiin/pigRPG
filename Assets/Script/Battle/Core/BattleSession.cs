using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

public interface IBattleSession
{
    IBattleContext Context { get; }
    BattleEventBus EventBus { get; }

    void Start();
    TabState Advance();
    UniTask<TabState> ApplyInputAsync(BattleInput input);
    UniTask EndAsync();
}

public enum BattleInputType
{
    SelectSkill,
    SelectRange,
    SelectTarget,
    StockSkill,
    Escape,
    DoNothing,
    Next,
    Cancel
}

public readonly struct BattleInput
{
    public BattleInputType Type { get; }
    public BaseStates Actor { get; }
    public BaseSkill Skill { get; }
    public DirectedWill TargetWill { get; }
    public IReadOnlyList<BaseStates> Targets { get; }
    public SkillZoneTrait RangeWill { get; }
    public bool IsOption { get; }

    public BattleInput(
        BattleInputType type,
        BaseStates actor = null,
        BaseSkill skill = null,
        DirectedWill targetWill = DirectedWill.One,
        IReadOnlyList<BaseStates> targets = null,
        SkillZoneTrait rangeWill = 0,
        bool isOption = false)
    {
        Type = type;
        Actor = actor;
        Skill = skill;
        TargetWill = targetWill;
        Targets = targets;
        RangeWill = rangeWill;
        IsOption = isOption;
    }

    public static BattleInput Next() => new(BattleInputType.Next);
    public static BattleInput DoNothing(BaseStates actor = null) => new(BattleInputType.DoNothing, actor);
    public static BattleInput Escape(BaseStates actor = null) => new(BattleInputType.Escape, actor);
    public static BattleInput Cancel(BaseStates actor = null) => new(BattleInputType.Cancel, actor);
    public static BattleInput SelectSkill(BaseStates actor, BaseSkill skill) => new(BattleInputType.SelectSkill, actor, skill);
    public static BattleInput StockSkill(BaseStates actor, BaseSkill skill) => new(BattleInputType.StockSkill, actor, skill);
    public static BattleInput SelectRange(BaseStates actor, SkillZoneTrait rangeWill, bool isOption = false)
        => new(BattleInputType.SelectRange, actor, rangeWill: rangeWill, isOption: isOption);
    public static BattleInput SelectTarget(BaseStates actor, DirectedWill targetWill, IReadOnlyList<BaseStates> targets = null)
        => new(BattleInputType.SelectTarget, actor, targetWill: targetWill, targets: targets);
}

public sealed class BattleSession : IBattleSession
{
    private readonly BattleManager _manager;

    public BattleSession(BattleManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public IBattleContext Context => _manager;
    public BattleEventBus EventBus => _manager.EventBus;

    public void Start()
    {
        _manager.NotifyBattleStarted();
    }

    public TabState Advance()
    {
        return _manager.ACTPop();
    }

    public UniTask<TabState> ApplyInputAsync(BattleInput input)
    {
        if (_manager == null)
        {
            return UniTask.FromResult(TabState.walk);
        }

        var actor = input.Actor ?? _manager.Acter;
        var actorName = actor != null ? actor.CharacterName : "";
        _manager.EventRecorder?.RecordInput(input, actor, _manager.BattleTurnCount, _manager.AllCharacters);
        switch (input.Type)
        {
            // 選択系入力はUIフロー(BattleOrchestrator.ApplyInput)で処理され、イベント発行しない
            // リプレイ時も同様にイベントを発行しないことで、検証時のイベントシーケンスを一致させる
            case BattleInputType.SelectSkill:
                return UniTask.FromResult(ApplySkillSelect(actor, input.Skill));
            case BattleInputType.StockSkill:
                return UniTask.FromResult(ApplyStockSkill(actor, input.Skill));
            case BattleInputType.SelectRange:
                return UniTask.FromResult(ApplyRangeSelect(actor, input.RangeWill, input.IsOption));
            case BattleInputType.SelectTarget:
                return UniTask.FromResult(ApplyTargetSelect(actor, input.TargetWill, input.Targets));
            case BattleInputType.DoNothing:
                // DoNothingはフラグセットのみ。進行は後続のNext入力で行う（UIフローとの一貫性）
                _manager.DoNothing = true;
                return UniTask.FromResult(TabState.NextWait);
            // 以下はUIフロー経由でもBattleSession経由で処理される入力
            case BattleInputType.Escape:
                if (actor != null)
                {
                    actor.SelectedEscape = true;
                }
                EventBus?.Publish(BattleEvent.InputApplied("Input:Escape", _manager.BattleTurnCount, actorName));
                return _manager.CharacterActBranching();
            case BattleInputType.Cancel:
                _manager.PassiveCancel = true;
                EventBus?.Publish(BattleEvent.InputApplied("Input:Cancel", _manager.BattleTurnCount, actorName));
                return _manager.CharacterActBranching();
            case BattleInputType.Next:
                EventBus?.Publish(BattleEvent.InputApplied("Input:Next", _manager.BattleTurnCount, actorName));
                return _manager.CharacterActBranching();
            default:
                _manager.ActionContext.Logger.LogWarning($"BattleSession.ApplyInputAsync: unsupported input type {input.Type}");
                return UniTask.FromResult(TabState.NextWait);
        }
    }

    public UniTask EndAsync()
    {
        return _manager.OnBattleEnd();
    }

    private TabState ApplySkillSelect(BaseStates actor, BaseSkill skill)
    {
        if (actor == null || skill == null)
        {
            return TabState.NextWait;
        }

        // 掛け合わせチェック: 武器装備中に非武器スキルを選択した場合、
        // 武器との掛け合わせスキルに差し替える
        skill = ResolveCombination(actor, skill);

        actor.SKillUseCall(skill);

        if (!skill.HasZoneTrait(SkillZoneTrait.CanSelectRange))
        {
            var normalizedTrait = SkillZoneTraitNormalizer.NormalizeForInitial(skill.ZoneTrait);
            actor.RangeWill = actor.RangeWill.Add(normalizedTrait);
        }

        var hasSingleTargetReservation = _manager.Acts.TryPeek(out var entry) && entry.SingleTarget != null;
        return hasSingleTargetReservation ? TabState.NextWait : AllyClass.DetermineNextUIState(actor.NowUseSkill);
    }

    private TabState ApplyStockSkill(BaseStates actor, BaseSkill skill)
    {
        if (actor == null || skill == null)
        {
            return TabState.NextWait;
        }
        if (skill.IsFullStock())
        {
            _manager.ActionContext.Logger.Log(skill.SkillName + "をストックが満杯。");
            return TabState.NextWait;
        }

        skill.ATKCountStock();
        _manager.ActionContext.Logger.Log(skill.SkillName + "をストックしました。");

        var list = actor.SkillList
            .Where(item => !ReferenceEquals(item, skill) && item.HasConsecutiveType(SkillConsecutiveType.Stockpile))
            .ToList();
        foreach (var stockSkill in list)
        {
            stockSkill.ForgetStock();
        }

        _manager.SkillStock = true;
        return TabState.NextWait;
    }

    private TabState ApplyRangeSelect(BaseStates actor, SkillZoneTrait rangeWill, bool isOption)
    {
        if (actor == null)
        {
            return TabState.NextWait;
        }

        actor.RangeWill = SkillZoneTraitNormalizer.Normalize(actor.RangeWill.Add(rangeWill));
        if (isOption)
        {
            return TabState.SelectRange;
        }

        return actor.HasRangeWill(SkillZoneTrait.AllTarget) ? TabState.NextWait : TabState.SelectTarget;
    }

    private TabState ApplyTargetSelect(BaseStates actor, DirectedWill targetWill, IReadOnlyList<BaseStates> targets)
    {
        if (actor == null)
        {
            return TabState.NextWait;
        }

        actor.Target = targetWill;

        if (targets != null && targets.Count > 0)
        {
            var context = _manager.ActionContext;
            context.Unders.ClearAndSetCurrentSkill(actor.NowUseSkill);
            var array = targets.ToArray();
            _manager.ActionContext.Random.Shuffle(array);
            foreach (var t in array)
            {
                ApplyExposureModifier(context, actor, t);
                context.Unders.CharaAdd(t);
            }
        }

        return TabState.NextWait;
    }

    /// <summary>
    /// 掛け合わせ解決: 武器装備中に非武器スキルを使った場合、
    /// 掛け合わせスキルが定義されていれば差し替える。
    /// </summary>
    private static BaseSkill ResolveCombination(BaseStates actor, BaseSkill skill)
    {
        var weapon = actor.NowUseWeapon;
        if (weapon == null || weapon.CombinationEntries == null || weapon.CombinationEntries.Count == 0)
            return skill;

        // 武器スキルそのものを選択した場合は掛け合わせしない
        if (ReferenceEquals(skill, weapon.WeaponSkill))
            return skill;

        // AllySkillの場合のみIDで掛け合わせ検索
        if (skill is AllySkill allySkill)
        {
            var combined = weapon.GetCombinedSkill(allySkill.ID);
            if (combined != null)
            {
                UnityEngine.Debug.Log($"掛け合わせ発動: {weapon.name} × {skill.SkillName} → {combined.SkillName}");
                return combined;
            }
        }

        return skill;
    }

    private void ApplyExposureModifier(BattleActionContext context, BaseStates actor, BaseStates target)
    {
        if (actor is not AllyClass allyActer)
        {
            return;
        }
        if (context.IsFriend(actor, target))
        {
            return;
        }

        var exposureModifier = allyActer.GetExposureAccuracyPercentageBonus(target.PassivesTargetProbability());
        if (exposureModifier > 0)
        {
            allyActer.SetCharaConditionalModifierList(target, "隙だらけ", StatModifier.Eye, exposureModifier);
        }
    }
}
