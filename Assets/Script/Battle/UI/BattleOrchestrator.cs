using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using RandomExtensions;
using UnityEngine;
using static CommonCalc;

public sealed class BattleOrchestrator
{
    public BattleManager Manager { get; }
    public BattlePhase Phase { get; private set; } = BattlePhase.Idle;
    public ChoiceRequest CurrentChoiceRequest { get; private set; } = ChoiceRequest.None;
    private int _requestSequence = 0;
    private TabState _lastTabState = TabState.NextWait;
    private bool _isAdvancing = false;
    private bool _pendingAdvance = false;

    public BattleOrchestrator(
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        BattleStartSituation first,
        MessageDropper messageDropper,
        float escapeRate,
        IBattleMetaProvider metaProvider,
        IPlayersSkillUI skillUi,
        IPlayersRoster roster)
    {
        Manager = new BattleManager(allyGroup, enemyGroup, first, messageDropper, escapeRate, metaProvider, skillUi, roster);
    }

    public TabState StartBattle()
    {
        var state = Manager.ACTPop();
        UpdateChoiceState(state);
        return state;
    }

    public UniTask<TabState> Step()
    {
        return StepInternal();
    }

    public async UniTask<TabState> RequestAdvance()
    {
        if (Phase == BattlePhase.Completed)
        {
            return CurrentUiState;
        }
        if (_isAdvancing)
        {
            _pendingAdvance = true;
            return CurrentUiState;
        }

        _isAdvancing = true;
        try
        {
            TabState state;
            do
            {
                _pendingAdvance = false;
                state = await StepInternal();
            } while (_pendingAdvance && state == TabState.NextWait && Phase == BattlePhase.WaitingForInput);
            return state;
        }
        finally
        {
            _isAdvancing = false;
            _pendingAdvance = false;
        }
    }

    public UniTask EndBattle()
    {
        Phase = BattlePhase.Completed;
        CurrentChoiceRequest = ChoiceRequest.None;
        BattleOrchestratorHub.Clear(this);
        return Manager.OnBattleEnd();
    }

    public TabState CurrentUiState => MapChoiceToUiState();

    public void SyncFromUiState(TabState state)
    {
        UpdateChoiceState(state);
    }

    public TabState ApplyInput(ActionInput input)
    {
        if (input == null)
        {
            return CurrentUiState;
        }
        if (!CanAcceptInput(input))
        {
            return CurrentUiState;
        }

        switch (input.Kind)
        {
            case ActionInputKind.SkillSelect:
                return ApplySkillSelect(input);
            case ActionInputKind.StockSkill:
                return ApplyStockSkill(input);
            case ActionInputKind.DoNothing:
                return ApplyDoNothing();
            case ActionInputKind.RangeSelect:
                return ApplyRangeSelect(input);
            case ActionInputKind.TargetSelect:
                return ApplyTargetSelect(input);
            default:
                return CurrentUiState;
        }
    }

    private bool CanAcceptInput(ActionInput input)
    {
        if (Phase != BattlePhase.WaitingForInput)
        {
            Debug.LogWarning($"ActionInput rejected (phase): {Phase}");
            return false;
        }
        if (CurrentChoiceRequest.Kind == ChoiceKind.None)
        {
            Debug.LogWarning("ActionInput rejected (no active choice)");
            return false;
        }
        if (!IsChoiceKindCompatible(input.Kind, CurrentChoiceRequest.Kind))
        {
            Debug.LogWarning($"ActionInput rejected (kind mismatch): input={input.Kind}, expected={CurrentChoiceRequest.Kind}");
            return false;
        }
        if (input.RequestId != 0 && input.RequestId != CurrentChoiceRequest.RequestId)
        {
            Debug.LogWarning($"ActionInput rejected (request id mismatch): input={input.RequestId}, expected={CurrentChoiceRequest.RequestId}");
            return false;
        }
        if (CurrentChoiceRequest.Actor != null && input.Actor != null && !ReferenceEquals(CurrentChoiceRequest.Actor, input.Actor))
        {
            Debug.LogWarning("ActionInput rejected (actor mismatch)");
            return false;
        }
        return true;
    }

    private bool IsChoiceKindCompatible(ActionInputKind inputKind, ChoiceKind expectedKind)
    {
        switch (inputKind)
        {
            case ActionInputKind.SkillSelect:
            case ActionInputKind.StockSkill:
            case ActionInputKind.DoNothing:
                return expectedKind == ChoiceKind.Skill;
            case ActionInputKind.RangeSelect:
                return expectedKind == ChoiceKind.Range;
            case ActionInputKind.TargetSelect:
                return expectedKind == ChoiceKind.Target;
            default:
                return false;
        }
    }

