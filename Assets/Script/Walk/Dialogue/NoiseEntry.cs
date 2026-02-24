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

    [Header("立ち絵連動")]
    [Tooltip("この雑音の発話時に、話者の立ち絵を一時的にこの表情に変更する。空なら変更なし。")]
    [SerializeField] private string expression;

    public string Speaker => speaker;
    public string Text => text;
    public float DelaySeconds => delaySeconds;
    public float SpeedMultiplier => speedMultiplier;
    public float VerticalOffset => verticalOffset;
    public string Expression => expression;

    public NoiseEntry() { }

    public NoiseEntry(string speaker, string text, float delay = 0f, float speedMult = 1f, float vOffset = 0f, string expression = null)
    {
        this.speaker = speaker;
        this.text = text;
        this.delaySeconds = delay;
        this.speedMultiplier = speedMult;
        this.verticalOffset = vOffset;
        this.expression = expression;
    }
}
