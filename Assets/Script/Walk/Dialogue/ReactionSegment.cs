using System;
using UnityEngine;

/// <summary>
/// リアクションの種類。
/// </summary>
public enum ReactionType
{
    Battle,     // 戦闘起動
    // 将来拡張用
    // Event,   // EventDefinitionSO発火
    // Custom,  // カスタム処理
}

/// <summary>
/// リアクション可能なテキストセグメント。
/// 1つのセリフ内に複数配置可能。
/// </summary>
[Serializable]
public sealed class ReactionSegment
{
    [Header("表示")]
    [SerializeField] private string text;           // リアクション可能な文字列
    [SerializeField] private Color color = new Color(1f, 0.5f, 0f); // デフォルト: オレンジ
    [SerializeField] private int startIndex;        // 本文中の開始位置

    [Header("発火内容")]
    [SerializeField] private ReactionType type;     // リアクションの種類
    [SerializeField] private EncounterSO encounter; // 戦闘用（type=Battle時）

    public string Text => text;
    public Color Color => color;
    public int StartIndex => startIndex;
    public ReactionType Type => type;
    public EncounterSO Encounter => encounter;

    /// <summary>
    /// 本文中の終了位置（開始位置 + テキスト長）。
    /// </summary>
    public int EndIndex => startIndex + (text?.Length ?? 0);

    public ReactionSegment() { }

    public ReactionSegment(string text, int startIndex, ReactionType type, Color color)
    {
        this.text = text;
        this.startIndex = startIndex;
        this.type = type;
        this.color = color;
    }

    public ReactionSegment(string text, int startIndex, EncounterSO encounter)
    {
        this.text = text;
        this.startIndex = startIndex;
        this.type = ReactionType.Battle;
        this.encounter = encounter;
        this.color = new Color(1f, 0.5f, 0f); // オレンジ
    }
}
