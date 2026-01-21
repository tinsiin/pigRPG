using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WalkingSystemManager))]
public sealed class WalkingSystemManagerEditor : Editor
{
    private WalkConditionCollector collector;
    private bool showDebugFoldout = true;
    private bool showTagsFoldout = true;
    private bool showFlagsFoldout = true;
    private bool showCountersFoldout = true;
    private bool showOverlaysFoldout = true;

    private string newTagInput = "";
    private string newFlagInput = "";
    private string newCounterKey = "";
    private int newCounterValue = 0;
    private string newOverlayId = "";
    private float newOverlayMultiplier = 1f;
    private int newOverlaySteps = -1;

    private HashSet<string> manualTags = new();
    private HashSet<string> manualFlags = new();
    private HashSet<string> manualCounters = new();

    public override void OnInspectorGUI()
    {
        // PlayMode中は通常フィールドを編集禁止
        if (Application.isPlaying)
        {
            GUI.enabled = false;
        }

        DrawDefaultInspector();

        GUI.enabled = true;

        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            DrawDebugSection();
        }
    }

    private GameContext GetGameContext()
    {
        var manager = (WalkingSystemManager)target;
        return manager.GameContext;
    }

    private FlowGraphSO GetRootGraph()
    {
        var prop = serializedObject.FindProperty("rootGraph");
        return prop?.objectReferenceValue as FlowGraphSO;
    }

    private void DrawDebugSection()
    {
        showDebugFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showDebugFoldout, "Debug (PlayMode)");
        if (!showDebugFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        var context = GetGameContext();
        if (context == null)
        {
            EditorGUILayout.HelpBox("GameContext未初期化（歩行システム起動前）", MessageType.Info);
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        // Ensure collector is initialized
        if (collector == null)
        {
            collector = new WalkConditionCollector();
            var graph = GetRootGraph();
            if (graph != null)
            {
                collector.CollectFromFlowGraph(graph);
            }
        }

        // Status
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        var currentNode = context.CurrentNode;
        EditorGUILayout.LabelField("Node", currentNode != null ? currentNode.DisplayName : "(none)");
        EditorGUILayout.LabelField("Steps",
            $"Global={context.Counters.GlobalSteps} Node={context.Counters.NodeSteps} Track={context.Counters.TrackProgress}");

        // Encounter Multiplier breakdown
        float nodeMultiplier = currentNode != null ? currentNode.EncounterRateMultiplier : 1f;
        float overlayMultiplier = context.GetEncounterMultiplier();
        float combinedMultiplier = nodeMultiplier * overlayMultiplier;
        EditorGUILayout.LabelField("Encounter Multiplier",
            $"Node x{nodeMultiplier:F2} × Overlay x{overlayMultiplier:F2} = x{combinedMultiplier:F2}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // Tags
        DrawTagsSection(context);

        EditorGUILayout.Space();

        // Flags
        DrawFlagsSection(context);

        EditorGUILayout.Space();

        // Counters
        DrawCountersSection(context);

        EditorGUILayout.Space();

        // Overlays
        DrawOverlaysSection(context);

        EditorGUILayout.Space();

        // Buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Conditions"))
        {
            var graph = GetRootGraph();
            if (graph != null)
            {
                collector.CollectFromFlowGraph(graph);
            }
        }
        if (GUILayout.Button("Clear Debug State"))
        {
            if (EditorUtility.DisplayDialog("Clear Debug State",
                "タグ/フラグ/カウンター/オーバーレイをすべてクリアします。\nゲーム中に設定された値も消去されますが、よろしいですか？",
                "クリア", "キャンセル"))
            {
                ClearDebugState(context);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndFoldoutHeaderGroup();

        // Force repaint for live updates
        Repaint();
    }

    private void DrawTagsSection(GameContext context)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        showTagsFoldout = EditorGUILayout.Foldout(showTagsFoldout, "Tags", true);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (showTagsFoldout)
        {
            var currentTags = context.GetAllTags();
            var allTags = new HashSet<string>(collector.UsedTags.Keys);
            allTags.UnionWith(currentTags);
            allTags.UnionWith(manualTags);

            foreach (var tag in allTags.OrderBy(t => t))
            {
                var hasTag = currentTags.Contains(tag);
                var usageCount = collector.UsedTags.TryGetValue(tag, out var count) ? count : 0;
                var isManual = manualTags.Contains(tag) && usageCount == 0;

                EditorGUILayout.BeginHorizontal();
                var newValue = EditorGUILayout.Toggle(hasTag, GUILayout.Width(20));
                EditorGUILayout.LabelField(tag, GUILayout.ExpandWidth(true));
                if (usageCount > 0)
                {
                    EditorGUILayout.LabelField($"({usageCount}箇所)", GUILayout.Width(60));
                }
                else if (isManual)
                {
                    EditorGUILayout.LabelField("(手動)", GUILayout.Width(60));
                }
                EditorGUILayout.EndHorizontal();

                if (newValue != hasTag)
                {
                    if (newValue)
                        context.AddTag(tag);
                    else
                        context.RemoveTag(tag);
                }
            }

            // Add new tag
            EditorGUILayout.BeginHorizontal();
            newTagInput = EditorGUILayout.TextField(newTagInput, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("+", GUILayout.Width(25)) && !string.IsNullOrEmpty(newTagInput))
            {
                manualTags.Add(newTagInput);
                context.AddTag(newTagInput);
                newTagInput = "";
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawFlagsSection(GameContext context)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        showFlagsFoldout = EditorGUILayout.Foldout(showFlagsFoldout, "Flags", true);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (showFlagsFoldout)
        {
            var currentFlags = context.GetAllFlags();
            var allFlags = new HashSet<string>(collector.UsedFlags.Keys);
            allFlags.UnionWith(currentFlags.Keys);
            allFlags.UnionWith(manualFlags);

            foreach (var flag in allFlags.OrderBy(f => f))
            {
                var hasFlag = currentFlags.TryGetValue(flag, out var flagValue) && flagValue;
                var usageCount = collector.UsedFlags.TryGetValue(flag, out var count) ? count : 0;
                var isManual = manualFlags.Contains(flag) && usageCount == 0;

                EditorGUILayout.BeginHorizontal();
                var newValue = EditorGUILayout.Toggle(hasFlag, GUILayout.Width(20));
                EditorGUILayout.LabelField(flag, GUILayout.ExpandWidth(true));
                if (usageCount > 0)
                {
                    EditorGUILayout.LabelField($"({usageCount}箇所)", GUILayout.Width(60));
                }
                else if (isManual)
                {
                    EditorGUILayout.LabelField("(手動)", GUILayout.Width(60));
                }
                EditorGUILayout.EndHorizontal();

                if (newValue != hasFlag)
                {
                    context.SetFlag(flag, newValue);
                }
            }

            // Add new flag
            EditorGUILayout.BeginHorizontal();
            newFlagInput = EditorGUILayout.TextField(newFlagInput, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("+", GUILayout.Width(25)) && !string.IsNullOrEmpty(newFlagInput))
            {
                manualFlags.Add(newFlagInput);
                context.SetFlag(newFlagInput, true);
                newFlagInput = "";
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCountersSection(GameContext context)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        showCountersFoldout = EditorGUILayout.Foldout(showCountersFoldout, "Counters", true);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (showCountersFoldout)
        {
            var currentCounters = context.GetAllCounters();
            var allCounters = new HashSet<string>(collector.UsedCounters.Keys);
            allCounters.UnionWith(currentCounters.Keys);
            allCounters.UnionWith(manualCounters);

            foreach (var key in allCounters.OrderBy(k => k))
            {
                var currentValue = currentCounters.TryGetValue(key, out var val) ? val : 0;
                var usageCount = collector.UsedCounters.TryGetValue(key, out var count) ? count : 0;
                var isManual = manualCounters.Contains(key) && usageCount == 0;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(key, GUILayout.Width(120));
                var newValue = EditorGUILayout.IntField(currentValue, GUILayout.Width(60));
                if (usageCount > 0)
                {
                    EditorGUILayout.LabelField($"({usageCount}箇所)", GUILayout.Width(60));
                }
                else if (isManual)
                {
                    EditorGUILayout.LabelField("(手動)", GUILayout.Width(60));
                }
                EditorGUILayout.EndHorizontal();

                if (newValue != currentValue)
                {
                    context.SetCounter(key, newValue);
                }
            }

            // Add new counter
            EditorGUILayout.BeginHorizontal();
            newCounterKey = EditorGUILayout.TextField(newCounterKey, GUILayout.Width(100));
            newCounterValue = EditorGUILayout.IntField(newCounterValue, GUILayout.Width(60));
            if (GUILayout.Button("+", GUILayout.Width(25)) && !string.IsNullOrEmpty(newCounterKey))
            {
                manualCounters.Add(newCounterKey);
                context.SetCounter(newCounterKey, newCounterValue);
                newCounterKey = "";
                newCounterValue = 0;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawOverlaysSection(GameContext context)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        showOverlaysFoldout = EditorGUILayout.Foldout(showOverlaysFoldout, "Encounter Overlays", true);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (showOverlaysFoldout)
        {
            // 全てのアクティブなオーバーレイを表示（永続/非永続を問わず）
            var overlays = context.EncounterOverlays.GetAllActive();
            foreach (var overlay in overlays)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(overlay.Id, GUILayout.Width(100));
                EditorGUILayout.LabelField($"x{overlay.Multiplier:F2}", GUILayout.Width(50));
                var stepsLabel = overlay.RemainingSteps < 0 ? "永続" : $"残り{overlay.RemainingSteps}歩";
                EditorGUILayout.LabelField(stepsLabel, GUILayout.Width(70));
                // 永続/一時の表示
                EditorGUILayout.LabelField(overlay.Persistent ? "[P]" : "[T]", GUILayout.Width(25));
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    context.RemoveEncounterOverlay(overlay.Id);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (overlays.Count == 0)
            {
                EditorGUILayout.LabelField("(なし)", EditorStyles.miniLabel);
            }

            // Add new overlay
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Add Overlay", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            newOverlayId = EditorGUILayout.TextField(newOverlayId, GUILayout.Width(80));
            newOverlayMultiplier = EditorGUILayout.FloatField(newOverlayMultiplier, GUILayout.Width(50));
            newOverlaySteps = EditorGUILayout.IntField(newOverlaySteps, GUILayout.Width(40));
            EditorGUILayout.LabelField("歩", GUILayout.Width(20));
            if (GUILayout.Button("+", GUILayout.Width(25)) && !string.IsNullOrEmpty(newOverlayId))
            {
                context.PushEncounterOverlay(newOverlayId, newOverlayMultiplier, newOverlaySteps, true);
                newOverlayId = "";
                newOverlayMultiplier = 1f;
                newOverlaySteps = -1;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("（-1 = 永続）", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private void ClearDebugState(GameContext context)
    {
        // Clear tags - need to get all tags first since we can't modify during iteration
        var allTags = context.GetAllTags().ToList();
        foreach (var tag in allTags)
        {
            context.RemoveTag(tag);
        }

        // Clear flags
        var allFlags = context.GetAllFlags().Keys.ToList();
        foreach (var flag in allFlags)
        {
            context.SetFlag(flag, false);
        }

        // Clear counters
        var allCounters = context.GetAllCounters().Keys.ToList();
        foreach (var counter in allCounters)
        {
            context.SetCounter(counter, 0);
        }

        // Clear overlays
        context.EncounterOverlays.Clear();

        // Clear manual lists
        manualTags.Clear();
        manualFlags.Clear();
        manualCounters.Clear();

        Debug.Log("[WalkDebugInspector] Debug state cleared.");
    }
}
