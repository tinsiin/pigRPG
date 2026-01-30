using System;
using UnityEngine;

/// <summary>
/// ノベルパートの1ステップ。
/// 背景、立ち絵、テキスト、雑音、選択肢、Effectを含む。
/// </summary>
[Serializable]
public sealed class DialogueStep
{
    [Header("基本")]
    [SerializeField] private string speaker;
    [TextArea(3, 10)]
    [SerializeField] private string text;

    [Header("表示モード")]
    [SerializeField] private DisplayMode displayMode;

    [Header("立ち絵")]
    [SerializeField] private PortraitState leftPortrait;
    [SerializeField] private PortraitState rightPortrait;

    [Header("背景")]
    [SerializeField] private bool hasBackground;
    [SerializeField] private string backgroundId;

    [Header("雑音")]
    [SerializeField] private NoiseEntry[] noises;

    [Header("選択肢")]
    [SerializeField] private DialogueChoice[] choices;

    [Header("Effect")]
    [SerializeField] private EffectSO[] effects;

    [Header("リアクション")]
    [SerializeField] private ReactionSegment[] reactions;

    [Header("中央オブジェクト")]
    [SerializeField] private Sprite centralObjectSprite;

    public string Speaker => speaker;
    public string Text => text;
    public DisplayMode DisplayMode => displayMode;
    public PortraitState LeftPortrait => leftPortrait;
    public PortraitState RightPortrait => rightPortrait;
    public bool HasBackground => hasBackground;
    public string BackgroundId => backgroundId;
    public NoiseEntry[] Noises => noises;
    public DialogueChoice[] Choices => choices;
    public EffectSO[] Effects => effects;

    public bool HasChoices => choices != null && choices.Length > 0;
    public bool HasNoises => noises != null && noises.Length > 0;
    public bool HasEffects => effects != null && effects.Length > 0;
    public ReactionSegment[] Reactions => reactions;
    public bool HasReactions => reactions != null && reactions.Length > 0;

    public Sprite CentralObjectSprite => centralObjectSprite;
    public bool HasCentralObjectChange => centralObjectSprite != null;
}
