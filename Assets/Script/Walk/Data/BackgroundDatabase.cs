using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背景画像のデータベース。
/// </summary>
[CreateAssetMenu(menuName = "Walk/Background Database")]
public sealed class BackgroundDatabase : ScriptableObject
{
    [SerializeField] private BackgroundData[] backgrounds;

    private Dictionary<string, BackgroundData> backgroundMap;

    private void OnEnable()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        backgroundMap = new Dictionary<string, BackgroundData>();
        if (backgrounds == null) return;

        foreach (var bg in backgrounds)
        {
            if (bg == null || string.IsNullOrEmpty(bg.BackgroundId)) continue;
            backgroundMap[bg.BackgroundId] = bg;
        }
    }

    public Sprite GetBackground(string backgroundId)
    {
        if (string.IsNullOrEmpty(backgroundId)) return null;
        if (backgroundMap == null) BuildMap();
        return backgroundMap.TryGetValue(backgroundId, out var data) ? data.Sprite : null;
    }

    public BackgroundData GetBackgroundData(string backgroundId)
    {
        if (string.IsNullOrEmpty(backgroundId)) return null;
        if (backgroundMap == null) BuildMap();
        return backgroundMap.TryGetValue(backgroundId, out var data) ? data : null;
    }
}

[Serializable]
public sealed class BackgroundData
{
    [SerializeField] private string backgroundId;
    [SerializeField] private Sprite sprite;
    [SerializeField] private Color tint = Color.white;

    public string BackgroundId => backgroundId;
    public Sprite Sprite => sprite;
    public Color Tint => tint;
}
