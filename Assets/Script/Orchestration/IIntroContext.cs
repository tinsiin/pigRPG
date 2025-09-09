using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 導入処理（準備/演出/配置）で必要となる最小限の文脈情報。
/// WatchUIUpdate から提供される参照やラベル（Scenario/Preset/Tags）を外部化します。
/// </summary>
public interface IIntroContext
{
    // 論理情報
    string ScenarioName { get; }
    int PresetIndex { get; }            // 0-based index
    string PresetSummary { get; }
    IReadOnlyDictionary<string, string> Tags { get; }

    // UI/Transform 参照
    RectTransform EnemySpawnArea { get; }
    RectTransform ZoomFrontContainer { get; }
    RectTransform ZoomBackContainer { get; }

    // Zoom パラメータ（WatchUI の現在値を受け渡し）
    Vector2 GotoScaleXY { get; }
    Vector2 GotoPos { get; }
    float ZoomDuration { get; }
    AnimationCurve ZoomCurve { get; }
}
