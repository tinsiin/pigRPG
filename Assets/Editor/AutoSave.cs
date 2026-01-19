using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// シーンの自動保存とバックアップ機能
/// 元コード: tsubaki氏 (2014年) - Unity 2022.3対応にリファクタリング
/// </summary>
[InitializeOnLoad]
public static class AutoSave
{
    private static class ConfigKeys
    {
        public const string Enabled = "autosave@enabled";
        public const string SavePrefab = "autosave@prefab";
        public const string SaveScene = "autosave@scene";
        public const string TimerEnabled = "autosave@timer";
        public const string Interval = "autosave@interval";
        public const string ManualSave = "autosave@manualSave";
    }

    private const int DefaultInterval = 60;
    private const int MinInterval = 60;
    private const string BackupFolder = "Backup";

    private static double nextSaveTime;
    private static bool hierarchyDirty;

    static AutoSave()
    {
        IsManualSave = true;

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;

        nextSaveTime = EditorApplication.timeSinceStartup + Interval;
    }

    #region Properties

    public static bool IsManualSave
    {
        get => EditorPrefs.GetBool(ConfigKeys.ManualSave, true);
        private set => EditorPrefs.SetBool(ConfigKeys.ManualSave, value);
    }

    private static bool IsEnabled
    {
        get => GetBoolConfig(ConfigKeys.Enabled);
        set => SetBoolConfig(ConfigKeys.Enabled, value);
    }

    private static bool SavePrefab
    {
        get => GetBoolConfig(ConfigKeys.SavePrefab, defaultValue: true);
        set => SetBoolConfig(ConfigKeys.SavePrefab, value);
    }

    private static bool SaveScene
    {
        get => GetBoolConfig(ConfigKeys.SaveScene, defaultValue: true);
        set => SetBoolConfig(ConfigKeys.SaveScene, value);
    }

    private static bool TimerEnabled
    {
        get => GetBoolConfig(ConfigKeys.TimerEnabled);
        set => SetBoolConfig(ConfigKeys.TimerEnabled, value);
    }

    private static int Interval
    {
        get
        {
            string value = EditorUserSettings.GetConfigValue(ConfigKeys.Interval);
            if (int.TryParse(value, out int result))
                return Math.Max(result, MinInterval);
            return DefaultInterval;
        }
        set
        {
            int clamped = Math.Max(value, MinInterval);
            EditorUserSettings.SetConfigValue(ConfigKeys.Interval, clamped.ToString());
        }
    }

    #endregion

    #region Config Helpers

    private static bool GetBoolConfig(string key, bool defaultValue = false)
    {
        string value = EditorUserSettings.GetConfigValue(key);
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return value.Equals("True", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetBoolConfig(string key, bool value)
    {
        EditorUserSettings.SetConfigValue(key, value.ToString());
    }

    #endregion

    #region Event Handlers

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        if (!IsEnabled) return;

        PerformSave("PlayMode開始");
        hierarchyDirty = false;
    }

    private static void OnEditorUpdate()
    {
        if (!hierarchyDirty) return;
        if (nextSaveTime > EditorApplication.timeSinceStartup) return;

        nextSaveTime = EditorApplication.timeSinceStartup + Interval;

        if (TimerEnabled && IsEnabled && !EditorApplication.isPlaying)
        {
            PerformSave("タイマー");
        }

        hierarchyDirty = false;
    }

    private static void OnHierarchyChanged()
    {
        if (!EditorApplication.isPlaying)
        {
            hierarchyDirty = true;
        }
    }

    #endregion

    #region Save Logic

    private static void PerformSave(string trigger)
    {
        IsManualSave = false;

        try
        {
            if (SavePrefab)
            {
                AssetDatabase.SaveAssets();
            }

            if (SaveScene)
            {
                Debug.Log($"[AutoSave] {trigger}: {DateTime.Now}");
                EditorSceneManager.SaveOpenScenes();
            }
        }
        finally
        {
            IsManualSave = true;
        }
    }

    #endregion

    #region Backup

    [MenuItem("File/Backup/Backup")]
    public static bool Backup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        string scenePath = scene.path;

        if (string.IsNullOrEmpty(scenePath))
        {
            Debug.LogWarning("[AutoSave] Cannot backup: no scene is open or scene is not saved.");
            return false;
        }

        string backupPath = GetBackupPath(scenePath);

        try
        {
            string directory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(scenePath, backupPath, overwrite: true);
            Debug.Log($"[AutoSave] Backup created: {backupPath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoSave] Backup failed: {e.Message}");
            return false;
        }
    }

