using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class WalkTestSetup
{
    private const string RootFolder = "Assets/ScriptableObject/WalkTest";

    [MenuItem("Tools/Walk/Create P0 Test Assets")]
    public static void CreateP0TestAssets()
    {
        EnsureFolder("Assets", "ScriptableObject");
        EnsureFolder("Assets/ScriptableObject", "WalkTest");

        var nodeA = CreateAsset<NodeSO>($"{RootFolder}/Node_A.asset");
        var nodeB = CreateAsset<NodeSO>($"{RootFolder}/Node_B.asset");
        ConfigureNodeA(nodeA);
        ConfigureNodeB(nodeB);

        var graph = CreateAsset<FlowGraphSO>($"{RootFolder}/FlowGraph_Test.asset");
        ConfigureGraph(graph, nodeA, nodeB);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = graph;
        Debug.Log($"P0 test assets created: {RootFolder}");
    }

    [MenuItem("Tools/Walk/Create P1 Test Assets")]
    public static void CreateP1TestAssets()
    {
        CreateP0TestAssets();

        var sideEvent = CreateAsset<EventDefinitionSO>($"{RootFolder}/Event_Side.asset");
        ConfigureEventDefinition(sideEvent, "サイドイベントが発生");

        var centerEvent = CreateAsset<EventDefinitionSO>($"{RootFolder}/Event_Center.asset");
        ConfigureEventDefinition(centerEvent, "中央イベントが発生");

        var sideLeftA = CreateAsset<SideObjectSO>($"{RootFolder}/Side_Left_A.asset");
        var leftPrefabA = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/SideObjects/Left_Tower1.prefab");
        ConfigureSideObject(sideLeftA, "side_left_a", "<", sideEvent, leftPrefabA, null);

        var sideLeftB = CreateAsset<SideObjectSO>($"{RootFolder}/Side_Left_B.asset");
        var leftPrefabB = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/SideObjects/Left_RadioToer.prefab");
        ConfigureSideObject(sideLeftB, "side_left_b", "<", sideEvent, leftPrefabB, null);

        var sideRight = CreateAsset<SideObjectSO>($"{RootFolder}/Side_Right.asset");
        var rightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/SideObjects/Right_Tower1 1.prefab");
        ConfigureSideObject(sideRight, "side_right", ">", sideEvent, null, rightPrefab);

        var sideTable = CreateAsset<SideObjectTableSO>($"{RootFolder}/SideTable.asset");
        ConfigureSideTable(sideTable, sideLeftA, sideLeftB, sideRight);

        var nodeA = AssetDatabase.LoadAssetAtPath<NodeSO>($"{RootFolder}/Node_A.asset");
        ConfigureNodeAForP1(nodeA, sideTable, centerEvent);
        var nodeB = AssetDatabase.LoadAssetAtPath<NodeSO>($"{RootFolder}/Node_B.asset");
        ConfigureNodeBForP1(nodeB, sideTable);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = nodeA;
        Debug.Log($"P1 test assets created: {RootFolder}");
    }

    [MenuItem("Tools/Walk/Create P2 Test Assets")]
    public static void CreateP2TestAssets()
    {
        CreateP1TestAssets();

        var encounterWin = CreateAsset<EventDefinitionSO>($"{RootFolder}/Event_Encounter_Win.asset");
        ConfigureEventDefinition(encounterWin, "Encounter win");

        var encounterLose = CreateAsset<EventDefinitionSO>($"{RootFolder}/Event_Encounter_Lose.asset");
        ConfigureEventDefinition(encounterLose, "Encounter lose");

        var encounterEscape = CreateAsset<EventDefinitionSO>($"{RootFolder}/Event_Encounter_Escape.asset");
        ConfigureEventDefinition(encounterEscape, "Encounter escape");

        var encounter = CreateAsset<EncounterSO>($"{RootFolder}/Encounter_Test.asset");
        ConfigureEncounter(encounter, "encounter_test", "Encounter", encounterWin, encounterLose, encounterEscape);

        var encounterTable = CreateAsset<EncounterTableSO>($"{RootFolder}/EncounterTable_Test.asset");
        ConfigureEncounterTable(encounterTable, encounter);

        var nodeA = AssetDatabase.LoadAssetAtPath<NodeSO>($"{RootFolder}/Node_A.asset");
        ConfigureNodeAForP2(nodeA, encounterTable);
        var nodeB = AssetDatabase.LoadAssetAtPath<NodeSO>($"{RootFolder}/Node_B.asset");
        ConfigureNodeBForP2(nodeB, encounterTable);

        TryPopulateEncounterFromStage(encounter);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = encounterTable;
        Debug.Log($"P2 test assets created: {RootFolder}");
    }

    [MenuItem("Tools/Walk/Create P2 Gate Test Assets")]
    public static void CreateP2GateTestAssets()
    {
        CreateP2TestAssets();

        // Create condition assets
        var gateCondition = CreateAsset<HasFlagCondition>($"{RootFolder}/Condition_GateTest.asset");
        ConfigureHasFlagCondition(gateCondition, "gateTestFlag", true);

        // Create anchor effect assets
        var createAnchor = CreateAsset<CreateAnchorEffect>($"{RootFolder}/Effect_CreateAnchor.asset");
        ConfigureCreateAnchorEffect(createAnchor, "testAnchor", AnchorScope.Region);

        var rewindAnchor = CreateAsset<RewindToAnchorEffect>($"{RootFolder}/Effect_RewindAnchor.asset");
        ConfigureRewindToAnchorEffect(rewindAnchor, "testAnchor", RewindMode.PositionAndState);

        // Create gate event
        var gateEvent = CreateAsset<EventDefinitionSO>($"{RootFolder}/Event_Gate.asset");
        ConfigureEventDefinition(gateEvent, "ゲートに到達しました");

        // Configure NodeA with gates and trackConfig
        var nodeA = AssetDatabase.LoadAssetAtPath<NodeSO>($"{RootFolder}/Node_A.asset");
        ConfigureNodeAWithGates(nodeA, gateCondition, createAnchor, rewindAnchor, gateEvent);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = nodeA;
        Debug.Log($"P2 Gate test assets created: {RootFolder}");
    }

    private const string MigrationFolder = "Assets/ScriptableObject/WalkMigration";
    private const string Phase2Folder = "Assets/ScriptableObject/WalkMigration/Phase2";

    [MenuItem("Tools/Walk/Setup P2 Gate on WalkMigration")]
    public static void SetupP2GateOnWalkMigration()
    {
        EnsureFolder(MigrationFolder, "Phase2");

        // Create condition assets
        var gateCondition = CreateAsset<HasFlagCondition>($"{Phase2Folder}/Condition_GateTest.asset");
        ConfigureHasFlagCondition(gateCondition, "gateTestFlag", true);

        // Create anchor effect assets
        var createAnchor = CreateAsset<CreateAnchorEffect>($"{Phase2Folder}/Effect_CreateAnchor.asset");
        ConfigureCreateAnchorEffect(createAnchor, "testAnchor", AnchorScope.Region);

        var rewindAnchor = CreateAsset<RewindToAnchorEffect>($"{Phase2Folder}/Effect_RewindAnchor.asset");
        ConfigureRewindToAnchorEffect(rewindAnchor, "testAnchor", RewindMode.PositionAndState);

        // Create gate event
        var gateEvent = CreateAsset<EventDefinitionSO>($"{Phase2Folder}/Event_Gate.asset");
        ConfigureEventDefinition(gateEvent, "ゲートに到達しました");

        // Configure existing Node_0__________ with gates and trackConfig
        var existingNode = AssetDatabase.LoadAssetAtPath<NodeSO>($"{MigrationFolder}/0__________/Node_0__________.asset");
        if (existingNode != null)
        {
            ConfigureNodeAWithGates(existingNode, gateCondition, createAnchor, rewindAnchor, gateEvent);
            Debug.Log($"P2 Gate configured on: {existingNode.name}");
        }
        else
        {
            Debug.LogWarning("Node_0__________ not found in WalkMigration folder");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = existingNode;
        Debug.Log($"P2 Gate assets created in: {Phase2Folder}");
    }

    private static void ConfigureHasFlagCondition(HasFlagCondition condition, string flagKey, bool expectedValue)
    {
        if (condition == null) return;
        var serialized = new SerializedObject(condition);
        serialized.FindProperty("flagKey").stringValue = flagKey;
        serialized.FindProperty("expectedValue").boolValue = expectedValue;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(condition);
    }

    private static void ConfigureCreateAnchorEffect(CreateAnchorEffect effect, string anchorId, AnchorScope scope)
    {
        if (effect == null) return;
        var serialized = new SerializedObject(effect);
        serialized.FindProperty("anchorId").stringValue = anchorId;
        serialized.FindProperty("scope").enumValueIndex = (int)scope;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(effect);
    }

    private static void ConfigureRewindToAnchorEffect(RewindToAnchorEffect effect, string anchorId, RewindMode mode)
    {
        if (effect == null) return;
        var serialized = new SerializedObject(effect);
        serialized.FindProperty("anchorId").stringValue = anchorId;
        serialized.FindProperty("mode").enumValueIndex = (int)mode;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(effect);
    }

    private static void ConfigureNodeAWithGates(NodeSO node, HasFlagCondition gateCondition, CreateAnchorEffect createAnchor, RewindToAnchorEffect rewindAnchor, EventDefinitionSO gateEvent)
    {
        if (node == null) return;
        var serialized = new SerializedObject(node);

        // Configure TrackConfig
        var trackConfig = serialized.FindProperty("trackConfig");
        trackConfig.FindPropertyRelative("length").intValue = 50;
        trackConfig.FindPropertyRelative("stepDelta").intValue = 1;
        trackConfig.FindPropertyRelative("progressKey").stringValue = "testProgress";

        // Configure Gates
        var gates = serialized.FindProperty("gates");
        gates.arraySize = 2;

        // Gate 1: Hard block at step 20, requires flag condition, creates anchor on pass
        var gate1 = gates.GetArrayElementAtIndex(0);
        gate1.FindPropertyRelative("gateId").stringValue = "gate_checkpoint";
        gate1.FindPropertyRelative("order").intValue = 0;

        var pos1 = gate1.FindPropertyRelative("positionSpec");
        pos1.FindPropertyRelative("type").enumValueIndex = (int)GatePositionSpec.PositionType.AbsSteps;
        pos1.FindPropertyRelative("absSteps").intValue = 20;

        var conditions1 = gate1.FindPropertyRelative("passConditions");
        conditions1.arraySize = 1;
        conditions1.GetArrayElementAtIndex(0).objectReferenceValue = gateCondition;

        var onPass1 = gate1.FindPropertyRelative("onPass");
        onPass1.arraySize = 1;
        onPass1.GetArrayElementAtIndex(0).objectReferenceValue = createAnchor;

        gate1.FindPropertyRelative("onFail").arraySize = 0;
        gate1.FindPropertyRelative("gateEvent").objectReferenceValue = gateEvent;
        gate1.FindPropertyRelative("eventTiming").enumValueIndex = (int)GateEventTiming.OnAppear;
        gate1.FindPropertyRelative("blockingMode").enumValueIndex = (int)GateBlockingMode.HardBlock;
        gate1.FindPropertyRelative("repeatable").boolValue = false;
        gate1.FindPropertyRelative("cooldownSteps").intValue = 0;
        gate1.FindPropertyRelative("resetOnSkip").boolValue = true;
        gate1.FindPropertyRelative("resetOnFail").boolValue = true;
        gate1.FindPropertyRelative("resetTarget").enumValueIndex = (int)GateResetTarget.NodeStepsOnly;

        // Gate 2: Soft block at 80%, repeatable, rewinds to anchor on fail
        var gate2 = gates.GetArrayElementAtIndex(1);
        gate2.FindPropertyRelative("gateId").stringValue = "gate_loop";
        gate2.FindPropertyRelative("order").intValue = 1;

        var pos2 = gate2.FindPropertyRelative("positionSpec");
        pos2.FindPropertyRelative("type").enumValueIndex = (int)GatePositionSpec.PositionType.Percent;
        pos2.FindPropertyRelative("percent").floatValue = 0.8f;

        gate2.FindPropertyRelative("passConditions").arraySize = 0;

        gate2.FindPropertyRelative("onPass").arraySize = 0;

        var onFail2 = gate2.FindPropertyRelative("onFail");
        onFail2.arraySize = 1;
        onFail2.GetArrayElementAtIndex(0).objectReferenceValue = rewindAnchor;

        gate2.FindPropertyRelative("gateEvent").objectReferenceValue = null;
        gate2.FindPropertyRelative("eventTiming").enumValueIndex = (int)GateEventTiming.OnPass;
        gate2.FindPropertyRelative("blockingMode").enumValueIndex = (int)GateBlockingMode.SoftBlock;
        gate2.FindPropertyRelative("repeatable").boolValue = true;
        gate2.FindPropertyRelative("cooldownSteps").intValue = 5;
        gate2.FindPropertyRelative("resetOnSkip").boolValue = false;
        gate2.FindPropertyRelative("resetOnFail").boolValue = false;
        gate2.FindPropertyRelative("resetTarget").enumValueIndex = (int)GateResetTarget.TrackProgressOnly;

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(node);
    }

    private static void EnsureFolder(string parent, string child)
    {
        var path = $"{parent}/{child}";
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(parent, child);
    }

    private static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void ConfigureNodeA(NodeSO node)
    {
        var serialized = new SerializedObject(node);
        serialized.FindProperty("nodeId").stringValue = "Node_A";
        serialized.FindProperty("displayName").stringValue = "Node A";

        var exitSpawn = serialized.FindProperty("exitSpawn");
        exitSpawn.FindPropertyRelative("mode").enumValueIndex = (int)ExitSpawnMode.Steps;
        exitSpawn.FindPropertyRelative("steps").intValue = 3;
        exitSpawn.FindPropertyRelative("rate").floatValue = 1f;

        var exits = serialized.FindProperty("exits");
        exits.arraySize = 1;
        var exit = exits.GetArrayElementAtIndex(0);
        exit.FindPropertyRelative("id").stringValue = "exit_to_B";
        exit.FindPropertyRelative("toNodeId").stringValue = "Node_B";
        exit.FindPropertyRelative("uiLabel").stringValue = "Next";

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(node);
    }

    private static void ConfigureNodeAForP1(NodeSO node, SideObjectTableSO sideTable, EventDefinitionSO centerEvent)
    {
        if (node == null) return;
        var serialized = new SerializedObject(node);
        serialized.FindProperty("sideObjectTable").objectReferenceValue = sideTable;
        serialized.FindProperty("centralEvent").objectReferenceValue = centerEvent;
        var visual = serialized.FindProperty("centralVisual");
        visual.FindPropertyRelative("sprite").objectReferenceValue = null;
        visual.FindPropertyRelative("size").vector2Value = new Vector2(160f, 160f);
        visual.FindPropertyRelative("offset").vector2Value = new Vector2(0f, 120f);
        visual.FindPropertyRelative("tint").colorValue = Color.white;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(node);
    }

    private static void ConfigureNodeBForP1(NodeSO node, SideObjectTableSO sideTable)
    {
        if (node == null) return;
        var serialized = new SerializedObject(node);
        serialized.FindProperty("sideObjectTable").objectReferenceValue = sideTable;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(node);
    }

    private static void ConfigureNodeAForP2(NodeSO node, EncounterTableSO encounterTable)
    {
        if (node == null) return;
        var serialized = new SerializedObject(node);
        serialized.FindProperty("encounterTable").objectReferenceValue = encounterTable;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(node);
    }

    private static void ConfigureNodeBForP2(NodeSO node, EncounterTableSO encounterTable)
    {
        if (node == null) return;
        var serialized = new SerializedObject(node);
        serialized.FindProperty("encounterTable").objectReferenceValue = encounterTable;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(node);
    }

    private static void ConfigureNodeB(NodeSO node)
    {
        var serialized = new SerializedObject(node);
        serialized.FindProperty("nodeId").stringValue = "Node_B";
        serialized.FindProperty("displayName").stringValue = "Node B";

        var exitSpawn = serialized.FindProperty("exitSpawn");
        exitSpawn.FindPropertyRelative("mode").enumValueIndex = (int)ExitSpawnMode.None;
        exitSpawn.FindPropertyRelative("steps").intValue = 0;
        exitSpawn.FindPropertyRelative("rate").floatValue = 0f;

        var exits = serialized.FindProperty("exits");
        exits.arraySize = 0;

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(node);
    }

    private static void ConfigureGraph(FlowGraphSO graph, NodeSO nodeA, NodeSO nodeB)
    {
        var serialized = new SerializedObject(graph);
        serialized.FindProperty("startNodeId").stringValue = "Node_A";

        var nodes = serialized.FindProperty("nodes");
        nodes.arraySize = 2;
        nodes.GetArrayElementAtIndex(0).objectReferenceValue = nodeA;
        nodes.GetArrayElementAtIndex(1).objectReferenceValue = nodeB;

        var edges = serialized.FindProperty("edges");
        edges.arraySize = 0;

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(graph);
    }

    private static void ConfigureEventDefinition(EventDefinitionSO definition, string message)
    {
        var serialized = new SerializedObject(definition);
        var steps = serialized.FindProperty("steps");
        steps.arraySize = 1;
        var step = steps.GetArrayElementAtIndex(0);
        step.FindPropertyRelative("message").stringValue = message;
        step.FindPropertyRelative("choices").arraySize = 0;
        step.FindPropertyRelative("effects").arraySize = 0;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(definition);
    }

    private static void ConfigureEncounter(EncounterSO encounter, string id, string label, EventDefinitionSO onWin, EventDefinitionSO onLose, EventDefinitionSO onEscape)
    {
        if (encounter == null) return;
        var serialized = new SerializedObject(encounter);
        serialized.FindProperty("id").stringValue = id;
        serialized.FindProperty("uiLabel").stringValue = label;
        serialized.FindProperty("enemyCount").intValue = 2;
        serialized.FindProperty("escapeRate").floatValue = 50f;
        serialized.FindProperty("onWin").objectReferenceValue = onWin;
        serialized.FindProperty("onLose").objectReferenceValue = onLose;
        serialized.FindProperty("onEscape").objectReferenceValue = onEscape;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(encounter);
    }

    private static void ConfigureEncounterTable(EncounterTableSO table, EncounterSO encounter)
    {
        if (table == null) return;
        var serialized = new SerializedObject(table);
        serialized.FindProperty("baseRate").floatValue = 1f;
        serialized.FindProperty("cooldownSteps").intValue = 0;
        serialized.FindProperty("graceSteps").intValue = 0;
        serialized.FindProperty("pityIncrement").floatValue = 0f;
        serialized.FindProperty("pityMax").floatValue = 1f;
        var entries = serialized.FindProperty("entries");
        entries.arraySize = 1;
        var entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("encounter").objectReferenceValue = encounter;
        entry.FindPropertyRelative("weight").floatValue = 1f;
        entry.FindPropertyRelative("conditions").arraySize = 0;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(table);
    }

    private static void TryPopulateEncounterFromStage(EncounterSO encounter)
    {
        if (encounter == null) return;
        var stages = Object.FindObjectOfType<Stages>();
        if (stages == null)
        {
            Debug.LogWarning("P2 test assets: Stages object not found in scene.");
            return;
        }

        var stageDates = stages.StageDates;
        if (stageDates == null || stageDates.Count == 0)
        {
            Debug.LogWarning("P2 test assets: StageDates is empty.");
            return;
        }

        StageCut sourceCut = null;
        var sourceEnemies = new List<NormalEnemy>();
        foreach (var stage in stageDates)
        {
            if (stage == null || stage.CutArea == null) continue;
            foreach (var cut in stage.CutArea)
            {
                if (cut == null || cut.EnemyList == null || cut.EnemyList.Count == 0) continue;
                sourceCut = cut;
                sourceEnemies.AddRange(cut.EnemyList.Where(enemy => enemy != null));
                break;
            }
            if (sourceCut != null) break;
        }

        if (sourceCut == null || sourceEnemies.Count == 0)
        {
            Debug.LogWarning("P2 test assets: No enemies found in StageDates.");
            return;
        }

        var clonedEnemies = new List<NormalEnemy>();
        foreach (var enemy in sourceEnemies)
        {
            clonedEnemies.Add(enemy.DeepCopy());
        }

        var field = typeof(EncounterSO).GetField("enemyList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            Debug.LogWarning("P2 test assets: EncounterSO.enemyList field not found.");
            return;
        }

        field.SetValue(encounter, clonedEnemies);
        EditorUtility.SetDirty(encounter);
        Debug.Log($"P2 test assets: Copied {clonedEnemies.Count} enemies from stage {sourceCut.AreaName}.");
    }

    private static void ConfigureSideObject(SideObjectSO sideObject, string id, string label, EventDefinitionSO definition, GameObject leftPrefab, GameObject rightPrefab)
    {
        var serialized = new SerializedObject(sideObject);
        serialized.FindProperty("id").stringValue = id;
        serialized.FindProperty("uiLabel").stringValue = label;
        serialized.FindProperty("prefabLeft").objectReferenceValue = leftPrefab;
        serialized.FindProperty("prefabRight").objectReferenceValue = rightPrefab;
        serialized.FindProperty("eventDefinition").objectReferenceValue = definition;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(sideObject);
    }

    private static void ConfigureSideTable(SideObjectTableSO table, params SideObjectSO[] sideObjects)
    {
        var serialized = new SerializedObject(table);
        var entries = serialized.FindProperty("entries");
        entries.arraySize = sideObjects != null ? sideObjects.Length : 0;
        if (sideObjects != null)
        {
            for (var i = 0; i < sideObjects.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("sideObject").objectReferenceValue = sideObjects[i];
                entry.FindPropertyRelative("weight").floatValue = 1f;
            }
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(table);
    }
}
