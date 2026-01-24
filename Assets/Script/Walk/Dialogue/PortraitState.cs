using System;
using UnityEngine;

/// <summary>
/// 立ち絵の状態。
/// </summary>
[Serializable]
public sealed class PortraitState
{
    [SerializeField] private string characterId;
    [SerializeField] private string expression;
    [SerializeField] private Sprite portraitSprite;
    [SerializeField] private PortraitTransition transitionType;

    public string CharacterId => characterId;
    public string Expression => expression;
    public Sprite PortraitSprite => portraitSprite;
    public PortraitTransition TransitionType => transitionType;

    public PortraitState() { }

    public PortraitState(string characterId, string expression, PortraitTransition transition = PortraitTransition.None)
    {
        this.characterId = characterId;
        this.expression = expression;
        this.transitionType = transition;
    }

    /// <summary>
    /// 状態をクローンする。
    /// </summary>
    public PortraitState Clone()
    {
        return new PortraitState
        {
            characterId = this.characterId,
            expression = this.expression,
            portraitSprite = this.portraitSprite,
            transitionType = this.transitionType
        };
    }
}
