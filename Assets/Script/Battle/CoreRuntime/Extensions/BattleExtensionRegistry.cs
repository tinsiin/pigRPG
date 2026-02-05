using System.Collections.Generic;

public sealed class BattleExtensionRegistry
{
    private readonly List<IBattleExtension> _extensions = new();
    private readonly Dictionary<string, IBattleExtension> _byId = new();

    public IReadOnlyList<IBattleExtension> Extensions => _extensions;

    public bool Register(IBattleExtension extension)
    {
        if (extension == null) return false;
        var id = extension.Info.Id;
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (_byId.ContainsKey(id)) return false;

        _extensions.Add(extension);
        _byId.Add(id, extension);
        return true;
    }

    public BattleExtensionApplyReport ApplyTo(
        BattleRuleRegistry registry,
        BattleExtensionCompatibilityPolicy policy = null)
    {
        var report = new BattleExtensionApplyReport();
        if (registry == null) return report;
        policy ??= BattleExtensionCompatibilityPolicy.Default;
        for (var i = 0; i < _extensions.Count; i++)
        {
            var ext = _extensions[i];
            if (!policy.IsCompatible(ext.Info, out var reason))
            {
                report.Skipped.Add(new BattleExtensionSkipInfo(ext.Info.Id, reason));
                continue;
            }
            ext.Register(registry);
            report.AppliedCount++;
        }
        return report;
    }
}
