using System;
using System.Collections.Generic;
using Effects.Core;
using UnityEngine;

namespace Effects.Rendering
{
    /// <summary>
    /// Texture2Dへの図形描画を行う静的クラス
    /// </summary>
    public static class ShapeDrawer
    {
        /// <summary>
        /// 現在のブレンドモード（EffectRendererがシェイプごとに設定・リセット）
        /// </summary>
        public static BlendMode CurrentBlendMode = BlendMode.Normal;

        #region Coordinate Helpers

        /// <summary>座標を四捨五入</summary>
        public static int RoundCoord(float v) => Mathf.RoundToInt(v);

        /// <summary>サイズを四捨五入（バリデーション用、最小値なし）</summary>
        public static int RoundSize(float v) => Mathf.RoundToInt(v);

        /// <summary>サイズを四捨五入（描画用、最小1）</summary>
        public static int RoundSizeMin1(float v) => Mathf.Max(1, Mathf.RoundToInt(v));

        /// <summary>線幅を切り上げ（最小1）</summary>
        public static int CeilWidth(float v) => Mathf.Max(1, Mathf.CeilToInt(v));

        /// <summary>
        /// エフェクト座標系（左上原点）からTexture2D座標系（左下原点）に変換
        /// </summary>
        public static int ToTexY(int y, int canvasSize) => (canvasSize - 1) - y;

        /// <summary>
        /// 角度を0-360の範囲に正規化
        /// </summary>
        public static float NormalizeAngle(float angle)
        {
            angle = angle % 360f;
            if (angle < 0) angle += 360f;
            return angle;
        }

        #endregion

        #region Pixel Operations

        /// <summary>
        /// 安全にピクセルを設定（範囲外は無視、アルファブレンド適用）
        /// </summary>
        public static void SetPixelSafe(Texture2D tex, int x, int y, Color color, int canvasSize)
        {
            // Texture2D座標に変換
            int texY = ToTexY(y, canvasSize);

            if (x >= 0 && x < canvasSize && texY >= 0 && texY < canvasSize)
            {
                if (CurrentBlendMode == BlendMode.Additive)
                {
                    // 加算ブレンド
                    Color dst = tex.GetPixel(x, texY);
                    color = EffectColorUtility.BlendPixelAdditive(dst, color);
                }
                else if (color.a < 1f)
                {
                    // アルファブレンド
                    Color dst = tex.GetPixel(x, texY);
                    color = EffectColorUtility.BlendPixel(dst, color);
                }
                tex.SetPixel(x, texY, color);
            }
        }

        /// <summary>
        /// 安全にピクセルを設定（アルファブレンドなし、完全上書き）
        /// </summary>
        public static void SetPixelSafeNoBlend(Texture2D tex, int x, int y, Color color, int canvasSize)
        {
            int texY = ToTexY(y, canvasSize);
            if (x >= 0 && x < canvasSize && texY >= 0 && texY < canvasSize)
            {
                tex.SetPixel(x, texY, color);
            }
        }

        #endregion

        #region Point

        /// <summary>
        /// 点を描画（penの色で塗りつぶし円として描画）
        /// </summary>
        public static void DrawPoint(Texture2D tex, float x, float y, float size, Color color, int canvasSize)
        {
            int iSize = RoundSize(size);
            if (iSize <= 0) return; // 無効なサイズはスキップ

            int cx = RoundCoord(x);
            int cy = RoundCoord(y);
            int radius = iSize / 2;

            // size=1の場合、radius=0なので1ピクセル描画
            if (radius <= 0)
            {
                SetPixelSafe(tex, cx, cy, color, canvasSize);
                return;
            }

            // 塗りつぶし円として描画
            FillCircleInternal(tex, cx, cy, radius, color, canvasSize);
        }

        #endregion

        #region Line

