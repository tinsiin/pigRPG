using System;
using System.Collections.Generic;

public sealed class BattleExtensionCompatibilityPolicy
{
    public string ApiVersion = "1.0";
    public bool AllowUnknownApiVersion = false;
    public bool RequireSameMajor = true;

    public bool IsCompatible(BattleExtensionInfo info, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(ApiVersion))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(info.ApiVersion))
        {
            if (AllowUnknownApiVersion) return true;
            reason = "Missing ApiVersion.";
            return false;
        }

        if (RequireSameMajor)
        {
            if (!TryGetMajor(ApiVersion, out var expectedMajor) || !TryGetMajor(info.ApiVersion, out var actualMajor))
            {
                if (string.Equals(ApiVersion, info.ApiVersion, StringComparison.Ordinal))
                {
                    return true;
                }

                reason = $"ApiVersion mismatch. expected={ApiVersion}, actual={info.ApiVersion}";
                return false;
            }

            if (expectedMajor != actualMajor)
            {
                reason = $"ApiVersion major mismatch. expected={expectedMajor}, actual={actualMajor}";
                return false;
            }
        }
        else if (!string.Equals(ApiVersion, info.ApiVersion, StringComparison.Ordinal))
        {
            reason = $"ApiVersion mismatch. expected={ApiVersion}, actual={info.ApiVersion}";
            return false;
        }

        return true;
    }

    private static bool TryGetMajor(string version, out int major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(version)) return false;
        var i = 0;
        while (i < version.Length && !char.IsDigit(version[i])) i++;
        if (i >= version.Length) return false;
        var start = i;
        while (i < version.Length && char.IsDigit(version[i])) i++;
        return int.TryParse(version.Substring(start, i - start), out major);
    }

    public static BattleExtensionCompatibilityPolicy Default => new BattleExtensionCompatibilityPolicy();
}

public readonly struct BattleExtensionSkipInfo
{
    public string Id { get; }
    public string Reason { get; }

    public BattleExtensionSkipInfo(string id, string reason)
    {
        Id = id ?? "";
        Reason = reason ?? "";
    }
}

public sealed class BattleExtensionApplyReport
{
    public int AppliedCount { get; internal set; }
    public List<BattleExtensionSkipInfo> Skipped { get; } = new();
}
