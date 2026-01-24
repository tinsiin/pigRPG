using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// キャラクターの立ち絵とアイコンのデータベース。
/// </summary>
[CreateAssetMenu(menuName = "Walk/Portrait Database")]
public sealed class PortraitDatabase : ScriptableObject
{
    [SerializeField] private PortraitCharacterData[] characters;

    private Dictionary<string, PortraitCharacterData> characterMap;

    private void OnEnable()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        characterMap = new Dictionary<string, PortraitCharacterData>();
        if (characters == null) return;

        foreach (var character in characters)
        {
            if (character == null || string.IsNullOrEmpty(character.CharacterId)) continue;
            characterMap[character.CharacterId] = character;
        }
    }

    public Sprite GetIcon(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return null;
        if (characterMap == null) BuildMap();
        return characterMap.TryGetValue(characterId, out var data) ? data.Icon : null;
    }

    public Sprite GetPortrait(string characterId, string expression = null)
    {
        if (string.IsNullOrEmpty(characterId)) return null;
        if (characterMap == null) BuildMap();
        if (!characterMap.TryGetValue(characterId, out var data)) return null;

        if (string.IsNullOrEmpty(expression))
        {
            return data.DefaultPortrait;
        }

        return data.GetExpression(expression) ?? data.DefaultPortrait;
    }

    public Color GetThemeColor(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return Color.white;
        if (characterMap == null) BuildMap();
        return characterMap.TryGetValue(characterId, out var data) ? data.ThemeColor : Color.white;
    }
}

[Serializable]
public sealed class PortraitCharacterData
{
    [SerializeField] private string characterId;
    [SerializeField] private Sprite icon;
    [SerializeField] private Sprite defaultPortrait;
    [SerializeField] private Color themeColor = Color.white;
    [SerializeField] private PortraitExpressionData[] expressions;

    private Dictionary<string, Sprite> expressionMap;

    public string CharacterId => characterId;
    public Sprite Icon => icon;
    public Sprite DefaultPortrait => defaultPortrait;
    public Color ThemeColor => themeColor;

    public Sprite GetExpression(string expressionId)
    {
        if (string.IsNullOrEmpty(expressionId)) return null;

        if (expressionMap == null)
        {
            expressionMap = new Dictionary<string, Sprite>();
            if (expressions != null)
            {
                foreach (var expr in expressions)
                {
                    if (expr == null || string.IsNullOrEmpty(expr.ExpressionId)) continue;
                    expressionMap[expr.ExpressionId] = expr.Portrait;
                }
            }
        }

        return expressionMap.TryGetValue(expressionId, out var sprite) ? sprite : null;
    }
}

[Serializable]
public sealed class PortraitExpressionData
{
    [SerializeField] private string expressionId;
    [SerializeField] private Sprite portrait;

    public string ExpressionId => expressionId;
    public Sprite Portrait => portrait;
}
