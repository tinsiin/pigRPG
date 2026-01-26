using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IExitSelectionUI
{
    UniTask<int> ShowExitChoices(string[] labels, string[] ids);
}

public sealed class AreaController
{
    private readonly FlowGraphSO graph;
    private readonly GameContext context;
    private readonly IExitSelectionUI exitSelectionUI;
    private WalkApproachUI approachUI;
    private readonly SideObjectPresenter sidePresenter;
    private readonly CentralObjectPresenter centralPresenter;
    private readonly EventHost eventHost;
    private readonly SideObjectSelector sideObjectSelector = new SideObjectSelector();
    private readonly EncounterResolver encounterResolver = new EncounterResolver();
    private readonly ExitResolver exitResolver = new ExitResolver();
    private const int RewindStepsOnFail = 10;
    private NodeSO currentNode;
    private bool hasEnteredCurrentNode;

    // Phase 2: Gate/Anchor integration
    private GateResolver gateResolver;
    private AnchorManager anchorManager;
    private IWalkInputProvider walkInputProvider;

    // Progress tracking
    private readonly ProgressCalculator progressCalculator = new ProgressCalculator();
    private System.Action<ProgressSnapshot> onProgressChanged;

    public AreaController(
        FlowGraphSO graph,
        GameContext context,
        IExitSelectionUI exitSelectionUI,
        WalkApproachUI approachUI,
        EventHost eventHost,
        SideObjectPresenter sidePresenter,
        CentralObjectPresenter centralPresenter,
        IWalkInputProvider walkInputProvider = null)
    {
        this.graph = graph;
        this.context = context;
        this.exitSelectionUI = exitSelectionUI;
        this.approachUI = approachUI;
        this.eventHost = eventHost;
        this.sidePresenter = sidePresenter;
        this.centralPresenter = centralPresenter;
        this.walkInputProvider = walkInputProvider;
        // Clear stale refresh requests so the first step advances normally.
        context.RequestRefreshWithoutStep = false;

        // Phase 2: Initialize Gate/Anchor systems
        gateResolver = new GateResolver();
        anchorManager = new AnchorManager();
        context.GateResolver = gateResolver;
        context.AnchorManager = anchorManager;
        context.SideObjectSelector = sideObjectSelector;

        InitializeStartNode();
        hasEnteredCurrentNode = false;

        // ゲート情報を初期化（UI表示用）
        InitializeGateResolverForCurrentNode();

        // Phase 1.1: Initialize side object selector
        ConfigureSideObjectSelector();
    }

    public string CurrentNodeId => currentNode != null ? currentNode.NodeId : null;
    public NodeSO CurrentNode => currentNode;
    public SideObjectSelector SideObjectSelector => sideObjectSelector;

    public void SetApproachUI(WalkApproachUI ui)
    {
        approachUI = ui;
    }

    public void SetWalkInputProvider(IWalkInputProvider provider)
    {
        walkInputProvider = provider;
    }

    public void SetProgressCallback(System.Action<ProgressSnapshot> callback)
    {
        onProgressChanged = callback;
    }

    private void NotifyProgressChanged()
    {
        if (onProgressChanged == null) return;
        var snapshot = progressCalculator.Calculate(currentNode, gateResolver, context.Counters);
        onProgressChanged(snapshot);
    }

    public ProgressSnapshot GetCurrentProgress()
    {
        return progressCalculator.Calculate(currentNode, gateResolver, context.Counters);
    }

