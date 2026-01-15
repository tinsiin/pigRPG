using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Encounter Table")]
public sealed class EncounterTableSO : ScriptableObject
{
    [Range(0f, 1f)]
    [SerializeField] private float baseRate = 0.1f;
    [SerializeField] private int cooldownSteps;
    [SerializeField] private int graceSteps;
    [SerializeField] private float pityIncrement;
    [Range(0f, 1f)]
    [SerializeField] private float pityMax = 1f;
    [SerializeField] private bool enableDebugLog;
    [SerializeField] private EncounterEntry[] entries;

    public float BaseRate => baseRate;
    public int CooldownSteps => cooldownSteps;
    public int GraceSteps => graceSteps;
    public float PityIncrement => pityIncrement;
    public float PityMax => pityMax;
    public bool EnableDebugLog => enableDebugLog;
    public EncounterEntry[] Entries => entries;
}

[System.Serializable]
public sealed class EncounterEntry
{
    [SerializeField] private EncounterSO encounter;
    [SerializeField] private float weight = 1f;
    [SerializeField] private ConditionSO[] conditions;

    public EncounterSO Encounter => encounter;
    public float Weight => weight;
    public ConditionSO[] Conditions => conditions;
}
