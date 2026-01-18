using System;
using RandomExtensions;
using UnityEngine;

public enum ExitSpawnMode
{
    None,
    Steps,
    Probability
}

[Serializable]
public sealed class ExitSpawnRule
{
    [SerializeField] private ExitSpawnMode mode = ExitSpawnMode.Steps;
    [SerializeField] private int steps = 1;
    [SerializeField] private float rate = 1f;
    [SerializeField] private bool requireAllGatesCleared = true;

    public ExitSpawnMode Mode => mode;
    public int Steps => steps;
    public float Rate => rate;
    public bool RequireAllGatesCleared => requireAllGatesCleared;

    public bool ShouldSpawn(WalkCountersSnapshot nextCounters, bool allGatesCleared)
    {
        // If gates are required but not all cleared, don't spawn exit
        if (requireAllGatesCleared && !allGatesCleared)
            return false;

        switch (mode)
        {
            case ExitSpawnMode.Steps:
                if (steps <= 0) return true;
                return nextCounters.NodeSteps >= steps;
            case ExitSpawnMode.Probability:
                return RandomEx.Shared.NextFloat(0f, 1f) < Mathf.Clamp01(rate);
            default:
                return false;
        }
    }

    // Backward compatibility overload
    public bool ShouldSpawn(WalkCountersSnapshot nextCounters)
    {
        return ShouldSpawn(nextCounters, allGatesCleared: true);
    }
}