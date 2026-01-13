using System.IO;
using System.Text;
using UnityEngine;

public static class PlayersSaveIO
{
    public const string DefaultFolderName = "save";
    public const string DefaultFileName = "players_save.json";

    public static string GetDefaultFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, DefaultFolderName);
    }

    public static string GetDefaultFilePath(string fileName = DefaultFileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) fileName = DefaultFileName;
        return Path.Combine(GetDefaultFolderPath(), fileName);
    }

    public static bool SaveDefault(PlayersSaveData data, string fileName = DefaultFileName)
    {
        if (data == null)
        {
            Debug.LogError("PlayersSaveIO.SaveDefault: data is null.");
            return false;
        }

        var folder = GetDefaultFolderPath();
        Directory.CreateDirectory(folder);
        var path = GetDefaultFilePath(fileName);
        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return true;
    }

    public static bool TryLoadDefault(out PlayersSaveData data, string fileName = DefaultFileName)
    {
        data = null;
        var path = GetDefaultFilePath(fileName);
        if (!File.Exists(path)) return false;

        try
        {
            var json = File.ReadAllText(path, new UTF8Encoding(false));
            data = JsonUtility.FromJson<PlayersSaveData>(json);
            return data != null;
        }
        catch (IOException ex)
        {
            Debug.LogError($"PlayersSaveIO.TryLoadDefault: {ex.Message}");
            data = null;
            return false;
        }
    }
}
