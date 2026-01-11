using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class BattleUIBridge
{
    public static BattleUIBridge Active { get; private set; }

    private IBattleContext battleContext;
    private readonly MessageDropper messageDropper;
    private readonly BattleEventHistory eventHistory = new BattleEventHistory();

    public BattleUIBridge(MessageDropper messageDropper)
    {
        this.messageDropper = messageDropper;
    }

    public static void SetActive(BattleUIBridge bridge)
    {
        Active = bridge;
    }

    public void BindBattleContext(IBattleContext context)
    {
        battleContext = context;
    }

    public IBattleContext BattleContext => battleContext;
    public BattleEventHistory EventHistory => eventHistory;

    public void SetUserUiState(TabState state, bool syncOrchestrator = true)
    {
        var orchestrator = BattleOrchestratorHub.Current;
        if (syncOrchestrator && orchestrator != null)
        {
            orchestrator.SyncFromUiState(state);
        }
        var userState = UIStateHub.UserState;
        if (userState != null)
        {
            userState.Value = orchestrator?.CurrentUiState ?? state;
        }
        else
        {
            Debug.LogError("BattleUIBridge.SetUserUiState: UIStateHub.UserState が null です");
        }
    }

    public void SetSkillUiState(SkillUICharaState state)
    {
        var skillState = UIStateHub.SkillState;
        if (skillState != null)
        {
            skillState.Value = state;
        }
        else
        {
            Debug.LogError("BattleUIBridge.SetSkillUiState: UIStateHub.SkillState が null です");
        }
    }

    public void PushMessage(string message)
    {
        messageDropper?.CreateMessage(message);
    }

    public void DisplayLogs()
    {
        var log = SchizoLog.Instance;
        if (log == null) return;
        log.ClearLogs();
        foreach (var entry in eventHistory.Entries)
        {
            log.AddLog(entry.Message, entry.Important);
        }
        log.DisplayAllAsync().Forget();
    }

    public void SetSelectedActor(BaseStates acter)
    {
        CharaconfigController.Instance?.SetSelectedByActor(acter);
    }

    public void SwitchAllySkillUiState(BaseStates acter, bool hasSingleTargetReservation)
    {
        if (acter == null) return;

        var skillUi = PlayersStatesHub.SkillUI;
        if (skillUi == null)
        {
            Debug.LogError("BattleUIBridge.SwitchAllySkillUiState: PlayersStatesHub.SkillUI が null です");
            return;
        }

        var onlyRemainButtonByType = Enum.GetValues(typeof(SkillType))
            .Cast<SkillType>()
            .Aggregate((current, next) => current | next);
        var onlyRemainButtonByZoneTrait = Enum.GetValues(typeof(SkillZoneTrait))
            .Cast<SkillZoneTrait>()
            .Aggregate((SkillZoneTrait)0, (cur, next) => cur | next);

        if (hasSingleTargetReservation)
        {
            onlyRemainButtonByZoneTrait = SkillFilterPresets.SingleTargetZoneTraitMask;
            onlyRemainButtonByType = SkillFilterPresets.SingleTargetTypeMask;
        }

        switch (acter)
        {
            case StairStates:
                skillUi.OnlySelectActs(onlyRemainButtonByZoneTrait, onlyRemainButtonByType, 0);
                skillUi.OnSkillSelectionScreenTransition(0);
                SetSkillUiState(SkillUICharaState.geino);
                break;
            case BassJackStates:
                skillUi.OnlySelectActs(onlyRemainButtonByZoneTrait, onlyRemainButtonByType, 1);
                skillUi.OnSkillSelectionScreenTransition(1);
                SetSkillUiState(SkillUICharaState.normalia);
                break;
            case SateliteProcessStates:
                skillUi.OnlySelectActs(onlyRemainButtonByZoneTrait, onlyRemainButtonByType, 2);
                skillUi.OnSkillSelectionScreenTransition(2);
                SetSkillUiState(SkillUICharaState.sites);
                break;
        }
    }

    public void AddLog(string message, bool important)
    {
        eventHistory.Add(message, important);
        SchizoLog.Instance.AddLog(message, important);
    }

    public void HardStopAndClearLogs()
    {
        eventHistory.Clear();
        SchizoLog.Instance.HardStopAndClearAsync().Forget();
    }

    public void MoveActionMarkToActorScaled(BaseStates acter, bool immediate, bool waitForIntro)
    {
        var ui = WatchUIUpdate.Instance;
        if (ui != null && acter != null)
        {
            ui.MoveActionMarkToActorScaled(acter, immediate, waitForIntro).Forget();
        }
    }

    public void ShowActionMarkFromSpawn()
    {
        WatchUIUpdate.Instance?.ShowActionMarkFromSpawn();
    }

    public bool PrepareBattleEnd()
    {
        var ui = WatchUIUpdate.Instance;
        if (ui == null)
        {
            Debug.LogError("OnBattleEndでWatchUIUpdateが認識されていない");
            return false;
        }

        ui.HideActionMark();
        ui.EraceEnemyUI();
        return true;
    }

    public UniTask RestoreZoomViaOrchestrator(bool animated, float duration)
    {
        var ui = WatchUIUpdate.Instance;
        if (ui == null) return UniTask.CompletedTask;
        return ui.RestoreZoomViaOrchestrator(animated: animated, duration: duration);
    }

    public void ClearArrows()
    {
        BattleSystemArrowManager.Instance.ClearQueue();
    }

    public void NextArrow()
    {
        BattleSystemArrowManager.Instance.Next();
    }

    public void SetArrowColors(Color frameColor, Color twoColor)
    {
        BattleSystemArrowManager.Instance.SetColorsForAll(frameColor, twoColor);
    }

    public void SetArrowColorsFromStage()
    {
        var walking = Walking.Instance;
        if (walking == null) return;
        var stage = walking.NowStageData;
        if (stage == null) return;
        SetArrowColors(stage.StageThemeColorUI.FrameArtColor, stage.StageThemeColorUI.TwoColor);
    }

    public void ApplyVanguardEffect(BaseStates newVanguard, BaseStates oldVanguard)
    {
        if (newVanguard != null)
        {
            newVanguard.UI.BeVanguardEffect();
        }
        if (oldVanguard != null)
        {
            oldVanguard.UI.LostVanguardEffect();
        }
    }
}
