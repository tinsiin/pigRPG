using System.IO;
using System.Text;
using UnityEngine;

public static class BattleRuleCatalogIO
{
    public const string DefaultFolderName = "battle_rules";
    public const string DefaultFileName = "battle_rules.json";

    public static string GetDefaultFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, DefaultFolderName);
    }

    public static string GetDefaultFilePath(string fileName = DefaultFileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) fileName = DefaultFileName;
        return Path.Combine(GetDefaultFolderPath(), fileName);
    }

    public static bool SaveDefault(BattleRuleCatalog catalog, string fileName = DefaultFileName)
    {
        if (catalog == null)
        {
            Debug.LogError("BattleRuleCatalogIO.SaveDefault: catalog is null.");
            return false;
        }

        var folder = GetDefaultFolderPath();
        Directory.CreateDirectory(folder);
        var path = GetDefaultFilePath(fileName);
        var json = JsonUtility.ToJson(catalog, true);
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return true;
    }

    public static bool TryLoadDefault(out BattleRuleCatalog catalog, string fileName = DefaultFileName)
    {
        catalog = null;
        var path = GetDefaultFilePath(fileName);
        if (!File.Exists(path)) return false;

        try
        {
            var json = File.ReadAllText(path, new UTF8Encoding(false));
            catalog = JsonUtility.FromJson<BattleRuleCatalog>(json);
            return catalog != null;
        }
        catch (IOException ex)
        {
            Debug.LogError($"BattleRuleCatalogIO.TryLoadDefault: {ex.Message}");
            catalog = null;
            return false;
        }
    }
}
