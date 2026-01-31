#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Reflection;

/// <summary>
/// 既存のEventDefinitionSOフィールドをEventQueueEntry[]配列に移行するエディタスクリプト。
/// </summary>
public static class EventQueueMigration
{
    [MenuItem("Tools/Walk/Migrate EventDefinition to EventQueue")]
    public static void MigrateAll()
    {
        var modified = 0;

        // CentralObjectSO
        var centralGuids = AssetDatabase.FindAssets("t:CentralObjectSO");
        foreach (var guid in centralGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<CentralObjectSO>(path);
            if (MigrateCentralObject(so))
            {
                EditorUtility.SetDirty(so);
                modified++;
            }
        }

        // SideObjectSO
        var sideGuids = AssetDatabase.FindAssets("t:SideObjectSO");
        foreach (var guid in sideGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<SideObjectSO>(path);
            if (MigrateSideObject(so))
            {
                EditorUtility.SetDirty(so);
                modified++;
            }
        }

        // EncounterSO
        var encounterGuids = AssetDatabase.FindAssets("t:EncounterSO");
        foreach (var guid in encounterGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<EncounterSO>(path);
            if (MigrateEncounter(so))
            {
                EditorUtility.SetDirty(so);
                modified++;
            }
        }

        // NodeSO
        var nodeGuids = AssetDatabase.FindAssets("t:NodeSO");
        foreach (var guid in nodeGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<NodeSO>(path);
            if (MigrateNode(so))
            {
                EditorUtility.SetDirty(so);
                modified++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[EventQueueMigration] Migration complete. Modified {modified} assets.");
    }

    private static bool MigrateCentralObject(CentralObjectSO so)
    {
        if (so == null) return false;

        // 旧フィールド（eventDefinition）を取得
        var oldField = GetPrivateField(so, "eventDefinition");
        var newField = GetPrivateField(so, "events");

        if (oldField == null || newField != null) return false;

        var oldValue = oldField as EventDefinitionSO;
        if (oldValue == null) return false;

        // 既にeventsが設定されていれば移行不要
        var events = GetPrivateFieldValue<EventQueueEntry[]>(so, "events");
        if (events != null && events.Length > 0) return false;

        Debug.Log($"[EventQueueMigration] CentralObjectSO '{so.name}' has legacy eventDefinition. Manual migration required.");
        return false; // 自動移行は危険なので手動確認を促す
    }

    private static bool MigrateSideObject(SideObjectSO so)
    {
        if (so == null) return false;

        var events = GetPrivateFieldValue<EventQueueEntry[]>(so, "events");
        if (events != null && events.Length > 0) return false;

        // 旧eventDefinitionがあるか確認
        var oldValue = GetPrivateFieldValue<EventDefinitionSO>(so, "eventDefinition");
        if (oldValue != null)
        {
            Debug.Log($"[EventQueueMigration] SideObjectSO '{so.name}' has legacy eventDefinition. Manual migration required.");
        }
        return false;
    }

    private static bool MigrateEncounter(EncounterSO so)
    {
        if (so == null) return false;

        // 各アウトカムをチェック
        var onWin = GetPrivateFieldValue<EventDefinitionSO>(so, "onWin");
        var onLose = GetPrivateFieldValue<EventDefinitionSO>(so, "onLose");
        var onEscape = GetPrivateFieldValue<EventDefinitionSO>(so, "onEscape");

        if (onWin != null || onLose != null || onEscape != null)
        {
            Debug.Log($"[EventQueueMigration] EncounterSO '{so.name}' has legacy event fields. Manual migration required.");
        }
        return false;
    }

    private static bool MigrateNode(NodeSO so)
    {
        if (so == null) return false;

        var onEnter = GetPrivateFieldValue<EventDefinitionSO>(so, "onEnterEvent");
        var onExit = GetPrivateFieldValue<EventDefinitionSO>(so, "onExitEvent");

        if (onEnter != null || onExit != null)
        {
            Debug.Log($"[EventQueueMigration] NodeSO '{so.name}' has legacy event fields. Manual migration required.");
        }
        return false;
    }

    private static object GetPrivateField(object obj, string fieldName)
    {
        var type = obj.GetType();
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(obj);
    }

    private static T GetPrivateFieldValue<T>(object obj, string fieldName) where T : class
    {
        return GetPrivateField(obj, fieldName) as T;
    }
}
#endif