    public async UniTask WalkStep()
    {
        // Anchor rewind後のノード同期チェック
        SyncCurrentNodeFromWalkState();

        if (currentNode == null)
        {
            Debug.LogError("AreaController.WalkStep: currentNode is null.");
            return;
        }

        // Phase 2: Handle refresh without step (after rewind)
        if (context.RequestRefreshWithoutStep)
        {
            context.RequestRefreshWithoutStep = false;
            await RefreshWithoutStep();
            NotifyProgressChanged(); // 1回だけ通知
            return;
        }

        // Capture node entry flag before updating it
        var isNodeEntry = !hasEnteredCurrentNode;

        if (!hasEnteredCurrentNode)
        {
            await TriggerEvent(currentNode.OnEnterEvent);
            // Note: GateResolver is already initialized in constructor
            hasEnteredCurrentNode = true;
        }

        // Advance counters at the start of step (so UI updates immediately)
        context.Counters.Advance(1);

        // Phase 2: Update track progress (additional progress beyond the base 1)
        UpdateTrackProgress();

        // Advance side object cooldowns
        sideObjectSelector.AdvanceStep();

        // Advance encounter overlays
        context.AdvanceEncounterOverlays();

        var sidePair = sideObjectSelector.RollPair(currentNode.SideObjectTable, currentNode, context, isNodeEntry: isNodeEntry);
        var centralEvent = currentNode.CentralEvent;
        var hasCentral = centralEvent != null || currentNode.CentralVisual.HasVisual;

        ShowApproachObjects(sidePair, hasCentral);

        // Phase 2: Check gates before encounter
        var gateHandled = await CheckGates();

        // ゲート処理中にAnchor rewindでノードが変わった可能性をチェック
        if (SyncCurrentNodeFromWalkState())
        {
            // ノードが変わったので次のステップで処理
            NotifyProgressChanged();
            return;
        }

        if (gateHandled)
        {
            NotifyProgressChanged(); // 1回だけ通知
            return;
        }

        // ここで一度UIを更新して、歩数の進行を即時反映させる
        NotifyProgressChanged();

        var encounterResult = encounterResolver.Resolve(currentNode.EncounterTable, context, skipRoll: false, currentNode.EncounterRateMultiplier);
        if (encounterResult.Triggered)
        {
            var battleResult = await RunEncounter(encounterResult.Encounter);
            if (battleResult.Encountered)
            {
                var outcomeContext = await HandleEncounterOutcome(encounterResult.Encounter, battleResult.Outcome, sidePair, centralEvent, hasCentral);
                sidePair = outcomeContext.SidePair;
                centralEvent = outcomeContext.CentralEvent;
                hasCentral = outcomeContext.HasCentral;
            }
        }

        // 強制イベントチェック（エンカウント後、アプローチ前）
        var forcedEventHandled = await CheckForcedEvents();
        if (forcedEventHandled)
        {
            NotifyProgressChanged();
            return;
        }

        await HandleApproach(sidePair, centralEvent, hasCentral);

        // If an event triggered a rewind (e.g., RewindToAnchorEffect), skip exit check
        // The next WalkStep will handle the new node state
        if (context.RequestRefreshWithoutStep)
        {
            NotifyProgressChanged();
            return;
        }

        // Phase 2: Check exit with gate cleared status
        // カウンターを再取得（ゲート処理でリセットされている可能性があるため）
        var allGatesCleared = gateResolver.AllGatesCleared(currentNode);
        var maxGatePosition = gateResolver.GetMaxResolvedPosition();
        var exitCounters = new WalkCountersSnapshot(
            context.Counters.GlobalSteps,
            context.Counters.NodeSteps,
            context.Counters.TrackProgress);
        if (currentNode.ExitSpawn != null && currentNode.ExitSpawn.ShouldSpawn(exitCounters, allGatesCleared, maxGatePosition))
        {
            var exitResult = await ShowExitAndSelectDestination();

            if (exitResult.Transitioned)
            {
                NotifyProgressChanged(); // 1回だけ通知
                return;
            }

            // HandleExitSkipped() already handles TrackProgress reset for Steps mode
            NotifyProgressChanged(); // 1回だけ通知
            return;
        }

        NotifyProgressChanged(); // 1回だけ通知（最後に1回）
    }

    /// <summary>
    /// Refresh without advancing step count (used after rewind)
    /// </summary>
    private async UniTask RefreshWithoutStep()
    {
        // Clear pending on refresh without step
        sideObjectSelector.ClearPending();

        var sidePair = sideObjectSelector.RollPair(currentNode.SideObjectTable, currentNode, context, isNodeEntry: false);
        var centralEvent = currentNode.CentralEvent;
        var hasCentral = centralEvent != null || currentNode.CentralVisual.HasVisual;

        ShowApproachObjects(sidePair, hasCentral);

        // Check gates but don't advance counters
        await CheckGates();
        // Skip encounter roll
        // Skip counter advancement
    }

