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
