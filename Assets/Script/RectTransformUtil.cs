using UnityEngine;

/// <summary>
/// RectTransformのワールド座標計算ユーティリティ。
/// 複数のズーム/フィット計算クラスで共用する。
/// </summary>
public static class RectTransformUtil
{
    private static Vector3[] s_corners;

    /// <summary>
    /// RectTransformのワールド座標での中心点とサイズを取得する。
    /// </summary>
    public static void GetWorldRect(RectTransform rt, out Vector2 center, out Vector2 size)
    {
        var corners = s_corners ??= new Vector3[4];
        rt.GetWorldCorners(corners);
        var min = new Vector2(corners[0].x, corners[0].y);
        var max = new Vector2(corners[2].x, corners[2].y);
        center = (min + max) * 0.5f;
        size = max - min;
    }
}