    /// <summary>
    /// Initialize gate resolver when entering a node
    /// </summary>
    private void InitializeGateResolverForCurrentNode()
    {
        if (currentNode == null) return;
        var nodeSeed = context.WalkState.NodeSeed;
        gateResolver.InitializeForNode(currentNode, nodeSeed);
        context.CurrentNode = currentNode;
    }

    /// <summary>
    /// Sync currentNode with WalkState.CurrentNodeId (after anchor rewind)
    /// Returns true if node was changed
    /// </summary>
    private bool SyncCurrentNodeFromWalkState()
    {
        var expectedNodeId = context.WalkState.CurrentNodeId;
        if (currentNode != null && currentNode.NodeId == expectedNodeId) return false;

        if (string.IsNullOrEmpty(expectedNodeId)) return false;

        if (!graph.TryGetNode(expectedNodeId, out var node))
        {
            Debug.LogError($"AreaController.SyncCurrentNodeFromWalkState: node not found: {expectedNodeId}");
            return false;
        }

        // Preserve gate state snapshot before re-initialization
        // (may have been restored by AnchorManager or save data)
        // Only restore if snapshot belongs to the target node
        // Use GetRestoredFromNodeId first (set by AnchorManager for cross-node rewinds),
        // fallback to GetCachedNodeId for same-node rewinds or save data loads
        var snapshotNodeId = gateResolver.GetRestoredFromNodeId() ?? gateResolver.GetCachedNodeId();
        var preservedGateSnapshot = gateResolver.TakeSnapshot();

        currentNode = node;
        hasEnteredCurrentNode = false; // 新しいノードなのでOnEnterを再実行
        InitializeGateResolverForCurrentNode();

        // Restore preserved gate state only if snapshot belongs to target node
        // (for PositionAndState rewind or save data load)
        // Skip for PositionOnly rewind (snapshot is from old node)
        if (preservedGateSnapshot.Count > 0 && snapshotNodeId == node.NodeId)
        {
            gateResolver.RestoreFromSnapshot(preservedGateSnapshot);
        }
        return true;
    }

    /// <summary>
    /// Update track progress based on node's TrackConfig
    /// Note: Advance(1) already increments TrackProgress by 1,
    /// so this method only adds the extra amount (StepDelta - 1)
    /// </summary>
    private void UpdateTrackProgress()
    {
        var config = currentNode?.TrackConfig;
        if (config == null || !config.HasConfig) return;

        // Advance(1) will add 1, so only add the extra amount
        var extraProgress = config.StepDelta - 1;
        if (extraProgress > 0)
        {
            context.Counters.AdvanceTrackProgress(extraProgress);
        }

        if (config.HasProgressKey)
        {
            // Advance(1)とAdvanceTrackProgress(extraProgress)後の現在値を使用
            context.SetCounter(config.ProgressKey, context.Counters.TrackProgress);
        }
    }

    /// <summary>
    /// Check and handle gates. Returns true if gate blocked further processing.
    /// </summary>
    private async UniTask<bool> CheckGates()
    {
        var gate = gateResolver.GetNextGate(currentNode, context.Counters.TrackProgress);
        if (gate == null) return false;

        // GateEvent: OnAppear timing
        if (gate.GateEvent != null && gate.EventTiming == GateEventTiming.OnAppear)
        {
            await TriggerEvent(gate.GateEvent);
        }

        // Show gate（常にボタン表示）
        centralPresenter?.ShowGate(gate.Visual);

        // Wait for approach button (GateApproachButton or walk button to skip)
        if (approachUI != null)
        {
            var gateLabel = !string.IsNullOrEmpty(gate.Visual.Label) ? gate.Visual.Label : "門";
            var choice = await approachUI.WaitForGateSelection(gateLabel);

            if (choice == ApproachChoice.Skip)
            {
                // Walk button = skip
                await HandleGateSkipped(gate);
                centralPresenter?.Hide();
                return true;
            }
            // GateApproachButton click = approach and check conditions
        }

        // Check pass conditions (null-safe)
        var passed = CheckPassConditions(gate.PassConditions);

        if (passed)
        {
            // Play pass SFX
            centralPresenter?.PlayGatePassSFX(gate.Visual);

            // GateEvent: OnPass timing
            if (gate.GateEvent != null && gate.EventTiming == GateEventTiming.OnPass)
            {
                await TriggerEvent(gate.GateEvent);
            }

            await ApplyEffects(gate.OnPass);
            gateResolver.MarkCleared(gate);
        }
        else
        {
            // Play fail SFX
            centralPresenter?.PlayGateFailSFX(gate.Visual);

            // GateEvent: OnFail timing
            if (gate.GateEvent != null && gate.EventTiming == GateEventTiming.OnFail)
            {
                await TriggerEvent(gate.GateEvent);
            }

            await ApplyEffects(gate.OnFail);

            // If rewind effect was triggered, skip failure bookkeeping
            // (the restored state would be overwritten otherwise)
            if (context.RequestRefreshWithoutStep)
            {
                centralPresenter?.Hide();
                return true;
            }

            gateResolver.MarkFailed(gate);
            ResetTrackProgressForGate();
        }

        // ゲート処理完了後は常にHideしてreturn true（歩行をブロック）
        centralPresenter?.Hide();
        return true;
    }

