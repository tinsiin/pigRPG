using System;
using UnityEngine;

[Serializable]
public sealed class ExitCandidate
{
    [SerializeField] private string id;
    [SerializeField] private string toNodeId;
    [SerializeField] private string uiLabel;

    public string Id => id;
    public string ToNodeId => toNodeId;
    public string UILabel => uiLabel;
}