using UnityEditor;
using UnityEngine;

public static class ReorganizeDataFolders
{
    [MenuItem("Tools/Reorganize Data Folders")]
    public static void Execute()
    {
        // 1. Create target folders
        CreateFolderIfNeeded("Assets/Data", "Walk");
        CreateFolderIfNeeded("Assets/Data", "Battle");
        CreateFolderIfNeeded("Assets/Data", "Debug");

        var moved = 0;
        var errors = 0;

        // 2. Move WalkSO contents into Data/Walk
        // Move the subfolders and files inside WalkSO (not the folder itself, to avoid nesting)
        var walkAssets = AssetDatabase.FindAssets("", new[] { "Assets/ScriptableObject/WalkSO" });
        // Move the folder itself
        moved += MoveAsset("Assets/ScriptableObject/WalkSO/0__________", "Assets/Data/Walk/0__________", ref errors);
        moved += MoveAsset("Assets/ScriptableObject/WalkSO/Phase2", "Assets/Data/Walk/Phase2", ref errors);
        moved += MoveAsset("Assets/ScriptableObject/WalkSO/TestFeatures", "Assets/Data/Walk/TestFeatures", ref errors);
        moved += MoveAsset("Assets/ScriptableObject/WalkSO/FlowGraph_Stages.asset", "Assets/Data/Walk/FlowGraph_Stages.asset", ref errors);

        // 3. Move BattleBrainAI → Data/Battle
        var brainAssets = AssetDatabase.FindAssets("", new[] { "Assets/ScriptableObject/BattleBrainAI" });
        foreach (var guid in brainAssets)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == "Assets/ScriptableObject/BattleBrainAI") continue;
            var fileName = System.IO.Path.GetFileName(path);
            moved += MoveAsset(path, "Assets/Data/Battle/" + fileName, ref errors);
        }

        // 4. Move loose files to Battle
        moved += MoveAsset("Assets/ScriptableObject/LogBridgeCollection.asset", "Assets/Data/Battle/LogBridgeCollection.asset", ref errors);
        moved += MoveAsset("Assets/ScriptableObject/MotionFlavor1.asset", "Assets/Data/Battle/MotionFlavor1.asset", ref errors);

        // 5. Move BenchiMark → Data/Debug/BenchiMark
        moved += MoveAsset("Assets/ScriptableObject/BenchiMark", "Assets/Data/Debug/BenchiMark", ref errors);

        // 6. Move ReactionSystemTest → Data/Debug/ReactionSystemTest
        moved += MoveAsset("Assets/ScriptableObject/ReactionSystemTest", "Assets/Data/Debug/ReactionSystemTest", ref errors);

        // 7. Move MetricsSettings → Data/Debug
        moved += MoveAsset("Assets/ScriptableObject/MetricsSettings.asset", "Assets/Data/Debug/MetricsSettings.asset", ref errors);

        // 8. Move New Event Definition SO → Data/Walk
        moved += MoveAsset("Assets/ScriptableObject/New Event Definition SO.asset", "Assets/Data/Walk/New Event Definition SO.asset", ref errors);

        // 9. Rename Data/audio → Data/Audio (Unity requires move for rename)
        moved += MoveAsset("Assets/Data/audio", "Assets/Data/Audio", ref errors);

        // 10. Clean up empty folders
        TryDeleteFolder("Assets/ScriptableObject/WalkSO");
        TryDeleteFolder("Assets/ScriptableObject/BattleBrainAI");
        TryDeleteFolder("Assets/ScriptableObject/BenchiMark");
        TryDeleteFolder("Assets/ScriptableObject/ReactionSystemTest");
        TryDeleteFolder("Assets/ScriptableObject");

        AssetDatabase.Refresh();
        Debug.Log($"[ReorganizeDataFolders] Done. Moved: {moved}, Errors: {errors}");
    }

    private static void CreateFolderIfNeeded(string parent, string name)
    {
        var full = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, name);
            Debug.Log($"[ReorganizeDataFolders] Created folder: {full}");
        }
    }

    private static int MoveAsset(string from, string to, ref int errors)
    {
        if (!AssetDatabase.AssetPathExists(from))
        {
            Debug.LogWarning($"[ReorganizeDataFolders] Source not found: {from}");
            return 0;
        }

        var result = AssetDatabase.MoveAsset(from, to);
        if (string.IsNullOrEmpty(result))
        {
            Debug.Log($"[ReorganizeDataFolders] Moved: {from} → {to}");
            return 1;
        }
        else
        {
            Debug.LogError($"[ReorganizeDataFolders] Failed: {from} → {to} | {result}");
            errors++;
            return 0;
        }
    }

    private static void TryDeleteFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            // Only delete if empty
            var remaining = AssetDatabase.FindAssets("", new[] { path });
            if (remaining.Length == 0)
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[ReorganizeDataFolders] Deleted empty folder: {path}");
            }
            else
            {
                Debug.LogWarning($"[ReorganizeDataFolders] Folder not empty, skipped: {path} ({remaining.Length} items)");
            }
        }
    }
}
