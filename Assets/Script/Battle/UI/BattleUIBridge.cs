using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class BattleUIBridge
{
    public static BattleUIBridge Active { get; private set; }

    private IBattleContext battleContext;
    private readonly MessageDropper messageDropper;
    private readonly IPlayersSkillUI skillUi;
    private readonly IPlayersRoster roster;
    private readonly BattleEventHistory eventHistory = new BattleEventHistory();

    public BattleUIBridge(MessageDropper messageDropper, IPlayersSkillUI skillUi, IPlayersRoster roster)
    {
        this.messageDropper = messageDropper;
        this.skillUi = skillUi;
        this.roster = roster;
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

        if (skillUi == null)
        {
            Debug.LogError("BattleUIBridge.SwitchAllySkillUiState: SkillUI が null です");
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

        if (!TryGetAllyId(acter, out var allyId))
        {
            Debug.LogWarning("BattleUIBridge.SwitchAllySkillUiState: AllyId が特定できません。");
            return;
        }

        skillUi.OnlySelectActs(onlyRemainButtonByZoneTrait, onlyRemainButtonByType, allyId);
        skillUi.OnSkillSelectionScreenTransition(allyId);
        SetSkillUiState(ToSkillUiState(allyId));
    }

    private bool TryGetAllyId(BaseStates actor, out AllyId id)
    {
        id = default;
        if (actor == null) return false;
        if (roster != null && roster.TryGetAllyId(actor, out id)) return true;

        switch (actor)
        {
            case StairStates:
                id = AllyId.Geino;
                return true;
            case BassJackStates:
                id = AllyId.Noramlia;
                return true;
            case SateliteProcessStates:
                id = AllyId.Sites;
                return true;
            default:
                return false;
        }
    }

    private SkillUICharaState ToSkillUiState(AllyId allyId)
    {
        return allyId switch
        {
            AllyId.Geino => SkillUICharaState.geino,
            AllyId.Noramlia => SkillUICharaState.normalia,
            AllyId.Sites => SkillUICharaState.sites,
            _ => SkillUICharaState.geino
        };
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
        WalkingSystemManager manager = UnityEngine.Object.FindObjectOfType<WalkingSystemManager>();
        if (manager == null)
        {
            var all = Resources.FindObjectsOfTypeAll<WalkingSystemManager>();
            for (var i = 0; i < all.Length; i++)
            {
                var candidate = all[i];
                if (candidate == null) continue;
                if (!candidate.gameObject.scene.IsValid()) continue;
                manager = candidate;
                break;
            }
        }
        if (manager == null) return;
        manager.ApplyCurrentNodeUI();
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
