using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 武器攻撃時のスラッシュ（斬撃線）アニメーション。
/// ViewportArea 上にランダム折れ線を6フレームで描画する。
/// anoyo LINE_ANIMATION_SPEC 準拠の太さベルカーブ「細→太→細」。
/// </summary>
public static class WeaponSlashAnimator
{
    private const int TexSize = 360;
    private const int FrameCount = 6;
    private const int PointCount = FrameCount + 1; // 7点で6セグメント

    /// <summary>
    /// 太さベルカーブ（anoyo準拠）。各フレームの (最小太さ, ランダム範囲)。
    /// 実太さ = min + Random.Range(0, range)
    /// </summary>
    private static readonly (int min, int range)[] ThicknessCurve =
    {
        (1, 3),   // frame 0: 1-3   (細い導入)
        (5, 8),   // frame 1: 5-12  (中太)
        (8, 13),  // frame 2: 8-20  (最太ピーク)
        (5, 9),   // frame 3: 5-13  (やや太い)
        (3, 6),   // frame 4: 3-8   (中細)
        (1, 3),   // frame 5: 1-3   (細い収束)
    };

    /// <summary>
    /// スラッシュアニメーションを再生（fire-and-forget）
    /// </summary>
    /// <param name="viewportParent">ViewportArea の RectTransform</param>
    /// <param name="targetLocalPositions">ターゲットの viewport 内ローカル座標リスト</param>
    /// <param name="slashColor">スラッシュの色（武器定義）</param>
    /// <param name="scale">表示スケール</param>
    /// <param name="speed">速度（1.0 = 最速＝1フレーム間隔、0.05 = 20フレーム間隔）</param>
    public static async UniTaskVoid PlayAsync(
        RectTransform viewportParent,
        List<Vector2> targetLocalPositions,
        Color slashColor,
        float scale = 1f,
        float speed = 1f)
    {
        if (viewportParent == null || targetLocalPositions == null || targetLocalPositions.Count == 0)
            return;

        var texture = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        // Color32バッファで描画し、Apply一回で反映（SetPixel逐次呼びよりAndroidで高速）
        var buffer = new Color32[TexSize * TexSize];
        var slashColor32 = (Color32)slashColor;
        var clear32 = new Color32(0, 0, 0, 0);

        // RawImage 用 GameObject を生成
        var go = new GameObject("WeaponSlash");
        go.transform.SetParent(viewportParent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 表示スケール適用（テクスチャ内容は変えず、RawImage自体を拡大）
        if (scale > 0f && !Mathf.Approximately(scale, 1f))
        {
            rt.localScale = new Vector3(scale, scale, 1f);
        }

        var rawImage = go.AddComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.raycastTarget = false;

        try
        {
            // ターゲット位置をテクスチャ座標（左下原点）に変換
            Rect vpRect = viewportParent.rect;
            var texTargets = new List<Vector2Int>(targetLocalPositions.Count);
            foreach (var pos in targetLocalPositions)
            {
                float nx = vpRect.width > 0 ? (pos.x - vpRect.xMin) / vpRect.width : 0.5f;
                float ny = vpRect.height > 0 ? (pos.y - vpRect.yMin) / vpRect.height : 0.5f;
                int tx = Mathf.Clamp(Mathf.RoundToInt(nx * (TexSize - 1)), 0, TexSize - 1);
                int ty = Mathf.Clamp(Mathf.RoundToInt(ny * (TexSize - 1)), 0, TexSize - 1);
                texTargets.Add(new Vector2Int(tx, ty));
            }

            // ターゲット群のバウンディングボックス
            int minX = TexSize, maxX = 0, minY = TexSize, maxY = 0;
            foreach (var t in texTargets)
            {
                if (t.x < minX) minX = t.x;
                if (t.x > maxX) maxX = t.x;
                if (t.y < minY) minY = t.y;
                if (t.y > maxY) maxY = t.y;
            }

            // パディング追加（テクスチャの20%、最小50px）
            int padding = Mathf.Max(50, TexSize / 5);
            minX = Mathf.Max(0, minX - padding);
            maxX = Mathf.Min(TexSize - 1, maxX + padding);
            minY = Mathf.Max(0, minY - padding);
            maxY = Mathf.Min(TexSize - 1, maxY + padding);

            // 7点のランダム折れ線を生成
            var points = new Vector2Int[PointCount];
            for (int i = 0; i < PointCount; i++)
            {
                points[i] = new Vector2Int(
                    Random.Range(minX, maxX + 1),
                    Random.Range(minY, maxY + 1)
                );
            }

            // 6フレームアニメーション
            for (int frame = 0; frame < FrameCount; frame++)
            {
                if (go == null) return; // シーン遷移等で破棄された場合

                // バッファクリア
                System.Array.Fill(buffer, clear32);

                // 太さ決定
                var (min, range) = ThicknessCurve[frame];
                int width = min + Random.Range(0, range);

                // 線描画 (points[frame] → points[frame+1])
                DrawThickLine(buffer,
                    points[frame].x, points[frame].y,
                    points[frame + 1].x, points[frame + 1].y,
                    slashColor32, width);

                texture.SetPixels32(buffer);
                texture.Apply();

                // speed=1.0 → 1フレーム待ち（最速）、speed=0.05 → 20フレーム待ち（最遅）
                int frameWait = Mathf.Max(1, Mathf.RoundToInt(1f / Mathf.Max(speed, 0.05f)));
                for (int w = 0; w < frameWait; w++)
                    await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }
        finally
        {
            if (go != null) Object.Destroy(go);
            if (texture != null) Object.Destroy(texture);
        }
    }

    /// <summary>
    /// Bresenham + 円充填による太線描画（Color32バッファ版）
    /// </summary>
    private static void DrawThickLine(Color32[] buf, int x1, int y1, int x2, int y2, Color32 color, int width)
    {
        int dx = Mathf.Abs(x2 - x1);
        int dy = Mathf.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;
        int radius = width / 2;

        while (true)
        {
            FillCircle(buf, x1, y1, radius, color);

            if (x1 == x2 && y1 == y2) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x1 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y1 += sy;
            }
        }
    }

    /// <summary>
    /// 塗りつぶし円（太線の各点を太くするために使用）
    /// </summary>
    private static void FillCircle(Color32[] buf, int cx, int cy, int radius, Color32 color)
    {
        if (radius <= 0)
        {
            SetPixelSafe(buf, cx, cy, color);
            return;
        }

        int r2 = radius * radius;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int maxDx = (int)Mathf.Sqrt(r2 - dy * dy);
            int py = cy + dy;
            if (py < 0 || py >= TexSize) continue;
            int rowOffset = py * TexSize;
            for (int dx = -maxDx; dx <= maxDx; dx++)
            {
                int px = cx + dx;
                if (px >= 0 && px < TexSize)
                    buf[rowOffset + px] = color;
            }
        }
    }

    /// <summary>
    /// 範囲チェック付きピクセル設定（Color32バッファ、左下原点座標）
    /// </summary>
    private static void SetPixelSafe(Color32[] buf, int x, int y, Color32 color)
    {
        if (x >= 0 && x < TexSize && y >= 0 && y < TexSize)
        {
            buf[y * TexSize + x] = color;
        }
    }
}
