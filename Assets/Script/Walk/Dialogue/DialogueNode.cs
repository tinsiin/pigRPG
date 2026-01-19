using System;
using UnityEngine;

/// <summary>
/// Placeholder for future dialogue node.
/// Future: Support speaker, portrait, choices, conditions, effects.
/// Current: Simple text-only via MessageDropper.
/// </summary>
[Serializable]
public sealed class DialogueNode
{
    [SerializeField] private string nodeId;
    [SerializeField] private string speaker;
    [SerializeField] private Sprite portrait;
    [TextArea(3, 10)]
    [SerializeField] private string text;
    [SerializeField] private string nextNodeId;

    public string NodeId => nodeId;
    public string Speaker => speaker;
    public Sprite Portrait => portrait;
    public string Text => text;
    public string NextNodeId => nextNodeId;
    public bool HasNext => !string.IsNullOrEmpty(nextNodeId);
}
