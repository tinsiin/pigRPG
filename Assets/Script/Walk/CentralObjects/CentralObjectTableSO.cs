using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/CentralObject Table")]
public sealed class CentralObjectTableSO : ScriptableObject
{
    [SerializeField] private CentralObjectEntry[] entries;
    [SerializeField] private float varietyBias = 0.5f;
    [Tooltip("Number of recent picks to track for variety bias. 0 = disabled.")]
    [SerializeField] private int varietyDepth;

    public CentralObjectEntry[] Entries => entries;
    public float VarietyBias => varietyBias;
    public int VarietyDepth => varietyDepth;
}

[Serializable]
public sealed class CentralObjectEntry
{
    [SerializeField] private CentralObjectSO centralObject;
    [SerializeField] private float weight = 1f;
    [SerializeField] private ConditionSO[] conditions;
    [Tooltip("Steps before this can appear again after selection. 0 = no cooldown.")]
    [SerializeField] private int cooldownSteps;

    public CentralObjectSO CentralObject => centralObject;
    public float Weight => weight;
    public ConditionSO[] Conditions => conditions;
    public int CooldownSteps => cooldownSteps;
}
