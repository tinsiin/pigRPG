using System.IO;
using System.Text;
using UnityEngine;

public static class BattleReplayIO
{
    public const string DefaultFolderName = "replay";
    public const string DefaultFileName = "battle_replay.json";

    public static string GetDefaultFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, DefaultFolderName);
    }

    public static string GetDefaultFilePath(string fileName = DefaultFileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) fileName = DefaultFileName;
        return Path.Combine(GetDefaultFolderPath(), fileName);
    }

    public static bool SaveDefault(BattleReplayData data, string fileName = DefaultFileName)
    {
        if (data == null)
        {
            Debug.LogError("BattleReplayIO.SaveDefault: data is null.");
            return false;
        }

        var folder = GetDefaultFolderPath();
        Directory.CreateDirectory(folder);
        var path = GetDefaultFilePath(fileName);
        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return true;
    }

    public static bool TryLoadDefault(out BattleReplayData data, string fileName = DefaultFileName)
    {
        data = null;
        var path = GetDefaultFilePath(fileName);
        if (!File.Exists(path)) return false;

        try
        {
            var json = File.ReadAllText(path, new UTF8Encoding(false));
            data = JsonUtility.FromJson<BattleReplayData>(json);
            return data != null;
        }
        catch (IOException ex)
        {
            Debug.LogError($"BattleReplayIO.TryLoadDefault: {ex.Message}");
            data = null;
            return false;
        }
    }
}
