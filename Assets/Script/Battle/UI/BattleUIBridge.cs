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

    // Phase 1: DI注入されたコントローラー
    private readonly IActionMarkController _actionMark;
    private readonly IEnemyPlacementController _enemyPlacement;
    private readonly IIntroOrchestratorFacade _introOrchestrator;

    // Phase 3a: SchizoLog DI注入
    private readonly SchizoLog _schizoLog;

    // Phase 3d: ArrowManager DI注入
    private readonly IArrowManager _arrowManager;

    public BattleUIBridge(
        MessageDropper messageDropper,
        IPlayersSkillUI skillUi,
        IPlayersRoster roster,
        IActionMarkController actionMark = null,
        IEnemyPlacementController enemyPlacement = null,
        IIntroOrchestratorFacade introOrchestrator = null,
        SchizoLog schizoLog = null,
        IArrowManager arrowManager = null)
    {
        this.messageDropper = messageDropper;
        this.skillUi = skillUi;
        this.roster = roster;
        _actionMark = actionMark;
        _enemyPlacement = enemyPlacement;
        _introOrchestrator = introOrchestrator;
        _schizoLog = schizoLog;
        _arrowManager = arrowManager;
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
        if (_schizoLog == null) return;
        _schizoLog.ClearLogs();
        foreach (var entry in eventHistory.Entries)
        {
            _schizoLog.AddLog(entry.Message, entry.Important);
        }
        _schizoLog.DisplayAllAsync().Forget();
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

        // まず roster から AllyId を取得
        if (roster != null && roster.TryGetAllyId(actor, out id)) return true;

        // CharacterId ベースで判定（派生クラスに依存しない）
        if (actor is AllyClass ally && ally.CharacterId.IsValid)
        {
            if (ally.CharacterId == CharacterId.Geino)
            {
                id = AllyId.Geino;
                return true;
            }
            if (ally.CharacterId == CharacterId.Noramlia)
            {
                id = AllyId.Noramlia;
                return true;
            }
            if (ally.CharacterId == CharacterId.Sites)
            {
                id = AllyId.Sites;
                return true;
            }
        }
        return false;
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
        _schizoLog?.AddLog(message, important);
    }

    public void HardStopAndClearLogs()
    {
        eventHistory.Clear();
        _schizoLog?.HardStopAndClearAsync().Forget();
    }

    public void MoveActionMarkToActorScaled(BaseStates acter, bool immediate, bool waitForIntro)
    {
        if (_actionMark != null && acter != null)
        {
            _actionMark.MoveToActorScaled(acter, immediate, waitForIntro).Forget();
        }
    }

    public void ShowActionMarkFromSpawn()
    {
        _actionMark?.ShowFromSpawn();
    }

    public bool PrepareBattleEnd()
    {
        if (_actionMark == null && _enemyPlacement == null)
        {
            Debug.LogError("OnBattleEndでActionMark/EnemyPlacementが認識されていない");
            return false;
        }

        _actionMark?.Hide();
        _enemyPlacement?.ClearEnemyUI();
        return true;
    }

    public UniTask RestoreZoomViaOrchestrator(bool animated, float duration)
    {
        if (_introOrchestrator == null) return UniTask.CompletedTask;
        return _introOrchestrator.RestoreAsync(animated, duration);
    }

    public void ClearArrows()
    {
        _arrowManager?.ClearQueue();
    }

    public void NextArrow()
    {
        _arrowManager?.Next();
    }

    public void SetArrowColors(Color frameColor, Color twoColor)
    {
        _arrowManager?.SetColorsForAll(frameColor, twoColor);
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
