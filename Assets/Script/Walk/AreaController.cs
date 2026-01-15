using System.Collections.Generic;
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
    private readonly SideObjectResolver sideObjectResolver = new SideObjectResolver();
    private readonly EncounterResolver encounterResolver = new EncounterResolver();
    private const int RewindStepsOnFail = 10;
    private NodeSO currentNode;
    private bool hasEnteredCurrentNode;

    public AreaController(
        FlowGraphSO graph,
        GameContext context,
        IExitSelectionUI exitSelectionUI,
        WalkApproachUI approachUI,
        EventHost eventHost,
        SideObjectPresenter sidePresenter,
        CentralObjectPresenter centralPresenter)
    {
        this.graph = graph;
        this.context = context;
        this.exitSelectionUI = exitSelectionUI;
        this.approachUI = approachUI;
        this.eventHost = eventHost;
        this.sidePresenter = sidePresenter;
        this.centralPresenter = centralPresenter;
        InitializeStartNode();
        hasEnteredCurrentNode = false;
    }

    public string CurrentNodeId => currentNode != null ? currentNode.NodeId : null;
    public NodeSO CurrentNode => currentNode;

    public void SetApproachUI(WalkApproachUI ui)
    {
        approachUI = ui;
    }

    public async UniTask WalkStep()
    {
        if (currentNode == null)
        {
            Debug.LogError("AreaController.WalkStep: currentNode is null.");
            return;
        }

        if (!hasEnteredCurrentNode)
        {
            await TriggerEvent(currentNode.OnEnterEvent);
            hasEnteredCurrentNode = true;
        }

        var nextCounters = context.Counters.PeekNext(1);

        var sidePair = sideObjectResolver.RollPair(currentNode.SideObjectTable);
        var centralEvent = currentNode.CentralEvent;
        var hasCentral = centralEvent != null || currentNode.CentralVisual.HasVisual;

        ShowApproachObjects(sidePair, hasCentral);

        var encounterResult = encounterResolver.Resolve(currentNode.EncounterTable, context, skipRoll: false);
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

        await HandleApproach(sidePair, centralEvent, hasCentral);

        if (currentNode.ExitSpawn != null && currentNode.ExitSpawn.ShouldSpawn(nextCounters))
        {
            var selectedExit = await SelectExit(currentNode);
            context.Counters.Advance(1);
            if (selectedExit != null)
            {
                await TriggerEvent(currentNode.OnExitEvent);
                TransitionTo(selectedExit.ToNodeId, selectedExit.Id);
                hasEnteredCurrentNode = false;
                return;
            }
            if (currentNode.ExitSpawn.Mode == ExitSpawnMode.Steps)
            {
                context.Counters.ResetNodeSteps();
            }
            return;
        }

        context.Counters.Advance(1);
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

    private async UniTask<ExitCandidate> SelectExit(NodeSO node)
    {
        if (exitSelectionUI == null)
        {
            Debug.LogError("AreaController.SelectExit: exitSelectionUI is null.");
            return null;
        }

        if (node.Exits == null || node.Exits.Length == 0)
        {
            Debug.LogError("AreaController.SelectExit: no exits on node.");
            return null;
        }

        var exits = new List<ExitCandidate>();
        for (var i = 0; i < node.Exits.Length; i++)
        {
            var candidate = node.Exits[i];
            if (candidate == null) continue;
            exits.Add(candidate);
        }

        if (exits.Count == 0)
        {
            Debug.LogError("AreaController.SelectExit: all exits are null.");
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

        var leftLabel = sidePair != null && sidePair.Length > 0 ? sidePair[0]?.SideObject?.UILabel : null;
        var rightLabel = sidePair != null && sidePair.Length > 1 ? sidePair[1]?.SideObject?.UILabel : null;

        var choice = await approachUI.WaitForSelection(leftLabel, rightLabel, hasCentral, "中央");
        switch (choice)
        {
            case ApproachChoice.Left:
                await TriggerEvent(sidePair != null && sidePair.Length > 0 ? sidePair[0]?.SideObject?.EventDefinition : null);
                break;
            case ApproachChoice.Right:
                await TriggerEvent(sidePair != null && sidePair.Length > 1 ? sidePair[1]?.SideObject?.EventDefinition : null);
                break;
            case ApproachChoice.Center:
                await TriggerEvent(centralEvent);
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
            sidePair = sideObjectResolver.RollPair(currentNode.SideObjectTable);
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
    }
}
