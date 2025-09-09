#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MetricsDefineToggle
{
    private const string DefineToken = "METRICS_DISABLED";

    [MenuItem("Tools/Metrics/Enable Metrics (Remove METRICS_DISABLED)")]
    public static void EnableMetrics()
    {
        ToggleDefine(false);
    }

    [MenuItem("Tools/Metrics/Disable Metrics (Add METRICS_DISABLED)")]
    public static void DisableMetrics()
    {
        ToggleDefine(true);
    }

    private static void ToggleDefine(bool add)
    {
        var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        if (group == BuildTargetGroup.Unknown)
        {
            Debug.LogWarning("[Metrics] Unknown build target group. Aborting define toggle.");
            return;
        }
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        var list = new System.Collections.Generic.List<string>(defines.Split(';'));
        list.RemoveAll(s => string.IsNullOrWhiteSpace(s));
        bool changed = false;
        if (add)
        {
            if (!list.Contains(DefineToken)) { list.Add(DefineToken); changed = true; }
        }
        else
        {
            if (list.Remove(DefineToken)) changed = true;
        }
        if (!changed)
        {
            Debug.Log("[Metrics] No change to scripting define symbols.");
            return;
        }
        string next = string.Join(";", list);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, next);
        Debug.Log("[Metrics] Updated scripting define symbols for " + group + ": " + next);
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Metrics/Enable Metrics (All Common Targets)")]
    public static void EnableMetricsAll() => ToggleDefineOnGroups(false, CommonGroups());

    [MenuItem("Tools/Metrics/Disable Metrics (All Common Targets)")]
    public static void DisableMetricsAll() => ToggleDefineOnGroups(true, CommonGroups());

    private static BuildTargetGroup[] CommonGroups()
    {
        // 本プロジェクトのリリース対象: WebGL / Android / iOS
        return new[]
        {
            BuildTargetGroup.WebGL,
            BuildTargetGroup.Android,
            BuildTargetGroup.iOS,
        };
    }

    private static void ToggleDefineOnGroups(bool add, BuildTargetGroup[] groups)
    {
        foreach (var g in groups)
        {
            if (g == BuildTargetGroup.Unknown) continue;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(g);
            var list = new System.Collections.Generic.List<string>(defines.Split(';'));
            list.RemoveAll(s => string.IsNullOrWhiteSpace(s));
            bool changed = false;
            if (add)
            {
                if (!list.Contains(DefineToken)) { list.Add(DefineToken); changed = true; }
            }
            else
            {
                if (list.Remove(DefineToken)) changed = true;
            }
            if (changed)
            {
                string next = string.Join(";", list);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(g, next);
                Debug.Log("[Metrics] Updated scripting define symbols for " + g + ": " + next);
            }
        }
        AssetDatabase.Refresh();
    }
}
#endif
