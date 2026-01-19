using System;
using UnityEngine;

[Serializable]
public struct ExitVisual
{
    [SerializeField] private Sprite sprite;
    [SerializeField] private Sprite backSprite;
    [SerializeField] private Vector2 size;
    [SerializeField] private Vector2 offset;
    [SerializeField] private Color tint;
    [SerializeField] private Color backTint;
    [SerializeField] private string label;
    [SerializeField] private string sfxOnAppear;

    public Sprite Sprite => sprite;
    public Sprite BackSprite => backSprite;
    public Vector2 Size => size;
    public Vector2 Offset => offset;
    public Color Tint => tint.a > 0f ? tint : Color.white;
    public Color BackTint => backTint.a > 0f ? backTint : Color.white;
    public string Label => label;
    public string SfxOnAppear => sfxOnAppear;
    public bool HasSprite => sprite != null;
    public bool HasBackSprite => backSprite != null;

    public GateVisual ToGateVisual()
    {
        return new GateVisual(
            sprite,
            size,
            offset,
            tint,
            backSprite,
            backTint,
            label);
    }
}