        /// <summary>
        /// 直線を描画（Bresenhamアルゴリズム + 太さ対応）
        /// </summary>
        public static void DrawLine(Texture2D tex, float x1, float y1, float x2, float y2, Color color, float width, int canvasSize)
        {
            int ix1 = RoundCoord(x1);
            int iy1 = RoundCoord(y1);
            int ix2 = RoundCoord(x2);
            int iy2 = RoundCoord(y2);
            int iWidth = CeilWidth(width);

            // 無効判定: 丸め後の始点と終点が同一座標
            if (ix1 == ix2 && iy1 == iy2) return;

            if (iWidth == 1)
            {
                DrawLineBresenham(tex, ix1, iy1, ix2, iy2, color, canvasSize);
            }
            else
            {
                DrawLineThick(tex, ix1, iy1, ix2, iy2, color, iWidth, canvasSize);
            }
        }

        private static void DrawLineBresenham(Texture2D tex, int x1, int y1, int x2, int y2, Color color, int canvasSize)
        {
            int dx = Mathf.Abs(x2 - x1);
            int dy = Mathf.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                SetPixelSafe(tex, x1, y1, color, canvasSize);

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

        private static void DrawLineThick(Texture2D tex, int x1, int y1, int x2, int y2, Color color, int width, int canvasSize)
        {
            int dx = Mathf.Abs(x2 - x1);
            int dy = Mathf.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;
            int radius = width / 2;

            while (true)
            {
                // 各点で円を描画して太さを表現
                FillCircleInternal(tex, x1, y1, radius, color, canvasSize);

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

        #endregion

        #region Circle

        /// <summary>
        /// 円の輪郭を描画
        /// </summary>
        public static void DrawCircle(Texture2D tex, float cx, float cy, float radius, Color color, float width, int canvasSize)
        {
            int icx = RoundCoord(cx);
            int icy = RoundCoord(cy);
            int iRadius = RoundSize(radius);
            int iWidth = CeilWidth(width);

            if (iRadius <= 0) return;

            if (iWidth == 1)
            {
                DrawCircleMidpoint(tex, icx, icy, iRadius, color, canvasSize);
            }
            else
            {
                // 太い輪郭: 内側と外側の半径で描画
                int outerR = iRadius + iWidth / 2;
                int innerR = iRadius - iWidth / 2;
                if (innerR < 0) innerR = 0;

                DrawCircleThick(tex, icx, icy, innerR, outerR, color, canvasSize);
            }
        }

        /// <summary>
        /// 円を塗りつぶす
        /// </summary>
        public static void FillCircle(Texture2D tex, float cx, float cy, float radius, Color color, int canvasSize)
        {
            int icx = RoundCoord(cx);
            int icy = RoundCoord(cy);
            int iRadius = RoundSize(radius);

            if (iRadius <= 0) return;

            FillCircleInternal(tex, icx, icy, iRadius, color, canvasSize);
        }

        private static void DrawCircleMidpoint(Texture2D tex, int cx, int cy, int radius, Color color, int canvasSize)
        {
            int x = radius;
            int y = 0;
            int radiusError = 1 - x;

            while (x >= y)
            {
                // 8分円の対称性を利用
                SetPixelSafe(tex, cx + x, cy + y, color, canvasSize);
                SetPixelSafe(tex, cx + y, cy + x, color, canvasSize);
                SetPixelSafe(tex, cx - y, cy + x, color, canvasSize);
                SetPixelSafe(tex, cx - x, cy + y, color, canvasSize);
                SetPixelSafe(tex, cx - x, cy - y, color, canvasSize);
                SetPixelSafe(tex, cx - y, cy - x, color, canvasSize);
                SetPixelSafe(tex, cx + y, cy - x, color, canvasSize);
                SetPixelSafe(tex, cx + x, cy - y, color, canvasSize);

                y++;
                if (radiusError < 0)
                {
                    radiusError += 2 * y + 1;
                }
                else
                {
                    x--;
                    radiusError += 2 * (y - x + 1);
                }
            }
        }

        private static void DrawCircleThick(Texture2D tex, int cx, int cy, int innerR, int outerR, Color color, int canvasSize)
        {
            for (int y = -outerR; y <= outerR; y++)
            {
                for (int x = -outerR; x <= outerR; x++)
                {
                    int distSq = x * x + y * y;
                    if (distSq >= innerR * innerR && distSq <= outerR * outerR)
                    {
                        SetPixelSafe(tex, cx + x, cy + y, color, canvasSize);
                    }
                }
            }
        }

        private static void FillCircleInternal(Texture2D tex, int cx, int cy, int radius, Color color, int canvasSize)
        {
            // スキャンライン法で塗りつぶし
            for (int y = -radius; y <= radius; y++)
            {
                int xSpan = (int)Mathf.Sqrt(radius * radius - y * y);
                for (int x = -xSpan; x <= xSpan; x++)
                {
                    SetPixelSafe(tex, cx + x, cy + y, color, canvasSize);
                }
            }
        }

        #endregion

        #region Ellipse

        /// <summary>
        /// 楕円の輪郭を描画
        /// </summary>
        public static void DrawEllipse(Texture2D tex, float cx, float cy, float rx, float ry, float rotation, Color color, float width, int canvasSize)
        {
            int icx = RoundCoord(cx);
            int icy = RoundCoord(cy);
            int irx = RoundSize(rx);
            int iry = RoundSize(ry);
            int iWidth = CeilWidth(width);

            if (irx <= 0 || iry <= 0) return;

            float rad = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            // 楕円上の点をパラメトリックに計算
            int segments = Mathf.Max(32, (irx + iry) * 2);
            float angleStep = 2f * Mathf.PI / segments;

            for (int i = 0; i < segments; i++)
            {
                float t1 = i * angleStep;
                float t2 = (i + 1) * angleStep;

                // 楕円上の点（回転前）
                float ex1 = irx * Mathf.Cos(t1);
                float ey1 = iry * Mathf.Sin(t1);
                float ex2 = irx * Mathf.Cos(t2);
                float ey2 = iry * Mathf.Sin(t2);

                // 回転適用
                float px1 = ex1 * cos - ey1 * sin + icx;
                float py1 = ex1 * sin + ey1 * cos + icy;
                float px2 = ex2 * cos - ey2 * sin + icx;
                float py2 = ex2 * sin + ey2 * cos + icy;

                DrawLine(tex, px1, py1, px2, py2, color, width, canvasSize);
            }
        }

        /// <summary>
        /// 楕円を塗りつぶす
        /// </summary>
        public static void FillEllipse(Texture2D tex, float cx, float cy, float rx, float ry, float rotation, Color color, int canvasSize)
        {
            int icx = RoundCoord(cx);
            int icy = RoundCoord(cy);
            int irx = RoundSize(rx);
            int iry = RoundSize(ry);

            if (irx <= 0 || iry <= 0) return;

            float rad = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            // バウンディングボックスを計算
            int bound = Mathf.Max(irx, iry) + 1;

            for (int py = -bound; py <= bound; py++)
            {
                for (int px = -bound; px <= bound; px++)
                {
                    // 逆回転して楕円の標準形で判定
                    float rx2 = px * cos + py * sin;
                    float ry2 = -px * sin + py * cos;

                    // 楕円の内部判定
                    float val = (rx2 * rx2) / (irx * irx) + (ry2 * ry2) / (iry * iry);
                    if (val <= 1f)
                    {
                        SetPixelSafe(tex, icx + px, icy + py, color, canvasSize);
                    }
                }
            }
        }

        #endregion

        #region Arc

        /// <summary>
        /// 弧を描画
        /// </summary>
        public static void DrawArc(Texture2D tex, float cx, float cy, float radius, float startAngle, float endAngle, Color color, float width, int canvasSize)
        {
            int icx = RoundCoord(cx);
            int icy = RoundCoord(cy);
            int iRadius = RoundSize(radius);
            int iWidth = CeilWidth(width);

            if (iRadius <= 0) return;

            // 角度を正規化
            startAngle = NormalizeAngle(startAngle);
            endAngle = NormalizeAngle(endAngle);

            // 角度範囲を計算（startAngle > endAngleの場合は360度をまたぐ）
            float angleRange;
            if (startAngle <= endAngle)
            {
                angleRange = endAngle - startAngle;
            }
            else
            {
                angleRange = (360f - startAngle) + endAngle;
            }

            // セグメント数を計算
            int segments = Mathf.Max(8, (int)(angleRange / 360f * iRadius * 4));
            float angleStep = angleRange / segments;

            for (int i = 0; i < segments; i++)
            {
                float a1 = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                float a2 = (startAngle + (i + 1) * angleStep) * Mathf.Deg2Rad;

                float px1 = icx + iRadius * Mathf.Cos(a1);
                float py1 = icy + iRadius * Mathf.Sin(a1);
                float px2 = icx + iRadius * Mathf.Cos(a2);
                float py2 = icy + iRadius * Mathf.Sin(a2);

                DrawLine(tex, px1, py1, px2, py2, color, width, canvasSize);
            }
        }

        #endregion

        #region Rect

        /// <summary>
        /// 矩形の輪郭を描画
        /// </summary>
        public static void DrawRect(Texture2D tex, float x, float y, float w, float h, float rotation, Color color, float width, int canvasSize)
        {
            int icx = RoundCoord(x);
            int icy = RoundCoord(y);
            int iw = RoundSize(w);
            int ih = RoundSize(h);

            if (iw <= 0 || ih <= 0) return;

            // 4つの頂点を計算（中心基準）
            float hw = iw / 2f;
            float hh = ih / 2f;

            Vector2[] corners = new Vector2[4]
            {
                new Vector2(-hw, -hh),
                new Vector2(hw, -hh),
                new Vector2(hw, hh),
                new Vector2(-hw, hh)
            };

            // 回転適用
            if (Mathf.Abs(rotation) > 0.001f)
            {
                float rad = rotation * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                for (int i = 0; i < 4; i++)
                {
                    float rx = corners[i].x * cos - corners[i].y * sin;
                    float ry = corners[i].x * sin + corners[i].y * cos;
                    corners[i] = new Vector2(rx, ry);
                }
            }

            // 中心座標を加算
            for (int i = 0; i < 4; i++)
            {
                corners[i] += new Vector2(icx, icy);
            }

            // 4辺を描画
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                DrawLine(tex, corners[i].x, corners[i].y, corners[next].x, corners[next].y, color, width, canvasSize);
            }
        }

        /// <summary>
        /// 矩形を塗りつぶす
        /// </summary>
        public static void FillRect(Texture2D tex, float x, float y, float w, float h, float rotation, Color color, int canvasSize)
        {
            int icx = RoundCoord(x);
            int icy = RoundCoord(y);
            int iw = RoundSize(w);
            int ih = RoundSize(h);

            if (iw <= 0 || ih <= 0) return;

            // 回転がない場合は高速パス
            if (Mathf.Abs(rotation) < 0.001f)
            {
                int left = icx - iw / 2;
                int top = icy - ih / 2;

                for (int py = 0; py < ih; py++)
                {
                    for (int px = 0; px < iw; px++)
                    {
                        SetPixelSafe(tex, left + px, top + py, color, canvasSize);
                    }
                }
                return;
            }

            // 回転がある場合は多角形として塗りつぶし
            float hw = iw / 2f;
            float hh = ih / 2f;

            Vector2[] corners = new Vector2[4]
            {
                new Vector2(-hw, -hh),
                new Vector2(hw, -hh),
                new Vector2(hw, hh),
                new Vector2(-hw, hh)
            };

            float rad = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            for (int i = 0; i < 4; i++)
            {
                float rx = corners[i].x * cos - corners[i].y * sin;
                float ry = corners[i].x * sin + corners[i].y * cos;
                corners[i] = new Vector2(rx + icx, ry + icy);
            }

            FillPolygonInternal(tex, corners, color, canvasSize);
        }

        #endregion

        #region Polygon

        /// <summary>
        /// 多角形の輪郭を描画
        /// </summary>
        public static void DrawPolygon(Texture2D tex, Vector2[] points, Color color, float width, int canvasSize)
        {
            if (points == null || points.Length < 3) return;

            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;
                DrawLine(tex, points[i].x, points[i].y, points[next].x, points[next].y, color, width, canvasSize);
            }
        }

        /// <summary>
        /// 多角形を塗りつぶす（偶奇規則）
        /// </summary>
        public static void FillPolygon(Texture2D tex, Vector2[] points, Color color, int canvasSize)
        {
            if (points == null || points.Length < 3) return;
            FillPolygonInternal(tex, points, color, canvasSize);
        }

        private static void FillPolygonInternal(Texture2D tex, Vector2[] points, Color color, int canvasSize)
        {
            // バウンディングボックスを計算
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var p in points)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            int startY = RoundCoord(minY);
            int endY = RoundCoord(maxY);

            // スキャンライン法（偶奇規則）
            for (int y = startY; y <= endY; y++)
            {
                List<float> intersections = new List<float>();

                for (int i = 0; i < points.Length; i++)
                {
                    int j = (i + 1) % points.Length;
                    Vector2 p1 = points[i];
                    Vector2 p2 = points[j];

                    // 辺がスキャンラインと交差するか
                    if ((p1.y <= y && p2.y > y) || (p2.y <= y && p1.y > y))
                    {
                        float t = (y - p1.y) / (p2.y - p1.y);
                        float x = p1.x + t * (p2.x - p1.x);
                        intersections.Add(x);
                    }
                }

                // 交点をソート
                intersections.Sort();

                // 偶奇規則で塗りつぶし
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    int startX = RoundCoord(intersections[i]);
                    int endX = RoundCoord(intersections[i + 1]);

                    for (int x = startX; x <= endX; x++)
                    {
                        SetPixelSafe(tex, x, y, color, canvasSize);
                    }
                }
            }
        }

