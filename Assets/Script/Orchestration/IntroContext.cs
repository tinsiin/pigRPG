using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IIntroContext のシンプル実装。必要な値を保持するだけのDTOです。
/// </summary>
public sealed class IntroContext : IIntroContext
{
    public string ScenarioName { get; private set; }
    public int PresetIndex { get; private set; }
    public string PresetSummary { get; private set; }
    public IReadOnlyDictionary<string, string> Tags { get; private set; }

    public RectTransform EnemySpawnArea { get; private set; }
    public RectTransform ZoomFrontContainer { get; private set; }
    public RectTransform ZoomBackContainer  { get; private set; }

    public Vector2 GotoScaleXY { get; private set; }
    public Vector2 GotoPos     { get; private set; }
    public float   ZoomDuration { get; private set; }
    public AnimationCurve ZoomCurve { get; private set; }

    public IntroContext(
        string scenarioName,
        int presetIndex,
        string presetSummary,
        IReadOnlyDictionary<string, string> tags,
        RectTransform enemySpawnArea,
        RectTransform zoomFrontContainer,
        RectTransform zoomBackContainer,
        Vector2 gotoScaleXY,
        Vector2 gotoPos,
        float zoomDuration,
        AnimationCurve zoomCurve)
    {
        ScenarioName = scenarioName ?? string.Empty;
        PresetIndex = presetIndex;
        PresetSummary = presetSummary ?? string.Empty;
        Tags = tags;
        EnemySpawnArea = enemySpawnArea;
        ZoomFrontContainer = zoomFrontContainer;
        ZoomBackContainer  = zoomBackContainer;
        GotoScaleXY = gotoScaleXY;
        GotoPos     = gotoPos;
        ZoomDuration = zoomDuration;
        ZoomCurve    = zoomCurve;
    }
}
