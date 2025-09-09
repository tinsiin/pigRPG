using System;
using System.Collections.Generic;

/// <summary>
/// ベンチ用シナリオの簡易カタログ。将来シナリオが増えたらここへ登録します。
/// UI（Dropdown等）から index/key で選べるよう、静的な一覧を提供します。
/// </summary>
public static class ScenarioCatalog
{
    public sealed class ScenarioInfo
    {
        public string Key;
        public string Display;
        public Func<IBenchmarkScenario> Factory;
    }

    private static readonly ScenarioInfo[] _all = new ScenarioInfo[]
    {
        new ScenarioInfo { Key = "walk1", Display = "Walk(1)", Factory = () => new WalkOneStepScenario() },
        // 追加シナリオはここに登録
    };

    public static int Count => _all.Length;
    public static ScenarioInfo GetInfo(int index)
    {
        if (index < 0 || index >= _all.Length) index = 0;
        return _all[index];
    }

    public static int FindIndexByKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        for (int i = 0; i < _all.Length; i++)
        {
            if (string.Equals(_all[i].Key, key, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return 0;
    }

    public static IBenchmarkScenario CreateByIndex(int index)
    {
        var info = GetInfo(index);
        return info.Factory != null ? info.Factory() : null;
    }

    public static IBenchmarkScenario CreateByKey(string key)
    {
        return CreateByIndex(FindIndexByKey(key));
    }

    public static IEnumerable<ScenarioInfo> All => _all;
}
