using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/SideObject Table")]
public sealed class SideObjectTableSO : ScriptableObject
{
    [SerializeField] private SideObjectEntry[] entries;
    [SerializeField] private float varietyBias = 0.5f;
    [Tooltip("Number of recent picks to track for variety bias. 0 = disabled.")]
    [SerializeField] private int varietyDepth;

    public SideObjectEntry[] Entries => entries;
    public float VarietyBias => varietyBias;
    public int VarietyDepth => varietyDepth;
}

[Serializable]
public sealed class SideObjectEntry
{
    [SerializeField] private SideObjectSO sideObject;
    [SerializeField] private float weight = 1f;
    [SerializeField] private ConditionSO[] conditions;
    [Tooltip("Steps before this can appear again after selection. 0 = no cooldown.")]
    [SerializeField] private int cooldownSteps;

    public SideObjectSO SideObject => sideObject;
    public float Weight => weight;
    public ConditionSO[] Conditions => conditions;
    public int CooldownSteps => cooldownSteps;
}