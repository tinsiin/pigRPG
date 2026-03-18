using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class BattleUIBridge : IBattleUiAdapter
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

    // Orchestrator（フォールバック付き）
    private BattleOrchestrator _orchestrator;
    private BattleOrchestrator Orchestrator => _orchestrator ?? BattleOrchestratorHub.Current;
    public void BindOrchestrator(BattleOrchestrator orchestrator) => _orchestrator = orchestrator;

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
        var orchestrator = Orchestrator;
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

    /// <summary>
    /// CharacterIdでスキルUI状態を設定する。
    /// </summary>
    public void SetSkillUiState(CharacterId id)
    {
        UIStateHub.SetSelectedCharacter(id);
    }

    public void PushMessage(string message)
    {
        messageDropper?.CreateMessage(message);
    }

    public void DisplayLogs()
    {
        if (_schizoLog == null) return;

        var (start, count) = eventHistory.AdvanceDisplayCursor();
        if (count <= 0) return;

        var entries = eventHistory.Entries;
        for (int i = start; i < start + count; i++)
        {
            if (entries[i].Type != BattleEventType.Log) continue;
            _schizoLog.AddLog(entries[i].Message, entries[i].Important);
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

        // イラつき攻撃時: 攻撃系スキルのみに制限
        if (acter.IsIrritationAttack && acter.IrritationForcedTarget != null)
        {
            onlyRemainButtonByType = SkillType.Attack;
        }

        // CharacterIdベースで処理（新キャラにも対応）
        if (!TryGetCharacterId(acter, out var characterId))
        {
            Debug.LogWarning("BattleUIBridge.SwitchAllySkillUiState: CharacterId が特定できません。");
            return;
        }

        skillUi.OnlySelectActs(onlyRemainButtonByZoneTrait, onlyRemainButtonByType, characterId);
        skillUi.OnSkillSelectionScreenTransition(characterId);
        UIStateHub.SetSelectedCharacter(characterId);
    }

    /// <summary>
    /// BaseStatesからCharacterIdを取得する。
    /// </summary>
    private bool TryGetCharacterId(BaseStates actor, out CharacterId id)
    {
        id = default;
        if (actor == null) return false;

        // AllyClass の場合は CharacterId を直接取得
        if (actor is AllyClass ally && ally.CharacterId.IsValid)
        {
            id = ally.CharacterId;
            return true;
        }

        // roster から CharacterId を逆引き
        if (roster is PlayersRoster playersRoster && playersRoster.TryGetCharacterId(actor, out id))
        {
            return true;
        }

        return false;
    }

    public void AddLog(string message, bool important)
    {
        eventHistory.Add(message, important);
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
        if (newVanguard?.UI != null)
        {
            newVanguard.UI.BeVanguardEffect();
        }
        if (oldVanguard?.UI != null)
        {
            oldVanguard.UI.LostVanguardEffect();
        }
    }
}