        #endregion

        #region Bezier

        /// <summary>
        /// ベジェ曲線を描画
        /// </summary>
        public static void DrawBezier(Texture2D tex, Vector2[] controlPoints, Color color, float width, int canvasSize)
        {
            if (controlPoints == null || controlPoints.Length < 3) return;

            // セグメント数を決定（制御点間の距離に基づく）
            float totalLength = 0f;
            for (int i = 0; i < controlPoints.Length - 1; i++)
            {
                totalLength += Vector2.Distance(controlPoints[i], controlPoints[i + 1]);
            }
            int segments = Mathf.Max(8, (int)(totalLength / 2));

            Vector2 prevPoint = EvaluateBezier(controlPoints, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector2 point = EvaluateBezier(controlPoints, t);
                DrawLine(tex, prevPoint.x, prevPoint.y, point.x, point.y, color, width, canvasSize);
                prevPoint = point;
            }
        }

        /// <summary>
        /// De Casteljauアルゴリズムでベジェ曲線上の点を計算
        /// </summary>
        private static Vector2 EvaluateBezier(Vector2[] points, float t)
        {
            Vector2[] temp = new Vector2[points.Length];
            Array.Copy(points, temp, points.Length);

            int n = temp.Length;
            while (n > 1)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    temp[i] = Vector2.Lerp(temp[i], temp[i + 1], t);
                }
                n--;
            }

            return temp[0];
        }

        #endregion

        #region Brush-aware Fills

        /// <summary>
        /// グラデーションブラシで円を塗りつぶす
        /// </summary>
        public static void FillCircleWithBrush(Texture2D tex, float cx, float cy, float radius, BrushContext brush, int canvasSize)
        {
            int icx = RoundCoord(cx);
            int icy = RoundCoord(cy);
            int iRadius = RoundSize(radius);
            if (iRadius <= 0) return;

            for (int y = -iRadius; y <= iRadius; y++)
            {
                int xSpan = (int)Mathf.Sqrt(iRadius * iRadius - y * y);
                for (int x = -xSpan; x <= xSpan; x++)
                {
                    Color color = brush.GetColorAt(icx + x, icy + y);
                    SetPixelSafe(tex, icx + x, icy + y, color, canvasSize);
                }
            }
        }

        /// <summary>
        /// グラデーションブラシで楕円を塗りつぶす
        /// </summary>
        public static void FillEllipseWithBrush(Texture2D tex, float cx, float cy, float rx, float ry, float rotation, BrushContext brush, int canvasSize)
        {
            int icx = RoundCoord(cx);
            int icy = RoundCoord(cy);
            int irx = RoundSize(rx);
            int iry = RoundSize(ry);
            if (irx <= 0 || iry <= 0) return;

            float rad = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            int bound = Mathf.Max(irx, iry) + 1;

            for (int py = -bound; py <= bound; py++)
            {
                for (int px = -bound; px <= bound; px++)
                {
                    float rx2 = px * cos + py * sin;
                    float ry2 = -px * sin + py * cos;
                    float val = (rx2 * rx2) / (irx * irx) + (ry2 * ry2) / (iry * iry);
                    if (val <= 1f)
                    {
                        Color color = brush.GetColorAt(icx + px, icy + py);
                        SetPixelSafe(tex, icx + px, icy + py, color, canvasSize);
                    }
                }
            }
        }

        /// <summary>
        /// グラデーションブラシで矩形を塗りつぶす
        /// </summary>
        public static void FillRectWithBrush(Texture2D tex, float x, float y, float w, float h, float rotation, BrushContext brush, int canvasSize)
        {
            int icx = RoundCoord(x);
            int icy = RoundCoord(y);
            int iw = RoundSize(w);
            int ih = RoundSize(h);
            if (iw <= 0 || ih <= 0) return;

            if (Mathf.Abs(rotation) < 0.001f)
            {
                int left = icx - iw / 2;
                int top = icy - ih / 2;
                for (int py = 0; py < ih; py++)
                {
                    for (int px = 0; px < iw; px++)
                    {
                        int worldX = left + px;
                        int worldY = top + py;
                        Color color = brush.GetColorAt(worldX, worldY);
                        SetPixelSafe(tex, worldX, worldY, color, canvasSize);
                    }
                }
                return;
            }

            float hw = iw / 2f;
            float hh = ih / 2f;
            Vector2[] corners = new Vector2[4]
            {
                new Vector2(-hw, -hh),
                new Vector2(hw, -hh),
                new Vector2(hw, hh),
                new Vector2(-hw, hh)
            };

            float rad = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            for (int i = 0; i < 4; i++)
            {
                float rx = corners[i].x * cos - corners[i].y * sin;
                float ry = corners[i].x * sin + corners[i].y * cos;
                corners[i] = new Vector2(rx + icx, ry + icy);
            }

            FillPolygonWithBrush(tex, corners, brush, canvasSize);
        }

        /// <summary>
        /// グラデーションブラシで多角形を塗りつぶす（偶奇規則）
        /// </summary>
        public static void FillPolygonWithBrush(Texture2D tex, Vector2[] points, BrushContext brush, int canvasSize)
        {
            if (points == null || points.Length < 3) return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in points)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            int startY = RoundCoord(minY);
            int endY = RoundCoord(maxY);

            for (int y = startY; y <= endY; y++)
            {
                List<float> intersections = new List<float>();
                for (int i = 0; i < points.Length; i++)
                {
                    int j = (i + 1) % points.Length;
                    Vector2 p1 = points[i];
                    Vector2 p2 = points[j];
                    if ((p1.y <= y && p2.y > y) || (p2.y <= y && p1.y > y))
                    {
                        float t = (y - p1.y) / (p2.y - p1.y);
                        float ix = p1.x + t * (p2.x - p1.x);
                        intersections.Add(ix);
                    }
                }
                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    int startX = RoundCoord(intersections[i]);
                    int endX = RoundCoord(intersections[i + 1]);
                    for (int ix = startX; ix <= endX; ix++)
                    {
                        Color color = brush.GetColorAt(ix, y);
                        SetPixelSafe(tex, ix, y, color, canvasSize);
                    }
                }
            }
        }

        #endregion

        #region TaperedLine

        /// <summary>
        /// テーパーライン（先細り線）を描画
        /// </summary>
        public static void DrawTaperedLine(Texture2D tex, float x1, float y1, float x2, float y2,
            float widthStart, float widthEnd, Color color, int canvasSize)
        {
            int ix1 = RoundCoord(x1);
            int iy1 = RoundCoord(y1);
            int ix2 = RoundCoord(x2);
            int iy2 = RoundCoord(y2);
            if (ix1 == ix2 && iy1 == iy2) return;

            int dx = Mathf.Abs(ix2 - ix1);
            int dy = Mathf.Abs(iy2 - iy1);
            int sx = ix1 < ix2 ? 1 : -1;
            int sy = iy1 < iy2 ? 1 : -1;
            int err = dx - dy;
            int steps = Mathf.Max(dx, dy);
            int step = 0;
            int cx = ix1, cy = iy1;

            while (true)
            {
                float t = steps > 0 ? (float)step / steps : 0f;
                float w = Mathf.Lerp(widthStart, widthEnd, t);
                int radius = Mathf.Max(0, Mathf.RoundToInt(w / 2f));

                if (radius <= 0)
                {
                    SetPixelSafe(tex, cx, cy, color, canvasSize);
                }
                else
                {
                    FillCircleInternal(tex, cx, cy, radius, color, canvasSize);
                }

                if (cx == ix2 && cy == iy2) break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; cx += sx; }
                if (e2 < dx) { err += dx; cy += sy; }
                step++;
            }
        }

        #endregion

        #region Ring

        /// <summary>
        /// リング（ドーナツ形状）を塗りつぶす
        /// </summary>
        public static void FillRing(Texture2D tex, float cx, float cy, float innerRadius, float outerRadius,
            Color color, int canvasSize)
        {
            int icx = RoundCoord(cx);
            int icy = RoundCoord(cy);
            int iInner = RoundSize(innerRadius);
            int iOuter = RoundSize(outerRadius);
            if (iOuter <= 0 || iInner >= iOuter) return;

            int innerSq = iInner * iInner;
            int outerSq = iOuter * iOuter;

            for (int y = -iOuter; y <= iOuter; y++)
            {
                for (int x = -iOuter; x <= iOuter; x++)
                {
                    int distSq = x * x + y * y;
                    if (distSq >= innerSq && distSq <= outerSq)
                    {
                        SetPixelSafe(tex, icx + x, icy + y, color, canvasSize);
                    }
                }
            }
        }

        /// <summary>
        /// グラデーションブラシでリングを塗りつぶす
        /// </summary>
        public static void FillRingWithBrush(Texture2D tex, float cx, float cy, float innerRadius, float outerRadius,
            BrushContext brush, int canvasSize)
        {
            int icx = RoundCoord(cx);
            int icy = RoundCoord(cy);
            int iInner = RoundSize(innerRadius);
            int iOuter = RoundSize(outerRadius);
            if (iOuter <= 0 || iInner >= iOuter) return;

            int innerSq = iInner * iInner;
            int outerSq = iOuter * iOuter;

            for (int y = -iOuter; y <= iOuter; y++)
            {
                for (int x = -iOuter; x <= iOuter; x++)
                {
                    int distSq = x * x + y * y;
                    if (distSq >= innerSq && distSq <= outerSq)
                    {
                        Color color = brush.GetColorAt(icx + x, icy + y);
                        SetPixelSafe(tex, icx + x, icy + y, color, canvasSize);
                    }
                }
            }
        }

        /// <summary>
        /// リングの輪郭を描画
        /// </summary>
        public static void DrawRing(Texture2D tex, float cx, float cy, float innerRadius, float outerRadius,
            Color color, float penWidth, int canvasSize)
        {
            int icx = RoundCoord(cx);
            int icy = RoundCoord(cy);
            int iOuter = RoundSize(outerRadius);
            int iInner = RoundSize(innerRadius);
            int iWidth = CeilWidth(penWidth);

            if (iOuter > 0)
            {
                if (iWidth <= 1)
                    DrawCircleMidpoint(tex, icx, icy, iOuter, color, canvasSize);
                else
                {
                    int outerR = iOuter + iWidth / 2;
                    int innerR = iOuter - iWidth / 2;
                    if (innerR < 0) innerR = 0;
                    DrawCircleThick(tex, icx, icy, innerR, outerR, color, canvasSize);
                }
            }
            if (iInner > 0)
            {
                if (iWidth <= 1)
                    DrawCircleMidpoint(tex, icx, icy, iInner, color, canvasSize);
                else
                {
                    int outerR = iInner + iWidth / 2;
                    int innerR = iInner - iWidth / 2;
                    if (innerR < 0) innerR = 0;
                    DrawCircleThick(tex, icx, icy, innerR, outerR, color, canvasSize);
                }
            }
        }

        #endregion

        #region Emitter

        /// <summary>
        /// パーティクルエミッタを描画（決定的乱数でシミュレーション）
        /// </summary>
        public static void DrawEmitter(Texture2D tex, ShapeDefinition shape, int frameIndex, int canvasSize)
        {
            // KfxCompilerが設定したローカルフレームがあればそちらを使う
            int simFrame = shape.EmitterLocalFrame >= 0 ? shape.EmitterLocalFrame : frameIndex;

            int count = Mathf.Max(1, shape.Count);

            float minAngle = 0f, maxAngle = 360f;
            if (shape.AngleRange != null && shape.AngleRange.Count >= 2)
            {
                minAngle = shape.AngleRange[0];
                maxAngle = shape.AngleRange[1];
            }

            float minSpeed = 0.5f, maxSpeed = 3f;
            if (shape.SpeedRange != null && shape.SpeedRange.Count >= 2)
            {
                minSpeed = shape.SpeedRange[0];
                maxSpeed = shape.SpeedRange[1];
            }

            float minSize = 1f, maxSize = 3f;
            if (shape.SizeRange != null && shape.SizeRange.Count >= 2)
            {
                minSize = shape.SizeRange[0];
                maxSize = shape.SizeRange[1];
            }

            float gravity = shape.Gravity;
            float drag = shape.Drag;
            int lifetime = Mathf.Max(1, shape.Lifetime);

            Color colorStart = !string.IsNullOrEmpty(shape.ColorStart)
                ? EffectColorUtility.ParseColor(shape.ColorStart)
                : Color.white;
            Color colorEnd = !string.IsNullOrEmpty(shape.ColorEnd)
                ? EffectColorUtility.ParseColor(shape.ColorEnd)
                : new Color(1f, 1f, 1f, 0f);

            var rng = new System.Random(shape.Seed);

            for (int i = 0; i < count; i++)
            {
                // パーティクルパラメータを決定的に生成
                float angle = Mathf.Lerp(minAngle, maxAngle, (float)rng.NextDouble()) * Mathf.Deg2Rad;
                float speed = Mathf.Lerp(minSpeed, maxSpeed, (float)rng.NextDouble());
                float size = Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble());

                float vx = Mathf.Cos(angle) * speed;
                float vy = Mathf.Sin(angle) * speed;

                // lifetime超過は描画しない
                if (simFrame >= lifetime) continue;

                // simFrame分シミュレーション
                float px = shape.X;
                float py = shape.Y;
                for (int f = 0; f < simFrame; f++)
                {
                    px += vx;
                    py += vy;
                    vy += gravity;
                    vx *= drag;
                    vy *= drag;
                }

                float lifeT = (float)simFrame / lifetime;
                Color color = Color.Lerp(colorStart, colorEnd, lifeT);
                float currentSize = Mathf.Lerp(size, size * 0.3f, lifeT);

                int iSize = Mathf.Max(1, RoundSize(currentSize));
                int radius = iSize / 2;

                if (radius <= 0)
                {
                    SetPixelSafe(tex, RoundCoord(px), RoundCoord(py), color, canvasSize);
                }
                else
                {
                    FillCircleInternal(tex, RoundCoord(px), RoundCoord(py), radius, color, canvasSize);
                }
            }
        }

        #endregion
    }
}
