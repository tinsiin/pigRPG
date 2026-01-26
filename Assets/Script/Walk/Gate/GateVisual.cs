using System;
using UnityEngine;

[Serializable]
public struct GateVisual
{
    [SerializeField] private Sprite sprite;
    [SerializeField] private Vector2 size;
    [SerializeField] private Vector2 offset;
    [SerializeField] private Color tint;

    [SerializeField] private Sprite backSprite;
    [SerializeField] private Color backTint;
    [SerializeField] private Vector2 backOffset;
    [SerializeField] private Vector2 backSize;

    [SerializeField] private string label;

    [SerializeField] private GateAppearAnimation appearAnim;
    [SerializeField] private GateHideAnimation hideAnim;
    [SerializeField] private string sfxOnAppear;
    [SerializeField] private string sfxOnPass;
    [SerializeField] private string sfxOnFail;

    [Header("Zoom Focus")]
    [SerializeField] private FocusArea focusArea;

    public Sprite Sprite => sprite;
    public Vector2 Size => size;
    public Vector2 Offset => offset;
    public Color Tint => tint.a > 0f ? tint : Color.white;
    public bool HasSprite => sprite != null;

    public Sprite BackSprite => backSprite;
    public Color BackTint => backTint.a > 0f ? backTint : Color.white;
    public Vector2 BackOffset => backOffset;
    public Vector2 BackSize => backSize;
    public bool HasBackSprite => backSprite != null;

    public string Label => label;
    public GateAppearAnimation AppearAnim => appearAnim;
    public GateHideAnimation HideAnim => hideAnim;
    public string SfxOnAppear => sfxOnAppear;
    public string SfxOnPass => sfxOnPass;
    public string SfxOnFail => sfxOnFail;

    /// <summary>
    /// ズーム時のフォーカス領域。
    /// </summary>
    public FocusArea FocusArea => focusArea;

    public GateVisual(
        Sprite sprite,
        Vector2 size,
        Vector2 offset,
        Color tint,
        Sprite backSprite,
        Color backTint,
        string label)
    {
        this.sprite = sprite;
        this.size = size;
        this.offset = offset;
        this.tint = tint;
        this.backSprite = backSprite;
        this.backTint = backTint;
        this.backOffset = Vector2.zero;
        this.backSize = Vector2.zero;
        this.label = label;
        this.appearAnim = GateAppearAnimation.None;
        this.hideAnim = GateHideAnimation.None;
        this.sfxOnAppear = null;
        this.sfxOnPass = null;
        this.sfxOnFail = null;
        this.focusArea = FocusArea.Default;
    }
}

public enum GateAppearAnimation
{
    None,
    FadeIn,
    ScaleUp,
    SlideFromTop
}

public enum GateHideAnimation
{
    None,
    FadeOut,
    ScaleDown,
    Explode
}
