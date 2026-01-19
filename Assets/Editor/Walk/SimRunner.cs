using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class SimRunner
{
    public struct SimResult
    {
        public int TotalSteps;
        public int EncounterCount;
        public float EncounterRate;
        public Dictionary<string, int> SideObjectCounts;
        public Dictionary<string, int> NodeVisitCounts;
        public List<string> Errors;
    }

    // シミュレーション用のシード付きRNG
    // Note: 出口選択は決定的だが、SideObjectSelector/EncounterResolverは
    // 内部でRandomEx.Sharedを使用するため完全な再現性は保証されない
    private System.Random rng;

    public SimResult Run(FlowGraphSO graph, int steps, uint seed = 0)
    {
        var result = new SimResult
        {
            TotalSteps = steps,
            SideObjectCounts = new Dictionary<string, int>(),
            NodeVisitCounts = new Dictionary<string, int>(),
            Errors = new List<string>()
        };

        if (graph == null)
        {
            result.Errors.Add("Graph is null.");
            return result;
        }

        if (!graph.TryGetNode(graph.StartNodeId, out var startNode))
        {
            result.Errors.Add($"Start node '{graph.StartNodeId}' not found.");
            return result;
        }

        // シード付きRNGを初期化（決定的シミュレーション用）
        int actualSeed = seed == 0 ? (int)(DateTime.Now.Ticks & 0x7FFFFFFF) : (int)seed;
        rng = new System.Random(actualSeed);

        // Create simulation context
        var context = new GameContext(null);
        context.WalkState.CurrentNodeId = startNode.NodeId;

        var encounterResolver = new EncounterResolver();
        var sideObjectSelector = new SideObjectSelector();
        var exitResolver = new ExitResolver();
        var currentNode = startNode;

        for (int step = 0; step < steps; step++)
        {
            if (currentNode == null)
            {
                result.Errors.Add($"Simulation ended early at step {step}: current node is null.");
                break;
            }

            // Record node visit
            var nodeId = currentNode.NodeId;
            if (!result.NodeVisitCounts.ContainsKey(nodeId))
            {
                result.NodeVisitCounts[nodeId] = 0;
            }
            result.NodeVisitCounts[nodeId]++;

            // Advance counters
            context.Counters.Advance(1);
            sideObjectSelector.AdvanceStep();
            context.AdvanceEncounterOverlays();

            // Roll side objects
            var sidePair = sideObjectSelector.RollPair(currentNode.SideObjectTable, currentNode, context);
            if (sidePair != null)
            {
                foreach (var entry in sidePair)
                {
                    if (entry?.SideObject == null) continue;
                    var soId = entry.SideObject.Id;
                    if (string.IsNullOrEmpty(soId)) soId = entry.SideObject.name;
                    if (!result.SideObjectCounts.ContainsKey(soId))
                    {
                        result.SideObjectCounts[soId] = 0;
                    }
                    result.SideObjectCounts[soId]++;
                }
            }

            // Roll encounter
            var encounterResult = encounterResolver.Resolve(
                currentNode.EncounterTable,
                context,
                skipRoll: false,
                currentNode.EncounterRateMultiplier);

            if (encounterResult.Triggered)
            {
                result.EncounterCount++;
            }

            // Check for exit (simplified - no spawn rule check)
            var exits = exitResolver.ResolveExits(
                currentNode,
                graph,
                context,
                currentNode.ExitSelectionMode,
                currentNode.MaxExitChoices);

            if (exits.Count > 0)
            {
                // Pick random exit（シード付きRNGで決定的に選択）
                var exitIndex = rng.Next(0, exits.Count);
                var chosenExit = exits[exitIndex];

                if (graph.TryGetNode(chosenExit.ToNodeId, out var nextNode))
                {
                    currentNode = nextNode;
                    context.WalkState.CurrentNodeId = nextNode.NodeId;
                    context.Counters.ResetNodeSteps();
                }
            }
        }

        result.EncounterRate = steps > 0 ? (float)result.EncounterCount / steps : 0f;
        return result;
    }
}

public class SimRunnerWindow : EditorWindow
{
    private FlowGraphSO graph;
    private int steps = 1000;
    private uint seed = 0;
    private SimRunner.SimResult lastResult;
    private Vector2 scrollPos;

    [MenuItem("Walk/Simulation Runner")]
    public static void ShowWindow()
    {
        GetWindow<SimRunnerWindow>("Walk Simulator");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Walk Simulation", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        graph = EditorGUILayout.ObjectField("Graph", graph, typeof(FlowGraphSO), false) as FlowGraphSO;
        steps = EditorGUILayout.IntField("Steps", steps);
        seed = (uint)EditorGUILayout.IntField("Seed (0=random)", (int)seed);

        EditorGUILayout.Space();

        if (GUILayout.Button("Run Simulation"))
        {
            if (graph != null)
            {
                var runner = new SimRunner();
                lastResult = runner.Run(graph, steps, seed);
            }
        }

        EditorGUILayout.Space();

        if (lastResult.TotalSteps > 0)
        {
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Steps: {lastResult.TotalSteps}");
            EditorGUILayout.LabelField($"Encounters: {lastResult.EncounterCount} ({lastResult.EncounterRate:P1})");

            if (lastResult.Errors.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Errors:", EditorStyles.boldLabel);
                foreach (var error in lastResult.Errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            if (lastResult.NodeVisitCounts.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Node Visits:", EditorStyles.boldLabel);
                foreach (var kvp in lastResult.NodeVisitCounts)
                {
                    float pct = (float)kvp.Value / lastResult.TotalSteps;
                    EditorGUILayout.LabelField($"  {kvp.Key}: {kvp.Value} ({pct:P1})");
                }
            }

            if (lastResult.SideObjectCounts.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Side Object Distribution:", EditorStyles.boldLabel);
                var total = 0;
                foreach (var kvp in lastResult.SideObjectCounts)
                {
                    total += kvp.Value;
                }
                foreach (var kvp in lastResult.SideObjectCounts)
                {
                    float pct = total > 0 ? (float)kvp.Value / total : 0f;
                    EditorGUILayout.LabelField($"  {kvp.Key}: {kvp.Value} ({pct:P1})");
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
