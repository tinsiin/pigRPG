using System;
using UnityEngine;

[Serializable]
public sealed class TrackConfig
{
    [SerializeField] private int length = 100;
    [SerializeField] private int stepDelta = 1;
    [SerializeField] private string progressKey;

    public int Length => length;
    public int StepDelta => stepDelta;
    public string ProgressKey => progressKey;
    public bool HasConfig => length > 0;
    public bool HasProgressKey => !string.IsNullOrEmpty(progressKey);
}