    /// <summary>
    /// Check and handle forced events. Returns true if event was triggered.
    /// </summary>
    private async UniTask<bool> CheckForcedEvents()
    {
        // 歩数を進める（クールダウン用）
        context.ForcedEventStateManager.IncrementSteps();

        // トリガーがなければスキップ
        if (!currentNode.HasForcedEventTriggers) return false;

        var triggers = currentNode.ForcedEventTriggers;
        if (triggers == null) return false;

        foreach (var trigger in triggers)
        {
            if (trigger == null) continue;

            // 発火可能かチェック
            if (!context.ForcedEventStateManager.CanTrigger(trigger)) continue;

            // 条件チェック
            if (trigger.HasConditions)
            {
                var conditionsMet = true;
                foreach (var condition in trigger.Conditions)
                {
                    if (condition == null) continue;
                    if (!condition.IsMet(context))
                    {
                        conditionsMet = false;
                        break;
                    }
                }
                if (!conditionsMet) continue;
            }

            // タイプ別判定
            var shouldTrigger = false;
            switch (trigger.Type)
            {
                case ForcedEventType.Steps:
                    shouldTrigger = context.Counters.NodeSteps >= trigger.StepCount;
                    break;
                case ForcedEventType.Probability:
                    shouldTrigger = context.GetRandomFloat() < trigger.Probability;
                    break;
            }

            if (!shouldTrigger) continue;

            // イベント実行（EventDefinitionSO経由）
            if (trigger.EventDefinition != null)
            {
                await TriggerEvent(trigger.EventDefinition);
            }

            // 発火を記録
            context.ForcedEventStateManager.RecordTrigger(trigger.TriggerId, trigger.ConsumeOnTrigger);

            // 1つ発火したら終了（1歩で複数発火は避ける）
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handle gate skip (walk button pressed)
    /// </summary>
    private async UniTask HandleGateSkipped(GateMarker gate)
    {
        ResetTrackProgressForGate();
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// Reset track progress when gate is skipped or failed.
    /// This allows the player to walk back to the gate position and try again.
    /// </summary>
    private void ResetTrackProgressForGate()
    {
        context.Counters.ResetTrackProgress();

        // Sync ProgressKey counter with TrackProgress to maintain consistency
        var progressKey = currentNode?.TrackConfig?.ProgressKey;
        if (!string.IsNullOrEmpty(progressKey))
        {
            context.SetCounter(progressKey, 0);
        }
    }

    /// <summary>
    /// Apply effects array
    /// </summary>
    private async UniTask ApplyEffects(EffectSO[] effects)
    {
        if (effects == null) return;
        foreach (var effect in effects)
        {
            if (effect == null) continue;
            await effect.Apply(context);
        }
    }

    /// <summary>
    /// Check pass conditions with null-safety
    /// </summary>
    private bool CheckPassConditions(ConditionSO[] conditions)
    {
        if (conditions == null || conditions.Length == 0) return true;

        foreach (var condition in conditions)
        {
            // null要素はスキップ（Unityインスペクタで空スロットになることがある）
            if (condition == null) continue;
            if (!condition.IsMet(context)) return false;
        }
        return true;
    }

    /// <summary>
    /// Show exit and handle selection with unified skip operation
    /// </summary>
    private async UniTask<ExitResult> ShowExitAndSelectDestination()
    {
        // Check if any exits are available (considering conditions)
        var availableExits = exitResolver.ResolveExits(
            currentNode,
            context,
            currentNode.ExitSelectionMode,
            currentNode.MaxExitChoices);

        if (availableExits.Count == 0)
        {
            return new ExitResult(false, false);
        }

        // Show exit visual
        if (currentNode.ExitVisual.HasSprite)
        {
            centralPresenter?.ShowExit(currentNode.ExitVisual, allGatesCleared: true);
        }

        // Wait for approach button (GateApproachButton or walk button to skip)
        if (approachUI != null)
        {
            var choice = await approachUI.WaitForGateSelection("出口");

            if (choice == ApproachChoice.Skip)
            {
                // Walk button = skip exit
                HandleExitSkipped();
                centralPresenter?.Hide();
                return new ExitResult(false, true);
            }
            // GateApproachButton click = show selection UI
        }

        // Click = show selection UI
        var selectedExit = await SelectExit(currentNode);
        centralPresenter?.Hide();

        if (selectedExit.HasValue)
        {
            await TriggerEvent(currentNode.OnExitEvent);
            TransitionTo(selectedExit.Value.ToNodeId, selectedExit.Value.Id);
            hasEnteredCurrentNode = false;
            return new ExitResult(true, false);
        }

        return new ExitResult(false, false);
    }

    /// <summary>
    /// Handle exit skip (walk button pressed)
    /// </summary>
    private void HandleExitSkipped()
    {
        if (currentNode.ExitSpawn?.Mode == ExitSpawnMode.Steps)
        {
            context.Counters.ResetTrackProgress();
        }
    }

    private readonly struct ExitResult
    {
        public bool Transitioned { get; }
        public bool Skipped { get; }

        public ExitResult(bool transitioned, bool skipped)
        {
            Transitioned = transitioned;
            Skipped = skipped;
        }
    }

    private void InitializeStartNode()
    {
        if (graph == null)
        {
            Debug.LogError("AreaController.InitializeStartNode: graph is null.");
            return;
        }

        if (!string.IsNullOrEmpty(context.WalkState.CurrentNodeId) &&
            graph.TryGetNode(context.WalkState.CurrentNodeId, out var savedNode))
        {
            currentNode = savedNode;
            return;
        }

        if (graph.TryGetNode(graph.StartNodeId, out var startNode))
        {
            currentNode = startNode;
            context.WalkState.CurrentNodeId = startNode.NodeId;
            return;
        }

        Debug.LogError("AreaController.InitializeStartNode: start node not found.");
    }

    private async UniTask<ResolvedExit?> SelectExit(NodeSO node)
    {
        if (exitSelectionUI == null)
        {
            Debug.LogError("AreaController.SelectExit: exitSelectionUI is null.");
            return null;
        }

        var exits = exitResolver.ResolveExits(
            node,
            context,
            node.ExitSelectionMode,
            node.MaxExitChoices);

        if (exits.Count == 0)
        {
            Debug.LogError("AreaController.SelectExit: no valid exits found.");
            return null;
        }

        var labels = new string[exits.Count];
        var ids = new string[exits.Count];
        for (var i = 0; i < exits.Count; i++)
        {
            var exit = exits[i];
            labels[i] = string.IsNullOrEmpty(exit.UILabel) ? exit.Id : exit.UILabel;
            ids[i] = i.ToString();
        }

        var selectedIndex = await exitSelectionUI.ShowExitChoices(labels, ids);
        if (selectedIndex < 0 || selectedIndex >= exits.Count) return null;

        return exits[selectedIndex];
    }

    private async UniTask HandleApproach(SideObjectEntry[] sidePair, EventDefinitionSO centralEvent, bool hasCentral)
    {
        if (sidePair == null && !hasCentral) return;

        if (approachUI == null)
        {
            Debug.LogWarning("AreaController.HandleApproach: approachUI is null.");
            return;
        }

        var leftEntry = sidePair != null && sidePair.Length > 0 ? sidePair[0] : null;
        var rightEntry = sidePair != null && sidePair.Length > 1 ? sidePair[1] : null;
        var leftLabel = leftEntry?.SideObject?.UILabel;
        var rightLabel = rightEntry?.SideObject?.UILabel;

        var choice = await approachUI.WaitForSelection(leftLabel, rightLabel, hasCentral, "中央");
        switch (choice)
        {
            case ApproachChoice.Left:
                if (leftEntry != null)
                {
                    sideObjectSelector.OnSideObjectSelected(leftEntry, leftEntry.CooldownSteps);
                    // Retain unselected side if enabled
                    if (currentNode.RetainUnselectedSide)
                    {
                        sideObjectSelector.SetPending(null, rightEntry?.SideObject?.Id);
                    }
                }
                await TriggerEvent(leftEntry?.SideObject?.EventDefinition);
                break;
            case ApproachChoice.Right:
                if (rightEntry != null)
                {
                    sideObjectSelector.OnSideObjectSelected(rightEntry, rightEntry.CooldownSteps);
                    // Retain unselected side if enabled
                    if (currentNode.RetainUnselectedSide)
                    {
                        sideObjectSelector.SetPending(leftEntry?.SideObject?.Id, null);
                    }
                }
                await TriggerEvent(rightEntry?.SideObject?.EventDefinition);
                break;
            case ApproachChoice.Center:
                await RunCentralEvent(centralEvent);
                break;
            case ApproachChoice.Skip:
                // Neither selected, clear pending
                sideObjectSelector.ClearPending();
                break;
        }

        centralPresenter?.Hide();
    }

    private void ShowApproachObjects(SideObjectEntry[] sidePair, bool hasCentral)
    {
        sidePresenter?.Show(sidePair);
        centralPresenter?.Show(currentNode.CentralVisual, hasCentral);
    }

    private async UniTask<EncounterOutcomeContext> HandleEncounterOutcome(EncounterSO encounter, BattleOutcome outcome, SideObjectEntry[] sidePair, EventDefinitionSO centralEvent, bool hasCentral)
    {
        await TriggerEncounterOutcome(encounter, outcome);

        if (outcome == BattleOutcome.Defeat || outcome == BattleOutcome.Escape)
        {
            context.Counters.Rewind(RewindStepsOnFail);
            sideObjectSelector.ClearPending();
            sidePair = sideObjectSelector.RollPair(currentNode.SideObjectTable, currentNode, context, isNodeEntry: false);
            centralEvent = currentNode.CentralEvent;
            hasCentral = centralEvent != null || currentNode.CentralVisual.HasVisual;
            ShowApproachObjects(sidePair, hasCentral);
        }

        return new EncounterOutcomeContext(sidePair, centralEvent, hasCentral);
    }

    private async UniTask<BattleResult> RunEncounter(EncounterSO encounter)
    {
        if (encounter == null) return BattleResult.None;
        if (context == null || context.BattleRunner == null)
        {
            Debug.LogWarning("AreaController.RunEncounter: BattleRunner is null.");
            return BattleResult.None;
        }

        return await context.BattleRunner.RunBattleAsync(new EncounterContext(encounter, context));
    }

    private UniTask TriggerEncounterOutcome(EncounterSO encounter, BattleOutcome outcome)
    {
        if (encounter == null) return UniTask.CompletedTask;
        switch (outcome)
        {
            case BattleOutcome.Victory:
                return TriggerEvent(encounter.OnWin);
            case BattleOutcome.Defeat:
                return TriggerEvent(encounter.OnLose);
            case BattleOutcome.Escape:
                return TriggerEvent(encounter.OnEscape);
            default:
                return UniTask.CompletedTask;
        }
    }

    private readonly struct EncounterOutcomeContext
    {
        public SideObjectEntry[] SidePair { get; }
        public EventDefinitionSO CentralEvent { get; }
        public bool HasCentral { get; }

        public EncounterOutcomeContext(SideObjectEntry[] sidePair, EventDefinitionSO centralEvent, bool hasCentral)
        {
            SidePair = sidePair;
            CentralEvent = centralEvent;
            HasCentral = hasCentral;
        }
    }

    private UniTask TriggerEvent(EventDefinitionSO definition)
    {
        if (definition == null) return UniTask.CompletedTask;
        if (eventHost == null)
        {
            Debug.LogWarning("AreaController.TriggerEvent: eventHost is null.");
            return UniTask.CompletedTask;
        }

        return eventHost.Trigger(definition);
    }

    /// <summary>
    /// CentralEventを実行。
    /// ズーム責務はNovelDialogueStep側に移行したため、ここではズームしない。
    /// CentralObjectRTをEventContextに渡すことで、NovelDialogueStepがズーム可能になる。
    /// </summary>
    private async UniTask RunCentralEvent(EventDefinitionSO centralEvent)
    {
        if (centralEvent == null)
        {
            return;
        }

        if (eventHost == null)
        {
            Debug.LogWarning("AreaController.RunCentralEvent: eventHost is null.");
            return;
        }

        // CentralObjectRTを含むEventContextを生成
        var centralRT = centralPresenter?.GetCurrentRectTransform();
        var eventContext = eventHost.CreateEventContext(centralRT);

        // TriggerWithContextで実行（ズーム責務はNovelDialogueStep側）
        var effects = await eventHost.TriggerWithContext(centralEvent, eventContext);

        // Effectを適用
        if (effects != null)
        {
            foreach (var effect in effects)
            {
                if (effect != null)
                {
                    await effect.Apply(context);
                }
            }
        }
    }

    private void TransitionTo(string nodeId, string exitId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            Debug.LogError("AreaController.TransitionTo: nodeId is empty.");
            return;
        }

        if (!graph.TryGetNode(nodeId, out var nextNode))
        {
            Debug.LogError($"AreaController.TransitionTo: node not found: {nodeId}");
            return;
        }

        currentNode = nextNode;
        context.WalkState.CurrentNodeId = nextNode.NodeId;
        context.WalkState.LastExitId = exitId;
        context.Counters.ResetNodeSteps();

        // Reset ProgressKey for the new node to keep it consistent with TrackProgress
        var progressKey = nextNode?.TrackConfig?.ProgressKey;
        if (!string.IsNullOrEmpty(progressKey))
        {
            context.SetCounter(progressKey, 0);
        }

        // Phase 2: Generate new seed for the next node
        context.WalkState.NodeSeed = GenerateNodeSeed(nextNode.NodeId);

        // Phase 2: Initialize gate resolver for new node
        InitializeGateResolverForCurrentNode();

        // Phase 2: Clear node-scoped anchors
        anchorManager.ClearAnchorsInScope(AnchorScope.Node);

        // Phase 1.1: Reset side object state on node transition
        sideObjectSelector.ClearPending();
        ConfigureSideObjectSelector();
    }

    /// <summary>
    /// Configure side object selector based on current node's table settings
    /// </summary>
    private void ConfigureSideObjectSelector()
    {
        var table = currentNode?.SideObjectTable;
        var varietyDepth = table?.VarietyDepth ?? 0;
        sideObjectSelector.Configure(varietyDepth);
    }

    /// <summary>
    /// Generate a deterministic seed for node-specific randomization.
    /// Uses FNV-1a hash for cross-platform stability (string.GetHashCode is not stable).
    /// </summary>
    private uint GenerateNodeSeed(string nodeId)
    {
        unchecked
        {
            var hash = (uint)context.Counters.GlobalSteps;
            if (!string.IsNullOrEmpty(nodeId))
            {
                hash ^= GetStableStringHash(nodeId) * 16777619u;
            }
            return hash;
        }
    }

    /// <summary>
    /// FNV-1a hash for strings. Stable across platforms and .NET versions.
    /// </summary>
    private static uint GetStableStringHash(string str)
    {
        if (string.IsNullOrEmpty(str)) return 0;

        unchecked
        {
            const uint fnvPrime = 16777619u;
            const uint fnvOffsetBasis = 2166136261u;

            var hash = fnvOffsetBasis;
            for (int i = 0; i < str.Length; i++)
            {
                hash ^= str[i];
                hash *= fnvPrime;
            }
            return hash;
        }
    }
}
