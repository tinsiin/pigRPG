using System;
using UnityEngine;

/// <summary>
/// ノベルパートの選択肢。
/// </summary>
[Serializable]
public sealed class DialogueChoice
{
    [SerializeField] private string text;
    [SerializeField] private string spiritProperty;
    [SerializeField] private EffectSO[] effects;

    public string Text => text;
    public string SpiritProperty => spiritProperty;
    public EffectSO[] Effects => effects;

    public DialogueChoice() { }

    public DialogueChoice(string text, string spiritProperty = null)
    {
        this.text = text;
        this.spiritProperty = spiritProperty;
    }
}