    private TabState ApplySkillSelect(ActionInput input)
    {
        var actor = input.Actor ?? Manager.Acter;
        if (actor == null || input.Skill == null)
        {
            return CurrentUiState;
        }

        actor.SKillUseCall(input.Skill);

        if (!input.Skill.HasZoneTrait(SkillZoneTrait.CanSelectRange))
        {
            actor.RangeWill |= input.Skill.ZoneTrait;
        }

        var nextState = Manager.Acts.GetAtSingleTarget(0) != null
            ? TabState.NextWait
            : AllyClass.DetermineNextUIState(actor.NowUseSkill);

        UpdateChoiceState(nextState);
        return CurrentUiState;
    }

    private TabState ApplyStockSkill(ActionInput input)
    {
        var actor = input.Actor ?? Manager.Acter;
        var skill = input.Skill;
        if (actor == null || skill == null)
        {
            return CurrentUiState;
        }
        if (skill.IsFullStock())
        {
            Debug.Log(skill.SkillName + "をストックが満杯。");
            return CurrentUiState;
        }

        skill.ATKCountStock();
        Debug.Log(skill.SkillName + "をストックしました。");

        var list = actor.SkillList
            .Where(item => !ReferenceEquals(item, skill) && item.HasConsecutiveType(SkillConsecutiveType.Stockpile))
            .ToList();
        foreach (var stockSkill in list)
        {
            stockSkill.ForgetStock();
        }

        Manager.SkillStock = true;
        UpdateChoiceState(TabState.NextWait);
        return CurrentUiState;
    }

    private TabState ApplyDoNothing()
    {
        Manager.DoNothing = true;
        UpdateChoiceState(TabState.NextWait);
        return CurrentUiState;
    }

    private TabState ApplyRangeSelect(ActionInput input)
    {
        var actor = input.Actor ?? Manager.Acter;
        if (actor == null)
        {
            return CurrentUiState;
        }

        actor.RangeWill |= input.RangeWill;
        if (input.IsOption)
        {
            return CurrentUiState;
        }

        var nextState = actor.HasRangeWill(SkillZoneTrait.AllTarget)
            ? TabState.NextWait
            : TabState.SelectTarget;
        UpdateChoiceState(nextState);
        return CurrentUiState;
    }

    private TabState ApplyTargetSelect(ActionInput input)
    {
        var actor = input.Actor ?? Manager.Acter;
        if (actor == null)
        {
            return CurrentUiState;
        }

        actor.Target = input.TargetWill;

        if (input.Targets != null && input.Targets.Count > 0)
        {
            var targets = input.Targets.ToArray();
            RandomEx.Shared.Shuffle(targets);
            foreach (var target in targets)
            {
                ApplyExposureModifier(actor, target);
                Manager.unders.CharaAdd(target);
            }
        }

        UpdateChoiceState(TabState.NextWait);
        return CurrentUiState;
    }

    private void ApplyExposureModifier(BaseStates actor, BaseStates target)
    {
        if (actor is not AllyClass allyActer)
        {
            return;
        }
        if (Manager.IsFriend(actor, target))
        {
            return;
        }

        var exposureModifier = allyActer.GetExposureAccuracyPercentageBonus(target.PassivesTargetProbability());
        if (exposureModifier > 0)
        {
            allyActer.SetCharaConditionalModifierList(target, "隙だらけ", whatModify.eye, exposureModifier);
        }
    }

    private async UniTask<TabState> StepInternal()
    {
        Phase = BattlePhase.Resolving;
        var state = await Manager.CharacterActBranching();
        UpdateChoiceState(state);
        return state;
    }

    private void UpdateChoiceState(TabState state)
    {
        _lastTabState = state;
        if (state == TabState.walk)
        {
            Phase = BattlePhase.Completed;
            CurrentChoiceRequest = ChoiceRequest.None;
            return;
        }
        Phase = BattlePhase.WaitingForInput;
        var kind = ChoiceKind.None;
        switch (state)
        {
            case TabState.Skill:
                kind = ChoiceKind.Skill;
                break;
            case TabState.SelectRange:
                kind = ChoiceKind.Range;
                break;
            case TabState.SelectTarget:
                kind = ChoiceKind.Target;
                break;
        }
        if (kind == ChoiceKind.None)
        {
            CurrentChoiceRequest = ChoiceRequest.None;
            return;
        }
        _requestSequence++;
        var hasSingleTargetReservation = Manager.Acts.GetAtSingleTarget(0) != null;
        CurrentChoiceRequest = new ChoiceRequest
        {
            Kind = kind,
            RequestId = _requestSequence,
            Actor = Manager.Acter,
            HasSingleTargetReservation = hasSingleTargetReservation,
            RangeMask = hasSingleTargetReservation ? SkillFilterPresets.SingleTargetZoneTraitMask : 0,
            TypeMask = hasSingleTargetReservation ? SkillFilterPresets.SingleTargetTypeMask : 0
        };
    }

    private TabState MapChoiceToUiState()
    {
        switch (CurrentChoiceRequest.Kind)
        {
            case ChoiceKind.Skill:
                return TabState.Skill;
            case ChoiceKind.Range:
                return TabState.SelectRange;
            case ChoiceKind.Target:
                return TabState.SelectTarget;
            default:
                return _lastTabState;
        }
    }
}
