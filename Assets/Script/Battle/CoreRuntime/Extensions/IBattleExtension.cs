public interface IBattleExtension
{
    BattleExtensionInfo Info { get; }
    void Register(BattleRuleRegistry registry);
}

public readonly struct BattleExtensionInfo
{
    public string Id { get; }
    public string Version { get; }
    public string ApiVersion { get; }
    public string Author { get; }

    public BattleExtensionInfo(string id, string version, string apiVersion = null, string author = null)
    {
        Id = id ?? "";
        Version = version ?? "";
        ApiVersion = apiVersion ?? "";
        Author = author ?? "";
    }
}
