using System;
using UnityEngine;

/// <summary>
/// 雑音（弾幕コメント風に流れるテキスト）の1件分。
/// </summary>
[Serializable]
public sealed class NoiseEntry
{
    [SerializeField] private string speaker;
    [SerializeField] private string text;

    [Header("再生ルール")]
    [SerializeField] private float delaySeconds;
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField] private float verticalOffset;

    public string Speaker => speaker;
    public string Text => text;
    public float DelaySeconds => delaySeconds;
    public float SpeedMultiplier => speedMultiplier;
    public float VerticalOffset => verticalOffset;

    public NoiseEntry() { }

    public NoiseEntry(string speaker, string text, float delay = 0f, float speedMult = 1f, float vOffset = 0f)
    {
        this.speaker = speaker;
        this.text = text;
        this.delaySeconds = delay;
        this.speedMultiplier = speedMult;
        this.verticalOffset = vOffset;
    }
}
