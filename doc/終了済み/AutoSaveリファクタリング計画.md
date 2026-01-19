# AutoSave / SceneBackup リファクタリング計画

> **Status: 完了** (2025-01)

## 概要

2014年製のAutoSave/SceneBackupコードをUnity 2022.3対応のモダンなコードに書き換える。

**対象ファイル:**
- `Assets/Editor/AutoSave.cs`
- `Assets/Editor/SceneBackup.cs`

**方針:** 2ファイルを1ファイル `AutoSave.cs` に統合

---

## Phase 1: 非推奨API置換

### 1.1 イベント系

| Before | After |
|--------|-------|
| `EditorApplication.playmodeStateChanged` | `EditorApplication.playModeStateChanged` |
| `EditorApplication.hierarchyWindowChanged` | `EditorApplication.hierarchyChanged` |

### 1.2 シーン操作系

| Before | After |
|--------|-------|
| `EditorApplication.SaveScene()` | `EditorSceneManager.SaveOpenScenes()` |
| `EditorApplication.currentScene` | `EditorSceneManager.GetActiveScene().path` |

**必要なusing追加:**
```csharp
using UnityEditor.SceneManagement;
```

### 1.3 Preferences UI

| Before | After |
|--------|-------|
| `[PreferenceItem("Auto Save")]` | `SettingsProvider` |

**新しいパターン:**
```csharp
public class AutoSaveSettingsProvider : SettingsProvider
{
    public AutoSaveSettingsProvider(string path, SettingsScope scope)
        : base(path, scope) { }

    [SettingsProvider]
    public static SettingsProvider Create()
    {
        return new AutoSaveSettingsProvider("Preferences/Auto Save", SettingsScope.User);
    }

    public override void OnGUI(string searchContext)
    {
        // UI描画
    }
}
```

---

## Phase 2: コード整理

### 2.1 保存ロジックの抽出

**Before（重複）:**
```csharp
// 2箇所で同じコード
if (IsSavePrefab)
    AssetDatabase.SaveAssets();
if (IsSaveScene) {
    Debug.Log("save scene " + System.DateTime.Now);
    EditorApplication.SaveScene();
}
```

**After:**
```csharp
private static void PerformSave(string trigger)
{
    if (IsSavePrefab)
        AssetDatabase.SaveAssets();

    if (IsSaveScene)
    {
        Debug.Log($"[AutoSave] {trigger}: {DateTime.Now}");
        EditorSceneManager.SaveOpenScenes();
    }
}
```

### 2.2 設定プロパティのヘルパー化

**Before（4プロパティで同じパターン）:**
```csharp
static bool IsAutoSave {
    get {
        string value = EditorUserSettings.GetConfigValue(autoSave);
        return !string.IsNullOrEmpty(value) && value.Equals("True");
    }
    set {
        EditorUserSettings.SetConfigValue(autoSave, value.ToString());
    }
}
```

**After:**
```csharp
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

// 使用例
static bool IsAutoSave
{
    get => GetBoolConfig(ConfigKeys.AutoSave);
    set => SetBoolConfig(ConfigKeys.AutoSave, value);
}
```

### 2.3 設定キーの定数化

```csharp
private static class ConfigKeys
{
    public const string AutoSave = "autosave@enabled";
    public const string SavePrefab = "autosave@prefab";
    public const string SaveScene = "autosave@scene";
    public const string TimerEnabled = "autosave@timer";
    public const string Interval = "autosave@interval";
    public const string ManualSave = "autosave@manualSave";
}
```

---

## Phase 3: エラーハンドリング強化

### 3.1 Interval パース

**Before:**
```csharp
return int.Parse(value);
```

**After:**
```csharp
return int.TryParse(value, out int result) ? Math.Max(result, MinInterval) : DefaultInterval;
```

### 3.2 Backup/Rollback の安全性

**Before:**
```csharp
byte[] data = File.ReadAllBytes(EditorApplication.currentScene);
File.WriteAllBytes(expoertPath, data);
```

**After:**
```csharp
public static bool Backup()
{
    string scenePath = EditorSceneManager.GetActiveScene().path;
    if (string.IsNullOrEmpty(scenePath))
    {
        Debug.LogWarning("[AutoSave] Cannot backup: no scene is open.");
        return false;
    }

    string backupPath = GetBackupPath(scenePath);

    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
        File.Copy(scenePath, backupPath, overwrite: true);
        return true;
    }
    catch (Exception e)
    {
        Debug.LogError($"[AutoSave] Backup failed: {e.Message}");
        return false;
    }
}
```

---

## Phase 4: SceneBackup統合

`SceneBackup.cs` を削除し、`AutoSave.cs` 内に `AssetModificationProcessor` を統合。

```csharp
// AutoSave.cs 内に追加
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
```

---

## Phase 5: コードスタイル修正

- typo修正: `expoertPath` → `backupPath`
- 未使用using削除: `System.Collections`
- スペース修正: `return!string` → `return !string`
- メソッド名改善: `ExampleOnGUI` → 削除（SettingsProviderに移行）

---

## 最終構成

```
Assets/Editor/
├── AutoSave.cs          # 統合後（約150行想定）
└── (SceneBackup.cs削除)
```

**AutoSave.cs 構成:**
```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AutoSave
{
    // === 定数 ===
    private static class ConfigKeys { ... }
    private const int DefaultInterval = 60;
    private const int MinInterval = 60;
    private const string BackupFolder = "Backup";

    // === 状態 ===
    private static double nextSaveTime;
    private static bool hierarchyDirty;

    // === 初期化 ===
    static AutoSave() { ... }

    // === 設定プロパティ ===
    public static bool IsManualSave { get; private set; }
    private static bool IsEnabled { get; set; }
    private static bool SavePrefab { get; set; }
    private static bool SaveScene { get; set; }
    private static bool TimerEnabled { get; set; }
    private static int Interval { get; set; }

    // === ヘルパー ===
    private static bool GetBoolConfig(string key, bool defaultValue = false) { ... }
    private static void SetBoolConfig(string key, bool value) { ... }

    // === 保存処理 ===
    private static void PerformSave(string trigger) { ... }

    // === バックアップ ===
    [MenuItem("File/Backup/Backup")]
    public static bool Backup() { ... }

    [MenuItem("File/Backup/Rollback")]
    public static bool Rollback() { ... }

    private static string GetBackupPath(string scenePath) { ... }

    // === AssetModificationProcessor ===
    private class BackupProcessor : AssetModificationProcessor { ... }
}

// === Settings UI ===
public class AutoSaveSettingsProvider : SettingsProvider { ... }
```

---

## テスト項目

- [ ] PlayMode開始時の自動保存が動作する
- [ ] タイマーによる定期保存が動作する
- [ ] 手動保存時にバックアップが作成される
- [ ] Backup/Rollbackメニューが動作する
- [ ] Preferences画面で設定変更できる
- [ ] 未保存シーンでBackupがエラーにならない
- [ ] Rollback時にファイルが存在しない場合のエラーハンドリング

---

## 備考

- 元コード: tsubaki氏 (2014年) https://gist.github.com/tsubaki/8709502
- Unity 2022.3 LTS対応