    [MenuItem("File/Backup/Rollback")]
    public static bool Rollback()
    {
        var scene = EditorSceneManager.GetActiveScene();
        string scenePath = scene.path;

        if (string.IsNullOrEmpty(scenePath))
        {
            Debug.LogWarning("[AutoSave] Cannot rollback: no scene is open or scene is not saved.");
            return false;
        }

        string backupPath = GetBackupPath(scenePath);

        if (!File.Exists(backupPath))
        {
            Debug.LogWarning($"[AutoSave] Cannot rollback: backup not found at {backupPath}");
            return false;
        }

        try
        {
            File.Copy(backupPath, scenePath, overwrite: true);
            AssetDatabase.Refresh(ImportAssetOptions.Default);
            Debug.Log($"[AutoSave] Rollback completed from: {backupPath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoSave] Rollback failed: {e.Message}");
            return false;
        }
    }

    private static string GetBackupPath(string scenePath)
    {
        return Path.Combine(BackupFolder, scenePath);
    }

    #endregion

    #region AssetModificationProcessor

    private class BackupProcessor : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            if (IsManualSave)
            {
                Backup();
            }
            return paths;
        }
    }

    #endregion
}

/// <summary>
/// AutoSave設定用のPreferencesパネル
/// </summary>
public class AutoSaveSettingsProvider : SettingsProvider
{
    public AutoSaveSettingsProvider(string path, SettingsScope scope)
        : base(path, scope) { }

    [SettingsProvider]
    public static SettingsProvider Create()
    {
        return new AutoSaveSettingsProvider("Preferences/Auto Save", SettingsScope.User)
        {
            keywords = new[] { "auto", "save", "backup", "scene" }
        };
    }

    public override void OnGUI(string searchContext)
    {
        EditorGUILayout.Space(10);

        bool isEnabled = GetBool("autosave@enabled");
        bool newEnabled = EditorGUILayout.BeginToggleGroup("Auto Save", isEnabled);
        if (newEnabled != isEnabled) SetBool("autosave@enabled", newEnabled);

        EditorGUILayout.Space(5);

        // Save Prefab/Scene default to true for better UX
        bool savePrefab = GetBool("autosave@prefab", defaultValue: true);
        bool newSavePrefab = EditorGUILayout.ToggleLeft("Save Prefabs", savePrefab);
        if (newSavePrefab != savePrefab) SetBool("autosave@prefab", newSavePrefab);

        bool saveScene = GetBool("autosave@scene", defaultValue: true);
        bool newSaveScene = EditorGUILayout.ToggleLeft("Save Scene", saveScene);
        if (newSaveScene != saveScene) SetBool("autosave@scene", newSaveScene);

        // Warning if Auto Save is enabled but nothing will be saved
        if (newEnabled && !newSavePrefab && !newSaveScene)
        {
            EditorGUILayout.HelpBox(
                "Save PrefabsとSave Sceneが両方OFFのため、何も保存されません。",
                MessageType.Warning);
        }

        EditorGUILayout.Space(5);

        bool timerEnabled = GetBool("autosave@timer");
        bool newTimerEnabled = EditorGUILayout.BeginToggleGroup("Timer Save", timerEnabled);
        if (newTimerEnabled != timerEnabled) SetBool("autosave@timer", newTimerEnabled);

        string intervalStr = EditorUserSettings.GetConfigValue("autosave@interval") ?? "60";
        if (!int.TryParse(intervalStr, out int interval)) interval = 60;

        int newInterval = EditorGUILayout.IntField("Interval (sec, min 60)", interval);
        if (newInterval != interval)
        {
            newInterval = Math.Max(newInterval, 60);
            EditorUserSettings.SetConfigValue("autosave@interval", newInterval.ToString());
        }

        EditorGUILayout.EndToggleGroup();
        EditorGUILayout.EndToggleGroup();

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Auto Save: PlayMode開始時に自動保存\n" +
            "Timer Save: 指定間隔で自動保存（Hierarchy変更時のみ）\n" +
            "手動保存時は自動でバックアップが作成されます",
            MessageType.Info);
    }

    private static bool GetBool(string key, bool defaultValue = false)
    {
        string value = EditorUserSettings.GetConfigValue(key);
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return value.Equals("True", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetBool(string key, bool value)
    {
        EditorUserSettings.SetConfigValue(key, value.ToString());
    }
}
