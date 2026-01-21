using System;
using UnityEngine;

[Serializable]
public sealed class GateMarker
{
    [SerializeField] private string gateId;
    [SerializeField] private int order;

    [SerializeField] private GatePositionSpec positionSpec;

    [SerializeField] private ConditionSO[] passConditions;
    [SerializeField] private EffectSO[] onPass;
    [SerializeField] private EffectSO[] onFail;
    [SerializeField] private EventDefinitionSO gateEvent;
    [SerializeField] private GateEventTiming eventTiming;

    [SerializeField] private GateVisual visual;

    public string GateId => gateId;
    public int Order => order;
    public GatePositionSpec PositionSpec => positionSpec;
    public ConditionSO[] PassConditions => passConditions;
    public EffectSO[] OnPass => onPass;
    public EffectSO[] OnFail => onFail;
    public EventDefinitionSO GateEvent => gateEvent;
    public GateEventTiming EventTiming => eventTiming;
    public GateVisual Visual => visual;
}

public enum GateEventTiming
{
    OnAppear,
    OnPass,
    OnFail
}

[Serializable]
public struct GatePositionSpec
{
    public enum PositionType { AbsSteps, Percent, Range }

    [SerializeField] private PositionType type;
    [SerializeField] private int absSteps;
    [SerializeField] private float percent;
    [SerializeField] private int rangeMin;
    [SerializeField] private int rangeMax;

    public PositionType Type => type;
    public int AbsSteps => absSteps;
    public float Percent => percent;
    public int RangeMin => rangeMin;
    public int RangeMax => rangeMax;

    public int ResolvePosition(int trackLength, uint nodeSeed, string gateId)
    {
        switch (type)
        {
            case PositionType.AbsSteps:
                return absSteps;
            case PositionType.Percent:
                return Mathf.RoundToInt(trackLength * percent);
            case PositionType.Range:
                // Use stable hash for gateId (string.GetHashCode is not stable across platforms)
                var gateIdHash = GetStableStringHash(gateId);
                var combinedSeed = HashCombine(nodeSeed, (int)gateIdHash);
                var rng = new System.Random((int)combinedSeed);
                return rng.Next(rangeMin, rangeMax + 1);
            default:
                return 0;
        }
    }

    private static uint HashCombine(uint seed, int value)
    {
        unchecked
        {
            return (seed ^ (uint)value) * 16777619u;
        }
    }

    /// <summary>
    /// FNV-1a hash for strings. Stable across platforms and .NET versions.
    /// </summary>
    private static uint GetStableStringHash(string str)
    {
        if (string.IsNullOrEmpty(str)) return 0;

        unchecked
        {
            const uint fnvPrime = 16777619u;
            const uint fnvOffsetBasis = 2166136261u;

            var hash = fnvOffsetBasis;
            for (int i = 0; i < str.Length; i++)
            {
                hash ^= str[i];
                hash *= fnvPrime;
            }
            return hash;
        }
    }
}
