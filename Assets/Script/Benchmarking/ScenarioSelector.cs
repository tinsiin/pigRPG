using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// ベンチマーク対象シナリオをUI（任意：TMP_Dropdown）で切り替えるためのコンポーネント。
/// シーンに1つ配置し、WatchUIUpdate などから参照します。
/// UI未割り当ての場合は Inspector の defaultKey で選択します。
/// </summary>
public sealed class ScenarioSelector : MonoBehaviour
{
    private const string PrefKey = "benchmark_scenario_key";

    [Header("任意: TMP_Dropdown（UIから切替する場合に割り当て）")]
    [SerializeField] private TMP_Dropdown dropdown;

    [Header("UI未使用時の既定キー（ScenarioCatalog の Key）")]
    [SerializeField] private string defaultKey = "walk1";

    public string SelectedKey { get; private set; }

    private void OnEnable()
    {
        // 既定キーの決定（PlayerPrefs > Inspector）
        var key = PlayerPrefs.GetString(PrefKey, defaultKey);
        if (string.IsNullOrEmpty(key)) key = defaultKey;
        SelectedKey = key;

        // UI構築
        if (dropdown != null)
        {
            BuildDropdownOptions();
            int idx = ScenarioCatalog.FindIndexByKey(SelectedKey);
            dropdown.value = idx;
            dropdown.onValueChanged.AddListener(OnDropdownChanged);
        }
    }

    private void OnDisable()
    {
        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        }
    }

    private void BuildDropdownOptions()
    {
        if (dropdown == null) return;
        var opts = new List<TMP_Dropdown.OptionData>();
        foreach (var info in ScenarioCatalog.All)
        {
            opts.Add(new TMP_Dropdown.OptionData(info.Display));
        }
        dropdown.options = opts;
        dropdown.RefreshShownValue();
    }

    private void OnDropdownChanged(int index)
    {
        var info = ScenarioCatalog.GetInfo(index);
        SelectedKey = info.Key;
        PlayerPrefs.SetString(PrefKey, SelectedKey);
        PlayerPrefs.Save();
    }

    public IBenchmarkScenario CreateSelectedScenario()
    {
        return ScenarioCatalog.CreateByKey(SelectedKey);
    }
}
