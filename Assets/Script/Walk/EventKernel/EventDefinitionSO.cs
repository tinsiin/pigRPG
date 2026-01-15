using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Event Definition")]
public sealed class EventDefinitionSO : ScriptableObject
{
    [SerializeField] private EventStep[] steps;

    public EventStep[] Steps => steps;
}

[Serializable]
public sealed class EventStep
{
    [TextArea(2, 6)]
    [SerializeField] private string message;
    [SerializeField] private EventChoice[] choices;
    [SerializeField] private EffectSO[] effects;

    public string Message => message;
    public EventChoice[] Choices => choices;
    public EffectSO[] Effects => effects;
}

[Serializable]
public sealed class EventChoice
{
    [SerializeField] private string label;
    [SerializeField] private EffectSO[] effects;

    public string Label => label;
    public EffectSO[] Effects => effects;
}