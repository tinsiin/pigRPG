using UnityEngine;

/// <summary>
/// Placeholder for future dialogue system.
/// Currently, use MessageDropper directly via ShowMessageEffect for simple messages.
/// </summary>
[CreateAssetMenu(menuName = "Walk/DialogueDefinition")]
public sealed class DialogueDefinitionSO : ScriptableObject
{
    [SerializeField] private string dialogueId;
    [SerializeField] private DialogueNode[] nodes;

    public string DialogueId => dialogueId;
    public DialogueNode[] Nodes => nodes;
}
